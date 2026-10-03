// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;

namespace TrimC.Desktop.Playback
{
    /// <summary>
    /// Plays the source media for preview while segments are being edited.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The player renders into a native window supplied by the view through <see cref="Attach"/>. Until a window is
    /// attached, <see cref="IsReady"/> is <see langword="false"/> and playback commands are ignored, which lets the
    /// view model issue commands without knowing the view's lifecycle.
    /// </para>
    /// <para>
    /// <see cref="StateChanged"/> may be raised on a background thread. Subscribers that touch UI state must
    /// marshal to the UI thread.
    /// </para>
    /// </remarks>
    internal interface IVideoPlayer : IDisposable
    {
        /// <summary>
        /// Occurs when <see cref="Position"/>, <see cref="IsPaused"/> or <see cref="IsFileLoaded"/> changes.
        /// </summary>
        event EventHandler? StateChanged;

        /// <summary>
        /// Gets a value indicating whether the player is attached to a window and can accept commands.
        /// </summary>
        bool IsReady { get; }

        /// <summary>
        /// Gets the reason the player could not start, for example a missing native library, or <see langword="null"/>.
        /// </summary>
        string? FailureReason { get; }

        /// <summary>
        /// Gets a value indicating whether a file is loaded and ready to play.
        /// </summary>
        bool IsFileLoaded { get; }

        /// <summary>
        /// Gets the current playback position.
        /// </summary>
        TimeSpan Position { get; }

        /// <summary>
        /// Gets a value indicating whether playback is paused.
        /// </summary>
        bool IsPaused { get; }

        /// <summary>
        /// Starts the player inside the specified native window.
        /// </summary>
        /// <param name="windowHandle">The platform handle of the window to render into (an HWND on Windows, an XID on X11).</param>
        void Attach(nint windowHandle);

        /// <summary>
        /// Loads a file, paused at its first frame.
        /// </summary>
        /// <param name="filePath">The file to load.</param>
        void Open(string filePath);

        /// <summary>
        /// Pauses or resumes playback.
        /// </summary>
        /// <param name="paused"><see langword="true"/> to pause; <see langword="false"/> to play.</param>
        void SetPaused(bool paused);

        /// <summary>
        /// Moves the playback position.
        /// </summary>
        /// <param name="position">The target position.</param>
        /// <param name="exact">
        /// <see langword="true"/> to decode up to the exact frame, which is slower; <see langword="false"/> to jump to
        /// the nearest keyframe, which is fast enough for continuous scrubbing.
        /// </param>
        void Seek(TimeSpan position, bool exact);

        /// <summary>
        /// Moves exactly one frame forward or backward and pauses.
        /// </summary>
        /// <param name="backward"><see langword="true"/> to step backward; <see langword="false"/> to step forward.</param>
        void StepFrame(bool backward);
    }
}
