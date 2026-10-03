// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using Xunit;

namespace TrimC.Editing.Tests
{
    public class CutListTests
    {
        [Fact]
        public void Add_OutOfOrder_KeepsSegmentsSortedByStart()
        {
            CutList list = new(Seconds(100));

            list.Add(Range(50, 60));
            list.Add(Range(10, 20));
            list.Add(Range(30, 40));

            Assert.Equal([Range(10, 20), Range(30, 40), Range(50, 60)], RangesOf(list));
        }

        [Fact]
        public void Add_OverlappingRange_ThrowsAndLeavesListUnchanged()
        {
            CutList list = new(Seconds(100));
            list.Add(Range(10, 20));

            Assert.Throws<InvalidOperationException>(() => list.Add(Range(15, 25)));
            Assert.Equal(1, list.Count);
        }

        [Fact]
        public void Add_AdjacentRange_IsAllowed()
        {
            CutList list = new(Seconds(100));
            list.Add(Range(10, 20));

            list.Add(Range(20, 30));

            Assert.Equal(2, list.Count);
        }

        [Fact]
        public void Add_PastMediaDuration_Throws()
        {
            CutList list = new(Seconds(100));

            Assert.Throws<ArgumentOutOfRangeException>(() => list.Add(Range(90, 101)));
        }

        [Fact]
        public void SetRange_MovingPastNeighbour_ReordersAndKeepsIdentity()
        {
            CutList list = new(Seconds(100));
            Segment first = list.Add(Range(10, 20), "intro");
            list.Add(Range(30, 40));

            Segment moved = list.SetRange(first.Id, Range(50, 60));

            Assert.Equal(first.Id, moved.Id);
            Assert.Equal("intro", moved.Label);
            Assert.Equal([Range(30, 40), Range(50, 60)], RangesOf(list));
        }

        [Fact]
        public void SetRange_OverlappingAnotherSegment_Throws()
        {
            CutList list = new(Seconds(100));
            Segment first = list.Add(Range(10, 20));
            list.Add(Range(30, 40));

            Assert.Throws<InvalidOperationException>(() => list.SetRange(first.Id, Range(10, 35)));
        }

        [Fact]
        public void SetRange_UnknownId_Throws()
        {
            CutList list = new(Seconds(100));

            Assert.Throws<KeyNotFoundException>(() => list.SetRange(Guid.NewGuid(), Range(0, 1)));
        }

        [Fact]
        public void Split_InsideSegment_ProducesAdjacentSegments()
        {
            CutList list = new(Seconds(100));
            Segment original = list.Add(Range(10, 30), "clip");

            (Segment First, Segment Second)? result = list.Split(Seconds(18));

            Assert.NotNull(result);
            Assert.Equal(original.Id, result.Value.First.Id);
            Assert.NotEqual(original.Id, result.Value.Second.Id);
            Assert.Equal("clip", result.Value.Second.Label);
            Assert.Equal([Range(10, 18), Range(18, 30)], RangesOf(list));
        }

        [Fact]
        public void SetMuted_ChangesOnlyTheSoundAndSurvivesSplitting()
        {
            CutList list = new(Seconds(100));
            Segment original = list.Add(Range(10, 30), "clip");
            int changes = 0;
            list.Changed += (_, _) => changes++;

            Segment muted = list.SetMuted(original.Id, isMuted: true);
            (Segment First, Segment Second)? halves = list.Split(Seconds(20));

            Assert.Equal(original with { IsMuted = true }, muted);
            Assert.True(halves!.Value.First.IsMuted);
            Assert.True(halves.Value.Second.IsMuted);
            Assert.Equal(2, changes);
            Assert.Throws<KeyNotFoundException>(() => list.SetMuted(Guid.NewGuid(), isMuted: true));
        }

        [Theory]
        [InlineData(10)]
        [InlineData(30)]
        [InlineData(50)]
        public void Split_OnBoundaryOrGap_ReturnsNull(double position)
        {
            CutList list = new(Seconds(100));
            list.Add(Range(10, 30));

            Assert.Null(list.Split(Seconds(position)));
            Assert.Equal(1, list.Count);
        }

        [Fact]
        public void GetGaps_ReturnsUncoveredRanges()
        {
            CutList list = new(Seconds(100));
            list.Add(Range(10, 20));
            list.Add(Range(20, 30));
            list.Add(Range(60, 100));

            Assert.Equal([Range(0, 10), Range(30, 60)], list.GetGaps());
        }

        [Fact]
        public void Invert_ReplacesSegmentsWithGaps()
        {
            CutList list = new(Seconds(100));
            list.Add(Range(0, 10));
            list.Add(Range(40, 50));

            list.Invert();

            Assert.Equal([Range(10, 40), Range(50, 100)], RangesOf(list));
        }

        [Fact]
        public void Changed_IsRaisedForEveryMutation()
        {
            CutList list = new(Seconds(100));
            int raised = 0;
            list.Changed += (_, _) => raised++;

            Segment segment = list.Add(Range(10, 20));
            list.SetLabel(segment.Id, "x");
            list.Split(Seconds(15));
            list.Remove(segment.Id);
            list.Clear();

            Assert.Equal(5, raised);
        }

        [Fact]
        public void TotalDuration_SumsSegments()
        {
            CutList list = new(Seconds(100));
            list.Add(Range(0, 10));
            list.Add(Range(40, 45));

            Assert.Equal(Seconds(15), list.TotalDuration);
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
