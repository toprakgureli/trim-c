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
    public class ExportPlannerTests
    {
        private static readonly string s_outputDirectory = Path.Combine(Path.GetTempPath(), "trimc-tests");

        private static readonly MediaInfo s_recording = new()
        {
            FilePath = Path.Combine(s_outputDirectory, "Recording.mkv"),
            FormatName = "matroska,webm",
            Duration = TimeSpan.FromMinutes(4),
            Streams =
            [
                new MediaStreamInfo { Index = 0, Kind = StreamKind.Video, CodecName = "h264", IsDefault = true },
                new MediaStreamInfo { Index = 1, Kind = StreamKind.Audio, CodecName = "aac" },
                new MediaStreamInfo { Index = 2, Kind = StreamKind.Subtitle, CodecName = "subrip" },
                new MediaStreamInfo { Index = 3, Kind = StreamKind.Attachment, CodecName = "ttf" },
            ],
        };

        private static readonly KeyframeIndex s_keyframes = KeyframeIndex.Create([Seconds(0), Seconds(2), Seconds(4), Seconds(6), Seconds(8)]);

        [Fact]
        public void CreatePlan_SeparateFiles_CreatesOneSnappedCopyPerSegment()
        {
            ExportPlan plan = CreatePlan([Segment(3, 5), Segment(6.5, 9)], Options());

            Assert.Equal(2, plan.Steps.Count);
            Assert.Empty(plan.TemporaryFiles);

            CopyRangeStep first = Assert.IsType<CopyRangeStep>(plan.Steps[0]);
            CopyRangeStep second = Assert.IsType<CopyRangeStep>(plan.Steps[1]);
            Assert.Equal(Range(2, 5), first.Range);
            Assert.Equal(Range(6, 9), second.Range);
            Assert.Equal([first.OutputPath, second.OutputPath], plan.OutputFiles);
        }

        [Theory]
        [InlineData(3.0, 5.0, 3.5)]
        [InlineData(4.0, 9.0, 5.5)]
        [InlineData(4.0, 4.4, 4.3)]
        public void CreatePlan_SeekPosition_LiesInsideGopOfAlignedStart(double start, double end, double expectedSeek)
        {
            ExportPlan plan = CreatePlan([Segment(start, end)], Options());

            Assert.Equal(Seconds(expectedSeek), Assert.IsType<CopyRangeStep>(plan.Steps[0]).SeekPosition);
        }

        [Fact]
        public void CreatePlan_Merge_CopiesTemporaryPartsThenConcatenates()
        {
            ExportPlan plan = CreatePlan([Segment(0, 2), Segment(4, 7)], Options() with { Mode = ExportMode.Merge });

            Assert.Equal(3, plan.Steps.Count);
            ConcatStep concat = Assert.IsType<ConcatStep>(plan.Steps[2]);
            Assert.Equal(plan.TemporaryFiles, concat.InputPaths);
            Assert.Equal([concat.OutputPath], plan.OutputFiles);
            Assert.Equal(Seconds(5), concat.TotalDuration);
            Assert.Equal(Seconds(10), plan.TotalWorkload);
        }

        [Fact]
        public void CreatePlan_MergeWithSingleSegment_SkipsConcat()
        {
            ExportPlan plan = CreatePlan([Segment(0, 2)], Options() with { Mode = ExportMode.Merge });

            Assert.IsType<CopyRangeStep>(Assert.Single(plan.Steps));
            Assert.Empty(plan.TemporaryFiles);
        }

        [Fact]
        public void CreatePlan_Matroska_IncludesEveryKnownStream()
        {
            ExportPlan plan = CreatePlan([Segment(0, 2)], Options());

            Assert.Equal([0, 1, 2, 3], Assert.IsType<CopyRangeStep>(plan.Steps[0]).StreamIndexes);
        }

        [Fact]
        public void CreatePlan_Mp4_ExcludesStreamsTheContainerCannotCarry()
        {
            ExportPlan plan = CreatePlan([Segment(0, 2)], Options() with { Container = ContainerFormat.Mp4 });

            CopyRangeStep step = Assert.IsType<CopyRangeStep>(plan.Steps[0]);
            Assert.Equal([0, 1], step.StreamIndexes);
            Assert.EndsWith(".mp4", step.OutputPath, StringComparison.Ordinal);
            Assert.True(step.FastStart);
        }

        [Fact]
        public void CreatePlan_ExplicitStreams_AreHonouredAndSorted()
        {
            ExportPlan plan = CreatePlan([Segment(0, 2)], Options() with { StreamIndexes = [1, 0] });

            Assert.Equal([0, 1], Assert.IsType<CopyRangeStep>(plan.Steps[0]).StreamIndexes);
        }

        [Fact]
        public void CreatePlan_UnknownStreamIndex_Throws()
        {
            Assert.Throws<ArgumentException>(() => CreatePlan([Segment(0, 2)], Options() with { StreamIndexes = [9] }));
        }

        [Fact]
        public void CreatePlan_SegmentCollapsingUnderNextSnap_IsSkipped()
        {
            ExportPlan plan = CreatePlan([Segment(2.5, 3.5), Segment(4, 6)], Options() with { SnapMode = KeyframeSnapMode.Next });

            Assert.Equal(Range(4, 6), Assert.IsType<CopyRangeStep>(Assert.Single(plan.Steps)).Range);
        }

        [Fact]
        public void CreatePlan_EverySegmentCollapses_Throws()
        {
            Assert.Throws<ArgumentException>(() => CreatePlan([Segment(2.5, 3.5)], Options() with { SnapMode = KeyframeSnapMode.Next }));
        }

        [Fact]
        public void CreatePlan_NoSegments_Throws()
        {
            Assert.Throws<ArgumentException>(() => CreatePlan([], Options()));
        }

        [Fact]
        public void CreatePlan_ExistingOutput_IsNotOverwritten()
        {
            string taken = Path.Combine(s_outputDirectory, "Recording-00.00.02.000-00.00.05.000.mkv");

            ExportPlan plan = ExportPlanner.CreatePlan(s_recording, s_keyframes, [Segment(3, 5)], Options(), path => path == taken);

            Assert.Equal(Path.Combine(s_outputDirectory, "Recording-00.00.02.000-00.00.05.000 (2).mkv"), plan.OutputFiles[0]);
        }

        private static ExportPlan CreatePlan(IReadOnlyList<Segment> segments, ExportOptions options) =>
            ExportPlanner.CreatePlan(s_recording, s_keyframes, segments, options, static _ => false);

        private static ExportOptions Options() => new() { OutputDirectory = s_outputDirectory };

        private static Segment Segment(double start, double end) => new() { Id = Guid.NewGuid(), Range = Range(start, end) };

        private static TimeRange Range(double start, double end) => new(Seconds(start), Seconds(end));

        private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
    }
}
