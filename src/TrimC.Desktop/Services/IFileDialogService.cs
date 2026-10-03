// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Threading.Tasks;

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// Shows the platform file pickers.
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
        /// Asks the user where to save a file, with the platform's standard save dialog.
        /// </summary>
        /// <param name="request">What the dialog suggests and offers.</param>
        /// <returns>The chosen path, or <see langword="null"/> if the dialog was dismissed.</returns>
        /// <remarks>
        /// The dialog itself asks before an existing file is replaced, so a returned path that exists has been confirmed.
        /// </remarks>
        Task<string?> PickSaveFileAsync(SaveFileRequest request);
    }
}
