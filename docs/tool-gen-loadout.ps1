$ErrorActionPreference = 'Stop'

# Per-character exclusive data: awakening+ gem S hash, trait1 T, trait2 T, war-spirit gem,
# war-spirit trait (WarGem is the character's war-spirit sigil gem; a character may own
# several war-spirit variants - the value below is the one the built-in template uses)
$chars = @(
    @{ Hash = '079DF0CC'; Awake = '98A6D249'; T1 = '151E4674'; T2 = 'A374FDF0'; War = 'D76F4D24'; WarGem = '1791546F' },
    @{ Hash = '0D21B430'; Awake = '4F01D6CA'; T1 = '6EBFA176'; T2 = 'F1D5DBD0'; War = '4F135217'; WarGem = '6674C639' },
    @{ Hash = '18E2F9F9'; Awake = '9ADA3E00'; T1 = '3BFED918'; T2 = 'F8496336'; War = '9AFDFA9E'; WarGem = '621A2A97' },
    @{ Hash = '1BB37EF0'; Awake = '895ABBF6'; T1 = '26956F25'; T2 = '1DE14C65'; War = 'DBA19768'; WarGem = '41AC1082' },
    @{ Hash = '22E437E5'; Awake = 'E19B1965'; T1 = '8CDF9382'; T2 = 'D1012D8C'; War = '6316CBEB'; WarGem = '8A3819C0' },
    @{ Hash = '25D46F4B'; Awake = 'D8A464F1'; T1 = '9ACE140B'; T2 = '7B5B081D'; War = '79266456'; WarGem = '6B920DA2' },
    @{ Hash = '296471BE'; Awake = '6AAE4B8F'; T1 = '77C809F5'; T2 = '9230E3F5'; War = '7B4FC47A'; WarGem = '67C1E5E3' },
    @{ Hash = '2A26B1B2'; Awake = '52A6E299'; T1 = 'CD030268'; T2 = 'A38510E2'; War = 'DADE14DC'; WarGem = '0713D928' },
    @{ Hash = 'A4ACBA76'; Awake = '52A6E299'; T1 = 'CD030268'; T2 = 'A38510E2'; War = 'DADE14DC'; WarGem = '0713D928' },
    @{ Hash = '2EBE91D5'; Awake = '673C5D8F'; T1 = '2E65A774'; T2 = '16EFF868'; War = 'D8F66C1C'; WarGem = '3D8D9109' },
    @{ Hash = '4D0A60C3'; Awake = 'E2B380E5'; T1 = 'B48EEF48'; T2 = '11AAE5F5'; War = 'C00163B3'; WarGem = '43F26A91' },
    @{ Hash = '627BCB0D'; Awake = 'AB835493'; T1 = '86CBCDC4'; T2 = '05FA4599'; War = 'C7D379F1'; WarGem = '450B862C' },
    @{ Hash = '646C3168'; Awake = '5A360EA8'; T1 = '30773197'; T2 = '47384248'; War = '807B6684'; WarGem = '2D70C37D' },
    @{ Hash = '718E1A14'; Awake = 'B8C44D5E'; T1 = 'D40D1E9B'; T2 = '15806DFC'; War = '4E5F6706'; WarGem = '5D592FDD' },
    @{ Hash = '74DD4C79'; Awake = 'A8A0CBFF'; T1 = '06719232'; T2 = 'ED8D8AD8'; War = '5559232F'; WarGem = '4119F09B' },
    @{ Hash = '978E4B18'; Awake = 'CE16D68B'; T1 = '5463232F'; T2 = '451D814C'; War = '0F026CF0'; WarGem = '3069C2FE' },
    @{ Hash = '9A8AF295'; Awake = '95CC3CB8'; T1 = 'D176D262'; T2 = '461A8E07'; War = 'B953CC1E'; WarGem = '51E98A7C' },
    @{ Hash = '9B15CFB1'; Awake = '23953FD4'; T1 = '7D75D904'; T2 = 'BE3404B9'; War = '3EB345D7'; WarGem = '61A1A299' },
    @{ Hash = 'A3A3CB2F'; Awake = 'AF8E7E7E'; T1 = '93A2093C'; T2 = '7AD0C010'; War = 'B064A634'; WarGem = '98E9E6EF' },
    @{ Hash = 'AA66178A'; Awake = '02B1F8C0'; T1 = 'EC3CF174'; T2 = 'AF513A9D'; War = 'E6B92E34'; WarGem = '5FB842E4' },
    @{ Hash = 'BAD16E3B'; Awake = '8ECBB0A3'; T1 = 'E85FF8E0'; T2 = '8572B8AF'; War = '81B293D9'; WarGem = 'AD8CAEFB' },
    @{ Hash = 'BDEF7181'; Awake = '02472C43'; T1 = 'E60A735C'; T2 = '6FF05223'; War = 'BA504607'; WarGem = '4CDCE25B' },
    @{ Hash = 'C3FFD418'; Awake = 'B441275D'; T1 = 'D908223D'; T2 = '7351D602'; War = 'A339D642'; WarGem = '40738CDD' },
    @{ Hash = 'C8616284'; Awake = '9BD1CC24'; T1 = '23D0F67F'; T2 = 'C2A4C7A9'; War = '8519AD4A'; WarGem = '515E693C' },
    @{ Hash = 'DD7A151E'; Awake = '1BBE919C'; T1 = 'AA83F548'; T2 = '921B6B0C'; War = '0E42BE1B'; WarGem = '7C7EC053' },
    @{ Hash = 'E7053919'; Awake = '1A57AEF1'; T1 = '29B07BEB'; T2 = 'A63B89CD'; War = 'FDD1AD24'; WarGem = 'CEF31894' },
    @{ Hash = 'F0EB77EF'; Awake = 'E4F986D9'; T1 = '7440E869'; T2 = 'CD124165'; War = 'D7F9BB88'; WarGem = '590C19C8' },
    @{ Hash = 'FC6CDF7B'; Awake = '119B24A8'; T1 = '0CD6C625'; T2 = 'A3B49220'; War = 'DAEFBB27'; WarGem = '76D4716B' },
    @{ Hash = 'FD3BE362'; Awake = 'AEEF8343'; T1 = '9A9DC170'; T2 = '522E2388'; War = 'B85202BC'; WarGem = '34C77091' }
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

# Emits the per-character arrays consumed by template_loadout.cpp; the
# CharacterExclusiveLoadout struct and BuildCharacterTemplate live in that file.
$root = Split-Path -Parent $PSScriptRoot
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('constexpr CharacterExclusiveLoadout kCharacterExclusives[] = {')
foreach ($c in $chars) {
    [void]$sb.AppendLine("   { 0x$($c.Hash), // character")
    [void]$sb.AppendLine("      TemplateGemSlot{0x$($c.Awake), 0x$($c.T1), 15, 0x$($c.T2), 15, 15}, // slot0 awakening+ (2 exclusives)")
    [void]$sb.AppendLine("      TemplateGemSlot{0x$($c.WarGem), 0x$($c.War), 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single trait)")
    [void]$sb.AppendLine('   },')
}
[void]$sb.AppendLine('};')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('// General slots 3-9 (identical for every character):')
[void]$sb.AppendLine('constexpr std::array<TemplateGemSlot, 7> kGeneralSlots = {')
$generalNames = @(
    '激昂V＋ (single)',
    '豪胆V＋',
    '不动V＋',
    '刚健V＋',
    '守护V＋',
    '追击V＋',
    '钳蟹 Lv20'
)
$generalIndex = 0
foreach ($s in $common) {
    $lv1 = [int]$s.LV
    $lv2 = if ($s.T2 -eq '887AE0B0') { 0 } else { 15 }
    $comma = if ($generalIndex -lt $common.Count - 1) { ',' } else { '' }
    $slotLine = "   TemplateGemSlot{0x$($s.Gem), 0x$($s.T1), $lv1, 0x$($s.T2), $lv2, $lv1}$comma // slot$($generalIndex + 3) $($generalNames[$generalIndex])"
    [void]$sb.AppendLine($slotLine)
    $generalIndex++
}
[void]$sb.AppendLine('};')

$out = $sb.ToString()
Set-Content -Path "$env:TEMP\loadout_table.txt" -Value $out -Encoding UTF8
# Slot count for native_internal.h kTemplateSlotCount (= 2 exclusives + general)
$slotCount = 2 + $common.Count
Write-Output "generated: $($chars.Count) characters, $slotCount slots, $($out.Length) chars -> $env:TEMP\loadout_table.txt"
Write-Output "SLOT_COUNT=$slotCount (sync native_internal.h kTemplateSlotCount if changed)"

# --- pre-loadout.json: captain (Gran) template as an editable starting
# point for the loadout editor. items[] shape only (gem/level); zh/en names
# are re-resolved client-side from sigils.json and are not needed on load. ---
# The editor only manages GENERAL slots (slots 0/1 = character exclusives stay
# mod-injected): the default list skips awakening+ and war spirit.
$builtinSlots = foreach ($s in $common) {
    $lv1 = [int]$s.LV
    $lv2 = if ($s.T2 -eq '887AE0B0') { 0 } else { 15 }
    $items = @(
        @{ gem = $s.Gem; level = $lv1; zh = ''; en = '' }
    )
    if ($s.T2 -ne '887AE0B0') {
        $items += @{ hash = $s.T2; level = $lv2; zh = ''; en = '' }
    }
    @{ items = $items; enabled = $true }
}
$builtinOut = Join-Path $root 'GBFR.PreEquippedSigils\pre-loadout.json'
[System.IO.File]::WriteAllText(
    $builtinOut,
    (@{ slots = @($builtinSlots) } | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false))
Write-Output "wrote pre-loadout.json -> $builtinOut"
