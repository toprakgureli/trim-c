// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Globalization;

namespace TrimC.Desktop.Formatting
{
    /// <summary>
    /// Formats timeline positions for display.
    /// </summary>
    internal static class Timecode
    {
        /// <summary>
        /// Formats a position as <c>m:ss.fff</c>, or <c>h:mm:ss.fff</c> when it is an hour or longer.
        /// </summary>
        /// <param name="value">The position to format.</param>
        /// <returns>The formatted position.</returns>
        public static string Format(TimeSpan value)
        {
            if (value < TimeSpan.Zero)
            {
                value = TimeSpan.Zero;
            }

            return value.TotalHours >= 1
                ? string.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalHours}:{value.Minutes:D2}:{value.Seconds:D2}.{value.Milliseconds:D3}")
                : string.Create(CultureInfo.InvariantCulture, $"{value.Minutes}:{value.Seconds:D2}.{value.Milliseconds:D3}");
        }

        /// <summary>
        /// Formats a position for a ruler label, omitting fractional seconds when <paramref name="interval"/> is whole seconds.
        /// </summary>
        /// <param name="value">The position to format.</param>
        /// <param name="interval">The spacing between ruler labels.</param>
        /// <returns>The formatted label.</returns>
        public static string FormatRulerLabel(TimeSpan value, TimeSpan interval)
        {
            bool wholeSeconds = interval.Ticks % TimeSpan.TicksPerSecond == 0;
            string seconds = wholeSeconds
                ? value.Seconds.ToString("D2", CultureInfo.InvariantCulture)
                : string.Create(CultureInfo.InvariantCulture, $"{value.Seconds:D2}.{value.Milliseconds / 100}");

            return value.TotalHours >= 1
                ? string.Create(CultureInfo.InvariantCulture, $"{(int)value.TotalHours}:{value.Minutes:D2}:{seconds}")
                : string.Create(CultureInfo.InvariantCulture, $"{value.Minutes}:{seconds}");
        }

        /// <summary>
        /// Formats a bit rate in bits per second using the largest fitting decimal unit, for example <c>73.2 Mbps</c>.
        /// </summary>
        /// <param name="bitsPerSecond">The bit rate.</param>
        /// <returns>The formatted bit rate.</returns>
        public static string FormatBitRate(long bitsPerSecond) => bitsPerSecond switch
        {
            >= 1_000_000 => string.Create(CultureInfo.InvariantCulture, $"{bitsPerSecond / 1_000_000.0:0.0} Mbps"),
            >= 1_000 => string.Create(CultureInfo.InvariantCulture, $"{bitsPerSecond / 1_000.0:0} kbps"),
            _ => string.Create(CultureInfo.InvariantCulture, $"{bitsPerSecond} bps"),
        };
    }
}
