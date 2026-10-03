// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrimC.Desktop.Diagnostics;
using TrimC.Desktop.Playback;
using TrimC.Desktop.Services;
using TrimC.Desktop.ViewModels;
using TrimC.Desktop.Views;

namespace TrimC.Desktop
{
    /// <summary>
    /// The Avalonia application and composition root.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every service is wired here and nowhere else. Types below this point receive their dependencies through
    /// constructors and never resolve services themselves, which keeps the object graph visible in one place.
    /// </para>
    /// <para>
    /// The application also installs the last line of defence against unexpected errors. An exception that escapes a
    /// command on the UI thread is logged and reported in the status bar instead of terminating the process, so a user
    /// never loses their cut list to a bug, and the log file names the exact cause.
    /// </para>
    /// </remarks>
    internal sealed partial class App : Application
    {
        private ServiceProvider? _services;
        private string? _logFilePath;
        private Window? _mainWindow;
        private ILogger<App>? _logger;
        private MainWindowViewModel? _viewModel;

        /// <inheritdoc/>
        public override void Initialize() => AvaloniaXamlLoader.Load(this);

        /// <inheritdoc/>
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                _services = ConfigureServices();
                _logger = _services.GetRequiredService<ILogger<App>>();
                InstallExceptionHandlers();

                _viewModel = _services.GetRequiredService<MainWindowViewModel>();
                _mainWindow = new MainWindow { DataContext = _viewModel };
                desktop.MainWindow = _mainWindow;
                desktop.Exit += OnExit;

                if (desktop.Args is [string filePath, ..])
                {
                    _mainWindow.Opened += (_, _) => _viewModel.OpenFileCommand.Execute(filePath);
                }

                LogStarted(typeof(App).Assembly.GetName().Version?.ToString() ?? "unknown", _logFilePath ?? "none");
            }

            base.OnFrameworkInitializationCompleted();
        }

        private ServiceProvider ConfigureServices()
        {
            ServiceCollection services = new();

            // The logger factory takes ownership of the provider and disposes it together with the service provider.
            FileLoggerProvider fileLogger = new(FileLoggerProvider.DefaultDirectory);
            _logFilePath = fileLogger.LogFilePath;

            services.AddLogging(logging => logging
                .SetMinimumLevel(LogLevel.Information)
                .AddDebug()
                .AddProvider(fileLogger));

            services.AddSingleton<IDispatcher>(Dispatcher.UIThread);
            services.AddSingleton<IFileDialogService>(_ => new StorageFileDialogService(() => _mainWindow));
            services.AddSingleton<IVideoPlayer, MpvPlayer>();
            services.AddSingleton<MediaToolchain>();
            services.AddSingleton<MainWindowViewModel>();

            return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        }

        private void InstallExceptionHandlers()
        {
            Dispatcher.UIThread.UnhandledException += OnUnhandledUiException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }

        private void OnUnhandledUiException(object? sender, DispatcherUnhandledExceptionEventArgs e)
        {
            LogUnexpectedError(e.Exception);
            _viewModel?.ReportUnexpectedError(e.Exception, _logFilePath);
            e.Handled = true;
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            LogUnexpectedError(e.Exception);
            e.SetObserved();
        }

        private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            // Exceptions on background threads cannot be recovered from; the entry is written so the cause is known.
            if (e.ExceptionObject is Exception exception)
            {
                LogFatalError(exception);
            }
        }

        private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            // Disposing the provider disposes every singleton, including the player and the view model.
            _services?.Dispose();
        }

        private void LogStarted(string version, string logFile)
        {
            if (_logger is not null)
            {
                LogStartedCore(_logger, version, logFile);
            }
        }

        private void LogUnexpectedError(Exception exception)
        {
            if (_logger is not null)
            {
                LogUnexpectedErrorCore(_logger, exception);
            }
        }

        private void LogFatalError(Exception exception)
        {
            if (_logger is not null)
            {
                LogFatalErrorCore(_logger, exception);
            }
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "trim-c {Version} started; logging to {LogFile}")]
        private static partial void LogStartedCore(ILogger logger, string version, string logFile);

        [LoggerMessage(Level = LogLevel.Error, Message = "Unexpected error handled on the UI thread")]
        private static partial void LogUnexpectedErrorCore(ILogger logger, Exception exception);

        [LoggerMessage(Level = LogLevel.Critical, Message = "Unhandled exception terminated the application")]
        private static partial void LogFatalErrorCore(ILogger logger, Exception exception);
    }
}
