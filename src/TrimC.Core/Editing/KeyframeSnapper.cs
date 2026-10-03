// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using TrimC.Media;

namespace TrimC.Editing
{
    /// <summary>
    /// Aligns segment boundaries to the keyframe grid so that stream copy output matches what the user sees.
    /// </summary>
    /// <remarks>
    /// Only the start of a range is snapped. A decoder needs a keyframe to begin but can stop after any frame, so the
    /// end of a stream copy is accurate to within the B-frame reorder depth of the video, typically one or two
    /// frames, and to within one packet for audio.
    /// </remarks>
    public static class KeyframeSnapper
    {
        /// <summary>
        /// Returns <paramref name="range"/> with its start aligned according to <paramref name="mode"/>.
        /// </summary>
        /// <param name="range">The range to align.</param>
        /// <param name="keyframes">The keyframe index of the primary video stream.</param>
        /// <param name="mode">The alignment strategy.</param>
        /// <returns>
        /// The aligned range, or <see langword="null"/> when alignment leaves no content, which happens when
        /// <see cref="KeyframeSnapMode.Next"/> moves the start to or past the end.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="keyframes"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> is not a defined value.</exception>
        public static TimeRange? Snap(TimeRange range, KeyframeIndex keyframes, KeyframeSnapMode mode)
        {
            ArgumentNullException.ThrowIfNull(keyframes);

            // Audio-only media has no keyframe grid; every packet is independently decodable.
            if (keyframes.Count == 0)
            {
                return range;
            }

            TimeSpan? start = mode switch
            {
                KeyframeSnapMode.Previous => keyframes.FindPrevious(range.Start),
                KeyframeSnapMode.Next => keyframes.FindNext(range.Start),
                KeyframeSnapMode.Nearest => keyframes.FindNearest(range.Start),
                KeyframeSnapMode.None => range.Start,
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };

            // A start before the first keyframe can only be served from the first keyframe onwards.
            start ??= keyframes.Positions[0];

            if (start.Value >= range.End)
            {
                return null;
            }

            return new TimeRange(start.Value, range.End);
        }
    }
}
