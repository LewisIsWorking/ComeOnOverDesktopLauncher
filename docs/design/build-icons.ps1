<#
.SYNOPSIS
    Rasterise docs/design/appicon.svg into the .ico + .png assets that
    the launcher consumes at build time and runtime.

.DESCRIPTION
    Idempotent: run this any time the SVG changes. Outputs:

      ComeOnOverDesktopLauncher/Assets/appicon.ico        - nested
          16/24/32/48/64/128/256 px variants for <ApplicationIcon>
          in the csproj (Explorer/Start menu/pinned shortcut icon).
      ComeOnOverDesktopLauncher/Assets/appicon-256.png    - consumed
          by MainWindow.axaml and TrayIconService.
      ComeOnOverDesktopLauncher/Assets/appicon-64.png     - optional
          taskbar-size preview for README/ROADMAP screenshots.
      ComeOnOverDesktopLauncher/Assets/appicon-32.png     - tray
          icon fallback size.

    Requires ImageMagick 7+ on PATH. Install via:
        winget install ImageMagick.ImageMagick

.NOTES
    The generated .ico and .png files are committed to git alongside
    the SVG so that the release CI runner does NOT need ImageMagick
    installed - CI just consumes the pre-rasterised binaries. Rerun
    this script locally whenever the SVG is edited.
#>

$ErrorActionPreference = 'Stop'

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir '..' '..')
$svg = Join-Path $scriptDir 'appicon.svg'
$assetsDir = Join-Path $repoRoot 'ComeOnOverDesktopLauncher\Assets'

if (-not (Test-Path $svg)) {
    throw "Source SVG not found at $svg"
}
if (-not (Get-Command magick -ErrorAction SilentlyContinue)) {
    throw "ImageMagick 'magick' not on PATH. Install via: winget install ImageMagick.ImageMagick"
}

Write-Host "Source: $svg"
Write-Host "Output: $assetsDir"
Write-Host ""

New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null

# Single PNGs at specific sizes. -background none preserves transparency
# around the rounded-square backdrop (we want the dark corners in the
# icon artwork, but transparent OUTSIDE the rounded square so the icon
# blends into light/dark taskbars without a white halo).
$sizes = @(
    @{ Size = 256; Out = 'appicon-256.png' },
    @{ Size = 64;  Out = 'appicon-64.png' },
    @{ Size = 32;  Out = 'appicon-32.png' }
)

foreach ($s in $sizes) {
    $out = Join-Path $assetsDir $s.Out
    Write-Host "-> $($s.Out) ($($s.Size)x$($s.Size))"
    magick -background none -density 600 $svg -resize "$($s.Size)x$($s.Size)" $out
    if ($LASTEXITCODE -ne 0) { throw "magick failed on $($s.Out)" }
}

# Multi-resolution .ico. ImageMagick generates each size by resampling
# the SVG; auto-resize=16,24,32,48,64,128,256 bakes all seven into one
# .ico so Windows picks the best variant for whatever context it's
# rendering in (16px for file list, 32px for taskbar, 256px for
# detailed-view Explorer).
$ico = Join-Path $assetsDir 'appicon.ico'
Write-Host "-> appicon.ico (multi-resolution: 16,24,32,48,64,128,256)"
magick -background none -density 600 $svg -define icon:auto-resize="16,24,32,48,64,128,256" $ico
if ($LASTEXITCODE -ne 0) { throw "magick failed on appicon.ico" }

Write-Host ""
Write-Host "Done. Generated assets:"
Get-ChildItem $assetsDir -Filter 'appicon*' | ForEach-Object {
    "  $($_.Name) ($([Math]::Round($_.Length / 1KB, 1)) KB)"
}
