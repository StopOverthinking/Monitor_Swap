param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

$compiler = @(
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $compiler) {
    throw "C# compiler not found in the .NET Framework installation."
}

$assetScript = Join-Path $PSScriptRoot "tools\GenerateBrandAssets.ps1"
if (-not (Test-Path (Join-Path $PSScriptRoot "Assets\MonitorSwap.ico"))) {
    & $assetScript
}

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $PSScriptRoot ("bin\" + $Configuration)
}

$outputPath = Join-Path $OutputDirectory "MonitorSwap.exe"
$win32IconPath = Join-Path $PSScriptRoot "Assets\MonitorSwap.ico"

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$sources = @(
    "Program.cs",
    "TrayApplicationContext.cs",
    "Properties\AssemblyInfo.cs",
    "Models\AppLanguage.cs",
    "Models\AppSettings.cs",
    "Native\NativeMethods.cs",
    "Services\AutoStartService.cs",
    "Services\AppIconProvider.cs",
    "Services\AppLocalization.cs",
    "Services\HotkeyManager.cs",
    "Services\MonitorDisplayService.cs",
    "Services\RotationTraceService.cs",
    "Services\SettingsService.cs",
    "Services\WindowRotationService.cs",
    "Forms\SettingsForm.cs"
)

$debugType = if ($Configuration -eq "Release") { "pdbonly" } else { "full" }
$optimizeFlag = if ($Configuration -eq "Release") { "/optimize+" } else { "/optimize-" }
$defineConstants = if ($Configuration -eq "Release") { "TRACE" } else { "DEBUG;TRACE" }

$arguments = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/out:$outputPath",
    "/debug:$debugType",
    $optimizeFlag,
    "/define:$defineConstants",
    "/win32manifest:app.manifest",
    "/win32icon:$win32IconPath",
    "/r:System.dll",
    "/r:System.Core.dll",
    "/r:System.Drawing.dll",
    "/r:System.Runtime.Serialization.dll",
    "/r:System.Windows.Forms.dll"
) + $sources

& $compiler @arguments

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

Write-Host "Built $outputPath"
