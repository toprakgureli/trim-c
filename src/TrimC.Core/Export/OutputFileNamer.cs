// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TrimC.Editing;

namespace TrimC.Export
{
    /// <summary>
    /// Produces collision-free, file-system-safe names for exported files.
    /// </summary>
    /// <remarks>
    /// Names encode the source file and the exported time range so that a directory of exports remains
    /// self-describing. Uniqueness is checked both against the file system and against names already handed
    /// out by the same instance, because files of a single plan do not exist on disk until it executes.
    /// </remarks>
    public sealed class OutputFileNamer
    {
        private const int MaxLabelLength = 64;

        private readonly string _directory;
        private readonly string _baseName;
        private readonly Func<string, bool> _fileExists;
        private readonly HashSet<string> _reserved = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputFileNamer"/> class.
        /// </summary>
        /// <param name="directory">The directory the files are created in.</param>
        /// <param name="sourcePath">The path of the source file, whose name prefixes every output.</param>
        /// <param name="fileExists">A predicate that reports whether a path is already taken on disk.</param>
        /// <exception cref="ArgumentNullException">Any argument is <see langword="null"/>.</exception>
        public OutputFileNamer(string directory, string sourcePath, Func<string, bool> fileExists)
            : this(directory, sourcePath, outputName: null, fileExists)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OutputFileNamer"/> class with a name chosen by the user.
        /// </summary>
        /// <param name="directory">The directory the files are created in.</param>
        /// <param name="sourcePath">The path of the source file.</param>
        /// <param name="outputName">
        /// The name chosen by the user, without extension, which prefixes every output instead of the source name; or
        /// <see langword="null"/> to use the source name.
        /// </param>
        /// <param name="fileExists">A predicate that reports whether a path is already taken on disk.</param>
        /// <exception cref="ArgumentNullException"><paramref name="directory"/>, <paramref name="sourcePath"/> or <paramref name="fileExists"/> is <see langword="null"/>.</exception>
        public OutputFileNamer(string directory, string sourcePath, string? outputName, Func<string, bool> fileExists)
        {
            ArgumentNullException.ThrowIfNull(directory);
            ArgumentNullException.ThrowIfNull(sourcePath);
            ArgumentNullException.ThrowIfNull(fileExists);

            _directory = directory;
            _baseName = string.IsNullOrWhiteSpace(outputName) ? Path.GetFileNameWithoutExtension(sourcePath) : outputName;
            _fileExists = fileExists;
        }

        /// <summary>
        /// Creates the path of a file whose name the user chose, without avoiding an existing file of that name.
        /// </summary>
        /// <param name="extension">The file extension, including the leading period.</param>
        /// <returns>The full path made of the directory, the chosen name and <paramref name="extension"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="extension"/> is <see langword="null"/>.</exception>
        public string ForChosenName(string extension)
        {
            ArgumentNullException.ThrowIfNull(extension);

            string path = Path.Combine(_directory, _baseName + extension);
            _reserved.Add(path);
            return path;
        }

        /// <summary>
        /// Creates the name of a file that holds a single segment.
        /// </summary>
        /// <param name="segment">The exported segment.</param>
        /// <param name="range">The keyframe-aligned range actually written, which may differ from the segment range.</param>
        /// <param name="extension">The file extension, including the leading period.</param>
        /// <returns>A full path that is not in use.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="segment"/> or <paramref name="extension"/> is <see langword="null"/>.</exception>
        public string ForSegment(Segment segment, TimeRange range, string extension)
        {
            ArgumentNullException.ThrowIfNull(segment);
            ArgumentNullException.ThrowIfNull(extension);

            string suffix = string.IsNullOrWhiteSpace(segment.Label)
                ? $"{FormatPosition(range.Start)}-{FormatPosition(range.End)}"
                : Sanitize(segment.Label);

            return Reserve($"{_baseName}-{suffix}", extension);
        }

        /// <summary>
        /// Creates the name of a file that holds every segment joined together.
        /// </summary>
        /// <param name="extension">The file extension, including the leading period.</param>
        /// <returns>A full path that is not in use.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="extension"/> is <see langword="null"/>.</exception>
        public string ForMerged(string extension)
        {
            ArgumentNullException.ThrowIfNull(extension);

            return Reserve($"{_baseName}-cut", extension);
        }

        /// <summary>
        /// Creates the name of an intermediate file that is deleted after the export.
        /// </summary>
        /// <param name="ordinal">The zero-based position of the part within the plan.</param>
        /// <param name="extension">The file extension, including the leading period.</param>
        /// <returns>A full path that is not in use.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="extension"/> is <see langword="null"/>.</exception>
        public string ForTemporaryPart(int ordinal, string extension)
        {
            ArgumentNullException.ThrowIfNull(extension);

            // The leading period hides the part on Unix-like systems and keeps it sorted away from real outputs. The name is
            // sanitized because frame-accurate parts are joined through ffmpeg's concat protocol, which splits on '|'.
            return Reserve(string.Create(CultureInfo.InvariantCulture, $".{Sanitize(_baseName)}.trimc-part{ordinal:D3}"), extension);
        }

        /// <summary>
        /// Formats a timeline position for use inside a file name, for example <c>00.01.23.456</c>.
        /// </summary>
        /// <param name="position">The position to format.</param>
        /// <returns>The formatted position, which contains only digits and periods.</returns>
        internal static string FormatPosition(TimeSpan position) =>
            string.Create(
                CultureInfo.InvariantCulture,
                $"{(int)position.TotalHours:D2}.{position.Minutes:D2}.{position.Seconds:D2}.{position.Milliseconds:D3}");

        /// <summary>
        /// Replaces characters that are invalid in file names on any supported platform.
        /// </summary>
        /// <param name="value">The raw text.</param>
        /// <returns>The sanitized text, truncated to a reasonable length.</returns>
        internal static string Sanitize(string value)
        {
            // Path.GetInvalidFileNameChars is platform dependent; Windows is the strictest platform, so its rules
            // are applied everywhere to keep exports portable between machines.
            const string InvalidCharacters = "<>:\"/\\|?*";

            StringBuilder builder = new(Math.Min(value.Length, MaxLabelLength));
            foreach (char c in value.Trim())
            {
                if (builder.Length == MaxLabelLength)
                {
                    break;
                }

                builder.Append(char.IsControl(c) || InvalidCharacters.Contains(c, StringComparison.Ordinal) ? '_' : c);
            }

            // Windows silently strips trailing periods and spaces, which could make two different labels collide.
            return builder.ToString().TrimEnd('.', ' ');
        }

        private string Reserve(string stem, string extension)
        {
            string candidate = Path.Combine(_directory, stem + extension);
            for (int attempt = 2; _reserved.Contains(candidate) || _fileExists(candidate); attempt++)
            {
                candidate = Path.Combine(_directory, string.Create(CultureInfo.InvariantCulture, $"{stem} ({attempt}){extension}"));
            }

            _reserved.Add(candidate);
            return candidate;
        }
    }
}
