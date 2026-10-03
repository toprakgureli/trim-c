// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Collections.Generic;

namespace TrimC.Editing
{
    /// <summary>
    /// Records the states of a <see cref="CutList"/> so that edits can be undone and redone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The history stores snapshots of the segment list rather than inverse operations. Segments are immutable records,
    /// so a snapshot is a small array of references, and restoring one is always exact regardless of how complex the
    /// edit was. Every operation of <see cref="CutList"/> is therefore undoable without per-operation code.
    /// </para>
    /// <para>
    /// Continuous gestures, such as dragging a trim handle, call <see cref="Capture"/> when the gesture starts and
    /// <see cref="Commit"/> when it ends, which records the whole gesture as a single step.
    /// </para>
    /// </remarks>
    public sealed class EditHistory
    {
        private const int DefaultCapacity = 200;

        private readonly CutList _cutList;
        private readonly int _capacity;
        private readonly LinkedList<IReadOnlyList<Segment>> _undo = new();
        private readonly Stack<IReadOnlyList<Segment>> _redo = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="EditHistory"/> class.
        /// </summary>
        /// <param name="cutList">The cut list whose edits are recorded.</param>
        /// <param name="capacity">The maximum number of undo steps kept; the oldest steps are discarded first.</param>
        /// <exception cref="ArgumentNullException"><paramref name="cutList"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="capacity"/> is not positive.</exception>
        public EditHistory(CutList cutList, int capacity = DefaultCapacity)
        {
            ArgumentNullException.ThrowIfNull(cutList);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

            _cutList = cutList;
            _capacity = capacity;
        }

        /// <summary>
        /// Occurs when <see cref="CanUndo"/> or <see cref="CanRedo"/> may have changed.
        /// </summary>
        public event EventHandler? Changed;

        /// <summary>
        /// Gets a value indicating whether there is an edit to undo.
        /// </summary>
        public bool CanUndo => _undo.Count > 0;

        /// <summary>
        /// Gets a value indicating whether there is an undone edit to redo.
        /// </summary>
        public bool CanRedo => _redo.Count > 0;

        /// <summary>
        /// Applies an edit and records it as one undoable step.
        /// </summary>
        /// <param name="edit">The edit to apply to the cut list.</param>
        /// <exception cref="ArgumentNullException"><paramref name="edit"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// If the edit throws, the cut list keeps its previous state (its operations validate before they mutate) and
        /// nothing is recorded. An edit that leaves the segments unchanged is not recorded either.
        /// </remarks>
        public void Execute(Action<CutList> edit)
        {
            ArgumentNullException.ThrowIfNull(edit);

            IReadOnlyList<Segment> before = _cutList.Segments;
            edit(_cutList);
            Commit(before);
        }

        /// <summary>
        /// Takes a snapshot of the current state at the start of a continuous gesture.
        /// </summary>
        /// <returns>The snapshot to pass to <see cref="Commit"/> when the gesture ends.</returns>
        public IReadOnlyList<Segment> Capture() => _cutList.Segments;

        /// <summary>
        /// Records the change from <paramref name="before"/> to the current state as one undoable step.
        /// </summary>
        /// <param name="before">The state before the change, obtained from <see cref="Capture"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="before"/> is <see langword="null"/>.</exception>
        public void Commit(IReadOnlyList<Segment> before)
        {
            ArgumentNullException.ThrowIfNull(before);

            if (AreEqual(before, _cutList.Segments))
            {
                return;
            }

            _undo.AddLast(before);
            if (_undo.Count > _capacity)
            {
                _undo.RemoveFirst();
            }

            // A new edit forks the timeline of states, so the undone states can no longer be reached.
            _redo.Clear();
            OnChanged();
        }

        /// <summary>
        /// Restores the state before the most recent edit.
        /// </summary>
        /// <returns><see langword="true"/> if an edit was undone; otherwise, <see langword="false"/>.</returns>
        public bool Undo()
        {
            if (_undo.Last is not LinkedListNode<IReadOnlyList<Segment>> last)
            {
                return false;
            }

            _undo.RemoveLast();
            _redo.Push(_cutList.Segments);
            _cutList.Restore(last.Value);
            OnChanged();
            return true;
        }

        /// <summary>
        /// Reapplies the most recently undone edit.
        /// </summary>
        /// <returns><see langword="true"/> if an edit was redone; otherwise, <see langword="false"/>.</returns>
        public bool Redo()
        {
            if (!_redo.TryPop(out IReadOnlyList<Segment>? next))
            {
                return false;
            }

            _undo.AddLast(_cutList.Segments);
            _cutList.Restore(next);
            OnChanged();
            return true;
        }

        private static bool AreEqual(IReadOnlyList<Segment> a, IReadOnlyList<Segment> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }

        private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
    }
}
