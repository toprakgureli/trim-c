// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using TrimC.Editing;
using TrimC.Export;

namespace TrimC.Desktop.Settings
{
    /// <summary>
    /// The preferences that persist between sessions.
    /// </summary>
    /// <remarks>
    /// Every property has a default, so a missing or partial settings file always yields a usable configuration and new
    /// properties can be added without migrating existing files.
    /// </remarks>
    internal sealed record AppSettings
    {
        /// <summary>
        /// Gets the user interface language as a culture name such as <c>en</c> or <c>tr</c>, or an empty string to follow Windows.
        /// </summary>
        public string Language { get; init; } = string.Empty;

        /// <summary>
        /// Gets the preferred output container.
        /// </summary>
        public ContainerFormat Container { get; init; } = ContainerFormat.SameAsSource;

        /// <summary>
        /// Gets how multiple segments are exported.
        /// </summary>
        public ExportMode Mode { get; init; } = ExportMode.SeparateFiles;

        /// <summary>
        /// Gets the cut precision. Exact frames are the default because the trim handles select individual frames, and
        /// the user should get the frames they selected.
        /// </summary>
        public CutMode CutMode { get; init; } = CutMode.FrameAccurate;

        /// <summary>
        /// Gets how segment starts are aligned in keyframe mode.
        /// </summary>
        public KeyframeSnapMode SnapMode { get; init; } = KeyframeSnapMode.Previous;

        /// <summary>
        /// Gets the folder the last export was saved in, where the next save dialog opens; or <see langword="null"/> to
        /// open it next to the source file.
        /// </summary>
        public string? LastExportDirectory { get; init; }

        /// <summary>
        /// Gets a value indicating whether the output folder opens after a successful export.
        /// </summary>
        public bool OpenFolderWhenDone { get; init; } = true;

        /// <summary>
        /// Gets a value indicating whether the advanced editing panel is shown.
        /// </summary>
        public bool IsAdvancedPanelOpen { get; init; }

        /// <summary>
        /// Gets a value indicating whether trim-c registers itself in the "Open with" menu of video files.
        /// </summary>
        public bool ShowInOpenWith { get; init; } = true;
    }
}
