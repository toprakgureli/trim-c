// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrimC.Editing;
using TrimC.Media;

namespace TrimC.Export
{
    /// <summary>
    /// Translates segments and options into an <see cref="ExportPlan"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Planning is a pure function of its inputs: it performs no I/O other than the injected existence check,
    /// so every decision about snapping, stream selection, naming and merging is covered by unit tests
    /// without invoking FFmpeg.
    /// </para>
    /// <para>
    /// A merge is planned as one stream copy per segment into temporary parts followed by a concat step.
    /// Seeking each part independently keeps every cut keyframe aligned, whereas a single FFmpeg invocation with
    /// multiple trims would force a re-encode.
    /// </para>
    /// <para>
    /// In <see cref="CutMode.FrameAccurate"/> mode a segment becomes up to three parts: the frames before its first
    /// keyframe are re-encoded, the GOPs between its first and last keyframe are copied, and the frames after its last
    /// keyframe are re-encoded. The audio of the segment is copied as one continuous part. Every part keeps its source
    /// timestamps, shifted so that the segment follows the previous one on the output timeline, and is written as an
    /// MPEG transport stream, which repeats the codec parameter sets in front of every keyframe. The parts are then
    /// joined byte by byte, without re-timing. Only the two partial GOPs are ever encoded, so the cost and the
    /// quality impact do not grow with the length of the segment.
    /// </para>
    /// <para>
    /// A muted segment loses its audio streams when it becomes a file of its own, and so does a merge in which every
    /// segment is muted. When muted and audible segments are merged, the muted ones carry silence encoded in the codec
    /// of the source, because every part of a concatenation must have the same streams.
    /// </para>
    /// </remarks>
    public static class ExportPlanner
    {
        // Fraction of the GOP after its keyframe at which the demuxer seek is placed; see CopyRangeStep.SeekPosition.
        // Three quarters clears ffmpeg's B-frame seek adjustment of about 0.13 s for any GOP longer than about 0.18 s,
        // while staying before the next keyframe.
        private const double SeekPositionWithinGop = 0.75;

        /// <summary>
        /// Creates a plan that exports <paramref name="segments"/> from <paramref name="source"/>.
        /// </summary>
        /// <param name="source">The probed source media.</param>
        /// <param name="keyframes">The keyframe index of the primary video stream.</param>
        /// <param name="segments">The segments to export, in timeline order.</param>
        /// <param name="options">The export settings.</param>
        /// <returns>The resolved plan.</returns>
        /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="segments"/> is empty, every segment collapses during keyframe snapping, or no stream is selected.
        /// </exception>
        public static ExportPlan CreatePlan(MediaInfo source, KeyframeIndex keyframes, IReadOnlyList<Segment> segments, ExportOptions options) =>
            CreatePlan(source, keyframes, segments, options, File.Exists);

        /// <summary>
        /// Creates a plan using a custom file existence check.
        /// </summary>
        /// <param name="source">The probed source media.</param>
        /// <param name="keyframes">The keyframe index of the primary video stream.</param>
        /// <param name="segments">The segments to export, in timeline order.</param>
        /// <param name="options">The export settings.</param>
        /// <param name="fileExists">A predicate that reports whether a path is already taken.</param>
        /// <returns>The resolved plan.</returns>
        /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="segments"/> is empty, every segment collapses during keyframe snapping, or no stream is selected.
        /// </exception>
        public static ExportPlan CreatePlan(
            MediaInfo source,
            KeyframeIndex keyframes,
            IReadOnlyList<Segment> segments,
            ExportOptions options,
            Func<string, bool> fileExists)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(keyframes);
            ArgumentNullException.ThrowIfNull(segments);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(fileExists);

            if (segments.Count == 0)
            {
                throw new ArgumentException("At least one segment is required.", nameof(segments));
            }

            ContainerFormat container = ContainerFormats.Resolve(options.Container, source.FilePath);
            IReadOnlyList<int> streamIndexes = SelectStreams(source, container, options.StreamIndexes);
            PlanContext context = new(
                source,
                keyframes,
                container,
                ContainerFormats.GetExtension(container),
                options.FastStart && ContainerFormats.IsIsoBaseMedia(container),
                streamIndexes,
                SelectAudio(source, streamIndexes),
                new OutputFileNamer(options.OutputDirectory, source.FilePath, options.OutputName, fileExists),
                !string.IsNullOrWhiteSpace(options.OutputName));

