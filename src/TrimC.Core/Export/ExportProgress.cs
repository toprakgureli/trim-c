// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Export
{
    /// <summary>
    /// A progress report emitted while an <see cref="ExportPlan"/> executes.
    /// </summary>
    /// <param name="StepNumber">The one-based number of the step being executed.</param>
    /// <param name="StepCount">The total number of steps in the plan.</param>
    /// <param name="Fraction">The completed fraction of the whole plan, between 0 and 1.</param>
    public readonly record struct ExportProgress(int StepNumber, int StepCount, double Fraction);
}
