// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Desktop.Services
{
    /// <summary>
    /// A file type offered by a save dialog.
    /// </summary>
    /// <param name="Name">The name shown to the user, for example "MP4 video".</param>
    /// <param name="Extension">The extension, including the leading period.</param>
    internal sealed record SaveFileType(string Name, string Extension);
}