            MediaStreamInfo? video = source.PrimaryVideoStream;
            bool exportsVideo = video is not null && keyframes.Count > 0 && streamIndexes.Contains(video.Index);

            // Audio has no GOP structure, so a keyframe-mode copy of audio-only media is already exact.
            if (options.CutMode == CutMode.FrameAccurate && exportsVideo)
            {
                return PlanFrameAccurate(context, video!, segments, options.Mode);
            }

            List<(Segment Segment, TimeRange Range)> aligned = Align(segments, keyframes, options.SnapMode);

            return options.Mode == ExportMode.Merge && aligned.Count > 1
                ? PlanMerge(context, aligned)
                : PlanSeparate(context, aligned);
        }

        private static bool CanSilence(string codecName) =>
            codecName is "aac" or "opus" or "mp3" or "ac3" or "eac3" or "flac" or "vorbis" or "alac" ||
            codecName.StartsWith("pcm_", StringComparison.Ordinal);

        private static List<MediaStreamInfo> SelectAudio(MediaInfo source, IReadOnlyList<int> streamIndexes)
        {
            List<MediaStreamInfo> audio = [];
            foreach (MediaStreamInfo stream in source.Streams)
            {
                if (stream.Kind == StreamKind.Audio && streamIndexes.Contains(stream.Index))
                {
                    audio.Add(stream);
                }
            }

            audio.Sort((left, right) => left.Index.CompareTo(right.Index));
            return audio;
        }

        private static ExportPlan PlanFrameAccurate(PlanContext context, MediaStreamInfo video, IReadOnlyList<Segment> segments, ExportMode mode)
        {
            if (video.CodecName is not ("h264" or "hevc"))
            {
                throw new ArgumentException(
                    $"Frame-accurate cutting supports H.264 and HEVC video, but this file uses {video.CodecName}. Use keyframe mode instead.",
                    nameof(video));
            }

            MediaInfo source = context.Source;

            // Transport stream parts carry only audio and video; other streams cannot be split and rejoined.
            List<int> audioStreams = context.AudioStreams.Select(stream => stream.Index).ToList();

            // A merge keeps an audio track while any segment is audible; muted segments then contribute silence.
            bool mergeHasSound = mode == ExportMode.Merge && segments.Any(segment => !segment.IsMuted);

            // Positions reported by the player and the keyframe index can differ by rounding; half a frame is the largest
            // difference that still identifies the same frame.
            TimeSpan tolerance = TimeSpan.FromSeconds(0.5 / (video.FrameRate ?? 30));

            List<ExportStep> steps = [];
            List<string> outputs = [];
            List<string> temporaries = [];
            List<string> videoParts = [];
            List<string> otherParts = [];
            TimeSpan outputPosition = TimeSpan.Zero;
            int ordinal = 0;
            string NextPartPath() => context.Namer.ForTemporaryPart(ordinal++, ".ts");

            foreach (Segment segment in segments)
            {
                // Separate files each start at zero; merged segments follow each other on one timeline.
                TimeSpan offset = (mode == ExportMode.Merge ? outputPosition : TimeSpan.Zero) - segment.Range.Start;

                foreach (ExportStep part in CreateVideoParts(source, video, context.Keyframes, segment.Range, offset, tolerance, NextPartPath))
                {
                    steps.Add(part);
                    videoParts.Add(part.OutputPath);
                }

                bool hasSound = mode == ExportMode.Merge ? mergeHasSound : !segment.IsMuted;
                if (audioStreams.Count > 0 && hasSound)
                {
                    CopyRangeStep audio = new()
                    {
                        SourcePath = source.FilePath,
                        Range = segment.Range,
                        SeekPosition = segment.Range.Start,
                        StartsExactly = true,
                        TimestampOffset = offset,
                        StreamIndexes = audioStreams,
                        SilencedStreams = segment.IsMuted ? context.Silenced() : [],
                        OutputPath = NextPartPath(),
                        Container = ContainerFormat.MpegTransportStream,
                    };
                    steps.Add(audio);
                    otherParts.Add(audio.OutputPath);
                }

                outputPosition += segment.Range.Duration;

                if (mode == ExportMode.Merge)
                {
                    continue;
                }

                string outputPath = context.UsesChosenName && segments.Count == 1
                    ? context.Namer.ForChosenName(context.Extension)
                    : context.Namer.ForSegment(segment, segment.Range, context.Extension);
                steps.Add(CreateMuxStep(videoParts, otherParts, segment.Range.Duration, context.Container, context.FastStart, outputPath));
                outputs.Add(outputPath);
                temporaries.AddRange(videoParts);
                temporaries.AddRange(otherParts);
                videoParts = [];
                otherParts = [];
            }

            if (mode == ExportMode.Merge)
            {
                string outputPath = context.UsesChosenName ? context.Namer.ForChosenName(context.Extension) : context.Namer.ForMerged(context.Extension);
                steps.Add(CreateMuxStep(videoParts, otherParts, outputPosition, context.Container, context.FastStart, outputPath));
                outputs.Add(outputPath);
                temporaries.AddRange(videoParts);
                temporaries.AddRange(otherParts);
            }

            return new ExportPlan
            {
                Steps = steps,
                OutputFiles = outputs,
                TemporaryFiles = temporaries,
                ReplaceableFiles = context.UsesChosenName && outputs.Count == 1 ? outputs : [],
            };
        }

