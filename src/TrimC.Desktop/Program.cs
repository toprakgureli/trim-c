// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using Avalonia;

namespace TrimC.Desktop
{
    /// <summary>
    /// The process entry point.
    /// </summary>
    internal static class Program
    {
        /// <summary>
        /// Starts the application. A file path passed as the first argument is opened on startup, which makes the
        /// application usable from the shell's "Open with" menu.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The process exit code.</returns>
        [STAThread]
        public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        /// <summary>
        /// Configures Avalonia. The XAML previewer calls this method by name, so its signature must stay unchanged.
        /// </summary>
        /// <returns>The configured application builder.</returns>
        public static AppBuilder BuildAvaloniaApp() =>
            AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
