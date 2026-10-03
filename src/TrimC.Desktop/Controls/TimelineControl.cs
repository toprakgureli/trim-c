// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using TrimC.Desktop.Formatting;
using TrimC.Editing;
using TrimC.Media;

namespace TrimC.Desktop.Controls
{
    /// <summary>
    /// A zoomable timeline that shows segments, keyframes and the playhead, and lets the user scrub through the media.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The control is drawn directly in <see cref="Render"/> rather than composed from child controls. A recording can
    /// contain tens of thousands of keyframes, and drawing them as primitives culled to the visible window keeps the
    /// cost proportional to the control's width rather than to the length of the media.
    /// </para>
    /// <para>
    /// The visible window is described by <c>_viewStart</c> and <c>_viewDuration</c>. The mouse wheel pans the window
    /// and Ctrl+wheel zooms around the pointer. All interaction is reported through <see cref="SeekCommand"/>, so
    /// the control holds no editing state of its own.
    /// </para>
    /// </remarks>
    internal sealed class TimelineControl : Control
    {
        /// <summary>Defines the <see cref="Duration"/> property.</summary>
        public static readonly StyledProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.Register<TimelineControl, TimeSpan>(nameof(Duration));

        /// <summary>Defines the <see cref="Position"/> property.</summary>
        public static readonly StyledProperty<TimeSpan> PositionProperty =
            AvaloniaProperty.Register<TimelineControl, TimeSpan>(nameof(Position));

        /// <summary>Defines the <see cref="Keyframes"/> property.</summary>
        public static readonly StyledProperty<KeyframeIndex?> KeyframesProperty =
            AvaloniaProperty.Register<TimelineControl, KeyframeIndex?>(nameof(Keyframes));

        /// <summary>Defines the <see cref="Segments"/> property.</summary>
        public static readonly StyledProperty<IReadOnlyList<Segment>?> SegmentsProperty =
            AvaloniaProperty.Register<TimelineControl, IReadOnlyList<Segment>?>(nameof(Segments));

        /// <summary>Defines the <see cref="SelectedSegmentId"/> property.</summary>
        public static readonly StyledProperty<Guid?> SelectedSegmentIdProperty =
            AvaloniaProperty.Register<TimelineControl, Guid?>(nameof(SelectedSegmentId));

        /// <summary>Defines the <see cref="MarkIn"/> property.</summary>
        public static readonly StyledProperty<TimeSpan?> MarkInProperty =
            AvaloniaProperty.Register<TimelineControl, TimeSpan?>(nameof(MarkIn));

        /// <summary>Defines the <see cref="SeekCommand"/> property.</summary>
        public static readonly StyledProperty<ICommand?> SeekCommandProperty =
            AvaloniaProperty.Register<TimelineControl, ICommand?>(nameof(SeekCommand));

        private const double RulerHeight = 20;
        private const double KeyframeTickHeight = 7;
        private const double MinimumLabelSpacing = 90;
        private const double ZoomStep = 1.25;

        // Ruler intervals in seconds, chosen so that labels land on values a person would pick when reading a clock.
        private static readonly double[] s_rulerIntervals = [0.1, 0.2, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600, 900, 1800, 3600];

