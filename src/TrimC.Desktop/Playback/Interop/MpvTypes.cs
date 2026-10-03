// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System.Runtime.InteropServices;

namespace TrimC.Desktop.Playback.Interop
{
    // Native types from libmpv/client.h. Values and field order must match the C definitions exactly.

    /// <summary>
    /// <c>mpv_error</c>: zero or positive values indicate success, negative values are errors.
    /// </summary>
    internal enum MpvError
    {
        Success = 0,
    }

    /// <summary>
    /// <c>mpv_format</c>: the data type of an option, property or event payload.
    /// </summary>
    internal enum MpvFormat
    {
        None = 0,
        String = 1,
        OsdString = 2,
        Flag = 3,
        Int64 = 4,
        Double = 5,
    }

    /// <summary>
    /// <c>mpv_event_id</c>: the subset of event identifiers the player reacts to.
    /// </summary>
    internal enum MpvEventId
    {
        None = 0,
        Shutdown = 1,
        EndFile = 7,
        FileLoaded = 8,
        PropertyChange = 22,
    }

    /// <summary>
    /// <c>mpv_event</c>: returned by <c>mpv_wait_event</c> and owned by the mpv handle.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MpvEvent
    {
        public MpvEventId EventId;
        public MpvError Error;
        public ulong ReplyUserdata;
        public void* Data;
    }

    /// <summary>
    /// <c>mpv_event_property</c>: the payload of <see cref="MpvEventId.PropertyChange"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct MpvEventProperty
    {
        public byte* Name;
        public MpvFormat Format;
        public void* Data;
    }
}
