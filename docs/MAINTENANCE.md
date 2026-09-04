# GBFR Pre-Equipped Sigils 鈥?AI 鎺ユ墜缁存姢鎵嬪唽

> 闈㈠悜瀵硅薄锛氬悗缁帴绠℃湰椤圭洰鐨?AI agent / 寮€鍙戣€呫€?
> 闃呰鍓嶆彁锛氬厛璇?`README.md`锛堢敤鎴峰悜璇存槑锛夈€傛湰鎵嬪唽鏄?*鎶€鏈淮鎶?*鏂囨。銆?
> 椤圭洰浣嶇疆锛氭湰浠撳簱鏍圭洰褰曘€傛簮鐮侊細https://github.com/baagod/GBFR-Pre-Equipped-Sigils
> 娓告垙鐗堟湰锛欸ranblue Fantasy: Relink Endless Ragnarok **2.0.5**銆?
> 褰撳墠鐗堟湰锛?.3.6锛圱1 閰嶇疆鍖栧凡鏈湴鏋勫缓锛孨exus 灏氭湭鍙戝竷锛?.3.5 宸插彂甯?Nexus锛夈€傛淳鐢熻嚜 GBFR Extra Sigil Slots锛圚iyajomaho-num9锛夛紝宸插ぇ骞呯簿绠€銆?

---

## 1. 涓€鍙ヨ瘽璇存槑

娓告垙鍘熺敓鍙绠?12 涓彲瑙佸洜瀛愭Ы锛堝唴閮?trait 寰幆涓婇檺 13锛夈€傛湰 mod 鎶婂惊鐜笂闄愭墿鍒?
`13 + kTemplateSlotCount`锛屽苟 Hook 鍥犲瓙璇诲彇鍑芥暟锛氬綋娓告垙璇㈤棶绗?13 鍙疯捣鐨勮櫄鎷熸Ы鏃讹紝
鎸?*鍐呯疆妯℃澘琛?*鐜板満鍚堟垚涓€浠?GemData 浜ょ粰娓告垙銆?*涓嶅啓瀛樻。銆佷笉渚濊禆搴撳瓨銆佷笉鏀?
`GemData.WORN_BY`**锛涙垬鏂楁暟鍊兼槸鐪熷疄鐨勬湰鍦版晥鏋滐紙鍦ㄧ嚎 = 浣滃紛绾э紝椋庨櫓鑷礋锛夈€?

## 2. 鐩綍缁撴瀯涓庢枃浠惰亴璐?

```
build-release.ps1                    鏋勫缓+鎵撳寘鑴氭湰锛圡SBuild native 鈫?dotnet managed 鈫?zip锛?
docs/
  gbfr-sigil-hashes.zh-CN.tsv        hash 鏌ヨ琛紙S=鐗╁搧/gem_id锛孴=璇嶆潯/trait锛屼粎鍙傝€冧笉鎵撳寘锛?
  gbfr-sigil-hashes.en.tsv           鍚屼笂锛堣嫳鏂囷級
  MAINTENANCE.md                     鏈墜鍐?
GBFR.PreEquippedSigils/             C# 鎵樼灞傦紙Reloaded-II 鎻掍欢澹筹級
  Mod.cs                             鐢熷懡鍛ㄦ湡銆佹棩蹇楋紙鏃堕棿鎴筹級銆?50ms 缁存寔 Tick
  NativeCore.cs                      鍘熺敓闂ㄩ潰锛氬姞杞?ABI 鏍￠獙/鏃ュ織鍥炶皟/Tick/Shutdown/娑堟伅璇诲彇
  NativeCore.Interop.cs              P/Invoke 澹版槑锛堝繀椤讳笌 native_api.h 鍚屾锛?
  ModConfig.json                     ModId/鐗堟湰/鎻忚堪锛堝彂甯冧俊鎭級
GBFR.PreEquippedSigils.Native/      C++ 鍘熺敓鏍稿績
  native_api.h                       鍐荤粨鐨?C ABI锛坴15锛? 涓鍑?+ GemData 缁撴瀯锛?
  native_internal.h                  鍐呴儴鐘舵€?澹版槑/甯搁噺锛堟ā鏉挎Ы甯搁噺銆侀妫€瀛楄妭绛夛級
  src/
    dllmain.cpp                      DLL 鍏ュ彛锛堜粎瀛樻ā鍧楀彞鏌勶紝loader-lock-safe锛?
    exports.cpp                      6 涓?C 瀵煎嚭瀹炵幇
    runtime.cpp                      鍒濆鍖栭『搴忕紪鎺?+ 闃舵鏃ュ織
    runtime_state.cpp                鍏ㄥ眬鍘熷瓙/Log锛堝甫鏃堕棿鎴筹級/phase 鏈哄埗/娑堟伅缂撳啿
    layout_resolver.cpp              鈽呰涔夊竷灞€瑙ｆ瀽锛?.0.5 閿氱偣锛屾渶楂橀闄╋級
    safe_game_access.cpp             鈽匰EH 瀹夊叏鍐呭瓨璇诲啓銆佺姸鎬侀噸寤恒€佹巿鏉冩彁浜?
    trait_hooks.cpp                  鈽呮敞鍏ユ牳蹇冿細getter detour銆乶atural bind銆乭ot-apply 瑙﹀彂
    selection_store.cpp              瑙掕壊閫夋嫨瀛樺偍銆乭ot-apply 闃熷垪锛坓eneration 鏈哄埗锛?
    name_tables.cpp                  鍏煎琛ㄥ姞杞斤紙199 鏉¤鑹查檺鍒舵槧灏勶紝缂哄け鍗?fail-closed锛?
    template_loadout.cpp             鈽呪槄閰嶈琛ㄢ€斺€旀棩甯哥淮鎶ゅ敮涓€瑕佹敼鐨勬枃浠?
```

