// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using TrimC.Editing;

namespace TrimC.Desktop.Controls
{
    /// <summary>
    /// The parameter passed to <see cref="TimelineControl.TrimCommand"/> while a trim handle is dragged.
    /// </summary>
    /// <param name="SegmentId">The segment whose boundary is dragged.</param>
    /// <param name="Edge">The boundary being dragged.</param>
    /// <param name="Position">The timeline position under the pointer.</param>
    /// <param name="Phase">The stage of the drag.</param>
    internal sealed record TimelineTrimRequest(Guid SegmentId, SegmentEdge Edge, TimeSpan Position, TrimPhase Phase);
}
