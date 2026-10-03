// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;

namespace TrimC.Editing
{
    /// <summary>
    /// The ordered set of segments selected for export from a single media file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The list maintains two invariants that the rest of the application relies on: segments are sorted by
    /// start position, and no two segments overlap. Every mutating operation validates its input against
    /// these invariants before changing state, so the list is never observed in an inconsistent state.
    /// </para>
    /// <para>
    /// The type is not thread safe. It is owned by the UI thread and handed to the export pipeline as an
    /// immutable snapshot through <see cref="Segments"/>.
    /// </para>
    /// </remarks>
    public sealed class CutList
    {
        private readonly List<Segment> _segments = [];

        /// <summary>
        /// Initializes a new instance of the <see cref="CutList"/> class.
        /// </summary>
        /// <param name="mediaDuration">The duration of the media that segments are bounded by.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mediaDuration"/> is not positive.</exception>
        public CutList(TimeSpan mediaDuration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(mediaDuration, TimeSpan.Zero);

            MediaDuration = mediaDuration;
        }

        /// <summary>
        /// Occurs after any change to the set of segments.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets the duration of the media that bounds every segment.
        /// </summary>
        public TimeSpan MediaDuration { get; }

        /// <summary>
        /// Gets a snapshot of the segments in ascending start order.
        /// </summary>
        public IReadOnlyList<Segment> Segments => _segments.ToArray();

        /// <summary>
        /// Gets the number of segments.
        /// </summary>
        public int Count => _segments.Count;

        /// <summary>
        /// Gets the combined duration of all segments.
        /// </summary>
        public TimeSpan TotalDuration
        {
            get
            {
                TimeSpan total = TimeSpan.Zero;
                foreach (Segment segment in _segments)
                {
                    total += segment.Range.Duration;
                }

                return total;
            }
        }

        /// <summary>
        /// Adds a new segment.
        /// </summary>
        /// <param name="range">The region to cover.</param>
        /// <param name="label">An optional label for the segment.</param>
        /// <returns>The created segment.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> extends past <see cref="MediaDuration"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="range"/> overlaps an existing segment.</exception>
        public Segment Add(TimeRange range, string? label = null)
        {
            EnsureWithinMedia(range);
            EnsureNoOverlap(range, ignoredId: null);

            Segment segment = new() { Id = Guid.NewGuid(), Range = range, Label = label };
            _segments.Insert(FindInsertionIndex(range.Start), segment);
            OnChanged();
            return segment;
        }

        /// <summary>
        /// Removes the segment with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier of the segment to remove.</param>
        /// <returns><see langword="true"/> if a segment was removed; otherwise, <see langword="false"/>.</returns>
        public bool Remove(Guid id)
        {
            int index = IndexOf(id);
            if (index < 0)
            {
                return false;
            }

            _segments.RemoveAt(index);
            OnChanged();
            return true;
        }

        /// <summary>
        /// Removes every segment.
        /// </summary>
        public void Clear()
        {
            if (_segments.Count == 0)
            {
                return;
            }

            _segments.Clear();
            OnChanged();
        }

        /// <summary>
        /// Replaces the range of an existing segment, keeping its identity and label.
        /// </summary>
        /// <param name="id">The identifier of the segment to change.</param>
        /// <param name="range">The new range.</param>
        /// <returns>The updated segment.</returns>
        /// <exception cref="KeyNotFoundException">No segment has the identifier <paramref name="id"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> extends past <see cref="MediaDuration"/>.</exception>
        /// <exception cref="InvalidOperationException"><paramref name="range"/> overlaps another segment.</exception>
        public Segment SetRange(Guid id, TimeRange range)
        {
            int index = IndexOf(id);
            if (index < 0)
            {
                throw new KeyNotFoundException($"No segment with id '{id}' exists.");
            }

            EnsureWithinMedia(range);
            EnsureNoOverlap(range, ignoredId: id);

            Segment updated = _segments[index] with { Range = range };

            // The start may have moved past a neighbour, so the segment is re-inserted to keep the order.
            _segments.RemoveAt(index);
            _segments.Insert(FindInsertionIndex(range.Start), updated);
            OnChanged();
            return updated;
        }

        /// <summary>
        /// Replaces the label of an existing segment.
        /// </summary>
        /// <param name="id">The identifier of the segment to change.</param>
        /// <param name="label">The new label, or <see langword="null"/> to clear it.</param>
        /// <returns>The updated segment.</returns>
        /// <exception cref="KeyNotFoundException">No segment has the identifier <paramref name="id"/>.</exception>
        public Segment SetLabel(Guid id, string? label)
        {
            int index = IndexOf(id);
            if (index < 0)
            {
                throw new KeyNotFoundException($"No segment with id '{id}' exists.");
            }

            Segment updated = _segments[index] with { Label = label };
            _segments[index] = updated;
            OnChanged();
            return updated;
        }

