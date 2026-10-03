// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using Microsoft.Extensions.Logging;
using TrimC.Export;
using TrimC.FFmpeg;
using TrimC.FFmpeg.Exporting;
using TrimC.FFmpeg.Probing;
using TrimC.Media;

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// Provides the media services, which exist only when FFmpeg is installed.
    /// </summary>
    /// <remarks>
    /// FFmpeg is an external dependency that can be missing on a fresh machine. Instead of failing at startup, the
    /// application starts normally and reports the missing tools, which lets the user fix the installation without
    /// guessing why the window never appeared.
    /// </remarks>
    internal sealed class MediaToolchain
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MediaToolchain"/> class by locating FFmpeg.
        /// </summary>
        /// <param name="loggerFactory">The factory used to create loggers for the media services.</param>
        /// <exception cref="ArgumentNullException"><paramref name="loggerFactory"/> is <see langword="null"/>.</exception>
        public MediaToolchain(ILoggerFactory loggerFactory)
        {
            ArgumentNullException.ThrowIfNull(loggerFactory);

            FFmpegTools? tools = FFmpegTools.Locate();
            if (tools is null)
            {
                return;
            }

            Tools = tools;
            Probe = new FFprobeMediaProbe(tools, loggerFactory.CreateLogger<FFprobeMediaProbe>());
            Executor = new FFmpegExportExecutor(tools, loggerFactory.CreateLogger<FFmpegExportExecutor>());
        }

        /// <summary>
        /// Gets the located tools, or <see langword="null"/> when FFmpeg is not installed.
        /// </summary>
        public FFmpegTools? Tools { get; }

        /// <summary>
        /// Gets the media probe, or <see langword="null"/> when FFmpeg is not installed.
        /// </summary>
        public IMediaProbe? Probe { get; }

        /// <summary>
        /// Gets the export executor, or <see langword="null"/> when FFmpeg is not installed.
        /// </summary>
        public IExportExecutor? Executor { get; }
    }
}
