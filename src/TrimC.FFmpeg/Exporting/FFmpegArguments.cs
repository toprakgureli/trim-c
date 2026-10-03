// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TrimC.Export;
using TrimC.Media;

namespace TrimC.FFmpeg.Exporting
{
    /// <summary>
    /// Builds ffmpeg argument lists for export steps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Commands copy packets with <c>-c copy</c> wherever they can, which is what makes the export lossless. Encoders run
    /// only for the frames around an exact cut point and for the silence of a muted segment that is merged with audible
    /// ones. The commands share a common prologue that makes ffmpeg non-interactive, silent on standard
    /// error except for real errors, and emit machine readable progress on standard output.
    /// </para>
    /// <para>
    /// Existing files are never overwritten (<c>-n</c>). The planner guarantees unique names, so a collision indicates
    /// that another process created the file in the meantime, and failing is safer than destroying it.
    /// </para>
    /// </remarks>
    internal static class FFmpegArguments
    {
        /// <summary>
        /// Builds the arguments for a <see cref="CopyRangeStep"/>.
        /// </summary>
        /// <param name="step">The step to translate.</param>
        /// <returns>The argument list.</returns>
        public static List<string> ForCopyRange(CopyRangeStep step)
        {
            ArgumentNullException.ThrowIfNull(step);

            List<string> arguments = CreatePrologue();

            // -ss before -i performs a fast demuxer-level seek, and stream copy then starts at the keyframe at or before
            // the seek position. The planner places that position inside the GOP of the aligned start (see
            // CopyRangeStep.SeekPosition), so copying begins exactly at the range start.
            if (step.TimestampOffset is TimeSpan offset)
            {
                // Timeline-preserving parts keep source timestamps (-copyts). Output-side limits would then be measured
                // from zero rather than from the seek position, so the duration is applied while reading the input.
                arguments.Add("-copyts");
                arguments.AddRange(["-ss", FormatSeconds(step.SeekPosition)]);
                arguments.AddRange(["-t", FormatSeconds(step.Range.End - step.SeekPosition)]);
                arguments.AddRange(["-i", step.SourcePath]);

                // A timeline-preserving part is either copied or, for a muted segment, made entirely of audio decoded from
                // the same input and silenced. The accurate seek then starts it exactly at the range start, with the
                // source timestamps that -copyts preserves.
                AddStreamCopy(arguments, step, generatesSilence: false);
                arguments.AddRange(["-output_ts_offset", FormatSignedSeconds(offset)]);
            }
            else
            {
                // -t counts from the seek position, which makes the output end at the range end.
                arguments.AddRange(["-ss", FormatSeconds(step.SeekPosition)]);

                if (step.SilencedStreams.Count == 0)
                {
                    arguments.AddRange(["-i", step.SourcePath]);
                    arguments.AddRange(["-t", FormatSeconds(step.Range.End - step.SeekPosition)]);
                }
                else
                {
                    // ffmpeg measures an output duration limit for encoded streams from their first frame rather than
                    // from zero, so the copied streams are limited while reading instead. Each silenced stream comes
                    // from a generator that lasts exactly as long as the range and is shifted onto the copied video,
                    // whose zero is the seek position rather than the keyframe the range starts on.
                    arguments.AddRange(["-t", FormatSeconds(step.Range.End - step.SeekPosition)]);
                    arguments.AddRange(["-i", step.SourcePath]);
                    foreach (MediaStreamInfo stream in step.SilencedStreams)
                    {
                        arguments.AddRange(["-itsoffset", FormatSignedSeconds(step.Range.Start - step.SeekPosition)]);
                        arguments.AddRange(["-f", "lavfi", "-t", FormatSeconds(step.Range.Duration)]);
                        arguments.AddRange(["-i", CreateSilenceSource(stream)]);
                    }
                }

                AddStreamCopy(arguments, step, generatesSilence: true);
                arguments.AddRange(["-map_metadata", "0"]);

                // Shifts timestamps so that the output starts at zero, which players and editors expect.
                arguments.AddRange(["-avoid_negative_ts", "make_zero"]);
            }

            // Chapter markers refer to the original timeline and would point outside the exported range.
            arguments.AddRange(["-map_chapters", "-1"]);

            AddEpilogue(arguments, step.Container, step.FastStart, step.OutputPath);
            return arguments;
        }