        /// <summary>
        /// Splits the segment that contains <paramref name="position"/> into two adjacent segments.
        /// </summary>
        /// <param name="position">The split point. It must lie strictly inside a segment.</param>
        /// <returns>
        /// The two resulting segments, or <see langword="null"/> when no segment strictly contains
        /// <paramref name="position"/>. The first segment keeps the original identity.
        /// </returns>
        public (Segment First, Segment Second)? Split(TimeSpan position)
        {
            int index = _segments.FindIndex(s => s.Range.Start < position && position < s.Range.End);
            if (index < 0)
            {
                return null;
            }

            Segment original = _segments[index];
            Segment first = original with { Range = new TimeRange(original.Range.Start, position) };
            Segment second = new()
            {
                Id = Guid.NewGuid(),
                Range = new TimeRange(position, original.Range.End),
                Label = original.Label,
            };

            _segments[index] = first;
            _segments.Insert(index + 1, second);
            OnChanged();
            return (first, second);
        }

        /// <summary>
        /// Removes a range from the selection, the way a film or broadcast editor cuts out the frames between two marks.
        /// </summary>
        /// <param name="range">The range to remove.</param>
        /// <returns><see langword="true"/> if any selected content was removed; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="range"/> extends past <see cref="MediaDuration"/>.</exception>
        /// <remarks>
        /// An empty list stands for "nothing selected yet", so the first exclusion starts from the whole media and the
        /// result keeps everything except <paramref name="range"/>. Segments that the range splits in two keep their
        /// identity and label on the left part; the right part becomes a new segment with the same label.
        /// </remarks>
        public bool Exclude(TimeRange range)
        {
            EnsureWithinMedia(range);

            if (_segments.Count == 0)
            {
                _segments.Add(new Segment { Id = Guid.NewGuid(), Range = new TimeRange(TimeSpan.Zero, MediaDuration) });
            }

            List<Segment> result = new(_segments.Count + 1);
            bool changed = false;

            foreach (Segment segment in _segments)
            {
                if (!segment.Range.Overlaps(range))
                {
                    result.Add(segment);
                    continue;
                }

                changed = true;
                bool hasLeft = segment.Range.Start < range.Start;
                if (hasLeft)
                {
                    result.Add(segment with { Range = new TimeRange(segment.Range.Start, range.Start) });
                }

                if (range.End < segment.Range.End)
                {
                    TimeRange right = new(range.End, segment.Range.End);
                    result.Add(hasLeft
                        ? new Segment { Id = Guid.NewGuid(), Range = right, Label = segment.Label }
                        : segment with { Range = right });
                }
            }

            _segments.Clear();
            _segments.AddRange(result);
            OnChanged();
            return changed;
        }

        /// <summary>
        /// Finds the segment that contains the specified position.
        /// </summary>
        /// <param name="position">The position to look up.</param>
        /// <returns>The containing segment, or <see langword="null"/> if the position falls in a gap.</returns>
        public Segment? FindAt(TimeSpan position) => _segments.Find(s => s.Range.Contains(position));

        /// <summary>
        /// Finds the segment with the specified identifier.
        /// </summary>
        /// <param name="id">The identifier to look up.</param>
        /// <returns>The segment, or <see langword="null"/> if it does not exist.</returns>
        public Segment? Find(Guid id)
        {
            int index = IndexOf(id);
            return index >= 0 ? _segments[index] : null;
        }

        /// <summary>
        /// Computes the parts of the media that are not covered by any segment.
        /// </summary>
        /// <remarks>
        /// This supports the "mark what to remove" workflow: the user selects unwanted parts and the
        /// complement becomes the set of ranges that is actually exported.
        /// </remarks>
        /// <returns>The uncovered ranges in ascending order.</returns>
        public IReadOnlyList<TimeRange> GetGaps()
        {
            List<TimeRange> gaps = [];
            TimeSpan cursor = TimeSpan.Zero;

            foreach (Segment segment in _segments)
            {
                if (segment.Range.Start > cursor)
                {
                    gaps.Add(new TimeRange(cursor, segment.Range.Start));
                }

                cursor = segment.Range.End;
            }

            if (cursor < MediaDuration)
            {
                gaps.Add(new TimeRange(cursor, MediaDuration));
            }

            return gaps;
        }

