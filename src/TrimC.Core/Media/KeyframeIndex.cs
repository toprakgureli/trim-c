// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;

namespace TrimC.Media
{
    /// <summary>
    /// An immutable, sorted index of keyframe positions for a single video stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Stream copy can only begin decoding at a keyframe, so every lossless cut decision is made against
    /// this index. Positions are relative to <see cref="MediaInfo.StartTime"/> and lookups are
    /// O(log n) binary searches, which keeps snapping cheap enough to run on every pointer move.
    /// </para>
    /// <para>
    /// An index built from <see cref="GroupOfPictures"/> values also knows how many packets each GOP contains. Frame
    /// accurate exports need that number: with B-frames, a time-based stream copy ends a few packets late because the
    /// first packets of the next GOP are decoded before the last frames of the current one are shown.
    /// </para>
    /// </remarks>
    public sealed class KeyframeIndex
    {
        private readonly TimeSpan[] _positions;
        private readonly int[]? _packetCounts;

        private KeyframeIndex(TimeSpan[] positions, int[]? packetCounts)
        {
            _positions = positions;
            _packetCounts = packetCounts;
        }

        /// <summary>
        /// Gets an index that contains no keyframes, used for audio-only media where any position is a valid cut point.
        /// </summary>
        public static KeyframeIndex Empty { get; } = new KeyframeIndex([], null);

        /// <summary>
        /// Gets the number of keyframes in the index.
        /// </summary>
        public int Count => _positions.Length;

        /// <summary>
        /// Gets the keyframe positions in ascending order.
        /// </summary>
        public ReadOnlySpan<TimeSpan> Positions => _positions;

        /// <summary>
        /// Creates an index from an arbitrary sequence of keyframe positions.
        /// </summary>
        /// <param name="positions">The keyframe positions. Order does not matter and duplicates are removed.</param>
        /// <returns>A new <see cref="KeyframeIndex"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="positions"/> is <see langword="null"/>.</exception>
        public static KeyframeIndex Create(IEnumerable<TimeSpan> positions)
        {
            ArgumentNullException.ThrowIfNull(positions);

            // Demuxers report packets in decode order, which differs from presentation order when
            // B-frames are present, so the input is normalized rather than trusted to be sorted.
            SortedSet<TimeSpan> unique = new(positions);
            if (unique.Count == 0)
            {
                return Empty;
            }

            TimeSpan[] sorted = new TimeSpan[unique.Count];
            unique.CopyTo(sorted);
            return new KeyframeIndex(sorted, null);
        }

        /// <summary>
        /// Creates an index that also records the number of packets in each group of pictures.
        /// </summary>
        /// <param name="groups">The groups of pictures. Order does not matter; for duplicate starts the first entry wins.</param>
        /// <returns>A new <see cref="KeyframeIndex"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="groups"/> is <see langword="null"/>.</exception>
        public static KeyframeIndex Create(IEnumerable<GroupOfPictures> groups)
        {
            ArgumentNullException.ThrowIfNull(groups);

            SortedDictionary<TimeSpan, int> ordered = [];
            foreach (GroupOfPictures group in groups)
            {
                ordered.TryAdd(group.Start, group.PacketCount);
            }

            if (ordered.Count == 0)
            {
                return Empty;
            }

            TimeSpan[] positions = new TimeSpan[ordered.Count];
            int[] counts = new int[ordered.Count];
            ordered.Keys.CopyTo(positions, 0);
            ordered.Values.CopyTo(counts, 0);
            return new KeyframeIndex(positions, counts);
        }

