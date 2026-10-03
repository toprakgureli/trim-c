// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Threading.Tasks;

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// Shows the platform file and folder pickers.
    /// </summary>
    /// <remarks>
    /// View models depend on this abstraction instead of on a window, which keeps them free of UI types and testable.
    /// </remarks>
    internal interface IFileDialogService
    {
        /// <summary>
        /// Asks the user to choose a media file.
        /// </summary>
        /// <returns>The chosen file path, or <see langword="null"/> if the dialog was dismissed.</returns>
        Task<string?> PickMediaFileAsync();

        /// <summary>
        /// Asks the user to choose a folder.
        /// </summary>
        /// <param name="initialDirectory">The folder the dialog opens in, or <see langword="null"/>.</param>
        /// <returns>The chosen folder path, or <see langword="null"/> if the dialog was dismissed.</returns>
        Task<string?> PickFolderAsync(string? initialDirectory);
    }
}
