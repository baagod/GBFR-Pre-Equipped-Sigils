#include "../native_internal.h"

namespace gbfr::native
{
namespace
{
// v0.3 built-in template loadout table: all 29 playable characters.
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
//   slot 4: 不动Ⅴ＋           (不动 + 躲避性能)
//   slot 5: 坚持Ⅴ＋           (坚持 + 药水携带数)
//
// Djeeta (姬塔) shares Gran's captain exclusives (captain compatibility).
// Regenerate with docs/tool-gen-loadout.ps1 after changing the per-character
// table inside that script.
constexpr CharacterTemplate kDefaultTemplates[] = {
   {
      0x079DF0CC, // character
      {
         TemplateGemSlot{
            0x98A6D249, // gem_id: awakening+
            0x151E4674, // trait1
            15,
            0xA374FDF0, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xD76F4D24, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x0D21B430, // character
      {
         TemplateGemSlot{
            0x4F01D6CA, // gem_id: awakening+
            0x6EBFA176, // trait1
            15,
            0xF1D5DBD0, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x4F135217, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x18E2F9F9, // character
      {
         TemplateGemSlot{
            0x9ADA3E00, // gem_id: awakening+
            0x3BFED918, // trait1
            15,
            0xF8496336, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x9AFDFA9E, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x1BB37EF0, // character
      {
         TemplateGemSlot{
            0x895ABBF6, // gem_id: awakening+
            0x26956F25, // trait1
            15,
            0x1DE14C65, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xDBA19768, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x22E437E5, // character
      {
         TemplateGemSlot{
            0xE19B1965, // gem_id: awakening+
            0x8CDF9382, // trait1
            15,
            0xD1012D8C, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x6316CBEB, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x25D46F4B, // character
      {
         TemplateGemSlot{
            0xD8A464F1, // gem_id: awakening+
            0x9ACE140B, // trait1
            15,
            0x7B5B081D, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x79266456, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x296471BE, // character
      {
         TemplateGemSlot{
            0x6AAE4B8F, // gem_id: awakening+
            0x77C809F5, // trait1
            15,
            0x9230E3F5, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x7B4FC47A, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x2A26B1B2, // character
      {
         TemplateGemSlot{
            0x52A6E299, // gem_id: awakening+
            0xCD030268, // trait1
            15,
            0xA38510E2, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xDADE14DC, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xA4ACBA76, // character
      {
         TemplateGemSlot{
            0x52A6E299, // gem_id: awakening+
            0xCD030268, // trait1
            15,
            0xA38510E2, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xDADE14DC, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x2EBE91D5, // character
      {
         TemplateGemSlot{
            0x673C5D8F, // gem_id: awakening+
            0x2E65A774, // trait1
            15,
            0x16EFF868, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xD8F66C1C, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x4D0A60C3, // character
      {
         TemplateGemSlot{
            0xE2B380E5, // gem_id: awakening+
            0xB48EEF48, // trait1
            15,
            0x11AAE5F5, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xC00163B3, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x627BCB0D, // character
      {
         TemplateGemSlot{
            0xAB835493, // gem_id: awakening+
            0x86CBCDC4, // trait1
            15,
            0x05FA4599, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xC7D379F1, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x646C3168, // character
      {
         TemplateGemSlot{
            0x5A360EA8, // gem_id: awakening+
            0x30773197, // trait1
            15,
            0x47384248, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x807B6684, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x718E1A14, // character
      {
         TemplateGemSlot{
            0xB8C44D5E, // gem_id: awakening+
            0xD40D1E9B, // trait1
            15,
            0x15806DFC, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x4E5F6706, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x74DD4C79, // character
      {
         TemplateGemSlot{
            0xA8A0CBFF, // gem_id: awakening+
            0x06719232, // trait1
            15,
            0xED8D8AD8, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x5559232F, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x978E4B18, // character
      {
         TemplateGemSlot{
            0xCE16D68B, // gem_id: awakening+
            0x5463232F, // trait1
            15,
            0x451D814C, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x0F026CF0, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x9A8AF295, // character
      {
         TemplateGemSlot{
            0x95CC3CB8, // gem_id: awakening+
            0xD176D262, // trait1
            15,
            0x461A8E07, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xB953CC1E, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0x9B15CFB1, // character
      {
         TemplateGemSlot{
            0x23953FD4, // gem_id: awakening+
            0x7D75D904, // trait1
            15,
            0xBE3404B9, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x3EB345D7, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xA3A3CB2F, // character
      {
         TemplateGemSlot{
            0xAF8E7E7E, // gem_id: awakening+
            0x93A2093C, // trait1
            15,
            0x7AD0C010, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xB064A634, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xAA66178A, // character
      {
         TemplateGemSlot{
            0x02B1F8C0, // gem_id: awakening+
            0xEC3CF174, // trait1
            15,
            0xAF513A9D, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xE6B92E34, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xBAD16E3B, // character
      {
         TemplateGemSlot{
            0x8ECBB0A3, // gem_id: awakening+
            0xE85FF8E0, // trait1
            15,
            0x8572B8AF, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x81B293D9, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xBDEF7181, // character
      {
         TemplateGemSlot{
            0x02472C43, // gem_id: awakening+
            0xE60A735C, // trait1
            15,
            0x6FF05223, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xBA504607, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xC3FFD418, // character
      {
         TemplateGemSlot{
            0xB441275D, // gem_id: awakening+
            0xD908223D, // trait1
            15,
            0x7351D602, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xA339D642, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xC8616284, // character
      {
         TemplateGemSlot{
            0x9BD1CC24, // gem_id: awakening+
            0x23D0F67F, // trait1
            15,
            0xC2A4C7A9, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x8519AD4A, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xDD7A151E, // character
      {
         TemplateGemSlot{
            0x1BBE919C, // gem_id: awakening+
            0xAA83F548, // trait1
            15,
            0x921B6B0C, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0x0E42BE1B, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xE7053919, // character
      {
         TemplateGemSlot{
            0x1A57AEF1, // gem_id: awakening+
            0x29B07BEB, // trait1
            15,
            0xA63B89CD, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xFDD1AD24, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xF0EB77EF, // character
      {
         TemplateGemSlot{
            0xE4F986D9, // gem_id: awakening+
            0x7440E869, // trait1
            15,
            0xCD124165, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xD7F9BB88, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xFC6CDF7B, // character
      {
         TemplateGemSlot{
            0x119B24A8, // gem_id: awakening+
            0x0CD6C625, // trait1
            15,
            0xA3B49220, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xDAEFBB27, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
   {
      0xFD3BE362, // character
      {
         TemplateGemSlot{
            0xAEEF8343, // gem_id: awakening+
            0x9A9DC170, // trait1
            15,
            0x522E2388, // trait2
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: Inspire V+
            0xB5FF9FD3, // trait1: Inspire
            15,
            0xB85202BC, // trait2: war spirit
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5,
            0xE69A4694,
            15,
            0x95F3FA86,
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211,
            0xB6E31F76,
            15,
            0x8B3BF60C,
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20,
            0x1470F860,
            15,
            0x24883AF3,
            15,
            15,
         },
      },
   },
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
