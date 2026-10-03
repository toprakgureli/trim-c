// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;

namespace TrimC.Media
{
    /// <summary>
    /// Describes a media file and the streams it contains.
    /// </summary>
    public sealed record MediaInfo
    {
        /// <summary>
        /// Gets the full path of the media file.
        /// </summary>
        public required string FilePath { get; init; }

        /// <summary>
        /// Gets the container format names reported by the demuxer, for example <c>matroska,webm</c>.
        /// </summary>
        public required string FormatName { get; init; }

        /// <summary>
        /// Gets the total duration of the file.
        /// </summary>
        public required TimeSpan Duration { get; init; }

        /// <summary>
        /// Gets the timestamp of the first packet in the file.
        /// </summary>
        /// <remarks>
        /// Some containers, most notably MPEG-TS and files produced by screen recorders, do not start at zero.
        /// All positions exposed by the editor are relative to this value.
        /// </remarks>
        public TimeSpan StartTime { get; init; }

        /// <summary>
        /// Gets the overall bit rate of the file in bits per second, when known.
        /// </summary>
        public long? BitRate { get; init; }

        /// <summary>
        /// Gets the streams contained in the file, ordered by <see cref="MediaStreamInfo.Index"/>.
        /// </summary>
        public required IReadOnlyList<MediaStreamInfo> Streams { get; init; }

        /// <summary>
        /// Gets the stream that drives the timeline: the first default video stream, or the first video stream
        /// when none is marked default. Cover art is never selected.
        /// </summary>
        /// <value>The primary video stream, or <see langword="null"/> for audio-only files.</value>
        public MediaStreamInfo? PrimaryVideoStream
        {
            get
            {
                MediaStreamInfo? firstVideo = null;
                foreach (MediaStreamInfo stream in Streams)
                {
                    if (stream.Kind != StreamKind.Video || stream.IsAttachedPicture)
                    {
                        continue;
                    }

                    if (stream.IsDefault)
                    {
                        return stream;
                    }

                    firstVideo ??= stream;
                }

                return firstVideo;
            }
        }
    }
}
