// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.IO;
using TrimC.Editing;
using TrimC.Export;
using TrimC.FFmpeg.Probing;
using Xunit;

namespace TrimC.FFmpeg.Exporting.Tests
{
    public class EncodeArgumentsTests
    {
        [Fact]
        public void ForEncodeRange_H264_MatchesSourceProfileAndPixelFormat()
        {
            EncodeRangeStep step = new()
            {
                SourcePath = "in.mkv",
                Range = new TimeRange(TimeSpan.FromSeconds(3.5), TimeSpan.FromSeconds(4)),
                VideoStreamIndex = 0,
                TimestampOffset = TimeSpan.FromSeconds(-3.5),
                VideoCodecName = "h264",
                VideoProfile = "High",
                PixelFormat = "yuv420p",
                OutputPath = "part.ts",
                Container = ContainerFormat.MpegTransportStream,
            };

            List<string> arguments = FFmpegArguments.ForEncodeRange(step);

            Assert.Equal(["-copyts", "-ss", "3.500000", "-t", "0.500000", "-i", "in.mkv", "-map", "0:0"], arguments.GetRange(arguments.IndexOf("-copyts"), 9));
            Assert.Contains("libx264", arguments);
            Assert.Equal("high", arguments[arguments.IndexOf("-profile:v") + 1]);
            Assert.Equal("yuv420p", arguments[arguments.IndexOf("-pix_fmt") + 1]);
            Assert.Equal("-3.500000", arguments[arguments.IndexOf("-output_ts_offset") + 1]);
            Assert.Equal(["-f", "mpegts", "part.ts"], arguments.GetRange(arguments.Count - 3, 3));
        }

        [Fact]
        public void ForCopyRange_TimelinePreservingAudio_DropsPacketsBeforeStart()
        {
            CopyRangeStep step = new()
            {
                SourcePath = "in.mkv",
                Range = new TimeRange(TimeSpan.FromSeconds(3.5), TimeSpan.FromSeconds(7.25)),
                SeekPosition = TimeSpan.FromSeconds(3.5),
                StartsExactly = true,
                TimestampOffset = TimeSpan.FromSeconds(-3.5),
                StreamIndexes = [1],
                OutputPath = "audio.ts",
                Container = ContainerFormat.MpegTransportStream,
            };

            List<string> arguments = FFmpegArguments.ForCopyRange(step);

            Assert.Equal(["-copyts", "-ss", "3.500000", "-t", "3.750000", "-i", "in.mkv"], arguments.GetRange(arguments.IndexOf("-copyts"), 7));
            Assert.Equal("0", arguments[arguments.IndexOf("-copypriorss") + 1]);
            Assert.DoesNotContain("-avoid_negative_ts", arguments);
        }

        [Fact]
        public void ForMuxParts_JoinsVideoAndAudioPartsByFileName()
        {
            string directory = Path.Combine(Path.GetTempPath(), "parts");
            MuxPartsStep step = new()
            {
                VideoPartPaths = [Path.Combine(directory, "v0.ts"), Path.Combine(directory, "v1.ts")],
                OtherPartPaths = [Path.Combine(directory, "a0.ts")],
                TotalDuration = TimeSpan.FromSeconds(5),
                OutputPath = Path.Combine(Path.GetTempPath(), "out.mp4"),
                Container = ContainerFormat.Mp4,
            };

            List<string> arguments = FFmpegArguments.ForMuxParts(step);

            Assert.Equal(["-i", "concat:v0.ts|v1.ts", "-i", "concat:a0.ts", "-map", "0", "-map", "1", "-c", "copy"], arguments.GetRange(arguments.IndexOf("-i"), 10));
            Assert.Equal(Path.GetFullPath(directory), FFmpegArguments.GetPartsDirectory(step));
        }

        [Fact]
        public void ForCopyRange_WithPacketLimit_LimitsVideoFrames()
        {
            CopyRangeStep step = new()
            {
                SourcePath = "in.mkv",
                Range = new TimeRange(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(6)),
                SeekPosition = TimeSpan.FromSeconds(5.5),
                VideoPacketLimit = 120,
                StreamIndexes = [0],
                OutputPath = "part.ts",
                Container = ContainerFormat.MpegTransportStream,
            };

            List<string> arguments = FFmpegArguments.ForCopyRange(step);

            Assert.Equal("120", arguments[arguments.IndexOf("-frames:v") + 1]);
        }

        [Theory]
        [InlineData("h264", "Constrained Baseline", "baseline")]
        [InlineData("h264", "High 10", "high10")]
        [InlineData("hevc", "Main 10", "main10")]
        [InlineData("h264", "Unknown", null)]
        [InlineData("h264", null, null)]
        public void MapProfile_TranslatesProbeNamesToEncoderNames(string codec, string? profile, string? expected)
        {
            Assert.Equal(expected, FFmpegArguments.MapProfile(codec, profile));
        }

        [Fact]
        public void TryParsePacketLine_ReportsNonKeyframesAndMissingTimestamps()
        {
            Assert.True(ProbeParser.TryParsePacketLine("2.500000,___", TimeSpan.Zero, out bool isKeyframe, out TimeSpan? position));
            Assert.False(isKeyframe);
            Assert.Equal(TimeSpan.FromSeconds(2.5), position);

            Assert.True(ProbeParser.TryParsePacketLine("N/A,K__", TimeSpan.Zero, out isKeyframe, out position));
            Assert.True(isKeyframe);
            Assert.Null(position);
        }
    }
}
