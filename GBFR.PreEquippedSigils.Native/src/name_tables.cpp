#include "../native_internal.h"

#include <charconv>
#include <fstream>

namespace gbfr::native
{
std::unordered_map<uint32_t, uint32_t> g_required_character_by_gem;

namespace
{
// Extracts an 8-digit hex value from a flat JSON line, e.g.
//   "hash": "9F08F697",            -> value = 0x9F08F697
//   "character": "079DF0CC"       -> value = 0x079DF0CC
// Returns false when the field is absent or not exactly 8 hex digits.
bool ReadHexField(const std::string& line, std::string_view field, uint32_t& value) noexcept
{
   const std::string prefix = std::string("\"") + std::string(field) + "\":";
   const size_t pos = line.find(prefix);
   if (pos == std::string::npos)
      return false;
   size_t cursor = pos + prefix.size();
   while (cursor < line.size() && (line[cursor] == ' ' || line[cursor] == '\t'))
      ++cursor;
   if (cursor >= line.size() || line[cursor] != '"')
      return false;
   ++cursor;
   uint32_t parsed = 0;
   const auto result = std::from_chars(
      line.data() + cursor, line.data() + std::min(line.size(), cursor + 8), parsed, 16);
   if (result.ec != std::errc{} || result.ptr != line.data() + cursor + 8)
      return false;
   // The value must be terminated by the closing quote; a longer hex segment
   // is a layout deviation and must fail closed, not be silently truncated.
   if (cursor + 8 >= line.size() || line[cursor + 8] != '"')
      return false;
   value = parsed;
   return true;
}
}

// Contract-based loader from the tool's merged table (sigils.json, produced by
// the extract pipeline; field names follow gem.xlsx headers).
//
// == FORMAT CONTRACT (change here in lockstep with gen\数据表说明.md §2) ==
//  - flat JSON objects, one field per line, UTF-8, LF or CRLF
//  - field names: "hash" and "character", values are 8 hex digits (no 0x)
//  - exclusive rows (player != "") also carry "character"; regular rows do not
//  - the "character" field is the last one of the object (no trailing comma)
// The loader never parses generic JSON: it scans the stable field layout and
// fails closed on any deviation (expected count check below).
bool LoadCompatibilityTable(const std::filesystem::path& path)
{
   g_required_character_by_gem.clear();
   std::ifstream stream(path, std::ios::binary);
   if (!stream)
   {
      Log("sigils.json (character restrictions) is missing: " +
         path.filename().string());
      return false;
   }

   uint32_t current_gem = 0;
   uint32_t current_character = 0;
   bool gem_seen = false;
   bool character_seen = false;
   uint32_t loaded = 0;
   std::string line;
   while (std::getline(stream, line))
   {
      if (!line.empty() && line.back() == '\r')
         line.pop_back();

      // Blank lines must never reach substr(npos): that would throw
      // std::out_of_range and terminate the process instead of failing
      // closed. Skip them.
      const size_t first = line.find_first_not_of(" \t");
      if (first == std::string::npos)
         continue;
      const std::string_view trimmed = std::string_view(line).substr(first);
      if (trimmed == "}," || trimmed == "}" || trimmed == "]")
      {
         // End of a sigil object: a hash with a character field is an
         // exclusive row (commit the pair). A hash without character is a
         // regular row (legal, ignored). A character without hash is malformed.
         if (gem_seen && character_seen)
         {
            g_required_character_by_gem[current_gem] = current_character;
            ++loaded;
         }
         else if (character_seen && !gem_seen)
         {
            return false; // malformed row - fail closed
         }
         current_gem = 0;
         current_character = 0;
         gem_seen = false;
         character_seen = false;
         continue;
      }

      uint32_t value = 0;
      if (ReadHexField(line, "hash", value))
      {
         current_gem = value;
         gem_seen = true;
      }
      else if (ReadHexField(line, "character", value))
      {
         current_character = value;
         character_seen = true;
      }
   }

   Log("Loaded " + std::to_string(loaded) + " character-restricted sigil mappings from sigils.json.");
   return loaded == kExpectedCompatibilityMappingCount &&
      g_required_character_by_gem.size() == kExpectedCompatibilityMappingCount;
}

uint32_t GetRequiredCharacterHash(uint32_t gem_hash)
{
   const auto iterator = g_required_character_by_gem.find(gem_hash);
   return iterator == g_required_character_by_gem.end() ? 0 : iterator->second;
}
}
