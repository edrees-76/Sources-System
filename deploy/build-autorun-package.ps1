<#
.SYNOPSIS
    Assembles the final distribution folder: AutoRun launcher + installer + user guides (Round 214).

.DESCRIPTION
    1. Builds Sources.AutoRun (Release; net48 so it runs before the system is installed).
    2. Verifies the installer against its .sha256 file.
    3. Creates <OutputDir> containing:
         SourcesAutoRun.exe (+ .config), autorun.inf, autorun.config,
         SourcesSystemSetup_v<Version>.exe (+ .sha256),
         Docs\UserGuide.ar.pdf, Docs\UserGuide.en.pdf

.PARAMETER Version
    Installer version. Defaults to <Version> in Sources-System-Project\Sources.csproj.

.PARAMETER InstallerDir
    Folder holding the installer and its .sha256. Defaults to deploy\output\v<Version>.

.PARAMETER ArabicGuide
    Path to the Arabic user guide PDF.

.PARAMETER EnglishGuide
    Path to the English user guide PDF.

.PARAMETER ActivationCode
    System activation code shown by the launcher. If omitted, deploy\autorun\autorun.config is copied.
    The real code is never committed to the repository.

.PARAMETER OutputDir
    Destination folder. Defaults to deploy\output\v<Version>-AutoRun. It is recreated on each run.

.EXAMPLE
    .\deploy\build-autorun-package.ps1 -ArabicGuide "C:\guides\ar.pdf" -EnglishGuide "C:\guides\en.pdf" -ActivationCode "AAAA-BBBB"
#>

[CmdletBinding()]
param(
    [string]$Version,
    [string]$InstallerDir,
    [Parameter(Mandatory = $true)][string]$ArabicGuide,
    [Parameter(Mandatory = $true)][string]$EnglishGuide,
    [string]$ActivationCode,
    [string]$OutputDir,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$deployDir = $PSScriptRoot
$repoRoot = (Resolve-Path (Join-Path $deployDir "..")).Path
$autorunProject = Join-Path $repoRoot "Sources.AutoRun\Sources.AutoRun.csproj"
$autorunAssets = Join-Path $deployDir "autorun"

if (-not $Version) {
    $csproj = Get-Content (Join-Path $repoRoot "Sources-System-Project\Sources.csproj") -Raw
    if ($csproj -notmatch '<Version>([^<]+)</Version>') { throw "Could not read <Version> from Sources.csproj." }
    $Version = $Matches[1]
}
if (-not $InstallerDir) { $InstallerDir = Join-Path $deployDir "output\v$Version" }
if (-not $OutputDir) { $OutputDir = Join-Path $deployDir "output\v$Version-AutoRun" }

$installerName = "SourcesSystemSetup_v$Version.exe"
$installerPath = Join-Path $InstallerDir $installerName
$checksumPath = "$installerPath.sha256"

foreach ($required in @($installerPath, $checksumPath, $ArabicGuide, $EnglishGuide)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) { throw "Required file not found: $required" }
}

Write-Host "=== Step 1/4: verify installer checksum ==="
$expected = ((Get-Content -LiteralPath $checksumPath -Raw).Trim() -split '\s+')[0]
$actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $installerPath).Hash
if ($expected -ne $actual) { throw "Installer checksum mismatch. Expected $expected but got $actual." }
Write-Host "OK  $actual"

Write-Host "=== Step 2/4: build Sources.AutoRun ($Configuration) ==="
& dotnet build $autorunProject --configuration $Configuration --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet build failed (exit $LASTEXITCODE)." }
$buildDir = Join-Path $repoRoot "Sources.AutoRun\bin\$Configuration\net48"

Write-Host "=== Step 3/4: resolve activation code ==="
$configSource = Join-Path $autorunAssets "autorun.config"
if ($ActivationCode) {
    $configText = "ActivationCode=$ActivationCode`r`n"
} elseif (Test-Path -LiteralPath $configSource) {
    $configText = Get-Content -LiteralPath $configSource -Raw
} else {
    throw "No activation code: pass -ActivationCode or create deploy\autorun\autorun.config (see autorun.config.example)."
}
if ($configText -notmatch '(?m)^\s*ActivationCode\s*=\s*\S+') { throw "autorun.config has no ActivationCode value." }
if ($configText -match 'XXXX-XXXX') { throw "ActivationCode is still the placeholder from autorun.config.example." }

Write-Host "=== Step 4/4: assemble $OutputDir ==="
if (Test-Path -LiteralPath $OutputDir) { Remove-Item -LiteralPath $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path (Join-Path $OutputDir "Docs") | Out-Null

Copy-Item -LiteralPath (Join-Path $buildDir "SourcesAutoRun.exe") -Destination $OutputDir
$exeConfig = Join-Path $buildDir "SourcesAutoRun.exe.config"
if (Test-Path -LiteralPath $exeConfig) { Copy-Item -LiteralPath $exeConfig -Destination $OutputDir }
Copy-Item -LiteralPath (Join-Path $autorunAssets "autorun.inf") -Destination $OutputDir
[System.IO.File]::WriteAllText((Join-Path $OutputDir "autorun.config"), $configText, (New-Object System.Text.UTF8Encoding($false)))
Copy-Item -LiteralPath $installerPath -Destination $OutputDir
Copy-Item -LiteralPath $checksumPath -Destination $OutputDir
Copy-Item -LiteralPath $ArabicGuide -Destination (Join-Path $OutputDir "Docs\UserGuide.ar.pdf")
Copy-Item -LiteralPath $EnglishGuide -Destination (Join-Path $OutputDir "Docs\UserGuide.en.pdf")

Write-Host ""
Write-Host "Package ready: $OutputDir"
Get-ChildItem -LiteralPath $OutputDir -Recurse -File | ForEach-Object {
    "{0,12:N0}  {1}" -f $_.Length, $_.FullName.Substring($OutputDir.Length + 1)
}
