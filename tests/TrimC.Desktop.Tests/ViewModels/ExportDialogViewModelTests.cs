// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using TrimC.Desktop.Settings;
using TrimC.Editing;
using TrimC.Export;
using Xunit;

namespace TrimC.Desktop.ViewModels.Tests
{
    public class ExportDialogViewModelTests
    {
        [Fact]
        public void Constructor_StartsFromTheSavedSettings()
        {
            AppSettings settings = new() { Container = ContainerFormat.Mp4, CutMode = CutMode.Keyframe, SnapMode = KeyframeSnapMode.Next, OpenFolderWhenDone = false };

            ExportDialogViewModel dialog = new(settings, 1, TimeSpan.FromSeconds(30));

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
            ExportDialogViewModel dialog = new(new AppSettings(), 2, TimeSpan.FromSeconds(30));
            dialog.SelectedMode = dialog.ModeOptions[1];
            dialog.SelectedCutMode = dialog.CutModeOptions[1];
            dialog.OpenFolderWhenDone = false;

            AppSettings result = dialog.ApplyTo(new AppSettings { Language = "tr", LastExportDirectory = @"D:\Exports" });

            Assert.Equal(ExportMode.Merge, result.Mode);
            Assert.Equal(CutMode.Keyframe, result.CutMode);
            Assert.False(result.OpenFolderWhenDone);
            Assert.Equal("tr", result.Language);
            Assert.Equal(@"D:\Exports", result.LastExportDirectory);
            Assert.True(dialog.CanMerge);
        }
    }
}
