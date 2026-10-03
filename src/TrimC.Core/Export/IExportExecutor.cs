// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace TrimC.Export
{
    /// <summary>
    /// Executes an <see cref="ExportPlan"/> against a concrete media toolchain.
    /// </summary>
    public interface IExportExecutor
    {
        /// <summary>
        /// Runs every step of the plan in order.
        /// </summary>
        /// <param name="plan">The plan to execute.</param>
        /// <param name="progress">An optional receiver for progress reports.</param>
        /// <param name="cancellationToken">A token that cancels the export.</param>
        /// <returns>A task that completes when every output file has been written.</returns>
        /// <remarks>
        /// Implementations must delete <see cref="ExportPlan.TemporaryFiles"/> whether the plan succeeds, fails or
        /// is canceled, and must delete partially written outputs when it does not succeed.
        /// </remarks>
        Task ExecuteAsync(ExportPlan plan, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default);
    }
}
