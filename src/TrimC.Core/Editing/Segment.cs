// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;

namespace TrimC.Editing
{
    /// <summary>
    /// A user-defined region of the source media that will be exported.
    /// </summary>
    /// <remarks>
    /// Segments are immutable values. Edits produce a new instance that keeps the same <see cref="Id"/>,
    /// which lets the UI preserve selection and identity across changes without tracking object references.
    /// </remarks>
    public sealed record Segment
    {
        /// <summary>
        /// Gets the stable identifier of the segment.
        /// </summary>
        public required Guid Id { get; init; }

        /// <summary>
        /// Gets the region of the timeline covered by the segment.
        /// </summary>
        public required TimeRange Range { get; init; }

        /// <summary>
        /// Gets an optional user-provided label that is used when naming exported files.
        /// </summary>
        public string? Label { get; init; }

        /// <summary>
        /// Gets a value indicating whether the audio of the segment is left out of the export.
        /// </summary>
        /// <remarks>
        /// A muted segment exported on its own has no audio streams. When it is merged with segments that keep their
        /// sound, it carries silence instead, so that the joined file still has one continuous audio track.
        /// </remarks>
        public bool IsMuted { get; init; }
    }
}
