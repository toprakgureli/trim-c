// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;

namespace TrimC.FFmpeg
{
    /// <summary>
    /// The exception that is thrown when an FFmpeg tool exits unsuccessfully or produces output that cannot be interpreted.
    /// </summary>
    public sealed class FFmpegException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegException"/> class.
        /// </summary>
        public FFmpegException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegException"/> class with a message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public FFmpegException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegException"/> class with a message and an inner exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that caused this exception.</param>
        public FFmpegException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="FFmpegException"/> class for a failed process.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="exitCode">The exit code of the tool.</param>
        /// <param name="standardError">The diagnostic output of the tool.</param>
        public FFmpegException(string message, int exitCode, string standardError)
            : base(message)
        {
            ExitCode = exitCode;
            StandardError = standardError;
        }

        /// <summary>
        /// Gets the exit code of the tool, or <see langword="null"/> when the failure was not a non-zero exit.
        /// </summary>
        public int? ExitCode { get; }

        /// <summary>
        /// Gets the diagnostic output written by the tool, which usually names the exact cause of the failure.
        /// </summary>
        public string? StandardError { get; }
    }
}