        private static List<ExportStep> CreateVideoParts(
            MediaInfo source,
            MediaStreamInfo video,
            KeyframeIndex keyframes,
            TimeRange range,
            TimeSpan offset,
            TimeSpan tolerance,
            Func<string> nextPartPath)
        {
            List<ExportStep> parts = new(3);
            TimeSpan? bodyStart = keyframes.FindNext(range.Start - tolerance);
            TimeSpan? bodyEnd = keyframes.FindPrevious(range.End + tolerance);

            // A segment that contains no complete GOP is short enough to be re-encoded as a whole.
            if (bodyStart is null || bodyEnd is null || bodyEnd.Value - bodyStart.Value <= tolerance)
            {
                parts.Add(CreateEncodeStep(source, video, range, offset, nextPartPath()));
                return parts;
            }

            if (bodyStart.Value - range.Start > tolerance)
            {
                parts.Add(CreateEncodeStep(source, video, new TimeRange(range.Start, bodyStart.Value), offset, nextPartPath()));
            }

            TimeRange body = new(bodyStart.Value, bodyEnd.Value);
            parts.Add(new CopyRangeStep
            {
                SourcePath = source.FilePath,
                Range = body,
                SeekPosition = GetSeekPosition(body, keyframes),
                VideoPacketLimit = keyframes.CountPackets(body.Start, body.End),
                TimestampOffset = offset,
                StreamIndexes = [video.Index],
                OutputPath = nextPartPath(),
                Container = ContainerFormat.MpegTransportStream,
            });

            if (range.End - bodyEnd.Value > tolerance)
            {
                parts.Add(CreateEncodeStep(source, video, new TimeRange(bodyEnd.Value, range.End), offset, nextPartPath()));
            }

            return parts;
        }

        private static EncodeRangeStep CreateEncodeStep(MediaInfo source, MediaStreamInfo video, TimeRange range, TimeSpan offset, string outputPath) =>
            new()
            {
                SourcePath = source.FilePath,
                Range = range,
                VideoStreamIndex = video.Index,
                TimestampOffset = offset,
                VideoCodecName = video.CodecName,
                VideoProfile = video.Profile,
                PixelFormat = video.PixelFormat,
                OutputPath = outputPath,
                Container = ContainerFormat.MpegTransportStream,
            };

        private static MuxPartsStep CreateMuxStep(
            IReadOnlyList<string> videoParts,
            IReadOnlyList<string> otherParts,
            TimeSpan duration,
            ContainerFormat container,
            bool fastStart,
            string outputPath) =>
            new()
            {
                VideoPartPaths = videoParts,
                OtherPartPaths = otherParts,
                TotalDuration = duration,
                OutputPath = outputPath,
                Container = container,
                FastStart = fastStart,
            };

