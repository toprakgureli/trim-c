// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

namespace TrimC.Media
{
    /// <summary>
    /// Describes a single elementary stream inside a media file.
    /// </summary>
    /// <remarks>
    /// Only the properties that influence lossless editing decisions are modeled. Properties that do not
    /// apply to the stream's <see cref="Kind"/> are <see langword="null"/>; for example, an audio stream has
    /// no <see cref="Width"/>.
    /// </remarks>
    public sealed record MediaStreamInfo
    {
        /// <summary>
        /// Gets the zero-based index of the stream within its container.
        /// </summary>
        /// <remarks>This is the absolute index used by FFmpeg stream specifiers such as <c>0:3</c>.</remarks>
        public required int Index { get; init; }

        /// <summary>
        /// Gets the type of the stream.
        /// </summary>
        public required StreamKind Kind { get; init; }

        /// <summary>
        /// Gets the short codec name, for example <c>h264</c>, <c>hevc</c> or <c>aac</c>.
        /// </summary>
        public required string CodecName { get; init; }

        /// <summary>
        /// Gets the human readable codec description, when the container exposes one.
        /// </summary>
        public string? CodecLongName { get; init; }

        /// <summary>
        /// Gets the codec profile, for example <c>High</c> or <c>Main 10</c>, when the stream reports one.
        /// </summary>
        public string? Profile { get; init; }

        /// <summary>
        /// Gets the pixel format of a video stream, for example <c>yuv420p</c>.
        /// </summary>
        public string? PixelFormat { get; init; }

        /// <summary>
        /// Gets the ISO 639 language tag of the stream, when present.
        /// </summary>
        public string? Language { get; init; }

        /// <summary>
        /// Gets the stream title, when present.
        /// </summary>
        public string? Title { get; init; }

        /// <summary>
        /// Gets a value indicating whether the container marks this stream as the default of its kind.
        /// </summary>
        public bool IsDefault { get; init; }

        /// <summary>
        /// Gets the coded width of a video stream in pixels.
        /// </summary>
        public int? Width { get; init; }

        /// <summary>
        /// Gets the coded height of a video stream in pixels.
        /// </summary>
        public int? Height { get; init; }

        /// <summary>
        /// Gets the average frame rate of a video stream in frames per second.
        /// </summary>
        public double? FrameRate { get; init; }

        /// <summary>
        /// Gets the number of channels of an audio stream.
        /// </summary>
        public int? Channels { get; init; }

        /// <summary>
        /// Gets the sample rate of an audio stream in hertz.
        /// </summary>
        public int? SampleRate { get; init; }

        /// <summary>
        /// Gets the stream bit rate in bits per second, when the container reports it.
        /// </summary>
        public long? BitRate { get; init; }

        /// <summary>
        /// Gets a value indicating whether the stream is a still image attached as cover art rather than real video.
        /// </summary>
        public bool IsAttachedPicture { get; init; }
    }
}