        /// <summary>
        /// Counts the video packets between two keyframes.
        /// </summary>
        /// <param name="fromKeyframe">The keyframe at which counting starts, inclusive.</param>
        /// <param name="toKeyframe">The position at which counting stops; groups starting at or after it are excluded.</param>
        /// <returns>
        /// The number of packets, or <see langword="null"/> when the index carries no packet counts or
        /// <paramref name="fromKeyframe"/> is not a keyframe.
        /// </returns>
        public int? CountPackets(TimeSpan fromKeyframe, TimeSpan toKeyframe)
        {
            if (_packetCounts is null)
            {
                return null;
            }

            int index = Array.BinarySearch(_positions, fromKeyframe);
            if (index < 0)
            {
                return null;
            }

            int total = 0;
            for (; index < _positions.Length && _positions[index] < toKeyframe; index++)
            {
                total += _packetCounts[index];
            }

            return total;
        }

        /// <summary>
        /// Finds the last keyframe at or before the specified position.
        /// </summary>
        /// <param name="position">The position to search from.</param>
        /// <returns>The keyframe position, or <see langword="null"/> when no keyframe precedes <paramref name="position"/>.</returns>
        public TimeSpan? FindPrevious(TimeSpan position)
        {
            int index = Array.BinarySearch(_positions, position);
            if (index >= 0)
            {
                return _positions[index];
            }

            // A negative result is the bitwise complement of the index of the first larger element.
            int previous = ~index - 1;
            return previous >= 0 ? _positions[previous] : null;
        }

        /// <summary>
        /// Finds the first keyframe at or after the specified position.
        /// </summary>
        /// <param name="position">The position to search from.</param>
        /// <returns>The keyframe position, or <see langword="null"/> when no keyframe follows <paramref name="position"/>.</returns>
        public TimeSpan? FindNext(TimeSpan position)
        {
            int index = Array.BinarySearch(_positions, position);
            if (index >= 0)
            {
                return _positions[index];
            }

            int next = ~index;
            return next < _positions.Length ? _positions[next] : null;
        }

        /// <summary>
        /// Finds the keyframe closest to the specified position. Ties resolve to the earlier keyframe.
        /// </summary>
        /// <param name="position">The position to search from.</param>
        /// <returns>The keyframe position, or <see langword="null"/> when the index is empty.</returns>
        public TimeSpan? FindNearest(TimeSpan position)
        {
            TimeSpan? previous = FindPrevious(position);
            TimeSpan? next = FindNext(position);

            if (previous is null)
            {
                return next;
            }

            if (next is null)
            {
                return previous;
            }

            return position - previous.Value <= next.Value - position ? previous : next;
        }

        /// <summary>
        /// Finds the first keyframe strictly after the specified position.
        /// </summary>
        /// <param name="position">The position to search from.</param>
        /// <returns>The keyframe position, or <see langword="null"/> when <paramref name="position"/> is at or past the last keyframe.</returns>
        public TimeSpan? FindAfter(TimeSpan position)
        {
            int index = Array.BinarySearch(_positions, position);
            int next = index >= 0 ? index + 1 : ~index;
            return next < _positions.Length ? _positions[next] : null;
        }

        /// <summary>
        /// Finds the last keyframe strictly before the specified position.
        /// </summary>
        /// <param name="position">The position to search from.</param>
        /// <returns>The keyframe position, or <see langword="null"/> when <paramref name="position"/> is at or before the first keyframe.</returns>
        public TimeSpan? FindBefore(TimeSpan position)
        {
            int index = Array.BinarySearch(_positions, position);
            int previous = index >= 0 ? index - 1 : ~index - 1;
            return previous >= 0 ? _positions[previous] : null;
        }

        /// <summary>
        /// Determines whether a keyframe exists within <paramref name="tolerance"/> of the specified position.
        /// </summary>
        /// <param name="position">The position to test.</param>
        /// <param name="tolerance">The maximum allowed distance, typically half a frame duration.</param>
        /// <returns><see langword="true"/> if a keyframe lies within the tolerance; otherwise, <see langword="false"/>.</returns>
        public bool IsKeyframe(TimeSpan position, TimeSpan tolerance)
        {
            TimeSpan? nearest = FindNearest(position);
            return nearest is not null && (nearest.Value - position).Duration() <= tolerance;
        }
    }
}
