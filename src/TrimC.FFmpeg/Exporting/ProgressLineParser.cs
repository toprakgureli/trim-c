// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Globalization;

namespace TrimC.FFmpeg.Exporting
{
    /// <summary>
    /// Interprets the key/value lines ffmpeg writes when started with <c>-progress pipe:1</c>.
    /// </summary>
    internal static class ProgressLineParser
    {
        private const string OutTimeKey = "out_time_us=";

        /// <summary>
        /// Extracts the output position from a progress line.
        /// </summary>
        /// <param name="line">A single line of progress output, for example <c>out_time_us=1500000</c>.</param>
        /// <param name="position">When this method returns <see langword="true"/>, the amount of media written so far.</param>
        /// <returns><see langword="true"/> if the line carries an output position; otherwise, <see langword="false"/>.</returns>
        public static bool TryParseOutTime(ReadOnlySpan<char> line, out TimeSpan position)
        {
            position = default;

            if (!line.StartsWith(OutTimeKey, StringComparison.Ordinal))
            {
                return false;
            }

            // ffmpeg reports N/A before the first packet is written and may report small negative values
            // while timestamps settle; neither represents forward progress.
            if (!long.TryParse(line[OutTimeKey.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out long microseconds) || microseconds < 0)
            {
                return false;
            }

            position = TimeSpan.FromMicroseconds(microseconds);
            return true;
        }
    }
}
