// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TrimC.Desktop.Controls;
using TrimC.Desktop.Formatting;
using TrimC.Desktop.Playback;
using TrimC.Desktop.Services;
using TrimC.Editing;
using TrimC.Export;
using TrimC.FFmpeg;
using TrimC.Media;

namespace TrimC.Desktop.ViewModels
{
    /// <summary>
    /// Coordinates the editing session: the loaded media, its cut list, preview playback and export.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The view model owns exactly one <see cref="CutList"/> per loaded file and treats it as the source of truth.
    /// Collections exposed to the view are projections that are rebuilt from the cut list whenever it raises
    /// <see cref="CutList.Changed"/>, so the view can never drift from the domain state.
    /// </para>
    /// <para>
    /// The player reports state on its own thread. Updates are marshalled to the UI thread through the injected
    /// <see cref="IDispatcher"/>, which keeps the view model free of thread affinity assumptions.
    /// </para>
    /// </remarks>
    internal sealed partial class MainWindowViewModel : ObservableObject, IDisposable
    {
        // After a user seek, reports still in flight from the player describe the old position. They are ignored until
        // the player arrives at the requested position, so rapid keyframe jumps build on each other instead of on stale
        // positions. The timeout covers seeks the player clamps or cannot complete, such as past the end of the file.
        private static readonly TimeSpan s_seekArrivalTolerance = TimeSpan.FromMilliseconds(50);
        private static readonly TimeSpan s_seekTimeout = TimeSpan.FromSeconds(1);

        private static readonly TimeSpan s_positionEpsilon = TimeSpan.FromMilliseconds(1);

        private readonly IVideoPlayer _player;
        private readonly MediaToolchain _toolchain;
        private readonly IFileDialogService _dialogs;
        private readonly IDispatcher _dispatcher;
        private readonly ILogger<MainWindowViewModel> _logger;

        private CutList? _cutList;
        private CancellationTokenSource? _loadCancellation;
        private TimeSpan? _pendingSeekTarget;
        private long _pendingSeekTimestamp;
        private bool _isOutputDirectoryUserSelected;
        private bool _isSelectingProgrammatically;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
        /// </summary>
        /// <param name="player">The preview player.</param>
        /// <param name="toolchain">The FFmpeg-backed media services.</param>
        /// <param name="dialogs">The file and folder pickers.</param>
        /// <param name="dispatcher">The UI thread dispatcher.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
        public MainWindowViewModel(
            IVideoPlayer player,
            MediaToolchain toolchain,
            IFileDialogService dialogs,
            IDispatcher dispatcher,
            ILogger<MainWindowViewModel> logger)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(toolchain);
            ArgumentNullException.ThrowIfNull(dialogs);
            ArgumentNullException.ThrowIfNull(dispatcher);
            ArgumentNullException.ThrowIfNull(logger);

            _player = player;
            _toolchain = toolchain;
            _dialogs = dialogs;
            _dispatcher = dispatcher;
            _logger = logger;

            SelectedContainer = ContainerOptions[0];
            SelectedMode = ModeOptions[0];
            SelectedCutMode = CutModeOptions[0];
            SelectedSnapMode = SnapModeOptions[0];

            StatusMessage = toolchain.Tools is null
                ? "FFmpeg was not found. Install FFmpeg or place ffmpeg and ffprobe next to the application."
                : "Open a file or drop it onto the window.";
            IsStatusError = toolchain.Tools is null;

            _player.StateChanged += OnPlayerStateChanged;
        }

        /// <summary>Gets the selectable output containers.</summary>
        public IReadOnlyList<ChoiceOption<ContainerFormat>> ContainerOptions { get; } =
        [
            new(ContainerFormat.SameAsSource, "Same as source"),
            new(ContainerFormat.Mp4, "MP4"),
            new(ContainerFormat.Matroska, "MKV"),
            new(ContainerFormat.QuickTime, "MOV"),
        ];

        /// <summary>Gets the selectable export modes.</summary>
        public IReadOnlyList<ChoiceOption<ExportMode>> ModeOptions { get; } =
        [
            new(ExportMode.SeparateFiles, "One file per segment"),
            new(ExportMode.Merge, "Merge segments"),
        ];

