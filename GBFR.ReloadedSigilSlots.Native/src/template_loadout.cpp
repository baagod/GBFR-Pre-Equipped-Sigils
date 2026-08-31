#include "../native_internal.h"

namespace gbfr::native
{
namespace
{
// v0.2 built-in template loadout table.
//
// Template slots synthesize a GemData directly into the game's trait
// calculation; no inventory copy is required. The selected slot id stored in
// the character selection is a synthetic id in the kTemplateSlotIdBase range,
// decoded back to the virtual slot index by the trait hooks.
//
// Narmaya (娜露梅) full loadout:
//   slot 1: 斩姬之觉醒＋   (斩姬梦幻 + 斩姬武艺)
//   slot 2: 激昂Ⅴ＋       (激昂 + 斩姬的战气)
//   slot 3: 豪胆Ⅴ＋       (豪胆 + 自动复活)
//   slot 4: 不动Ⅴ＋       (不动 + 躲避性能)
//   slot 5: 坚持Ⅴ＋       (坚持 + 药水携带数)
// All traits at level 15 (V+).
constexpr CharacterTemplate kDefaultTemplates[] = {
   {
      0xE7053919, // Narmaya
      {
         TemplateGemSlot{
            0x1A57AEF1, // gem_id: 斩姬之觉醒＋
            0x29B07BEB, // trait1: 斩姬梦幻
            15,
            0xA63B89CD, // trait2: 斩姬武艺
            15,
            15,
         },
         TemplateGemSlot{
            0x04AC2281, // gem_id: 激昂Ⅴ＋
            0xB5FF9FD3, // trait1: 激昂
            15,
            0xFDD1AD24, // trait2: 斩姬的战气
            15,
            15,
         },
         TemplateGemSlot{
            0x335DA2A5, // gem_id: 豪胆Ⅴ＋
            0xE69A4694, // trait1: 豪胆
            15,
            0x95F3FA86, // trait2: 自动复活
            15,
            15,
         },
         TemplateGemSlot{
            0xB1CCC211, // gem_id: 不动Ⅴ＋
            0xB6E31F76, // trait1: 不动
            15,
            0x8B3BF60C, // trait2: 躲避性能
            15,
            15,
         },
         TemplateGemSlot{
            0x041D7B20, // gem_id: 坚持Ⅴ＋
            0x1470F860, // trait1: 坚持
            15,
            0x24883AF3, // trait2: 药水携带数
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
