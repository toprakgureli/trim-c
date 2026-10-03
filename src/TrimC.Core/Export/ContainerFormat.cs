// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Export
{
    /// <summary>
    /// Specifies the container that exported files are written to.
    /// </summary>
    /// <remarks>
    /// Changing the container only remuxes the existing packets; it never re-encodes. A container must still
    /// be able to carry the codecs being copied, which is why MP4 and QuickTime exclude data and attachment
    /// streams by default.
    /// </remarks>
    public enum ContainerFormat
    {
        /// <summary>Use the container of the source file.</summary>
        SameAsSource = 0,

        /// <summary>MPEG-4 Part 14 (<c>.mp4</c>), the most widely supported container.</summary>
        Mp4,

        /// <summary>Matroska (<c>.mkv</c>), which can carry practically any codec and stream type.</summary>
        Matroska,

        /// <summary>QuickTime (<c>.mov</c>).</summary>
        QuickTime,

        /// <summary>
        /// MPEG transport stream (<c>.ts</c>). Used for the intermediate parts of frame-accurate exports, because it
        /// repeats the codec parameter sets in front of every keyframe, which lets parts produced by different encoders
        /// be joined into one stream.
        /// </summary>
        MpegTransportStream,
    }
}