        /// <summary>Gets the selectable cut precisions.</summary>
        public IReadOnlyList<ChoiceOption<CutMode>> CutModeOptions { get; } =
        [
            new(CutMode.Keyframe, "Keyframe (lossless, instant)"),
            new(CutMode.FrameAccurate, "Exact frame (re-encodes cut points only)"),
        ];

        /// <summary>Gets the selectable keyframe alignment strategies.</summary>
        public IReadOnlyList<ChoiceOption<KeyframeSnapMode>> SnapModeOptions { get; } =
        [
            new(KeyframeSnapMode.Previous, "Previous keyframe (keep everything)"),
            new(KeyframeSnapMode.Next, "Next keyframe (nothing extra)"),
            new(KeyframeSnapMode.Nearest, "Nearest keyframe"),
        ];

        /// <summary>Gets the segments in timeline order, projected for the segment list.</summary>
        public ObservableCollection<SegmentViewModel> Segments { get; } = [];

        /// <summary>Gets the preview player, exposed so that the view can attach it to a native window.</summary>
        public IVideoPlayer Player => _player;

        /// <summary>Gets the loaded media, or <see langword="null"/> when no file is open.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
        [NotifyPropertyChangedFor(nameof(Title), nameof(HasMedia))]
        public partial MediaInfo? Media { get; private set; }

        /// <summary>Gets the keyframe index of the primary video stream, or <see langword="null"/> while it is being read.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
        public partial KeyframeIndex? Keyframes { get; private set; }

        /// <summary>Gets the duration of the loaded media.</summary>
        [ObservableProperty]
        public partial TimeSpan Duration { get; private set; }

        /// <summary>Gets the playhead position.</summary>
        [ObservableProperty]
        public partial TimeSpan Position { get; private set; }

        /// <summary>Gets a value indicating whether playback is paused.</summary>
        [ObservableProperty]
        public partial bool IsPaused { get; private set; } = true;

        /// <summary>Gets the pending in-point set with <see cref="SetMarkInCommand"/>.</summary>
        [ObservableProperty]
        public partial TimeSpan? MarkIn { get; private set; }

        /// <summary>Gets the segments as domain objects for the timeline control.</summary>
        [ObservableProperty]
        public partial IReadOnlyList<Segment> TimelineSegments { get; private set; } = [];

