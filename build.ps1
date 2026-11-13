#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes the ParentalControl projects and builds the Inno Setup installer.
.DESCRIPTION
    1. Publishes ParentalControl.Admin, Overlay, and Service as self-contained win-x64 with shared runtime deduplication and stripped debug symbols.
    2. Invokes the Inno Setup compiler with ultra-high LZMA2 solid compression.
#>
param(
    [string]$InnoSetupPath = "",
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot   = $PSScriptRoot
$srcDir     = Join-Path $repoRoot "src"
$publishDir = Join-Path $repoRoot "publish"
$appPublish = Join-Path $publishDir "app"
$issFile    = Join-Path $repoRoot "installer\ParentalControl.iss"

# Detect Inno Setup compiler
if (-not $InnoSetupPath -or -not (Test-Path $InnoSetupPath)) {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
        "C:\Program Files\Inno Setup 6\ISCC.exe"
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) {
            $InnoSetupPath = $c
            break
        }
    }
}

if (-not $InnoSetupPath -or -not (Test-Path $InnoSetupPath)) {
    Write-Error "Inno Setup compiler not found. Install from https://jrsoftware.org/isdownload.php or pass -InnoSetupPath."
    exit 1
}

# Clean previous publish output
if (Test-Path $publishDir) {
    Write-Host "Cleaning previous publish output..." -ForegroundColor Yellow
    Remove-Item $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $appPublish -Force | Out-Null

$publishArgs = @(
    "-c", $Configuration,
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:DebugType=None",
    "-p:DebugSymbols=false",
    "-p:SatelliteResourceLanguages=en-US",
    "-o", $appPublish
)

# Publish Admin
Write-Host "`n=== Publishing ParentalControl.Admin ===" -ForegroundColor Cyan
dotnet publish "$srcDir\ParentalControl.Admin\ParentalControl.Admin.csproj" @publishArgs
if ($LASTEXITCODE -ne 0) { Write-Error "Admin publish failed."; exit 1 }

# Publish Overlay
Write-Host "`n=== Publishing ParentalControl.Overlay ===" -ForegroundColor Cyan
dotnet publish "$srcDir\ParentalControl.Overlay\ParentalControl.Overlay.csproj" @publishArgs
if ($LASTEXITCODE -ne 0) { Write-Error "Overlay publish failed."; exit 1 }

# Publish Service
Write-Host "`n=== Publishing ParentalControl.Service ===" -ForegroundColor Cyan
dotnet publish "$srcDir\ParentalControl.Service\ParentalControl.Service.csproj" @publishArgs
if ($LASTEXITCODE -ne 0) { Write-Error "Service publish failed."; exit 1 }

# Build Installer
Write-Host "`n=== Building Installer with Ultra LZMA2 Compression ===" -ForegroundColor Cyan
& $InnoSetupPath $issFile
if ($LASTEXITCODE -ne 0) { Write-Error "Inno Setup compilation failed."; exit 1 }

$versionedExe = Join-Path $repoRoot "installer\Output\ParentalControlSetup-v1.3.0.exe"
$genericExe   = Join-Path $repoRoot "installer\Output\ParentalControlSetup.exe"
if (Test-Path $versionedExe) {
    Copy-Item $versionedExe $genericExe -Force
}
$sizeMb = [math]::Round((Get-Item $versionedExe).Length / 1MB, 2)
Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "Installer (Versioned): $versionedExe"
Write-Host "Installer (Generic):   $genericExe"
Write-Host "Installer Size: $sizeMb MB"
