// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Threading.Tasks;
using TrimC.Desktop.Services;
using TrimC.Desktop.Settings;
using TrimC.Editing;
using TrimC.Export;
using Xunit;

namespace TrimC.Desktop.ViewModels.Tests
{
    public class ExportDialogViewModelTests
    {
        private const string SourceDirectory = @"C:\Videos";

        [Fact]
        public void Constructor_StartsFromTheSavedSettings()
        {
            AppSettings settings = new() { Container = ContainerFormat.Mp4, CutMode = CutMode.Keyframe, SnapMode = KeyframeSnapMode.Next, OpenFolderWhenDone = false };

            ExportDialogViewModel dialog = Create(settings, segmentCount: 1);

            Assert.Equal(ContainerFormat.Mp4, dialog.SelectedContainer.Value);
            Assert.Equal(CutMode.Keyframe, dialog.SelectedCutMode.Value);
            Assert.Equal(KeyframeSnapMode.Next, dialog.SelectedSnapMode.Value);
            Assert.True(dialog.IsKeyframeMode);
            Assert.False(dialog.OpenFolderWhenDone);
            Assert.False(dialog.CanMerge);
        }

        [Fact]
        public void ApplyTo_ReturnsTheChoicesMadeInTheDialog()
        {
            ExportDialogViewModel dialog = Create(new AppSettings(), segmentCount: 2);
            dialog.SelectedMode = dialog.ModeOptions[1];
            dialog.SelectedCutMode = dialog.CutModeOptions[1];
            dialog.OpenFolderWhenDone = false;

            AppSettings result = dialog.ApplyTo(new AppSettings { Language = "tr" });

            Assert.Equal(ExportMode.Merge, result.Mode);
            Assert.Equal(CutMode.Keyframe, result.CutMode);
            Assert.False(result.OpenFolderWhenDone);
            Assert.Equal("tr", result.Language);
            Assert.True(dialog.CanMerge);
        }

        [Fact]
        public async Task ChooseFolder_AndUseSourceFolder_ControlTheOutputDirectory()
        {
            ExportDialogViewModel dialog = Create(new AppSettings(), segmentCount: 1, new FakeDialogs(@"D:\Exports"));
            Assert.Equal(SourceDirectory, dialog.EffectiveOutputDirectory);

            await dialog.ChooseFolderCommand.ExecuteAsync(null);
            Assert.Equal(@"D:\Exports", dialog.EffectiveOutputDirectory);
            Assert.True(dialog.HasCustomOutputDirectory);

            dialog.UseSourceFolderCommand.Execute(null);
            Assert.Equal(SourceDirectory, dialog.EffectiveOutputDirectory);
            Assert.Null(dialog.ApplyTo(new AppSettings()).OutputDirectory);
        }

        private static ExportDialogViewModel Create(AppSettings settings, int segmentCount, IFileDialogService? dialogs = null) =>
            new(settings, segmentCount, TimeSpan.FromSeconds(30), SourceDirectory, dialogs ?? new FakeDialogs(null));

        private sealed class FakeDialogs(string? folder) : IFileDialogService
        {
            public Task<string?> PickMediaFileAsync() => Task.FromResult<string?>(null);

            public Task<string?> PickFolderAsync(string? initialDirectory) => Task.FromResult(folder);
        }
    }
}
