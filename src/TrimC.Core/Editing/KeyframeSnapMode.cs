// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Editing
{
    /// <summary>
    /// Specifies how a segment start is aligned to the keyframe grid before a stream copy export.
    /// </summary>
    public enum KeyframeSnapMode
    {
        /// <summary>
        /// Moves the start back to the previous keyframe. The export contains everything that was
        /// selected plus up to one GOP of leading material. This is the safe default.
        /// </summary>
        Previous = 0,

        /// <summary>
        /// Moves the start forward to the next keyframe. The export never contains material before the
        /// selection but may drop up to one GOP of the selected content.
        /// </summary>
        Next,

        /// <summary>
        /// Moves the start to whichever keyframe is closest.
        /// </summary>
        Nearest,

        /// <summary>
        /// Leaves the start untouched. The muxer still begins at the preceding keyframe, so the output
        /// contains leading frames that are hidden through an edit list or negative timestamps.
        /// </summary>
        None,
    }
}