        private static List<(Segment Segment, TimeRange Range)> Align(IReadOnlyList<Segment> segments, KeyframeIndex keyframes, KeyframeSnapMode mode)
        {
            List<(Segment, TimeRange)> aligned = new(segments.Count);
            foreach (Segment segment in segments)
            {
                // Segments shorter than one GOP can vanish under Next snapping; they are skipped rather than failing the whole export.
                TimeRange? range = KeyframeSnapper.Snap(segment.Range, keyframes, mode);
                if (range is not null)
                {
                    aligned.Add((segment, range.Value));
                }
            }

            if (aligned.Count == 0)
            {
                throw new ArgumentException("Every segment is shorter than the distance to its next keyframe.", nameof(segments));
            }

            return aligned;
        }

        private static List<int> SelectStreams(MediaInfo source, ContainerFormat container, IReadOnlyList<int>? requested)
        {
            List<int> selected = [];

            if (requested is not null)
            {
                foreach (int index in requested)
                {
                    if (!ContainsStream(source, index))
                    {
                        throw new ArgumentException($"The source has no stream with index {index}.", nameof(requested));
                    }

                    selected.Add(index);
                }
            }
            else
            {
                foreach (MediaStreamInfo stream in source.Streams)
                {
                    if (ContainerFormats.SupportsByDefault(container, stream))
                    {
                        selected.Add(stream.Index);
                    }
                }
            }

            if (selected.Count == 0)
            {
                throw new ArgumentException("No stream is selected for export.", nameof(requested));
            }

            selected.Sort();
            return selected;
        }

        private static TimeSpan GetSeekPosition(TimeRange range, KeyframeIndex keyframes)
        {
            // Without a keyframe grid (audio-only media), or when the start was deliberately left unaligned, the seek
            // goes to the start itself and the demuxer picks the preceding keyframe.
            if (keyframes.Count == 0 || !keyframes.IsKeyframe(range.Start, TimeSpan.Zero))
            {
                return range.Start;
            }

            // The seek must stay before both the next keyframe and the end of the range. A range shorter than ffmpeg's
            // adjustment may still start one GOP early, which is the closest a stream copy can get.
            TimeSpan gopEnd = keyframes.FindAfter(range.Start) ?? range.End;
            TimeSpan limit = gopEnd < range.End ? gopEnd : range.End;
            return range.Start + ((limit - range.Start) * SeekPositionWithinGop);
        }

        private static bool ContainsStream(MediaInfo source, int index)
        {
            foreach (MediaStreamInfo stream in source.Streams)
            {
                if (stream.Index == index)
                {
                    return true;
                }
            }

            return false;
        }

        private static ExportPlan PlanSeparate(PlanContext context, List<(Segment Segment, TimeRange Range)> aligned)
        {
            List<ExportStep> steps = new(aligned.Count);
            List<string> outputs = new(aligned.Count);
            bool usesChosenName = context.UsesChosenName && aligned.Count == 1;

            foreach ((Segment segment, TimeRange range) in aligned)
            {
                string outputPath = usesChosenName
                    ? context.Namer.ForChosenName(context.Extension)
                    : context.Namer.ForSegment(segment, range, context.Extension);
                IReadOnlyList<int> streams = segment.IsMuted ? context.WithoutAudio() : context.StreamIndexes;
                steps.Add(CreateCopyStep(context, range, streams, [], context.FastStart, outputPath));
                outputs.Add(outputPath);
            }

            return new ExportPlan
            {
                Steps = steps,
                OutputFiles = outputs,
                TemporaryFiles = [],
                ReplaceableFiles = usesChosenName ? outputs : [],
            };
        }

