// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Globalization;

namespace TrimC.Editing
{
    /// <summary>
    /// A half-open interval <c>[Start, End)</c> on the media timeline.
    /// </summary>
    /// <remarks>
    /// Half-open semantics allow adjacent ranges such as <c>[0, 5)</c> and <c>[5, 10)</c> to touch without
    /// overlapping, which is exactly what splitting a segment produces.
    /// </remarks>
    public readonly record struct TimeRange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TimeRange"/> struct.
        /// </summary>
        /// <param name="start">The inclusive start position.</param>
        /// <param name="end">The exclusive end position.</param>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="start"/> is negative, or <paramref name="end"/> is not greater than <paramref name="start"/>.
        /// </exception>
        public TimeRange(TimeSpan start, TimeSpan end)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(start, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(end, start);

            Start = start;
            End = end;
        }

        /// <summary>
        /// Gets the inclusive start position.
        /// </summary>
        public TimeSpan Start { get; }

        /// <summary>
        /// Gets the exclusive end position.
        /// </summary>
        public TimeSpan End { get; }

        /// <summary>
        /// Gets the length of the range.
        /// </summary>
        public TimeSpan Duration => End - Start;

        /// <summary>
        /// Determines whether the specified position lies inside the range.
        /// </summary>
        /// <param name="position">The position to test.</param>
        /// <returns><see langword="true"/> if <paramref name="position"/> is in <c>[Start, End)</c>; otherwise, <see langword="false"/>.</returns>
        public bool Contains(TimeSpan position) => position >= Start && position < End;

        /// <summary>
        /// Determines whether this range shares any position with another range.
        /// </summary>
        /// <param name="other">The range to compare against.</param>
        /// <returns><see langword="true"/> if the ranges overlap; otherwise, <see langword="false"/>.</returns>
        public bool Overlaps(TimeRange other) => Start < other.End && other.Start < End;

        /// <inheritdoc/>
        public override string ToString() =>
            string.Create(CultureInfo.InvariantCulture, $"[{Start:c}, {End:c})");
    }
}