        /// <summary>
        /// Replaces every segment with the current gaps, inverting the selection.
        /// </summary>
        public void Invert()
        {
            IReadOnlyList<TimeRange> gaps = GetGaps();

            _segments.Clear();
            foreach (TimeRange gap in gaps)
            {
                _segments.Add(new Segment { Id = Guid.NewGuid(), Range = gap });
            }

            OnChanged();
        }

        /// <summary>
        /// Moves one boundary of a segment, keeping every invariant of the list intact.
        /// </summary>
        /// <param name="id">The identifier of the segment to change.</param>
        /// <param name="edge">The boundary to move.</param>
        /// <param name="position">The requested position of the boundary.</param>
        /// <param name="minimumDuration">The shortest length the segment may be given, typically one frame.</param>
        /// <returns>The updated segment.</returns>
        /// <exception cref="KeyNotFoundException">No segment has the identifier <paramref name="id"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="minimumDuration"/> is negative.</exception>
        /// <remarks>
        /// Unlike <see cref="SetRange"/>, this method never fails for an out-of-range position. The boundary is clamped
        /// to the media, to the neighbouring segments and to <paramref name="minimumDuration"/>, which is what a trim
        /// handle dragged by the pointer needs: the handle stops at an obstacle instead of rejecting the whole gesture.
        /// </remarks>
        public Segment MoveEdge(Guid id, SegmentEdge edge, TimeSpan position, TimeSpan minimumDuration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(minimumDuration, TimeSpan.Zero);

            int index = IndexOf(id);
            if (index < 0)
            {
                throw new KeyNotFoundException($"No segment with id '{id}' exists.");
            }

            TimeRange range = _segments[index].Range;
            TimeSpan lowerBound = index > 0 ? _segments[index - 1].Range.End : TimeSpan.Zero;
            TimeSpan upperBound = index < _segments.Count - 1 ? _segments[index + 1].Range.Start : MediaDuration;

            // The minimum length never overrides a neighbour or the media bounds: a segment that is already shorter than
            // the minimum can still be moved within the space that is actually free, but never into another segment.
            // A boundary also never reaches the opposite one, because a range must not be empty.
            TimeSpan length = Max(minimumDuration, TimeSpan.FromTicks(1));
            TimeRange updatedRange;
            if (edge == SegmentEdge.Start)
            {
                TimeSpan latestStart = Max(range.End - length, lowerBound);
                updatedRange = new TimeRange(Clamp(position, lowerBound, latestStart), range.End);
            }
            else
            {
                TimeSpan earliestEnd = Min(range.Start + length, upperBound);
                updatedRange = new TimeRange(range.Start, Clamp(position, earliestEnd, upperBound));
            }

            if (updatedRange == range)
            {
                return _segments[index];
            }

            Segment updated = _segments[index] with { Range = updatedRange };
            _segments[index] = updated;
            OnChanged();
            return updated;
        }

        /// <summary>
        /// Replaces every segment with a previously captured state, as used by undo and redo.
        /// </summary>
        /// <param name="segments">The segments to restore, in ascending start order.</param>
        /// <exception cref="ArgumentNullException"><paramref name="segments"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">The segments are unsorted, overlap or extend past <see cref="MediaDuration"/>.</exception>
        public void Restore(IReadOnlyList<Segment> segments)
        {
            ArgumentNullException.ThrowIfNull(segments);

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].Range.End > MediaDuration || (i > 0 && segments[i].Range.Start < segments[i - 1].Range.End))
                {
                    throw new ArgumentException("The segments must be sorted, must not overlap and must lie within the media.", nameof(segments));
                }
            }

            _segments.Clear();
            _segments.AddRange(segments);
            OnChanged();
        }

        private static TimeSpan Clamp(TimeSpan value, TimeSpan minimum, TimeSpan maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;

        private static TimeSpan Min(TimeSpan a, TimeSpan b) => a < b ? a : b;

        private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

        private int IndexOf(Guid id) => _segments.FindIndex(s => s.Id == id);

        private int FindInsertionIndex(TimeSpan start)
        {
            int index = _segments.FindIndex(s => s.Range.Start > start);
            return index < 0 ? _segments.Count : index;
        }

        private void EnsureWithinMedia(TimeRange range)
        {
            if (range.End > MediaDuration)
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "The range extends past the end of the media.");
            }
        }

        private void EnsureNoOverlap(TimeRange range, Guid? ignoredId)
        {
            foreach (Segment segment in _segments)
            {
                if (segment.Id != ignoredId && segment.Range.Overlaps(range))
                {
                    throw new InvalidOperationException($"The range {range} overlaps the existing segment {segment.Range}.");
                }
            }
        }

        private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
