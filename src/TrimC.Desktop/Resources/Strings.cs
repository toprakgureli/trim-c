// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Globalization;
using System.Resources;

namespace TrimC.Desktop.Resources
{
    /// <summary>
    /// Provides the localized text of the user interface.
    /// </summary>
    /// <remarks>
    /// The English text lives in <c>Strings.resx</c> and the Turkish text in <c>Strings.tr.resx</c>. Each property returns
    /// the text for <see cref="CultureInfo.CurrentUICulture"/>, which is set once at startup from the language setting.
    /// A unit test keeps this class and both resource files in sync, including the number of format placeholders.
    /// </remarks>
    internal static class Strings
    {
        private static readonly ResourceManager s_resources = new("TrimC.Desktop.Resources.Strings", typeof(Strings).Assembly);

        /// <summary>Gets text like "Open&#x2026;".</summary>
        public static string OpenButton => Get(nameof(OpenButton));

        /// <summary>Gets text like "Open a video (Ctrl+O)".</summary>
        public static string OpenTooltip => Get(nameof(OpenTooltip));

        /// <summary>Gets text like "Export&#x2026;".</summary>
        public static string ExportButton => Get(nameof(ExportButton));

        /// <summary>Gets text like "Choose the export settings and save the selection (Ctrl+E)".</summary>
        public static string ExportTooltip => Get(nameof(ExportTooltip));

        /// <summary>Gets text like "Cancel".</summary>
        public static string CancelExportButton => Get(nameof(CancelExportButton));

        /// <summary>Gets text like "Undo (Ctrl+Z)".</summary>
        public static string UndoTooltip => Get(nameof(UndoTooltip));

        /// <summary>Gets text like "Redo (Ctrl+Y)".</summary>
        public static string RedoTooltip => Get(nameof(RedoTooltip));

        /// <summary>Gets text like "Advanced".</summary>
        public static string AdvancedButton => Get(nameof(AdvancedButton));

        /// <summary>Gets text like "Show the segment list and the manual editing tools".</summary>
        public static string AdvancedTooltip => Get(nameof(AdvancedTooltip));

        /// <summary>Gets text like "More options".</summary>
        public static string MoreTooltip => Get(nameof(MoreTooltip));

        /// <summary>Gets text like "Language".</summary>
        public static string MenuLanguage => Get(nameof(MenuLanguage));

        /// <summary>Gets text like "System default".</summary>
        public static string LanguageSystem => Get(nameof(LanguageSystem));

        /// <summary>Gets text like "English".</summary>
        public static string LanguageEnglish => Get(nameof(LanguageEnglish));

        /// <summary>Gets text like "T&#xFC;rk&#xE7;e".</summary>
        public static string LanguageTurkish => Get(nameof(LanguageTurkish));

        /// <summary>Gets text like "Show in &#x201C;Open with&#x201D; for video files".</summary>
        public static string MenuOpenWith => Get(nameof(MenuOpenWith));

        /// <summary>Gets text like "Open the log folder".</summary>
        public static string MenuOpenLogs => Get(nameof(MenuOpenLogs));

        /// <summary>Gets text like "Drop a video here or press Ctrl+O".</summary>
        public static string DropHint => Get(nameof(DropHint));

        /// <summary>Gets text like "Keep {0} &#x2013; {1}  ({2})".</summary>
        public static string TrimSummary => Get(nameof(TrimSummary));

        /// <summary>Gets text like "{0} segments, {1} in total".</summary>
        public static string TrimSummaryMultiple => Get(nameof(TrimSummaryMultiple));

        /// <summary>Gets text like "Drag the yellow handles on the timeline to choose the part to keep.".</summary>
        public static string TrimHint => Get(nameof(TrimHint));

        /// <summary>Gets text like "Previous keyframe (Ctrl+Left)".</summary>
        public static string PreviousKeyframeTooltip => Get(nameof(PreviousKeyframeTooltip));

        /// <summary>Gets text like "Previous frame (Left)".</summary>
        public static string PreviousFrameTooltip => Get(nameof(PreviousFrameTooltip));

        /// <summary>Gets text like "Play or pause (Space)".</summary>
        public static string PlayPauseTooltip => Get(nameof(PlayPauseTooltip));

        /// <summary>Gets text like "Next frame (Right)".</summary>
        public static string NextFrameTooltip => Get(nameof(NextFrameTooltip));

