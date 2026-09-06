$ErrorActionPreference = 'Stop'

# ============================================================================
# 专属因子权威数据（改这里的 Hash/T1/T2/War/Awake 后重新生成）：
#   Awake = 觉醒＋（组合物品 gem，携带 T1/T2 两词条）；T1/T2 = 觉醒＋两专属词条；
#   War = 战气词条。T1Gem/T2Gem/WarGem（独立因子与战气的物品 gem）由脚本从
#   sigils.json 推导（special=False 且 skill 匹配）；Awake 优先用 sigils.json
#   的 special=True 行（skill=T1 & sec=T2），仅 3 个角色（1BB37EF0/25D46F4B/
#   646C3168）在表中无该组合行，此时回退本列。
# ============================================================================
$chars = @(
    @{ Hash = '079DF0CC'; Name = 'Rackam'; NameZh = '拉卡姆'; Awake = '98A6D249'; T1 = '151E4674'; T2 = 'A374FDF0'; War = 'D76F4D24' }, # Rackam
    @{ Hash = '0D21B430'; Name = 'Zeta'; NameZh = '泽塔'; Awake = '4F01D6CA'; T1 = '6EBFA176'; T2 = 'F1D5DBD0'; War = '4F135217' }, # Zeta
    @{ Hash = '18E2F9F9'; Name = 'Katalina'; NameZh = '卡塔莉娜'; Awake = '9ADA3E00'; T1 = '3BFED918'; T2 = 'F8496336'; War = '9AFDFA9E' }, # Katalina
    @{ Hash = '1BB37EF0'; Name = 'Gallanza'; NameZh = '加兰扎'; Awake = '895ABBF6'; T1 = '26956F25'; T2 = '1DE14C65'; War = 'DBA19768' }, # Gallanza
    @{ Hash = '22E437E5'; Name = 'Lancelot'; NameZh = '兰斯洛特'; Awake = 'E19B1965'; T1 = '8CDF9382'; T2 = 'D1012D8C'; War = '6316CBEB' }, # Lancelot
    @{ Hash = '25D46F4B'; Name = 'Maglielle'; NameZh = '玛格里耶'; Awake = 'D8A464F1'; T1 = '9ACE140B'; T2 = '7B5B081D'; War = '79266456' }, # Maglielle
    @{ Hash = '296471BE'; Name = 'Seofon'; NameZh = '西奥芬'; Awake = '6AAE4B8F'; T1 = '77C809F5'; T2 = '9230E3F5'; War = '7B4FC47A' }, # Seofon
    @{ Hash = '2A26B1B2'; Name = 'Gran'; NameZh = '古兰'; Awake = '52A6E299'; T1 = 'CD030268'; T2 = 'A38510E2'; War = 'DADE14DC' }, # Gran
    @{ Hash = 'A4ACBA76'; Name = 'Djeeta'; NameZh = '姬塔'; Awake = '52A6E299'; T1 = 'CD030268'; T2 = 'A38510E2'; War = 'DADE14DC' }, # Djeeta (shares captain exclusives)
    @{ Hash = '2EBE91D5'; Name = 'Vane'; NameZh = '瓦恩'; Awake = '673C5D8F'; T1 = '2E65A774'; T2 = '16EFF868'; War = 'D8F66C1C' }, # Vane
    @{ Hash = '4D0A60C3'; Name = 'Io'; NameZh = '伊欧'; Awake = 'E2B380E5'; T1 = 'B48EEF48'; T2 = '11AAE5F5'; War = 'C00163B3' }, # Io
    @{ Hash = '627BCB0D'; Name = 'Siegfried'; NameZh = '齐格飞'; Awake = 'AB835493'; T1 = '86CBCDC4'; T2 = '05FA4599'; War = 'C7D379F1' }, # Siegfried
    @{ Hash = '646C3168'; Name = 'Fraux'; NameZh = '芙罗'; Awake = '5A360EA8'; T1 = '30773197'; T2 = '47384248'; War = '807B6684' }, # Fraux
    @{ Hash = '718E1A14'; Name = 'Sandalphon'; NameZh = '桑德冯'; Awake = 'B8C44D5E'; T1 = 'D40D1E9B'; T2 = '15806DFC'; War = '4E5F6706' }, # Sandalphon
    @{ Hash = '74DD4C79'; Name = 'Fediel'; NameZh = '菲迪埃'; Awake = 'A8A0CBFF'; T1 = '06719232'; T2 = 'ED8D8AD8'; War = '5559232F' }, # Fediel
    @{ Hash = '978E4B18'; Name = 'Ghandagoza'; NameZh = '甘达戈萨'; Awake = 'CE16D68B'; T1 = '5463232F'; T2 = '451D814C'; War = '0F026CF0' }, # Ghandagoza
    @{ Hash = '9A8AF295'; Name = 'Beatrix'; NameZh = '比亚特莉丝'; Awake = '95CC3CB8'; T1 = 'D176D262'; T2 = '461A8E07'; War = 'B953CC1E' }, # Beatrix
    @{ Hash = '9B15CFB1'; Name = 'Eustace'; NameZh = '尤斯塔斯'; Awake = '23953FD4'; T1 = '7D75D904'; T2 = 'BE3404B9'; War = '3EB345D7' }, # Eustace
    @{ Hash = 'A3A3CB2F'; Name = 'Id'; NameZh = '伊德'; Awake = 'AF8E7E7E'; T1 = '93A2093C'; T2 = '7AD0C010'; War = 'B064A634' }, # Id
    @{ Hash = 'AA66178A'; Name = 'Cagliostro'; NameZh = '卡莉奥斯特罗'; Awake = '02B1F8C0'; T1 = 'EC3CF174'; T2 = 'AF513A9D'; War = 'E6B92E34' }, # Cagliostro
    @{ Hash = 'BAD16E3B'; Name = 'Tweyen'; NameZh = '特维因'; Awake = '8ECBB0A3'; T1 = 'E85FF8E0'; T2 = '8572B8AF'; War = '81B293D9' }, # Tweyen
    @{ Hash = 'BDEF7181'; Name = 'Percival'; NameZh = '珀西瓦尔'; Awake = '02472C43'; T1 = 'E60A735C'; T2 = '6FF05223'; War = 'BA504607' }, # Percival
    @{ Hash = 'C3FFD418'; Name = 'Ferry'; NameZh = '费里'; Awake = 'B441275D'; T1 = 'D908223D'; T2 = '7351D602'; War = 'A339D642' }, # Ferry
    @{ Hash = 'C8616284'; Name = 'Rosetta'; NameZh = '罗塞塔'; Awake = '9BD1CC24'; T1 = '23D0F67F'; T2 = 'C2A4C7A9'; War = '8519AD4A' }, # Rosetta
    @{ Hash = 'DD7A151E'; Name = 'Eugen'; NameZh = '欧根'; Awake = '1BBE919C'; T1 = 'AA83F548'; T2 = '921B6B0C'; War = '0E42BE1B' }, # Eugen
    @{ Hash = 'E7053919'; Name = 'Narmaya'; NameZh = '娜露梅'; Awake = '1A57AEF1'; T1 = '29B07BEB'; T2 = 'A63B89CD'; War = 'FDD1AD24' }, # Narmaya
    @{ Hash = 'F0EB77EF'; Name = 'Vaseraga'; NameZh = '巴萨拉加'; Awake = 'E4F986D9'; T1 = '7440E869'; T2 = 'CD124165'; War = 'D7F9BB88' }, # Vaseraga
    @{ Hash = 'FC6CDF7B'; Name = 'Yodarha'; NameZh = '约达尔哈'; Awake = '119B24A8'; T1 = '0CD6C625'; T2 = 'A3B49220'; War = 'DAEFBB27' }, # Yodarha
    @{ Hash = 'FD3BE362'; Name = 'Charlotta'; NameZh = '夏洛特'; Awake = 'AEEF8343'; T1 = '9A9DC170'; T2 = '522E2388'; War = 'B85202BC' }  # Charlotta
)

