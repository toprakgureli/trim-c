// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using Microsoft.Extensions.Logging.Abstractions;
using TrimC.Desktop.Controls;
using TrimC.Desktop.Playback;
using TrimC.Desktop.Services;
using TrimC.Desktop.Settings;
using TrimC.Editing;
using TrimC.Export;
using TrimC.Media;
using Xunit;

namespace TrimC.Desktop.ViewModels.Tests
{
    public sealed class MainWindowViewModelTests : IDisposable
    {
        private const double FrameRate = 60;

        private static readonly MediaInfo s_media = new()
        {
            FilePath = Path.Combine(Path.GetTempPath(), "recording.mkv"),
            FormatName = "matroska,webm",
            Duration = TimeSpan.FromSeconds(90),
            Streams =
            [
                new MediaStreamInfo { Index = 0, Kind = StreamKind.Video, CodecName = "h264", FrameRate = FrameRate, IsDefault = true },
                new MediaStreamInfo { Index = 1, Kind = StreamKind.Audio, CodecName = "aac" },
            ],
        };

        private readonly string _directory = Directory.CreateTempSubdirectory("trimc-vm-").FullName;
        private readonly FakeShellIntegration _shell = new();
        private readonly FakeExportDialog _exportDialog = new();
        private readonly FakeExecutor _executor = new();
        private readonly FakePlayer _player = new();
        private readonly FakeDialogs _dialogs = new();
        private readonly SettingsStore _settings;
        private readonly MainWindowViewModel _viewModel;

        public MainWindowViewModelTests()
        {
            _settings = new SettingsStore(Path.Combine(_directory, "settings.json"));
            _settings.Save(new AppSettings { OpenFolderWhenDone = false });
            _viewModel = new MainWindowViewModel(
                _player,
                new MediaToolchain(new FakeProbe(), _executor),
                _dialogs,
                _exportDialog,
                _shell,
                _settings,
                new ImmediateDispatcher(),
                NullLogger<MainWindowViewModel>.Instance);
        }

        public void Dispose()
        {
            _viewModel.Dispose();
            _player.Dispose();
            Directory.Delete(_directory, recursive: true);
        }

        [Fact]
        public async Task OpenFile_SelectsTheWholeVideo()
        {
            await OpenAsync();

            Segment segment = Assert.Single(_viewModel.TimelineSegments);
            Assert.Equal(new TimeRange(TimeSpan.Zero, s_media.Duration), segment.Range);
            Assert.Equal(segment.Id, _viewModel.SelectedSegmentId);
            Assert.False(_viewModel.UndoCommand.CanExecute(null));
        }

        [Fact]
        public async Task Trim_SnapsToFramesAndUndoesAsOneStep()
        {
            await OpenAsync();
            Guid id = _viewModel.TimelineSegments[0].Id;

            Drag(id, SegmentEdge.Start, 10.004, 12.0, 15.4996);

            Assert.Equal(TimeSpan.FromSeconds(Math.Round(15.4996 * FrameRate) / FrameRate), _viewModel.TimelineSegments[0].Range.Start);
            Assert.True(_viewModel.UndoCommand.CanExecute(null));

            _viewModel.UndoCommand.Execute(null);
            Assert.Equal(TimeSpan.Zero, _viewModel.TimelineSegments[0].Range.Start);
            Assert.False(_viewModel.UndoCommand.CanExecute(null));

            _viewModel.RedoCommand.Execute(null);
            Assert.Equal(TimeSpan.FromSeconds(15.5), _viewModel.TimelineSegments[0].Range.Start);
        }

        [Fact]
        public async Task SetMarkInAndOut_ReplaceTheUntouchedWholeVideo()
        {
            await OpenAsync();

            SeekTo(10);
            _viewModel.SetMarkInCommand.Execute(null);
            SeekTo(20);
            _viewModel.SetMarkOutCommand.Execute(null);

            Segment segment = Assert.Single(_viewModel.TimelineSegments);
            Assert.Equal(new TimeRange(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20)), segment.Range);
        }

        [Fact]
        public async Task CutOut_RemovesTheFramesBetweenTheMarks()
        {
            await OpenAsync();

            SeekTo(30);
            _viewModel.SetMarkInCommand.Execute(null);
            SeekTo(40);
            _viewModel.CutOutCommand.Execute(null);

            Assert.Equal(2, _viewModel.TimelineSegments.Count);
            Assert.Equal(TimeSpan.FromSeconds(30), _viewModel.TimelineSegments[0].Range.End);
            Assert.Equal(TimeSpan.FromSeconds(40), _viewModel.TimelineSegments[1].Range.Start);

            _viewModel.UndoCommand.Execute(null);
            Assert.Single(_viewModel.TimelineSegments);
        }

