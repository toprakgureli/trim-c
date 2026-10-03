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
using TrimC.Desktop.Diagnostics;
using TrimC.Desktop.Formatting;
using TrimC.Desktop.Playback;
using TrimC.Desktop.Resources;
using TrimC.Desktop.Services;
using TrimC.Desktop.Settings;
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
    /// Every change to the cut list goes through <see cref="EditHistory"/>, which makes all edits undoable: the trim
    /// handles, the manual tools and the segment list alike. A newly opened file starts with one segment that covers
    /// the whole media, so the plain workflow is to drag its two handles and export.
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
        private readonly IExportDialogService _exportDialog;
        private readonly IShellIntegration _shellIntegration;
        private readonly SettingsStore _settings;
        private readonly IDispatcher _dispatcher;
        private readonly ILogger<MainWindowViewModel> _logger;

        private CutList? _cutList;
        private EditHistory? _history;
        private IReadOnlyList<Segment>? _trimSnapshot;
        private CancellationTokenSource? _loadCancellation;
        private TimeSpan? _pendingSeekTarget;
        private long _pendingSeekTimestamp;
        private bool _isSelectingProgrammatically;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
        /// </summary>
        /// <param name="player">The preview player.</param>
        /// <param name="toolchain">The FFmpeg-backed media services.</param>
        /// <param name="dialogs">The file and folder pickers.</param>
        /// <param name="exportDialog">The export settings dialog.</param>
        /// <param name="shellIntegration">The "Open with" registration.</param>
        /// <param name="settings">The persisted preferences.</param>
        /// <param name="dispatcher">The UI thread dispatcher.</param>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
        public MainWindowViewModel(
            IVideoPlayer player,
            MediaToolchain toolchain,
            IFileDialogService dialogs,
            IExportDialogService exportDialog,
            IShellIntegration shellIntegration,
            SettingsStore settings,
            IDispatcher dispatcher,
            ILogger<MainWindowViewModel> logger)
        {
            ArgumentNullException.ThrowIfNull(player);
            ArgumentNullException.ThrowIfNull(toolchain);
            ArgumentNullException.ThrowIfNull(dialogs);
            ArgumentNullException.ThrowIfNull(exportDialog);
            ArgumentNullException.ThrowIfNull(shellIntegration);
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(dispatcher);
            ArgumentNullException.ThrowIfNull(logger);

            _player = player;
            _toolchain = toolchain;
            _dialogs = dialogs;
            _exportDialog = exportDialog;
            _shellIntegration = shellIntegration;
            _settings = settings;
            _dispatcher = dispatcher;
            _logger = logger;

            IsAdvancedPanelOpen = settings.Current.IsAdvancedPanelOpen;
            StatusMessage = toolchain.Probe is null ? Strings.StatusFFmpegMissing : Strings.StatusWelcome;
            IsStatusError = toolchain.Probe is null;

            _player.StateChanged += OnPlayerStateChanged;
        }

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

        /// <summary>Gets the pending start mark set with <see cref="SetMarkInCommand"/>.</summary>
        [ObservableProperty]
        public partial TimeSpan? MarkIn { get; private set; }

        /// <summary>Gets the segments as domain objects for the timeline control.</summary>
        [ObservableProperty]
        public partial IReadOnlyList<Segment> TimelineSegments { get; private set; } = [];

        /// <summary>Gets or sets the segment selected in the list and on the timeline.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SelectedSegmentId))]
        [NotifyCanExecuteChangedFor(nameof(RemoveSelectedCommand), nameof(SetSelectedStartCommand), nameof(SetSelectedEndCommand))]
        public partial SegmentViewModel? SelectedSegment { get; set; }

        /// <summary>Gets a one-line technical summary of the loaded media.</summary>
        [ObservableProperty]
        public partial string MediaSummary { get; private set; } = string.Empty;

        /// <summary>Gets a description of what will be exported, shown under the video.</summary>
        [ObservableProperty]
        public partial string TrimSummary { get; private set; } = string.Empty;

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

        /// <summary>Gets the first file written by the most recent successful export.</summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RevealLastExportCommand))]
        public partial string? LastExportedFile { get; private set; }

        /// <summary>Gets or sets a value indicating whether the advanced editing panel is shown.</summary>
        [ObservableProperty]
        public partial bool IsAdvancedPanelOpen { get; set; }

        /// <summary>Gets the window title.</summary>
        public string Title => Media is null ? "trim-c" : $"{Path.GetFileName(Media.FilePath)} - trim-c";

        /// <summary>Gets a value indicating whether a file is loaded.</summary>
        public bool HasMedia => Media is not null;

        /// <summary>Gets the identifier of the selected segment for the timeline control.</summary>
        public Guid? SelectedSegmentId => SelectedSegment?.Segment.Id;

        /// <summary>Gets a value indicating whether the "Open with" integration is available on this platform.</summary>
        public bool CanIntegrateWithShell => _shellIntegration.IsSupported;

        /// <summary>Gets or sets a value indicating whether trim-c is offered in the "Open with" menu of video files.</summary>
        public bool ShowInOpenWith
        {
            get => _settings.Current.ShowInOpenWith;
            set
            {
                if (value == _settings.Current.ShowInOpenWith)
                {
                    return;
                }

                _settings.Save(_settings.Current with { ShowInOpenWith = value });
                OnPropertyChanged();
                ApplyShellIntegration(reportResult: true);
            }
        }

        /// <summary>Gets a value indicating whether the interface follows the Windows language.</summary>
        public bool IsLanguageSystem => _settings.Current.Language.Length == 0;

        /// <summary>Gets a value indicating whether the interface is in English.</summary>
        public bool IsLanguageEnglish => _settings.Current.Language == "en";

        /// <summary>Gets a value indicating whether the interface is in Turkish.</summary>
        public bool IsLanguageTurkish => _settings.Current.Language == "tr";

        /// <inheritdoc/>
        public void Dispose()
        {
            _player.StateChanged -= OnPlayerStateChanged;
            _loadCancellation?.Cancel();
            _loadCancellation?.Dispose();
        }

        /// <summary>
        /// Registers or removes the "Open with" entries according to the current setting.
        /// </summary>
        /// <param name="reportResult">Whether the outcome is shown in the status bar.</param>
        public void ApplyShellIntegration(bool reportResult)
        {
            if (!_shellIntegration.IsSupported || Environment.ProcessPath is not string executable)
            {
                return;
            }

            try
            {
                if (ShowInOpenWith)
                {
                    _shellIntegration.Register(executable);
                }
                else
                {
                    _shellIntegration.Unregister();
                }

                if (reportResult)
                {
                    SetStatus(ShowInOpenWith ? Strings.StatusOpenWithOn : Strings.StatusOpenWithOff);
                }
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                LogShellIntegrationFailed(ex);
                if (reportResult)
                {
                    SetStatus(Strings.Format(Strings.StatusOpenWithFailed, ex.Message), isError: true);
                }
            }
        }

        /// <summary>
        /// Reports an error that escaped a command, so that the user sees it and the session stays usable.
        /// </summary>
        /// <param name="exception">The unexpected exception.</param>
        /// <param name="logFilePath">The log file that holds the details, or <see langword="null"/> if logging is unavailable.</param>
        public void ReportUnexpectedError(Exception exception, string? logFilePath)
        {
            ArgumentNullException.ThrowIfNull(exception);

            string message = Strings.Format(Strings.StatusUnexpectedError, exception.Message);
            if (logFilePath is not null)
            {
                message += " " + Strings.Format(Strings.StatusDetailsInLog, logFilePath);
            }

            SetStatus(message, isError: true);
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
                SetStatus(Strings.StatusFFmpegMissing, isError: true);
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
                SetStatus(Strings.Format(Strings.StatusReading, Path.GetFileName(filePath)));
                MediaInfo media = await probe.ProbeAsync(filePath, cancellationToken).ConfigureAwait(true);

                LoadMedia(media);
                _player.Open(filePath);

                MediaStreamInfo? video = media.PrimaryVideoStream;
                if (video is null)
                {
                    Keyframes = KeyframeIndex.Empty;
                    SetStatus(Strings.StatusReadyAudio);
                    return;
                }

                SetStatus(Strings.StatusIndexing);
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
                SetStatus(Strings.Format(Strings.StatusOpenFailed, Path.GetFileName(filePath), DescribeFailure(ex)), isError: true);
            }
        }

        private bool CanOpenFile(string? filePath) => !IsExporting;

        /// <summary>Moves the playhead in response to timeline interaction.</summary>
        /// <param name="request">The requested position.</param>
        [RelayCommand]
        private void Seek(TimelineSeekRequest? request)
        {
            if (request is not null && Media is not null)
            {
                SeekTo(request.Position, request.IsFinal);
            }
        }

        /// <summary>Selects the segment that was clicked on the timeline.</summary>
        /// <param name="segmentId">The identifier of the clicked segment.</param>
        [RelayCommand]
        private void SelectSegment(Guid segmentId) => SelectSegmentById(segmentId, seek: false);

        /// <summary>
        /// Applies a trim handle drag. The whole drag is recorded as one undoable edit, and the preview follows the handle
        /// so that the user sees the frame they are about to cut at.
        /// </summary>
        /// <param name="request">The state of the drag.</param>
        [RelayCommand]
        private void Trim(TimelineTrimRequest? request)
        {
            if (request is null || _cutList is null || _history is null)
            {
                return;
            }

            if (request.Phase == TrimPhase.Started)
            {
                _trimSnapshot = _history.Capture();
                SelectSegmentById(request.SegmentId, seek: false);
            }

            // Pointer positions fall between frames; snapping keeps every boundary on a real frame, which is what an exact
            // cut needs and what the timecodes in the summary should show.
            Segment segment = _cutList.MoveEdge(request.SegmentId, request.Edge, SnapToFrame(request.Position), FrameDuration());
            TimeSpan edgePosition = request.Edge == SegmentEdge.Start ? segment.Range.Start : segment.Range.End;

            // The end of a range is exclusive, so the preview shows the last kept frame rather than the first removed one.
            TimeSpan preview = request.Edge == SegmentEdge.End ? edgePosition - FrameDuration() : edgePosition;
            SeekTo(preview < TimeSpan.Zero ? TimeSpan.Zero : preview, exact: request.Phase == TrimPhase.Completed);

            if (request.Phase == TrimPhase.Completed && _trimSnapshot is not null)
            {
                _history.Commit(_trimSnapshot);
                _trimSnapshot = null;
            }
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

        /// <summary>Undoes the most recent edit.</summary>
        [RelayCommand(CanExecute = nameof(CanUndo))]
        private void Undo()
        {
            if (_history?.Undo() == true)
            {
                SetStatus(Strings.StatusUndone);
            }
        }

        private bool CanUndo() => _history?.CanUndo == true && !IsExporting;

        /// <summary>Reapplies the most recently undone edit.</summary>
        [RelayCommand(CanExecute = nameof(CanRedo))]
        private void Redo()
        {
            if (_history?.Redo() == true)
            {
                SetStatus(Strings.StatusRedone);
            }
        }

        private bool CanRedo() => _history?.CanRedo == true && !IsExporting;

        /// <summary>Shows or hides the advanced editing panel.</summary>
        [RelayCommand]
        private void ToggleAdvancedPanel() => IsAdvancedPanelOpen = !IsAdvancedPanelOpen;

        /// <summary>Marks the first frame of a new segment, or of a part to cut out.</summary>
        [RelayCommand]
        private void SetMarkIn()
        {
            if (Media is null)
            {
                return;
            }

            MarkIn = Position;
            SetStatus(Strings.Format(Strings.StatusMarkIn, Timecode.Format(Position)));
        }

        /// <summary>
        /// Closes a segment at the playhead. While the selection is still the untouched whole video, the new segment
        /// replaces it, because marking a start and an end is a way of choosing what to keep.
        /// </summary>
        [RelayCommand]
        private void SetMarkOut()
        {
            if (_cutList is null || _history is null)
            {
                return;
            }

            TimeSpan end = Position;
            bool replacesWholeVideo = IsWholeVideoSelected();
            TimeSpan start = MarkIn ?? (replacesWholeVideo ? TimeSpan.Zero : FindGapStart(_cutList, end));
            if (end <= start)
            {
                SetStatus(Strings.StatusEndBeforeStart, isError: true);
                return;
            }

            Segment? added = null;
            TryEdit(list =>
            {
                if (replacesWholeVideo)
                {
                    list.Clear();
                }

                added = list.Add(new TimeRange(start, end));
            });

            if (added is not null)
            {
                MarkIn = null;
                SelectSegmentById(added.Id, seek: false);
                SetStatus(Strings.Format(Strings.StatusSegmentAdded, Timecode.Format(start), Timecode.Format(end)));
            }
        }

        /// <summary>Moves the start of the selected segment to the playhead.</summary>
        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void SetSelectedStart() => MoveSelectedEdge(SegmentEdge.Start);

        /// <summary>Moves the end of the selected segment to the playhead.</summary>
        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void SetSelectedEnd() => MoveSelectedEdge(SegmentEdge.End);

        /// <summary>Splits the segment under the playhead in two.</summary>
        [RelayCommand]
        private void Split()
        {
            if (_cutList is null || _history is null)
            {
                return;
            }

            if (_cutList.FindAt(Position) is not Segment segment || segment.Range.Start == Position)
            {
                SetStatus(Strings.StatusSplitOutside, isError: true);
                return;
            }

            TryEdit(list => list.Split(Position));
        }

        /// <summary>Removes the frames between the start mark and the playhead, keeping everything else.</summary>
        /// <remarks>
        /// This is the editing model used in film and broadcast work: mark two frames and cut out what lies between them.
        /// The removed range starts at the first marked frame and ends just before the frame under the playhead, so the
        /// frame under the playhead is the first one that is kept.
        /// </remarks>
        [RelayCommand]
        private void CutOut()
        {
            if (_cutList is null || _history is null)
            {
                return;
            }

            if (MarkIn is not TimeSpan mark)
            {
                SetStatus(Strings.StatusCutOutNoMark, isError: true);
                return;
            }

            TimeSpan start = mark < Position ? mark : Position;
            TimeSpan end = mark < Position ? Position : mark;
            if (end <= start)
            {
                SetStatus(Strings.StatusCutOutEmpty, isError: true);
                return;
            }

            if (TryEdit(list => list.Exclude(new TimeRange(start, end))))
            {
                MarkIn = null;
                SetStatus(Strings.Format(Strings.StatusCutOutDone, Timecode.Format(start), Timecode.Format(end)));
            }
        }

        /// <summary>Removes the selected segment.</summary>
        [RelayCommand(CanExecute = nameof(HasSelection))]
        private void RemoveSelected()
        {
            if (SelectedSegment is not null)
            {
                Guid id = SelectedSegment.Segment.Id;
                TryEdit(list => list.Remove(id));
            }
        }

        /// <summary>Selects the whole video again, discarding every segment.</summary>
        [RelayCommand]
        private void ResetSegments()
        {
            if (_cutList is null)
            {
                return;
            }

            TimeSpan duration = _cutList.MediaDuration;
            TryEdit(list =>
            {
                list.Clear();
                list.Add(new TimeRange(TimeSpan.Zero, duration));
            });
            MarkIn = null;
        }

        /// <summary>Replaces the segments with the parts between them, turning "parts to remove" into "parts to keep".</summary>
        [RelayCommand]
        private void InvertSegments() => TryEdit(list => list.Invert());

        /// <summary>Opens the export dialog and exports the segments with the confirmed settings.</summary>
        /// <param name="cancellationToken">Signaled by the generated cancel command.</param>
        /// <returns>A task that completes when the export finishes, fails or is canceled.</returns>
        [RelayCommand(CanExecute = nameof(CanExport), IncludeCancelCommand = true)]
        private async Task ExportAsync(CancellationToken cancellationToken)
        {
            if (Media is null || Keyframes is null || _cutList is null || _toolchain.Executor is null)
            {
                return;
            }

            IReadOnlyList<Segment> segments = _cutList.Segments;
            string sourceDirectory = Path.GetDirectoryName(Media.FilePath) ?? Environment.CurrentDirectory;
            ExportDialogViewModel dialog = new(_settings.Current, segments.Count, _cutList.TotalDuration, sourceDirectory, _dialogs);
            if (!await _exportDialog.ShowAsync(dialog).ConfigureAwait(true))
            {
                return;
            }

            AppSettings settings = dialog.ApplyTo(_settings.Current);
            _settings.Save(settings);

            if (settings.CutMode == CutMode.FrameAccurate && Media.PrimaryVideoStream is { CodecName: not ("h264" or "hevc") } video)
            {
                SetStatus(Strings.Format(Strings.StatusExactUnsupportedCodec, video.CodecName), isError: true);
                return;
            }

            ExportOptions options = new()
            {
                OutputDirectory = dialog.EffectiveOutputDirectory,
                Container = settings.Container,
                Mode = settings.Mode,
                CutMode = settings.CutMode,
                SnapMode = settings.SnapMode,
            };

            ExportPlan plan;
            try
            {
                plan = ExportPlanner.CreatePlan(Media, Keyframes, segments, options);
            }
            catch (ArgumentException ex)
            {
                SetStatus(Strings.Format(Strings.StatusCannotExport, ex.Message), isError: true);
                return;
            }

            await RunExportAsync(plan, options, settings.OpenFolderWhenDone, cancellationToken).ConfigureAwait(true);
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

        /// <summary>Opens the folder that holds the log files.</summary>
        [RelayCommand]
        private void OpenLogFolder()
        {
            Directory.CreateDirectory(FileLoggerProvider.DefaultDirectory);
            LogOpeningLogFolder(FileLoggerProvider.DefaultDirectory);
            FileManager.OpenFolder(FileLoggerProvider.DefaultDirectory);
        }

        /// <summary>Stores the interface language, which takes effect at the next start.</summary>
        /// <param name="language">A culture name such as <c>en</c> or <c>tr</c>, or an empty string to follow Windows.</param>
        [RelayCommand]
        private void SetLanguage(string? language)
        {
            language ??= string.Empty;
            if (language == _settings.Current.Language)
            {
                return;
            }

            _settings.Save(_settings.Current with { Language = language });
            OnPropertyChanged(nameof(IsLanguageSystem));
            OnPropertyChanged(nameof(IsLanguageEnglish));
            OnPropertyChanged(nameof(IsLanguageTurkish));
            SetStatus(Strings.StatusLanguageRestart);
        }

        private bool HasSelection() => SelectedSegment is not null;

        partial void OnSelectedSegmentChanged(SegmentViewModel? value)
        {
            // Picking a segment in the list previews its first frame, which is what the user is about to judge. Selections
            // made by the view model itself keep the playhead where the user left it.
            if (!_isSelectingProgrammatically && value is not null && (Position < value.Segment.Range.Start || Position >= value.Segment.Range.End))
            {
                SeekTo(value.Segment.Range.Start, exact: true);
            }
        }

        partial void OnIsAdvancedPanelOpenChanged(bool value) =>
            _settings.Save(_settings.Current with { IsAdvancedPanelOpen = value });

        partial void OnIsExportingChanged(bool value)
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }

        private async Task RunExportAsync(ExportPlan plan, ExportOptions options, bool openFolderWhenDone, CancellationToken cancellationToken)
        {
            IsExporting = true;
            ExportPercent = 0;
            LastExportedFile = null;
            SetStatus(Strings.Format(Strings.StatusExporting, 0.ToString("P0", CultureInfo.CurrentCulture)));

            // Progress<T> captures the UI synchronization context here, so reports arrive on the UI thread.
            Progress<ExportProgress> progress = new(p =>
            {
                ExportPercent = p.Fraction * 100;
                StatusMessage = Strings.Format(Strings.StatusExporting, p.Fraction.ToString("P0", CultureInfo.CurrentCulture));
            });

            try
            {
                await _toolchain.Executor!.ExecuteAsync(plan, progress, cancellationToken).ConfigureAwait(true);

                LastExportedFile = plan.OutputFiles[0];
                SetStatus(DescribeExport(plan, options));

                if (openFolderWhenDone)
                {
                    FileManager.Reveal(LastExportedFile);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SetStatus(Strings.StatusExportCanceled);
            }
            catch (Exception ex) when (ex is FFmpegException or IOException or UnauthorizedAccessException)
            {
                LogExportFailed(ex);
                SetStatus(Strings.Format(Strings.StatusExportFailed, DescribeFailure(ex)), isError: true);
            }
            finally
            {
                IsExporting = false;
                ExportPercent = 0;
            }
        }

        private void LoadMedia(MediaInfo media)
        {
            if (_cutList is not null)
            {
                _cutList.Changed -= OnCutListChanged;
            }

            if (_history is not null)
            {
                _history.Changed -= OnHistoryChanged;
            }

            // A new file starts with the whole media selected, so trimming is a matter of dragging the two handles.
            _cutList = new CutList(media.Duration);
            _cutList.Add(new TimeRange(TimeSpan.Zero, media.Duration));
            _cutList.Changed += OnCutListChanged;
            _history = new EditHistory(_cutList);
            _history.Changed += OnHistoryChanged;
            _trimSnapshot = null;

            Media = media;
            Keyframes = null;
            Duration = media.Duration;
            Position = TimeSpan.Zero;
            MarkIn = null;
            LastExportedFile = null;
            MediaSummary = DescribeMedia(media);

            OnCutListChanged(_cutList, EventArgs.Empty);
            OnHistoryChanged(_history, EventArgs.Empty);
        }

        private void OnHistoryChanged(object? sender, EventArgs e)
        {
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
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
            if (!HasSameIdentities(segments))
            {
                Segments.Clear();
                for (int i = 0; i < segments.Count; i++)
                {
                    Segments.Add(new SegmentViewModel(segments[i], i + 1, RenameSegment));
                }
            }
            else
            {
                for (int i = 0; i < segments.Count; i++)
                {
                    if (Segments[i].Segment.Range != segments[i].Range)
                    {
                        Segments[i] = new SegmentViewModel(segments[i], i + 1, RenameSegment);
                    }
                }
            }

            // A single segment is always the one the handles act on; otherwise the previous selection is kept if it survived.
            SelectSegmentById(segments.Count == 1 ? segments[0].Id : selectedId, seek: false);

            TimelineSegments = segments;
            TrimSummary = DescribeSelection(segments, _cutList.TotalDuration);
            ExportCommand.NotifyCanExecuteChanged();
        }

        private bool HasSameIdentities(IReadOnlyList<Segment> segments)
        {
            if (segments.Count != Segments.Count)
            {
                return false;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].Id != Segments[i].Segment.Id)
                {
                    return false;
                }
            }

            return true;
        }

        // Labels are not recorded for undo: every keystroke would otherwise become a separate step.
        private void RenameSegment(Guid id, string? label) => _cutList?.SetLabel(id, label);

        private void SelectSegmentById(Guid? id, bool seek)
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

            _isSelectingProgrammatically = !seek;
            try
            {
                SelectedSegment = match;
            }
            finally
            {
                _isSelectingProgrammatically = false;
            }
        }

        private void MoveSelectedEdge(SegmentEdge edge)
        {
            if (SelectedSegment is null)
            {
                return;
            }

            Guid id = SelectedSegment.Segment.Id;
            TimeSpan position = Position;
            TimeSpan minimum = FrameDuration();
            TryEdit(list => list.MoveEdge(id, edge, position, minimum));
        }

        private bool IsWholeVideoSelected() =>
            _cutList is { Count: 1 } list && list.Segments[0].Range == new TimeRange(TimeSpan.Zero, list.MediaDuration);

        private bool TryEdit(Action<CutList> edit)
        {
            if (_history is null)
            {
                return false;
            }

            try
            {
                _history.Execute(edit);
                return true;
            }
            catch (InvalidOperationException)
            {
                SetStatus(Strings.StatusSegmentOverlaps, isError: true);
                return false;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                SetStatus(ex.Message, isError: true);
                return false;
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
            TimeSpan target = backward ? Position - FrameDuration() : Position + FrameDuration();
            Position = target < TimeSpan.Zero ? TimeSpan.Zero : target > Duration ? Duration : target;
        }

        private TimeSpan FrameDuration() => TimeSpan.FromSeconds(1 / FrameRate());

        private double FrameRate() => Media?.PrimaryVideoStream?.FrameRate ?? 30;

        private TimeSpan SnapToFrame(TimeSpan position)
        {
            double frames = Math.Round(position.TotalSeconds * FrameRate());
            TimeSpan snapped = TimeSpan.FromSeconds(frames / FrameRate());
            return snapped > Duration ? Duration : snapped;
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

        private static string DescribeSelection(IReadOnlyList<Segment> segments, TimeSpan total)
        {
            if (segments.Count == 1)
            {
                TimeRange range = segments[0].Range;
                return Strings.Format(Strings.TrimSummary, Timecode.Format(range.Start), Timecode.Format(range.End), Timecode.Format(range.Duration));
            }

            return Strings.Format(Strings.TrimSummaryMultiple, segments.Count, Timecode.Format(total));
        }

        private static string DescribeExport(ExportPlan plan, ExportOptions options)
        {
            bool exact = options.CutMode == CutMode.FrameAccurate;
            return plan.OutputFiles.Count == 1
                ? Strings.Format(exact ? Strings.StatusExportedOneExact : Strings.StatusExportedOne, Path.GetFileName(plan.OutputFiles[0]))
                : Strings.Format(exact ? Strings.StatusExportedManyExact : Strings.StatusExportedMany, plan.OutputFiles.Count, options.OutputDirectory);
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
                return Strings.StatusReadyAudio;
            }

            double averageGop = duration.TotalSeconds / keyframes.Count;
            return Strings.Format(Strings.StatusReady, keyframes.Count, averageGop.ToString("0.##", CultureInfo.CurrentCulture));
        }

        private static string DescribeFailure(Exception exception)
        {
            // ffmpeg's own diagnostic is more precise than any message the application could compose, so its last line is shown.
            return exception is FFmpegException { StandardError: { Length: > 0 } standardError }
                ? standardError.Trim().Split('\n')[^1].Trim()
                : exception.Message;
        }

        [LoggerMessage(Level = LogLevel.Error, Message = "Could not open {FilePath}")]
        private partial void LogOpenFailed(string filePath, Exception exception);

        [LoggerMessage(Level = LogLevel.Error, Message = "Export failed")]
        private partial void LogExportFailed(Exception exception);

        [LoggerMessage(Level = LogLevel.Information, Message = "Opening the log folder {Folder}")]
        private partial void LogOpeningLogFolder(string folder);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not update the Open with registration")]
        private partial void LogShellIntegrationFailed(Exception exception);
    }
}
