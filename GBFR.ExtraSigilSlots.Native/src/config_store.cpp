#include "../native_internal.h"

#include <cwchar>
#include <iomanip>
#include <sstream>

namespace gbfr::native
{
UiSettings g_settings;
std::mutex g_settings_mutex;
std::atomic_int32_t g_virtual_slot_count{kDefaultVirtualSlotCount};

namespace
{
std::wstring ReadIniString(const wchar_t* section, const wchar_t* key, const wchar_t* fallback)
{
   std::array<wchar_t, 1024> buffer{};
   GetPrivateProfileStringW(
      section,
      key,
      fallback,
      buffer.data(),
      static_cast<DWORD>(buffer.size()),
      g_config_path.c_str());
   return buffer.data();
}

std::wstring ReadIniStringDynamic(
   const wchar_t* section,
   const wchar_t* key,
   const wchar_t* fallback)
{
   size_t capacity = 256;
   while (capacity <= 65536)
   {
      std::vector<wchar_t> buffer(capacity, L'\0');
      const DWORD copied = GetPrivateProfileStringW(
         section,
         key,
         fallback,
         buffer.data(),
         static_cast<DWORD>(buffer.size()),
         g_config_path.c_str());
      if (copied < buffer.size() - 1)
         return std::wstring(buffer.data(), copied);
      capacity *= 2;
   }
   return L"<invalid-overlong-value>";
}

void WriteIniInt(const wchar_t* section, const wchar_t* key, int value)
{
   const std::wstring text = std::to_wstring(value);
   WritePrivateProfileStringW(section, key, text.c_str(), g_config_path.c_str());
}

std::wstring CharacterSectionName(uint32_t character_hash)
{
   wchar_t buffer[32]{};
   swprintf_s(buffer, L"Character_%08X", character_hash);
   return buffer;
}

std::array<uint32_t, kVirtualSlotCapacity> ParseSlots(std::wstring_view text)
{
   std::array<uint32_t, kVirtualSlotCapacity> slots{};
   size_t slot_index = 0;
   size_t offset = 0;
   while (slot_index < slots.size() && offset <= text.size())
   {
      const size_t comma = text.find(L',', offset);
      const std::wstring token(text.substr(
         offset,
         comma == std::wstring_view::npos ? text.size() - offset : comma - offset));
      wchar_t* end = nullptr;
      const unsigned long value = std::wcstoul(token.c_str(), &end, 16);
      if (end != token.c_str())
         slots[slot_index] = static_cast<uint32_t>(value);
      ++slot_index;
      if (comma == std::wstring_view::npos)
         break;
      offset = comma + 1;
   }
   return slots;
}

int ParseConfiguredVirtualSlotCount(std::wstring_view raw, bool& rewrite)
{
   const auto is_space = [](wchar_t character) {
      return character == L' ' || character == L'\t' || character == L'\r' ||
         character == L'\n' || character == L'\v' || character == L'\f';
   };
   size_t begin = 0;
   while (begin < raw.size() && is_space(raw[begin]))
      ++begin;
   size_t end = raw.size();
   while (end > begin && is_space(raw[end - 1]))
      --end;
   const std::wstring_view text = raw.substr(begin, end - begin);
   if (text.empty())
   {
      rewrite = true;
      return kDefaultVirtualSlotCount;
   }

   uint32_t value = 0;
   bool exceeds_capacity = false;
   for (const wchar_t character : text)
   {
      if (character < L'0' || character > L'9')
      {
         rewrite = true;
         return kDefaultVirtualSlotCount;
      }
      if (exceeds_capacity)
         continue;
      const uint32_t digit = static_cast<uint32_t>(character - L'0');
      if (value > static_cast<uint32_t>((kVirtualSlotCapacity - digit) / 10))
      {
         exceeds_capacity = true;
         continue;
      }
      value = value * 10 + digit;
      if (value > kVirtualSlotCapacity)
         exceeds_capacity = true;
   }

   const int normalized = exceeds_capacity || value > kVirtualSlotCapacity
      ? kVirtualSlotCapacity
      : value == 0 ? 1 : static_cast<int>(value);
   rewrite = text != std::to_wstring(normalized);
   return normalized;
}
}

void SaveCharacterSelection(uint32_t character_hash)
{
   std::array<uint32_t, kVirtualSlotCapacity> slots{};
   {
      std::shared_lock lock(g_selection_mutex);
      const auto iterator = g_character_selections.find(character_hash);
      if (iterator != g_character_selections.end())
         slots = iterator->second;
   }

   std::wostringstream stream;
   stream << std::uppercase << std::hex << std::setfill(L'0');
   const size_t active_slot_count = static_cast<size_t>(GetVirtualSlotCount());
   for (size_t index = 0; index < active_slot_count; ++index)
   {
      if (index != 0)
         stream << L',';
      stream << std::setw(8) << slots[index];
   }
   const std::wstring section = CharacterSectionName(character_hash);
   const std::wstring value = stream.str();
   WritePrivateProfileStringW(section.c_str(), L"Slots", value.c_str(), g_config_path.c_str());
}

void LoadSettingsAndSelections()
{
   UiSettings settings;
   const int stored_settings_version = static_cast<int>(GetPrivateProfileIntW(
      L"Settings", L"ConfigVersion", 0, g_config_path.c_str()));
   int toggle_key = static_cast<int>(GetPrivateProfileIntW(
      L"Settings", L"ToggleKey", VK_F8, g_config_path.c_str()));
   if (stored_settings_version < kCurrentSettingsVersion && toggle_key == VK_NUMPAD8)
   {
      toggle_key = VK_F8;
      WriteIniInt(L"Settings", L"ToggleKey", toggle_key);
   }
   if (stored_settings_version < kCurrentSettingsVersion)
      WriteIniInt(L"Settings", L"ConfigVersion", kCurrentSettingsVersion);
   settings.toggle_key = std::clamp(toggle_key, 1, 255);
   settings.show_equipped =
      GetPrivateProfileIntW(L"Settings", L"ShowEquipped", 0, g_config_path.c_str()) != 0;
   settings.auto_apply = true;
   if (GetPrivateProfileIntW(L"Settings", L"AutoApply", 1, g_config_path.c_str()) == 0)
      WriteIniInt(L"Settings", L"AutoApply", 1);
   const std::string configured_language =
      WideToUtf8(ReadIniString(L"Settings", L"Language", L"zh-CN"));
   settings.language = configured_language == "en" ? "en" : "zh-CN";
   bool rewrite_virtual_slot_count = false;
   settings.virtual_slot_count = ParseConfiguredVirtualSlotCount(
      ReadIniStringDynamic(L"Settings", L"VirtualSlotCount", L""),
      rewrite_virtual_slot_count);
   if (rewrite_virtual_slot_count)
      WriteIniInt(L"Settings", L"VirtualSlotCount", settings.virtual_slot_count);
   g_virtual_slot_count.store(settings.virtual_slot_count, std::memory_order_release);
   {
      std::scoped_lock lock(g_settings_mutex);
      g_settings = std::move(settings);
   }

   std::vector<wchar_t> section_names(65536, L'\0');
   const DWORD copied = GetPrivateProfileSectionNamesW(
      section_names.data(), static_cast<DWORD>(section_names.size()), g_config_path.c_str());
   if (copied == 0)
      return;

   std::unique_lock lock(g_selection_mutex);
   for (const wchar_t* section = section_names.data(); *section != L'\0';
        section += std::wcslen(section) + 1)
   {
      constexpr std::wstring_view prefix = L"Character_";
      const std::wstring_view name(section);
      if (!name.starts_with(prefix) || name.size() != prefix.size() + 8)
         continue;
      wchar_t* end = nullptr;
      const uint32_t hash = static_cast<uint32_t>(
         std::wcstoul(section + prefix.size(), &end, 16));
      if (hash == 0 || end == section + prefix.size())
         continue;
      g_character_selections[hash] = ParseSlots(ReadIniString(section, L"Slots", L""));
   }

   std::vector<uint32_t> character_hashes;
   character_hashes.reserve(g_character_selections.size());
   for (const auto& [hash, slots] : g_character_selections)
      character_hashes.push_back(hash);
   std::sort(character_hashes.begin(), character_hashes.end());

   std::unordered_set<uint32_t> claimed_slot_ids;
   std::unordered_set<uint32_t> changed_characters;
   const int active_slot_count = GetVirtualSlotCount();
   g_virtual_owner_by_slot_id.clear();
   for (const uint32_t hash : character_hashes)
   {
      auto& slots = g_character_selections[hash];
      for (int index = 0; index < active_slot_count; ++index)
      {
         uint32_t& slot_id = slots[static_cast<size_t>(index)];
         if (slot_id == 0)
            continue;
         if (!claimed_slot_ids.emplace(slot_id).second)
         {
            slot_id = 0;
            changed_characters.emplace(hash);
            continue;
         }
         g_virtual_owner_by_slot_id[slot_id] = {hash, index};
      }
      for (int index = active_slot_count; index < kVirtualSlotCapacity; ++index)
      {
         uint32_t& inactive_slot_id = slots[static_cast<size_t>(index)];
         if (inactive_slot_id == 0)
            continue;
         inactive_slot_id = 0;
         changed_characters.emplace(hash);
      }
   }
   lock.unlock();
   for (const uint32_t hash : changed_characters)
      SaveCharacterSelection(hash);
}

void SaveUiSettings()
{
   UiSettings settings;
   {
      std::scoped_lock lock(g_settings_mutex);
      settings = g_settings;
   }
   WriteIniInt(L"Settings", L"ConfigVersion", kCurrentSettingsVersion);
   WriteIniInt(L"Settings", L"ToggleKey", settings.toggle_key);
   WriteIniInt(L"Settings", L"ShowEquipped", settings.show_equipped ? 1 : 0);
   WriteIniInt(L"Settings", L"AutoApply", settings.auto_apply ? 1 : 0);
   WriteIniInt(L"Settings", L"VirtualSlotCount", settings.virtual_slot_count);
   const std::wstring language(settings.language.begin(), settings.language.end());
   WritePrivateProfileStringW(
      L"Settings", L"Language", language.c_str(), g_config_path.c_str());
}
}
