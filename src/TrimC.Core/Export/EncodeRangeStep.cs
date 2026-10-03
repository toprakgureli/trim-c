// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using TrimC.Editing;

namespace TrimC.Export
{
    /// <summary>
    /// Re-encodes the video of a short time range so that it can start or end on a frame that is not a keyframe.
    /// </summary>
    /// <remarks>
    /// Frame-accurate exports use this step only for the partial GOPs at the cut points. The encoder is configured to
    /// match the source codec, profile and pixel format so that the encoded part and the copied parts can be joined
    /// into a single stream. Only video is produced; audio is exported separately in one continuous part per segment.
    /// </remarks>
    public sealed record EncodeRangeStep : ExportStep
    {
        /// <summary>
        /// Gets the path of the source media file.
        /// </summary>
        public required string SourcePath { get; init; }

        /// <summary>
        /// Gets the exact range to produce. Both ends may fall on any frame.
        /// </summary>
        public required TimeRange Range { get; init; }

        /// <summary>
        /// Gets the absolute index of the video stream to encode.
        /// </summary>
        public required int VideoStreamIndex { get; init; }

        /// <summary>
        /// Gets the shift applied to source timestamps, which places the part on the output timeline.
        /// </summary>
        public required TimeSpan TimestampOffset { get; init; }

        /// <summary>
        /// Gets the short codec name of the source video, for example <c>h264</c> or <c>hevc</c>, which selects the encoder.
        /// </summary>
        public required string VideoCodecName { get; init; }

        /// <summary>
        /// Gets the codec profile of the source video as reported by the demuxer, for example <c>High</c>, or <see langword="null"/>.
        /// </summary>
        public string? VideoProfile { get; init; }

        /// <summary>
        /// Gets the pixel format of the source video, for example <c>yuv420p</c>, or <see langword="null"/>.
        /// </summary>
        public string? PixelFormat { get; init; }

        /// <inheritdoc/>
        public override TimeSpan Workload => Range.Duration;
    }
}
