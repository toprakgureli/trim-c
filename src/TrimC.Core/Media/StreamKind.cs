// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Media
{
    /// <summary>
    /// Identifies the type of elementary stream carried inside a media container.
    /// </summary>
    public enum StreamKind
    {
        /// <summary>The stream type could not be determined.</summary>
        Unknown = 0,

        /// <summary>A video stream.</summary>
        Video,

        /// <summary>An audio stream.</summary>
        Audio,

        /// <summary>A subtitle stream.</summary>
        Subtitle,

        /// <summary>A timed data stream, such as timecode or telemetry.</summary>
        Data,

        /// <summary>An attachment, such as an embedded font in a Matroska file.</summary>
        Attachment,
    }
}
