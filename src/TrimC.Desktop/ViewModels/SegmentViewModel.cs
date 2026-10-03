// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using TrimC.Desktop.Formatting;
using TrimC.Editing;

namespace TrimC.Desktop.ViewModels
{
    /// <summary>
    /// Presents a single <see cref="Segment"/> in the segment list.
    /// </summary>
    /// <remarks>
    /// The view model is a projection of the domain segment and is recreated whenever the <see cref="CutList"/> changes.
    /// Edits to <see cref="Label"/> are written back through a callback so that the cut list remains the single source of truth.
    /// </remarks>
    internal sealed class SegmentViewModel : ObservableObject
    {
        private readonly Action<Guid, string?> _renameSegment;
        private string? _label;

        /// <summary>
        /// Initializes a new instance of the <see cref="SegmentViewModel"/> class.
        /// </summary>
        /// <param name="segment">The segment to present.</param>
        /// <param name="number">The one-based position of the segment in the list.</param>
        /// <param name="renameSegment">Writes a new label back to the cut list.</param>
        /// <exception cref="ArgumentNullException"><paramref name="segment"/> or <paramref name="renameSegment"/> is <see langword="null"/>.</exception>
        public SegmentViewModel(Segment segment, int number, Action<Guid, string?> renameSegment)
        {
            ArgumentNullException.ThrowIfNull(segment);
            ArgumentNullException.ThrowIfNull(renameSegment);

            Segment = segment;
            Number = number;
            _renameSegment = renameSegment;
            _label = segment.Label;
        }

        /// <summary>
        /// Gets the presented segment.
        /// </summary>
        public Segment Segment { get; }

        /// <summary>
        /// Gets the one-based position of the segment in the list.
        /// </summary>
        public int Number { get; }

        /// <summary>
        /// Gets the formatted start position.
        /// </summary>
        public string StartText => Timecode.Format(Segment.Range.Start);

        /// <summary>
        /// Gets the formatted end position.
        /// </summary>
        public string EndText => Timecode.Format(Segment.Range.End);

        /// <summary>
        /// Gets the formatted duration.
        /// </summary>
        public string DurationText => Timecode.Format(Segment.Range.Duration);

        /// <summary>
        /// Gets a value indicating whether the segment is exported without sound.
        /// </summary>
        public bool IsMuted => Segment.IsMuted;

        /// <summary>
        /// Gets or sets the label used when naming the exported file.
        /// </summary>
        public string? Label
        {
            get => _label;
            set
            {
                string? normalized = string.IsNullOrWhiteSpace(value) ? null : value;
                if (SetProperty(ref _label, normalized))
                {
                    _renameSegment(Segment.Id, normalized);
                }
            }
        }
    }
}
