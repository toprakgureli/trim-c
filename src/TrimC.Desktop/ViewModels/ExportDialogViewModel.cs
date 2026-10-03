// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using TrimC.Desktop.Formatting;
using TrimC.Desktop.Resources;
using TrimC.Desktop.Settings;
using TrimC.Editing;
using TrimC.Export;

namespace TrimC.Desktop.ViewModels
{
    /// <summary>
    /// Presents the export settings in a dialog that opens only when the user is ready to export.
    /// </summary>
    /// <remarks>
    /// The dialog starts from the persisted <see cref="AppSettings"/> and hands the confirmed choices back through
    /// <see cref="ApplyTo"/>, so the next export starts with the same choices. Where the files go is not part of it:
    /// confirming the dialog leads to the standard save dialog, which asks for the name and the folder every time.
    /// </remarks>
    internal sealed partial class ExportDialogViewModel : ObservableObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExportDialogViewModel"/> class.
        /// </summary>
        /// <param name="settings">The persisted settings the dialog starts from.</param>
        /// <param name="segmentCount">The number of segments that will be exported.</param>
        /// <param name="totalDuration">The combined duration of the segments.</param>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is <see langword="null"/>.</exception>
        public ExportDialogViewModel(AppSettings settings, int segmentCount, TimeSpan totalDuration)
        {
            ArgumentNullException.ThrowIfNull(settings);

            SegmentCount = segmentCount;
            Summary = Strings.Format(Strings.ExportSummary, segmentCount, Timecode.Format(totalDuration));

            SelectedContainer = Find(ContainerOptions, settings.Container);
            SelectedMode = Find(ModeOptions, settings.Mode);
            SelectedCutMode = Find(CutModeOptions, settings.CutMode);
            SelectedSnapMode = Find(SnapModeOptions, settings.SnapMode);
            OpenFolderWhenDone = settings.OpenFolderWhenDone;
        }

        /// <summary>Gets the selectable output containers.</summary>
        public IReadOnlyList<ChoiceOption<ContainerFormat>> ContainerOptions { get; } =
        [
            new(ContainerFormat.SameAsSource, Strings.ContainerSameAsSource),
            new(ContainerFormat.Mp4, "MP4"),
            new(ContainerFormat.Matroska, "MKV"),
            new(ContainerFormat.QuickTime, "MOV"),
        ];

        /// <summary>Gets the selectable export modes.</summary>
        public IReadOnlyList<ChoiceOption<ExportMode>> ModeOptions { get; } =
        [
            new(ExportMode.SeparateFiles, Strings.ModeSeparate),
            new(ExportMode.Merge, Strings.ModeMerge),
        ];

        /// <summary>Gets the selectable cut precisions.</summary>
        public IReadOnlyList<ChoiceOption<CutMode>> CutModeOptions { get; } =
        [
            new(CutMode.FrameAccurate, Strings.CutModeExact),
            new(CutMode.Keyframe, Strings.CutModeKeyframe),
        ];

        /// <summary>Gets the selectable keyframe alignment strategies.</summary>
        public IReadOnlyList<ChoiceOption<KeyframeSnapMode>> SnapModeOptions { get; } =
        [
            new(KeyframeSnapMode.Previous, Strings.SnapPrevious),
            new(KeyframeSnapMode.Next, Strings.SnapNext),
            new(KeyframeSnapMode.Nearest, Strings.SnapNearest),
        ];

        /// <summary>Gets a one-line description of what will be exported.</summary>
        public string Summary { get; }

        /// <summary>Gets the number of segments that will be exported.</summary>
        public int SegmentCount { get; }

        /// <summary>Gets a value indicating whether there is more than one segment, which makes the merge choice meaningful.</summary>
        public bool CanMerge => SegmentCount > 1;

        /// <summary>Gets or sets the output container.</summary>
        [ObservableProperty]
        public partial ChoiceOption<ContainerFormat> SelectedContainer { get; set; }

        /// <summary>Gets or sets how multiple segments are exported.</summary>
        [ObservableProperty]
        public partial ChoiceOption<ExportMode> SelectedMode { get; set; }

        /// <summary>Gets or sets the cut precision.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsKeyframeMode), nameof(PrecisionHint))]
        public partial ChoiceOption<CutMode> SelectedCutMode { get; set; }

        /// <summary>Gets or sets how segment starts are aligned in keyframe mode.</summary>
        [ObservableProperty]
        public partial ChoiceOption<KeyframeSnapMode> SelectedSnapMode { get; set; }

        /// <summary>Gets or sets a value indicating whether the output folder opens after the export.</summary>
        [ObservableProperty]
        public partial bool OpenFolderWhenDone { get; set; }

        /// <summary>Gets a value indicating whether keyframe alignment applies.</summary>
        public bool IsKeyframeMode => SelectedCutMode.Value == CutMode.Keyframe;

        /// <summary>Gets an explanation of the selected precision.</summary>
        public string PrecisionHint => IsKeyframeMode ? Strings.PrecisionKeyframeHint : Strings.PrecisionExactHint;

        /// <summary>
        /// Returns <paramref name="settings"/> updated with the choices made in the dialog.
        /// </summary>
        /// <param name="settings">The settings to update.</param>
        /// <returns>The updated settings.</returns>
        public AppSettings ApplyTo(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            return settings with
            {
                Container = SelectedContainer.Value,
                Mode = SelectedMode.Value,
                CutMode = SelectedCutMode.Value,
                SnapMode = SelectedSnapMode.Value,
                OpenFolderWhenDone = OpenFolderWhenDone,
            };
        }

        private static ChoiceOption<T> Find<T>(IReadOnlyList<ChoiceOption<T>> options, T value)
        {
            foreach (ChoiceOption<T> option in options)
            {
                if (EqualityComparer<T>.Default.Equals(option.Value, value))
                {
                    return option;
                }
            }

            return options[0];
        }
    }
}
