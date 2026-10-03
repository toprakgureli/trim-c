// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Diagnostics;
using System.IO;

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// Opens the platform file manager.
    /// </summary>
    internal static class FileManager
    {
        /// <summary>
        /// Shows a file in the platform file manager, selecting it where the platform supports selection.
        /// </summary>
        /// <param name="filePath">The file to reveal.</param>
        public static void Reveal(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            ProcessStartInfo startInfo;
            if (OperatingSystem.IsWindows())
            {
                // Explorer parses its own command line; the /select switch and the path must be one argument.
                startInfo = new ProcessStartInfo("explorer.exe", $"/select,\"{filePath}\"");
            }
            else if (OperatingSystem.IsMacOS())
            {
                startInfo = new ProcessStartInfo("open") { ArgumentList = { "-R", filePath } };
            }
            else
            {
                // Linux file managers have no common selection protocol, so the containing folder is opened instead.
                startInfo = new ProcessStartInfo("xdg-open") { ArgumentList = { Path.GetDirectoryName(filePath) ?? filePath } };
            }

            startInfo.UseShellExecute = false;
            using Process? process = Process.Start(startInfo);
        }

        /// <summary>
        /// Opens a folder in the platform file manager.
        /// </summary>
        /// <param name="folderPath">The folder to open.</param>
        public static void OpenFolder(string folderPath)
        {
            ArgumentNullException.ThrowIfNull(folderPath);

            string program = OperatingSystem.IsWindows() ? "explorer.exe" : OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            ProcessStartInfo startInfo = new(program) { UseShellExecute = false };
            startInfo.ArgumentList.Add(folderPath);
            using Process? process = Process.Start(startInfo);
        }
    }
}
