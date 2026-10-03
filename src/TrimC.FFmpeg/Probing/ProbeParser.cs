// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using TrimC.Media;

namespace TrimC.FFmpeg.Probing
{
    /// <summary>
    /// Converts raw ffprobe output into domain objects.
    /// </summary>
    /// <remarks>
    /// Parsing is separated from process execution so that the mapping rules can be verified against recorded
    /// ffprobe output without the tool being installed.
    /// </remarks>
    internal static class ProbeParser
    {
        /// <summary>
        /// Parses the JSON produced by <c>ffprobe -show_format -show_streams -print_format json</c>.
        /// </summary>
        /// <param name="json">The JSON document.</param>
        /// <param name="filePath">The path of the probed file.</param>
        /// <returns>The media description.</returns>
        /// <exception cref="FFmpegException">The document is malformed or lacks a usable duration.</exception>
        public static MediaInfo ParseMediaInfo(string json, string filePath)
        {
            ProbeDocument? document;
            try
            {
                document = JsonSerializer.Deserialize(json, ProbeJsonContext.Default.ProbeDocument);
            }
            catch (JsonException ex)
            {
                throw new FFmpegException("ffprobe returned output that is not valid JSON.", ex);
            }

            ProbeFormat format = document?.Format ?? throw new FFmpegException("ffprobe output does not contain a format section.");

            TimeSpan duration = ParseSeconds(format.Duration) ?? TimeSpan.Zero;
            if (duration <= TimeSpan.Zero)
            {
                // Live streams and some broken files report no duration; they cannot be placed on a finite timeline.
                throw new FFmpegException($"The file '{filePath}' does not report a duration.");
            }

            List<MediaStreamInfo> streams = [];
            foreach (ProbeStream stream in document.Streams ?? [])
            {
                streams.Add(MapStream(stream));
            }

            streams.Sort(static (a, b) => a.Index.CompareTo(b.Index));

            return new MediaInfo
            {
                FilePath = filePath,
                FormatName = format.FormatName ?? string.Empty,
                Duration = duration,
                StartTime = ParseSeconds(format.StartTime) ?? TimeSpan.Zero,
                BitRate = ParseInt64(format.BitRate),
                Streams = streams,
            };
        }

