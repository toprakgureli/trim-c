// Copyright (c) Toprak Gureli. All rights reserved.
// Licensed under the MIT license. See LICENSE in the repository root for details.

using System;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Xunit;

namespace TrimC.Desktop.Services.Tests
{
    /// <summary>
    /// Verifies the "Open with" registration against a scratch key instead of the real file associations.
    /// </summary>
    /// <remarks>
    /// The tests still write to the registry of the current user, so they run only in CI (where <c>CI</c> is set) or
    /// when <c>TRIMC_RUN_REGISTRY_TESTS</c> is set, and never as a side effect of a local test run.
    /// </remarks>
    [SupportedOSPlatform("windows")]
    public sealed class WindowsShellIntegrationTests : IDisposable
    {
        private readonly string _root = $@"Software\trim-c-tests\{Guid.NewGuid():N}\Classes";

        public void Dispose()
        {
            if (OperatingSystem.IsWindows())
            {
                Registry.CurrentUser.DeleteSubKeyTree(_root[..(_root.LastIndexOf('\\'))], throwOnMissingSubKey: false);
            }
        }

        [Fact]
        public void Register_AddsProgIdApplicationAndOpenWithEntries()
        {
            SkipUnlessAllowed();
            WindowsShellIntegration integration = new(_root);

            integration.Register(@"C:\Tools\trim-c\trim-c.exe");

            using RegistryKey command = Registry.CurrentUser.OpenSubKey($@"{_root}\TrimC.Video\shell\open\command")!;
            Assert.Equal("\"C:\\Tools\\trim-c\\trim-c.exe\" \"%1\"", command.GetValue(string.Empty));

            using RegistryKey application = Registry.CurrentUser.OpenSubKey($@"{_root}\Applications\trim-c.exe")!;
            Assert.Equal("trim-c", application.GetValue("FriendlyAppName"));

            using RegistryKey openWith = Registry.CurrentUser.OpenSubKey($@"{_root}\.mkv\OpenWithProgids")!;
            Assert.Contains("TrimC.Video", openWith.GetValueNames());
        }

        [Fact]
        public void Register_Again_FollowsAMovedExecutable()
        {
            SkipUnlessAllowed();
            WindowsShellIntegration integration = new(_root);

            integration.Register(@"C:\Old\trim-c.exe");
            integration.Register(@"D:\New\trim-c.exe");

            using RegistryKey command = Registry.CurrentUser.OpenSubKey($@"{_root}\TrimC.Video\shell\open\command")!;
            Assert.Equal("\"D:\\New\\trim-c.exe\" \"%1\"", command.GetValue(string.Empty));
        }

        [Fact]
        public void Unregister_RemovesEverythingButLeavesOtherOpenWithEntries()
        {
            SkipUnlessAllowed();
            WindowsShellIntegration integration = new(_root);
            using (RegistryKey other = Registry.CurrentUser.CreateSubKey($@"{_root}\.mp4\OpenWithProgids"))
            {
                other.SetValue("Other.App", Array.Empty<byte>(), RegistryValueKind.None);
            }

            integration.Register(@"C:\Tools\trim-c.exe");
            integration.Unregister();

            Assert.Null(Registry.CurrentUser.OpenSubKey($@"{_root}\TrimC.Video"));
            Assert.Null(Registry.CurrentUser.OpenSubKey($@"{_root}\Applications\trim-c.exe"));
            using RegistryKey openWith = Registry.CurrentUser.OpenSubKey($@"{_root}\.mp4\OpenWithProgids")!;
            Assert.Equal(["Other.App"], openWith.GetValueNames());
        }

        private static void SkipUnlessAllowed()
        {
            Assert.SkipUnless(OperatingSystem.IsWindows(), "The registry exists only on Windows.");
            Assert.SkipWhen(
                Environment.GetEnvironmentVariable("CI") is null && Environment.GetEnvironmentVariable("TRIMC_RUN_REGISTRY_TESTS") is null,
                "Registry tests run only in CI or when TRIMC_RUN_REGISTRY_TESTS is set.");
        }
    }
}
