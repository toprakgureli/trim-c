// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TrimC.Editing;
using TrimC.Export;
using TrimC.FFmpeg.Exporting;
using TrimC.FFmpeg.Probing;
using TrimC.Media;
using Xunit;

namespace TrimC.FFmpeg.Integration.Tests
{
    /// <summary>
    /// End-to-end tests that run the real ffmpeg and ffprobe binaries against a generated clip.
    /// </summary>
    /// <remarks>
    /// The tests are skipped when FFmpeg cannot be located, so the suite stays green on machines without it. Set the
    /// <c>TRIMC_FFMPEG_DIR</c> environment variable to point at a specific build. The source clip is synthesized with
    /// the lavfi test sources and a fixed two-second GOP, which makes every keyframe position known in advance. B-frames
    /// are enabled because they change how demuxers seek, and screen recorders commonly produce them.
    /// </remarks>
    public sealed class LosslessExportTests : IDisposable
    {
        private const int FrameRate = 30;
        private const int GopSeconds = 2;
        private const int ClipSeconds = 10;
        private const int BFrames = 2;

        private readonly FFmpegTools? _tools = FFmpegTools.Locate(Environment.GetEnvironmentVariable("TRIMC_FFMPEG_DIR"));
        private readonly string _directory = Directory.CreateTempSubdirectory("trimc-it-").FullName;

        public void Dispose() => Directory.Delete(_directory, recursive: true);

        [Fact]
        public async Task ReadKeyframesAsync_FixedGop_ReturnsEveryGopBoundary()
        {
            (FFprobeMediaProbe probe, MediaInfo media) = await PrepareAsync();

            KeyframeIndex keyframes = await probe.ReadKeyframesAsync(media, media.PrimaryVideoStream!.Index, TestContext.Current.CancellationToken);

            Assert.Equal([Seconds(0), Seconds(2), Seconds(4), Seconds(6), Seconds(8)], keyframes.Positions.ToArray());
        }

        [Fact]
        public async Task ExecuteAsync_SeparateFiles_CopiesSnappedRangeWithoutReencoding()
        {
            (FFprobeMediaProbe probe, MediaInfo media) = await PrepareAsync();
            KeyframeIndex keyframes = await probe.ReadKeyframesAsync(media, media.PrimaryVideoStream!.Index, TestContext.Current.CancellationToken);

            ExportPlan plan = ExportPlanner.CreatePlan(media, keyframes, [Segment(3, 7)], new ExportOptions { OutputDirectory = _directory, Container = ContainerFormat.Mp4 });
            await new FFmpegExportExecutor(_tools!).ExecuteAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

            MediaInfo output = await probe.ProbeAsync(plan.OutputFiles[0], TestContext.Current.CancellationToken);
            Assert.InRange(await CountVideoPacketsAsync(plan.OutputFiles[0]), 5 * FrameRate, (5 * FrameRate) + BFrames);
            Assert.Equal("h264", output.PrimaryVideoStream!.CodecName);

            // A stream copy is only playable from its first packet when that packet, in decode order, is a keyframe.
            Assert.StartsWith("K", await ReadFirstVideoPacketFlagsAsync(plan.OutputFiles[0]), StringComparison.Ordinal);
        }

        [Fact]
        public async Task ExecuteAsync_Merge_JoinsSegmentsAndRemovesTemporaryParts()
        {
            (FFprobeMediaProbe probe, MediaInfo media) = await PrepareAsync();
            KeyframeIndex keyframes = await probe.ReadKeyframesAsync(media, media.PrimaryVideoStream!.Index, TestContext.Current.CancellationToken);

            ExportOptions options = new() { OutputDirectory = _directory, Mode = ExportMode.Merge };
            ExportPlan plan = ExportPlanner.CreatePlan(media, keyframes, [Segment(0, 2), Segment(6, 9)], options);
            await new FFmpegExportExecutor(_tools!).ExecuteAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

            Assert.InRange(await CountVideoPacketsAsync(plan.OutputFiles[0]), 5 * FrameRate, (5 * FrameRate) + (2 * BFrames));
            Assert.All(plan.TemporaryFiles, path => Assert.False(File.Exists(path)));
        }