`鈽卄 = 楂橀闄╁尯锛岄櫎闈炴槑纭换鍔￠渶瑕侊紝涓嶈鍔ㄣ€?

## 3. 鏍稿績鏁版嵁娴?

```
鍚姩:
  Reloaded-II 鈫?Mod.cs 鈫?NativeCore.Initialize 鈫?exports.GBFR20_Initialize
    鈫?runtime.Initialize:
        executable-validation (蹇呴』 granblue_fantasy_relink.exe)
        compatibility-table     (compatibility.tsv, 199 鏉? 澶辫触鍗冲仠姝?
        semantic-layout-resolution (layout_resolver, 澶辫触鍗冲仠姝?
        template-selection-install (InstallDefaultTemplateSelections
                                      鈫?鎶?0xFE000000+i 鍚堟垚妲?id 鍐欏叆瑙掕壊閫夋嫨)
        native-hook-install    (4 涓?hook + 2 澶勫惊鐜笂闄?patch)

杩愯:
  娓告垙鐘舵€侀噸寤?鈫?GetGemDataByIndexDetour(slot 13..12+count)
    鈫?TryLoadVirtualTraitSelection 鈫?TryCopySelectedVirtualGem
        鈫?IsTemplateSlotId(0xFE000000+) 鈫?TryCopyTemplateGem
            鈫?浠?kDefaultTemplates 鍙?(gem_id, trait1/2, 绛夌骇)
            鈫?缁勮 GemData(worn_by=0x887AE0B0 鏈澶? flags=0) 鈫?SafeCopyToOutput
    鈫?natural bind 杩借釜: injected==expected 涓?identity 涓€鑷?鈫?CommitAuthorizedStatus
    鈫?鏃ュ織 "Live battle Trait contribution confirmed for 0x...: N/N"

缁存寔 (Mod.cs 250ms Tick 鈫?GBFR20_Tick):
  UpdateEditSessionState / ValidateAuthorizedStatuses /
  ScheduleSelectedStatusRebind / ProcessPendingHotApply / ConsumeApplyResult
  锛坔ot-apply 浜х敓 "Generation N ... copied N/N" 鏃ュ織锛岄獙璇佽澶囩晫闈?璁粌鍦鸿矾寰勶級
```

## 4. 妯℃澘閰嶈琛紙鏃ュ父缁存姢鏍稿績锛?

鏂囦欢锛歚GBFR.PreEquippedSigils.Native/src/template_loadout.cpp` 鐨?`kDefaultTemplates[]`銆?
**v0.3 璧疯鐩栧叏瑙掕壊**锛坴0.3.5 璧锋瘡瑙掕壊 8 妲斤級锛屾暟鎹敱鐢熸垚鑴氭湰缁存姢锛屼笉瑕佹墜鍐?hash锛?

| 宸ュ叿 | 浣滅敤 |
|---|---|
| `docs/tool-extract-exclusives.ps1` | 浠?compatibility.tsv + 鍚嶅瓧琛ㄦ彁鍙栨瘡瑙掕壊涓撳睘鍥犲瓙锛堣閱掞紜 gem銆佷袱涓笓灞炶瘝鏉°€佹垬姘旇瘝鏉★級 |
| `docs/tool-gen-loadout.ps1` | 鍐呭祵姣忚鑹蹭笓灞炴暟鎹?鈫?鐢熸垚 `kDefaultTemplates[]` 鏁扮粍鏂囨湰 |
| [Nenkai/relink-modding](https://nenkai.github.io/relink-modding/) + [GBFRDataTools](https://github.com/Nenkai/GBFRDataTools) | 寮€鍙戞湡鏁版嵁鏍稿疄锛堝畼鏂?ID 琛?/ 瑙ｅ寘瀵煎嚭锛夆€斺€?*杩愯鏃朵笉渚濊禆**锛屼粎寮€鍙戝伐鍏?|

**鏀归厤瑁呯殑鏍囧噯娴佺▼**锛氭敼 `tool-gen-loadout.ps1` 閲岀殑鏁版嵁琛紙鎴栨敼閫氱敤妲藉畾涔夛級鈫?杩愯鑴氭湰杈撳嚭鍒颁复鏃舵枃浠?鈫?鏇挎崲 `template_loadout.cpp` 涓?`constexpr CharacterTemplate kDefaultTemplates[] = { ... };` 娈碉紙鑷姩瀹氫綅璧锋鏇挎崲锛夈€?

缁撴瀯锛堟瘡妲戒竴涓?`TemplateGemSlot`锛夛細

```cpp
TemplateGemSlot{
   0x335DA2A5, // gem_id: 鐗╁搧 hash锛圫 琛岋級銆傛父鎴忔寜瀹冩煡 master 琛ㄦ嬁鏄剧ず鍚嶏紱璇嶆潯鏁堟灉鍚冪殑鏄笅闈袱涓?hash锛堝凡瀹為獙楠岃瘉锛?
   0xE69A4694, // trait1: 涓昏瘝鏉?hash锛圱 琛岋級
   15,         // trait1_level: 鈪わ紜 = 15锛堟紗榛戦挸锜?= 20锛?
   0x95F3FA86, // trait2: 鍓瘝鏉?hash銆?*鏃犲壇璇嶆潯蹇呴』鐢?0x887AE0B0锛?涓嶉€夋嫨"鍝ㄥ叺锛夛紝涓嶈兘鐢?0**锛?
   15,         // trait2_level
   15,         // sigil_level: 鐗╁搧鏄剧ず绛夌骇锛堟紗榛戦挸锜?= 20锛涜澶囧悗浜嬩欢鍥犲瓙鏄剧ず "-"銆佸叏鍒楄〃鏄剧ず 20锛?
},
```

> 鈿狅笍 **韪╄繃鐨勫潙锛?026-09-02锛孍R 2.0.5锛?*锛?鍗曡瘝鏉″洜瀛?锛堝婕嗛粦鐨勯挸锜瑰洜瀛?Lv20锛夋妸
> `trait2` 鍐欐垚 `0` 浼氬湪娓告垙"鍏ㄩ儴鍥犲瓙鍒楄〃"閲?*澶氭覆鏌撲竴涓┖鐨?Lv1 鏉＄洰**銆?
> 姝ｇ‘鍐欐硶鏄?`trait2 = 0x887AE0B0`锛堟父鎴忔湰浣撶殑"涓嶉€夋嫨"鍝ㄥ叺鍊硷紝鍙栬嚜浠庢父鎴忓唴淇敼鍣?
> 瑙傚療鍒扮殑鏄犲皠锛涙湰浣撲簨浠跺洜瀛愯澶囧悗绛夌骇鏄剧ず "-"锛屽叏鍒楄〃閲屾槸 20 鈥斺€?涓庢敞鍏ョ殑
> `trait1_level=20 / sigil_level=20` 涓€鑷达紝涓よ€呯嫭绔嬶紝閮戒笉鏄棶棰橈級銆?
> 璇ュ潙瑕嗙洊**鎵€鏈夊崟璇嶆潯妲戒綅**锛堟垬姘旀Ы / 婵€鏄?/ 閽宠煿锛夛紝鍏朵粬妲戒綅鍧囨湁鐪熷疄 trait2锛屼笉鍙楀奖鍝嶃€?

**瑙勫垯**锛?
- 姣忚鑹蹭竴涓?`CharacterTemplate{ character_hash, slots[24] }`锛沗slots` 浠?0 璧?*杩炵画**锛岄亣 `gem_id==0` 瑙嗕负琛ㄧ粨鏉燂紙`InstallDefaultTemplateSelections` 涓?`FindTemplateSlot` 渚濊禆姝ょ害瀹氾級銆?
- 鍚堟垚妲?id = `kTemplateSlotIdBase(0xFE000000) + 妲藉簭鍙穈锛屼笉浼氫笌鐪熷疄搴撳瓨妲戒綅鍐茬獊锛沗IsTemplateSlotId` 鍒ゅ畾銆?
- **鍐呯疆妯℃澘妲芥暟锛堝嚭鍘傞璁撅級**锛歚native_internal.h` 鐨?`kTemplateSlotCount`锛堝綋鍓?9锛夊彧鍐冲畾"鏃犵帺瀹堕厤缃椂鐨勯粯璁ゆЫ鏁?锛涚帺瀹堕厤缃紙loadout.json锛夊彲浠绘剰 2+鍚敤妲斤紙鈮?2锛夛紝**涓嶅彈璇ュ父閲忕害鏉?*銆備粎褰撲慨鏀瑰唴缃粯璁わ紙妯℃澘琛級鏃堕渶鍚屾璇ュ父閲忋€?
- 瑙掕壊涓撳睘鐗╁搧锛堣閱掞紜/鎴樻皵锛夊彈 `compatibility.tsv` 闄愬埗锛歚TryCopyTemplateGem` 浼氱敤
  `GetRequiredCharacterHash(gem_id)` 鏍￠獙锛屼笓灞炲洜瀛愬彧鑳借缁欏搴旇鑹诧紙鍙ゅ叞/濮浜掗€氾紝濮鏉＄洰浣跨敤鍙ゅ叞涓撳睘锛夈€?
- 璇嶆潯 hash 鏌ヨ锛歚docs/gbfr-sigil-hashes.zh-CN.tsv`锛圫=鐗╁搧銆乀=璇嶆潯锛汣trl+F 鎼滃悕瀛楋級銆?
- 瑙掕壊 hash锛堣鑹插悕 鈫?hash锛夛細瑙?`UiLocalization.cs` 鐨勫巻鍙茬増鏈垨 compatibility.tsv 鐨?
  character_key 鍒楋紱甯哥敤锛氬彜鍏?`2A26B1B2`銆佸К濉?`A4ACBA76`銆佸闇叉 `E7053919`銆?
  鑺欏姵 `646C3168`銆佽彶杩焹灏?`74DD4C79`銆?

## 5. 鏋勫缓涓庨儴缃?

鐜瑕佹眰锛歐indows x64銆乂S2022 Build Tools锛圡SVC v143 + Windows SDK锛夈€?NET 8 SDK銆?

```powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1   # 榛樿 Release/x64/0.3.6
# 浜х墿: dist\GBFR-Pre-Equipped-Sigils-<version>.zip
```

- 閮ㄧ讲锛?*娓告垙蹇呴』閫€鍑?*锛屾妸 `dist\GBFR.PreEquippedSigils` 鏁翠釜鏂囦欢澶瑰鍒跺埌
  Reloaded-II 鐨?`Mods\`锛堣鐩?鍏堝垹鏃х洰褰曪級銆?
- 鐗堟湰鍙凤細鍚屾鏀?`ModConfig.json` 鐨?`ModVersion` 涓?`build-release.ps1` 榛樿 `$Version`銆?

## 6. 楠岃瘉娓呭崟锛堟瘡娆℃敼鍔ㄥ悗蹇呴』锛?

1. 缂栬瘧锛?*0 璀﹀憡 0 閿欒**锛坱hird_party 鐨?C4834 宸插湪 vcxproj 鍗曠嫭鍘嬪埗锛夈€?
2. 鏃ュ織 `GBFR.PreEquippedSigils.Reloaded.log`锛坢od 鐩綍锛夛細
   - `Installed N built-in template loadout selection(s); inventory-independent.`
   - `Native hook installation completed with N virtual slots; ...`
   - 杩涙垬鏂楋細`Live battle Trait contribution confirmed for 0xE7053919: N/N ...`
   - 瑁呭鐣岄潰/璁粌鍦猴細`Generation M for 0xE7053919: equipment/test rebuild copied N/N ...`
3. 璁粌鍦哄疄娴嬭瘝鏉℃晥鏋滐紙濡傝豹鑳嗘繏姝讳笉姝汇€佽嚜鍔ㄥ娲昏嚜璧凤級+ 琛€鏉′笅 buff 鍥炬爣銆?
4. 閲嶅惎娓告垙閰嶇疆淇濈暀銆?

## 7. 闆峰尯锛坒ail-closed 涓庡畨鍏ㄨ竟鐣岋紝绂佹鍓婂急锛?

- `layout_resolver.cpp`锛氬敮涓€璇箟閿氱偣銆乧all/RIP 鎺ㄥ銆佺簿纭瓧鑺傞妫€銆傝В鏋愪笉瀹屾暣/澶氶噸鍖归厤/
  鏍￠獙涓嶈繃 鈫?**鏁村 gameplay hook 涓嶅畨瑁?*锛坒ail-closed锛夛紝涓嶉檷绾?鎵句釜鍍忕殑灏?Hook"銆?
- `trait_hooks.cpp`锛歞etour 鐨?TLS/generation/identity/context/expected/injected 鏍￠獙椤哄簭锛?
  natural bind 鐨勬巿鏉冩彁浜わ紙`CommitAuthorizedStatus`锛変笌 `ValidateAuthorizedStatuses`銆?
- `safe_game_access.cpp`锛氭墍鏈夋父鎴忓唴瀛樿鍙栧繀椤昏蛋 SEH 瀹夊叏鍖呰涓庡湴鍧€鑼冨洿妫€鏌ャ€?
- `compatibility.tsv` 缂哄け鎴栨潯鐩暟 != 199 鈫?鍚姩澶辫触锛坒ail-closed锛夈€?
- ABI锛歚native_api.h`锛堝鍑虹鍚嶃€乸acking銆乣GBFR20_ABI_VERSION=15`锛変笌
  `NativeCore.Interop.cs`銆乣NativeCore.cs` 鐨?`AbiVersion` 蹇呴』涓€鑷达紱鏀瑰姩闇€涓夋柟鍚屾 + 鐗堟湰鍙烽€掑銆?
- **鍙€夐厤缃?*锛欼NI 浣撶郴宸插垹闄わ紱鏃?`loadout.json` 鏃舵Ы鏁?= `kTemplateSlotCount` 甯搁噺锛坄native_internal.h`锛屽綋鍓?9 = 瑙夐啋锛?鎴樻皵 + 7 閫氱敤锛夛紱鏈夐厤缃椂 = 2 + 鍚敤妲芥暟锛堢敱 `LoadoutConfig` 瑙ｆ瀽鏍￠獙銆乵time 250ms 鐑簲鐢級銆?
- 绗笁鏂?`third_party/`锛坰afetyhook銆乑ydis锛夊彧鍙崌绾ф浛鎹紝涓嶅彲鎵嬫敼銆?
- 淇濇寔涓婃父 3 绌烘牸缂╄繘椋庢牸锛坣ative锛夛紝鎵樼灞?4 绌烘牸銆?

## 8. 淇濈暀浣嗘槗琚鍒や负"姝讳唬鐮?鐨勬満鍒?

| 鏈哄埗 | 浣嶇疆 | 浣滅敤 | 鍒犻櫎鍚庢灉 |
|---|---|---|---|
| hot-apply锛圧equestHotApply / ProcessPendingHotApply / ScheduleSelectedStatusRebind锛?| selection_store / trait_hooks / exports.Tick | 涓诲姩閲嶅缓瑙掕壊鐘舵€侊紝浜х敓 Generation 纭鏃ュ織锛岃澶囩晫闈㈠嵆鏃剁敓鏁?| 澶卞幓楠岃瘉鏃ュ織锛涢儴鍒嗗満鏅敓鏁堝欢杩熷埌涓嬫鑷劧閲嶅缓銆?*涓嶅缓璁垹** |
| EditSession 鐘舵€侊紙UpdateEditSessionState / SafeReadUiModes锛?| safe_game_access | hot-apply 鐨?context1 鍒嗘敮鍒ゆ嵁 | 涓?hot-apply 缁戝畾 |

## 9. 宸茬煡闄愬埗涓庢湭鏉ユ柟鍚?

- 閰嶈琛ㄧ紪璇戞湡鍐呯疆锛?*閰嶇疆鍖栧凡瀹屾垚**锛?026-09-04锛夛細`loadout.json` + Wails v3 宸ュ叿锛坄loadouttool/`锛屾墭鐩?鍗曞疄渚?鑷姩淇濆瓨/姣忚瘝鏉℃渶澶х瓑绾э級+ RegisterHotKey 鐑敭锛堥粯璁?F1锛? ABI v16锛岃瑙?[docs/PLAN-loadout-config.md](PLAN-loadout-config.md)锛堣鍒掑凡鎵ц锛屽亸宸褰曡璇ユ枃妗ｅご閮級銆?
- 褰撳墠宸茶鐩栧叏瑙掕壊锛涙墿灞曟柊瑙掕壊 = 鐢熸垚鍣ㄦ暟鎹〃鍔犳潯鐩?+ 鏌ヨ瑙掕壊瑙夐啋锛?鎴樻皵 hash銆?
- 娓告垙鏇存柊鍚庨渶鍥炲綊锛歚layout_resolver` 閿氱偣鍙兘澶辨晥 鈫?鏃ュ織鍑虹幇 layout failed 鈫?绛変笂娓?
  鏂规鎴栭噸鏂伴€嗗悜銆?

## 10. 甯哥敤鎿嶄綔閫熸煡锛堢粰鎺ユ墜 AI 鐨勬寚浠ゆā鏉匡級

- **鏀规煇瑙掕壊鏌愭Ы鐨勮瘝鏉?*锛氱紪杈?`template_loadout.cpp` 瀵瑰簲 `TemplateGemSlot` 鐨?
  `trait1/trait2` hash 涓庣瓑绾э紙hash 鏌?`docs/gbfr-sigil-hashes.zh-CN.tsv`锛夆啋 缂栬瘧 鈫?閮ㄧ讲 鈫?楠岃瘉銆?
- **鏀瑰嚭鍘傞粯璁わ紙妯℃澘琛級**锛歚tool-gen-loadout.ps1` 閫氱敤妲藉畾涔夎拷鍔?璋冩暣鏁版嵁 鈫?閲嶆柊鐢熸垚
  锛堟暟缁?+ `kTemplateSlotCount` 甯搁噺鍚屾鏇存柊鈥斺€斾粎褰卞搷鏃犻厤缃椂鐨勯粯璁わ級鈫?缂栬瘧 鈫?閮ㄧ讲 鈫?楠岃瘉銆?
- **鍔犺鑹?*锛氭煡璇ヨ鑹茶閱掞紜/鎴樻皵鐨?S/T hash锛坈ompatibility.tsv + 鍚嶅瓧琛級鈫?妯℃澘琛ㄥ姞
  `CharacterTemplate` 鏉＄洰 鈫?缂栬瘧 鈫?閮ㄧ讲 鈫?楠岃瘉銆?
- **鍗囩増鏈?*锛氳蛋 搂11 鍙戝竷娴佺▼锛堝惈鐗堟湰鍙峰悓姝ャ€佸叏鏂囨。鏃х増鏈彿娈嬬暀鎵弿銆丯exus 鎻忚堪鍚屾锛夈€?
- **鎻愪氦**锛歚git -c user.name="baagod" -c user.email="780810441@qq.com" commit ...`
  锛堜笉瑕佹敼鍏ㄥ眬 git config锛夈€傛彁浜ゅ墠 `git status` 纭鏃?bin/obj/dist 娣峰叆銆?
- **鎺ㄩ€?*锛歚git -c credential.helper="!gh auth git-credential" push origin main`
  锛堜粨搴撳凡閰嶇疆鏈湴浠ｇ悊 127.0.0.1:7890锛涜嫢鎻愮ず 403锛屾鏌?gh token 鐨?Contents: Read and write 鏉冮檺锛夈€?

## 11. 鍙戝竷涓?Nexus 鍚庣画缁存姢

**宸插彂甯?*锛坴0.3.5锛?026-09-03锛夛細https://www.nexusmods.com/granbluefantasyrelink/mods/823

鍙戝竷淇℃伅锛堝彂甯?鏇存柊鏃朵互鏈〃涓?mod README 涓哄噯锛夛細

| 椤?| 鍊?|
|---|---|
| 鍚嶇О | GBFR Pre-Equipped Sigils |
| 鍒嗙被 | Miscellaneous |
| 鏍囩 | AI-Generated Content / Cheating / Gameplay |
| 涓绘枃浠?| `dist/GBFR-Pre-Equipped-Sigils-<version>.zip` |
| 婧愮爜 | https://github.com/baagod/GBFR-Pre-Equipped-Sigils |

> **AI 鏍囩鍙ｅ緞锛?026-08-01 Nexus 鏂版斂锛?*锛欰I 鏍囩鍒嗕笁妗ｂ€斺€擿AI-Generated Content`锛堝惈
> AI 鐢熸垚鐨?*浠ｇ爜**銆乁I銆佽闊炽€佸璇濄€佺炕璇戙€侀煶涔愩€佹父鎴忓唴璧勪骇锛? `AI Media`锛圓I 鎺ㄥ箍鍥俱€?
> 缂╃暐鍥俱€佽棰戙€侀〉闈㈡弿杩扮瓑 mod 澶栧獟浣擄級/ `AI-Assisted`锛堣交寰娇鐢級銆傝鍒欙細**涓昏闈?AI
> 鍒朵綔鐨?mod 蹇呴』鎵?AI-Generated Content**锛涙墦 AI-Assisted 鐨勶紝瀹℃牳鏂瑰彲瑕佹眰璇佹槑寮€鍙?
> "浜虹被涓诲"銆傛湰 mod 浠ｇ爜鐢?AI 缂栧啓锛圧EADME 宸插叕寮€澹版槑锛夆啋 **蹇呴』淇濇寔 AI-Generated
> Content锛屽嬁闄嶄负 AI-Assisted**锛堥€変簡鍙兘琚姹傝瘉鏄庝汉绫讳富瀵硷紝椋庨櫓鍗曞悜锛夛紱鎴浘鍧囦负
> 娓告垙鍐呭疄鎷嶃€佹棤 AI 鍥?鈫?鏃犻渶 AI Media銆傚畞鍙亸閲嶏紝涓嶅彲鍋忎綆銆?

**鍙戝竷鍚庣淮鎶ゆ祦绋?*锛堟瘡娆″彂甯冩柊鐗堟湰渚濇鎵ц锛夛細

1. **鍗囩増鏈?*锛氭敼 `ModConfig.json` 鐨?`ModVersion` 涓?`build-release.ps1` 榛樿 `$Version`锛?
   鎸?搂10 鎵弿鍏ㄦ枃妗ｆ棫鐗堟湰鍙锋畫鐣欙紙README銆丮AINTENANCE 澶撮儴銆佹瀯寤烘敞閲婏級銆?
2. **鏋勫缓**锛歚build-release.ps1` 鈫?浜у嚭 `dist/GBFR-Pre-Equipped-Sigils-<version>.zip`銆?
3. **閮ㄧ讲楠岃瘉**锛氭父鎴忛€€鍑?鈫?澶嶅埗 `dist\GBFR.PreEquippedSigils` 鍒?`Mods\` 鈫?鎸?搂6 楠岃瘉娓呭崟瀹炴祴銆?
4. **鏇存柊 Nexus 鏂囦欢椤?*锛氫笂浼犳柊 zip锛汵exus 鍙鏈€鏂版枃浠剁増鏈紝鏃х増鑷姩褰掓。鍒板巻鍙层€?
   涓婁紶鏃朵繚鎸佸悕绉?鍒嗙被/鏍囩/鏉冮檺涓嶅彉锛堣涓婅〃锛夈€?
5. **鍚屾椤甸潰鎻忚堪**锛歂exus 鎻忚堪涓?`GBFR.PreEquippedSigils/README.md` 鍚屾簮鈥斺€?
   鏀归厤瑁呭悗蹇呴』涓ゅ鍚屾锛堥厤瑁呰〃銆佹憳瑕併€佹埅鍥句綅缃級銆?
6. **鎴浘**锛氫竴寰嬫父鎴忓唴鐪熷疄鎴浘锛屽彂甯冨悗涓婁紶鍒?Images 鍖猴紙涓嶈 AI 鐢熸垚鍥撅級銆?
7. **娓告垙鏇存柊鍚?*锛氬厛鏈満鍥炲綊锛埪?锛夛紱鑻?layout 瑙ｆ瀽澶辫触锛堟棩蹇楀嚭鐜?layout failed锛夛紝
   鍦ㄩ〉闈㈤《閮ㄥ姞"涓嶅吋瀹圭増鏈?璀﹀憡骞跺仠鏇达紝涓嶈闈欓粯澶辨晥銆?
8. **鎻愪氦鎺ㄩ€?*锛氭寜 搂10 鐨勬彁浜?鎺ㄩ€佹ā鏉挎墽琛岋紝鎶婄増鏈彿涓庡彂甯冭褰曞悓姝ュ埌浠撳簱銆?

> 澶囨敞锛歊ELEASE-NEXUS.md 宸插垹闄わ紝鍙戝竷鐩存帴鐢ㄦ湰鎵嬪唽銆?

## 12. 浼氳瘽浜ゆ帴鎯呮姤锛?026-09-03锛屼緵鏂颁細璇?AI 蹇€熷榻愶級

### 褰撳墠鐘舵€?
- **鐗堟湰**锛歷0.3.6锛堟湰鍦版瀯寤猴細ABI v16 + 閰嶇疆鍖?T1/T2 **宸插畬鎴?*锛屽疄娴嬮€氳繃鍚庡緟鍙戝竷 Nexus锛汵exus 鐜拌 0.3.5锛夈€傛Ы浣?9锛堣閱掞紜 / 鎴樻皵鍗曡瘝鏉?/ 婵€鏄?/ 璞儐+鑷姩澶嶆椿 / 涓嶅姩+鏄庨暅姝㈡按 / 鍒氬仴+鑽按 / 瀹堟姢+韬查伩鎬ц兘 / 杩藉嚮+杩呮嵎鑳藉姏 / 婕嗛粦鐨勯挸锜瑰洜瀛?Lv20锛? 閰嶈宸ュ叿锛圵ails锛夈€?
- **鍞竴鎬?*锛欸BFR 鍞竴"闆跺簱瀛橀閰嶈 + 杩愯鏃跺悎鎴?+ 涓嶇瀛樻。"鐨?mod锛涘師鐗堬紙657 Extra Sigil Slots锛夋湁搴撳瓨/UI/璺ㄨ鑹茬粦瀹氱棝鐐光€斺€旈渶宸紓鍖栵細"棰勯厤瑁?鍏ㄨ鑹?闆舵姌鑵?銆?

### 宸查獙璇侊紙瀹炴祴閫氳繃锛?
- 涓绘帶 + AI 瑙掕壊閮藉悆娉ㄥ叆锛堟槑闀滄姘?瀹堟姢/HP鍚告敹/杩藉嚮/杩呮嵎锛夆€斺€斿嵏涓绘Ы鍥犲瓙娴嬭瘯纭銆?

### 甯傚満鎯呮姤锛圢exus 绔炲搧锛?026-09-03~04锛?
**杞寲鐜囧彛寰勶細Total views 梅 Unique DLs锛堟暟鍊艰秺灏?= 杞寲瓒婂ソ锛夛紝濡?823 = 1,329梅125 = 10.63锛涘嬁涓?Unique梅Views 娣风敤銆?*
- **鎴戯紙823锛?*锛歎nique 125 / End 2 / Total 171 / Views 1,329锛堝彂甯冪 3 澶╁疄鏃舵姄鍙栵級鈥斺€?*杞寲 10.63**锛岃拷骞冲師鐗?11.78锛堝樊 1.15锛夈€?
- **823 鏇存柊锛?026-09-04 05:19 鍙戝竷锛屼粛涓?0.3.5 鐗堬細鍚拷鍑?杩呮嵎鑳藉姏瀹屾暣 8 妲介厤缃?+ 妲?7/8 浜ゆ崲 + 娓呯悊鍚庢瀯寤猴級**锛氭枃浠朵笅杞?u=48 / t=49锛堝彂甯冩暟灏忔椂鍐咃級鈥斺€斾互鑰佺敤鎴锋洿鏂板洖娴佷负涓伙紝鎷夋柊浠嶅彈 Views 鏇濆厜鐡堕闄愬埗锛?*涓嬫鍙戝竷搴斿崌 0.3.6锛堟湰娆″悓鍚?0.3.5 瑕嗙洊锛岀敤鎴蜂晶鐪嬩笉鍑哄彉鍖栵級**銆?
- 657 鍘熺増锛歎nique 1,489 / End 28 / Total 3,153 / Views 17,534 / 杞寲 11.78锛堥涓墿灞曟Ы銆佹棤绔炲搧鏈燂級鈥斺€旂洰鏍囷細棰勮浼樺寲杞寲鐜囪拷涓?瓒呰繃锛堝凡鍩烘湰杈炬垚锛夈€?

### 鐢ㄦ埛鍙嶉
- 闊╁浗鐜╁锛堟紗榛戦挸锜瑰洜瀛愶級鈥斺€斿凡瀹炵幇锛堟Ы 8锛? 鍥炲銆?
- 鐜╁闂?鑳藉惁涓?657 鍏卞瓨"鈥斺€斿洖澶?浼氬啿绐侊紙鍚?hook锛夛紝杩欐槸 657 鐨?drop-in replacement"銆?
- impact008锛?026-09-04锛夛細璇锋眰"杩呮嵎鑳藉姏/鎬掓稕/婵€鏄傞《婊?鐗堚€斺€?*鎷掔粷"椤堕厤/瓒呭己"**锛堜繚鎶ゅ钩琛★級锛屾帴鍙楀叾鐪熷疄璇夋眰锛堟€掓稕涓嶅湪妯℃澘銆佽瘝鏉″彲閫夋€э級鈫?褰掑叆閰嶇疆鍖栨柟鍚戯紱鍥炲璇濇湳 = "骞宠　 + 鍙厤缃?銆?

### 鏈潵鏂瑰悜锛堟湭鍋氾級
- ~~閰嶇疆鍖杶~ **宸插畬鎴?*锛堣涓婏紱涓嶅啀閲嶅绔嬮」锛夈€傚悗缁柟鍚戯細棰勮闆嗕赴瀵岋紙鐙傛垬澹?鏂反杈?浼ゅ涓婇檺/澶╂槦绯荤瓑锛変綔涓?pre-loadout 妯℃澘锛涙瘡瑙掕壊寮€鍏?v2锛涚墿鍝佹潈濞佺粍鍚堣〃銆?
- 鍧氭寔"鍚堢悊鎵╁睍妲?璺嚎锛堜笉鍋?瓒呭己鏁板€?椤堕厤"鈥斺€?87/819 鏄珵鍝侊紝涓嶆挒杞︼紱瀵圭帺瀹惰姹傜粺涓€璇濇湳鎷掔粷锛夈€?
- Reddit 鍙嶈惀閿€涓ユ牸鈥斺€?*涓嶈涓诲姩鍦?Reddit 鑷崘**锛堢ぞ鍖烘晫鎰?浣滃紛鑰?锛夈€?

### 澶囨敞
- 绔炲搧鏁板瓧鐢?Jina Reader 鎶撳彇锛堝彲鑳芥湁杞诲井璇樊锛夛紝浠呬綔鍙傝€冦€?
- 鎵€鏈?妲戒綅/鐗堟湰/涓嬭浇"鏀瑰姩鍚庡悓姝ワ細MAINTENANCE 澶撮儴銆丷EADME脳2銆丮odConfig銆乥uild-release.ps1銆?
