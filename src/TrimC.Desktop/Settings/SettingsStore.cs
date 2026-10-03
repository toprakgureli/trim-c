// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.IO;
using System.Text.Json;

namespace TrimC.Desktop.Settings
{
    /// <summary>
    /// Loads and saves <see cref="AppSettings"/> as JSON in the user's local application data folder.
    /// </summary>
    /// <remarks>
    /// Settings are a convenience, so every failure degrades to the defaults instead of preventing the application from
    /// starting. Saving writes to a temporary file and replaces the original, so an interrupted save never leaves a
    /// truncated file behind.
    /// </remarks>
    internal sealed class SettingsStore
    {
        private readonly string _path;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsStore"/> class.
        /// </summary>
        /// <param name="path">The settings file, or <see langword="null"/> for the default location.</param>
        public SettingsStore(string? path = null)
        {
            _path = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "trim-c", "settings.json");
            Current = Load();
        }

        /// <summary>
        /// Gets the settings currently in effect.
        /// </summary>
        public AppSettings Current { get; private set; }

        /// <summary>
        /// Replaces the settings and writes them to disk.
        /// </summary>
        /// <param name="settings">The new settings.</param>
        /// <returns><see langword="true"/> if the settings were written; otherwise, <see langword="false"/>.</returns>
        public bool Save(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            Current = settings;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                string temporary = _path + ".tmp";
                File.WriteAllText(temporary, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
                File.Move(temporary, _path, overwrite: true);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }

        private AppSettings Load()
        {
            try
            {
                return File.Exists(_path)
                    ? JsonSerializer.Deserialize(File.ReadAllText(_path), SettingsJsonContext.Default.AppSettings) ?? new AppSettings()
                    : new AppSettings();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                return new AppSettings();
            }
        }
    }
}
