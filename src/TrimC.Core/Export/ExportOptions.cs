// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Collections.Generic;
using TrimC.Editing;

namespace TrimC.Export
{
    /// <summary>
    /// User-selected settings that control how segments are exported.
    /// </summary>
    public sealed record ExportOptions
    {
        /// <summary>
        /// Gets the directory that receives the exported files.
        /// </summary>
        public required string OutputDirectory { get; init; }

        /// <summary>
        /// Gets the output container. Defaults to <see cref="ContainerFormat.SameAsSource"/>.
        /// </summary>
        public ContainerFormat Container { get; init; } = ContainerFormat.SameAsSource;

        /// <summary>
        /// Gets how multiple segments are written. Defaults to <see cref="ExportMode.SeparateFiles"/>.
        /// </summary>
        public ExportMode Mode { get; init; } = ExportMode.SeparateFiles;

        /// <summary>
        /// Gets how precisely segments are cut. Defaults to <see cref="CutMode.Keyframe"/>.
        /// </summary>
        public CutMode CutMode { get; init; } = CutMode.Keyframe;

        /// <summary>
        /// Gets how segment starts are aligned to keyframes in <see cref="CutMode.Keyframe"/> mode.
        /// Defaults to <see cref="KeyframeSnapMode.Previous"/>.
        /// </summary>
        public KeyframeSnapMode SnapMode { get; init; } = KeyframeSnapMode.Previous;

        /// <summary>
        /// Gets the absolute indexes of the source streams to include, or <see langword="null"/> to include every
        /// stream the target container can carry.
        /// </summary>
        public IReadOnlyList<int>? StreamIndexes { get; init; }

        /// <summary>
        /// Gets a value indicating whether the <c>moov</c> atom is moved to the start of MP4 and QuickTime files
        /// so that playback can begin before the whole file is downloaded. Defaults to <see langword="true"/>.
        /// </summary>
        public bool FastStart { get; init; } = true;
    }
}
