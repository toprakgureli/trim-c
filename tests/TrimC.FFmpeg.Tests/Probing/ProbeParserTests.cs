// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using TrimC.Media;
using Xunit;

namespace TrimC.FFmpeg.Probing.Tests
{
    public class ProbeParserTests
    {
        // Trimmed output of ffprobe 7 for an OBS recording: H.264 video at 60 fps plus AAC audio in Matroska.
        private const string ObsRecordingJson = """
            {
                "streams": [
                    {
                        "index": 1,
                        "codec_name": "aac",
                        "codec_long_name": "AAC (Advanced Audio Coding)",
                        "codec_type": "audio",
                        "sample_rate": "48000",
                        "channels": 2,
                        "disposition": { "default": 1, "attached_pic": 0 },
                        "tags": { "title": "Track1" }
                    },
                    {
                        "index": 0,
                        "codec_name": "h264",
                        "codec_type": "video",
                        "width": 1920,
                        "height": 1080,
                        "r_frame_rate": "60/1",
                        "avg_frame_rate": "60/1",
                        "disposition": { "default": 1, "attached_pic": 0 },
                        "tags": { "LANGUAGE": "und" }
                    }
                ],
                "format": {
                    "format_name": "matroska,webm",
                    "start_time": "0.000000",
                    "duration": "232.016000",
                    "bit_rate": "73164512"
                }
            }
            """;

        [Fact]
        public void ParseMediaInfo_ObsRecording_MapsFormatAndStreams()
        {
            MediaInfo media = ProbeParser.ParseMediaInfo(ObsRecordingJson, "recording.mkv");

            Assert.Equal("recording.mkv", media.FilePath);
            Assert.Equal("matroska,webm", media.FormatName);
            Assert.Equal(TimeSpan.FromSeconds(232.016), media.Duration);
            Assert.Equal(73_164_512, media.BitRate);
            Assert.Equal(2, media.Streams.Count);

            MediaStreamInfo video = media.Streams[0];
            Assert.Equal(StreamKind.Video, video.Kind);
            Assert.Equal(1920, video.Width);
            Assert.Equal(60, video.FrameRate);
            Assert.Equal("und", video.Language);
            Assert.Same(video, media.PrimaryVideoStream);

            MediaStreamInfo audio = media.Streams[1];
            Assert.Equal(StreamKind.Audio, audio.Kind);
            Assert.Equal(48_000, audio.SampleRate);
            Assert.Equal("Track1", audio.Title);
            Assert.Null(audio.FrameRate);
        }

        [Fact]
        public void ParseMediaInfo_MissingDuration_Throws()
        {
            Assert.Throws<FFmpegException>(() => ProbeParser.ParseMediaInfo("""{ "format": { "format_name": "hls" }, "streams": [] }""", "live.m3u8"));
        }

        [Fact]
        public void ParseMediaInfo_InvalidJson_Throws()
        {
            Assert.Throws<FFmpegException>(() => ProbeParser.ParseMediaInfo("not json", "file.mkv"));
        }

        [Theory]
        [InlineData("30000/1001", 29.97002997)]
        [InlineData("25", 25.0)]
        public void ParseRational_ValidValue_ReturnsQuotient(string value, double expected)
        {
            Assert.Equal(expected, ProbeParser.ParseRational(value)!.Value, precision: 6);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0/0")]
        [InlineData("abc")]
        public void ParseRational_InvalidValue_ReturnsNull(string? value)
        {
            Assert.Null(ProbeParser.ParseRational(value));
        }

        [Fact]
        public void TryParseKeyframeLine_Keyframe_ReturnsPositionRelativeToStart()
        {
            Assert.True(ProbeParser.TryParseKeyframeLine("12.500000,K__", TimeSpan.FromSeconds(1.5), out TimeSpan position));
            Assert.Equal(TimeSpan.FromSeconds(11), position);
        }

        [Theory]
        [InlineData("12.500000,___")]
        [InlineData("N/A,K__")]
        [InlineData("")]
        [InlineData("12.5")]
        public void TryParseKeyframeLine_NonKeyframeOrMalformed_ReturnsFalse(string line)
        {
            Assert.False(ProbeParser.TryParseKeyframeLine(line, TimeSpan.Zero, out _));
        }
    }
}
