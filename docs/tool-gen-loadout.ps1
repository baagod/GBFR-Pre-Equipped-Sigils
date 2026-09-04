$ErrorActionPreference = 'Stop'

# Per-character exclusive data: awakening+ gem S hash, trait1 T, trait2 T, war-spirit T
# (trait1/trait2 are the awakening+'s two traits = the two non-war-spirit exclusives)
$chars = @(
    @{ Hash = '079DF0CC'; Awake = '98A6D249'; T1 = '151E4674'; T2 = 'A374FDF0'; War = 'D76F4D24' }, # Rackam
    @{ Hash = '0D21B430'; Awake = '4F01D6CA'; T1 = '6EBFA176'; T2 = 'F1D5DBD0'; War = '4F135217' }, # Zeta
    @{ Hash = '18E2F9F9'; Awake = '9ADA3E00'; T1 = '3BFED918'; T2 = 'F8496336'; War = '9AFDFA9E' }, # Katalina
    @{ Hash = '1BB37EF0'; Awake = '895ABBF6'; T1 = '26956F25'; T2 = '1DE14C65'; War = 'DBA19768' }, # Gallanza
    @{ Hash = '22E437E5'; Awake = 'E19B1965'; T1 = '8CDF9382'; T2 = 'D1012D8C'; War = '6316CBEB' }, # Lancelot
    @{ Hash = '25D46F4B'; Awake = 'D8A464F1'; T1 = '9ACE140B'; T2 = '7B5B081D'; War = '79266456' }, # Maglielle
    @{ Hash = '296471BE'; Awake = '6AAE4B8F'; T1 = '77C809F5'; T2 = '9230E3F5'; War = '7B4FC47A' }, # Seofon
    @{ Hash = '2A26B1B2'; Awake = '52A6E299'; T1 = 'CD030268'; T2 = 'A38510E2'; War = 'DADE14DC' }, # Gran (war = brave heart)
    @{ Hash = 'A4ACBA76'; Awake = '52A6E299'; T1 = 'CD030268'; T2 = 'A38510E2'; War = 'DADE14DC' }, # Djeeta (shares captain exclusives)
    @{ Hash = '2EBE91D5'; Awake = '673C5D8F'; T1 = '2E65A774'; T2 = '16EFF868'; War = 'D8F66C1C' }, # Vane
    @{ Hash = '4D0A60C3'; Awake = 'E2B380E5'; T1 = 'B48EEF48'; T2 = '11AAE5F5'; War = 'C00163B3' }, # Io
    @{ Hash = '627BCB0D'; Awake = 'AB835493'; T1 = '86CBCDC4'; T2 = '05FA4599'; War = 'C7D379F1' }, # Siegfried
    @{ Hash = '646C3168'; Awake = '5A360EA8'; T1 = '30773197'; T2 = '47384248'; War = '807B6684' }, # Fraux
    @{ Hash = '718E1A14'; Awake = 'B8C44D5E'; T1 = 'D40D1E9B'; T2 = '15806DFC'; War = '4E5F6706' }, # Sandalphon
    @{ Hash = '74DD4C79'; Awake = 'A8A0CBFF'; T1 = '06719232'; T2 = 'ED8D8AD8'; War = '5559232F' }, # Fediel
    @{ Hash = '978E4B18'; Awake = 'CE16D68B'; T1 = '5463232F'; T2 = '451D814C'; War = '0F026CF0' }, # Ghandagoza
    @{ Hash = '9A8AF295'; Awake = '95CC3CB8'; T1 = 'D176D262'; T2 = '461A8E07'; War = 'B953CC1E' }, # Beatrix
    @{ Hash = '9B15CFB1'; Awake = '23953FD4'; T1 = '7D75D904'; T2 = 'BE3404B9'; War = '3EB345D7' }, # Eustace
    @{ Hash = 'A3A3CB2F'; Awake = 'AF8E7E7E'; T1 = '93A2093C'; T2 = '7AD0C010'; War = 'B064A634' }, # Id (war = power of will)
    @{ Hash = 'AA66178A'; Awake = '02B1F8C0'; T1 = 'EC3CF174'; T2 = 'AF513A9D'; War = 'E6B92E34' }, # Cagliostro
    @{ Hash = 'BAD16E3B'; Awake = '8ECBB0A3'; T1 = 'E85FF8E0'; T2 = '8572B8AF'; War = '81B293D9' }, # Tweyen
    @{ Hash = 'BDEF7181'; Awake = '02472C43'; T1 = 'E60A735C'; T2 = '6FF05223'; War = 'BA504607' }, # Percival
    @{ Hash = 'C3FFD418'; Awake = 'B441275D'; T1 = 'D908223D'; T2 = '7351D602'; War = 'A339D642' }, # Ferry
    @{ Hash = 'C8616284'; Awake = '9BD1CC24'; T1 = '23D0F67F'; T2 = 'C2A4C7A9'; War = '8519AD4A' }, # Rosetta
    @{ Hash = 'DD7A151E'; Awake = '1BBE919C'; T1 = 'AA83F548'; T2 = '921B6B0C'; War = '0E42BE1B' }, # Eugen
    @{ Hash = 'E7053919'; Awake = '1A57AEF1'; T1 = '29B07BEB'; T2 = 'A63B89CD'; War = 'FDD1AD24' }, # Narmaya
    @{ Hash = 'F0EB77EF'; Awake = 'E4F986D9'; T1 = '7440E869'; T2 = 'CD124165'; War = 'D7F9BB88' }, # Vaseraga
    @{ Hash = 'FC6CDF7B'; Awake = '119B24A8'; T1 = '0CD6C625'; T2 = 'A3B49220'; War = 'DAEFBB27' }, # Yodarha
    @{ Hash = 'FD3BE362'; Awake = 'AEEF8343'; T1 = '9A9DC170'; T2 = '522E2388'; War = 'B85202BC' }  # Charlotta
)