        /// <summary>Gets or sets the segment selected in the list and highlighted on the timeline.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedSegmentId))]
        [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand), nameof(SetSelectedStartCommand), nameof(SetSelectedEndCommand))]
        public partial SegmentViewModel? SelectedSegment { get; set; }

        /// <summary>Gets a one-line technical summary of the loaded media.</summary>
        [ObservableProperty]
        public partial string MediaSummary { get; private set; } = string.Empty;

        /// <summary>Gets the message shown in the status bar.</summary>
        [ObservableProperty]
        public partial string StatusMessage { get; private set; }

        /// <summary>Gets a value indicating whether <see cref="StatusMessage"/> describes an error.</summary>
        [ObservableProperty]
        public partial bool IsStatusError { get; private set; }

        /// <summary>Gets a message explaining why preview is unavailable, or <see langword="null"/>.</summary>
        [ObservableProperty]
        public partial string? PlayerMessage { get; private set; }

        /// <summary>Gets a value indicating whether an export is running.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ExportCommand), nameof(OpenFileCommand))]
        public partial bool IsExporting { get; private set; }

        /// <summary>Gets the export progress as a percentage.</summary>
        [ObservableProperty]
        public partial double ExportPercent { get; private set; }

        /// <summary>Gets the folder that receives exported files.</summary>
        [ObservableProperty]
        public partial string? OutputDirectory { get; private set; }

        /// <summary>Gets the first file written by the most recent successful export.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RevealLastExportCommand))]
        public partial string? LastExportedFile { get; private set; }

        /// <summary>Gets the combined duration of all segments.</summary>
        [ObservableProperty]
        public partial string SelectionSummary { get; private set; } = string.Empty;

        /// <summary>Gets or sets the output container.</summary>
        [ObservableProperty]
        public partial ChoiceOption<ContainerFormat> SelectedContainer { get; set; }

        /// <summary>Gets or sets the export mode.</summary>
        [ObservableProperty]
        public partial ChoiceOption<ExportMode> SelectedMode { get; set; }

        /// <summary>Gets or sets the cut precision.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsKeyframeMode))]
        public partial ChoiceOption<CutMode> SelectedCutMode { get; set; }

        /// <summary>Gets or sets the keyframe alignment strategy.</summary>
        [ObservableProperty]
        public partial ChoiceOption<KeyframeSnapMode> SelectedSnapMode { get; set; }

        /// <summary>Gets a value indicating whether keyframe alignment applies, which is only the case in keyframe mode.</summary>
        public bool IsKeyframeMode => SelectedCutMode.Value == CutMode.Keyframe;

        /// <summary>Gets the window title.</summary>
        public string Title => Media is null ? "trim-c" : $"{Path.GetFileName(Media.FilePath)} - trim-c";

        /// <summary>Gets a value indicating whether a file is loaded.</summary>
        public bool HasMedia => Media is not null;

        /// <summary>Gets the identifier of the selected segment for the timeline control.</summary>
        public Guid? SelectedSegmentId => SelectedSegment?.Segment.Id;

        /// <summary>
        /// Reports an error that escaped a command, so that the user sees it and the session stays usable.
        /// </summary>
        /// <param name="exception">The unexpected exception.</param>
        /// <param name="logFilePath">The log file that holds the details, or <see langword="null"/> if logging is unavailable.</param>
        public void ReportUnexpectedError(Exception exception, string? logFilePath)
        {
            ArgumentNullException.ThrowIfNull(exception);

            string details = logFilePath is null ? string.Empty : $" Details were written to {logFilePath}.";
            SetStatus($"Unexpected error: {exception.Message}{details}", isError: true);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _player.StateChanged -= OnPlayerStateChanged;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
        }

        /// <summary>
        /// Opens a media file, either the specified one or one chosen through the file picker.
        /// </summary>
        /// <param name="filePath">The file to open, or <see langword="null"/> to show the picker.</param>
        /// <returns>A task that completes when the file and its keyframes are loaded.</returns>
        [RelayCommand(CanExecute = nameof(CanOpenFile))]
        private async Task OpenFileAsync(string? filePath)
        {
            IMediaProbe? probe = _toolchain.Probe;
            if (probe is null)
            {
                SetStatus("FFmpeg was not found. Install FFmpeg or place ffmpeg and ffprobe next to the application.", isError: true);
                return;
            }

            filePath ??= await _dialogs.PickMediaFileAsync().ConfigureAwait(true);
            if (filePath is null)
            {
                return;
            }

            // Opening a new file abandons any keyframe scan still running for the previous one.
            if (_loadCancellation is not null)
            {
                await _loadCancellation.CancelAsync().ConfigureAwait(true);
                _loadCancellation.Dispose();
            }

            _loadCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = _loadCancellation.Token;

            try
            {
                SetStatus($"Reading {Path.GetFileName(filePath)}…");
                MediaInfo media = await probe.ProbeAsync(filePath, cancellationToken).ConfigureAwait(true);

                LoadMedia(media);
                _player.Open(filePath);

                MediaStreamInfo? video = media.PrimaryVideoStream;
                if (video is null)
                {
                    Keyframes = KeyframeIndex.Empty;
                    SetStatus("Ready. Audio-only media can be cut at any position.");
                    return;
                }

                SetStatus("Indexing keyframes…");
                KeyframeIndex keyframes = await probe.ReadKeyframesAsync(media, video.Index, cancellationToken).ConfigureAwait(true);
                Keyframes = keyframes;
                SetStatus(DescribeKeyframes(keyframes, media.Duration));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Superseded by another open request; that request reports its own status.
            }
            catch (Exception ex) when (ex is FFmpegException or IOException or UnauthorizedAccessException)
            {
                LogOpenFailed(filePath, ex);
                SetStatus(DescribeFailure($"Could not open {Path.GetFileName(filePath)}", ex), isError: true);
            }
        }

        private bool CanOpenFile(string? filePath) => !IsExporting;

        /// <summary>
        /// Moves the playhead in response to timeline interaction.
        /// </summary>
        /// <param name="request">The requested position.</param>
        [RelayCommand]
        private void Seek(TimelineSeekRequest? request)
        {
            if (request is null || Media is null)
            {
                return;
            }

            SeekTo(request.Position, request.IsFinal);
        }

        /// <summary>Toggles between playing and paused.</summary>
        [RelayCommand]
        private void TogglePlayback()
        {
            if (Media is not null)
            {
                _player.SetPaused(!IsPaused);
            }
        }

        /// <summary>Steps one frame forward.</summary>
        [RelayCommand]
        private void StepForward() => StepFrame(backward: false);

        /// <summary>Steps one frame backward.</summary>
        [RelayCommand]
        private void StepBackward() => StepFrame(backward: true);

        /// <summary>Jumps to the next keyframe, the next position where a lossless cut can start.</summary>
        [RelayCommand]
        private void NextKeyframe()
        {
            if (Keyframes?.FindAfter(Position + s_positionEpsilon) is TimeSpan next)
            {
                SeekTo(next, exact: true);
            }
        }

        /// <summary>Jumps to the previous keyframe.</summary>
        [RelayCommand]
        private void PreviousKeyframe()
        {
            if (Keyframes?.FindBefore(Position - s_positionEpsilon) is TimeSpan previous)
            {
                SeekTo(previous, exact: true);
            }
        }

        /// <summary>Sets the start of the next segment at the playhead.</summary>
        [RelayCommand]
        private void SetMarkIn()
        {
            if (Media is null)
            {
                return;
            }

            MarkIn = Position;
            SetStatus($"Start set at {Timecode.Format(Position)}. Move to the end and press O.");
        }

        /// <summary>
        /// Closes a segment at the playhead. Without a pending in-point, the segment starts where the gap before the playhead begins.
        /// </summary>
        [RelayCommand]
        private void SetMarkOut()
        {
            if (_cutList is null)
            {
                return;
            }

            TimeSpan end = Position;
            TimeSpan start = MarkIn ?? FindGapStart(_cutList, end);
            if (end <= start)
            {
                SetStatus("The end must be after the start.", isError: true);
                return;
            }

            TryEdit(() =>
            {
                Segment segment = _cutList.Add(new TimeRange(start, end));
                MarkIn = null;
                SelectSegment(segment.Id);
                SetStatus($"Segment added: {Timecode.Format(start)} to {Timecode.Format(end)}.");
            });
        }

        /// <summary>
        /// Removes the frames between the start mark and the playhead, keeping everything else.
        /// </summary>
        /// <remarks>
        /// This is the editing model used in film and broadcast work: mark two frames and cut out what lies between them.
        /// The removed range starts at the first marked frame and ends just before the frame under the playhead, so the
        /// frame under the playhead is the first one that is kept. With no segments yet, the whole media is the starting
        /// point.
        /// </remarks>
        [RelayCommand]
        private void CutOut()
        {
            if (_cutList is null)
            {
                return;
            }

            if (MarkIn is not TimeSpan mark)
            {
                SetStatus("Press I on the first frame to remove, then move to the first frame to keep and press X.", isError: true);
                return;
            }

            TimeSpan start = mark < Position ? mark : Position;
            TimeSpan end = mark < Position ? Position : mark;
            if (end <= start)
            {
                SetStatus("Move the playhead away from the start mark to choose the frames to remove.", isError: true);
                return;
            }

            TryEdit(() =>
            {
                _cutList.Exclude(new TimeRange(start, end));
                MarkIn = null;
                SetStatus($"Removed {Timecode.Format(start)} to {Timecode.Format(end)}. Export with exact frame precision to cut on these frames.");
            });
        }

        /// <summary>Moves the start of the selected segment to the playhead.</summary>
        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void SetSelectedStart()
        {
            if (_cutList is null || SelectedSegment is null)
            {
                return;
            }

            TimeRange range = SelectedSegment.Segment.Range;
            if (Position >= range.End)
            {
                SetStatus("The start must be before the end of the segment.", isError: true);
                return;
            }

            Guid id = SelectedSegment.Segment.Id;
            TryEdit(() => _cutList.SetRange(id, new TimeRange(Position, range.End)));
        }

        /// <summary>Moves the end of the selected segment to the playhead.</summary>
        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void SetSelectedEnd()
        {
            if (_cutList is null || SelectedSegment is null)
            {
                return;
            }

            TimeRange range = SelectedSegment.Segment.Range;
            if (Position <= range.Start)
            {
                SetStatus("The end must be after the start of the segment.", isError: true);
                return;
            }

            Guid id = SelectedSegment.Segment.Id;
            TryEdit(() => _cutList.SetRange(id, new TimeRange(range.Start, Position)));
        }

        /// <summary>Splits the segment under the playhead in two.</summary>
        [RelayCommand]
        private void Split()
        {
            if (_cutList is null)
            {
                return;
            }

            if (_cutList.Split(Position) is null)
            {
                SetStatus("Place the playhead inside a segment to split it.", isError: true);
            }
        }

        /// <summary>Removes the selected segment.</summary>
        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void RemoveSelected()
        {
            if (_cutList is not null && SelectedSegment is not null)
            {
                _cutList.Remove(SelectedSegment.Segment.Id);
            }
        }

        /// <summary>Removes every segment.</summary>
        [RelayCommand]
        private void ClearSegments()
        {
            _cutList?.Clear();
            MarkIn = null;
        }

        /// <summary>Replaces the segments with the parts between them, turning "parts to remove" into "parts to keep".</summary>
        [RelayCommand]
        private void InvertSegments() => _cutList?.Invert();

        /// <summary>Chooses the folder that receives exported files.</summary>
        /// <returns>A task that completes when the folder picker closes.</returns>
        [RelayCommand]
        private async Task ChooseOutputDirectoryAsync()
        {
            string? folder = await _dialogs.PickFolderAsync(OutputDirectory).ConfigureAwait(true);
            if (folder is not null)
            {
                OutputDirectory = folder;
                _isOutputDirectoryUserSelected = true;
            }
        }

        /// <summary>
        /// Exports the segments without re-encoding.
        /// </summary>
        /// <param name="cancellationToken">Signaled by the generated cancel command.</param>
        /// <returns>A task that completes when the export finishes, fails or is canceled.</returns>
        [RelayCommand(CanExecute = nameof(CanExport), IncludeCancelCommand = true)]
        private async Task ExportAsync(CancellationToken cancellationToken)
        {
            if (Media is null || Keyframes is null || _cutList is null || _toolchain.Executor is null || OutputDirectory is null)
            {
                return;
            }

            ExportOptions options = new()
            {
                OutputDirectory = OutputDirectory,
                Container = SelectedContainer.Value,
                Mode = SelectedMode.Value,
                CutMode = SelectedCutMode.Value,
                SnapMode = SelectedSnapMode.Value,
            };

            ExportPlan plan;
            try
            {
                plan = ExportPlanner.CreatePlan(Media, Keyframes, _cutList.Segments, options);
            }
            catch (ArgumentException ex)
            {
                SetStatus(ex.Message, isError: true);
                return;
            }

            IsExporting = true;
            ExportPercent = 0;
            LastExportedFile = null;
            SetStatus("Exporting…");

            // Progress<T> captures the UI synchronization context here, so reports arrive on the UI thread.
            Progress<ExportProgress> progress = new(p =>
            {
                ExportPercent = p.Fraction * 100;
                StatusMessage = string.Create(CultureInfo.InvariantCulture, $"Exporting step {p.StepNumber} of {p.StepCount}… {p.Fraction:P0}");
            });

            try
            {
                await _toolchain.Executor.ExecuteAsync(plan, progress, cancellationToken).ConfigureAwait(true);

                LastExportedFile = plan.OutputFiles[0];
                string written = plan.OutputFiles.Count == 1
                    ? $"Exported {Path.GetFileName(plan.OutputFiles[0])}"
                    : $"Exported {plan.OutputFiles.Count} files to {OutputDirectory}";
                SetStatus(options.CutMode == CutMode.FrameAccurate
                    ? $"{written} on the exact frames. Only the frames at the cut points were re-encoded."
                    : $"{written} without re-encoding.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetStatus("Export canceled. Partially written files were removed.");
            }
            catch (Exception ex) when (ex is FFmpegException or IOException or UnauthorizedAccessException)
            {
                LogExportFailed(ex);
                SetStatus(DescribeFailure("Export failed", ex), isError: true);
            }
            finally
            {
                IsExporting = false;
                ExportPercent = 0;
            }
        }

        private bool CanExport() => Media is not null && Keyframes is not null && _cutList is { Count: > 0 } && !IsExporting;

        /// <summary>Shows the most recently exported file in the file manager.</summary>
        [RelayCommand(CanExecute = nameof(CanRevealLastExport))]
        private void RevealLastExport()
        {
            if (LastExportedFile is not null)
            {
                FileManager.Reveal(LastExportedFile);
            }
        }

        private bool CanRevealLastExport() => LastExportedFile is not null;

        private bool HasSelection() => SelectedSegment is not null;

        partial void OnSelectedSegmentChanged(SegmentViewModel? value)
        {
            // Picking a segment in the list previews its first frame, which is what the user is about to judge. Selections
            // made by the view model itself, such as highlighting a segment that was just closed with O, keep the playhead
            // where the user left it.
            if (!_isSelectingProgrammatically && value is not null && (Position < value.Segment.Range.Start || Position >= value.Segment.Range.End))
            {
                SeekTo(value.Segment.Range.Start, exact: true);
            }
        }

        private void LoadMedia(MediaInfo media)
        {
            if (_cutList is not null)
            {
                _cutList.Changed -= OnCutListChanged;
            }

            _cutList = new CutList(media.Duration);
            _cutList.Changed += OnCutListChanged;

            Media = media;
            Keyframes = null;
            Duration = media.Duration;
            Position = TimeSpan.Zero;
            MarkIn = null;
            LastExportedFile = null;
            MediaSummary = DescribeMedia(media);

            if (!_isOutputDirectoryUserSelected)
            {
                OutputDirectory = Path.GetDirectoryName(media.FilePath);
            }

            OnCutListChanged(_cutList, EventArgs.Empty);
        }

        private void OnCutListChanged(object? sender, EventArgs e)
        {
            if (_cutList is null)
            {
                return;
            }

            IReadOnlyList<Segment> segments = _cutList.Segments;
            Guid? selectedId = SelectedSegment?.Segment.Id;

            // Label edits change no geometry; rebuilding the list in that case would recreate the text box being typed in.
            if (!HasSameGeometry(segments))
            {
                Segments.Clear();
                for (int i = 0; i < segments.Count; i++)
                {
                    Segments.Add(new SegmentViewModel(segments[i], i + 1, RenameSegment));
                }

                SelectSegment(selectedId);
            }

            TimelineSegments = segments;
            SelectionSummary = segments.Count == 0
                ? "No segments. Press I at the start and O at the end of a part to keep."
                : $"{segments.Count} segment(s), {Timecode.Format(_cutList.TotalDuration)} total";
            ExportCommand.NotifyCanExecuteChanged();
        }

        private bool HasSameGeometry(IReadOnlyList<Segment> segments)
        {
            if (segments.Count != Segments.Count)
            {
                return false;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].Id != Segments[i].Segment.Id || segments[i].Range != Segments[i].Segment.Range)
                {
                    return false;
                }
            }

            return true;
        }

        private void RenameSegment(Guid id, string? label) => _cutList?.SetLabel(id, label);

        private void SelectSegment(Guid? id)
        {
            SegmentViewModel? match = null;
            foreach (SegmentViewModel segment in Segments)
            {
                if (segment.Segment.Id == id)
                {
                    match = segment;
                    break;
                }
            }

            _isSelectingProgrammatically = true;
            try
            {
                SelectedSegment = match;
            }
            finally
            {
                _isSelectingProgrammatically = false;
            }
        }

        private void SeekTo(TimeSpan position, bool exact)
        {
            _pendingSeekTarget = position;
            _pendingSeekTimestamp = Environment.TickCount64;
            Position = position;
            _player.Seek(position, exact);
        }

        private void StepFrame(bool backward)
        {
            if (Media is null)
            {
                return;
            }

            if (_player.IsReady)
            {
                _player.StepFrame(backward);
                return;
            }

            // Without preview the playhead still moves by one nominal frame so that cut points can be set precisely.
            double frameRate = Media.PrimaryVideoStream?.FrameRate ?? 30;
            TimeSpan frame = TimeSpan.FromSeconds(1 / frameRate);
            TimeSpan target = backward ? Position - frame : Position + frame;
            Position = target < TimeSpan.Zero ? TimeSpan.Zero : target > Duration ? Duration : target;
        }

        private void TryEdit(Action edit)
        {
            try
            {
                edit();
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentOutOfRangeException)
            {
                SetStatus(ex.Message, isError: true);
            }
        }

        private void OnPlayerStateChanged(object? sender, EventArgs e) => _dispatcher.Post(SyncFromPlayer);

        private void SyncFromPlayer()
        {
            IsPaused = _player.IsPaused;
            PlayerMessage = _player.FailureReason;

            if (!_player.IsFileLoaded)
            {
                return;
            }

            TimeSpan reported = _player.Position;
            if (_pendingSeekTarget is TimeSpan target)
            {
                bool arrived = (reported - target).Duration() <= s_seekArrivalTolerance;
                bool timedOut = Environment.TickCount64 - _pendingSeekTimestamp > s_seekTimeout.TotalMilliseconds;
                if (!arrived && !timedOut)
                {
                    return;
                }

                _pendingSeekTarget = null;
            }

            Position = reported;
        }

        private void SetStatus(string message, bool isError = false)
        {
            StatusMessage = message;
            IsStatusError = isError;
        }

        private static TimeSpan FindGapStart(CutList cutList, TimeSpan position)
        {
            TimeSpan start = TimeSpan.Zero;
            foreach (Segment segment in cutList.Segments)
            {
                if (segment.Range.End <= position && segment.Range.End > start)
                {
                    start = segment.Range.End;
                }
            }

            return start;
        }

        private static string DescribeMedia(MediaInfo media)
        {
            StringBuilder summary = new();
            MediaStreamInfo? video = media.PrimaryVideoStream;
            if (video is not null)
            {
                summary.Append(CultureInfo.InvariantCulture, $"{video.CodecName.ToUpperInvariant()} {video.Width}×{video.Height}");
                if (video.FrameRate is double fps)
                {
                    summary.Append(CultureInfo.InvariantCulture, $" {fps:0.##} fps");
                }
            }

            foreach (MediaStreamInfo stream in media.Streams)
            {
                if (stream.Kind == StreamKind.Audio)
                {
                    summary.Append(CultureInfo.InvariantCulture, $" · {stream.CodecName.ToUpperInvariant()} {stream.Channels}ch");
                    break;
                }
            }

            if (media.BitRate is long bitRate)
            {
                summary.Append(" · ").Append(Timecode.FormatBitRate(bitRate));
            }

            summary.Append(" · ").Append(Timecode.Format(media.Duration));
            return summary.ToString();
        }

        private static string DescribeKeyframes(KeyframeIndex keyframes, TimeSpan duration)
        {
            if (keyframes.Count < 2)
            {
                return "Ready.";
            }

            double averageGop = duration.TotalSeconds / keyframes.Count;
            return string.Create(CultureInfo.InvariantCulture, $"Ready. {keyframes.Count} keyframes, one every {averageGop:0.##} s on average.");
        }

        private static string DescribeFailure(string summary, Exception exception)
        {
            // ffmpeg's own diagnostic is more precise than any message the application could compose, so its last line is shown.
            string? detail = exception is FFmpegException { StandardError: { Length: > 0 } standardError }
                ? standardError.Trim().Split('\n')[^1].Trim()
                : exception.Message;

            return $"{summary}: {detail}";
        }

        [LoggerMessage(Level = LogLevel.Error, Message = "Could not open {FilePath}")]
        private partial void LogOpenFailed(string filePath, Exception exception);

        [LoggerMessage(Level = LogLevel.Error, Message = "Export failed")]
        private partial void LogExportFailed(Exception exception);
    }
}
