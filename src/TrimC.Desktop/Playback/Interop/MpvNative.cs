// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Reflection;
using System.Runtime.InteropServices;

// libmpv is loaded only from the application directory and the system directories, never from the current working
// directory, which prevents a library planted next to an opened media file from being loaded (CA5392, CA5393).
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]

namespace TrimC.Desktop.Playback.Interop
{
    /// <summary>
    /// P/Invoke declarations for the subset of the libmpv client API (<c>client.h</c>, API version 2) used by the player.
    /// </summary>
    /// <remarks>
    /// Declarations use <see cref="LibraryImportAttribute"/>, so marshalling stubs are generated at compile time and
    /// the interop layer is compatible with trimming and native AOT. Managed names are the PascalCase form of the
    /// native names, which are kept verbatim in <see cref="LibraryImportAttribute.EntryPoint"/> for cross-referencing
    /// with the documentation in mpv's <c>libmpv/client.h</c>.
    /// </remarks>
    internal static unsafe partial class MpvNative
    {
        private const string LibraryName = "libmpv-2";

        static MpvNative()
        {
            // The Windows builds ship as libmpv-2.dll, while Linux and macOS distributions use the soname
            // libmpv.so.2 and libmpv.2.dylib, which the default probing rules would not find from "libmpv-2".
            NativeLibrary.SetDllImportResolver(typeof(MpvNative).Assembly, ResolveLibrary);
        }

        [LibraryImport(LibraryName, EntryPoint = "mpv_create")]
        public static partial nint Create();

        [LibraryImport(LibraryName, EntryPoint = "mpv_initialize")]
        public static partial MpvError Initialize(nint ctx);

        [LibraryImport(LibraryName, EntryPoint = "mpv_terminate_destroy")]
        public static partial void TerminateDestroy(nint ctx);

        [LibraryImport(LibraryName, EntryPoint = "mpv_set_option_string", StringMarshalling = StringMarshalling.Utf8)]
        public static partial MpvError SetOptionString(nint ctx, string name, string data);

        [LibraryImport(LibraryName, EntryPoint = "mpv_set_option", StringMarshalling = StringMarshalling.Utf8)]
        public static partial MpvError SetOption(nint ctx, string name, MpvFormat format, void* data);

        [LibraryImport(LibraryName, EntryPoint = "mpv_set_property_string", StringMarshalling = StringMarshalling.Utf8)]
        public static partial MpvError SetPropertyString(nint ctx, string name, string data);

        [LibraryImport(LibraryName, EntryPoint = "mpv_command")]
        public static partial MpvError Command(nint ctx, nint* args);

        [LibraryImport(LibraryName, EntryPoint = "mpv_observe_property", StringMarshalling = StringMarshalling.Utf8)]
        public static partial MpvError ObserveProperty(nint ctx, ulong replyUserdata, string name, MpvFormat format);

        [LibraryImport(LibraryName, EntryPoint = "mpv_wait_event")]
        public static partial MpvEvent* WaitEvent(nint ctx, double timeout);

        [LibraryImport(LibraryName, EntryPoint = "mpv_wakeup")]
        public static partial void Wakeup(nint ctx);

        [LibraryImport(LibraryName, EntryPoint = "mpv_error_string")]
        public static partial nint ErrorString(MpvError error);

        /// <summary>
        /// Gets the human readable description of an error code.
        /// </summary>
        /// <param name="error">The error code.</param>
        /// <returns>The description provided by libmpv.</returns>
        public static string GetErrorString(MpvError error) => Marshal.PtrToStringUTF8(ErrorString(error)) ?? error.ToString();

        private static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName != LibraryName)
            {
                return 0;
            }

            string[] candidates = OperatingSystem.IsWindows() ? ["libmpv-2", "mpv-2"]
                : OperatingSystem.IsMacOS() ? ["libmpv.2.dylib", "libmpv.dylib"]
                : ["libmpv.so.2", "libmpv.so"];

            foreach (string candidate in candidates)
            {
                if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out nint handle))
                {
                    return handle;
                }
            }

            // Returning zero falls back to default probing, which raises a DllNotFoundException naming the library.
            return 0;
        }
    }
}
