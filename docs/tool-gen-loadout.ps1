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
    @{ Hash = '2A26B1B2'; Awake = '52A6E299'; T1 = 'CD030268'; T2 = 'A38510E2'; War = 'DADE14DC' }, # Gran (war slot = brave heart)
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
    @{ Hash = 'A3A3CB2F'; Awake = 'AF8E7E7E'; T1 = '93A2093C'; T2 = '7AD0C010'; War = 'B064A634' }, # Id (war slot = power of will)
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

# Common slots (all characters)
$common = @(
    @{ Gem = '04AC2281'; T1 = 'B5FF9FD3'; T2 = $null },   # slot 2: Inspire V+ + war spirit (war T filled per char)
    @{ Gem = '335DA2A5'; T1 = 'E69A4694'; T2 = '95F3FA86' }, # slot 3: Guts V+ + Autorevive
    @{ Gem = 'B1CCC211'; T1 = 'B6E31F76'; T2 = '8B3BF60C' }, # slot 4: Steadfast V+ + Improved Dodging
    @{ Gem = '297D03F7'; T1 = '74AA75D6'; T2 = '24883AF3' }  # slot 5: 刚健Ⅴ＋ + 药水携带数 (Sturdy V+ + Potion Hoarder)
)

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('constexpr CharacterTemplate kDefaultTemplates[] = {')
foreach ($c in $chars) {
    [void]$sb.AppendLine('   {')
    [void]$sb.AppendLine("      0x$($c.Hash), // character")
    [void]$sb.AppendLine('      {')
    # slot 1: awakening+
    [void]$sb.AppendLine('         TemplateGemSlot{')
    [void]$sb.AppendLine("            0x$($c.Awake), // gem_id: awakening+")
    [void]$sb.AppendLine("            0x$($c.T1), // trait1")
    [void]$sb.AppendLine('            15,')
    [void]$sb.AppendLine("            0x$($c.T2), // trait2")
    [void]$sb.AppendLine('            15,')
    [void]$sb.AppendLine('            15,')
    [void]$sb.AppendLine('         },')
    # slot 2: inspire + war spirit
    [void]$sb.AppendLine('         TemplateGemSlot{')
    [void]$sb.AppendLine("            0x$($common[0].Gem), // gem_id: Inspire V+")
    [void]$sb.AppendLine("            0x$($common[0].T1), // trait1: Inspire")
    [void]$sb.AppendLine('            15,')
    [void]$sb.AppendLine("            0x$($c.War), // trait2: war spirit")
    [void]$sb.AppendLine('            15,')
    [void]$sb.AppendLine('            15,')
    [void]$sb.AppendLine('         },')
    # slots 3-5
    foreach ($s in $common[1..3]) {
        [void]$sb.AppendLine('         TemplateGemSlot{')
        [void]$sb.AppendLine("            0x$($s.Gem),")
        [void]$sb.AppendLine("            0x$($s.T1),")
        [void]$sb.AppendLine('            15,')
        [void]$sb.AppendLine("            0x$($s.T2),")
        [void]$sb.AppendLine('            15,')
        [void]$sb.AppendLine('            15,')
        [void]$sb.AppendLine('         },')
    }
    [void]$sb.AppendLine('      },')
    [void]$sb.AppendLine('   },')
}
[void]$sb.AppendLine('};')

$out = $sb.ToString()
Set-Content -Path "$env:TEMP\loadout_table.txt" -Value $out -Encoding UTF8
Write-Output "generated: $($chars.Count) characters, $($out.Length) chars -> $env:TEMP\loadout_table.txt"
