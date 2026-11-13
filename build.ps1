#Requires -Version 5.1
<#
.SYNOPSIS
    Publishes the ParentalControl projects and builds the Inno Setup installer.
.DESCRIPTION
    1. Publishes ParentalControl.Service and ParentalControl.Admin as self-contained win-x64.
    2. Invokes the Inno Setup compiler to produce ParentalControlSetup.exe.
.PARAMETER InnoSetupPath
    Path to iscc.exe. Defaults to the standard Inno Setup 6 install location.
.PARAMETER Configuration
    Build configuration. Defaults to Release.
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

# Publish Service
Write-Host "`n=== Publishing ParentalControl.Service ===" -ForegroundColor Cyan
dotnet publish "$srcDir\ParentalControl.Service\ParentalControl.Service.csproj" `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output "$publishDir\service"

if ($LASTEXITCODE -ne 0) { Write-Error "Service publish failed."; exit 1 }

# Publish Admin
Write-Host "`n=== Publishing ParentalControl.Admin ===" -ForegroundColor Cyan
dotnet publish "$srcDir\ParentalControl.Admin\ParentalControl.Admin.csproj" `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output "$publishDir\admin"

if ($LASTEXITCODE -ne 0) { Write-Error "Admin publish failed."; exit 1 }

# Publish Overlay
Write-Host "`n=== Publishing ParentalControl.Overlay ===" -ForegroundColor Cyan
dotnet publish "$srcDir\ParentalControl.Overlay\ParentalControl.Overlay.csproj" `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output "$publishDir\overlay"

if ($LASTEXITCODE -ne 0) { Write-Error "Overlay publish failed."; exit 1 }

# Build Installer
Write-Host "`n=== Building Installer ===" -ForegroundColor Cyan
& $InnoSetupPath $issFile

if ($LASTEXITCODE -ne 0) { Write-Error "Inno Setup compilation failed."; exit 1 }

$outputExe = Join-Path $repoRoot "installer\Output\ParentalControlSetup.exe"
Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "Installer: $outputExe"
