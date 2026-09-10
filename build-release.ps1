[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    [ValidateSet('x64')]
    [string]$Platform = 'x64',
    [ValidatePattern('^[0-9A-Za-z][0-9A-Za-z._-]*$')]
    [string]$Version = '0.5.7'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$nativeProject = Join-Path $root 'GBFR.PreEquippedSigils.Native\GBFR.PreEquippedSigils.Native.vcxproj'
$managedProject = Join-Path $root 'GBFR.PreEquippedSigils\GBFR.PreEquippedSigils.csproj'
$managedOutput = Join-Path $root "GBFR.PreEquippedSigils\bin\$Configuration"
$distRoot = Join-Path $root 'dist'
$packageDir = Join-Path $distRoot 'GBFR.PreEquippedSigils'
$zipPath = Join-Path $distRoot "GBFR-Pre-Equipped-Sigils-$Version.zip"

# --- release consistency gates ------------------------------------------------
# Native contract: the character-restriction loader fails closed unless
# sigils.json carries exactly kExpectedCharacterRestrictionCount "character"
# rows. Check it here so a data regeneration cannot silently break startup.
$sigilsPath = Join-Path $root 'GBFR.PreEquippedSigils\sigils.json'
$nativeInternalPath = Join-Path $root 'GBFR.PreEquippedSigils.Native\native_internal.h'
if (-not (Test-Path -LiteralPath $sigilsPath)) {
    throw "sigils.json is missing: $sigilsPath"
}
$expectedMatch = [regex]::Match(
    (Get-Content -LiteralPath $nativeInternalPath -Raw),
    'kExpectedCharacterRestrictionCount\s*=\s*(\d+)')
if (-not $expectedMatch.Success) {
    throw 'kExpectedCharacterRestrictionCount was not found in native_internal.h.'
}
$expectedMappings = [int]$expectedMatch.Groups[1].Value
$characterRows = ([regex]::Matches(
    (Get-Content -LiteralPath $sigilsPath -Raw), '"character"\s*:')).Count
if ($characterRows -ne $expectedMappings) {
    throw "sigils.json has $characterRows 'character' rows; the native loader expects $expectedMappings."
}
Write-Output "sigils.json character rows: $characterRows (native loader expects $expectedMappings)."

# The release manifest version must match the packaged version.
$manifestVersion = (Get-Content -LiteralPath (Join-Path $root 'GBFR.PreEquippedSigils\ModConfig.json') -Raw |
    ConvertFrom-Json).ModVersion
if ($manifestVersion -ne $Version) {
    throw "Version mismatch: -Version $Version but ModConfig.json declares $manifestVersion."
}
Write-Output "ModConfig.json version: $manifestVersion."

# --- data freshness gates -----------------------------------------------------
# 生成物必须与数据源一致；不一致 = 忘了跑生成器（构建不自动生成，避免每次重建数据源）。
#   sigils.json          <- gen\extracted\sigils.xlsx（gen\make-sigils-json.js）
#   character-exclusives.json + kCharacterExclusives[]  <- docs\tool-gen-loadout.ps1 的 $chars
$sigilsXlsx = Join-Path $root 'gen\extracted\sigils.xlsx'
$makeSigilsJson = Join-Path $root 'gen\make-sigils-json.js'
if (Test-Path -LiteralPath $sigilsXlsx) {
    & node $makeSigilsJson $sigilsXlsx $sigilsPath --check
    if ($LASTEXITCODE -ne 0) {
        throw 'sigils.json 与 gen\extracted\sigils.xlsx 不一致：先跑 node gen\make-sigils-json.js gen\extracted\sigils.xlsx GBFR.PreEquippedSigils\sigils.json'
    }
} else {
    Write-Output 'gen\extracted\sigils.xlsx not found; skipped the sigils.json freshness check.'
}
& pwsh -NoProfile -File (Join-Path $root 'docs\tool-gen-loadout.ps1') -Check
if ($LASTEXITCODE -ne 0) {
    throw 'character-exclusives.json / kCharacterExclusives[] 与 $chars 不一致：先跑 pwsh docs\tool-gen-loadout.ps1'
}

$msbuild = $null
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (Test-Path -LiteralPath $vswhere) {
    $msbuild = & $vswhere `
        -latest `
        -products '*' `
        -requires Microsoft.Component.MSBuild `
        -find 'MSBuild\**\Bin\MSBuild.exe' |
        Select-Object -First 1
}

if (-not $msbuild) {
    $fallbacks = @(
        'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe',
        'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    )
    $msbuild = $fallbacks | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
}

if (-not $msbuild) {
    throw 'MSBuild was not found. Install Visual Studio 2022 Build Tools with the C++ workload.'
}

& $msbuild $nativeProject `
    /t:Rebuild `
    /p:Configuration=$Configuration `
    /p:Platform=$Platform `
    /m `
    /v:minimal
if ($LASTEXITCODE -ne 0) {
    throw "Native build failed with exit code $LASTEXITCODE."
}

# NuGetAudit=false keeps offline builds green; check vulnerabilities with a
# one-off `dotnet list package --vulnerable` when the environment allows it.
& dotnet restore $managedProject `
    --ignore-failed-sources `
    --nologo `
    -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) {
    throw "Managed restore failed with exit code $LASTEXITCODE."
}

& dotnet clean $managedProject -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Managed clean failed with exit code $LASTEXITCODE."
}

& dotnet build $managedProject -c $Configuration --nologo --no-incremental --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "Managed build failed with exit code $LASTEXITCODE."
}

