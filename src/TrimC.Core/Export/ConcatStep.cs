// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;

namespace TrimC.Export
{
    /// <summary>
    /// Joins previously produced files with identical stream layouts into a single file.
    /// </summary>
    public sealed record ConcatStep : ExportStep
    {
        /// <summary>
        /// Gets the files to join, in playback order.
        /// </summary>
        public required IReadOnlyList<string> InputPaths { get; init; }

        /// <summary>
        /// Gets the combined duration of the inputs.
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
