[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$nativeOutput = Join-Path $root "GBFR.ExtraSigilSlots.Native\bin\$Configuration"
$managedOutput = Join-Path $root "GBFR.ExtraSigilSlots.Reloaded\bin\$Configuration"
$nativeDll = Join-Path $nativeOutput 'GBFR.ExtraSigilSlots.Native.dll'
$managedDll = Join-Path $managedOutput 'GBFR.ExtraSigilSlots.Reloaded.dll'
$layoutHarnessProject = Join-Path $PSScriptRoot 'NativeLayoutHarness\NativeLayoutHarness.vcxproj'
$layoutHarness = Join-Path $PSScriptRoot 'NativeLayoutHarness\bin\Release\NativeLayoutHarness.exe'

if (-not (Test-Path -LiteralPath $nativeDll -PathType Leaf)) {
    throw "Build the native $Configuration configuration first: $nativeDll"
}
if (-not (Test-Path -LiteralPath $managedDll -PathType Leaf)) {
    throw "Build the managed $Configuration configuration first: $managedDll"
}

function Resolve-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $resolved = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' |
            Select-Object -First 1
        if ($resolved) { return $resolved }
    }
    throw 'MSBuild was not found for NativeLayoutHarness.'
}

function Invoke-Harness {
    param(
        [Parameter(Mandatory = $true)][string]$Project,
        [Parameter(Mandatory = $true)][string]$OutputDirectory
    )
    & dotnet run --project $Project --configuration Release -- $OutputDirectory
    if ($LASTEXITCODE -ne 0) {
        throw "Smoke harness failed with exit code $LASTEXITCODE`: $Project"
    }
}

$game204 = $env:GBFR_EXE_204
$game205 = $env:GBFR_EXE_205
if ($game204 -or $game205) {
    if (-not (Test-Path -LiteralPath $game204 -PathType Leaf) -or
        -not (Test-Path -LiteralPath $game205 -PathType Leaf)) {
        throw 'Set both GBFR_EXE_204 and GBFR_EXE_205 to run native layout compatibility tests.'
    }
    $msbuild = Resolve-MSBuild
    & $msbuild $layoutHarnessProject /t:Build /p:Configuration=Release /p:Platform=x64 /m /v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Native layout harness build failed with exit code $LASTEXITCODE." }
    & $layoutHarness $game204 $game205
    if ($LASTEXITCODE -ne 0) { throw "Native layout harness failed with exit code $LASTEXITCODE." }
}
else {
    Write-Output 'NATIVE_LAYOUT_COMPATIBILITY=SKIP (set GBFR_EXE_204 and GBFR_EXE_205)'
}

Invoke-Harness `
    -Project (Join-Path $PSScriptRoot 'SlotConfigHarness\SlotConfigHarness.csproj') `
    -OutputDirectory $nativeOutput
Invoke-Harness `
    -Project (Join-Path $PSScriptRoot 'PresentBridgeHarness\PresentBridgeHarness.csproj') `
    -OutputDirectory $nativeOutput
Invoke-Harness `
    -Project (Join-Path $PSScriptRoot 'PresetStoreHarness\PresetStoreHarness.csproj') `
    -OutputDirectory $managedOutput
Invoke-Harness `
    -Project (Join-Path $PSScriptRoot 'InputPassThroughHarness\InputPassThroughHarness.csproj') `
    -OutputDirectory $managedOutput
Invoke-Harness `
    -Project (Join-Path $PSScriptRoot 'FrontendGateHarness\FrontendGateHarness.csproj') `
    -OutputDirectory $managedOutput
Invoke-Harness `
    -Project (Join-Path $PSScriptRoot 'HotkeyConfigHarness\HotkeyConfigHarness.csproj') `
    -OutputDirectory $managedOutput
Invoke-Harness `
    -Project (Join-Path $PSScriptRoot 'StartupDiagnosticsHarness\StartupDiagnosticsHarness.csproj') `
    -OutputDirectory $managedOutput
Invoke-Harness `
    -Project (Join-Path $PSScriptRoot 'OverlayHubContractHarness\OverlayHubContractHarness.csproj') `
    -OutputDirectory $managedOutput
Invoke-Harness `
    -Project (Join-Path $PSScriptRoot 'OverlayBrokerRecoveryHarness\OverlayBrokerRecoveryHarness.csproj') `
    -OutputDirectory $managedOutput
Invoke-Harness `
    -Project (Join-Path $PSScriptRoot 'HostedImguiBindingHarness\HostedImguiBindingHarness.csproj') `
    -OutputDirectory $managedOutput

Write-Output 'ALL_SMOKE_TESTS=PASS'
