// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TrimC.FFmpeg.Processes;
using TrimC.Media;

namespace TrimC.FFmpeg.Probing
{
    /// <summary>
    /// An <see cref="IMediaProbe"/> implemented with the <c>ffprobe</c> command line tool.
    /// </summary>
    public sealed partial class FFprobeMediaProbe : IMediaProbe
    {
        private readonly FFmpegTools _tools;
        private readonly ILogger<FFprobeMediaProbe> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFprobeMediaProbe"/> class.
        /// </summary>
        /// <param name="tools">The located FFmpeg tools.</param>
        /// <param name="logger">The logger, or <see langword="null"/> to disable logging.</param>
        /// <exception cref="ArgumentNullException"><paramref name="tools"/> is <see langword="null"/>.</exception>
        public FFprobeMediaProbe(FFmpegTools tools, ILogger<FFprobeMediaProbe>? logger = null)
        {
            ArgumentNullException.ThrowIfNull(tools);

            _tools = tools;
            _logger = logger ?? NullLogger<FFprobeMediaProbe>.Instance;
        }

        /// <inheritdoc/>
        /// <exception cref="ArgumentNullException"><paramref name="filePath"/> is <see langword="null"/>.</exception>
        /// <exception cref="FFmpegException">ffprobe failed or reported unusable data.</exception>
        public async Task<MediaInfo> ProbeAsync(string filePath, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            string[] arguments =
            [
                "-v", "error",
                "-print_format", "json",
                "-show_format",
                "-show_streams",
                filePath,
            ];

            ProcessResult result = await ProcessRunner.RunAsync(_tools.FFprobePath, arguments, standardOutputLine: null, cancellationToken).ConfigureAwait(false);
            EnsureSuccess(result, filePath);

            MediaInfo media = ProbeParser.ParseMediaInfo(result.StandardOutput, filePath);
            LogProbed(filePath, media.FormatName, media.Duration, media.Streams.Count);
            return media;
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Keyframes are read from packet flags rather than by decoding frames. The demuxer already knows which
        /// packets are keyframes, so this takes seconds even for multi-gigabyte recordings. Packets arrive in decode
        /// order, so counting them between keyframes yields the exact packet count of every GOP, which frame-accurate
        /// exports use to end a copy precisely on a GOP boundary.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="media"/> is <see langword="null"/>.</exception>
        /// <exception cref="FFmpegException">ffprobe failed.</exception>
        public async Task<KeyframeIndex> ReadKeyframesAsync(MediaInfo media, int streamIndex, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(media);

            string[] arguments =
            [
                "-v", "error",
                "-select_streams", streamIndex.ToString(CultureInfo.InvariantCulture),
                "-show_entries", "packet=pts_time,flags",
                "-of", "csv=print_section=0",
                media.FilePath,
            ];

            List<GroupOfPictures> groups = [];
            TimeSpan startTime = media.StartTime;
            TimeSpan? currentStart = null;
            int currentCount = 0;

            // Output is consumed line by line because a long recording produces one line per packet. Packets before the
            // first keyframe cannot be decoded on their own and belong to no group.
            ProcessResult result = await ProcessRunner.RunAsync(
                _tools.FFprobePath,
                arguments,
                line =>
                {
                    if (!ProbeParser.TryParsePacketLine(line, startTime, out bool isKeyframe, out TimeSpan? position))
                    {
                        return;
                    }

                    if (isKeyframe && position is not null)
                    {
                        if (currentStart is not null)
                        {
                            groups.Add(new GroupOfPictures(currentStart.Value, currentCount));
                        }

                        currentStart = position;
                        currentCount = 0;
                    }

                    if (currentStart is not null)
                    {
                        currentCount++;
                    }
                },
                cancellationToken).ConfigureAwait(false);

            EnsureSuccess(result, media.FilePath);

            if (currentStart is not null)
            {
                groups.Add(new GroupOfPictures(currentStart.Value, currentCount));
            }

            KeyframeIndex index = KeyframeIndex.Create(groups);
            LogKeyframesRead(media.FilePath, streamIndex, index.Count);
            return index;
        }

        private static void EnsureSuccess(ProcessResult result, string filePath)
        {
            if (result.ExitCode != 0)
            {
                throw new FFmpegException($"ffprobe could not read '{filePath}'.", result.ExitCode, result.StandardError);
            }
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Probed {FilePath}: format {FormatName}, duration {Duration}, {StreamCount} streams")]
        private partial void LogProbed(string filePath, string formatName, TimeSpan duration, int streamCount);

        [LoggerMessage(Level = LogLevel.Information, Message = "Read {KeyframeCount} keyframes from stream {StreamIndex} of {FilePath}")]
        private partial void LogKeyframesRead(string filePath, int streamIndex, int keyframeCount);
    }
}