        private static readonly IBrush s_trackBrush = new SolidColorBrush(Color.FromRgb(0x2B, 0x2D, 0x31));
        private static readonly IBrush s_rulerBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x1F, 0x22));
        private static readonly IBrush s_segmentBrush = new SolidColorBrush(Color.FromArgb(0x99, 0x3B, 0x82, 0xF6));
        private static readonly IBrush s_selectedSegmentBrush = new SolidColorBrush(Color.FromArgb(0xCC, 0x60, 0xA5, 0xFA));
        private static readonly IBrush s_textBrush = new SolidColorBrush(Color.FromRgb(0xB5, 0xBA, 0xC1));
        private static readonly IPen s_rulerTickPen = new Pen(new SolidColorBrush(Color.FromRgb(0x4E, 0x50, 0x58)), 1);
        private static readonly IPen s_keyframePen = new Pen(new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)), 1);
        private static readonly IPen s_selectedOutlinePen = new Pen(Brushes.White, 1.5);
        private static readonly IPen s_markInPen = new Pen(new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E)), 2);
        private static readonly IPen s_playheadPen = new Pen(new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44)), 2);
        private static readonly Typeface s_labelTypeface = new(FontFamily.Default);

        private TimeSpan _viewStart;
        private TimeSpan _viewDuration;
        private bool _isScrubbing;

        static TimelineControl()
        {
            AffectsRender<TimelineControl>(
                DurationProperty,
                PositionProperty,
                KeyframesProperty,
                SegmentsProperty,
                SelectedSegmentIdProperty,
                MarkInProperty);

            FocusableProperty.OverrideDefaultValue<TimelineControl>(true);
            ClipToBoundsProperty.OverrideDefaultValue<TimelineControl>(true);
        }

        /// <summary>Gets or sets the total length of the media.</summary>
        public TimeSpan Duration
        {
            get => GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        /// <summary>Gets or sets the playhead position.</summary>
        public TimeSpan Position
        {
            get => GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        /// <summary>Gets or sets the keyframes to draw as ticks under the segment track.</summary>
        public KeyframeIndex? Keyframes
        {
            get => GetValue(KeyframesProperty);
            set => SetValue(KeyframesProperty, value);
        }

        /// <summary>Gets or sets the segments to draw.</summary>
        public IReadOnlyList<Segment>? Segments
        {
            get => GetValue(SegmentsProperty);
            set => SetValue(SegmentsProperty, value);
        }

        /// <summary>Gets or sets the identifier of the segment drawn as selected.</summary>
        public Guid? SelectedSegmentId
        {
            get => GetValue(SelectedSegmentIdProperty);
            set => SetValue(SelectedSegmentIdProperty, value);
        }

        /// <summary>Gets or sets the pending in-point of a segment that has not been closed yet.</summary>
        public TimeSpan? MarkIn
        {
            get => GetValue(MarkInProperty);
            set => SetValue(MarkInProperty, value);
        }

        /// <summary>Gets or sets the command executed with a <see cref="TimelineSeekRequest"/> when the user scrubs.</summary>
        public ICommand? SeekCommand
        {
            get => GetValue(SeekCommandProperty);
            set => SetValue(SeekCommandProperty, value);
        }

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            Rect bounds = new(Bounds.Size);
            context.FillRectangle(s_trackBrush, bounds);
            context.FillRectangle(s_rulerBrush, new Rect(0, 0, bounds.Width, RulerHeight));

            if (Duration <= TimeSpan.Zero || bounds.Width <= 0)
            {
                return;
            }

            EnsureView();

            Rect track = new(0, RulerHeight, bounds.Width, bounds.Height - RulerHeight);
            DrawRuler(context, bounds.Width);
            DrawSegments(context, track);
            DrawKeyframes(context, track);

            if (MarkIn is TimeSpan markIn)
            {
                double x = ToX(markIn);
                context.DrawLine(s_markInPen, new Point(x, RulerHeight), new Point(x, bounds.Height));
            }

            double playheadX = ToX(Position);
            context.DrawLine(s_playheadPen, new Point(playheadX, 0), new Point(playheadX, bounds.Height));
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DurationProperty)
            {
                // A new file resets the view to show the whole media.
                _viewStart = TimeSpan.Zero;
                _viewDuration = Duration;
            }
            else if (change.Property == PositionProperty && !_isScrubbing)
            {
                KeepPlayheadVisible();
            }
        }

        /// <inheritdoc/>
        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || Duration <= TimeSpan.Zero)
            {
                return;
            }

            _isScrubbing = true;
            e.Pointer.Capture(this);
            Focus();
            RequestSeek(e.GetPosition(this).X, isFinal: false);
            e.Handled = true;
        }

        /// <inheritdoc/>
        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            if (_isScrubbing)
            {
                RequestSeek(e.GetPosition(this).X, isFinal: false);
                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);

            if (_isScrubbing)
            {
                _isScrubbing = false;
                e.Pointer.Capture(null);
                RequestSeek(e.GetPosition(this).X, isFinal: true);
                e.Handled = true;
            }
        }

        /// <inheritdoc/>
        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
        {
            base.OnPointerCaptureLost(e);
            _isScrubbing = false;
        }

        /// <inheritdoc/>
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
        {
            base.OnPointerWheelChanged(e);

            if (Duration <= TimeSpan.Zero || Bounds.Width <= 0)
            {
                return;
            }

            EnsureView();

            if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                // Zoom around the pointer so that the content under the cursor stays in place.
                double pointerX = e.GetPosition(this).X;
                TimeSpan anchor = ToTime(pointerX);
                double factor = e.Delta.Y > 0 ? 1 / ZoomStep : ZoomStep;

                TimeSpan minimum = TimeSpan.FromSeconds(0.5);
                _viewDuration = Clamp(_viewDuration * factor, minimum, Duration);
                _viewStart = anchor - (_viewDuration * (pointerX / Bounds.Width));
            }
            else
            {
                // Each wheel notch pans a tenth of the visible window, matching the feel of horizontal scrolling elsewhere.
                double delta = e.Delta.Y != 0 ? -e.Delta.Y : e.Delta.X;
                _viewStart += _viewDuration * (delta / 10);
            }

            ClampView();
            InvalidateVisual();
            e.Handled = true;
        }

        private void DrawRuler(DrawingContext context, double width)
        {
            double pixelsPerSecond = width / _viewDuration.TotalSeconds;
            double intervalSeconds = s_rulerIntervals[^1];
            foreach (double candidate in s_rulerIntervals)
            {
                if (candidate * pixelsPerSecond >= MinimumLabelSpacing)
                {
                    intervalSeconds = candidate;
                    break;
                }
            }

            TimeSpan interval = TimeSpan.FromSeconds(intervalSeconds);
            long firstIndex = (long)Math.Floor(_viewStart.TotalSeconds / intervalSeconds);
            TimeSpan viewEnd = _viewStart + _viewDuration;

            for (long i = firstIndex; ; i++)
            {
                TimeSpan time = interval * i;
                if (time > viewEnd)
                {
                    break;
                }

                double x = Math.Round(ToX(time)) + 0.5;
                context.DrawLine(s_rulerTickPen, new Point(x, RulerHeight - 6), new Point(x, RulerHeight));

                FormattedText label = new(
                    Timecode.FormatRulerLabel(time, interval),
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    s_labelTypeface,
                    10,
                    s_textBrush);
                context.DrawText(label, new Point(x + 3, 3));
            }
        }

        private void DrawSegments(DrawingContext context, Rect track)
        {
            IReadOnlyList<Segment>? segments = Segments;
            if (segments is null)
            {
                return;
            }

            double top = track.Top + 4;
            double height = track.Height - KeyframeTickHeight - 8;

            foreach (Segment segment in segments)
            {
                double left = ToX(segment.Range.Start);
                double right = ToX(segment.Range.End);
                if (right < 0 || left > track.Right)
                {
                    continue;
                }

                // Very short segments at low zoom still get a visible sliver so they can be found and selected.
                Rect rect = new(left, top, Math.Max(right - left, 2), height);
                bool isSelected = segment.Id == SelectedSegmentId;
                context.DrawRectangle(isSelected ? s_selectedSegmentBrush : s_segmentBrush, isSelected ? s_selectedOutlinePen : null, rect, 3, 3);
            }
        }

        private void DrawKeyframes(DrawingContext context, Rect track)
        {
            KeyframeIndex? keyframes = Keyframes;
            if (keyframes is null || keyframes.Count == 0)
            {
                return;
            }

            double bottom = track.Bottom;
            double top = bottom - KeyframeTickHeight;
            double lastX = double.NegativeInfinity;
            TimeSpan viewEnd = _viewStart + _viewDuration;

            // Start from the first visible keyframe and stop at the edge of the view; ticks closer than two pixels to
            // the previous one are skipped because they would render as a solid bar and convey nothing.
            ReadOnlySpan<TimeSpan> positions = keyframes.Positions;
            int firstIndex = positions.BinarySearch(_viewStart);
            if (firstIndex < 0)
            {
                firstIndex = Math.Max(~firstIndex - 1, 0);
            }

            foreach (TimeSpan position in positions[firstIndex..])
            {
                if (position > viewEnd)
                {
                    break;
                }

                double x = Math.Round(ToX(position)) + 0.5;
                if (x - lastX < 2)
                {
                    continue;
                }

                context.DrawLine(s_keyframePen, new Point(x, top), new Point(x, bottom));
                lastX = x;
            }
        }

        private void RequestSeek(double x, bool isFinal)
        {
            TimeSpan position = Clamp(ToTime(x), TimeSpan.Zero, Duration);
            TimelineSeekRequest request = new(position, isFinal);

            ICommand? command = SeekCommand;
            if (command?.CanExecute(request) == true)
            {
                command.Execute(request);
            }
        }

        private void KeepPlayheadVisible()
        {
            if (_viewDuration <= TimeSpan.Zero || _viewDuration >= Duration)
            {
                return;
            }

            // During playback the view pages forward rather than scrolling continuously, which keeps labels readable.
            TimeSpan position = Position;
            if (position < _viewStart || position > _viewStart + _viewDuration)
            {
                _viewStart = position - (_viewDuration / 10);
                ClampView();
            }
        }

        private void EnsureView()
        {
            if (_viewDuration <= TimeSpan.Zero || _viewDuration > Duration)
            {
                _viewStart = TimeSpan.Zero;
                _viewDuration = Duration;
            }
        }

        private void ClampView()
        {
            _viewStart = Clamp(_viewStart, TimeSpan.Zero, Duration - _viewDuration);
        }

        private double ToX(TimeSpan time) => (time - _viewStart).TotalSeconds / _viewDuration.TotalSeconds * Bounds.Width;

        private TimeSpan ToTime(double x) => _viewStart + (_viewDuration * (x / Bounds.Width));

        private static TimeSpan Clamp(TimeSpan value, TimeSpan minimum, TimeSpan maximum) =>
            value < minimum ? minimum : value > maximum ? maximum : value;
    }
}
