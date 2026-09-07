[CmdletBinding()]
param(
    [string]$Target = 'C:\Users\baago\Desktop\Reloaded-II\Mods\GBFR.PreEquippedSigils'
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$source = Join-Path $root 'dist\GBFR.PreEquippedSigils'

# 1. Verify the built package exists (run build-release.ps1 first).
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

# 3. Force-stop a running tool so the deployed files are not locked.
Get-Process -Name 'Loadout' -ErrorAction SilentlyContinue |
    Stop-Process -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

# 4. Replace the deployed folder.
$targetDir = Split-Path -Parent $Target
if (Test-Path -LiteralPath $Target) {
    Remove-Item -LiteralPath $Target -Recurse -Force
}
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
Copy-Item -Path $source -Destination $targetDir -Recurse -Force

# 5. Reopen the editor tool from the freshly deployed copy.
Start-Process -FilePath (Join-Path $Target 'Loadout.exe')

Write-Output "Deployed build to: $Target"
Write-Output "Deployed Loadout.exe is running."
