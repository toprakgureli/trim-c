// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// Registers the application with the operating system shell so that video files can be opened with it.
    /// </summary>
    internal interface IShellIntegration
    {
        /// <summary>
        /// Gets a value indicating whether the current platform supports the integration.
        /// </summary>
        bool IsSupported { get; }

        /// <summary>
        /// Adds the application to the "Open with" menu of the supported video file types.
        /// </summary>
        /// <param name="executablePath">The full path of the executable that opens the files.</param>
        void Register(string executablePath);

        /// <summary>
        /// Removes every entry written by <see cref="Register"/>.
        /// </summary>
        void Unregister();
    }
}
