// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrimC.Export;
using TrimC.FFmpeg.Processes;

namespace TrimC.FFmpeg.Exporting
{
    /// <summary>
    /// An <see cref="IExportExecutor"/> that runs each step of a plan as an ffmpeg process.
    /// </summary>
    /// <remarks>
    /// Steps run sequentially. Stream copy is bound by disk throughput rather than CPU, so running steps in
    /// parallel would only cause the processes to compete for the same disk.
    /// </remarks>
    public sealed partial class FFmpegExportExecutor : IExportExecutor
    {
        private readonly FFmpegTools _tools;
        private readonly ILogger<FFmpegExportExecutor> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegExportExecutor"/> class.
        /// </summary>
        /// <param name="tools">The located FFmpeg tools.</param>
        /// <param name="logger">The logger, or <see langword="null"/> to disable logging.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tools"/> is <see langword="null"/>.</exception>
        public FFmpegExportExecutor(FFmpegTools tools, ILogger<FFmpegExportExecutor>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(tools);

            _tools = tools;
            _logger = logger ?? NullLogger<FFmpegExportExecutor>.Instance;
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"><paramref name="plan"/> is <see langword="null"/>.</exception>
        /// <exception cref="FFmpegException">A step failed.</exception>
        public async Task ExecuteAsync(ExportPlan plan, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(plan);

            double totalTicks = Math.Max(plan.TotalWorkload.Ticks, 1);
            TimeSpan completed = TimeSpan.Zero;
            bool succeeded = false;

            // Only files this executor has started writing are ever deleted, so a failure can never remove a file
            // that some other process created at one of the planned paths after planning.
            List<string> created = [];

            try
            {
                for (int i = 0; i < plan.Steps.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ExportStep step = plan.Steps[i];
                    int stepNumber = i + 1;
                    TimeSpan stepStart = completed;

                    void ReportStep(TimeSpan written)
                    {
                        // ffmpeg can briefly overshoot the requested duration by up to one packet.
                        TimeSpan clamped = written > step.Workload ? step.Workload : written;
                        progress?.Report(new ExportProgress(stepNumber, plan.Steps.Count, (stepStart + clamped).Ticks / totalTicks));
                    }

                    if (File.Exists(step.OutputPath))
                    {
                        throw new FFmpegException($"The output file '{step.OutputPath}' was created by another process after the export was planned.");
                    }

                    ReportStep(TimeSpan.Zero);
                    LogStepStarting(stepNumber, plan.Steps.Count, step.OutputPath);
                    created.Add(step.OutputPath);

                    await ExecuteStepAsync(step, ReportStep, cancellationToken).ConfigureAwait(false);

                    completed += step.Workload;
                    ReportStep(step.Workload);
                }

                succeeded = true;
            }
            finally
            {
                // Temporary parts are removed in every outcome; partially written outputs are removed on failure or cancellation.
                DeleteFiles(succeeded ? created.FindAll(path => plan.TemporaryFiles.Contains(path)) : created);
            }
        }

        private async Task ExecuteStepAsync(ExportStep step, Action<TimeSpan> reportWritten, CancellationToken cancellationToken)
        {
            Action<string> onProgressLine = line =>
            {
                if (ProgressLineParser.TryParseOutTime(line, out TimeSpan written))
                {
                    reportWritten(written);
                }
            };

            switch (step)
            {
                case CopyRangeStep copy:
                    await RunFFmpegAsync(FFmpegArguments.ForCopyRange(copy), onProgressLine, step.OutputPath, cancellationToken).ConfigureAwait(false);
                    break;

                case MuxPartsStep mux:
                    await RunFFmpegAsync(FFmpegArguments.ForMuxParts(mux), onProgressLine, step.OutputPath, cancellationToken, FFmpegArguments.GetPartsDirectory(mux)).ConfigureAwait(false);
                    break;

                case EncodeRangeStep encode:
                    await RunFFmpegAsync(FFmpegArguments.ForEncodeRange(encode), onProgressLine, step.OutputPath, cancellationToken).ConfigureAwait(false);
                    break;

                case ConcatStep concat:
                    string listPath = concat.OutputPath + ".ffconcat";
                    try
                    {
                        // UTF-8 without a byte order mark: the concat demuxer would treat a BOM as part of the first directive.
                        await File.WriteAllTextAsync(listPath, FFmpegArguments.CreateConcatList(concat.InputPaths), new UTF8Encoding(false), cancellationToken).ConfigureAwait(false);
                        await RunFFmpegAsync(FFmpegArguments.ForConcat(concat, listPath), onProgressLine, step.OutputPath, cancellationToken).ConfigureAwait(false);
                    }
                    finally
                    {
                        DeleteFiles([listPath]);
                    }

                    break;

                default:
                    throw new NotSupportedException($"Export step type '{step.GetType().Name}' is not supported.");
            }
        }

        private async Task RunFFmpegAsync(
            List<string> arguments,
            Action<string> onProgressLine,
            string outputPath,
            CancellationToken cancellationToken,
            string? workingDirectory = null)
        {
            ProcessResult result = await ProcessRunner.RunAsync(_tools.FFmpegPath, arguments, onProgressLine, cancellationToken, workingDirectory).ConfigureAwait(false);

            if (result.ExitCode != 0)
            {
                LogStepFailed(outputPath, result.ExitCode, result.StandardError);
                throw new FFmpegException($"ffmpeg failed to write '{outputPath}'.", result.ExitCode, result.StandardError);
            }
        }

        private void DeleteFiles(IReadOnlyList<string> paths)
        {
            foreach (string path in paths)
            {
                try
                {
                    File.Delete(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Cleanup is best effort; a leftover file must not mask the original export result.
                    LogCleanupFailed(path, ex);
                }
            }
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Export step {StepNumber}/{StepCount} writing {OutputPath}")]
        private partial void LogStepStarting(int stepNumber, int stepCount, string outputPath);

        [LoggerMessage(Level = LogLevel.Error, Message = "ffmpeg exited with code {ExitCode} while writing {OutputPath}: {StandardError}")]
        private partial void LogStepFailed(string outputPath, int exitCode, string standardError);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete {Path}")]
        private partial void LogCleanupFailed(string path, Exception exception);
    }
}
