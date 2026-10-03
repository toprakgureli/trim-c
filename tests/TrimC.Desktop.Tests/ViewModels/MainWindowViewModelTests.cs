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
        private readonly SettingsStore _settings;
        private readonly MainWindowViewModel _viewModel;

        public MainWindowViewModelTests()
        {
            _settings = new SettingsStore(Path.Combine(_directory, "settings.json"));
            _settings.Save(new AppSettings { OpenFolderWhenDone = false });
            _viewModel = new MainWindowViewModel(
                _player,
                new MediaToolchain(new FakeProbe(), _executor),
                new FakeDialogs(),
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

            await _viewModel.ExportCommand.ExecuteAsync(null);

            ExportPlan plan = Assert.IsType<ExportPlan>(_executor.LastPlan);
            Assert.EndsWith(".mp4", plan.OutputFiles[0], StringComparison.Ordinal);
            Assert.Equal(ContainerFormat.Mp4, _settings.Current.Container);
            Assert.Equal(plan.OutputFiles[0], _viewModel.LastExportedFile);
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
            public Task<string?> PickMediaFileAsync() => Task.FromResult<string?>(null);

            public Task<string?> PickFolderAsync(string? initialDirectory) => Task.FromResult<string?>(null);
        }

        private sealed class FakePlayer : IVideoPlayer
        {
            public event EventHandler? StateChanged
            {
                add { }
                remove { }
            }

            public bool IsReady => false;

            public string? FailureReason => null;

            public bool IsFileLoaded => false;

            public TimeSpan Position => TimeSpan.Zero;

            public bool IsPaused => true;

            public void Attach(nint windowHandle)
            {
            }

            public void Open(string filePath)
            {
            }

            public void SetPaused(bool paused)
            {
            }

            public void Seek(TimeSpan position, bool exact)
            {
            }

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