        [Fact]
        public async Task Export_Canceled_RunsNothing()
        {
            await OpenAsync();
            _exportDialog.Confirm = false;

            await _viewModel.ExportCommand.ExecuteAsync(null);

            Assert.Null(_executor.LastPlan);
        }

        [Fact]
        public async Task Export_Confirmed_RunsThePlanAndRemembersTheChoices()
        {
            await OpenAsync();
            Drag(_viewModel.TimelineSegments[0].Id, SegmentEdge.End, 60);
            _exportDialog.Confirm = true;
            _exportDialog.Configure = dialog => dialog.SelectedContainer = dialog.ContainerOptions[1];
            _dialogs.SavePath = Path.Combine(_directory, "holiday.mp4");

            await _viewModel.ExportCommand.ExecuteAsync(null);

            ExportPlan plan = Assert.IsType<ExportPlan>(_executor.LastPlan);
            Assert.Equal([Path.Combine(_directory, "holiday.mp4")], plan.OutputFiles);
            Assert.Equal(ContainerFormat.Mp4, _settings.Current.Container);
            Assert.Equal(_directory, _settings.Current.LastExportDirectory);
            Assert.Equal(plan.OutputFiles[0], _viewModel.LastExportedFile);
        }

        [Fact]
        public async Task Export_SaveDialogDismissed_RunsNothing()
        {
            await OpenAsync();
            _exportDialog.Confirm = true;
            _dialogs.SavePath = null;

            await _viewModel.ExportCommand.ExecuteAsync(null);

            Assert.Null(_executor.LastPlan);
            Assert.NotNull(_dialogs.LastRequest);
        }

        [Fact]
        public async Task Export_SuggestsANameNextToTheSourceAndOffersTheContainerFirst()
        {
            await OpenAsync();
            Drag(_viewModel.TimelineSegments[0].Id, SegmentEdge.Start, 10);
            _exportDialog.Confirm = true;
            _exportDialog.Configure = dialog => dialog.SelectedContainer = dialog.ContainerOptions[3];
            _dialogs.SavePath = null;

            await _viewModel.ExportCommand.ExecuteAsync(null);

            SaveFileRequest request = Assert.IsType<SaveFileRequest>(_dialogs.LastRequest);
            Assert.Equal("recording-00.00.10.000-00.01.30.000.mov", request.SuggestedFileName);
            Assert.Equal(Path.GetDirectoryName(s_media.FilePath), request.InitialDirectory);
            Assert.Equal(".mov", request.FileTypes[0].Extension);
            Assert.Equal(3, request.FileTypes.Count);
        }

        [Fact]
        public async Task Export_ExtensionTypedInTheSaveDialog_ChoosesTheContainer()
        {
            await OpenAsync();
            _exportDialog.Confirm = true;
            _dialogs.SavePath = Path.Combine(_directory, "holiday.mkv");

            await _viewModel.ExportCommand.ExecuteAsync(null);

            ExportPlan plan = Assert.IsType<ExportPlan>(_executor.LastPlan);
            Assert.Equal(ContainerFormat.Matroska, plan.Steps[^1].Container);
            Assert.EndsWith("holiday.mkv", plan.OutputFiles[0], StringComparison.Ordinal);
        }

        [Fact]
        public async Task ToggleSound_MutesTheSelectedClipAndThePreviewAndCanBeUndone()
        {
            await OpenAsync();

            _viewModel.ToggleSoundCommand.Execute(null);

            Assert.True(_viewModel.TimelineSegments[0].IsMuted);
            Assert.True(_viewModel.IsSelectedMuted);
            Assert.True(_player.IsMuted);

            _viewModel.UndoCommand.Execute(null);

            Assert.False(_viewModel.TimelineSegments[0].IsMuted);
            Assert.False(_player.IsMuted);
        }

        [Fact]
        public async Task PreviewSound_FollowsThePlayheadAcrossMutedAndAudibleClips()
        {
            await OpenAsync();
            _viewModel.IsAdvancedPanelOpen = true;
            SeekTo(30);
            _viewModel.SplitCommand.Execute(null);
            _viewModel.ToggleSoundCommand.Execute(null);

            SeekTo(10);
            Assert.True(_player.IsMuted);

            SeekTo(40);
            Assert.False(_player.IsMuted);
        }

        [Fact]
        public void ShowInOpenWith_TogglesTheRegistration()
        {
            _viewModel.ShowInOpenWith = false;
            Assert.Equal(1, _shell.UnregisterCalls);
            Assert.False(_settings.Current.ShowInOpenWith);

            _viewModel.ShowInOpenWith = true;
            Assert.Equal(1, _shell.RegisterCalls);
        }

