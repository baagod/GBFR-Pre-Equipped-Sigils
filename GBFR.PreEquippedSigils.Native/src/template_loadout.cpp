#include "../native_internal.h"

namespace gbfr::native
{
namespace
{
// Built-in character-exclusive template: every playable character keeps its
// two exclusive sigil slots (slot 0 = awakening, slot 1 = war spirit; slot 0
// merges the two awakening traits T1+T2, or injects the single T1/T2 factor
// when the player picks only one of them). The general slots (3+) are no
// longer built-in: the player's loadout.json only; no config means the
// exclusive slots with no general sigils.
//
// Character-exclusive gems/traits follow sigils.json (the tool's source):
// T1/T2/war gem values are derived from it by docs/tool-gen-loadout.ps1.
//
// IMPORTANT: a "no second trait" gem must use trait2 = kUnwornCharacterHash
// (0x887AE0B0, the "not selected" sentinel the game understands), NOT 0.
// trait2 = 0 renders an extra empty Lv1 entry in the game's full-sigil list
// (observed 2026-09-02 on ER 2.0.5). trait1_level and sigil_level are
// independent: the former is the trait effect level, the latter the sigil's
// list display level.
//
// Djeeta (姬塔) shares Gran's captain exclusives (captain compatibility).
// Per-character unique template entries (slot 0 = awakening+,
// slot 1 = war spirit); slots 3-9 are the shared kGeneralSlots below.
// Bits for per-character exclusive overrides (default: all enabled).
enum ExclusiveState : uint8_t
{
   ExclusiveT1 = 1 << 0,
   ExclusiveT2 = 1 << 1,
   ExclusiveWar = 1 << 2,
   ExclusiveAll = ExclusiveT1 | ExclusiveT2 | ExclusiveWar,
};

struct CharacterExclusiveLoadout
{
   uint32_t character_hash = 0;
   TemplateGemSlot awakening{};   // slot 0: T1+T2 merged
   TemplateGemSlot war_spirit{};  // slot 1
   uint32_t awakening_t1_gem = 0; // independent T1 factor (single-only pick)
   uint32_t awakening_t2_gem = 0; // independent T2 factor (single-only pick)
};

// 由 docs/tool-gen-loadout.ps1 生成；勿手改。
constexpr CharacterExclusiveLoadout kCharacterExclusives[] = {
   { 0x079DF0CC, // character
      TemplateGemSlot{0x98A6D249, 0x151E4674, 15, 0xA374FDF0, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xBC53CE24, 0xD76F4D24, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x9F08F697, // t1 independent-factor gem
      0xD48ABDDA, // t2 independent-factor gem
   },
   { 0x0D21B430, // character
      TemplateGemSlot{0x4F01D6CA, 0x6EBFA176, 15, 0xF1D5DBD0, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xBFDF838C, 0x4F135217, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xB74C207B, // t1 independent-factor gem
      0x44D48479, // t2 independent-factor gem
   },
   { 0x18E2F9F9, // character
      TemplateGemSlot{0x9ADA3E00, 0x3BFED918, 15, 0xF8496336, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xAC175924, 0x9AFDFA9E, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x522004AB, // t1 independent-factor gem
      0x30A3F2EA, // t2 independent-factor gem
   },
   { 0x1BB37EF0, // character
      TemplateGemSlot{0x895ABBF6, 0x26956F25, 15, 0x1DE14C65, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x41AC1082, 0xDBA19768, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xF21404B1, // t1 independent-factor gem
      0x282DBFF0, // t2 independent-factor gem
   },
   { 0x22E437E5, // character
      TemplateGemSlot{0xE19B1965, 0x8CDF9382, 15, 0xD1012D8C, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x8A3819C0, 0x6316CBEB, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x85D7B335, // t1 independent-factor gem
      0xB5DA3E80, // t2 independent-factor gem
   },
   { 0x25D46F4B, // character
      TemplateGemSlot{0xD8A464F1, 0x9ACE140B, 15, 0x7B5B081D, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xEB766D87, 0x79266456, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x96D6FE5E, // t1 independent-factor gem
      0xEC9FFE77, // t2 independent-factor gem
   },
   { 0x296471BE, // character
      TemplateGemSlot{0x6AAE4B8F, 0x77C809F5, 15, 0x9230E3F5, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x9F72BAE0, 0x7B4FC47A, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x12DFD310, // t1 independent-factor gem
      0xAE9D89DF, // t2 independent-factor gem
   },
   { 0x2A26B1B2, // character
      TemplateGemSlot{0x52A6E299, 0xCD030268, 15, 0xA38510E2, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x0713D928, 0xDADE14DC, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x33F01810, // t1 independent-factor gem
      0x380A3CA8, // t2 independent-factor gem
   },
   { 0xA4ACBA76, // character
      TemplateGemSlot{0x52A6E299, 0xCD030268, 15, 0xA38510E2, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x0713D928, 0xDADE14DC, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x33F01810, // t1 independent-factor gem
      0x380A3CA8, // t2 independent-factor gem
   },
   { 0x2EBE91D5, // character
      TemplateGemSlot{0x673C5D8F, 0x2E65A774, 15, 0x16EFF868, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xA490BADF, 0xD8F66C1C, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x9D5BC5BF, // t1 independent-factor gem
      0xFB9B6DD5, // t2 independent-factor gem
   },
   { 0x4D0A60C3, // character
      TemplateGemSlot{0xE2B380E5, 0xB48EEF48, 15, 0x11AAE5F5, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x43F26A91, 0xC00163B3, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x9D88DEA1, // t1 independent-factor gem
      0xF6C0FCA5, // t2 independent-factor gem
   },
   { 0x627BCB0D, // character
      TemplateGemSlot{0xAB835493, 0x86CBCDC4, 15, 0x05FA4599, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xE21A4170, 0xC7D379F1, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xC0B5128E, // t1 independent-factor gem
      0xBCEDF060, // t2 independent-factor gem
   },
   { 0x646C3168, // character
      TemplateGemSlot{0x5A360EA8, 0x30773197, 15, 0x47384248, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x2D70C37D, 0x807B6684, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xBA28C81C, // t1 independent-factor gem
      0x64301E91, // t2 independent-factor gem
   },
   { 0x718E1A14, // character
      TemplateGemSlot{0xB8C44D5E, 0xD40D1E9B, 15, 0x15806DFC, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x5D592FDD, 0x4E5F6706, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x3EA4134B, // t1 independent-factor gem
      0x7E3A52A3, // t2 independent-factor gem
   },
   { 0x74DD4C79, // character
      TemplateGemSlot{0xA8A0CBFF, 0x06719232, 15, 0xED8D8AD8, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x9ABD2DA5, 0x5559232F, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x0523A202, // t1 independent-factor gem
      0x0723F7EC, // t2 independent-factor gem
   },
   { 0x978E4B18, // character
      TemplateGemSlot{0xCE16D68B, 0x5463232F, 15, 0x451D814C, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x3069C2FE, 0x0F026CF0, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x7D318FF7, // t1 independent-factor gem
      0x6CCA1FF7, // t2 independent-factor gem
   },
   { 0x9A8AF295, // character
      TemplateGemSlot{0x95CC3CB8, 0xD176D262, 15, 0x461A8E07, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x51E98A7C, 0xB953CC1E, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x9EC6C56D, // t1 independent-factor gem
      0xD4117FF3, // t2 independent-factor gem
   },
   { 0x9B15CFB1, // character
      TemplateGemSlot{0x23953FD4, 0x7D75D904, 15, 0xBE3404B9, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xD8C61507, 0x3EB345D7, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xF964A4CA, // t1 independent-factor gem
      0x1A359B67, // t2 independent-factor gem
   },
   { 0xA3A3CB2F, // character
      TemplateGemSlot{0xAF8E7E7E, 0x93A2093C, 15, 0x7AD0C010, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x98E9E6EF, 0xB064A634, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xB98A0F22, // t1 independent-factor gem
      0xEAA911B2, // t2 independent-factor gem
   },
   { 0xAA66178A, // character
      TemplateGemSlot{0x02B1F8C0, 0xEC3CF174, 15, 0xAF513A9D, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x66F1B128, 0xE6B92E34, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x14C58BF1, // t1 independent-factor gem
      0x147DA58B, // t2 independent-factor gem
   },
   { 0xBAD16E3B, // character
      TemplateGemSlot{0x8ECBB0A3, 0xE85FF8E0, 15, 0x8572B8AF, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xAD8CAEFB, 0x81B293D9, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xEB4AD96D, // t1 independent-factor gem
      0xDBE503C7, // t2 independent-factor gem
   },
   { 0xBDEF7181, // character
      TemplateGemSlot{0x02472C43, 0xE60A735C, 15, 0x6FF05223, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x4CDCE25B, 0xBA504607, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xB5725272, // t1 independent-factor gem
      0xC06F4708, // t2 independent-factor gem
   },
   { 0xC3FFD418, // character
      TemplateGemSlot{0xB441275D, 0xD908223D, 15, 0x7351D602, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xE496D882, 0xA339D642, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xE073EA65, // t1 independent-factor gem
      0xBF714A8A, // t2 independent-factor gem
   },
   { 0xC8616284, // character
      TemplateGemSlot{0x9BD1CC24, 0x23D0F67F, 15, 0xC2A4C7A9, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x515E693C, 0x8519AD4A, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x01D1A6CE, // t1 independent-factor gem
      0x21E10EB7, // t2 independent-factor gem
   },
   { 0xDD7A151E, // character
      TemplateGemSlot{0x1BBE919C, 0xAA83F548, 15, 0x921B6B0C, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xCAAE3F9C, 0x0E42BE1B, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0x64D63823, // t1 independent-factor gem
      0x05ACA892, // t2 independent-factor gem
   },
   { 0xE7053919, // character
      TemplateGemSlot{0x1A57AEF1, 0x29B07BEB, 15, 0xA63B89CD, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xCEF31894, 0xFDD1AD24, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xB143DAE6, // t1 independent-factor gem
      0xA879208F, // t2 independent-factor gem
   },
   { 0xF0EB77EF, // character
      TemplateGemSlot{0xE4F986D9, 0x7440E869, 15, 0xCD124165, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0xB3AB43F3, 0xD7F9BB88, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xFB0F9037, // t1 independent-factor gem
      0xA59C9613, // t2 independent-factor gem
   },
   { 0xFC6CDF7B, // character
      TemplateGemSlot{0x119B24A8, 0x0CD6C625, 15, 0xA3B49220, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x76D4716B, 0xDAEFBB27, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xE7624711, // t1 independent-factor gem
      0x49651C89, // t2 independent-factor gem
   },
   { 0xFD3BE362, // character
      TemplateGemSlot{0xAEEF8343, 0x9A9DC170, 15, 0x522E2388, 15, 15}, // slot0 awakening (t1+t2)
      TemplateGemSlot{0x4C28585A, 0xB85202BC, 15, 0x887AE0B0, 0, 15}, // slot1 war spirit (single)
      0xA0F94F69, // t1 independent-factor gem
      0x7C8580CA, // t2 independent-factor gem
   },
};

// character_hash -> index into g_runtime_templates (built once in
// InitializeRuntimeTemplates; ApplyCustomLoadout never reorders/removes
// entries, only rewrites their slots), so the hot getter path is O(1).
std::unordered_map<uint32_t, size_t> g_character_template_index;
// Per-character exclusive overrides (absent entry = ExclusiveAll), guarded by
// g_template_mutex and written by GBFR20_SetExclusiveOverrides.
std::unordered_map<uint32_t, uint8_t> g_exclusive_state;

TemplateGemSlot MakeSingleTraitSlot(uint32_t gem_id, uint32_t trait) noexcept
{
   TemplateGemSlot slot{};
   slot.gem_id = gem_id;
   slot.trait1 = trait;
   slot.trait1_level = 15;
   slot.trait2 = kUnwornCharacterHash;
   slot.trait2_level = 0;
   slot.sigil_level = 15;
   return slot;
}

// state bits: T1/T2 both enabled -> merged awakening (slot 0); single T1 or T2
// -> the independent factor gem; war -> slot 1. Other slots stay empty (they
// are filled by the player loadout in ApplyCustomLoadout).
CharacterTemplate BuildCharacterTemplate(
   const CharacterExclusiveLoadout& exclusive, uint8_t state) noexcept
{
   CharacterTemplate character{};
   character.character_hash = exclusive.character_hash;
   const bool t1 = (state & ExclusiveT1) != 0;
   const bool t2 = (state & ExclusiveT2) != 0;
   if (t1 && t2)
      character.slots[0] = exclusive.awakening;
   else if (t1)
      character.slots[0] = MakeSingleTraitSlot(
         exclusive.awakening_t1_gem, exclusive.awakening.trait1);
   else if (t2)
      character.slots[0] = MakeSingleTraitSlot(
         exclusive.awakening_t2_gem, exclusive.awakening.trait2);
   character.slots[1] =
      (state & ExclusiveWar) != 0 ? exclusive.war_spirit : TemplateGemSlot{};
   return character;
}

// Requires g_template_mutex held by the caller.
uint8_t ReadExclusiveStateLocked(uint32_t character_hash) noexcept
{
   const auto iterator = g_exclusive_state.find(character_hash);
   if (iterator == g_exclusive_state.end())
      return ExclusiveAll;
   return iterator->second & ExclusiveAll;
}

// Requires g_template_mutex held by the caller.
void ApplyExclusiveStateLocked(
   uint32_t character_hash, CharacterTemplate& character) noexcept
{
   const auto index = g_character_template_index.find(character_hash);
   if (index == g_character_template_index.end())
      return;
   const CharacterTemplate built = BuildCharacterTemplate(
      kCharacterExclusives[index->second],
      ReadExclusiveStateLocked(character.character_hash));
   character.slots[0] = built.slots[0];
   character.slots[1] = built.slots[1];
}







}

std::shared_mutex g_template_mutex;
std::array<CharacterTemplate, kRuntimeTemplateCapacity> g_runtime_templates{};

constexpr size_t kBuiltinTemplateCount =
   sizeof(kCharacterExclusives) / sizeof(kCharacterExclusives[0]);

void InitializeRuntimeTemplates()
{
   std::unique_lock lock(g_template_mutex);
   g_character_template_index.clear();
   size_t index = 0;
   for (const CharacterExclusiveLoadout& entry : kCharacterExclusives)
   {
      if (index >= g_runtime_templates.size())
         break;
      g_runtime_templates[index] =
         BuildCharacterTemplate(entry, ReadExclusiveStateLocked(entry.character_hash));
      g_character_template_index.emplace(entry.character_hash, index);
      ++index;
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
      const auto iterator = g_character_template_index.find(character_hash);
      if (iterator == g_character_template_index.end())
         return false;
      const CharacterTemplate& entry =
         g_runtime_templates[iterator->second];
      out = entry.slots[static_cast<size_t>(virtual_slot)];
      return out.gem_id != 0;
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
   // nullptr = no player config -> exclusive-only built-in template (per
   // character the exclusive slots assembled from the overrides, general
   // slots empty). Non-null (even with count 0) = player config present.
   const bool use_builtin = slots == nullptr;
   // Player configuration only fills general slots kBuiltinExclusiveSlotCount+;
   // slots 0/1 are assembled per character from the exclusives + overrides.
   const int32_t effective_count =
      use_builtin || count <= 0
         ? 0
         : (count > kVirtualSlotCapacity - kBuiltinExclusiveSlotCount
               ? kVirtualSlotCapacity - kBuiltinExclusiveSlotCount
               : count);
   const int32_t total_slot_count =
      use_builtin
         ? kBuiltinExclusiveSlotCount
         : kBuiltinExclusiveSlotCount + effective_count;
   const int32_t previous_count = g_virtual_slot_count.load(std::memory_order_acquire);
   if (total_slot_count != previous_count)
   {
      // Publish the new count before widening/narrowing the game's trait loop
      // limit: the detour gates virtual slots on the count, so it must already
      // match the patch game threads observe on their next loop iteration.
      g_virtual_slot_count.store(total_slot_count, std::memory_order_release);
      if (g_hooks_ready.load(std::memory_order_acquire) &&
          g_layout_ready.load(std::memory_order_acquire))
      {
         if (!ApplyTraitLoopLimits(total_slot_count))
         {
            g_virtual_slot_count.store(previous_count, std::memory_order_release);
            return false;
         }
      }
   }

   {
      std::unique_lock lock(g_template_mutex);
      for (CharacterTemplate& character : g_runtime_templates)
      {
         if (character.character_hash == 0)
            continue;
         ApplyExclusiveStateLocked(character.character_hash, character);
         if (use_builtin)
            continue;
         for (int32_t slot_index = kBuiltinExclusiveSlotCount;
              slot_index < kVirtualSlotCapacity; ++slot_index)
         {
            const int32_t config_index =
               slot_index - kBuiltinExclusiveSlotCount;
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

bool ApplyExclusiveOverrides(
   const GBFR20_ExclusiveOverride* overrides, int32_t count) noexcept
{
   {
      std::unique_lock lock(g_template_mutex);
      g_exclusive_state.clear();
      for (int32_t index = 0; index < count && overrides != nullptr; ++index)
      {
         const GBFR20_ExclusiveOverride& override = overrides[index];
         if (override.character_hash == 0)
            continue;
         uint8_t state = ExclusiveAll;
         if (override.disable_t1)
            state &= ~ExclusiveT1;
         if (override.disable_t2)
            state &= ~ExclusiveT2;
         if (override.disable_war)
            state &= ~ExclusiveWar;
         g_exclusive_state[override.character_hash] = state;
      }
      for (CharacterTemplate& character : g_runtime_templates)
      {
         if (character.character_hash != 0)
            ApplyExclusiveStateLocked(character.character_hash, character);
      }
   }
   InstallDefaultTemplateSelections();
   if (g_hooks_ready.load(std::memory_order_acquire))
      ScheduleSelectedStatusRebind();
   return true;
}
}
