// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Threading;
using System.Threading.Tasks;

namespace TrimC.Media
{
    /// <summary>
    /// Reads structural information from media files.
    /// </summary>
    /// <remarks>
    /// The abstraction separates the domain from the tool that inspects files, which lets the UI and the
    /// planner be exercised against fabricated media in tests.
    /// </remarks>
    public interface IMediaProbe
    {
        /// <summary>
        /// Reads the container and stream information of a file.
        /// </summary>
        /// <param name="filePath">The path of the file to inspect.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The media description.</returns>
        Task<MediaInfo> ProbeAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the keyframe positions of a video stream.
        /// </summary>
        /// <param name="media">The probed media.</param>
        /// <param name="streamIndex">The absolute index of the video stream.</param>
        /// <param name="cancellationToken">A token that cancels the operation.</param>
        /// <returns>The keyframe index, with positions relative to <see cref="MediaInfo.StartTime"/>.</returns>
        Task<KeyframeIndex> ReadKeyframesAsync(MediaInfo media, int streamIndex, CancellationToken cancellationToken = default);
    }
}
