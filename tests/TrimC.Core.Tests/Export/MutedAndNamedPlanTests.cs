// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TrimC.Editing;
using TrimC.Media;
using Xunit;

namespace TrimC.Export.Tests
{
    public class MutedAndNamedPlanTests
    {
        private static readonly string s_outputDirectory = Path.Combine(Path.GetTempPath(), "trimc-tests");

        private static readonly MediaInfo s_recording = new()
        {
            FilePath = Path.Combine(s_outputDirectory, "Recording.mkv"),
            FormatName = "matroska,webm",
            Duration = TimeSpan.FromMinutes(1),
            Streams =
            [
                new MediaStreamInfo { Index = 0, Kind = StreamKind.Video, CodecName = "h264", FrameRate = 30, IsDefault = true },
                new MediaStreamInfo { Index = 1, Kind = StreamKind.Audio, CodecName = "aac", SampleRate = 48_000, Channels = 2, ChannelLayout = "stereo" },
            ],
        };

        private static readonly KeyframeIndex s_keyframes = KeyframeIndex.Create([Seconds(0), Seconds(2), Seconds(4), Seconds(6), Seconds(8)]);

        [Theory]
        [InlineData(CutMode.Keyframe)]
        [InlineData(CutMode.FrameAccurate)]
        public void SeparateFiles_MutedSegment_LeavesItsAudioOut(CutMode cutMode)
        {
            ExportPlan plan = CreatePlan([Segment(2, 4, muted: true), Segment(4, 6)], Options() with { CutMode = cutMode });

            List<CopyRangeStep> copies = plan.Steps.OfType<CopyRangeStep>().ToList();
            Assert.All(copies, copy => Assert.Empty(copy.SilencedStreams));

            // The first file carries video only; the second still copies its sound.
            Assert.Equal(1, copies.Count(copy => copy.StreamIndexes.Contains(1)));
        }

        [Theory]
        [InlineData(CutMode.Keyframe)]
        [InlineData(CutMode.FrameAccurate)]
        public void Merge_MutedAndAudibleSegments_SilencesTheMutedOne(CutMode cutMode)
        {
            ExportOptions options = Options() with { CutMode = cutMode, Mode = ExportMode.Merge };

            ExportPlan plan = CreatePlan([Segment(2, 4), Segment(6, 8, muted: true)], options);

            CopyRangeStep silenced = Assert.Single(plan.Steps.OfType<CopyRangeStep>(), copy => copy.SilencedStreams.Count > 0);
            Assert.Equal(Range(6, 8), silenced.Range);
            Assert.Equal(1, Assert.Single(silenced.SilencedStreams).Index);
        }

        [Theory]
        [InlineData(CutMode.Keyframe)]
        [InlineData(CutMode.FrameAccurate)]
        public void Merge_EverySegmentMuted_LeavesTheAudioOut(CutMode cutMode)
        {
            ExportOptions options = Options() with { CutMode = cutMode, Mode = ExportMode.Merge };

            ExportPlan plan = CreatePlan([Segment(2, 4, muted: true), Segment(6, 8, muted: true)], options);

            Assert.All(plan.Steps.OfType<CopyRangeStep>(), copy => Assert.DoesNotContain(1, copy.StreamIndexes));
        }

        [Fact]
        public void Merge_MutedSegmentWithAnUnencodableCodec_Throws()
        {
            MediaInfo media = s_recording with
            {
                Streams = [s_recording.Streams[0], s_recording.Streams[1] with { CodecName = "dts" }],
            };
            ExportOptions options = Options() with { Mode = ExportMode.Merge };

            Assert.Throws<ArgumentException>(() =>
                ExportPlanner.CreatePlan(media, s_keyframes, [Segment(2, 4), Segment(6, 8, muted: true)], options, static _ => false));
        }

        [Theory]
        [InlineData(CutMode.Keyframe, ExportMode.SeparateFiles)]
        [InlineData(CutMode.Keyframe, ExportMode.Merge)]
        [InlineData(CutMode.FrameAccurate, ExportMode.Merge)]
        public void ChosenName_SingleOutput_IsUsedExactlyAndMayReplace(CutMode cutMode, ExportMode mode)
        {
            ExportOptions options = Options() with { OutputName = "holiday", CutMode = cutMode, Mode = mode };
            IReadOnlyList<Segment> segments = mode == ExportMode.Merge ? [Segment(2, 4), Segment(6, 8)] : [Segment(2, 4)];

            // Even when the file exists, it is the destination the user confirmed.
            string expected = Path.Combine(s_outputDirectory, "holiday.mkv");
            ExportPlan plan = ExportPlanner.CreatePlan(s_recording, s_keyframes, segments, options, path => path == expected);

            Assert.Equal([expected], plan.OutputFiles);
            Assert.Equal([expected], plan.ReplaceableFiles);
        }

        [Fact]
        public void ChosenName_SeveralFiles_PrefixesUniqueNames()
        {
            ExportOptions options = Options() with { OutputName = "holiday" };

            ExportPlan plan = CreatePlan([Segment(2, 4), Segment(6, 8)], options);

            Assert.Equal(
                [
                    Path.Combine(s_outputDirectory, "holiday-00.00.02.000-00.00.04.000.mkv"),
                    Path.Combine(s_outputDirectory, "holiday-00.00.06.000-00.00.08.000.mkv"),
                ],
                plan.OutputFiles);
            Assert.Empty(plan.ReplaceableFiles);
        }

        private static ExportPlan CreatePlan(IReadOnlyList<Segment> segments, ExportOptions options) =>
            ExportPlanner.CreatePlan(s_recording, s_keyframes, segments, options, static _ => false);

        private static ExportOptions Options() => new() { OutputDirectory = s_outputDirectory };

        private static Segment Segment(double start, double end, bool muted = false) =>
            new() { Id = Guid.NewGuid(), Range = Range(start, end), IsMuted = muted };

        private static TimeRange Range(double start, double end) => new(Seconds(start), Seconds(end));

        private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
    }
}