# Loadout editor tool: Wails v3 build (GUI subsystem, embedded frontend dist).
$toolDir = Join-Path $root 'Loadout'
Push-Location $toolDir
try {
    # Bindings are git-ignored generated output; regenerate before the frontend build.
    & wails3 generate bindings
    if ($LASTEXITCODE -ne 0) {
        throw "Wails bindings generation failed with exit code $LASTEXITCODE."
    }
    & npm --prefix (Join-Path $toolDir 'frontend') run build
    if ($LASTEXITCODE -ne 0) {
        throw "Tool frontend build failed with exit code $LASTEXITCODE."
    }
    & go build -trimpath -ldflags "-H windowsgui -s -w" -o Loadout.exe .
    if ($LASTEXITCODE -ne 0) {
        throw "Tool build failed with exit code $LASTEXITCODE."
    }
} finally {
    Pop-Location
}

# Keep the tool's dev-run data copies (git-ignored, next to the Go sources)
# identical to the packaged ones, so a Loadout.exe run from Loadout/ can never
# silently diverge from a release.
foreach ($dataFile in @('sigils.json', 'character-exclusives.json')) {
    Copy-Item -LiteralPath (Join-Path $root "GBFR.PreEquippedSigils\$dataFile") `
        -Destination (Join-Path $toolDir $dataFile) -Force
}
Write-Output 'Synced sigils.json/character-exclusives.json into Loadout/.'

$resolvedRoot = [IO.Path]::GetFullPath($root).TrimEnd('\') + '\'
$resolvedDist = [IO.Path]::GetFullPath($distRoot).TrimEnd('\') + '\'
if (-not $resolvedDist.StartsWith($resolvedRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean a dist path outside the repository: $distRoot"
}

$resolvedPackage = [IO.Path]::GetFullPath($packageDir).TrimEnd('\') + '\'
if (-not $resolvedPackage.StartsWith($resolvedDist, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to clean a package path outside dist: $packageDir"
}

# Force-stop a running editor tool: it locks dist\GBFR.PreEquippedSigils\Loadout.exe
# and would make the recursive dist cleanup below fail. The tool is reopened at
# the end of this script.
if ((Get-Process -Name 'Loadout' -ErrorAction SilentlyContinue) -ne $null) {
    Get-Process -Name 'Loadout' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    Write-Output 'Stopped the running Loadout.exe so dist can be replaced.'
}

New-Item -ItemType Directory -Path $distRoot -Force | Out-Null
if (Test-Path -LiteralPath $packageDir) {
    Remove-Item -LiteralPath $packageDir -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}
New-Item -ItemType Directory -Path $packageDir | Out-Null
Copy-Item -Path (Join-Path $managedOutput '*') -Destination $packageDir -Recurse -Force

$toolExe = Join-Path $toolDir 'Loadout.exe'
if (-not (Test-Path -LiteralPath $toolExe -PathType Leaf)) {
    throw "Loadout tool exe was not built: $toolExe"
}
Copy-Item -Path $toolExe -Destination $packageDir -Force

# Required release files — keep in sync with the deploy.ps1 completeness list
# (sigils.json lands here via the csproj CopyToOutputDirectory).
foreach ($requiredFile in @(
    'GBFR.PreEquippedSigils.dll',
    'GBFR.PreEquippedSigils.Native.dll',
    'Loadout.exe',
    'sigils.json',
    'character-exclusives.json'
)) {
    $requiredPath = Join-Path $packageDir $requiredFile
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required release file was not packaged: $requiredPath"
    }
}

# The managed PDB must never ship. Mutable config files are not deleted here:
# the guard below treats a packaged one as an error (fail closed).
$pdbPath = Join-Path $packageDir 'GBFR.PreEquippedSigils.pdb'
if (Test-Path -LiteralPath $pdbPath) {
    Remove-Item -LiteralPath $pdbPath -Force
}

$runtimesPath = Join-Path $packageDir 'runtimes'
if (Test-Path -LiteralPath $runtimesPath) {
    Get-ChildItem -LiteralPath $runtimesPath -Directory |
        Where-Object { $_.Name -ne 'win-x64' } |
        Remove-Item -Recurse -Force
}

$legacyArtifact = Get-ChildItem -LiteralPath $packageDir -Recurse -File |
    Where-Object {
        $_.Name -like 'GBFR.ExtraSigilSlots*' -or
        $_.Name -like '*ExtraSigilSlots20*'
    } |
    Select-Object -First 1
if ($legacyArtifact) {
    throw "Legacy ExtraSigilSlots artifact was packaged: $($legacyArtifact.FullName)"
}

$packagedConfig = Get-ChildItem -LiteralPath $packageDir -Recurse -File |
    Where-Object {
        $_.Name -ieq 'GBFR.PreEquippedSigilsConfig.ini' -or
        $_.Name -ieq 'GBFR.PreEquippedSigilsConfig.pending'
    } |
    Select-Object -First 1
if ($packagedConfig) {
    throw "Mutable config state must be runtime-created and was packaged unexpectedly: $($packagedConfig.FullName)"
}

Compress-Archive -LiteralPath $packageDir -DestinationPath $zipPath -CompressionLevel Optimal

Write-Output "Reloaded-II package: $packageDir"
Write-Output "ZIP: $zipPath"

# Dev convenience: open the editor tool for immediate review. If an older tool
# instance is already running, its single-instance mutex activates that window
# (a fresh start was attempted after each rebuild anyway).
Start-Process -FilePath (Join-Path $packageDir 'Loadout.exe')
