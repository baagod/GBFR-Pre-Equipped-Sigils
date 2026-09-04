$ErrorActionPreference = 'Stop'

# Generates traits.json (trait-name -> hash dictionary + recommended item gem)
# from docs/gbfr-sigil-hashes.zh-CN.tsv / .en.tsv. Output is consumed by both
# the managed mod (LoadoutConfig) and the loadout editor tool.
$root = Split-Path -Parent $PSScriptRoot
$zhPath = Join-Path $root 'docs\gbfr-sigil-hashes.zh-CN.tsv'
$enPath = Join-Path $root 'docs\gbfr-sigil-hashes.en.tsv'
$outPath = Join-Path $root 'GBFR.PreEquippedSigils\traits.json'

$zhT = @{}
$zhS = @()
$enT = @{}
foreach ($line in Get-Content $zhPath -Encoding UTF8) {
    if ($line -match "^T\t([0-9A-F]{8})\t(.+)$") { $zhT[$matches[1]] = $matches[2] }
    elseif ($line -match "^S\t([0-9A-F]{8})\t(.+)$") { $zhS += [pscustomobject]@{ Hash = $matches[1]; Name = $matches[2] } }
}
foreach ($line in Get-Content $enPath -Encoding UTF8) {
    if ($line -match "^T\t([0-9A-F]{8})\t(.+)$") { $enT[$matches[1]] = $matches[2] }
}

# Recommended item gem: prefer an S entry whose name contains the trait name
# and marks a high tier ("V+"); otherwise the first name match; 0 = fallback.
$entries = foreach ($pair in $zhT.GetEnumerator()) {
    $name = $pair.Value
    $gem = '0'
    $preferred = $zhS | Where-Object {
        $_.Name -match [regex]::Escape($name) -and ($_.Name -match 'V\+|Ⅴ')
    } | Select-Object -First 1
    if ($preferred) {
        $gem = $preferred.Hash
    } else {
        $any = $zhS | Where-Object { $_.Name -match [regex]::Escape($name) } | Select-Object -First 1
        if ($any) { $gem = $any.Hash }
    }
    [pscustomobject]@{
        nameZh = $name
        nameEn = if ($enT.ContainsKey($pair.Key)) { $enT[$pair.Key] } else { $name }
        hash   = $pair.Key
        gem    = $gem
    }
}

$duplicates = $entries | Group-Object nameZh | Where-Object Count -gt 1
if ($duplicates) {
    Write-Error "duplicate trait names: $($duplicates.Name -join ', ')"
    exit 1
}

$json = @{ traits = @($entries) } | ConvertTo-Json -Depth 3
[System.IO.File]::WriteAllText($outPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Output "wrote $($entries.Count) traits -> $outPath"
