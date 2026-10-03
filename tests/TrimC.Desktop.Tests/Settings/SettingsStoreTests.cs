// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.IO;
using TrimC.Export;
using Xunit;

namespace TrimC.Desktop.Settings.Tests
{
    public sealed class SettingsStoreTests : IDisposable
    {
        private readonly string _directory = Directory.CreateTempSubdirectory("trimc-settings-").FullName;

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        [Fact]
        public void Save_ThenLoad_RoundTripsEverySetting()
        {
            string path = Path.Combine(_directory, "settings.json");
            AppSettings settings = new()
            {
                Language = "tr",
                Container = ContainerFormat.Matroska,
                Mode = ExportMode.Merge,
                CutMode = CutMode.Keyframe,
                LastExportDirectory = @"D:\Exports",
                OpenFolderWhenDone = false,
                IsAdvancedPanelOpen = true,
                ShowInOpenWith = false,
            };

            Assert.True(new SettingsStore(path).Save(settings));

            Assert.Equal(settings, new SettingsStore(path).Current);
        }

        [Fact]
        public void Load_MissingOrCorruptFile_FallsBackToDefaults()
        {
            string path = Path.Combine(_directory, "settings.json");
            Assert.Equal(new AppSettings(), new SettingsStore(path).Current);

            File.WriteAllText(path, "{ not json");
            Assert.Equal(new AppSettings(), new SettingsStore(path).Current);
        }

        [Fact]
        public void Defaults_FavourExactFramesAndAPlainScreen()
        {
            AppSettings defaults = new();

            Assert.Equal(CutMode.FrameAccurate, defaults.CutMode);
            Assert.False(defaults.IsAdvancedPanelOpen);
            Assert.True(defaults.OpenFolderWhenDone);
            Assert.True(defaults.ShowInOpenWith);
        }
    }
}