        /// <summary>
        /// Parses one line of <c>ffprobe -show_entries packet=pts_time,flags -of csv=print_section=0</c> output,
        /// reporting whether the packet is a keyframe regardless of whether it carries a timestamp.
        /// </summary>
        /// <param name="line">A line such as <c>12.345000,K__</c>.</param>
        /// <param name="startTime">The container start time, subtracted to make the position timeline relative.</param>
        /// <param name="isKeyframe">When this method returns <see langword="true"/>, whether the packet is a keyframe.</param>
        /// <param name="position">When this method returns <see langword="true"/>, the relative position, or <see langword="null"/> without a timestamp.</param>
        /// <returns><see langword="true"/> if the line describes a packet; otherwise, <see langword="false"/>.</returns>
        public static bool TryParsePacketLine(ReadOnlySpan<char> line, TimeSpan startTime, out bool isKeyframe, out TimeSpan? position)
        {
            isKeyframe = false;
            position = null;

            int comma = line.IndexOf(',');
            if (comma <= 0)
            {
                return false;
            }

            isKeyframe = line[(comma + 1)..].Contains('K');
            if (double.TryParse(line[..comma], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
            {
                TimeSpan relative = TimeSpan.FromSeconds(seconds) - startTime;
                position = relative < TimeSpan.Zero ? TimeSpan.Zero : relative;
            }

            return true;
        }

        /// <summary>
        /// Parses one line of <c>ffprobe -show_entries packet=pts_time,flags -of csv=print_section=0</c> output.
        /// </summary>
        /// <param name="line">A line such as <c>12.345000,K__</c>.</param>
        /// <param name="startTime">The container start time, subtracted to make the position timeline relative.</param>
        /// <param name="position">When this method returns <see langword="true"/>, the relative keyframe position.</param>
        /// <returns><see langword="true"/> if the line describes a keyframe with a valid timestamp; otherwise, <see langword="false"/>.</returns>
        public static bool TryParseKeyframeLine(ReadOnlySpan<char> line, TimeSpan startTime, out TimeSpan position)
        {
            position = default;

            int comma = line.IndexOf(',');
            if (comma <= 0)
            {
                return false;
            }

            // The flags column is a fixed-width string in which 'K' marks a keyframe and '_' an unset flag.
            ReadOnlySpan<char> flags = line[(comma + 1)..];
            if (!flags.Contains('K'))
            {
                return false;
            }

            // Packets without a presentation timestamp are reported as "N/A" and cannot be placed on the timeline.
            if (!double.TryParse(line[..comma], NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds))
            {
                return false;
            }

            TimeSpan relative = TimeSpan.FromSeconds(seconds) - startTime;
            position = relative < TimeSpan.Zero ? TimeSpan.Zero : relative;
            return true;
        }

        /// <summary>
        /// Parses an FFmpeg rational such as <c>30000/1001</c> into a floating point value.
        /// </summary>
        /// <param name="value">The rational value.</param>
        /// <returns>The quotient, or <see langword="null"/> when the value is missing, malformed or has a zero denominator.</returns>
        internal static double? ParseRational(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            ReadOnlySpan<char> span = value;
            int slash = span.IndexOf('/');
            if (slash < 0)
            {
                return double.TryParse(span, NumberStyles.Float, CultureInfo.InvariantCulture, out double plain) ? plain : null;
            }

            if (!double.TryParse(span[..slash], NumberStyles.Float, CultureInfo.InvariantCulture, out double numerator) ||
                !double.TryParse(span[(slash + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out double denominator) ||
                denominator == 0)
            {
                return null;
            }

            return numerator / denominator;
        }

        private static MediaStreamInfo MapStream(ProbeStream stream)
        {
            StreamKind kind = stream.CodecType switch
            {
                "video" => StreamKind.Video,
                "audio" => StreamKind.Audio,
                "subtitle" => StreamKind.Subtitle,
                "data" => StreamKind.Data,
                "attachment" => StreamKind.Attachment,
                _ => StreamKind.Unknown,
            };

            // avg_frame_rate is 0/0 for variable frame rate content in some containers; r_frame_rate is the
            // lowest common timebase and is only used as a fallback because it can overstate the real rate.
            double? frameRate = kind == StreamKind.Video
                ? NonZero(ParseRational(stream.AvgFrameRate)) ?? NonZero(ParseRational(stream.RFrameRate))
                : null;

            return new MediaStreamInfo
            {
                Index = stream.Index,
                Kind = kind,
                CodecName = stream.CodecName ?? "unknown",
                CodecLongName = stream.CodecLongName,
                Profile = stream.Profile,
                PixelFormat = stream.PixFmt,
                Language = GetTag(stream.Tags, "language"),
                Title = GetTag(stream.Tags, "title"),
                IsDefault = stream.Disposition?.Default == 1,
                IsAttachedPicture = stream.Disposition?.AttachedPic == 1,
                Width = stream.Width,
                Height = stream.Height,
                FrameRate = frameRate,
                Channels = stream.Channels,
                ChannelLayout = string.IsNullOrEmpty(stream.ChannelLayout) ? null : stream.ChannelLayout,
                SampleRate = (int?)ParseInt64(stream.SampleRate),
                BitRate = ParseInt64(stream.BitRate),
            };
        }

        private static string? GetTag(Dictionary<string, string>? tags, string name)
        {
            if (tags is null)
            {
                return null;
            }

            // Matroska writers disagree on tag casing (TITLE versus title), so lookups ignore case.
            foreach (KeyValuePair<string, string> tag in tags)
            {
                if (string.Equals(tag.Key, name, StringComparison.OrdinalIgnoreCase))
                {
                    return tag.Value;
                }
            }

            return null;
        }

        private static TimeSpan? ParseSeconds(string? value) =>
            double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && double.IsFinite(seconds)
                ? TimeSpan.FromSeconds(seconds)
                : null;

        private static long? ParseInt64(string? value) =>
            long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long result) ? result : null;

        private static double? NonZero(double? value) => value is > 0 && double.IsFinite(value.Value) ? value : null;
    }
}
