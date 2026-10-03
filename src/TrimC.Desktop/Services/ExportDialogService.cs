// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using TrimC.Desktop.ViewModels;
using TrimC.Desktop.Views;

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// An <see cref="IExportDialogService"/> that shows <see cref="ExportDialog"/> as a modal window.
    /// </summary>
    internal sealed class ExportDialogService : IExportDialogService
    {
        private readonly Func<Window?> _ownerAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="ExportDialogService"/> class.
        /// </summary>
        /// <param name="ownerAccessor">Returns the window that owns the dialog. It is resolved lazily because services are created before the main window.</param>
        /// <exception cref="ArgumentNullException"><paramref name="ownerAccessor"/> is <see langword="null"/>.</exception>
        public ExportDialogService(Func<Window?> ownerAccessor)
        {
            ArgumentNullException.ThrowIfNull(ownerAccessor);

            _ownerAccessor = ownerAccessor;
        }

        /// <inheritdoc/>
        public async Task<bool> ShowAsync(ExportDialogViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            Window? owner = _ownerAccessor();
            if (owner is null)
            {
                return false;
            }

            ExportDialog dialog = new() { DataContext = viewModel };
            return await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
        }
    }
}
