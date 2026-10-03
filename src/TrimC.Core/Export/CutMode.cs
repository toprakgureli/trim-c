// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Export
{
    /// <summary>
    /// Specifies how precisely segment boundaries are honoured.
    /// </summary>
    public enum CutMode
    {
        /// <summary>
        /// Segment starts are aligned to keyframes and every packet is copied. The export is bit-identical to the
        /// source and runs at disk speed, but a start can move by up to one GOP.
        /// </summary>
        Keyframe = 0,

        /// <summary>
        /// Segments start and end on exactly the chosen frames, as in broadcast and film editing. Only the partial
        /// GOPs at the two cut points are re-encoded at near-lossless quality; everything between the first and the
        /// last keyframe inside the segment is copied unchanged.
        /// </summary>
        FrameAccurate,
    }
}
