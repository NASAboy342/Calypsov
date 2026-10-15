#!/usr/bin/env pwsh
# Builds Calypsov into a distributable Windows installer (.exe via Inno Setup).
#
# Usage: pwsh scripts/package-windows.ps1
# Must run on an actual Windows host (or the windows-latest GitHub Actions
# runner) - Inno Setup's ISCC.exe has no macOS/Linux build. Output lands in
# Deployed/Windows/.
#
# This does NOT code-sign the installer or the exe - unsigned builds trigger
# Windows SmartScreen on first run. Same already-accepted tradeoff as the
# macOS build's ad-hoc signing (see package-macos.sh).

$ErrorActionPreference = "Stop"

$RootDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$AppProjectDir = Join-Path $RootDir "Calypsov"
$UiDir = Join-Path $AppProjectDir "UserInterface"
$OutDir = Join-Path $RootDir "Deployed/Windows"
$Rid = "win-x64"

# Single source of truth is Calypsov.csproj's <Version> - read it live so this
# script can never drift from the app's own reported version.
$Version = (dotnet msbuild (Join-Path $AppProjectDir "Calypsov.csproj") -getProperty:Version -nologo).Trim()

Write-Host "==> Building frontend"
# build-only rather than build: the full `build` script also runs a type-check
# step that currently fails on a pre-existing TS parsing error inside
# @vitejs/plugin-vue-jsx's own .d.mts files (unrelated to app code, and
# unrelated to packaging) - it doesn't affect the actual Vite output.
Push-Location $UiDir
try {
    npm run build-only
} finally {
    Pop-Location
}

Write-Host "==> Syncing frontend build into wwwroot"
$WwwrootAssets = Join-Path $AppProjectDir "wwwroot/assets"
Get-ChildItem -Path $WwwrootAssets -Include "*.js", "*.css" -File -ErrorAction SilentlyContinue | Remove-Item -Force
Copy-Item -Path (Join-Path $UiDir "dist/assets/*") -Destination $WwwrootAssets -Recurse -Force
Copy-Item -Path (Join-Path $UiDir "dist/index.html") -Destination (Join-Path $AppProjectDir "wwwroot/index.html") -Force
Copy-Item -Path (Join-Path $UiDir "dist/favicon.ico") -Destination (Join-Path $AppProjectDir "wwwroot/favicon.ico") -Force

Write-Host "==> Publishing self-contained .NET app for $Rid"
Push-Location $AppProjectDir
try {
    dotnet publish -c Release -r $Rid --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true
} finally {
    Pop-Location
}

Write-Host "==> Building installer with Inno Setup"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

$Iscc = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if ($Iscc) {
    $IsccPath = $Iscc.Source
} else {
    $IsccPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $IsccPath)) {
        throw "ISCC.exe not found on PATH or at the default Inno Setup 6 install location. Install Inno Setup (https://jrsoftware.org/isinfo.php) to build the Windows installer."
    }
}

& $IsccPath "/DAppVersion=$Version" (Join-Path $PSScriptRoot "windows-installer.iss")
if ($LASTEXITCODE -ne 0) {
    throw "ISCC.exe exited with code $LASTEXITCODE"
}

Write-Host "==> Done"
Write-Host "Installer: $OutDir/Calypsov-$Version-win-x64-setup.exe"
