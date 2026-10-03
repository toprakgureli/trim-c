// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using TrimC.Desktop.Playback.Interop;

namespace TrimC.Desktop.Playback
{
    /// <summary>
    /// An <see cref="IVideoPlayer"/> backed by libmpv.
    /// </summary>
    /// <remarks>
    /// <para>
    /// mpv owns decoding and presentation entirely: it renders into the attached native window using its own GPU
    /// pipeline with hardware decoding, so no frame ever crosses into managed memory. This is what keeps scrubbing
    /// through high bit rate recordings smooth.
    /// </para>
    /// <para>
    /// State changes are delivered through property observation on a dedicated event thread. Commands are issued
    /// from the calling thread; libmpv's client API is thread safe for everything except concurrent calls to
    /// <c>mpv_wait_event</c> and destruction of the handle, both of which this type serializes.
    /// </para>
    /// </remarks>
    internal sealed partial class MpvPlayer : IVideoPlayer
    {
        private const ulong TimePositionObserverId = 1;
        private const ulong PauseObserverId = 2;

        private readonly ILogger<MpvPlayer> _logger;
        private nint _handle;
        private Thread? _eventThread;
        private long _positionTicks;
        private volatile bool _isPaused = true;
        private volatile bool _isFileLoaded;
        private volatile bool _isDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="MpvPlayer"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> is <see langword="null"/>.</exception>
        public MpvPlayer(ILogger<MpvPlayer> logger)
        {
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
        }

        /// <inheritdoc/>
        public event EventHandler? StateChanged;

        /// <inheritdoc/>
        public bool IsReady => _handle != 0 && !_isDisposed;

        /// <inheritdoc/>
        public string? FailureReason { get; private set; }

        /// <inheritdoc/>
        public bool IsFileLoaded => _isFileLoaded;

        /// <inheritdoc/>
        public TimeSpan Position => TimeSpan.FromTicks(Interlocked.Read(ref _positionTicks));

        /// <inheritdoc/>
        public bool IsPaused => _isPaused;

        /// <inheritdoc/>
        /// <exception cref="InvalidOperationException">The player is already attached.</exception>
        public unsafe void Attach(nint windowHandle)
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);
            if (_handle != 0)
            {
                throw new InvalidOperationException("The player is already attached to a window.");
            }

            nint handle;
            try
            {
                handle = MpvNative.Create();
            }
            catch (DllNotFoundException ex)
            {
                FailureReason = "libmpv was not found. Place libmpv-2.dll next to the application to enable preview.";
                LogLibraryMissing(ex);
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            if (handle == 0)
            {
                FailureReason = "libmpv could not create a player instance.";
                StateChanged?.Invoke(this, EventArgs.Empty);
                return;
            }

            try
            {
                // The window must be assigned before initialization; mpv creates its video output during mpv_initialize.
                long wid = windowHandle;
                Check(MpvNative.SetOption(handle, "wid", MpvFormat.Int64, &wid), "wid");

                // Editing-oriented defaults. User configuration is ignored so that behaviour is identical on every machine.
                SetOption(handle, "config", "no");
                SetOption(handle, "load-scripts", "no");
                SetOption(handle, "ytdl", "no");
                SetOption(handle, "terminal", "no");
                SetOption(handle, "osc", "no");
                SetOption(handle, "osd-level", "0");
                SetOption(handle, "input-default-bindings", "no");
                SetOption(handle, "input-vo-keyboard", "no");
                SetOption(handle, "input-cursor", "no");
                SetOption(handle, "cursor-autohide", "no");
                SetOption(handle, "idle", "yes");
                SetOption(handle, "keep-open", "always");
                SetOption(handle, "pause", "yes");
                SetOption(handle, "hwdec", "auto-safe");

                // Precise seeks decode from the preceding keyframe to the requested frame instead of snapping, and
                // dropping frames during that decode would make the displayed frame differ from the reported position.
                SetOption(handle, "hr-seek", "yes");
                SetOption(handle, "hr-seek-framedrop", "no");

                Check(MpvNative.Initialize(handle), "initialize");
                Check(MpvNative.ObserveProperty(handle, TimePositionObserverId, "time-pos", MpvFormat.Double), "time-pos");
                Check(MpvNative.ObserveProperty(handle, PauseObserverId, "pause", MpvFormat.Flag), "pause");
            }
            catch
            {
                // A handle that failed to initialize is still allocated and must be released.
                MpvNative.TerminateDestroy(handle);
                throw;
            }

            _handle = handle;
            _eventThread = new Thread(RunEventLoop) { IsBackground = true, Name = "mpv events" };
            _eventThread.Start();

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        public void Open(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            _isFileLoaded = false;
            Command("loadfile", filePath, "replace");
        }

        /// <inheritdoc/>
        public void SetPaused(bool paused)
        {
            if (IsReady)
            {
                Check(MpvNative.SetPropertyString(_handle, "pause", paused ? "yes" : "no"), "pause");
            }
        }

        /// <inheritdoc/>
        public void Seek(TimeSpan position, bool exact)
        {
            string seconds = position.TotalSeconds.ToString("0.000000", CultureInfo.InvariantCulture);
            Command("seek", seconds, exact ? "absolute+exact" : "absolute+keyframes");
        }

        /// <inheritdoc/>
        public void StepFrame(bool backward) => Command(backward ? "frame-back-step" : "frame-step");

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;

            nint handle = _handle;
            if (handle == 0)
            {
                return;
            }

            // mpv_terminate_destroy must not race with mpv_wait_event, so the event thread is woken and joined first.
            MpvNative.Wakeup(handle);
            _eventThread?.Join();

            _handle = 0;
            MpvNative.TerminateDestroy(handle);
        }

