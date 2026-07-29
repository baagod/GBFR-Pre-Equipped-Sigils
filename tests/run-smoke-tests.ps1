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

if (-not (Test-Path -LiteralPath $nativeDll -PathType Leaf)) {
    throw "Build the native $Configuration configuration first: $nativeDll"
}
if (-not (Test-Path -LiteralPath $managedDll -PathType Leaf)) {
    throw "Build the managed $Configuration configuration first: $managedDll"
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