        [Fact]
        public void SetLanguage_IsStoredForTheNextStart()
        {
            _viewModel.SetLanguageCommand.Execute("tr");

            Assert.Equal("tr", _settings.Current.Language);
            Assert.True(_viewModel.IsLanguageTurkish);
            Assert.False(_viewModel.IsLanguageSystem);
        }

        [Fact]
        public async Task Seek_OutsideTheTrim_StaysOnTheKeptFrames()
        {
            await OpenAsync();
            Trim(10, 20);

            SeekTo(5);
            Assert.Equal(TimeSpan.FromSeconds(10), _viewModel.Position);

            SeekTo(50);
            Assert.Equal(LastFrameBefore(20), _viewModel.Position);
            Assert.Equal(LastFrameBefore(20), _player.LastSeek);

            SeekTo(15);
            Assert.Equal(TimeSpan.FromSeconds(15), _viewModel.Position);
        }

        [Fact]
        public async Task Seek_WithTheAdvancedPanelOpen_ReachesTheWholeFile()
        {
            await OpenAsync();
            Trim(10, 20);

            _viewModel.IsAdvancedPanelOpen = true;
            SeekTo(50);

            Assert.Equal(TimeSpan.FromSeconds(50), _viewModel.Position);
            Assert.Null(_player.StopPosition);
        }

        [Fact]
        public async Task ClosingTheAdvancedPanel_BringsThePlayheadBackIntoTheTrim()
        {
            await OpenAsync();
            Trim(10, 20);
            _viewModel.IsAdvancedPanelOpen = true;
            SeekTo(50);

            _viewModel.IsAdvancedPanelOpen = false;

            Assert.Equal(LastFrameBefore(20), _viewModel.Position);
            Assert.NotNull(_player.StopPosition);
        }

        [Fact]
        public async Task StopPosition_FollowsTheEndHandle()
        {
            await OpenAsync();
            Trim(10, 20);
            Assert.InRange(_player.StopPosition!.Value, LastFrameBefore(20) - FrameDuration(), LastFrameBefore(20));

            Drag(_viewModel.TimelineSegments[0].Id, SegmentEdge.End, 30);

            Assert.InRange(_player.StopPosition!.Value, LastFrameBefore(30) - FrameDuration(), LastFrameBefore(30));
        }

        [Fact]
        public async Task Undo_ThatShrinksTheTrim_MovesThePlayheadInside()
        {
            await OpenAsync();
            Trim(10, 20);
            Drag(_viewModel.TimelineSegments[0].Id, SegmentEdge.End, 30);
            SeekTo(25);

            _viewModel.UndoCommand.Execute(null);

            Assert.Equal(LastFrameBefore(20), _viewModel.Position);
        }

        [Fact]
        public async Task Play_AtTheEndOfTheTrim_StartsAgainFromTheStart()
        {
            await OpenAsync();
            Trim(10, 20);
            SeekTo(20);

            _viewModel.TogglePlaybackCommand.Execute(null);

            Assert.Equal(TimeSpan.FromSeconds(10), _viewModel.Position);
            Assert.False(_player.LastPausedRequest);
        }

        [Fact]
        public async Task Play_InsideTheTrim_ResumesWhereItIs()
        {
            await OpenAsync();
            Trim(10, 20);
            SeekTo(14);

            _viewModel.TogglePlaybackCommand.Execute(null);

            Assert.Equal(TimeSpan.FromSeconds(14), _viewModel.Position);
            Assert.False(_player.LastPausedRequest);
        }

        [Fact]
        public async Task ReachingTheEndOfASegment_ContinuesInTheNextOne()
        {
            await OpenAsync();
            SeekTo(30);
            _viewModel.SetMarkInCommand.Execute(null);
            SeekTo(40);
            _viewModel.CutOutCommand.Execute(null);
            SeekTo(29);

            _player.ReachStopPosition();

            Assert.Equal(TimeSpan.FromSeconds(40), _viewModel.Position);
            Assert.False(_player.LastPausedRequest);
        }

        [Fact]
        public async Task ReachingTheEndOfTheLastSegment_SettlesOnTheLastKeptFrame()
        {
            await OpenAsync();
            Trim(10, 20);
            SeekTo(19.5);

            _player.ReachStopPosition();

            Assert.Equal(LastFrameBefore(20), _viewModel.Position);
            Assert.Null(_player.LastPausedRequest);
        }

        [Fact]
        public async Task StepFrame_AtTheEndOfTheTrim_StaysPut()
        {
            await OpenAsync();
            Trim(10, 20);
            SeekTo(20);

            _viewModel.StepForwardCommand.Execute(null);

            Assert.Equal(LastFrameBefore(20), _viewModel.Position);
        }

        private static TimeSpan FrameDuration() => TimeSpan.FromSeconds(1 / FrameRate);

