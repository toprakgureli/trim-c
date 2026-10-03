// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using TrimC.Media;
using Xunit;

namespace TrimC.Editing.Tests
{
    public class KeyframeSnapperTests
    {
        private static readonly KeyframeIndex s_keyframes = KeyframeIndex.Create([Seconds(0), Seconds(2), Seconds(4), Seconds(6)]);

        [Theory]
        [InlineData(KeyframeSnapMode.Previous, 2.0)]
        [InlineData(KeyframeSnapMode.Next, 4.0)]
        [InlineData(KeyframeSnapMode.Nearest, 4.0)]
        [InlineData(KeyframeSnapMode.None, 3.5)]
        public void Snap_MovesOnlyTheStart(KeyframeSnapMode mode, double expectedStart)
        {
            TimeRange? snapped = KeyframeSnapper.Snap(Range(3.5, 9), s_keyframes, mode);

            Assert.Equal(Range(expectedStart, 9), snapped);
        }

        [Fact]
        public void Snap_NextPastEnd_ReturnsNull()
        {
            Assert.Null(KeyframeSnapper.Snap(Range(2.5, 3.5), s_keyframes, KeyframeSnapMode.Next));
        }

        [Fact]
        public void Snap_PreviousBeforeFirstKeyframe_UsesFirstKeyframe()
        {
            KeyframeIndex keyframes = KeyframeIndex.Create([Seconds(1), Seconds(3)]);

            Assert.Equal(Range(1, 5), KeyframeSnapper.Snap(Range(0.5, 5), keyframes, KeyframeSnapMode.Previous));
        }

        [Fact]
        public void Snap_WithoutKeyframes_ReturnsRangeUnchanged()
        {
            Assert.Equal(Range(3.5, 9), KeyframeSnapper.Snap(Range(3.5, 9), KeyframeIndex.Empty, KeyframeSnapMode.Next));
        }

        private static TimeRange Range(double start, double end) => new(Seconds(start), Seconds(end));

        private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
    }
}
