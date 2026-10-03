// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Editing
{
    /// <summary>
    /// Identifies one of the two boundaries of a <see cref="Segment"/>.
    /// </summary>
    public enum SegmentEdge
    {
        /// <summary>The inclusive start of the segment.</summary>
        Start = 0,

        /// <summary>The exclusive end of the segment.</summary>
        End,
    }
}
