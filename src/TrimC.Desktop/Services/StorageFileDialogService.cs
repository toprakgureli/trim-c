// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using TrimC.Desktop.Resources;

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// An <see cref="IFileDialogService"/> built on Avalonia's <see cref="IStorageProvider"/>.
    /// </summary>
    internal sealed class StorageFileDialogService : IFileDialogService
    {
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
                Title = Strings.OpenDialogTitle,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType(Strings.MediaFilesFilter)
                    {
                        Patterns = ["*.mkv", "*.mp4", "*.mov", "*.m4v", "*.webm", "*.ts", "*.m2ts", "*.mts", "*.flv", "*.avi", "*.wmv", "*.mp3", "*.m4a", "*.flac", "*.wav"],
                        MimeTypes = ["video/*", "audio/*"],
                    },
                    FilePickerFileTypes.All,
                ],
            }).ConfigureAwait(true);

            return files.Count > 0 ? files[0].TryGetLocalPath() : null;
        }

        /// <inheritdoc/>
        public async Task<string?> PickSaveFileAsync(SaveFileRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            IStorageProvider? storage = _topLevelAccessor()?.StorageProvider;
            if (storage is null)
            {
                return null;
            }

            List<FilePickerFileType> types = [];
            foreach (SaveFileType type in request.FileTypes)
            {
                types.Add(new FilePickerFileType(type.Name) { Patterns = ["*" + type.Extension] });
            }

            IStorageFolder? start = request.InitialDirectory is null ? null : await storage.TryGetFolderFromPathAsync(request.InitialDirectory).ConfigureAwait(true);
            IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = request.Title,
                SuggestedFileName = request.SuggestedFileName,
                SuggestedStartLocation = start,
                DefaultExtension = request.FileTypes.Count > 0 ? request.FileTypes[0].Extension.TrimStart('.') : null,
                FileTypeChoices = types,
                ShowOverwritePrompt = true,
            }).ConfigureAwait(true);

            return file?.TryGetLocalPath();
        }
    }
}