        /// <summary>Gets text like "Next keyframe (Ctrl+Right)".</summary>
        public static string NextKeyframeTooltip => Get(nameof(NextKeyframeTooltip));

        /// <summary>Gets text like "Segments".</summary>
        public static string SegmentsHeader => Get(nameof(SegmentsHeader));

        /// <summary>Gets text like "Label (used in the file name)".</summary>
        public static string SegmentLabelPlaceholder => Get(nameof(SegmentLabelPlaceholder));

        /// <summary>Gets text like "Remove".</summary>
        public static string RemoveButton => Get(nameof(RemoveButton));

        /// <summary>Gets text like "Remove the selected segment (Delete)".</summary>
        public static string RemoveTooltip => Get(nameof(RemoveTooltip));

        /// <summary>Gets text like "Invert".</summary>
        public static string InvertButton => Get(nameof(InvertButton));

        /// <summary>Gets text like "Keep everything except the current segments".</summary>
        public static string InvertTooltip => Get(nameof(InvertTooltip));

        /// <summary>Gets text like "Reset".</summary>
        public static string ResetButton => Get(nameof(ResetButton));

        /// <summary>Gets text like "Select the whole video again".</summary>
        public static string ResetTooltip => Get(nameof(ResetTooltip));

        /// <summary>Gets text like "Manual tools".</summary>
        public static string ManualToolsHeader => Get(nameof(ManualToolsHeader));

        /// <summary>Gets text like "Set start [I]".</summary>
        public static string SetStartButton => Get(nameof(SetStartButton));

        /// <summary>Gets text like "Mark the first frame of a new segment, or of a part to cut out".</summary>
        public static string SetStartTooltip => Get(nameof(SetStartTooltip));

        /// <summary>Gets text like "Set end [O]".</summary>
        public static string SetEndButton => Get(nameof(SetEndButton));

        /// <summary>Gets text like "Close a segment at the playhead".</summary>
        public static string SetEndTooltip => Get(nameof(SetEndTooltip));

        /// <summary>Gets text like "Split [S]".</summary>
        public static string SplitButton => Get(nameof(SplitButton));

        /// <summary>Gets text like "Split the segment under the playhead in two".</summary>
        public static string SplitTooltip => Get(nameof(SplitTooltip));

        /// <summary>Gets text like "Cut out [X]".</summary>
        public static string CutOutButton => Get(nameof(CutOutButton));

        /// <summary>Gets text like "Remove the frames between the start mark and the playhead".</summary>
        public static string CutOutTooltip => Get(nameof(CutOutTooltip));

        /// <summary>Gets text like "Shift+I and Shift+O move the start and end of the selected segment to the playhead.".</summary>
        public static string ManualToolsHint => Get(nameof(ManualToolsHint));

        /// <summary>Gets text like "Show in folder".</summary>
        public static string ShowInFolderButton => Get(nameof(ShowInFolderButton));

        /// <summary>Gets text like "Export".</summary>
        public static string ExportDialogTitle => Get(nameof(ExportDialogTitle));

        /// <summary>Gets text like "{0} segment(s), {1} in total".</summary>
        public static string ExportSummary => Get(nameof(ExportSummary));

        /// <summary>Gets text like "Container".</summary>
        public static string ContainerLabel => Get(nameof(ContainerLabel));

        /// <summary>Gets text like "Segments".</summary>
        public static string SegmentsModeLabel => Get(nameof(SegmentsModeLabel));

        /// <summary>Gets text like "Cut precision".</summary>
        public static string PrecisionLabel => Get(nameof(PrecisionLabel));

        /// <summary>Gets text like "Cut starts at".</summary>
        public static string SnapLabel => Get(nameof(SnapLabel));

        /// <summary>Gets text like "Output folder".</summary>
        public static string OutputFolderLabel => Get(nameof(OutputFolderLabel));

        /// <summary>Gets text like "Next to the source file".</summary>
        public static string NextToSource => Get(nameof(NextToSource));

        /// <summary>Gets text like "Change&#x2026;".</summary>
        public static string ChangeFolderButton => Get(nameof(ChangeFolderButton));

        /// <summary>Gets text like "Use source folder".</summary>
        public static string UseSourceFolderButton => Get(nameof(UseSourceFolderButton));

        /// <summary>Gets text like "Open the folder when finished".</summary>
        public static string OpenFolderWhenDone => Get(nameof(OpenFolderWhenDone));

        /// <summary>Gets text like "Export".</summary>
        public static string ExportConfirmButton => Get(nameof(ExportConfirmButton));

