// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TrimC.FFmpeg.Processes
{
    /// <summary>
    /// Runs a command line tool to completion while consuming its output streams concurrently.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Both pipes are drained in parallel for the whole lifetime of the process. Reading them one after the other
    /// can deadlock: the child blocks once the unread pipe buffer fills, and never exits.
    /// </para>
    /// <para>
    /// Arguments are passed through <see cref="ProcessStartInfo.ArgumentList"/>, which applies the platform's
    /// quoting rules. File paths containing spaces, quotes or non-ASCII characters therefore never need manual escaping.
    /// </para>
    /// </remarks>
    internal static class ProcessRunner
    {
        // FFmpeg can log a line per frame on failure; only the tail is needed to diagnose the cause.
        private const int MaxStandardErrorLength = 32 * 1024;

        /// <summary>
        /// Starts a process and waits for it to exit.
        /// </summary>
        /// <param name="fileName">The executable to run.</param>
        /// <param name="arguments">The arguments, passed verbatim without shell interpretation.</param>
        /// <param name="standardOutputLine">
        /// A callback invoked for every line of standard output, or <see langword="null"/> to capture standard output
        /// into <see cref="ProcessResult.StandardOutput"/>.
        /// </param>
        /// <param name="cancellationToken">A token that terminates the process tree when canceled.</param>
        /// <param name="workingDirectory">The working directory of the process, or <see langword="null"/> to inherit it.</param>
        /// <returns>The exit code and captured output.</returns>
        /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was canceled.</exception>
        /// <exception cref="FFmpegException">The executable could not be started.</exception>
        public static async Task<ProcessResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            Action<string>? standardOutputLine,
            CancellationToken cancellationToken,
            string? workingDirectory = null)
        {
            ProcessStartInfo startInfo = new(fileName)
            {
                WorkingDirectory = workingDirectory ?? string.Empty,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using Process process = new() { StartInfo = startInfo };
            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                // The executable exists but cannot run, for example because it was built for another architecture or
                // an antivirus product blocked it. Callers handle FFmpegException, so the failure is reported, not fatal.
                throw new FFmpegException($"Could not start '{fileName}': {ex.Message}", ex);
            }

            StringBuilder captured = new();
            Action<string> onOutput = standardOutputLine ?? (line => captured.AppendLine(line));

            Task<string> errorTask = ReadTailAsync(process.StandardError, cancellationToken);
            Task outputTask = ReadLinesAsync(process.StandardOutput, onOutput, cancellationToken);

            try
            {
                await Task.WhenAll(outputTask, errorTask).ConfigureAwait(false);
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Terminate(process);
                throw;
            }

            return new ProcessResult(process.ExitCode, captured.ToString(), await errorTask.ConfigureAwait(false));
        }

        private static async Task ReadLinesAsync(StreamReader reader, Action<string> onLine, CancellationToken cancellationToken)
        {
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is string line)
            {
                onLine(line);
            }
        }

        private static async Task<string> ReadTailAsync(StreamReader reader, CancellationToken cancellationToken)
        {
            StringBuilder tail = new();
            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is string line)
            {
                tail.AppendLine(line);
                if (tail.Length > MaxStandardErrorLength)
                {
                    tail.Remove(0, tail.Length - MaxStandardErrorLength);
                }
            }

            return tail.ToString();
        }

        private static void Terminate(Process process)
        {
            try
            {
                // FFmpeg may spawn helper processes for some protocols, so the whole tree is terminated.
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
            }
            catch (InvalidOperationException)
            {
                // The process exited between the cancellation and the kill request; nothing is left to clean up.
            }
        }
    }
}
