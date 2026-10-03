// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.IO;

namespace TrimC.FFmpeg
{
    /// <summary>
    /// The resolved locations of the FFmpeg command line tools.
    /// </summary>
    public sealed record FFmpegTools
    {
        /// <summary>
        /// Gets the full path of the <c>ffmpeg</c> executable.
        /// </summary>
        public required string FFmpegPath { get; init; }

        /// <summary>
        /// Gets the full path of the <c>ffprobe</c> executable.
        /// </summary>
        public required string FFprobePath { get; init; }

        /// <summary>
        /// Searches well-known locations for a matching pair of <c>ffmpeg</c> and <c>ffprobe</c> executables.
        /// </summary>
        /// <param name="preferredDirectory">A user-configured directory that is searched first, or <see langword="null"/>.</param>
        /// <returns>The located tools, or <see langword="null"/> if no directory contains both executables.</returns>
        /// <remarks>
        /// The search order is the preferred directory, the application directory, an <c>ffmpeg</c> folder next to
        /// the application, and finally every entry of the <c>PATH</c> environment variable. Both tools must be
        /// found in the same directory so that their versions are guaranteed to match.
        /// </remarks>
        public static FFmpegTools? Locate(string? preferredDirectory = null)
        {
            foreach (string directory in GetCandidateDirectories(preferredDirectory))
            {
                string ffmpeg = Path.Combine(directory, GetExecutableName("ffmpeg"));
                string ffprobe = Path.Combine(directory, GetExecutableName("ffprobe"));

                if (File.Exists(ffmpeg) && File.Exists(ffprobe))
                {
                    return new FFmpegTools { FFmpegPath = ffmpeg, FFprobePath = ffprobe };
                }
            }

            return null;
        }

        private static IEnumerable<string> GetCandidateDirectories(string? preferredDirectory)
        {
            if (!string.IsNullOrWhiteSpace(preferredDirectory))
            {
                yield return preferredDirectory;
            }

            yield return AppContext.BaseDirectory;
            yield return Path.Combine(AppContext.BaseDirectory, "ffmpeg");

            string? path = Environment.GetEnvironmentVariable("PATH");
            if (path is null)
            {
                yield break;
            }

            foreach (string entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                yield return entry;
            }
        }

        private static string GetExecutableName(string tool) => OperatingSystem.IsWindows() ? tool + ".exe" : tool;
    }
}
