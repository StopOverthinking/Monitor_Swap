param(
    [string]$Version
)

$ErrorActionPreference = "Stop"

$assetScript = Join-Path $PSScriptRoot "tools\GenerateBrandAssets.ps1"
& $assetScript

if (-not $Version) {
    $assemblyInfoPath = Join-Path $PSScriptRoot "Properties\AssemblyInfo.cs"
    $assemblyInfo = Get-Content $assemblyInfoPath -Raw
    $match = [regex]::Match($assemblyInfo, 'AssemblyFileVersion\("([^"]+)"\)')
    if (-not $match.Success) {
        throw "Unable to determine the application version from Properties\\AssemblyInfo.cs."
    }

    $Version = $match.Groups[1].Value
}

& (Join-Path $PSScriptRoot "build.ps1") -Configuration Release

$isccPath = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 6\ISCC.exe")
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $isccPath) {
    $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($command) {
        $isccPath = $command.Source
    }
}

if (-not $isccPath) {
    throw "Inno Setup 6 was not found. Install it, then rerun package.ps1."
}

$releaseDirectory = Join-Path $PSScriptRoot "bin\Release"
$distDirectory = Join-Path $PSScriptRoot "dist"
$installerScriptPath = Join-Path $PSScriptRoot "installer\MonitorSwap.iss"

New-Item -ItemType Directory -Force -Path $distDirectory | Out-Null

& $isccPath "/DAppVersion=$Version" "/DSourceDir=$releaseDirectory" "/DOutputDir=$distDirectory" $installerScriptPath

if ($LASTEXITCODE -ne 0) {
    throw "Installer build failed with exit code $LASTEXITCODE"
}

Write-Host "Built installer in $distDirectory"