        private static ExportPlan PlanMerge(PlanContext context, List<(Segment Segment, TimeRange Range)> aligned)
        {
            List<ExportStep> steps = new(aligned.Count + 1);
            List<string> parts = new(aligned.Count);
            TimeSpan total = TimeSpan.Zero;

            // Every part of a concatenation must have the same streams: without any audible segment the audio is left
            // out altogether, otherwise muted segments carry silence.
            bool hasSound = aligned.Any(item => !item.Segment.IsMuted);
            IReadOnlyList<int> streams = hasSound || context.AudioStreams.Count == 0 ? context.StreamIndexes : context.WithoutAudio();

            for (int i = 0; i < aligned.Count; i++)
            {
                (Segment segment, TimeRange range) = aligned[i];
                string partPath = context.Namer.ForTemporaryPart(i, context.Extension);
                IReadOnlyList<MediaStreamInfo> silenced = hasSound && segment.IsMuted ? context.Silenced() : [];

                // Parts are concatenated afterwards; relocating the index of each part would be wasted I/O.
                steps.Add(CreateCopyStep(context, range, streams, silenced, fastStart: false, partPath));
                parts.Add(partPath);
                total += range.Duration;
            }

            string outputPath = context.UsesChosenName ? context.Namer.ForChosenName(context.Extension) : context.Namer.ForMerged(context.Extension);
            steps.Add(new ConcatStep
            {
                InputPaths = parts,
                OutputPath = outputPath,
                Container = context.Container,
                TotalDuration = total,
                FastStart = context.FastStart,
            });

            return new ExportPlan
            {
                Steps = steps,
                OutputFiles = [outputPath],
                TemporaryFiles = parts,
                ReplaceableFiles = context.UsesChosenName ? [outputPath] : [],
            };
        }

        private static CopyRangeStep CreateCopyStep(
            PlanContext context,
            TimeRange range,
            IReadOnlyList<int> streamIndexes,
            IReadOnlyList<MediaStreamInfo> silencedStreams,
            bool fastStart,
            string outputPath) =>
            new()
            {
                SourcePath = context.Source.FilePath,
                Range = range,
                SeekPosition = GetSeekPosition(range, context.Keyframes),
                StreamIndexes = streamIndexes,
                SilencedStreams = silencedStreams,
                OutputPath = outputPath,
                Container = context.Container,
                FastStart = fastStart,
            };

        /// <summary>
        /// The inputs every part of a plan is built from.
        /// </summary>
        /// <param name="Source">The probed source media.</param>
        /// <param name="Keyframes">The keyframe index of the primary video stream.</param>
        /// <param name="Container">The resolved output container.</param>
        /// <param name="Extension">The file extension of <paramref name="Container"/>.</param>
        /// <param name="FastStart">Whether the index of the final files is placed at the start.</param>
        /// <param name="StreamIndexes">The selected source streams, in index order.</param>
        /// <param name="AudioStreams">The selected audio streams, in index order.</param>
        /// <param name="Namer">Names the output and temporary files.</param>
        /// <param name="UsesChosenName">Whether a single output file takes the name the user chose.</param>
        private sealed record PlanContext(
            MediaInfo Source,
            KeyframeIndex Keyframes,
            ContainerFormat Container,
            string Extension,
            bool FastStart,
            IReadOnlyList<int> StreamIndexes,
            IReadOnlyList<MediaStreamInfo> AudioStreams,
            OutputFileNamer Namer,
            bool UsesChosenName)
        {
            /// <summary>Returns the selected streams without audio, for a muted segment exported on its own.</summary>
            /// <returns>The remaining stream indexes.</returns>
            /// <exception cref="ArgumentException">The source has nothing but audio.</exception>
            public List<int> WithoutAudio()
            {
                List<int> indexes = [];
                foreach (int index in StreamIndexes)
                {
                    if (!AudioStreams.Any(stream => stream.Index == index))
                    {
                        indexes.Add(index);
                    }
                }

                if (indexes.Count == 0)
                {
                    throw new ArgumentException("A muted segment of a file that only has sound leaves nothing to export.", nameof(StreamIndexes));
                }

                return indexes;
            }

            /// <summary>Returns the audio streams to silence for a muted segment that is merged with audible ones.</summary>
            /// <returns>The audio streams.</returns>
            /// <exception cref="ArgumentException">Silence cannot be produced in the codec of one of the streams.</exception>
            public IReadOnlyList<MediaStreamInfo> Silenced()
            {
                foreach (MediaStreamInfo stream in AudioStreams)
                {
                    if (!CanSilence(stream.CodecName) || stream.SampleRate is null || (stream.Channels is null && stream.ChannelLayout is null))
                    {
                        throw new ArgumentException(
                            $"Merging a muted segment needs silence in the {stream.CodecName} codec, which cannot be produced. Export the segments as separate files instead.",
                            nameof(AudioStreams));
                    }
                }

                return AudioStreams;
            }
        }
    }
}
