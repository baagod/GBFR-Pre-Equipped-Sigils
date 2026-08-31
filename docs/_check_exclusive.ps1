$ErrorActionPreference = 'Stop'
$comp = 'D:\Games\Relink\GBFR.ReloadedSigilSlots\GBFR.ReloadedSigilSlots.Native\GBFR-ReloadedSigilSlots.compatibility.tsv'
$names = 'D:\Games\Relink\GBFR.ReloadedSigilSlots\docs\gbfr-sigil-hashes.zh-CN.tsv'

$nameMap = @{}
foreach ($line in Get-Content $names) {
    $parts = $line -split "`t"
    if ($parts.Count -ge 3 -and $parts[1] -match '^[0-9A-Fa-f]{8}$') {
        $nameMap[$parts[1].ToUpper()] = $parts[2]
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
    $names2 = @()
    foreach ($gem in $gems) {
        $n = if ($nameMap.ContainsKey($gem)) { $nameMap[$gem] } else { "???($gem)" }
        $names2 += $n
    }
    # classify: base (no + suffix) vs plus
    $base = @($names2 | Where-Object { $_ -notmatch '＋$' })
    $plus = @($names2 | Where-Object { $_ -match '＋$' })
    Write-Output ("=== {0}  共 {1} 条 ===" -f $charHash, $names2.Count)
    Write-Output ("  基础({0}): {1}" -f $base.Count, ($base -join ' / '))
    Write-Output ("  ＋版({0}): {1}" -f $plus.Count, ($plus -join ' / '))
}
