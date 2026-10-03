// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Threading.Tasks;
using TrimC.Desktop.ViewModels;

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// Shows the export settings dialog.
    /// </summary>
    internal interface IExportDialogService
    {
        /// <summary>
        /// Shows the dialog modally over the main window.
        /// </summary>
        /// <param name="viewModel">The settings to present and edit.</param>
        /// <returns><see langword="true"/> if the user confirmed the export; otherwise, <see langword="false"/>.</returns>
        Task<bool> ShowAsync(ExportDialogViewModel viewModel);
    }
}
