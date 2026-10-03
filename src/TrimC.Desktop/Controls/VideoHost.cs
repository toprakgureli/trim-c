// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using TrimC.Desktop.Playback;

namespace TrimC.Desktop.Controls
{
    /// <summary>
    /// Hosts the native window that an <see cref="IVideoPlayer"/> renders into.
    /// </summary>
    /// <remarks>
    /// The host owns the lifetime of the player's output: the player is attached as soon as both the native window
    /// and the player are available, and it is disposed when the native window is destroyed, because a player that
    /// keeps rendering into a destroyed window would fault inside the native video output.
    /// </remarks>
    internal sealed class VideoHost : NativeControlHost
    {
        /// <summary>
        /// Defines the <see cref="Player"/> property.
        /// </summary>
        public static readonly StyledProperty<IVideoPlayer?> PlayerProperty =
            AvaloniaProperty.Register<VideoHost, IVideoPlayer?>(nameof(Player));

        private IPlatformHandle? _nativeHandle;

        /// <summary>
        /// Gets or sets the player that renders into this host.
        /// </summary>
        public IVideoPlayer? Player
        {
            get => GetValue(PlayerProperty);
            set => SetValue(PlayerProperty, value);
        }

        /// <inheritdoc/>
        protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
        {
            _nativeHandle = base.CreateNativeControlCore(parent);
            TryAttach();
            return _nativeHandle;
        }

        /// <inheritdoc/>
        protected override void DestroyNativeControlCore(IPlatformHandle control)
        {
            Player?.Dispose();
            _nativeHandle = null;
            base.DestroyNativeControlCore(control);
        }

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == PlayerProperty)
            {
                TryAttach();
            }
        }

        private void TryAttach()
        {
            IVideoPlayer? player = Player;
            if (_nativeHandle is not null && player is { IsReady: false, FailureReason: null })
            {
                player.Attach(_nativeHandle.Handle);
            }
        }
    }
}
