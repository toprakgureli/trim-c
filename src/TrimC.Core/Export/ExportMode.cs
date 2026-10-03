// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Export
{
    /// <summary>
    /// Specifies how multiple segments are written to disk.
    /// </summary>
    public enum ExportMode
    {
        /// <summary>Each segment becomes its own file.</summary>
        SeparateFiles = 0,

        /// <summary>All segments are joined, in timeline order, into a single file.</summary>
        Merge,
    }
}
