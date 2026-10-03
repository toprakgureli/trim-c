// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Desktop.Controls
{
    /// <summary>
    /// Identifies the stage of a trim handle drag.
    /// </summary>
    internal enum TrimPhase
    {
        /// <summary>The pointer grabbed the handle; nothing has moved yet.</summary>
        Started = 0,

        /// <summary>The handle is being dragged.</summary>
        Moved,

        /// <summary>The pointer released the handle.</summary>
        Completed,
    }
}
