# GBFR Pre-Equipped Sigils

涓哄叏瑙掕壊棰勯厤鎵╁睍鍥犲瓙锛屼笉鍗犵敤鏈綋 12 妲戒綅锛屾棤闇€搴撳瓨銆佷笉鍐欏瓨妗ｃ€?
**[Mod](https://github.com/baagod/GBFR-Pre-Equipped-Sigils) 鐨勬湰璐ㄦ槸鎶婅繖浜?鈥滃繀甯︽Ы鈥?浠庣帺瀹剁殑棰勭畻閲屾娊璧帮紝鏈綋 12 妲戒綅鐣欑粰鐜╁鑷敱鍙戞尌锛屾瘡涓鑹查兘鑳藉鍑哄嚑鎴愰厤瑁呰嚜鐢便€?*

> **AI 杈呭姪寮€鍙戝０鏄?*锛氭湰 Mod 浠ｇ爜鐢?AI 鍔╂墜鍦ㄤ汉绫绘寚瀵间笅缂栧啓锛涢渶姹傝璁°€侀厤瑁呭唴瀹广€佹父鎴忓唴楠岃瘉涓庢枃妗ｇ敱浜虹被涓诲銆?
## 閰嶈

**鍏朵粬鐜╁鐪嬩笉鍒版墿灞曞洜瀛愶紝鍦ㄧ嚎娓哥帺鏃堕闄╄嚜璐熴€?*

1. 瑙夐啋+ ( 璇ヨ鑹蹭袱涓笓灞炶瘝鏉°€傚濞滈湶姊咃細鏂╁К姊﹀够 + 鏂╁К姝﹁壓 )
2. 婵€鏄?+ 瑙掕壊鎴樻皵 ( 瑙掕壊涓撳睘 )
3. 璞儐 + 鑷姩澶嶆椿
4. 涓嶅姩 + 鏄庨暅姝㈡按
5. 鍒氬仴 + 鑽按鎼哄甫鏁?6. 瀹堟姢 + 韬查伩鎬ц兘
7. 杩藉嚮 锛?杩呮嵎鑳藉姏
8. 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙

## 瀹夎

1. 瀹夎 [Reloaded-II](https://github.com/Reloaded-Project/Reloaded-II)锛?2. 鎶?zip 瑙ｅ帇鍒?Reloaded-II 鐨?Mods 鐩綍锛?3. 鍚敤 Mod 鍚庡惎鍔ㄦ父鎴忋€?
## 鑷磋阿锛圕redit锛?
- - 娲剧敓鑷?[GBFR Extra Sigil Slots](https://www.nexusmods.com/granbluefantasyrelink/mods/657) ( 浣滆€?Hiyajomaho-num9 )锛?*缁忎綔鑰呰鍙彂甯?*銆?- 鏁版嵁鏍稿疄鍙傝€冪ぞ鍖哄伐鍏烽摼锛歔Nenkai/relink-modding](https://nenkai.github.io/relink-modding/) ( 瀹樻柟 ID 琛?) 涓?[GBFRDataTools](https://github.com/Nenkai/GBFRDataTools) ( 瑙ｅ寘/瀵煎嚭 )銆?
---

## Build (for review)

Source: https://github.com/baagod/GBFR-Pre-Equipped-Sigils

Requirements: Windows x64, Visual Studio 2022 Build Tools (MSVC v143 + Windows SDK), .NET 8 SDK, Go, Node.js.

`powershell
powershell -ExecutionPolicy Bypass -File .\build-release.ps1
# outputs dist\GBFR-Pre-Equipped-Sigils-<version>.zip
`

The release package contains:
- `GBFR.PreEquippedSigils.dll` — C# (Reloaded-II mod hook, built by uild-release.ps1),
- `GBFR.PreEquippedSigils.Native.dll` — C++ (game hook, same script),
- `LoadoutTool.exe` — Wails v3 (Go) GUI tool (its frontend is also built by the script; no external assets are downloaded at build time).
