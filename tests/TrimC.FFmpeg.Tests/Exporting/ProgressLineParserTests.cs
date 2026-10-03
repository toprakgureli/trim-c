// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using Xunit;

namespace TrimC.FFmpeg.Exporting.Tests
{
    public class ProgressLineParserTests
    {
        [Fact]
        public void TryParseOutTime_ValidLine_ReturnsPosition()
        {
            Assert.True(ProgressLineParser.TryParseOutTime("out_time_us=1500000", out TimeSpan position));
            Assert.Equal(TimeSpan.FromSeconds(1.5), position);
        }

        [Theory]
        [InlineData("out_time_us=N/A")]
        [InlineData("out_time_us=-23220")]
        [InlineData("out_time=00:00:01.500000")]
        [InlineData("progress=continue")]
        public void TryParseOutTime_OtherLines_ReturnFalse(string line)
        {
            Assert.False(ProgressLineParser.TryParseOutTime(line, out _));
        }
    }
}