        [Fact]
        public async Task ExecuteAsync_FrameAccurate_ProducesExactlyTheSelectedFrames()
        {
            (FFprobeMediaProbe probe, MediaInfo media) = await PrepareAsync();
            KeyframeIndex keyframes = await probe.ReadKeyframesAsync(media, media.PrimaryVideoStream!.Index, TestContext.Current.CancellationToken);

            // 3.5 s and 7.2 s fall inside GOPs, so the plan encodes a head and a tail around a copied body.
            ExportOptions options = new() { OutputDirectory = _directory, CutMode = CutMode.FrameAccurate, Container = ContainerFormat.Mp4 };
            ExportPlan plan = ExportPlanner.CreatePlan(media, keyframes, [Segment(3.5, 7.2)], options);
            await new FFmpegExportExecutor(_tools!).ExecuteAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal((int)((7.2 - 3.5) * FrameRate), await CountVideoPacketsAsync(plan.OutputFiles[0]));
            Assert.StartsWith("K", await ReadFirstVideoPacketFlagsAsync(plan.OutputFiles[0]), StringComparison.Ordinal);
            await AssertContinuousAsync(plan.OutputFiles[0], includeAudio: true);
            Assert.All(plan.TemporaryFiles, path => Assert.False(File.Exists(path)));
        }

        [Fact]
        public async Task ExecuteAsync_FrameAccurateMerge_JoinsExactFrames()
        {
            (FFprobeMediaProbe probe, MediaInfo media) = await PrepareAsync();
            KeyframeIndex keyframes = await probe.ReadKeyframesAsync(media, media.PrimaryVideoStream!.Index, TestContext.Current.CancellationToken);

            ExportOptions options = new() { OutputDirectory = _directory, CutMode = CutMode.FrameAccurate, Mode = ExportMode.Merge };
            ExportPlan plan = ExportPlanner.CreatePlan(media, keyframes, [Segment(1.5, 3.2), Segment(5.1, 8.9)], options);
            await new FFmpegExportExecutor(_tools!).ExecuteAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

            Assert.Equal((int)Math.Round((1.7 + 3.8) * FrameRate), await CountVideoPacketsAsync(plan.OutputFiles[0]));
            await AssertContinuousAsync(plan.OutputFiles[0]);
        }

        [Fact]
        public async Task ExecuteAsync_Canceled_LeavesNoFilesBehind()
        {
            (FFprobeMediaProbe probe, MediaInfo media) = await PrepareAsync();
            KeyframeIndex keyframes = await probe.ReadKeyframesAsync(media, media.PrimaryVideoStream!.Index, TestContext.Current.CancellationToken);
            ExportPlan plan = ExportPlanner.CreatePlan(media, keyframes, [Segment(0, 4), Segment(4, 8)], new ExportOptions { OutputDirectory = _directory });

            using CancellationTokenSource cancellation = new();
            await cancellation.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FFmpegExportExecutor(_tools!).ExecuteAsync(plan, cancellationToken: cancellation.Token));
            Assert.All(plan.OutputFiles, path => Assert.False(File.Exists(path)));
        }

