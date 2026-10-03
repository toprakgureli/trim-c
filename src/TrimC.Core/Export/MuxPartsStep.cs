// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;

namespace TrimC.Export
{
    /// <summary>
    /// Joins the parts of a frame-accurate export into the final file.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Parts are MPEG transport streams whose timestamps already lie on the output timeline, so they are joined byte by
    /// byte rather than re-timed: the video parts form one track and the parts of the other streams form another.
    /// Keeping the audio of each segment in a single part means that the cut points inside a segment, where the video is
    /// stitched together from encoded and copied pieces, never touch the audio.
    /// </para>
    /// <para>
    /// Every part must live in the same directory, because the parts are addressed by file name.
    /// </para>
    /// </remarks>
    public sealed record MuxPartsStep : ExportStep
    {
        /// <summary>
        /// Gets the video parts, in playback order.
        /// </summary>
        public required IReadOnlyList<string> VideoPartPaths { get; init; }

        /// <summary>
        /// Gets the parts that carry the remaining streams, typically audio, in playback order. The list is empty when
        /// only video is exported.
        /// </summary>
        public required IReadOnlyList<string> OtherPartPaths { get; init; }

        /// <summary>
        /// Gets the combined duration of the output.
        /// </summary>
        public required TimeSpan TotalDuration { get; init; }

        /// <summary>
        /// Gets a value indicating whether the index is placed at the start of the file.
        /// </summary>
        public bool FastStart { get; init; }

        /// <inheritdoc/>
        public override TimeSpan Workload => TotalDuration;
    }
}