# Common slots (all characters), slot indices 2..8 (general/editor-managed).
# slot2 = 激昂 V+ alone (single trait; moved out of the war-spirit slot).
# "no second trait" uses the not-selected sentinel 0x887AE0B0, level 0.
$common = @(
    @{ Gem = '04AC2281'; T1 = 'B5FF9FD3'; T2 = '887AE0B0'; LV = 15 },              # slot3: 激昂 V+（单独，只有主因子）
    @{ Gem = '335DA2A5'; T1 = 'E69A4694'; T2 = '95F3FA86'; LV = 15 },              # slot4: Guts V+ + Autorevive
    @{ Gem = 'B1CCC211'; T1 = 'B6E31F76'; T2 = 'D2C8E10A'; LV = 15 },              # slot5: Steadfast V+ + Perfect Dodge
    @{ Gem = '297D03F7'; T1 = '74AA75D6'; T2 = '24883AF3'; LV = 15 },              # slot6: Sturdy V+ + Potion Hoarder
    @{ Gem = '35637B96'; T1 = 'E0ABFDFE'; T2 = '8B3BF60C'; LV = 15 },              # slot7: Guardian V+ + Improved Dodging
    @{ Gem = '1E2EBC39'; T1 = '57AB5B10'; T2 = '318D12E9'; LV = 15 },              # slot8: Pursuit V+ + Swift Ability V+
    @{ Gem = '49434696'; T1 = 'BF78FBFC'; T2 = '887AE0B0'; LV = 20 }               # slot9: 钳蟹 Lv20（副 = 不选择）
)

# hash -> item gem lookup (war spirit signature sigils and trait names)
$root = Split-Path -Parent $PSScriptRoot
$traitsJson = Get-Content (Join-Path $root 'GBFR.PreEquippedSigils\traits.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$gemOf = @{}
foreach ($t in $traitsJson.traits) { $gemOf[$t.hash] = $t.gem }

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('constexpr CharacterTemplate kDefaultTemplates[] = {')
foreach ($c in $chars) {
    [void]$sb.AppendLine("   { 0x$($c.Hash), { // character")
    # slot 0: awakening+
    [void]$sb.AppendLine("      TemplateGemSlot{0x$($c.Awake), 0x$($c.T1), 15, 0x$($c.T2), 15, 15}, // slot1 awakening+ (2 exclusives)")
    # slot 1: war spirit alone (single trait; 副因子 = 不选择)
    $warGem = if ($gemOf[$c.War] -and $gemOf[$c.War] -ne '0') { $gemOf[$c.War] } else { $c.War }
    [void]$sb.AppendLine("      TemplateGemSlot{0x$warGem, 0x$($c.War), 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)")
    # slots 2..8: general (editor-managed) loadout
    $slotNum = 3
    foreach ($s in $common) {
        $lv1 = [int]$s.LV
        $lv2 = if ($s.T2 -eq '887AE0B0') { 0 } else { 15 }
        [void]$sb.AppendLine("      TemplateGemSlot{0x$($s.Gem), 0x$($s.T1), $lv1, 0x$($s.T2), $lv2, $lv1}, // slot$slotNum general")
        $slotNum++
    }
    [void]$sb.AppendLine('   }},')
}
[void]$sb.AppendLine('};')

$out = $sb.ToString()
Set-Content -Path "$env:TEMP\loadout_table.txt" -Value $out -Encoding UTF8
# Slot count for native_internal.h kTemplateSlotCount (= 2 exclusives + general)
$slotCount = 2 + $common.Count
Write-Output "generated: $($chars.Count) characters, $slotCount slots, $($out.Length) chars -> $env:TEMP\loadout_table.txt"
Write-Output "SLOT_COUNT=$slotCount (sync native_internal.h kTemplateSlotCount if changed)"

# --- pre-loadout.json: captain (Gran) template as an editable starting
# point for the loadout editor (trait-level; same shape as loadout.json). ---
$hashNames = @{}
foreach ($line in Get-Content (Join-Path $root 'docs\gbfr-sigil-hashes.zh-CN.tsv') -Encoding UTF8) {
    if ($line -match "^T\t([0-9A-F]{8})\t(.+)$") { $hashNames[$matches[1]] = $matches[2] }
}
# The editor only manages GENERAL slots (raw 0/1 = character exclusives stay
# mod-injected): the default list skips awakening+ and war spirit.
$builtinSlots = foreach ($s in $common) {
    $lv1 = [int]$s.LV
    $lv2 = if ($s.T2 -eq '887AE0B0') { 0 } else { 15 }
    @{
        trait1  = $hashNames[$s.T1]
        level1  = $lv1
        trait2  = if ($s.T2 -eq '887AE0B0') { '' } else { $hashNames[$s.T2] }
        level2  = $lv2
        enabled = $true
    }
}
$builtinOut = Join-Path $root 'GBFR.PreEquippedSigils\pre-loadout.json'
[System.IO.File]::WriteAllText(
    $builtinOut,
    (@{ slots = @($builtinSlots) } | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false))
Write-Output "wrote pre-loadout.json -> $builtinOut"