        private static TimeSpan LastFrameBefore(double seconds) => TimeSpan.FromSeconds(seconds) - FrameDuration();

        private void Trim(double start, double end)
        {
            Drag(_viewModel.TimelineSegments[0].Id, SegmentEdge.Start, start);
            Drag(_viewModel.TimelineSegments[0].Id, SegmentEdge.End, end);
        }

        private async Task OpenAsync() => await _viewModel.OpenFileCommand.ExecuteAsync(s_media.FilePath);

        private void Drag(Guid id, SegmentEdge edge, params double[] seconds)
        {
            _viewModel.TrimCommand.Execute(new TimelineTrimRequest(id, edge, TimeSpan.FromSeconds(seconds[0]), TrimPhase.Started));
            for (int i = 1; i < seconds.Length - 1; i++)
            {
                _viewModel.TrimCommand.Execute(new TimelineTrimRequest(id, edge, TimeSpan.FromSeconds(seconds[i]), TrimPhase.Moved));
            }

            _viewModel.TrimCommand.Execute(new TimelineTrimRequest(id, edge, TimeSpan.FromSeconds(seconds[^1]), TrimPhase.Completed));
        }

        private void SeekTo(double seconds) => _viewModel.SeekCommand.Execute(new TimelineSeekRequest(TimeSpan.FromSeconds(seconds), IsFinal: true));

        private sealed class FakeProbe : IMediaProbe
        {
            public Task<MediaInfo> ProbeAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(s_media);

            public Task<KeyframeIndex> ReadKeyframesAsync(MediaInfo media, int streamIndex, CancellationToken cancellationToken = default)
            {
                List<GroupOfPictures> groups = [];
                for (int second = 0; second < 90; second += 2)
                {
                    groups.Add(new GroupOfPictures(TimeSpan.FromSeconds(second), (int)(2 * FrameRate)));
                }

                return Task.FromResult(KeyframeIndex.Create(groups));
            }
        }

        private sealed class FakeExecutor : IExportExecutor
        {
            public ExportPlan? LastPlan { get; private set; }

            public Task ExecuteAsync(ExportPlan plan, IProgress<ExportProgress>? progress = null, CancellationToken cancellationToken = default)
            {
                LastPlan = plan;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeExportDialog : IExportDialogService
        {
            public bool Confirm { get; set; }

            public Action<ExportDialogViewModel>? Configure { get; set; }

            public Task<bool> ShowAsync(ExportDialogViewModel viewModel)
            {
                Configure?.Invoke(viewModel);
                return Task.FromResult(Confirm);
            }
        }

        private sealed class FakeShellIntegration : IShellIntegration
        {
            public int RegisterCalls { get; private set; }

            public int UnregisterCalls { get; private set; }

            public bool IsSupported => true;

            public void Register(string executablePath) => RegisterCalls++;

            public void Unregister() => UnregisterCalls++;
        }

        private sealed class FakeDialogs : IFileDialogService
        {
            public string? SavePath { get; set; }

            public SaveFileRequest? LastRequest { get; private set; }

            public Task<string?> PickMediaFileAsync() => Task.FromResult<string?>(null);

            public Task<string?> PickSaveFileAsync(SaveFileRequest request)
            {
                LastRequest = request;
                return Task.FromResult(SavePath);
            }
        }

        /// <summary>
        /// A player without a window: it records commands and reports a paused, unloaded state, so the view model's own
        /// playhead is what the tests observe.
        /// </summary>
        private sealed class FakePlayer : IVideoPlayer
        {
            public event EventHandler? StateChanged
            {
                add { }
                remove { }
            }

            public event EventHandler? StopPositionReached;

            public TimeSpan? StopPosition { get; set; }

            public TimeSpan? LastSeek { get; private set; }

            public bool? LastPausedRequest { get; private set; }

            public bool IsReady => false;

            public string? FailureReason => null;

            public bool IsFileLoaded => false;

            public TimeSpan Position => TimeSpan.Zero;

            public bool IsPaused => true;

            public bool IsMuted { get; set; }

            public void ReachStopPosition() => StopPositionReached?.Invoke(this, EventArgs.Empty);

            public void Attach(nint windowHandle)
            {
            }

            public void Open(string filePath)
            {
            }

            public void SetPaused(bool paused) => LastPausedRequest = paused;

            public void Seek(TimeSpan position, bool exact) => LastSeek = position;

            public void StepFrame(bool backward)
            {
            }

            public void Dispose()
            {
            }
        }

        private sealed class ImmediateDispatcher : IDispatcher
        {
            public bool CheckAccess() => true;

            public void VerifyAccess()
            {
            }

            public void Post(Action action, DispatcherPriority priority = default) => action();
        }
    }
}