        /// <summary>
        /// Builds the arguments for an <see cref="EncodeRangeStep"/>.
        /// </summary>
        /// <param name="step">The step to translate.</param>
        /// <returns>The argument list.</returns>
        /// <remarks>
        /// With <c>-ss</c> before <c>-i</c> and an encoder in the chain, ffmpeg decodes from the preceding keyframe and
        /// discards frames until the seek position, so the first encoded frame is exactly the requested one. The encoder
        /// runs at near-lossless quality because the result sits between untouched source GOPs, where any visible step
        /// in quality would stand out. Source timestamps are kept and shifted onto the output timeline.
        /// </remarks>
        public static List<string> ForEncodeRange(EncodeRangeStep step)
        {
            ArgumentNullException.ThrowIfNull(step);

            List<string> arguments = CreatePrologue();
            arguments.Add("-copyts");
            arguments.AddRange(["-ss", FormatSeconds(step.Range.Start)]);
            arguments.AddRange(["-t", FormatSeconds(step.Range.Duration)]);
            arguments.AddRange(["-i", step.SourcePath]);
            arguments.AddRange(["-map", string.Create(CultureInfo.InvariantCulture, $"0:{step.VideoStreamIndex}")]);
            arguments.AddRange(GetEncoderArguments(step.VideoCodecName));

            if (MapProfile(step.VideoCodecName, step.VideoProfile) is string profile)
            {
                arguments.AddRange(["-profile:v", profile]);
            }

            if (!string.IsNullOrEmpty(step.PixelFormat))
            {
                arguments.AddRange(["-pix_fmt", step.PixelFormat]);
            }

            arguments.AddRange(["-output_ts_offset", FormatSignedSeconds(step.TimestampOffset)]);
            arguments.AddRange(["-map_chapters", "-1"]);

            AddEpilogue(arguments, step.Container, fastStart: false, step.OutputPath);
            return arguments;
        }

        /// <summary>
        /// Builds the arguments for a <see cref="MuxPartsStep"/>.
        /// </summary>
        /// <param name="step">The step to translate.</param>
        /// <returns>The argument list. It must run in the directory that contains the parts.</returns>
        /// <remarks>
        /// The concat protocol joins transport streams byte by byte, so the parts keep the timestamps they were written
        /// with. Parts are addressed by file name because the protocol separates entries with '|' and would misread the
        /// drive letter of an absolute Windows path as a protocol name.
        /// </remarks>
        /// <exception cref="ArgumentException">The parts are not all in the same directory.</exception>
        public static List<string> ForMuxParts(MuxPartsStep step)
        {
            ArgumentNullException.ThrowIfNull(step);

            List<string> arguments = CreatePrologue();
            arguments.AddRange(["-i", CreateConcatUri(step.VideoPartPaths)]);
            arguments.AddRange(["-map", "0"]);

            if (step.OtherPartPaths.Count > 0)
            {
                arguments.InsertRange(arguments.IndexOf("-map"), ["-i", CreateConcatUri(step.OtherPartPaths)]);
                arguments.AddRange(["-map", "1"]);
            }

            arguments.AddRange(["-c", "copy"]);
            arguments.AddRange(["-avoid_negative_ts", "make_zero"]);

            AddEpilogue(arguments, step.Container, step.FastStart, step.OutputPath);
            return arguments;
        }

