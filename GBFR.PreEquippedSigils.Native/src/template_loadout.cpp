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
//   slot 2: character war spirit (single trait; 副词条 = 不选择)
//   slot 3: 激昂Ⅴ＋           (single trait; moved from the war-spirit slot)
//   slot 4: 豪胆Ⅴ＋           (豪胆 + 自动复活)
//   slot 5: 不动Ⅴ＋           (不动 + 明镜止水)
//   slot 6: 刚健Ⅴ＋           (刚健 + 药水携带数)
//   slot 7: 守护Ⅴ＋           (守护 + 躲避性能)
//   slot 8: 追击Ⅴ＋           (追击 + 迅捷能力)
//   slot 9: 可怕的漆黑钳蟹因子 Lv20 (event sigil, single trait)
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
      TemplateGemSlot{0x1791546F, 0xD76F4D24, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x0D21B430, { // character
      TemplateGemSlot{0x4F01D6CA, 0x6EBFA176, 15, 0xF1D5DBD0, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x6674C639, 0x4F135217, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x18E2F9F9, { // character
      TemplateGemSlot{0x9ADA3E00, 0x3BFED918, 15, 0xF8496336, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x621A2A97, 0x9AFDFA9E, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x1BB37EF0, { // character
      TemplateGemSlot{0x895ABBF6, 0x26956F25, 15, 0x1DE14C65, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x41AC1082, 0xDBA19768, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x22E437E5, { // character
      TemplateGemSlot{0xE19B1965, 0x8CDF9382, 15, 0xD1012D8C, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x8A3819C0, 0x6316CBEB, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x25D46F4B, { // character
      TemplateGemSlot{0xD8A464F1, 0x9ACE140B, 15, 0x7B5B081D, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x6B920DA2, 0x79266456, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x296471BE, { // character
      TemplateGemSlot{0x6AAE4B8F, 0x77C809F5, 15, 0x9230E3F5, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x67C1E5E3, 0x7B4FC47A, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x2A26B1B2, { // character
      TemplateGemSlot{0x52A6E299, 0xCD030268, 15, 0xA38510E2, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x0713D928, 0xDADE14DC, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xA4ACBA76, { // character
      TemplateGemSlot{0x52A6E299, 0xCD030268, 15, 0xA38510E2, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x0713D928, 0xDADE14DC, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x2EBE91D5, { // character
      TemplateGemSlot{0x673C5D8F, 0x2E65A774, 15, 0x16EFF868, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x3D8D9109, 0xD8F66C1C, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x4D0A60C3, { // character
      TemplateGemSlot{0xE2B380E5, 0xB48EEF48, 15, 0x11AAE5F5, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x43F26A91, 0xC00163B3, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x627BCB0D, { // character
      TemplateGemSlot{0xAB835493, 0x86CBCDC4, 15, 0x05FA4599, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x450B862C, 0xC7D379F1, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x646C3168, { // character
      TemplateGemSlot{0x5A360EA8, 0x30773197, 15, 0x47384248, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x2D70C37D, 0x807B6684, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x718E1A14, { // character
      TemplateGemSlot{0xB8C44D5E, 0xD40D1E9B, 15, 0x15806DFC, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x5D592FDD, 0x4E5F6706, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x74DD4C79, { // character
      TemplateGemSlot{0xA8A0CBFF, 0x06719232, 15, 0xED8D8AD8, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x4119F09B, 0x5559232F, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x978E4B18, { // character
      TemplateGemSlot{0xCE16D68B, 0x5463232F, 15, 0x451D814C, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x3069C2FE, 0x0F026CF0, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x9A8AF295, { // character
      TemplateGemSlot{0x95CC3CB8, 0xD176D262, 15, 0x461A8E07, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x51E98A7C, 0xB953CC1E, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0x9B15CFB1, { // character
      TemplateGemSlot{0x23953FD4, 0x7D75D904, 15, 0xBE3404B9, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x61A1A299, 0x3EB345D7, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xA3A3CB2F, { // character
      TemplateGemSlot{0xAF8E7E7E, 0x93A2093C, 15, 0x7AD0C010, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x98E9E6EF, 0xB064A634, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xAA66178A, { // character
      TemplateGemSlot{0x02B1F8C0, 0xEC3CF174, 15, 0xAF513A9D, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x5FB842E4, 0xE6B92E34, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xBAD16E3B, { // character
      TemplateGemSlot{0x8ECBB0A3, 0xE85FF8E0, 15, 0x8572B8AF, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0xAD8CAEFB, 0x81B293D9, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xBDEF7181, { // character
      TemplateGemSlot{0x02472C43, 0xE60A735C, 15, 0x6FF05223, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x4CDCE25B, 0xBA504607, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xC3FFD418, { // character
      TemplateGemSlot{0xB441275D, 0xD908223D, 15, 0x7351D602, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x40738CDD, 0xA339D642, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xC8616284, { // character
      TemplateGemSlot{0x9BD1CC24, 0x23D0F67F, 15, 0xC2A4C7A9, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x515E693C, 0x8519AD4A, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xDD7A151E, { // character
      TemplateGemSlot{0x1BBE919C, 0xAA83F548, 15, 0x921B6B0C, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x7C7EC053, 0x0E42BE1B, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xE7053919, { // character
      TemplateGemSlot{0x1A57AEF1, 0x29B07BEB, 15, 0xA63B89CD, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0xCEF31894, 0xFDD1AD24, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xF0EB77EF, { // character
      TemplateGemSlot{0xE4F986D9, 0x7440E869, 15, 0xCD124165, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x590C19C8, 0xD7F9BB88, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xFC6CDF7B, { // character
      TemplateGemSlot{0x119B24A8, 0x0CD6C625, 15, 0xA3B49220, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x76D4716B, 0xDAEFBB27, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
   { 0xFD3BE362, { // character
      TemplateGemSlot{0xAEEF8343, 0x9A9DC170, 15, 0x522E2388, 15, 15}, // slot1 awakening+ (2 exclusives)
      TemplateGemSlot{0x34C77091, 0xB85202BC, 15, 0x887AE0B0, 0, 15}, // slot2 war spirit (single trait)
      TemplateGemSlot{0x04AC2281, 0xB5FF9FD3, 15, 0x887AE0B0, 0, 15}, // slot3 general
      TemplateGemSlot{0x335DA2A5, 0xE69A4694, 15, 0x95F3FA86, 15, 15}, // slot4 general
      TemplateGemSlot{0xB1CCC211, 0xB6E31F76, 15, 0xD2C8E10A, 15, 15}, // slot5 general
      TemplateGemSlot{0x297D03F7, 0x74AA75D6, 15, 0x24883AF3, 15, 15}, // slot6 general
      TemplateGemSlot{0x35637B96, 0xE0ABFDFE, 15, 0x8B3BF60C, 15, 15}, // slot7 general
      TemplateGemSlot{0x1E2EBC39, 0x57AB5B10, 15, 0x318D12E9, 15, 15}, // slot8 general
      TemplateGemSlot{0x49434696, 0xBF78FBFC, 20, 0x887AE0B0, 0, 20}, // slot9 general
   }},
};






}

std::shared_mutex g_template_mutex;
std::array<CharacterTemplate, kRuntimeTemplateCapacity> g_runtime_templates{};

constexpr size_t kBuiltinTemplateCount =
   sizeof(kDefaultTemplates) / sizeof(kDefaultTemplates[0]);

void InitializeRuntimeTemplates()
{
   std::unique_lock lock(g_template_mutex);
   size_t index = 0;
   for (const CharacterTemplate& entry : kDefaultTemplates)
   {
      if (index >= g_runtime_templates.size())
         break;
      g_runtime_templates[index++] = entry;
   }
}

bool TryGetRuntimeSlot(
   uint32_t character_hash, int virtual_slot, TemplateGemSlot& out) noexcept
{
   if (virtual_slot < 0 ||
       virtual_slot >= g_virtual_slot_count.load(std::memory_order_acquire))
      return false;
   try
   {
      std::shared_lock lock(g_template_mutex);
      for (const CharacterTemplate& entry : g_runtime_templates)
      {
         if (entry.character_hash == character_hash)
         {
            out = entry.slots[static_cast<size_t>(virtual_slot)];
            return out.gem_id != 0;
         }
      }
   }
   catch (...)
   {
   }
   return false;
}

void InstallDefaultTemplateSelections()
{
   size_t installed = 0;
   {
      std::unique_lock lock(g_selection_mutex);
      for (const CharacterTemplate& character : g_runtime_templates)
      {
         if (character.character_hash == 0)
            continue;
         auto& slots = g_character_selections[character.character_hash];
         for (int index = 0; index < kVirtualSlotCapacity; ++index)
            slots[static_cast<size_t>(index)] = 0;
         for (int index = 0; index < kVirtualSlotCapacity; ++index)
         {
            const TemplateGemSlot& template_slot =
               character.slots[static_cast<size_t>(index)];
            if (template_slot.gem_id == 0)
               break; // The table is dense from virtual slot 0.
            slots[static_cast<size_t>(index)] = MakeTemplateSlotId(index);
            ++installed;
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
   TemplateGemSlot template_slot{};
   if (!TryGetRuntimeSlot(character_hash, virtual_slot, template_slot))
      return false;

   // Character-restricted template gems (e.g. awakening / war-spirit sigils)
   // must still honor the compatibility table. Unrestricted gems pass for any
   // character (required hash == 0).
   if (!IsCharacterCompatible(
          GetRequiredCharacterHash(template_slot.gem_id), character_hash))
      return false;

   GemData gem{};
   gem.trait1 = template_slot.trait1;
   gem.trait1_level = template_slot.trait1_level;
   gem.trait2 = template_slot.trait2;
   gem.trait2_level = template_slot.trait2_level;
   gem.gem_id = template_slot.gem_id;
   gem.worn_by = kUnwornCharacterHash;
   gem.sigil_level = template_slot.sigil_level;
   gem.slot_id = selected_slot_id;
   gem.flags = 0;
   return SafeCopyToOutput(gem, output);
}

bool ApplyCustomLoadout(const TemplateGemSlot* slots, int32_t count) noexcept
{
   const bool use_builtin = slots == nullptr || count <= 0;
   // Player configuration only fills GENERAL slots 2+; slots 0/1 (the
   // per-character awakening+ and war spirit) keep the built-in exclusives,
   // injected by the mod as always. Total virtual slots = 2 + config count.
   const int32_t effective_count =
      use_builtin
         ? kTemplateSlotCount
         : (count > kVirtualSlotCapacity - 2 ? kVirtualSlotCapacity - 2 : count);
   const int32_t total_slot_count =
      use_builtin ? kTemplateSlotCount : 2 + effective_count;
   const int32_t previous_count = g_virtual_slot_count.load(std::memory_order_acquire);
   if (total_slot_count != previous_count)
   {
      if (g_hooks_ready.load(std::memory_order_acquire) &&
          g_layout_ready.load(std::memory_order_acquire))
      {
         if (!ApplyTraitLoopLimits(total_slot_count))
            return false;
      }
      g_virtual_slot_count.store(total_slot_count, std::memory_order_release);
   }

   {
      std::unique_lock lock(g_template_mutex);
      size_t builtin_index = 0;
      for (CharacterTemplate& character : g_runtime_templates)
      {
         if (character.character_hash == 0)
            continue;
         if (use_builtin)
         {
            if (builtin_index < kBuiltinTemplateCount)
               character = kDefaultTemplates[builtin_index++];
            else
               character = CharacterTemplate{};
            continue;
         }
         const CharacterTemplate* builtin_entry = nullptr;
         for (const CharacterTemplate& entry : kDefaultTemplates)
         {
            if (entry.character_hash == character.character_hash)
            {
               builtin_entry = &entry;
               break;
            }
         }
         // Keep the character exclusives; configure the general slots.
         character.slots[0] =
            builtin_entry != nullptr ? builtin_entry->slots[0] : TemplateGemSlot{};
         character.slots[1] =
            builtin_entry != nullptr ? builtin_entry->slots[1] : TemplateGemSlot{};
         for (int32_t slot_index = 2; slot_index < kVirtualSlotCapacity; ++slot_index)
         {
            const int32_t config_index = slot_index - 2;
            character.slots[static_cast<size_t>(slot_index)] =
               config_index < effective_count
                  ? slots[static_cast<size_t>(config_index)]
                  : TemplateGemSlot{};
         }
      }
   }

   InstallDefaultTemplateSelections();
   if (g_hooks_ready.load(std::memory_order_acquire))
      ScheduleSelectedStatusRebind();
   return true;
}
}
