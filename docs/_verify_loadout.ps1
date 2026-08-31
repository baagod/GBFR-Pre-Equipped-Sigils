$ErrorActionPreference = 'Stop'

# Official tables (downloaded earlier)
$gemHtml = Get-Content "$env:TEMP\gem_ids.html" -Raw -Encoding UTF8
$traitHtml = Get-Content "$env:TEMP\trait_ids.html" -Raw -Encoding UTF8

$gemRows = [regex]::Matches($gemHtml, '<tr>\s*<td[^>]*>(GEEN_\d+_\d+)</td>\s*<td[^>]*>([^<]*)</td>\s*<td[^>]*>([0-9A-F]{8})</td>\s*</tr>')
$traitRows = [regex]::Matches($traitHtml, '<tr>\s*<td[^>]*>([^<]*)</td>\s*<td[^>]*>([^<]*)</td>\s*<td[^>]*>([0-9A-F]{8})</td>\s*</tr>')

$gemIndex = @{}
foreach ($m in $gemRows) { $gemIndex[$m.Groups[3].Value] = "$($m.Groups[1].Value) $($m.Groups[2].Value)" }
$traitIndex = @{}
foreach ($m in $traitRows) { $traitIndex[$m.Groups[3].Value] = "$($m.Groups[1].Value) $($m.Groups[2].Value)" }

# Our per-character data (same as tool-gen-loadout.ps1)
$chars = @(
    @{ Hash = '079DF0CC'; Awake = '98A6D249'; T1 = '151E4674'; T2 = 'A374FDF0'; War = 'D76F4D24' },
    @{ Hash = '0D21B430'; Awake = '4F01D6CA'; T1 = '6EBFA176'; T2 = 'F1D5DBD0'; War = '4F135217' },
    @{ Hash = '18E2F9F9'; Awake = '9ADA3E00'; T1 = '3BFED918'; T2 = 'F8496336'; War = '9AFDFA9E' },
    @{ Hash = '1BB37EF0'; Awake = '895ABBF6'; T1 = '26956F25'; T2 = '1DE14C65'; War = 'DBA19768' },
    @{ Hash = '22E437E5'; Awake = 'E19B1965'; T1 = '8CDF9382'; T2 = 'D1012D8C'; War = '6316CBEB' },
    @{ Hash = '25D46F4B'; Awake = 'D8A464F1'; T1 = '9ACE140B'; T2 = '7B5B081D'; War = '79266456' },
    @{ Hash = '296471BE'; Awake = '6AAE4B8F'; T1 = '77C809F5'; T2 = '9230E3F5'; War = '7B4FC47A' },
    @{ Hash = '2A26B1B2'; Awake = '52A6E299'; T1 = 'CD030268'; T2 = 'A38510E2'; War = 'DADE14DC' },
    @{ Hash = 'A4ACBA76'; Awake = '52A6E299'; T1 = 'CD030268'; T2 = 'A38510E2'; War = 'DADE14DC' },
    @{ Hash = '2EBE91D5'; Awake = '673C5D8F'; T1 = '2E65A774'; T2 = '16EFF868'; War = 'D8F66C1C' },
    @{ Hash = '4D0A60C3'; Awake = 'E2B380E5'; T1 = 'B48EEF48'; T2 = '11AAE5F5'; War = 'C00163B3' },
    @{ Hash = '627BCB0D'; Awake = 'AB835493'; T1 = '86CBCDC4'; T2 = '05FA4599'; War = 'C7D379F1' },
    @{ Hash = '646C3168'; Awake = '5A360EA8'; T1 = '30773197'; T2 = '47384248'; War = '807B6684' },
    @{ Hash = '718E1A14'; Awake = 'B8C44D5E'; T1 = 'D40D1E9B'; T2 = '15806DFC'; War = '4E5F6706' },
    @{ Hash = '74DD4C79'; Awake = 'A8A0CBFF'; T1 = '06719232'; T2 = 'ED8D8AD8'; War = '5559232F' },
    @{ Hash = '978E4B18'; Awake = 'CE16D68B'; T1 = '5463232F'; T2 = '451D814C'; War = '0F026CF0' },
    @{ Hash = '9A8AF295'; Awake = '95CC3CB8'; T1 = 'D176D262'; T2 = '461A8E07'; War = 'B953CC1E' },
    @{ Hash = '9B15CFB1'; Awake = '23953FD4'; T1 = '7D75D904'; T2 = 'BE3404B9'; War = '3EB345D7' },
    @{ Hash = 'A3A3CB2F'; Awake = 'AF8E7E7E'; T1 = '93A2093C'; T2 = '7AD0C010'; War = 'B064A634' },
    @{ Hash = 'AA66178A'; Awake = '02B1F8C0'; T1 = 'EC3CF174'; T2 = 'AF513A9D'; War = 'E6B92E34' },
    @{ Hash = 'BAD16E3B'; Awake = '8ECBB0A3'; T1 = 'E85FF8E0'; T2 = '8572B8AF'; War = '81B293D9' },
    @{ Hash = 'BDEF7181'; Awake = '02472C43'; T1 = 'E60A735C'; T2 = '6FF05223'; War = 'BA504607' },
    @{ Hash = 'C3FFD418'; Awake = 'B441275D'; T1 = 'D908223D'; T2 = '7351D602'; War = 'A339D642' },
    @{ Hash = 'C8616284'; Awake = '9BD1CC24'; T1 = '23D0F67F'; T2 = 'C2A4C7A9'; War = '8519AD4A' },
    @{ Hash = 'DD7A151E'; Awake = '1BBE919C'; T1 = 'AA83F548'; T2 = '921B6B0C'; War = '0E42BE1B' },
    @{ Hash = 'E7053919'; Awake = '1A57AEF1'; T1 = '29B07BEB'; T2 = 'A63B89CD'; War = 'FDD1AD24' },
    @{ Hash = 'F0EB77EF'; Awake = 'E4F986D9'; T1 = '7440E869'; T2 = 'CD124165'; War = 'D7F9BB88' },
    @{ Hash = 'FC6CDF7B'; Awake = '119B24A8'; T1 = '0CD6C625'; T2 = 'A3B49220'; War = 'DAEFBB27' },
    @{ Hash = 'FD3BE362'; Awake = 'AEEF8343'; T1 = '9A9DC170'; T2 = '522E2388'; War = 'B85202BC' }
)

$bad = 0
foreach ($c in $chars) {
    $issues = @()
    foreach ($field in @('Awake','T1','T2','War')) {
        $h = $c[$field]
        $inGem = $gemIndex.ContainsKey($h)
        $inTrait = $traitIndex.ContainsKey($h)
        if (-not $inGem -and -not $inTrait) { $issues += "$field=$h 官方两张表都没有"; $bad++ }
        elseif ($field -eq 'Awake' -and -not $inGem) { $issues += "Awake=$h 不在 Gem 表"; $bad++ }
        elseif ($field -ne 'Awake' -and -not $inTrait) { $issues += "$field=$h missing from Trait table"; $bad++ }
    }
    $status = if ($issues.Count -eq 0) { 'OK' } else { "PROBLEM: $($issues -join '; ')" }
    $awakeName = if ($gemIndex.ContainsKey($c.Awake)) { $gemIndex[$c.Awake] } else { '?' }
    "{0}  {1}  {2}" -f $c.Hash, $status, $awakeName
}
Write-Output "=== 汇总: problems $bad / $($chars.Count * 4) ==="