        private async Task<(FFprobeMediaProbe Probe, MediaInfo Media)> PrepareAsync()
        {
            Assert.SkipWhen(_tools is null, "FFmpeg was not found; set TRIMC_FFMPEG_DIR or add ffmpeg to PATH to run integration tests.");

            string source = Path.Combine(_directory, "source.mkv");
            using Process process = Process.Start(new ProcessStartInfo(_tools.FFmpegPath)
            {
                ArgumentList =
                {
                    "-hide_banner", "-loglevel", "error", "-y",
                    "-f", "lavfi", "-i", $"testsrc2=size=640x360:rate={FrameRate}:duration={ClipSeconds}",
                    "-f", "lavfi", "-i", $"sine=frequency=440:sample_rate=48000:duration={ClipSeconds}",
                    "-c:v", "libx264", "-preset", "veryfast", "-bf", BFrames.ToString(CultureInfo.InvariantCulture),
                    "-g", (FrameRate * GopSeconds).ToString(CultureInfo.InvariantCulture),
                    "-sc_threshold", "0",
                    "-c:a", "aac",
                    source,
                },
                RedirectStandardError = true,
            })!;

            string errors = await process.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.True(process.ExitCode == 0, errors);

            FFprobeMediaProbe probe = new(_tools);
            return (probe, await probe.ProbeAsync(source, TestContext.Current.CancellationToken));
        }

        // A frame-accurate export joins up to three video parts per segment. If a part carried extra or missing material,
        // the joined timeline would jump at that joint, so consecutive frames must never be further apart than one frame
        // and consecutive audio packets never further apart than one packet (an AAC packet is 1024 samples at 48 kHz).
        private async Task AssertContinuousAsync(string path, bool includeAudio = false)
        {
            AssertNoGaps(await ReadPacketTimesAsync(path, "v:0"), 1.5 / FrameRate);

            if (includeAudio)
            {
                AssertNoGaps(await ReadPacketTimesAsync(path, "a:0"), 1.5 * 1024 / 48_000);
            }
        }

        private static void AssertNoGaps(List<double> times, double maximumStep)
        {
            times.Sort();
            for (int i = 1; i < times.Count; i++)
            {
                Assert.True(times[i] - times[i - 1] < maximumStep, $"Gap between {times[i - 1]} s and {times[i]} s.");
            }
        }

        private async Task<List<double>> ReadPacketTimesAsync(string path, string stream)
        {
            using Process process = Process.Start(new ProcessStartInfo(_tools!.FFprobePath)
            {
                ArgumentList = { "-v", "error", "-select_streams", stream, "-show_entries", "packet=pts_time", "-of", "csv=p=0", path },
                RedirectStandardOutput = true,
            })!;

            string output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);

            List<double> times = [];
            foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                times.Add(double.Parse(line.TrimEnd(','), CultureInfo.InvariantCulture));
            }

            return times;
        }

        // The container duration includes the trailing audio frame, so accuracy is asserted on the video packet count.
        // Starting one GOP early would add GopSeconds * FrameRate packets; at the end, a stream copy keeps up to BFrames
        // reference packets past the cut because the B-frames before it cannot be decoded without them.
        private async Task<int> CountVideoPacketsAsync(string path)
        {
            using Process process = Process.Start(new ProcessStartInfo(_tools!.FFprobePath)
            {
                ArgumentList = { "-v", "error", "-select_streams", "v:0", "-count_packets", "-show_entries", "stream=nb_read_packets", "-of", "csv=p=0", path },
                RedirectStandardOutput = true,
            })!;

            string output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            return int.Parse(output.Trim(), CultureInfo.InvariantCulture);
        }

        private async Task<string> ReadFirstVideoPacketFlagsAsync(string path)
        {
            using Process process = Process.Start(new ProcessStartInfo(_tools!.FFprobePath)
            {
                ArgumentList = { "-v", "error", "-select_streams", "v:0", "-read_intervals", "%+#1", "-show_entries", "packet=flags", "-of", "csv=p=0", path },
                RedirectStandardOutput = true,
            })!;

            string output = await process.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            return output.Trim();
        }

        private static Segment Segment(double start, double end) => new() { Id = Guid.NewGuid(), Range = new TimeRange(Seconds(start), Seconds(end)) };

        private static TimeSpan Seconds(double value) => TimeSpan.FromSeconds(value);
    }
}
