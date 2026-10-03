// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace TrimC.Desktop.Formatting
{
    /// <summary>
    /// Converts a <see cref="TimeSpan"/> into its timecode representation for one-way bindings.
    /// </summary>
    internal sealed class TimecodeConverter : IValueConverter
    {
        /// <summary>
        /// Gets the shared instance, referenced from XAML through <c>x:Static</c>.
        /// </summary>
        public static TimecodeConverter Instance { get; } = new();

        /// <inheritdoc/>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is TimeSpan time ? Timecode.Format(time) : BindingOperations.DoNothing;

        /// <inheritdoc/>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
