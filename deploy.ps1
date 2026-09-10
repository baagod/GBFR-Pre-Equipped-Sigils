[CmdletBinding()]
param(
    [string]$Target = 'C:\Users\baago\Desktop\Reloaded-II\Mods\GBFR.PreEquippedSigils'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$source = Join-Path $root 'dist\GBFR.PreEquippedSigils'

# 0. Refuse a target that is not the mod folder: the replacement below is a
# recursive delete, so a mistyped -Target must never hit an unrelated path.
$resolvedTarget = [IO.Path]::GetFullPath($Target).TrimEnd('\')
if (-not $resolvedTarget.EndsWith('\GBFR.PreEquippedSigils', [StringComparison]::OrdinalIgnoreCase)) {
    throw "Refusing to deploy to a path that is not the mod folder: $Target"
}

# 1. Verify the built package exists (run build-release.ps1 first).
# Keep in sync with the build-release.ps1 required-file list.
foreach ($required in @(
    'GBFR.PreEquippedSigils.dll',
    'GBFR.PreEquippedSigils.Native.dll',
    'Loadout.exe',
    'sigils.json',
    'character-exclusives.json'
)) {
    $requiredPath = Join-Path $source $required
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Built package is incomplete: $source (missing $required). Run build-release.ps1 first."
    }
}

# 2. The game must be closed: its mod DLLs are loaded from the Mods folder.
if (Get-Process -Name 'granblue_fantasy_relink' -ErrorAction SilentlyContinue) {
    throw 'The game is running; close it first (its Reloaded-II mods are loaded from the Mods folder).'
}

# 3. Stop a running tool so the deployed files are not locked, and wait until it
# is really gone: the tool holds a single-instance mutex, so a launch that races
# the shutdown would only activate the dying window and then exit by itself.
function Stop-LoadoutTool {
    Get-Process -Name 'Loadout' -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
    $deadline = (Get-Date).AddSeconds(15)
    while ((Get-Process -Name 'Loadout' -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
    }
}

# Launch from the deployed copy and report whether it survived the start.
function Start-LoadoutTool {
    Start-Process -FilePath (Join-Path $Target 'Loadout.exe')
    Start-Sleep -Seconds 3
    return [bool](Get-Process -Name 'Loadout' -ErrorAction SilentlyContinue)
}

Stop-LoadoutTool

# 4. Replace the deployed folder.
$targetDir = Split-Path -Parent $Target
if (Test-Path -LiteralPath $Target) {
    Remove-Item -LiteralPath $Target -Recurse -Force
}
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
Copy-Item -Path $source -Destination $targetDir -Recurse -Force

# 5. Reopen the editor tool from the freshly deployed copy. If that first start
# did not survive (a lingering instance still held the mutex), stop everything
# and start once more.
if (-not (Start-LoadoutTool)) {
    Stop-LoadoutTool
    if (-not (Start-LoadoutTool)) {
        throw 'Loadout.exe did not stay running after deploy.'
    }
}

$toolPid = (Get-Process -Name 'Loadout' | Select-Object -First 1).Id
Write-Output "Deployed build to: $Target"
Write-Output "Deployed Loadout.exe is running (PID $toolPid)."
