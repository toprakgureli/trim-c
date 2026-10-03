// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using TrimC.Editing;

namespace TrimC.Export
{
    /// <summary>
    /// Copies a time range of selected streams from the source into a new file without re-encoding.
    /// </summary>
    public sealed record CopyRangeStep : ExportStep
    {
        /// <summary>
        /// Gets the path of the source media file.
        /// </summary>
        public required string SourcePath { get; init; }

        /// <summary>
        /// Gets the keyframe-aligned range to copy.
        /// </summary>
        public required TimeRange Range { get; init; }

        /// <summary>
        /// Gets the position the demuxer seeks to before copying.
        /// </summary>
        /// <remarks>
        /// The position lies inside the GOP that begins at the start of <see cref="Range"/> rather than on the keyframe
        /// itself. Demuxers seek by decode timestamp, and ffmpeg moves the target back by a fraction of a second when the
        /// video has B-frames; seeking exactly to the keyframe would therefore land on the previous one and add a whole
        /// GOP to the output. Copying always begins at the keyframe at or before this position.
        /// </remarks>
        public required TimeSpan SeekPosition { get; init; }

        /// <summary>
        /// Gets the exact number of video packets to copy, or <see langword="null"/> to stop at the end of <see cref="Range"/>.
        /// </summary>
        /// <remarks>
        /// A copy that ends on a keyframe is limited by packet count rather than by time. With B-frames, the first
        /// packets of the following GOP precede the last frames of the current one in decode order, so a time limit
        /// would carry them into the output and duplicate frames that another part of the export already contains.
        /// </remarks>
        public int? VideoPacketLimit { get; init; }

        /// <summary>
        /// Gets the shift applied to source timestamps, or <see langword="null"/> to start the output at zero.
        /// </summary>
        /// <remarks>
        /// Parts of a frame-accurate export keep their source timestamps plus this offset, so that consecutive parts
        /// continue each other exactly and can be joined without any re-timing.
        /// </remarks>
        public TimeSpan? TimestampOffset { get; init; }

        /// <summary>
        /// Gets a value indicating whether packets that start before the range are dropped instead of being copied from
        /// the preceding keyframe. Used for audio, which has no keyframe dependency.
        /// </summary>
        public bool StartsExactly { get; init; }

        /// <summary>
        /// Gets the absolute indexes of the source streams to copy.
        /// </summary>
        public required IReadOnlyList<int> StreamIndexes { get; init; }

        /// <summary>
        /// Gets a value indicating whether the index is placed at the start of the file.
        /// </summary>
        public bool FastStart { get; init; }

        /// <inheritdoc/>
        public override TimeSpan Workload => Range.Duration;
    }
}