        /// <summary>Gets text like "Cancel".</summary>
        public static string CancelButton => Get(nameof(CancelButton));

        /// <summary>Gets text like "Copies the video untouched and finishes in seconds. A cut can start up to one keyframe interval early.".</summary>
        public static string PrecisionKeyframeHint => Get(nameof(PrecisionKeyframeHint));

        /// <summary>Gets text like "Cuts on exactly the chosen frames. Only the frames around the cut points are re-encoded, at near-lossless quality.".</summary>
        public static string PrecisionExactHint => Get(nameof(PrecisionExactHint));

        /// <summary>Gets text like "Same as source".</summary>
        public static string ContainerSameAsSource => Get(nameof(ContainerSameAsSource));

        /// <summary>Gets text like "One file per segment".</summary>
        public static string ModeSeparate => Get(nameof(ModeSeparate));

        /// <summary>Gets text like "Merge into one file".</summary>
        public static string ModeMerge => Get(nameof(ModeMerge));

        /// <summary>Gets text like "Keyframe (lossless, instant)".</summary>
        public static string CutModeKeyframe => Get(nameof(CutModeKeyframe));

        /// <summary>Gets text like "Exact frame".</summary>
        public static string CutModeExact => Get(nameof(CutModeExact));

        /// <summary>Gets text like "Previous keyframe (keep everything)".</summary>
        public static string SnapPrevious => Get(nameof(SnapPrevious));

        /// <summary>Gets text like "Next keyframe (nothing extra)".</summary>
        public static string SnapNext => Get(nameof(SnapNext));

        /// <summary>Gets text like "Nearest keyframe".</summary>
        public static string SnapNearest => Get(nameof(SnapNearest));

        /// <summary>Gets text like "FFmpeg was not found. Place ffmpeg and ffprobe in an ffmpeg folder next to trim-c.exe.".</summary>
        public static string StatusFFmpegMissing => Get(nameof(StatusFFmpegMissing));

        /// <summary>Gets text like "Open a video or drop it onto the window.".</summary>
        public static string StatusWelcome => Get(nameof(StatusWelcome));

        /// <summary>Gets text like "Reading {0}&#x2026;".</summary>
        public static string StatusReading => Get(nameof(StatusReading));

        /// <summary>Gets text like "Reading keyframes&#x2026;".</summary>
        public static string StatusIndexing => Get(nameof(StatusIndexing));

        /// <summary>Gets text like "Ready. Audio-only files can be cut at any position.".</summary>
        public static string StatusReadyAudio => Get(nameof(StatusReadyAudio));

        /// <summary>Gets text like "Ready. {0} keyframes, one every {1} s on average.".</summary>
        public static string StatusReady => Get(nameof(StatusReady));

        /// <summary>Gets text like "Could not open {0}: {1}".</summary>
        public static string StatusOpenFailed => Get(nameof(StatusOpenFailed));

        /// <summary>Gets text like "Start set at {0}. Move to the end and press O, or press X to cut out the part in between.".</summary>
        public static string StatusMarkIn => Get(nameof(StatusMarkIn));

        /// <summary>Gets text like "The end must be after the start.".</summary>
        public static string StatusEndBeforeStart => Get(nameof(StatusEndBeforeStart));

        /// <summary>Gets text like "Segment added: {0} &#x2013; {1}.".</summary>
        public static string StatusSegmentAdded => Get(nameof(StatusSegmentAdded));

        /// <summary>Gets text like "The new segment overlaps an existing one. Remove or trim the existing segment first.".</summary>
        public static string StatusSegmentOverlaps => Get(nameof(StatusSegmentOverlaps));

        /// <summary>Gets text like "Place the playhead inside a segment to split it.".</summary>
        public static string StatusSplitOutside => Get(nameof(StatusSplitOutside));

        /// <summary>Gets text like "Press I on the first frame to remove, then move to the first frame to keep and press X.".</summary>
        public static string StatusCutOutNoMark => Get(nameof(StatusCutOutNoMark));

        /// <summary>Gets text like "Move the playhead away from the start mark to choose the frames to remove.".</summary>
        public static string StatusCutOutEmpty => Get(nameof(StatusCutOutEmpty));

        /// <summary>Gets text like "Removed {0} &#x2013; {1}.".</summary>
        public static string StatusCutOutDone => Get(nameof(StatusCutOutDone));

