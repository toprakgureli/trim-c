// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;

namespace TrimC.Desktop.Controls
{
    /// <summary>
    /// The parameter passed to <see cref="TimelineControl.SeekCommand"/> when the user scrubs the timeline.
    /// </summary>
    /// <param name="Position">The requested position.</param>
    /// <param name="IsFinal">
    /// <see langword="false"/> while the pointer is still dragging, when a fast keyframe seek is sufficient;
    /// <see langword="true"/> when the pointer is released and the exact frame should be shown.
    /// </param>
    internal sealed record TimelineSeekRequest(TimeSpan Position, bool IsFinal);
}
