// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;

namespace TrimC.Export
{
    /// <summary>
    /// A single unit of work in an <see cref="ExportPlan"/>.
    /// </summary>
    /// <remarks>
    /// Steps describe <em>what</em> must happen in tool-agnostic terms. Translating a step into a concrete
    /// command line is the responsibility of an <see cref="IExportExecutor"/>, which keeps planning logic
    /// independent of FFmpeg and fully unit testable.
    /// </remarks>
    public abstract record ExportStep
    {
        private protected ExportStep()
        {
        }

        /// <summary>
        /// Gets the path of the file the step produces.
        /// </summary>
        public required string OutputPath { get; init; }

        /// <summary>
        /// Gets the container the output is written in.
        /// </summary>
        public required ContainerFormat Container { get; init; }

        /// <summary>
        /// Gets the amount of media time the step processes, used to weight overall progress.
        /// </summary>
        public abstract TimeSpan Workload { get; }
    }
}
