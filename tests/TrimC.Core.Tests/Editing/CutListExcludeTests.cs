// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using Xunit;

namespace TrimC.Editing.Tests
{
    public class CutListExcludeTests
    {
        [Fact]
        public void Exclude_OnEmptyList_KeepsEverythingElse()
        {
            CutList list = new(Seconds(100));

            Assert.True(list.Exclude(Range(20, 30)));

            Assert.Equal([Range(0, 20), Range(30, 100)], RangesOf(list));
        }

        [Fact]
        public void Exclude_InsideSegment_SplitsItAndKeepsIdentityOnTheLeft()
        {
            CutList list = new(Seconds(100));
            Segment original = list.Add(Range(10, 50), "match");

            list.Exclude(Range(20, 30));

            IReadOnlyList<Segment> segments = list.Segments;
            Assert.Equal([Range(10, 20), Range(30, 50)], RangesOf(list));
            Assert.Equal(original.Id, segments[0].Id);
            Assert.NotEqual(original.Id, segments[1].Id);
            Assert.Equal("match", segments[1].Label);
        }

        [Fact]
        public void Exclude_CoveringSegments_RemovesAndTrimsThem()
        {
            CutList list = new(Seconds(100));
            list.Add(Range(0, 10));
            list.Add(Range(15, 20));
            list.Add(Range(25, 40));

            list.Exclude(Range(5, 30));

            Assert.Equal([Range(0, 5), Range(30, 40)], RangesOf(list));
        }

        [Fact]
        public void Exclude_InGap_ReturnsFalse()
        {
            CutList list = new(Seconds(100));
            list.Add(Range(0, 10));

            Assert.False(list.Exclude(Range(20, 30)));
            Assert.Equal([Range(0, 10)], RangesOf(list));
        }

        [Fact]
        public void Exclude_FromMediaStart_LeavesNoLeadingSegment()
        {
            CutList list = new(Seconds(100));

            list.Exclude(Range(0, 12));

            Assert.Equal([Range(12, 100)], RangesOf(list));
        }

        private static TimeRange[] RangesOf(CutList list)
        {
            IReadOnlyList<Segment> segments = list.Segments;
            TimeRange[] ranges = new TimeRange[segments.Count];
            for (int i = 0; i < segments.Count; i++)
            {
                ranges[i] = segments[i].Range;
            }

            return ranges;
        }

        private static TimeRange Range(double start, double end) => new(Seconds(start), Seconds(end));

        private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
    }
}
