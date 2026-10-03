// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TrimC.FFmpeg.Probing
{
    // These types mirror the subset of the ffprobe JSON schema (-print_format json -show_format -show_streams)
    // that the application consumes. ffprobe serializes most numeric values as strings and omits keys that do
    // not apply to a stream, so every member is a nullable string or number and validation happens during mapping.

    /// <summary>
    /// The root object of ffprobe JSON output.
    /// </summary>
    internal sealed class ProbeDocument
    {
        public ProbeFormat? Format { get; set; }

        public List<ProbeStream>? Streams { get; set; }
    }

    /// <summary>
    /// The <c>format</c> section of ffprobe JSON output.
    /// </summary>
    internal sealed class ProbeFormat
    {
        public string? FormatName { get; set; }

        public string? Duration { get; set; }

        public string? StartTime { get; set; }

        public string? BitRate { get; set; }
    }

    /// <summary>
    /// An entry of the <c>streams</c> array of ffprobe JSON output.
    /// </summary>
    internal sealed class ProbeStream
    {
        public int Index { get; set; }

        public string? CodecType { get; set; }

        public string? CodecName { get; set; }

        public string? CodecLongName { get; set; }

        public string? Profile { get; set; }

        public string? PixFmt { get; set; }

        public int? Width { get; set; }

        public int? Height { get; set; }

        public string? AvgFrameRate { get; set; }

        public string? RFrameRate { get; set; }

        public int? Channels { get; set; }

        public string? SampleRate { get; set; }

        public string? BitRate { get; set; }

        public ProbeDisposition? Disposition { get; set; }

        public Dictionary<string, string>? Tags { get; set; }
    }

    /// <summary>
    /// The <c>disposition</c> flags of a stream, reported by ffprobe as 0 or 1.
    /// </summary>
    internal sealed class ProbeDisposition
    {
        public int Default { get; set; }

        public int AttachedPic { get; set; }
    }

    /// <summary>
    /// Source-generated serialization metadata, which avoids reflection and keeps the probe trimming and AOT safe.
    /// </summary>
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
    [JsonSerializable(typeof(ProbeDocument))]
    internal sealed partial class ProbeJsonContext : JsonSerializerContext
    {
    }
}
