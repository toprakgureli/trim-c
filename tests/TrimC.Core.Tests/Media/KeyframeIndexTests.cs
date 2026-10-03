// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using Xunit;

namespace TrimC.Media.Tests
{
    public class KeyframeIndexTests
    {
        private static readonly KeyframeIndex s_twoSecondGop = KeyframeIndex.Create([Seconds(4), Seconds(0), Seconds(2), Seconds(6), Seconds(2)]);

        [Fact]
        public void Create_UnsortedInputWithDuplicates_IsSortedAndDistinct()
        {
            Assert.Equal([Seconds(0), Seconds(2), Seconds(4), Seconds(6)], s_twoSecondGop.Positions.ToArray());
        }

        [Fact]
        public void Create_EmptyInput_ReturnsEmptySingleton()
        {
            Assert.Same(KeyframeIndex.Empty, KeyframeIndex.Create(Array.Empty<TimeSpan>()));
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(2.0, 2.0)]
        [InlineData(3.9, 2.0)]
        [InlineData(100.0, 6.0)]
        public void FindPrevious_ReturnsKeyframeAtOrBefore(double position, double expected)
        {
            Assert.Equal(Seconds(expected), s_twoSecondGop.FindPrevious(Seconds(position)));
        }

        [Fact]
        public void FindPrevious_BeforeFirstKeyframe_ReturnsNull()
        {
            KeyframeIndex index = KeyframeIndex.Create([Seconds(1)]);

            Assert.Null(index.FindPrevious(Seconds(0.5)));
        }

        [Theory]
        [InlineData(0.0, 0.0)]
        [InlineData(0.1, 2.0)]
        [InlineData(4.0, 4.0)]
        public void FindNext_ReturnsKeyframeAtOrAfter(double position, double expected)
        {
            Assert.Equal(Seconds(expected), s_twoSecondGop.FindNext(Seconds(position)));
        }

        [Fact]
        public void FindNext_PastLastKeyframe_ReturnsNull()
        {
            Assert.Null(s_twoSecondGop.FindNext(Seconds(6.1)));
        }

        [Theory]
        [InlineData(2.9, 2.0)]
        [InlineData(3.0, 2.0)]
        [InlineData(3.1, 4.0)]
        public void FindNearest_PrefersCloserKeyframeAndEarlierOnTie(double position, double expected)
        {
            Assert.Equal(Seconds(expected), s_twoSecondGop.FindNearest(Seconds(position)));
        }

        [Fact]
        public void FindAfter_OnKeyframe_ReturnsFollowingKeyframe()
        {
            Assert.Equal(Seconds(4), s_twoSecondGop.FindAfter(Seconds(2)));
        }

        [Fact]
        public void FindBefore_OnKeyframe_ReturnsPrecedingKeyframe()
        {
            Assert.Equal(Seconds(0), s_twoSecondGop.FindBefore(Seconds(2)));
        }

        [Fact]
        public void IsKeyframe_WithinTolerance_ReturnsTrue()
        {
            Assert.True(s_twoSecondGop.IsKeyframe(Seconds(2.005), TimeSpan.FromMilliseconds(8)));
            Assert.False(s_twoSecondGop.IsKeyframe(Seconds(2.02), TimeSpan.FromMilliseconds(8)));
        }

        [Fact]
        public void Lookups_OnEmptyIndex_ReturnNull()
        {
            Assert.Null(KeyframeIndex.Empty.FindPrevious(Seconds(1)));
            Assert.Null(KeyframeIndex.Empty.FindNext(Seconds(1)));
            Assert.Null(KeyframeIndex.Empty.FindNearest(Seconds(1)));
        }

        [Fact]
        public void CountPackets_SumsGopsBetweenKeyframes()
        {
            KeyframeIndex keyframes = KeyframeIndex.Create([new GroupOfPictures(Seconds(0), 60), new GroupOfPictures(Seconds(2), 58), new GroupOfPictures(Seconds(4), 61)]);

            Assert.Equal(118, keyframes.CountPackets(Seconds(0), Seconds(4)));
            Assert.Equal(61, keyframes.CountPackets(Seconds(4), Seconds(100)));
            Assert.Null(keyframes.CountPackets(Seconds(1), Seconds(4)));
            Assert.Null(KeyframeIndex.Create([Seconds(0), Seconds(2)]).CountPackets(Seconds(0), Seconds(2)));
        }

        private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
    }
}
