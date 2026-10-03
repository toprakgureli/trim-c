// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace TrimC.Desktop.Diagnostics
{
    /// <summary>
    /// Writes log entries to a daily file under the user's local application data folder.
    /// </summary>
    /// <remarks>
    /// A desktop application has no console, so without a file a failure on a user's machine leaves nothing behind to
    /// diagnose. Entries are appended and flushed immediately so that the last lines survive a crash of the process.
    /// </remarks>
    internal sealed class FileLoggerProvider : ILoggerProvider
    {
        private readonly Lock _gate = new();
        private readonly StreamWriter? _writer;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLoggerProvider"/> class.
        /// </summary>
        /// <param name="directory">The directory that receives the log files.</param>
        public FileLoggerProvider(string directory)
        {
            ArgumentNullException.ThrowIfNull(directory);

            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, string.Create(CultureInfo.InvariantCulture, $"trim-c-{DateTime.Now:yyyyMMdd}.log"));
                _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), new UTF8Encoding(false))
                {
                    AutoFlush = true,
                };
                LogFilePath = path;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never prevent the application from starting; without a writable folder entries are dropped.
                _writer = null;
            }
        }

        /// <summary>
        /// Gets the path of the current log file, or <see langword="null"/> when the log folder is not writable.
        /// </summary>
        public string? LogFilePath { get; }

        /// <summary>
        /// Gets the folder that holds the log files of the current user.
        /// </summary>
        public static string DefaultDirectory { get; } =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "trim-c", "logs");

        /// <inheritdoc/>
        public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (_gate)
            {
                _writer?.Dispose();
            }
        }

        private void Write(LogLevel level, string category, string message, Exception? exception)
        {
            if (_writer is null)
            {
                return;
            }

            StringBuilder line = new();
            line.Append(CultureInfo.InvariantCulture, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {category}: {message}");
            if (exception is not null)
            {
                line.AppendLine().Append(exception);
            }

            lock (_gate)
            {
                _writer.WriteLine(line.ToString());
            }
        }

        private sealed class FileLogger(FileLoggerProvider provider, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (IsEnabled(logLevel))
                {
                    provider.Write(logLevel, category, formatter(state, exception), exception);
                }
            }
        }
    }
}
