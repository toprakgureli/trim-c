// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;

namespace TrimC.Export
{
    /// <summary>
    /// An ordered, fully resolved description of the work required to perform an export.
    /// </summary>
    public sealed record ExportPlan
    {
        /// <summary>
        /// Gets the steps to execute, in order. Later steps may consume the output of earlier ones.
        /// </summary>
        public required IReadOnlyList<ExportStep> Steps { get; init; }

        /// <summary>
        /// Gets the files the user asked for. These are kept when the plan completes.
        /// </summary>
        public required IReadOnlyList<string> OutputFiles { get; init; }

        /// <summary>
        /// Gets intermediate files that must be deleted once the plan completes or fails.
        /// </summary>
        public required IReadOnlyList<string> TemporaryFiles { get; init; }

        /// <summary>
        /// Gets the outputs that may replace an existing file, because the user chose that file as the destination.
        /// </summary>
        /// <remarks>
        /// An executor writes these under a temporary name and moves them over the existing file only once the whole
        /// plan has succeeded, so a failed or canceled export leaves the original untouched.
        /// </remarks>
        public IReadOnlyList<string> ReplaceableFiles { get; init; } = [];

        /// <summary>
        /// Gets the total workload of all steps, used as the denominator for overall progress.
        /// </summary>
        public TimeSpan TotalWorkload
        {
            get
            {
                TimeSpan total = TimeSpan.Zero;
                foreach (ExportStep step in Steps)
                {
                    total += step.Workload;
                }

                return total;
            }
        }
    }
}
