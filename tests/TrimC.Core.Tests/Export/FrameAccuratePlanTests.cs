// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.IO;
using TrimC.Editing;
using TrimC.Media;
using Xunit;

namespace TrimC.Export.Tests
{
    public class FrameAccuratePlanTests
    {
        private const int PacketsPerGop = 60;

        private static readonly string s_outputDirectory = Path.Combine(Path.GetTempPath(), "trimc-tests");

        private static readonly MediaInfo s_recording = new()
        {
            FilePath = Path.Combine(s_outputDirectory, "Recording.mkv"),
            FormatName = "matroska,webm",
            Duration = TimeSpan.FromSeconds(10),
            Streams =
            [
                new MediaStreamInfo { Index = 0, Kind = StreamKind.Video, CodecName = "h264", Profile = "High", PixelFormat = "yuv420p", FrameRate = 30, IsDefault = true },
                new MediaStreamInfo { Index = 1, Kind = StreamKind.Audio, CodecName = "aac" },
                new MediaStreamInfo { Index = 2, Kind = StreamKind.Attachment, CodecName = "ttf" },
            ],
        };

        private static readonly KeyframeIndex s_keyframes = KeyframeIndex.Create(
        [
            new GroupOfPictures(Seconds(0), PacketsPerGop),
            new GroupOfPictures(Seconds(2), PacketsPerGop),
            new GroupOfPictures(Seconds(4), PacketsPerGop),
            new GroupOfPictures(Seconds(6), PacketsPerGop),
            new GroupOfPictures(Seconds(8), PacketsPerGop),
        ]);

        [Fact]
        public void CreatePlan_UnalignedSegment_EncodesEdgesCopiesWholeGopsAndKeepsAudioInOnePart()
        {
            ExportPlan plan = CreatePlan([Segment(3.5, 7.25)], ExportMode.SeparateFiles);

            Assert.Equal(5, plan.Steps.Count);

            EncodeRangeStep head = Assert.IsType<EncodeRangeStep>(plan.Steps[0]);
            Assert.Equal(Range(3.5, 4), head.Range);
            Assert.Equal(0, head.VideoStreamIndex);
            Assert.Equal("High", head.VideoProfile);
            Assert.Equal(Seconds(-3.5), head.TimestampOffset);
            Assert.Equal(ContainerFormat.MpegTransportStream, head.Container);

            CopyRangeStep body = Assert.IsType<CopyRangeStep>(plan.Steps[1]);
            Assert.Equal(Range(4, 6), body.Range);
            Assert.Equal([0], body.StreamIndexes);
            Assert.Equal(PacketsPerGop, body.VideoPacketLimit);
            Assert.Equal(Seconds(-3.5), body.TimestampOffset);

            EncodeRangeStep tail = Assert.IsType<EncodeRangeStep>(plan.Steps[2]);
            Assert.Equal(Range(6, 7.25), tail.Range);

            CopyRangeStep audio = Assert.IsType<CopyRangeStep>(plan.Steps[3]);
            Assert.Equal(Range(3.5, 7.25), audio.Range);
            Assert.Equal([1], audio.StreamIndexes);
            Assert.True(audio.StartsExactly);

            MuxPartsStep mux = Assert.IsType<MuxPartsStep>(plan.Steps[4]);
            Assert.Equal([head.OutputPath, body.OutputPath, tail.OutputPath], mux.VideoPartPaths);
            Assert.Equal([audio.OutputPath], mux.OtherPartPaths);
            Assert.Equal([mux.OutputPath], plan.OutputFiles);
            Assert.Equal(4, plan.TemporaryFiles.Count);
        }

        [Fact]
        public void CreatePlan_SegmentOnKeyframes_CopiesVideoWithoutEncoding()
        {
            ExportPlan plan = CreatePlan([Segment(2, 8)], ExportMode.SeparateFiles);

            CopyRangeStep body = Assert.IsType<CopyRangeStep>(plan.Steps[0]);
            Assert.Equal(Range(2, 8), body.Range);
            Assert.Equal(3 * PacketsPerGop, body.VideoPacketLimit);
            Assert.DoesNotContain(plan.Steps, step => step is EncodeRangeStep);
        }