        /// <summary>Gets text like "Exporting&#x2026; {0}".</summary>
        public static string StatusExporting => Get(nameof(StatusExporting));

        /// <summary>Gets text like "Exported {0} without re-encoding.".</summary>
        public static string StatusExportedOne => Get(nameof(StatusExportedOne));

        /// <summary>Gets text like "Exported {0} files to {1} without re-encoding.".</summary>
        public static string StatusExportedMany => Get(nameof(StatusExportedMany));

        /// <summary>Gets text like "Exported {0} on the exact frames. Only the frames at the cut points were re-encoded.".</summary>
        public static string StatusExportedOneExact => Get(nameof(StatusExportedOneExact));

        /// <summary>Gets text like "Exported {0} files to {1} on the exact frames. Only the frames at the cut points were re-encoded.".</summary>
        public static string StatusExportedManyExact => Get(nameof(StatusExportedManyExact));

        /// <summary>Gets text like "Export canceled. Partially written files were removed.".</summary>
        public static string StatusExportCanceled => Get(nameof(StatusExportCanceled));

        /// <summary>Gets text like "Export failed: {0}".</summary>
        public static string StatusExportFailed => Get(nameof(StatusExportFailed));

        /// <summary>Gets text like "This selection cannot be exported: {0}".</summary>
        public static string StatusCannotExport => Get(nameof(StatusCannotExport));

        /// <summary>Gets text like "Exact frame cutting supports H.264 and HEVC video, but this file uses {0}. Choose keyframe precision instead.".</summary>
        public static string StatusExactUnsupportedCodec => Get(nameof(StatusExactUnsupportedCodec));

        /// <summary>Gets text like "Unexpected error: {0}".</summary>
        public static string StatusUnexpectedError => Get(nameof(StatusUnexpectedError));

        /// <summary>Gets text like "Details were written to {0}.".</summary>
        public static string StatusDetailsInLog => Get(nameof(StatusDetailsInLog));

        /// <summary>Gets text like "The new language takes effect the next time trim-c starts.".</summary>
        public static string StatusLanguageRestart => Get(nameof(StatusLanguageRestart));

        /// <summary>Gets text like "trim-c now appears in &#x201C;Open with&#x201D; for video files.".</summary>
        public static string StatusOpenWithOn => Get(nameof(StatusOpenWithOn));

        /// <summary>Gets text like "trim-c was removed from &#x201C;Open with&#x201D;.".</summary>
        public static string StatusOpenWithOff => Get(nameof(StatusOpenWithOff));

        /// <summary>Gets text like "Could not update &#x201C;Open with&#x201D;: {0}".</summary>
        public static string StatusOpenWithFailed => Get(nameof(StatusOpenWithFailed));

        /// <summary>Gets text like "Undone.".</summary>
        public static string StatusUndone => Get(nameof(StatusUndone));

        /// <summary>Gets text like "Redone.".</summary>
        public static string StatusRedone => Get(nameof(StatusRedone));

        /// <summary>Gets text like "libmpv was not found. Place libmpv-2.dll next to trim-c.exe to enable the preview.".</summary>
        public static string PlayerLibraryMissing => Get(nameof(PlayerLibraryMissing));

        /// <summary>Gets text like "libmpv could not create a player.".</summary>
        public static string PlayerCreateFailed => Get(nameof(PlayerCreateFailed));

        /// <summary>Gets text like "Open video".</summary>
        public static string OpenDialogTitle => Get(nameof(OpenDialogTitle));

        /// <summary>Gets text like "Media files".</summary>
        public static string MediaFilesFilter => Get(nameof(MediaFilesFilter));

        /// <summary>Gets text like "Export to folder".</summary>
        public static string FolderDialogTitle => Get(nameof(FolderDialogTitle));

        /// <summary>Gets text like "Video (trim-c)".</summary>
        public static string ShellFileTypeName => Get(nameof(ShellFileTypeName));

        /// <summary>
        /// Formats a localized template with culture-aware formatting of its arguments.
        /// </summary>
        /// <param name="template">A template returned by one of the properties of this class.</param>
        /// <param name="arguments">The values for the placeholders of the template.</param>
        /// <returns>The formatted text.</returns>
        public static string Format(string template, params object?[] arguments) =>
            string.Format(CultureInfo.CurrentCulture, template, arguments);

        private static string Get(string name) =>
            s_resources.GetString(name, CultureInfo.CurrentUICulture) ?? throw new InvalidOperationException($"The resource '{name}' is missing.");
    }
}
