// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.Linq;
using TrimC.Editing;
using TrimC.Export;
using Xunit;

namespace TrimC.FFmpeg.Exporting.Tests
{
    public class FFmpegArgumentsTests
    {
        [Fact]
        public void ForCopyRange_SeeksInsideGopAndCopiesUntilRangeEnd()
        {
            CopyRangeStep step = new()
            {
                SourcePath = @"C:\Videos\Recording 1.mkv",
                Range = new TimeRange(TimeSpan.FromSeconds(82), TimeSpan.FromSeconds(90)),
                SeekPosition = TimeSpan.FromSeconds(83.456),
                StreamIndexes = [0, 2],
                OutputPath = @"C:\Videos\out.mp4",
                Container = ContainerFormat.Mp4,
                FastStart = true,
            };

            List<string> arguments = FFmpegArguments.ForCopyRange(step);

            AssertSequence(arguments, "-ss", "83.456000", "-i", @"C:\Videos\Recording 1.mkv", "-t", "6.544000");
            AssertSequence(arguments, "-map", "0:0", "-map", "0:2", "-c", "copy");
            AssertSequence(arguments, "-movflags", "+faststart", "-f", "mp4", @"C:\Videos\out.mp4");
            Assert.Contains("-n", arguments);
            Assert.DoesNotContain("-y", arguments);
        }

        [Fact]
        public void ForCopyRange_Matroska_OmitsFastStart()
        {
            CopyRangeStep step = new()
            {
                SourcePath = "in.mkv",
                Range = new TimeRange(TimeSpan.Zero, TimeSpan.FromSeconds(1)),
                SeekPosition = TimeSpan.Zero,
                StreamIndexes = [0],
                OutputPath = "out.mkv",
                Container = ContainerFormat.Matroska,
            };

            List<string> arguments = FFmpegArguments.ForCopyRange(step);

            Assert.DoesNotContain("-movflags", arguments);
            AssertSequence(arguments, "-f", "matroska", "out.mkv");
        }

        [Fact]
        public void ForConcat_UsesConcatDemuxerWithStreamCopy()
        {
            ConcatStep step = new()
            {
                InputPaths = ["a.mkv", "b.mkv"],
                TotalDuration = TimeSpan.FromSeconds(10),
                OutputPath = "out.mkv",
                Container = ContainerFormat.Matroska,
            };

            List<string> arguments = FFmpegArguments.ForConcat(step, "list.ffconcat");

            AssertSequence(arguments, "-f", "concat", "-safe", "0", "-i", "list.ffconcat", "-map", "0", "-c", "copy");
        }

        [Fact]
        public void CreateConcatList_EscapesSingleQuotes()
        {
            string list = FFmpegArguments.CreateConcatList([@"C:\clips\it's.mkv", "/tmp/b.mkv"]);

            Assert.Equal("ffconcat version 1.0\nfile 'C:\\clips\\it'\\''s.mkv'\nfile '/tmp/b.mkv'\n", list);
        }

        [Theory]
        [InlineData(0L, "0.000000")]
        [InlineData(10L, "0.000001")]
        [InlineData(36_000_000_000L, "3600.000000")]
        [InlineData(834_560_000L, "83.456000")]
        public void FormatSeconds_IsExactAndCultureInvariant(long ticks, string expected)
        {
            Assert.Equal(expected, FFmpegArguments.FormatSeconds(TimeSpan.FromTicks(ticks)));
        }

        private static void AssertSequence(List<string> arguments, params string[] expected)
        {
            int start = arguments.IndexOf(expected[0]);
            while (start >= 0)
            {
                if (start + expected.Length <= arguments.Count && arguments.GetRange(start, expected.Length).SequenceEqual(expected))
                {
                    return;
                }

                start = arguments.IndexOf(expected[0], start + 1);
            }

            Assert.Fail($"Expected the sequence [{string.Join(' ', expected)}] in [{string.Join(' ', arguments)}].");
        }
    }
}