        private static void SetOption(nint handle, string name, string value) =>
            Check(MpvNative.SetOptionString(handle, name, value), name);

        private static void Check(MpvError error, string operation)
        {
            if (error < MpvError.Success)
            {
                throw new InvalidOperationException($"mpv rejected '{operation}': {MpvNative.GetErrorString(error)}.");
            }
        }

        private unsafe void Command(params string[] arguments)
        {
            if (!IsReady)
            {
                return;
            }

            // mpv_command expects a NULL-terminated array of UTF-8 strings that remain valid for the duration of the call.
            nint* argv = stackalloc nint[arguments.Length + 1];
            try
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    argv[i] = Marshal.StringToCoTaskMemUTF8(arguments[i]);
                }

                argv[arguments.Length] = 0;

                MpvError error = MpvNative.Command(_handle, argv);
                if (error < MpvError.Success)
                {
                    LogCommandFailed(arguments[0], MpvNative.GetErrorString(error));
                }
            }
            finally
            {
                for (int i = 0; i < arguments.Length; i++)
                {
                    Marshal.FreeCoTaskMem(argv[i]);
                }
            }
        }

        private unsafe void RunEventLoop()
        {
            nint handle = _handle;
            while (!_isDisposed)
            {
                MpvEvent* e = MpvNative.WaitEvent(handle, -1);
                switch (e->EventId)
                {
                    case MpvEventId.Shutdown:
                        return;

                    case MpvEventId.FileLoaded:
                        _isFileLoaded = true;
                        StateChanged?.Invoke(this, EventArgs.Empty);
                        break;

                    case MpvEventId.EndFile:
                        _isFileLoaded = false;
                        StateChanged?.Invoke(this, EventArgs.Empty);
                        break;

                    case MpvEventId.PropertyChange:
                        OnPropertyChanged(e->ReplyUserdata, (MpvEventProperty*)e->Data);
                        break;
                }
            }
        }

        private unsafe void OnPropertyChanged(ulong observerId, MpvEventProperty* property)
        {
            // A property without data means it is currently unavailable, for example time-pos while no file is loaded.
            if (property->Data == null)
            {
                return;
            }

            switch (observerId)
            {
                case TimePositionObserverId when property->Format == MpvFormat.Double:
                    double seconds = *(double*)property->Data;
                    Interlocked.Exchange(ref _positionTicks, TimeSpan.FromSeconds(Math.Max(seconds, 0)).Ticks);
                    break;

                case PauseObserverId when property->Format == MpvFormat.Flag:
                    _isPaused = *(int*)property->Data != 0;
                    break;

                default:
                    return;
            }

            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        [LoggerMessage(Level = LogLevel.Warning, Message = "libmpv is not available; video preview is disabled")]
        private partial void LogLibraryMissing(Exception exception);

        [LoggerMessage(Level = LogLevel.Warning, Message = "mpv command {Command} failed: {Error}")]
        private partial void LogCommandFailed(string command, string error);
    }
}