        [Fact]
        public void CreatePlan_SegmentWithoutWholeGop_EncodesVideoEntirely()
        {
            ExportPlan plan = CreatePlan([Segment(4.5, 5.5)], ExportMode.SeparateFiles);

            Assert.Equal(Range(4.5, 5.5), Assert.IsType<EncodeRangeStep>(plan.Steps[0]).Range);
            Assert.IsType<CopyRangeStep>(plan.Steps[1]);
            Assert.IsType<MuxPartsStep>(plan.Steps[2]);
        }

        [Fact]
        public void CreatePlan_PositionWithinHalfFrameOfKeyframe_IsTreatedAsKeyframe()
        {
            ExportPlan plan = CreatePlan([Segment(4.01, 6)], ExportMode.SeparateFiles);

            Assert.IsType<CopyRangeStep>(plan.Steps[0]);
        }

        [Fact]
        public void CreatePlan_SeparateFiles_StartEverySegmentAtZero()
        {
            ExportPlan plan = CreatePlan([Segment(1, 3), Segment(5, 9)], ExportMode.SeparateFiles);

            Assert.Equal(2, plan.OutputFiles.Count);
            Assert.Equal(Seconds(-5), EncodeStepStartingAt(plan, 5).TimestampOffset);
        }

        [Fact]
        public void CreatePlan_Merge_PlacesSegmentsBackToBackAndJoinsOnce()
        {
            ExportPlan plan = CreatePlan([Segment(1, 3), Segment(5, 9)], ExportMode.Merge);

            // The second segment starts at 5 s in the source and directly after the first, 2 s long, in the output.
            Assert.Equal(Seconds(2 - 5), EncodeStepStartingAt(plan, 5).TimestampOffset);

            MuxPartsStep mux = Assert.IsType<MuxPartsStep>(plan.Steps[^1]);
            Assert.Equal(Seconds(6), mux.TotalDuration);
            Assert.Equal(2, mux.OtherPartPaths.Count);
            Assert.Equal(plan.Steps.Count - 1, plan.TemporaryFiles.Count);
            Assert.Single(plan.OutputFiles);
        }

        [Fact]
        public void CreatePlan_UnsupportedCodec_Throws()
        {
            MediaInfo vp9 = s_recording with
            {
                Streams = [new MediaStreamInfo { Index = 0, Kind = StreamKind.Video, CodecName = "vp9", IsDefault = true }],
            };

            Assert.Throws<ArgumentException>(() =>
                ExportPlanner.CreatePlan(vp9, s_keyframes, [Segment(3.5, 7.25)], Options(ExportMode.SeparateFiles), static _ => false));
        }

        private static EncodeRangeStep EncodeStepStartingAt(ExportPlan plan, double seconds)
        {
            foreach (ExportStep step in plan.Steps)
            {
                if (step is EncodeRangeStep encode && encode.Range.Start == Seconds(seconds))
                {
                    return encode;
                }
            }

            throw new InvalidOperationException($"No encode step starts at {seconds} s.");
        }

        private static ExportPlan CreatePlan(IReadOnlyList<Segment> segments, ExportMode mode) =>
            ExportPlanner.CreatePlan(s_recording, s_keyframes, segments, Options(mode), static _ => false);

        private static ExportOptions Options(ExportMode mode) => new()
        {
            OutputDirectory = s_outputDirectory,
            CutMode = CutMode.FrameAccurate,
            Mode = mode,
        };

        private static Segment Segment(double start, double end) => new() { Id = Guid.NewGuid(), Range = Range(start, end) };

        private static TimeRange Range(double start, double end) => new(Seconds(start), Seconds(end));

        private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
    }
}
