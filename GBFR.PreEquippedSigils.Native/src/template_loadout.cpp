#include "../native_internal.h"

namespace gbfr::native
{
namespace
{
// v0.3 built-in template loadout table: all playable characters.
//
// Template slots synthesize a GemData directly into the game's trait
// calculation; no inventory copy is required. The selected slot id stored in
// the character selection is a synthetic id in the kTemplateSlotIdBase range,
// decoded back to the virtual slot index by the trait hooks.
//
// Loadout per character (all traits level 15 = V+):
//   slot 1: character awakening+ (two character-exclusive traits)
//   slot 2: 激昂Ⅴ＋           (激昂 + character war spirit)
//   slot 3: 豪胆Ⅴ＋           (豪胆 + 自动复活)
//   slot 4: 不动Ⅴ＋           (不动 + 明镜止水)
//   slot 5: 刚健Ⅴ＋           (刚健 + 药水携带数)
//   slot 6: 守护Ⅴ＋           (守护 + 躲避性能)
//   slot 7: 可怕的漆黑钳蟹因子 Lv20 (event sigil, single trait)
//
// IMPORTANT: a "no second trait" gem must use trait2 = kUnwornCharacterHash
// (0x887AE0B0, the "not selected" sentinel the game understands), NOT 0.
// trait2 = 0 renders an extra empty Lv1 entry in the game's full-sigil list
// (observed 2026-09-02 on ER 2.0.5; fixed after checking the in-game modifier
// which maps "不选择" to 0x887AE0B0). trait1_level and sigil_level are
// independent: the former is the trait effect level, the latter the sigil's
// list display level (event sigils show "-" when equipped but Lv20 in the
// full list).
//
// Djeeta (姬塔) shares Gran's captain exclusives (captain compatibility).
// Regenerate with docs/tool-gen-loadout.ps1 after changing the per-character
// table inside that script.
constexpr CharacterTemplate kDefaultTemplates[] = {
   { 0x079DF0CC, { // character
      TemplateGemSlot{0x98A6D249, 0x151E4674, 15, 0xA374FDF0, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xD76F4D24, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x0D21B430, { // character
      TemplateGemSlot{0x4F01D6CA, 0x6EBFA176, 15, 0xF1D5DBD0, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x4F135217, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x18E2F9F9, { // character
      TemplateGemSlot{0x9ADA3E00, 0x3BFED918, 15, 0xF8496336, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x9AFDFA9E, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x1BB37EF0, { // character
      TemplateGemSlot{0x895ABBF6, 0x26956F25, 15, 0x1DE14C65, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xDBA19768, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x22E437E5, { // character
      TemplateGemSlot{0xE19B1965, 0x8CDF9382, 15, 0xD1012D8C, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x6316CBEB, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x25D46F4B, { // character
      TemplateGemSlot{0xD8A464F1, 0x9ACE140B, 15, 0x7B5B081D, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x79266456, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x296471BE, { // character
      TemplateGemSlot{0x6AAE4B8F, 0x77C809F5, 15, 0x9230E3F5, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x7B4FC47A, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x2A26B1B2, { // character
      TemplateGemSlot{0x52A6E299, 0xCD030268, 15, 0xA38510E2, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xDADE14DC, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xA4ACBA76, { // character
      TemplateGemSlot{0x52A6E299, 0xCD030268, 15, 0xA38510E2, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xDADE14DC, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x2EBE91D5, { // character
      TemplateGemSlot{0x673C5D8F, 0x2E65A774, 15, 0x16EFF868, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xD8F66C1C, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x4D0A60C3, { // character
      TemplateGemSlot{0xE2B380E5, 0xB48EEF48, 15, 0x11AAE5F5, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xC00163B3, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x627BCB0D, { // character
      TemplateGemSlot{0xAB835493, 0x86CBCDC4, 15, 0x05FA4599, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xC7D379F1, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x646C3168, { // character
      TemplateGemSlot{0x5A360EA8, 0x30773197, 15, 0x47384248, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x807B6684, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x718E1A14, { // character
      TemplateGemSlot{0xB8C44D5E, 0xD40D1E9B, 15, 0x15806DFC, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x4E5F6706, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x74DD4C79, { // character
      TemplateGemSlot{0xA8A0CBFF, 0x06719232, 15, 0xED8D8AD8, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x5559232F, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x978E4B18, { // character
      TemplateGemSlot{0xCE16D68B, 0x5463232F, 15, 0x451D814C, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x0F026CF0, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x9A8AF295, { // character
      TemplateGemSlot{0x95CC3CB8, 0xD176D262, 15, 0x461A8E07, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xB953CC1E, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0x9B15CFB1, { // character
      TemplateGemSlot{0x23953FD4, 0x7D75D904, 15, 0xBE3404B9, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x3EB345D7, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xA3A3CB2F, { // character
      TemplateGemSlot{0xAF8E7E7E, 0x93A2093C, 15, 0x7AD0C010, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xB064A634, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xAA66178A, { // character
      TemplateGemSlot{0x02B1F8C0, 0xEC3CF174, 15, 0xAF513A9D, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xE6B92E34, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xBAD16E3B, { // character
      TemplateGemSlot{0x8ECBB0A3, 0xE85FF8E0, 15, 0x8572B8AF, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x81B293D9, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xBDEF7181, { // character
      TemplateGemSlot{0x02472C43, 0xE60A735C, 15, 0x6FF05223, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xBA504607, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xC3FFD418, { // character
      TemplateGemSlot{0xB441275D, 0xD908223D, 15, 0x7351D602, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xA339D642, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xC8616284, { // character
      TemplateGemSlot{0x9BD1CC24, 0x23D0F67F, 15, 0xC2A4C7A9, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x8519AD4A, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xDD7A151E, { // character
      TemplateGemSlot{0x1BBE919C, 0xAA83F548, 15, 0x921B6B0C, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x0E42BE1B, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xE7053919, { // character
      TemplateGemSlot{0x1A57AEF1, 0x29B07BEB, 15, 0xA63B89CD, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xFDD1AD24, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xF0EB77EF, { // character
      TemplateGemSlot{0xE4F986D9, 0x7440E869, 15, 0xCD124165, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xD7F9BB88, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xFC6CDF7B, { // character
      TemplateGemSlot{0x119B24A8, 0x0CD6C625, 15, 0xA3B49220, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xDAEFBB27, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
   { 0xFD3BE362, { // character
      TemplateGemSlot{0xAEEF8343, 0x9A9DC170, 15, 0x522E2388, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0xB85202BC, 15, 15}, // slot2 Inspire V+ + war spirit)
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot3 Guts V+ + Autorevive)
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot4 Steadfast V+ + Perfect Dodge)
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot5 Sturdy V+ + Potion Hoarder)
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot6 Guardian V+ + Improved Dodging)
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot7 鍙€曠殑婕嗛粦閽宠煿鍥犲瓙 Lv20 (event sigil)
   }},
};






}

const CharacterTemplate* FindCharacterTemplate(uint32_t character_hash) noexcept
{
   for (const CharacterTemplate& entry : kDefaultTemplates)
   {
      if (entry.character_hash == character_hash)
         return &entry;
   }
   return nullptr;
}

const TemplateGemSlot* FindTemplateSlot(
   uint32_t character_hash, int virtual_slot) noexcept
{
   if (virtual_slot < 0 || virtual_slot >= kVirtualSlotCapacity)
      return nullptr;
   const CharacterTemplate* character = FindCharacterTemplate(character_hash);
   if (character == nullptr)
      return nullptr;
   const TemplateGemSlot& slot =
      character->slots[static_cast<size_t>(virtual_slot)];
   return slot.gem_id == 0 ? nullptr : &slot;
}

void InstallDefaultTemplateSelections()
{
   size_t installed = 0;
   {
      std::unique_lock lock(g_selection_mutex);
      for (const CharacterTemplate& character : kDefaultTemplates)
      {
         auto& slots = g_character_selections[character.character_hash];
         for (int index = 0; index < kVirtualSlotCapacity; ++index)
         {
            const TemplateGemSlot& template_slot =
               character.slots[static_cast<size_t>(index)];
            if (template_slot.gem_id == 0)
               break; // The table is dense from virtual slot 0.
            if (slots[static_cast<size_t>(index)] == 0)
            {
               slots[static_cast<size_t>(index)] = MakeTemplateSlotId(index);
               ++installed;
            }
         }
      }
   }
   Log(
      "Installed " + std::to_string(installed) +
      " built-in template loadout selection(s); inventory-independent.");
}

bool TryCopyTemplateGem(
   uint32_t character_hash, uint32_t selected_slot_id, void* output) noexcept
{
   if (output == nullptr || !IsTemplateSlotId(selected_slot_id))
      return false;

   const int virtual_slot =
      static_cast<int>(selected_slot_id - kTemplateSlotIdBase);
   const TemplateGemSlot* template_slot =
      FindTemplateSlot(character_hash, virtual_slot);
   if (template_slot == nullptr)
      return false;

   // Character-restricted template gems (e.g. awakening / war-spirit sigils)
   // must still honor the compatibility table. Unrestricted gems pass for any
   // character (required hash == 0).
   if (!IsCharacterCompatible(
          GetRequiredCharacterHash(template_slot->gem_id), character_hash))
      return false;

   GemData gem{};
   gem.trait1 = template_slot->trait1;
   gem.trait1_level = template_slot->trait1_level;
   gem.trait2 = template_slot->trait2;
   gem.trait2_level = template_slot->trait2_level;
   gem.gem_id = template_slot->gem_id;
   gem.worn_by = kUnwornCharacterHash;
   gem.sigil_level = template_slot->sigil_level;
   gem.slot_id = selected_slot_id;
   gem.flags = 0;
   return SafeCopyToOutput(gem, output);
}
}
