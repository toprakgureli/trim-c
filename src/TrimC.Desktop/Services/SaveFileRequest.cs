// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Collections.Generic;

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// Describes a save dialog.
    /// </summary>
    /// <param name="Title">The title of the dialog.</param>
    /// <param name="SuggestedFileName">The file name the dialog starts with, including its extension.</param>
    /// <param name="InitialDirectory">The folder the dialog opens in, or <see langword="null"/> for the platform default.</param>
    /// <param name="FileTypes">The file types offered, the default one first.</param>
    internal sealed record SaveFileRequest(string Title, string SuggestedFileName, string? InitialDirectory, IReadOnlyList<SaveFileType> FileTypes);
}
