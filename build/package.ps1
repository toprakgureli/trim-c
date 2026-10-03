<#
.SYNOPSIS
    Builds the ready-to-run Windows package of trim-c.

.DESCRIPTION
    Publishes the desktop application as a self-contained win-x64 build, adds pinned FFmpeg and libmpv builds and the
    license notices, and writes a zip archive with its SHA-256 checksum. The person who downloads the archive needs
    nothing else: no .NET runtime, no FFmpeg installation and no PATH changes.

    Third-party binaries are pinned to exact releases and verified against known SHA-256 hashes, so every package built
    from the same commit contains the same files.

.PARAMETER Version
    The version written into the binaries and the archive name. Defaults to the version in Directory.Build.props.

.PARAMETER OutputDirectory
    The directory that receives the archive. Defaults to artifacts/ in the repository root.

.EXAMPLE
    ./build/package.ps1 -Version 0.1.0
#>
[CmdletBinding()]
param(
    [string] $Version,
    [string] $OutputDirectory,
    [string] $FFmpegUrl = 'https://github.com/BtbN/FFmpeg-Builds/releases/download/autobuild-2026-10-01-13-06/ffmpeg-n9.0.2-22-g46d8f462ee-win64-gpl-shared-9.0.zip',
    [string] $FFmpegSha256 = '64f7d1460ce986804386582eeec3bd95117a4e84f7567ad416dd33f434fe8286',
    [string] $MpvUrl = 'https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20261003/mpv-dev-x86_64-20261003-git-3186d369f9.7z',
    [string] $MpvSha256 = 'b8c2d656c1584d5f08196fcee5c2d0783d1f76021897c4e0b3cc30f94c79522e'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts' }
if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $root 'Directory.Build.props'))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}

$work = Join-Path $OutputDirectory 'work'
$downloads = Join-Path $OutputDirectory 'downloads'
$name = "trim-c-$Version-win-x64"
$stage = Join-Path $work $name
$archive = Join-Path $OutputDirectory "$name.zip"

function Get-VerifiedFile([string] $Url, [string] $Sha256) {
    New-Item -ItemType Directory -Force $downloads | Out-Null
    $path = Join-Path $downloads ([IO.Path]::GetFileName(([Uri]$Url).LocalPath))

    # Downloads are cached by file name; a cached file is only reused when its hash still matches.
    if (-not (Test-Path $path) -or (Get-FileHash $path -Algorithm SHA256).Hash -ne $Sha256) {
        Write-Host "Downloading $Url"
        Invoke-WebRequest -Uri $Url -OutFile $path -UseBasicParsing
    }

    $actual = (Get-FileHash $path -Algorithm SHA256).Hash
    if ($actual -ne $Sha256) {
        throw "Checksum mismatch for $path. Expected $Sha256, got $actual."
    }

    return $path
}

function Expand-7z([string] $Archive, [string] $Destination) {
    # GitHub's Windows runners ship 7-Zip; recent Windows versions can also read 7z archives with the built-in tar.
    $sevenZip = Get-Command 7z -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force $Destination | Out-Null
    if ($sevenZip) {
        & $sevenZip.Source x $Archive "-o$Destination" -y | Out-Null
    }
    else {
        & "$env:SystemRoot\System32\tar.exe" -xf $Archive -C $Destination
    }

    if ($LASTEXITCODE -ne 0) { throw "Could not extract $Archive." }
}

Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null

Write-Host "Publishing trim-c $Version"
dotnet publish (Join-Path $root 'src/TrimC.Desktop/TrimC.Desktop.csproj') `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    --output $stage `
    -p:Version=$Version `
    -p:DebugType=none `
    -p:PublishReadyToRun=true
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

# XML documentation files are for compilers, not for people running the application.
Get-ChildItem $stage -Filter '*.xml' | Remove-Item

Write-Host 'Adding FFmpeg'
$ffmpegZip = Get-VerifiedFile $FFmpegUrl $FFmpegSha256
$ffmpegExtract = Join-Path $work 'ffmpeg-extract'
Expand-Archive $ffmpegZip -DestinationPath $ffmpegExtract
$ffmpegRoot = Get-ChildItem $ffmpegExtract -Directory | Select-Object -First 1
$ffmpegTarget = Join-Path $stage 'ffmpeg'
New-Item -ItemType Directory -Force $ffmpegTarget | Out-Null

# The application looks for ffmpeg and ffprobe in an ffmpeg folder next to trim-c.exe. ffplay is not needed.
Get-ChildItem (Join-Path $ffmpegRoot.FullName 'bin') | Where-Object { $_.Name -ne 'ffplay.exe' } | Copy-Item -Destination $ffmpegTarget
Copy-Item (Join-Path $ffmpegRoot.FullName 'LICENSE.txt') (Join-Path $ffmpegTarget 'LICENSE.txt')

Write-Host 'Adding libmpv'
$mpvArchive = Get-VerifiedFile $MpvUrl $MpvSha256
$mpvExtract = Join-Path $work 'mpv-extract'
Expand-7z $mpvArchive $mpvExtract

# libmpv is loaded through P/Invoke from the application directory.
Copy-Item (Join-Path $mpvExtract 'libmpv-2.dll') $stage

Copy-Item (Join-Path $root 'LICENSE') $stage
Copy-Item (Join-Path $root 'THIRD-PARTY-NOTICES.md') $stage

foreach ($required in 'trim-c.exe', 'libmpv-2.dll', 'ffmpeg/ffmpeg.exe', 'ffmpeg/ffprobe.exe') {
    if (-not (Test-Path (Join-Path $stage $required))) { throw "The package is missing $required." }
}

Write-Host "Writing $archive"
Remove-Item $archive -ErrorAction SilentlyContinue
Compress-Archive -Path $stage -DestinationPath $archive -CompressionLevel Optimal
$hash = (Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path "$archive.sha256" -Value "$hash  $name.zip" -NoNewline -Encoding ascii

Remove-Item $work -Recurse -Force
Write-Host "Done: $archive ($([Math]::Round((Get-Item $archive).Length / 1MB)) MB)"
