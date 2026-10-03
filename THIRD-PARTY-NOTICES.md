# Third-party notices

The source code of trim-c is licensed under the MIT license (see [LICENSE](LICENSE)). The ready-to-run Windows package
also contains the following components, which are distributed under their own licenses. Because some of them are
licensed under the GNU GPL, the package as a whole is distributed under the terms of the GNU GPL version 3; the MIT
license of the trim-c source code is compatible with it.

## FFmpeg

- Files: `ffmpeg/ffmpeg.exe`, `ffmpeg/ffprobe.exe` and the DLLs in the `ffmpeg` folder
- License: GNU General Public License version 3 or later (built with `--enable-gpl --enable-version3`); the full text is
  in `ffmpeg/LICENSE.txt`
- Build: [BtbN/FFmpeg-Builds, autobuild-2026-10-01-13-06](https://github.com/BtbN/FFmpeg-Builds/releases/tag/autobuild-2026-10-01-13-06),
  `ffmpeg-n9.0.2-22-g46d8f462ee-win64-gpl-shared-9.0.zip`
- Source: [FFmpeg n9.0.2-22-g46d8f462ee](https://github.com/FFmpeg/FFmpeg/tree/46d8f462ee) and the build scripts at
  [BtbN/FFmpeg-Builds](https://github.com/BtbN/FFmpeg-Builds)

trim-c runs FFmpeg as a separate program and does not link to it.

## libmpv

- File: `libmpv-2.dll`
- License: GNU General Public License version 2 or later, as configured by the build below
- Build: [shinchiro/mpv-winbuild-cmake, 20261003](https://github.com/shinchiro/mpv-winbuild-cmake/releases/tag/20261003),
  `mpv-dev-x86_64-20261003-git-3186d369f9.7z`
- Source: [mpv commit 3186d369f9](https://github.com/mpv-player/mpv/tree/3186d369f9) and the build scripts at
  [shinchiro/mpv-winbuild-cmake](https://github.com/shinchiro/mpv-winbuild-cmake)

## .NET runtime and libraries

The package includes the .NET runtime and the following libraries, all under the MIT license:

- [.NET](https://github.com/dotnet/runtime) and [Microsoft.Extensions](https://github.com/dotnet/extensions), Copyright (c) .NET Foundation and Contributors
- [Avalonia](https://github.com/AvaloniaUI/Avalonia), Copyright (c) AvaloniaUI OÜ and contributors
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet), Copyright (c) .NET Foundation and Contributors

Avalonia uses [SkiaSharp](https://github.com/mono/SkiaSharp) (MIT) and [HarfBuzzSharp](https://github.com/mono/SkiaSharp) (MIT), which
include Skia (BSD-3-Clause) and HarfBuzz (MIT). The Inter font is licensed under the SIL Open Font License 1.1.
