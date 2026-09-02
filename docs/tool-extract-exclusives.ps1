$ErrorActionPreference = 'Stop'
$comp = 'D:\Games\Relink\GBFR.PreEquippedSigils\GBFR.PreEquippedSigils.Native\GBFR-Pre-Equipped-Sigils.compatibility.tsv'
$names = 'D:\Games\Relink\GBFR.PreEquippedSigils\docs\gbfr-sigil-hashes.zh-CN.tsv'

$nameMap = @{}
$traitMap = @{}
foreach ($line in Get-Content $names) {
    $parts = $line -split "`t"
    if ($parts.Count -ge 3 -and $parts[1] -match '^[0-9A-Fa-f]{8}$') {
        $h = $parts[1].ToUpper()
        if ($parts[0] -eq 'S') { $nameMap[$h] = $parts[2] }
        elseif ($parts[0] -eq 'T') { $traitMap[$h] = $parts[2] }
    }
}

$byChar = @{}
foreach ($line in Get-Content $comp) {
    if ($line -match '^#' -or $line.Trim() -eq '') { continue }
    $p = $line -split "`t"
    $gem = $p[0].ToUpper()
    if (-not $byChar.ContainsKey($p[1])) { $byChar[$p[1]] = @() }
    $byChar[$p[1]] += $gem
}

foreach ($charHash in ($byChar.Keys | Sort-Object)) {
    $gems = $byChar[$charHash] | Sort-Object -Unique
    Write-Output "=== 角色 $charHash ==="
    foreach ($gem in $gems) {
        $sName = if ($nameMap.ContainsKey($gem)) { $nameMap[$gem] } else { '???' }
        # find trait hash with the same name
        $tHash = ($traitMap.GetEnumerator() | Where-Object { $_.Value -eq $sName } | Select-Object -First 1)
        $tStr = if ($tHash) { "T=$($tHash.Key)" } else { 'T=?' }
        Write-Output ("  {0}  S={1}  {2}" -f $sName, $gem, $tStr)
    }
}