        /// <summary>
        /// Gets the directory a <see cref="MuxPartsStep"/> must run in.
        /// </summary>
        /// <param name="step">The step.</param>
        /// <returns>The directory that contains every part.</returns>
        /// <exception cref="ArgumentException">The parts are not all in the same directory.</exception>
        public static string GetPartsDirectory(MuxPartsStep step)
        {
            ArgumentNullException.ThrowIfNull(step);

            string directory = Path.GetDirectoryName(Path.GetFullPath(step.VideoPartPaths[0])) ?? string.Empty;
            foreach (string path in (IEnumerable<string>)[.. step.VideoPartPaths, .. step.OtherPartPaths])
            {
                if (!string.Equals(Path.GetDirectoryName(Path.GetFullPath(path)), directory, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("Every part must be in the same directory.", nameof(step));
                }
            }

            return directory;
        }

        /// <summary>
        /// Builds the arguments for a <see cref="ConcatStep"/>.
        /// </summary>
        /// <param name="step">The step to translate.</param>
        /// <param name="listFilePath">The path of a concat list written with <see cref="CreateConcatList"/>.</param>
        /// <returns>The argument list.</returns>
        public static List<string> ForConcat(ConcatStep step, string listFilePath)
        {
            ArgumentNullException.ThrowIfNull(step);
            ArgumentNullException.ThrowIfNull(listFilePath);

            List<string> arguments = CreatePrologue();

            // The concat demuxer joins packets without decoding. -safe 0 permits absolute paths in the list,
            // which is required because parts live next to the output rather than next to the list file.
            arguments.AddRange(["-f", "concat", "-safe", "0", "-i", listFilePath]);
            arguments.AddRange(["-map", "0"]);
            arguments.AddRange(["-c", "copy"]);
            arguments.AddRange(["-avoid_negative_ts", "make_zero"]);

            AddEpilogue(arguments, step.Container, step.FastStart, step.OutputPath);
            return arguments;
        }

        /// <summary>
        /// Creates the contents of a concat demuxer list file.
        /// </summary>
        /// <param name="inputPaths">The files to join, in order.</param>
        /// <returns>The list file contents.</returns>
        public static string CreateConcatList(IEnumerable<string> inputPaths)
        {
            ArgumentNullException.ThrowIfNull(inputPaths);

            StringBuilder builder = new();
            builder.Append("ffconcat version 1.0\n");

            foreach (string path in inputPaths)
            {
                // Inside single quotes the concat parser treats every character literally, including backslashes,
                // so only the quote itself must be escaped by closing the quote, escaping it and reopening.
                builder.Append("file '").Append(path.Replace("'", @"'\''", StringComparison.Ordinal)).Append("'\n");
            }

            return builder.ToString();
        }

        /// <summary>
        /// Formats a time span as decimal seconds with microsecond precision, the unit ffmpeg uses internally.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>The formatted value, for example <c>83.456000</c>.</returns>
        internal static string FormatSeconds(TimeSpan value)
        {
            // Formatting from ticks avoids floating point rounding: 1 tick is 100 ns, so 10 ticks make a microsecond.
            long microseconds = value.Ticks / 10;
            long seconds = microseconds / 1_000_000;
            long fraction = microseconds % 1_000_000;
            return string.Create(CultureInfo.InvariantCulture, $"{seconds}.{fraction:D6}");
        }

        /// <summary>
        /// Formats a time span that may be negative as decimal seconds.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>The formatted value, for example <c>-12.500000</c>.</returns>
        internal static string FormatSignedSeconds(TimeSpan value) =>
            value < TimeSpan.Zero ? "-" + FormatSeconds(value.Negate()) : FormatSeconds(value);

        /// <summary>
        /// Gets the ffmpeg muxer name for a concrete container.
        /// </summary>
        /// <param name="container">The container format.</param>
        /// <returns>The muxer name passed to <c>-f</c>.</returns>
        internal static string GetMuxerName(ContainerFormat container) => container switch
        {
            ContainerFormat.Mp4 => "mp4",
            ContainerFormat.Matroska => "matroska",
            ContainerFormat.QuickTime => "mov",
            ContainerFormat.MpegTransportStream => "mpegts",
            _ => throw new ArgumentOutOfRangeException(nameof(container)),
        };

        /// <summary>
        /// Maps a profile name reported by ffprobe to the value the matching encoder accepts.
        /// </summary>
        /// <param name="codecName">The source codec name.</param>
        /// <param name="profile">The profile reported by ffprobe, for example <c>Constrained Baseline</c>.</param>
        /// <returns>The encoder profile, or <see langword="null"/> to let the encoder choose.</returns>
        internal static string? MapProfile(string codecName, string? profile) => (codecName, profile) switch
        {
            ("h264", "Constrained Baseline" or "Baseline") => "baseline",
            ("h264", "Main") => "main",
            ("h264", "High") => "high",
            ("h264", "High 10") => "high10",
            ("h264", "High 4:2:2") => "high422",
            ("h264", "High 4:4:4 Predictive") => "high444",
            ("hevc", "Main") => "main",
            ("hevc", "Main 10") => "main10",
            _ => null,
        };

        private static string[] GetEncoderArguments(string codecName) => codecName switch
        {
            // CRF 12 (x264) and 14 (x265) are visually indistinguishable from the high bit rate sources this targets.
            "h264" => ["-c:v", "libx264", "-preset", "slow", "-crf", "12"],
            "hevc" => ["-c:v", "libx265", "-preset", "slow", "-crf", "14", "-x265-params", "log-level=error"],
            _ => throw new NotSupportedException($"Re-encoding '{codecName}' video is not supported."),
        };

        private static void AddStreamCopy(List<string> arguments, CopyRangeStep step, bool generatesSilence)
        {
            foreach (int index in step.StreamIndexes)
            {
                int silenced = IndexOfSilenced(step, index);
                string map = silenced < 0 || !generatesSilence
                    ? string.Create(CultureInfo.InvariantCulture, $"0:{index}")
                    : string.Create(CultureInfo.InvariantCulture, $"{silenced + 1}:0");
                arguments.AddRange(["-map", map]);
            }

            arguments.AddRange(["-c", "copy"]);

            // Silenced streams are every audio stream of the step, listed in stream order, so the n-th of them is the
            // n-th audio stream of the output.
            for (int i = 0; i < step.SilencedStreams.Count; i++)
            {
                string specifier = string.Create(CultureInfo.InvariantCulture, $":a:{i}");
                arguments.AddRange(["-c" + specifier, GetSilenceEncoder(step.SilencedStreams[i].CodecName)]);
                if (!generatesSilence)
                {
                    arguments.AddRange(["-filter" + specifier, "volume=0"]);
                }
            }

            if (step.StartsExactly)
            {
                // Without this, copied packets from the keyframe the demuxer seeked to would precede the range start.
                arguments.AddRange(["-copypriorss", "0"]);
            }

            if (step.VideoPacketLimit is int packets)
            {
                arguments.AddRange(["-frames:v", packets.ToString(CultureInfo.InvariantCulture)]);
            }
        }

        /// <summary>
        /// Gets the ffmpeg encoder that produces audio in the same codec as the source stream.
        /// </summary>
        /// <param name="codecName">The source codec name reported by ffprobe.</param>
        /// <returns>The encoder name passed to <c>-c:a</c>.</returns>
        /// <exception cref="NotSupportedException">No encoder for <paramref name="codecName"/> is known.</exception>
        internal static string GetSilenceEncoder(string codecName) => codecName switch
        {
            "aac" or "ac3" or "eac3" or "flac" or "alac" => codecName,
            "opus" => "libopus",
            "mp3" => "libmp3lame",
            "vorbis" => "libvorbis",
            _ when codecName.StartsWith("pcm_", StringComparison.Ordinal) => codecName,
            _ => throw new NotSupportedException($"Silence cannot be encoded as '{codecName}' audio."),
        };

        private static int IndexOfSilenced(CopyRangeStep step, int index)
        {
            for (int i = 0; i < step.SilencedStreams.Count; i++)
            {
                if (step.SilencedStreams[i].Index == index)
                {
                    return i;
                }
            }

            return -1;
        }

        private static string CreateSilenceSource(MediaStreamInfo stream)
        {
            // "Nc" names a layout by its channel count alone, for streams whose container does not record the layout.
            string layout = stream.ChannelLayout ?? string.Create(CultureInfo.InvariantCulture, $"{stream.Channels}c");
            return string.Create(CultureInfo.InvariantCulture, $"anullsrc=sample_rate={stream.SampleRate}:channel_layout={layout}");
        }

        private static string CreateConcatUri(IReadOnlyList<string> paths)
        {
            StringBuilder uri = new("concat:");
            for (int i = 0; i < paths.Count; i++)
            {
                if (i > 0)
                {
                    uri.Append('|');
                }

                uri.Append(Path.GetFileName(paths[i]));
            }

            return uri.ToString();
        }

        private static List<string> CreatePrologue() =>
        [
            "-hide_banner",
            "-nostdin",
            "-loglevel", "error",
            "-nostats",
            "-progress", "pipe:1",
            "-n",
        ];

        private static void AddEpilogue(List<string> arguments, ContainerFormat container, bool fastStart, string outputPath)
        {
            if (fastStart)
            {
                arguments.AddRange(["-movflags", "+faststart"]);
            }

            // The muxer is named explicitly so that temporary or unusual extensions never change the output format.
            arguments.AddRange(["-f", GetMuxerName(container)]);
            arguments.Add(outputPath);
        }
    }
}
