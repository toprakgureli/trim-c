// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using TrimC.Desktop.Resources;

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// Adds trim-c to the Windows "Open with" menu of video files.
    /// </summary>
    /// <remarks>
    /// <para>
    /// trim-c ships as a portable folder without an installer, so the registration is written by the application itself
    /// into the per-user hive (<c>HKCU\Software\Classes</c>). That needs no administrator rights, affects only the
    /// current user, and is refreshed on every start so the entries follow the folder if it is moved.
    /// </para>
    /// <para>
    /// The registration never takes over a file type: it adds a programmatic identifier to each extension's
    /// <c>OpenWithProgids</c> list and leaves the default application untouched, which is the behaviour Windows
    /// recommends for applications that offer to open a type without owning it.
    /// </para>
    /// </remarks>
    internal sealed partial class WindowsShellIntegration : IShellIntegration
    {
        private const string ProgId = "TrimC.Video";
        private const string ExecutableName = "trim-c.exe";

        private static readonly string[] s_extensions =
            [".mp4", ".m4v", ".mkv", ".mov", ".webm", ".ts", ".m2ts", ".mts", ".flv", ".avi", ".wmv"];

        private readonly string _classesRoot;

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsShellIntegration"/> class that writes to the per-user classes.
        /// </summary>
        public WindowsShellIntegration()
            : this(@"Software\Classes")
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WindowsShellIntegration"/> class that writes below another key of
        /// the current user, which lets tests verify the registration without touching the real file associations.
        /// </summary>
        /// <param name="classesRoot">The key below <c>HKEY_CURRENT_USER</c> that plays the role of <c>Software\Classes</c>.</param>
        internal WindowsShellIntegration(string classesRoot)
        {
            _classesRoot = classesRoot;
        }

        /// <inheritdoc/>
        public bool IsSupported => OperatingSystem.IsWindows();

        /// <inheritdoc/>
        public void Register(string executablePath)
        {
            ArgumentNullException.ThrowIfNull(executablePath);
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            string command = $"\"{executablePath}\" \"%1\"";
            string icon = $"\"{executablePath}\",0";

            using (RegistryKey progId = Registry.CurrentUser.CreateSubKey($@"{_classesRoot}\{ProgId}"))
            {
                progId.SetValue(string.Empty, Strings.ShellFileTypeName);
                using RegistryKey defaultIcon = progId.CreateSubKey("DefaultIcon");
                defaultIcon.SetValue(string.Empty, icon);
                using RegistryKey open = progId.CreateSubKey(@"shell\open\command");
                open.SetValue(string.Empty, command);
            }

            // The Applications key supplies the name shown in the menu and lists the types the application can open.
            using (RegistryKey application = Registry.CurrentUser.CreateSubKey($@"{_classesRoot}\Applications\{ExecutableName}"))
            {
                application.SetValue("FriendlyAppName", "trim-c");
                using RegistryKey open = application.CreateSubKey(@"shell\open\command");
                open.SetValue(string.Empty, command);
                using RegistryKey supportedTypes = application.CreateSubKey("SupportedTypes");
                foreach (string extension in s_extensions)
                {
                    supportedTypes.SetValue(extension, string.Empty);
                }
            }

            foreach (string extension in s_extensions)
            {
                using RegistryKey openWith = Registry.CurrentUser.CreateSubKey($@"{_classesRoot}\{extension}\OpenWithProgids");
                openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);
            }

            NotifyAssociationsChanged();
        }

        /// <inheritdoc/>
        public void Unregister()
        {
            if (!OperatingSystem.IsWindows())
            {
                return;
            }

            Registry.CurrentUser.DeleteSubKeyTree($@"{_classesRoot}\{ProgId}", throwOnMissingSubKey: false);
            Registry.CurrentUser.DeleteSubKeyTree($@"{_classesRoot}\Applications\{ExecutableName}", throwOnMissingSubKey: false);

            foreach (string extension in s_extensions)
            {
                using RegistryKey? openWith = Registry.CurrentUser.OpenSubKey($@"{_classesRoot}\{extension}\OpenWithProgids", writable: true);
                openWith?.DeleteValue(ProgId, throwOnMissingValue: false);
            }

            NotifyAssociationsChanged();
        }

        // Explorer caches associations; SHCNE_ASSOCCHANGED tells it to reload them without a sign-out.
        private static void NotifyAssociationsChanged() => SHChangeNotify(0x08000000, 0, 0, 0);

        [LibraryImport("shell32.dll")]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        private static partial void SHChangeNotify(int eventId, uint flags, nint item1, nint item2);
    }
}