# sigils.json：skill -> 独立因子 gem（special=False）；"T1|T2" -> 觉醒＋组合 gem（special=True）
$root = Split-Path -Parent $PSScriptRoot
$sigils = Get-Content (Join-Path $root 'GBFR.PreEquippedSigils\sigils.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$indepGem = @{}
$awakeGem = @{}
foreach ($s in $sigils.sigils) {
    if (-not $s.special -and -not $indepGem.ContainsKey($s.skill)) { $indepGem[$s.skill] = $s.gem }
    if ($s.special -and -not $awakeGem.ContainsKey("$($s.skill)|$($s.sec)")) { $awakeGem["$($s.skill)|$($s.sec)"] = $s.gem }
}

$resolved = @()
foreach ($c in $chars) {
    $t1Gem = $indepGem[$c.T1]
    $t2Gem = $indepGem[$c.T2]
    $warGem = $indepGem[$c.War]
    $awake = $awakeGem["$($c.T1)|$($c.T2)"]
    if (-not $awake) { $awake = $c.Awake }   # 3 角色在 sigils.json 无组合行，回退显式值
    if (-not $t1Gem -or -not $t2Gem -or -not $warGem) {
        throw "cannot resolve exclusive gems for $($c.Hash): t1=$t1Gem t2=$t2Gem war=$warGem"
    }
    $resolved += [pscustomobject]@{
        Hash = $c.Hash; Name = $c.Name; NameZh = $c.NameZh; T1 = $c.T1; T2 = $c.T2; War = $c.War
        Awake = $awake; T1Gem = $t1Gem; T2Gem = $t2Gem; WarGem = $warGem
    }
}

# ---- 输出 1：template_loadout.cpp 的 kCharacterExclusives[] 段 ----
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('// 由 docs/tool-gen-loadout.ps1 生成；勿手改。')
[void]$sb.AppendLine('constexpr CharacterExclusiveLoadout kCharacterExclusives[] = {')
foreach ($r in $resolved) {
    [void]$sb.AppendLine("   { 0x$($r.Hash), // character")
    [void]$sb.AppendLine("      TemplateGemSlot{0x$($r.Awake), 0x$($r.T1), 15, 0x$($r.T2), 15, 15}, // slot0 awakening (t1+t2)")
    [void]$sb.AppendLine("      TemplateGemSlot{0x$($r.WarGem), 0x$($r.War), 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)")
    [void]$sb.AppendLine("      0x$($r.T1Gem), // t1 independent-factor gem")
    [void]$sb.AppendLine("      0x$($r.T2Gem), // t2 independent-factor gem")
    [void]$sb.AppendLine('   },')
}
[void]$sb.AppendLine('};')
$out = $sb.ToString()
Set-Content -Path "$env:TEMP\loadout_exclusives.txt" -Value $out -Encoding UTF8
Write-Output "generated kCharacterExclusives: $($resolved.Count) entries -> $env:TEMP\loadout_exclusives.txt"

# ---- 输出 2：character-exclusives.json（工具 "专属因子" 选项卡数据源） ----
$jsonDir = Join-Path $root 'GBFR.PreEquippedSigils'
$excl = @{
    exclusives = @($resolved | ForEach-Object {
        @{
            hash = $_.Hash
            name = $_.Name
            nameZh = $_.NameZh
            t1 = $_.T1
            t2 = $_.T2
            war = $_.War
            awakening = $_.Awake
            t1Gem = $_.T1Gem
            t2Gem = $_.T2Gem
            warGem = $_.WarGem
        }
    })
}
$exclOut = Join-Path $jsonDir 'character-exclusives.json'
[System.IO.File]::WriteAllText(
    $exclOut,
    ($excl | ConvertTo-Json -Depth 4),
    [System.Text.UTF8Encoding]::new($false))
Write-Output "wrote character-exclusives.json -> $exclOut"
