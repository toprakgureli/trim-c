// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// An <see cref="IFileDialogService"/> built on Avalonia's <see cref="IStorageProvider"/>.
    /// </summary>
    internal sealed class StorageFileDialogService : IFileDialogService
    {
        private static readonly FilePickerFileType s_mediaFiles = new("Media files")
        {
            Patterns = ["*.mkv", "*.mp4", "*.mov", "*.m4v", "*.webm", "*.ts", "*.m2ts", "*.flv", "*.avi", "*.mp3", "*.m4a", "*.flac", "*.wav"],
            MimeTypes = ["video/*", "audio/*"],
        };

        private readonly Func<TopLevel?> _topLevelAccessor;

        /// <summary>
        /// Initializes a new instance of the <see cref="StorageFileDialogService"/> class.
        /// </summary>
        /// <param name="topLevelAccessor">
        /// Returns the window that owns the dialogs. It is resolved lazily because services are created before the main window.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="topLevelAccessor"/> is <see langword="null"/>.</exception>
        public StorageFileDialogService(Func<TopLevel?> topLevelAccessor)
        {
            ArgumentNullException.ThrowIfNull(topLevelAccessor);

            _topLevelAccessor = topLevelAccessor;
        }

        /// <inheritdoc/>
        public async Task<string?> PickMediaFileAsync()
        {
            IStorageProvider? storage = _topLevelAccessor()?.StorageProvider;
            if (storage is null)
            {
                return null;
            }

            IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open media",
                AllowMultiple = false,
                FileTypeFilter = [s_mediaFiles, FilePickerFileTypes.All],
            }).ConfigureAwait(true);

            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }

        /// <inheritdoc/>
        public async Task<string?> PickFolderAsync(string? initialDirectory)
        {
            IStorageProvider? storage = _topLevelAccessor()?.StorageProvider;
            if (storage is null)
            {
                return null;
            }

            IStorageFolder? start = initialDirectory is null ? null : await storage.TryGetFolderFromPathAsync(initialDirectory).ConfigureAwait(true);
            IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Export to folder",
                AllowMultiple = false,
                SuggestedStartLocation = start,
            }).ConfigureAwait(true);

            return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        }
    }
}
