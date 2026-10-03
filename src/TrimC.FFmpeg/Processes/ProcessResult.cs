// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.FFmpeg.Processes
{
    /// <summary>
    /// The outcome of a completed child process.
    /// </summary>
    /// <param name="ExitCode">The process exit code.</param>
    /// <param name="StandardOutput">The captured standard output, or an empty string when output was streamed to a callback.</param>
    /// <param name="StandardError">The tail of the standard error output.</param>
    internal readonly record struct ProcessResult(int ExitCode, string StandardOutput, string StandardError);
}
