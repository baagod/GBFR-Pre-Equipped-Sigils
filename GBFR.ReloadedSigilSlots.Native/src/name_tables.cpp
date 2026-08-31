#include "../native_internal.h"

#include <charconv>
#include <fstream>

namespace gbfr::native
{
std::unordered_map<uint32_t, uint32_t> g_required_character_by_gem;

bool LoadCompatibilityTable(const std::filesystem::path& path)
{
   g_required_character_by_gem.clear();
   std::ifstream stream(path, std::ios::binary);
   if (!stream)
   {
      Log("Compatibility table is missing: " + path.filename().string());
      return false;
   }

   std::string line;
   uint32_t loaded = 0;
   while (std::getline(stream, line))
   {
      if (!line.empty() && line.back() == '\r')
         line.pop_back();
      if (line.empty() || line.front() == '#')
         continue;
      const size_t first_tab = line.find('\t');
      const size_t second_tab =
         first_tab == std::string::npos ? std::string::npos : line.find('\t', first_tab + 1);
      if (first_tab == std::string::npos)
         continue;
      const std::string_view gem_text(line.data(), first_tab);
      const size_t character_end = second_tab == std::string::npos ? line.size() : second_tab;
      const std::string_view character_text(
         line.data() + first_tab + 1, character_end - first_tab - 1);
      uint32_t gem_hash = 0;
      uint32_t character_hash = 0;
      const auto gem_conversion = std::from_chars(
         gem_text.data(), gem_text.data() + gem_text.size(), gem_hash, 16);
      const auto character_conversion = std::from_chars(
         character_text.data(),
         character_text.data() + character_text.size(),
         character_hash,
         16);
      if (gem_conversion.ec != std::errc{} ||
          character_conversion.ec != std::errc{} ||
          gem_hash == 0 || character_hash == 0)
         continue;
      g_required_character_by_gem[gem_hash] = character_hash;
      ++loaded;
   }
   Log("Loaded " + std::to_string(loaded) + " character-restricted sigil mappings.");
   return loaded == kExpectedCompatibilityMappingCount &&
      g_required_character_by_gem.size() == kExpectedCompatibilityMappingCount;
}

uint32_t GetRequiredCharacterHash(uint32_t gem_hash)
{
   const auto iterator = g_required_character_by_gem.find(gem_hash);
   return iterator == g_required_character_by_gem.end() ? 0 : iterator->second;
}
}
