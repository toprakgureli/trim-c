// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.IO;
using TrimC.Media;

namespace TrimC.Export
{
    /// <summary>
    /// Container-specific knowledge shared by the planner and the executor.
    /// </summary>
    public static class ContainerFormats
    {
        /// <summary>
        /// Resolves <see cref="ContainerFormat.SameAsSource"/> into a concrete container based on the source file extension.
        /// </summary>
        /// <param name="format">The requested format.</param>
        /// <param name="sourcePath">The path of the source file.</param>
        /// <returns>A concrete container. Unknown source extensions resolve to <see cref="ContainerFormat.Matroska"/>, which can carry any codec.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="sourcePath"/> is <see langword="null"/>.</exception>
        public static ContainerFormat Resolve(ContainerFormat format, string sourcePath)
        {
            ArgumentNullException.ThrowIfNull(sourcePath);

            if (format != ContainerFormat.SameAsSource)
            {
                return format;
            }

            string extension = Path.GetExtension(sourcePath);
            if (extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase) ||
                extension.Equals(".m4v", StringComparison.OrdinalIgnoreCase))
            {
                return ContainerFormat.Mp4;
            }

            if (extension.Equals(".mov", StringComparison.OrdinalIgnoreCase))
            {
                return ContainerFormat.QuickTime;
            }

            return ContainerFormat.Matroska;
        }

        /// <summary>
        /// Finds the container that a file name extension stands for.
        /// </summary>
        /// <param name="path">A file name or path.</param>
        /// <param name="format">When this method returns <see langword="true"/>, the container of the extension.</param>
        /// <returns><see langword="true"/> if the extension belongs to a container that can be written; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
        public static bool TryGetFromExtension(string path, out ContainerFormat format)
        {
            ArgumentNullException.ThrowIfNull(path);

            string extension = Path.GetExtension(path);
            format = extension.ToUpperInvariant() switch
            {
                ".MP4" or ".M4V" => ContainerFormat.Mp4,
                ".MKV" => ContainerFormat.Matroska,
                ".MOV" => ContainerFormat.QuickTime,
                _ => ContainerFormat.SameAsSource,
            };

            return format != ContainerFormat.SameAsSource;
        }

        /// <summary>
        /// Gets the file extension, including the leading period, for a concrete container.
        /// </summary>
        /// <param name="format">A concrete container format.</param>
        /// <returns>The file extension.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="format"/> is <see cref="ContainerFormat.SameAsSource"/> or undefined.</exception>
        public static string GetExtension(ContainerFormat format) => format switch
        {
            ContainerFormat.Mp4 => ".mp4",
            ContainerFormat.Matroska => ".mkv",
            ContainerFormat.QuickTime => ".mov",
            ContainerFormat.MpegTransportStream => ".ts",
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        /// <summary>
        /// Determines whether the container can carry a stream of the specified kind without transcoding.
        /// </summary>
        /// <param name="format">A concrete container format.</param>
        /// <param name="stream">The stream to test.</param>
        /// <returns><see langword="true"/> if the stream is copied by default; otherwise, <see langword="false"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="stream"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// ISO base media containers reject text subtitles other than <c>mov_text</c>, arbitrary data streams and
        /// attachments; including them would make the muxer fail, so they are excluded unless explicitly requested.
        /// </remarks>
        public static bool SupportsByDefault(ContainerFormat format, MediaStreamInfo stream)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (format == ContainerFormat.Matroska)
            {
                return stream.Kind != StreamKind.Unknown;
            }

            return stream.Kind switch
            {
                StreamKind.Video or StreamKind.Audio => true,
                StreamKind.Subtitle => stream.CodecName == "mov_text",
                _ => false,
            };
        }

        /// <summary>
        /// Determines whether the container stores its index in a <c>moov</c> atom that can be relocated for fast start.
        /// </summary>
        /// <param name="format">A concrete container format.</param>
        /// <returns><see langword="true"/> for MP4 and QuickTime; otherwise, <see langword="false"/>.</returns>
        public static bool IsIsoBaseMedia(ContainerFormat format) =>
            format is ContainerFormat.Mp4 or ContainerFormat.QuickTime;
    }
}
