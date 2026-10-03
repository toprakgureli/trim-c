// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using Xunit;

namespace TrimC.Editing.Tests
{
    public class EditHistoryTests
    {
        private static readonly TimeSpan s_frame = TimeSpan.FromSeconds(1.0 / 60);

        [Fact]
        public void Undo_AfterExecute_RestoresPreviousStateAndRedoReappliesIt()
        {
            CutList list = new(Seconds(100));
            EditHistory history = new(list);

            history.Execute(l => l.Add(Range(0, 10)));
            history.Execute(l => l.Add(Range(20, 30)));

            Assert.True(history.Undo());
            Assert.Equal([Range(0, 10)], RangesOf(list));

            Assert.True(history.Undo());
            Assert.Empty(list.Segments);
            Assert.False(history.CanUndo);

            Assert.True(history.Redo());
            Assert.True(history.Redo());
            Assert.Equal([Range(0, 10), Range(20, 30)], RangesOf(list));
            Assert.False(history.CanRedo);
        }

        [Fact]
        public void Execute_AfterUndo_DiscardsRedoSteps()
        {
            CutList list = new(Seconds(100));
            EditHistory history = new(list);
            history.Execute(l => l.Add(Range(0, 10)));
            history.Undo();

            history.Execute(l => l.Add(Range(50, 60)));

            Assert.False(history.CanRedo);
        }

        [Fact]
        public void Execute_FailingEdit_RecordsNothing()
        {
            CutList list = new(Seconds(100));
            EditHistory history = new(list);
            history.Execute(l => l.Add(Range(0, 10)));

            Assert.Throws<InvalidOperationException>(() => history.Execute(l => l.Add(Range(5, 15))));

            history.Undo();
            Assert.Empty(list.Segments);
            Assert.False(history.CanUndo);
        }

        [Fact]
        public void Execute_EditWithoutEffect_RecordsNothing()
        {
            CutList list = new(Seconds(100));
            EditHistory history = new(list);

            history.Execute(l => l.Split(Seconds(50)));

            Assert.False(history.CanUndo);
        }

        [Fact]
        public void CaptureAndCommit_RecordContinuousGestureAsOneStep()
        {
            CutList list = new(Seconds(100));
            EditHistory history = new(list);
            Segment segment = list.Add(Range(10, 20));

            IReadOnlyList<Segment> before = history.Capture();
            for (int i = 1; i <= 5; i++)
            {
                list.MoveEdge(segment.Id, SegmentEdge.End, Seconds(20 + i), s_frame);
            }

            history.Commit(before);

            history.Undo();
            Assert.Equal([Range(10, 20)], RangesOf(list));
            Assert.False(history.CanUndo);
        }

        [Fact]
        public void Capacity_DiscardsOldestSteps()
        {
            CutList list = new(Seconds(100));
            EditHistory history = new(list, capacity: 2);

            history.Execute(l => l.Add(Range(0, 1)));
            history.Execute(l => l.Add(Range(2, 3)));
            history.Execute(l => l.Add(Range(4, 5)));

            Assert.True(history.Undo());
            Assert.True(history.Undo());
            Assert.False(history.Undo());
            Assert.Equal([Range(0, 1)], RangesOf(list));
        }

        [Fact]
        public void MoveEdge_StopsAtNeighboursAndMediaBounds()
        {
            CutList list = new(Seconds(100));
            list.Add(Range(0, 10));
            Segment middle = list.Add(Range(20, 30));
            list.Add(Range(40, 50));

            Assert.Equal(Range(10, 30), list.MoveEdge(middle.Id, SegmentEdge.Start, Seconds(5), s_frame).Range);
            Assert.Equal(Range(10, 40), list.MoveEdge(middle.Id, SegmentEdge.End, Seconds(45), s_frame).Range);
        }

        [Fact]
        public void MoveEdge_KeepsMinimumLength()
        {
            CutList list = new(Seconds(100));
            Segment segment = list.Add(Range(10, 20));

            Assert.Equal(new TimeRange(Seconds(20) - s_frame, Seconds(20)), list.MoveEdge(segment.Id, SegmentEdge.Start, Seconds(25), s_frame).Range);
        }

        [Fact]
        public void MoveEdge_ShortSegmentNextToNeighbour_NeverOverlaps()
        {
            CutList list = new(Seconds(100));
            Segment shortSegment = list.Add(new TimeRange(Seconds(10), Seconds(10) + TimeSpan.FromMilliseconds(5)));
            list.Add(new TimeRange(Seconds(10) + TimeSpan.FromMilliseconds(5), Seconds(20)));

            Segment moved = list.MoveEdge(shortSegment.Id, SegmentEdge.End, Seconds(0), s_frame);

            Assert.True(moved.Range.End <= list.Segments[1].Range.Start);
            Assert.True(moved.Range.End > moved.Range.Start);
        }

        [Fact]
        public void Restore_InvalidState_Throws()
        {
            CutList list = new(Seconds(100));

            Assert.Throws<ArgumentException>(() => list.Restore(
            [
                new Segment { Id = Guid.NewGuid(), Range = Range(0, 10) },
                new Segment { Id = Guid.NewGuid(), Range = Range(5, 15) },
            ]));
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
