// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.IO;
using TrimC.Editing;
using Xunit;

namespace TrimC.Export.Tests
{
    public class OutputFileNamerTests
    {
        private static readonly string s_directory = Path.Combine(Path.GetTempPath(), "trimc-tests");

        [Fact]
        public void ForSegment_WithoutLabel_EncodesTheRange()
        {
            OutputFileNamer namer = CreateNamer();
            TimeRange range = new(new TimeSpan(0, 1, 2, 3, 450), new TimeSpan(0, 1, 2, 10, 0));

            string path = namer.ForSegment(new Segment { Id = Guid.NewGuid(), Range = range }, range, ".mp4");

            Assert.Equal(Path.Combine(s_directory, "recording-01.02.03.450-01.02.10.000.mp4"), path);
        }

        [Fact]
        public void ForSegment_WithLabel_UsesSanitizedLabel()
        {
            OutputFileNamer namer = CreateNamer();
            TimeRange range = new(TimeSpan.Zero, TimeSpan.FromSeconds(1));

            string path = namer.ForSegment(new Segment { Id = Guid.NewGuid(), Range = range, Label = "intro: part 1?" }, range, ".mkv");

            Assert.Equal(Path.Combine(s_directory, "recording-intro_ part 1_.mkv"), path);
        }

        [Fact]
        public void Reserve_SameNameTwice_AppendsCounter()
        {
            OutputFileNamer namer = CreateNamer();

            string first = namer.ForMerged(".mkv");
            string second = namer.ForMerged(".mkv");

            Assert.Equal(Path.Combine(s_directory, "recording-cut.mkv"), first);
            Assert.Equal(Path.Combine(s_directory, "recording-cut (2).mkv"), second);
        }

        [Theory]
        [InlineData("a/b\\c", "a_b_c")]
        [InlineData("  trailing. ", "trailing")]
        [InlineData("tab\tchar", "tab_char")]
        public void Sanitize_ReplacesCharactersInvalidOnWindows(string input, string expected)
        {
            Assert.Equal(expected, OutputFileNamer.Sanitize(input));
        }

        [Fact]
        public void Sanitize_LongLabel_IsTruncated()
        {
            Assert.Equal(64, OutputFileNamer.Sanitize(new string('x', 200)).Length);
        }

        private static OutputFileNamer CreateNamer() => new(s_directory, Path.Combine(s_directory, "recording.mkv"), static _ => false);
    }
}
