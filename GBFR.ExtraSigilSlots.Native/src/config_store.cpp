#include "../native_internal.h"

#include <cerrno>
#include <cwchar>
#include <iomanip>
#include <limits>
#include <sstream>

namespace gbfr::native
{
UiSettings g_settings;
std::mutex g_settings_mutex;
std::atomic_int32_t g_virtual_slot_count{kDefaultVirtualSlotCount};

namespace
{
constexpr std::string_view kDefaultConfigText =
   "[Settings]\r\n"
   "ConfigVersion=2\r\n"
   "ToggleKey=119\r\n"
   "ShowEquipped=0\r\n"
   "AutoApply=1\r\n"
   "Language=zh-CN\r\n"
   "VirtualSlotCount=8\r\n";
constexpr std::wstring_view kPendingSlotCountFileName =
   L"GBFR-ExtraSigilSlotsNumConfig.pending";
std::mutex g_slot_count_request_mutex;

enum class ConfigFileState
{
   Missing,
   Valid,
   Invalid,
};

struct ConfigFileInspection
{
   ConfigFileState state = ConfigFileState::Invalid;
   std::string original_bytes;
   bool original_bytes_available = false;
};

enum class ConfigSection
{
   None,
   Settings,
   Character,
   Other,
};

std::string_view TrimAscii(std::string_view text) noexcept
{
   const auto is_space = [](char character) {
      return character == ' ' || character == '\t' || character == '\r' ||
         character == '\n' || character == '\v' || character == '\f';
   };
   while (!text.empty() && is_space(text.front()))
      text.remove_prefix(1);
   while (!text.empty() && is_space(text.back()))
      text.remove_suffix(1);
   return text;
}

bool ParseDecimal(std::string_view text, uint32_t& value) noexcept
{
   value = 0;
   if (text.empty())
      return false;
   for (const char character : text)
   {
      if (character < '0' || character > '9')
         return false;
      const uint32_t digit = static_cast<uint32_t>(character - '0');
      if (value > (std::numeric_limits<uint32_t>::max() - digit) / 10)
         return false;
      value = value * 10 + digit;
   }
   return true;
}

bool ParseHex8(std::string_view text, uint32_t& value) noexcept
{
   value = 0;
   if (text.size() != 8)
      return false;
   for (const char character : text)
   {
      uint32_t digit = 0;
      if (character >= '0' && character <= '9')
         digit = static_cast<uint32_t>(character - '0');
      else if (character >= 'a' && character <= 'f')
         digit = static_cast<uint32_t>(character - 'a' + 10);
      else if (character >= 'A' && character <= 'F')
         digit = static_cast<uint32_t>(character - 'A' + 10);
      else
         return false;
      value = (value << 4) | digit;
   }
   return true;
}

bool ValidateSlots(
   std::string_view text,
   std::unordered_set<uint32_t>& claimed_slot_ids) noexcept
{
   size_t count = 0;
   size_t offset = 0;
   while (offset <= text.size())
   {
      const size_t comma = text.find(',', offset);
      const std::string_view token = TrimAscii(text.substr(
         offset,
         comma == std::string_view::npos ? text.size() - offset : comma - offset));
      uint32_t slot_id = 0;
      if (++count > static_cast<size_t>(kVirtualSlotCapacity) ||
          !ParseHex8(token, slot_id) ||
          (slot_id != 0 && !claimed_slot_ids.emplace(slot_id).second))
         return false;
      if (comma == std::string_view::npos)
         break;
      offset = comma + 1;
   }
   return count != 0;
}

bool ValidateConfigText(std::string_view text) noexcept
{
   if (text.starts_with("\xEF\xBB\xBF"))
      text.remove_prefix(3);
   if (text.empty() || text.find('\0') != std::string_view::npos)
      return false;

   ConfigSection section = ConfigSection::None;
   bool settings_seen = false;
   bool character_slots_seen = false;
   std::unordered_set<std::string> settings_keys;
   std::unordered_set<uint32_t> character_hashes;
   std::unordered_set<uint32_t> claimed_slot_ids;
   const auto finish_section = [&]() {
      return section != ConfigSection::Character || character_slots_seen;
   };

   size_t offset = 0;
   while (offset <= text.size())
   {
      const size_t newline = text.find('\n', offset);
      std::string_view line = text.substr(
         offset,
         newline == std::string_view::npos ? text.size() - offset : newline - offset);
      if (!line.empty() && line.back() == '\r')
         line.remove_suffix(1);
      line = TrimAscii(line);
      if (!line.empty() && line.front() != ';' && line.front() != '#')
      {
         if (line.front() == '[')
         {
            if (!finish_section() || line.size() < 3 || line.back() != ']')
               return false;
            const std::string_view name = line.substr(1, line.size() - 2);
            const std::string normalized_name = ToLowerAscii(std::string(name));
            character_slots_seen = false;
            if (normalized_name == "settings")
            {
               if (settings_seen)
                  return false;
               settings_seen = true;
               section = ConfigSection::Settings;
            }
            else if (normalized_name.starts_with("character_") && name.size() == 18)
            {
               uint32_t character_hash = 0;
               if (!ParseHex8(name.substr(10), character_hash) || character_hash == 0 ||
                   !character_hashes.emplace(character_hash).second)
                  return false;
               section = ConfigSection::Character;
            }
            else
            {
               section = ConfigSection::Other;
            }
         }
         else
         {
            const size_t equals = line.find('=');
            if (section == ConfigSection::None || equals == std::string_view::npos)
               return false;
            const std::string_view key = TrimAscii(line.substr(0, equals));
            const std::string_view value = TrimAscii(line.substr(equals + 1));
            if (key.empty())
               return false;
            const std::string normalized_key = ToLowerAscii(std::string(key));
            if (section == ConfigSection::Settings)
            {
               const bool known = normalized_key == "configversion" ||
                  normalized_key == "togglekey" || normalized_key == "showequipped" ||
                  normalized_key == "autoapply" || normalized_key == "language" ||
                  normalized_key == "virtualslotcount";
               if (known)
               {
                  if (!settings_keys.emplace(normalized_key).second)
                     return false;
                  uint32_t number = 0;
                  if (normalized_key == "configversion")
                  {
                     if (!ParseDecimal(value, number) || number != kCurrentSettingsVersion)
                        return false;
                  }
                  else if (normalized_key == "togglekey")
                  {
                     if (!ParseDecimal(value, number) || number < 1 || number > 255)
                        return false;
                  }
                  else if (normalized_key == "showequipped")
                  {
                     if (!ParseDecimal(value, number) || number > 1)
                        return false;
                  }
                  else if (normalized_key == "autoapply")
                  {
                     if (!ParseDecimal(value, number) || number != 1)
                        return false;
                  }
                  else if (normalized_key == "language" &&
                           value != "en" && value != "zh-CN")
                  {
                     return false;
                  }
                  else if (normalized_key == "virtualslotcount" &&
                           (!ParseDecimal(value, number) || number < 1 ||
                            number > kVirtualSlotCapacity))
                  {
                     return false;
                  }
               }
            }
            else if (section == ConfigSection::Character && normalized_key == "slots")
            {
               if (character_slots_seen || !ValidateSlots(value, claimed_slot_ids))
                  return false;
               character_slots_seen = true;
            }
         }
      }
      if (newline == std::string_view::npos)
         break;
      offset = newline + 1;
   }

   return finish_section() && settings_seen && settings_keys.size() == 6;
}

ConfigFileInspection InspectConfigFile()
{
   const DWORD attributes = GetFileAttributesW(g_config_path.c_str());
   if (attributes == INVALID_FILE_ATTRIBUTES)
   {
      const DWORD error = GetLastError();
      return {
         error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND
            ? ConfigFileState::Missing
            : ConfigFileState::Invalid,
         {},
         false};
   }
   if ((attributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
      return {};

   HANDLE file = CreateFileW(
      g_config_path.c_str(),
      GENERIC_READ,
      FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
      nullptr,
      OPEN_EXISTING,
      FILE_ATTRIBUTE_NORMAL,
      nullptr);
   if (file == INVALID_HANDLE_VALUE)
      return {};

   LARGE_INTEGER size{};
   constexpr LONGLONG kMaximumConfigSize = 4 * 1024 * 1024;
   if (!GetFileSizeEx(file, &size) || size.QuadPart < 0 ||
       size.QuadPart > kMaximumConfigSize)
   {
      CloseHandle(file);
      return {};
   }
   std::string text(static_cast<size_t>(size.QuadPart), '\0');
   DWORD total_read = 0;
   while (total_read < text.size())
   {
      DWORD current_read = 0;
      const DWORD remaining = static_cast<DWORD>(text.size() - total_read);
      if (!ReadFile(file, text.data() + total_read, remaining, &current_read, nullptr) ||
          current_read == 0)
      {
         CloseHandle(file);
         return {};
      }
      total_read += current_read;
   }
   CloseHandle(file);
   const ConfigFileState state =
      ValidateConfigText(text) ? ConfigFileState::Valid : ConfigFileState::Invalid;
   return {state, std::move(text), true};
}

std::wstring BuildConfigBackupSuffix(std::wstring_view reason)
{
   SYSTEMTIME time{};
   GetLocalTime(&time);
   wchar_t suffix[96]{};
   swprintf_s(
      suffix,
      L".%.*s-%04u%02u%02u-%02u%02u%02u-%03u.bak",
      static_cast<int>(reason.size()),
      reason.data(),
      time.wYear,
      time.wMonth,
      time.wDay,
      time.wHour,
      time.wMinute,
      time.wSecond,
      time.wMilliseconds);
   return suffix;
}

bool WriteNewFile(
   const std::filesystem::path& path,
   std::string_view bytes,
   DWORD& error) noexcept
{
   HANDLE file = CreateFileW(
      path.c_str(),
      GENERIC_WRITE,
      0,
      nullptr,
      CREATE_NEW,
      FILE_ATTRIBUTE_NORMAL,
      nullptr);
   if (file == INVALID_HANDLE_VALUE)
   {
      error = GetLastError();
      return false;
   }
   DWORD total_written = 0;
   bool succeeded = true;
   while (total_written < bytes.size())
   {
      DWORD current_written = 0;
      const DWORD remaining = static_cast<DWORD>(bytes.size() - total_written);
      if (!WriteFile(
             file,
             bytes.data() + total_written,
             remaining,
             &current_written,
             nullptr) ||
          current_written == 0)
      {
         succeeded = false;
         break;
      }
      total_written += current_written;
   }
   if (succeeded && FlushFileBuffers(file) == FALSE)
      succeeded = false;
   error = succeeded ? ERROR_SUCCESS : GetLastError();
   CloseHandle(file);
   if (!succeeded)
      (void)DeleteFileW(path.c_str());
   return succeeded;
}

bool WriteFileAtomically(
   const std::filesystem::path& destination,
   std::string_view bytes,
   bool replace_existing,
   DWORD& error)
{
   std::filesystem::path temporary;
   for (uint32_t attempt = 0; attempt < 32; ++attempt)
   {
      temporary = destination.wstring() + L".tmp." +
         std::to_wstring(GetCurrentProcessId()) + L"." +
         std::to_wstring(GetCurrentThreadId()) + L"." +
         std::to_wstring(GetTickCount64()) + L"." + std::to_wstring(attempt);
      if (WriteNewFile(temporary, bytes, error))
         break;
      if (error != ERROR_FILE_EXISTS && error != ERROR_ALREADY_EXISTS)
         return false;
      temporary.clear();
   }
   if (temporary.empty())
   {
      error = ERROR_FILE_EXISTS;
      return false;
   }

   DWORD flags = MOVEFILE_WRITE_THROUGH;
   if (replace_existing)
      flags |= MOVEFILE_REPLACE_EXISTING;
   if (!MoveFileExW(temporary.c_str(), destination.c_str(), flags))
   {
      error = GetLastError();
      (void)DeleteFileW(temporary.c_str());
      return false;
   }
   error = ERROR_SUCCESS;
   return true;
}

bool ReadSmallFile(
   const std::filesystem::path& path,
   size_t maximum_size,
   std::string& bytes,
   DWORD& error)
{
   HANDLE file = CreateFileW(
      path.c_str(),
      GENERIC_READ,
      FILE_SHARE_READ | FILE_SHARE_DELETE,
      nullptr,
      OPEN_EXISTING,
      FILE_ATTRIBUTE_NORMAL,
      nullptr);
   if (file == INVALID_HANDLE_VALUE)
   {
      error = GetLastError();
      return false;
   }

   LARGE_INTEGER size{};
   if (!GetFileSizeEx(file, &size))
   {
      error = GetLastError();
      CloseHandle(file);
      return false;
   }
   if (size.QuadPart < 0 || static_cast<uint64_t>(size.QuadPart) > maximum_size)
   {
      error = ERROR_FILE_TOO_LARGE;
      CloseHandle(file);
      return false;
   }

   bytes.assign(static_cast<size_t>(size.QuadPart), '\0');
   DWORD total_read = 0;
   while (total_read < bytes.size())
   {
      DWORD current_read = 0;
      const DWORD remaining = static_cast<DWORD>(bytes.size() - total_read);
      if (!ReadFile(file, bytes.data() + total_read, remaining, &current_read, nullptr) ||
          current_read == 0)
      {
         error = GetLastError();
         CloseHandle(file);
         return false;
      }
      total_read += current_read;
   }
   CloseHandle(file);
   error = ERROR_SUCCESS;
   return true;
}

bool BackupInvalidConfig(
   std::string_view original_bytes,
   std::filesystem::path& backup,
   DWORD& error)
{
   const std::wstring base =
      g_config_path.wstring() + BuildConfigBackupSuffix(L"invalid");
   for (uint32_t index = 0; index < 1000; ++index)
   {
      backup = index == 0 ? base : base + L"." + std::to_wstring(index);
      if (WriteNewFile(backup, original_bytes, error))
         return true;
      if (error != ERROR_FILE_EXISTS && error != ERROR_ALREADY_EXISTS)
         return false;
   }
   error = ERROR_FILE_EXISTS;
   return false;
}

bool WriteDefaultConfigAtomically(bool replace_existing, DWORD& error)
{
   return WriteFileAtomically(
      g_config_path, kDefaultConfigText, replace_existing, error);
}

bool EnsureValidConfigFile()
{
   ConfigFileInspection inspection = InspectConfigFile();
   if (inspection.state == ConfigFileState::Valid)
      return true;

   DWORD error = ERROR_SUCCESS;
   std::filesystem::path backup;
   if (inspection.state == ConfigFileState::Invalid)
   {
      if (!inspection.original_bytes_available)
      {
         Log(
            "Invalid NumConfig was left untouched because its original bytes could not be read safely.");
         return false;
      }
      if (!BackupInvalidConfig(inspection.original_bytes, backup, error))
      {
         Log(
            "Invalid NumConfig was left untouched because its backup could not be created; Win32 error=" +
            std::to_string(error) + ".");
         return false;
      }
   }

   if (!WriteDefaultConfigAtomically(
          inspection.state == ConfigFileState::Invalid, error))
   {
      Log(
         "The default NumConfig could not be created atomically; Win32 error=" +
         std::to_string(error) + ".");
      return false;
   }
   if (inspection.state == ConfigFileState::Missing)
   {
      Log("NumConfig was missing and a complete default INI was created.");
   }
   else
   {
      Log(
         "Invalid NumConfig was backed up to \"" + WideToUtf8(backup.wstring()) +
         "\" and replaced with a complete default INI.");
   }
   return true;
}

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
   const auto trim = [](std::wstring_view value) {
      constexpr std::wstring_view whitespace = L" \t\r\n\v\f";
      const size_t begin = value.find_first_not_of(whitespace);
      if (begin == std::wstring_view::npos)
         return std::wstring_view{};
      const size_t end = value.find_last_not_of(whitespace);
      return value.substr(begin, end - begin + 1);
   };
   std::array<uint32_t, kVirtualSlotCapacity> slots{};
   size_t slot_index = 0;
   size_t offset = 0;
   while (slot_index < slots.size() && offset <= text.size())
   {
      const size_t comma = text.find(L',', offset);
      const std::wstring token(trim(text.substr(
         offset,
         comma == std::wstring_view::npos ? text.size() - offset : comma - offset)));
      const bool valid_token = token.size() == 8 &&
         std::all_of(token.begin(), token.end(), [](wchar_t character) {
            return (character >= L'0' && character <= L'9') ||
               (character >= L'a' && character <= L'f') ||
               (character >= L'A' && character <= L'F');
         });
      if (valid_token)
      {
         wchar_t* end = nullptr;
         errno = 0;
         const unsigned long value = std::wcstoul(token.c_str(), &end, 16);
         if (errno != ERANGE && end == token.c_str() + token.size())
            slots[slot_index] = static_cast<uint32_t>(value);
      }
      ++slot_index;
      if (comma == std::wstring_view::npos)
         break;
      offset = comma + 1;
   }
   return slots;
}

std::filesystem::path PendingSlotCountPath()
{
   return g_module_directory / kPendingSlotCountFileName;
}

enum class PendingSlotCountState
{
   Missing,
   Valid,
   Invalid,
   ReadFailed,
};

PendingSlotCountState ReadPendingSlotCount(int& slot_count, DWORD& error)
{
   slot_count = 0;
   std::string bytes;
   if (!ReadSmallFile(PendingSlotCountPath(), 128, bytes, error))
   {
      if (error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND)
         return PendingSlotCountState::Missing;
      return PendingSlotCountState::ReadFailed;
   }

   constexpr std::string_view prefix = "VirtualSlotCount=";
   std::string_view text = TrimAscii(bytes);
   uint32_t parsed = 0;
   if (!text.starts_with(prefix) || text.find('\0') != std::string_view::npos ||
       !ParseDecimal(text.substr(prefix.size()), parsed) || parsed < 1 ||
       parsed > kVirtualSlotCapacity)
   {
      return PendingSlotCountState::Invalid;
   }
   slot_count = static_cast<int>(parsed);
   return PendingSlotCountState::Valid;
}

bool DeletePendingSlotCount(DWORD& error)
{
   if (DeleteFileW(PendingSlotCountPath().c_str()) != FALSE)
   {
      error = ERROR_SUCCESS;
      return true;
   }
   error = GetLastError();
   return error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND;
}

bool ReadConfiguredVirtualSlotCount(std::string_view text, int& slot_count)
{
   if (text.starts_with("\xEF\xBB\xBF"))
      text.remove_prefix(3);
   ConfigSection section = ConfigSection::None;
   size_t offset = 0;
   while (offset <= text.size())
   {
      const size_t newline = text.find('\n', offset);
      std::string_view line = text.substr(
         offset,
         newline == std::string_view::npos ? text.size() - offset : newline - offset);
      if (!line.empty() && line.back() == '\r')
         line.remove_suffix(1);
      line = TrimAscii(line);
      if (!line.empty() && line.front() != ';' && line.front() != '#')
      {
         if (line.front() == '[' && line.back() == ']')
         {
            const std::string normalized = ToLowerAscii(
               std::string(line.substr(1, line.size() - 2)));
            section = normalized == "settings" ? ConfigSection::Settings : ConfigSection::Other;
         }
         else if (section == ConfigSection::Settings)
         {
            const size_t equals = line.find('=');
            if (equals != std::string_view::npos &&
                ToLowerAscii(std::string(TrimAscii(line.substr(0, equals)))) ==
                   "virtualslotcount")
            {
               uint32_t parsed = 0;
               if (!ParseDecimal(TrimAscii(line.substr(equals + 1)), parsed) || parsed < 1 ||
                   parsed > kVirtualSlotCapacity)
                  return false;
               slot_count = static_cast<int>(parsed);
               return true;
            }
         }
      }
      if (newline == std::string_view::npos)
         break;
      offset = newline + 1;
   }
   return false;
}

std::vector<uint32_t> ParseAsciiSlots(std::string_view text)
{
   std::vector<uint32_t> slots;
   size_t offset = 0;
   while (offset <= text.size() && slots.size() < kVirtualSlotCapacity)
   {
      const size_t comma = text.find(',', offset);
      uint32_t slot_id = 0;
      if (!ParseHex8(TrimAscii(text.substr(
             offset,
             comma == std::string_view::npos ? text.size() - offset : comma - offset)),
             slot_id))
      {
         return {};
      }
      slots.push_back(slot_id);
      if (comma == std::string_view::npos)
         break;
      offset = comma + 1;
   }
   return slots;
}

std::string BuildResizedSlotList(
   std::string_view current_value,
   int current_slot_count,
   int target_slot_count)
{
   const std::vector<uint32_t> current_slots = ParseAsciiSlots(current_value);
   std::ostringstream stream;
   stream << std::uppercase << std::hex << std::setfill('0');
   const size_t retained = std::min({
      current_slots.size(),
      static_cast<size_t>(current_slot_count),
      static_cast<size_t>(target_slot_count)});
   for (int index = 0; index < target_slot_count; ++index)
   {
      if (index != 0)
         stream << ',';
      const uint32_t value = static_cast<size_t>(index) < retained
         ? current_slots[static_cast<size_t>(index)]
         : 0;
      stream << std::setw(8) << value;
   }
   return stream.str();
}

bool RewriteConfigForSlotCount(
   std::string_view original,
   int current_slot_count,
   int target_slot_count,
   std::string& rewritten)
{
   rewritten.clear();
   rewritten.reserve(original.size() + 256);
   ConfigSection section = ConfigSection::None;
   bool setting_replaced = false;
   size_t offset = 0;
   while (offset < original.size())
   {
      const size_t newline = original.find('\n', offset);
      size_t line_end = newline == std::string_view::npos ? original.size() : newline;
      const bool has_carriage_return = line_end > offset && original[line_end - 1] == '\r';
      const size_t content_end = has_carriage_return ? line_end - 1 : line_end;
      std::string_view line = original.substr(offset, content_end - offset);
      std::string_view parsed_line = line;
      if (offset == 0 && parsed_line.starts_with("\xEF\xBB\xBF"))
         parsed_line.remove_prefix(3);
      const std::string_view trimmed = TrimAscii(parsed_line);

      bool replaced = false;
      if (!trimmed.empty() && trimmed.front() != ';' && trimmed.front() != '#')
      {
         if (trimmed.front() == '[' && trimmed.back() == ']')
         {
            const std::string normalized = ToLowerAscii(
               std::string(trimmed.substr(1, trimmed.size() - 2)));
            if (normalized == "settings")
               section = ConfigSection::Settings;
            else if (normalized.starts_with("character_"))
               section = ConfigSection::Character;
            else
               section = ConfigSection::Other;
         }
         else
         {
            const size_t equals = line.find('=');
            if (equals != std::string_view::npos)
            {
               const std::string key = ToLowerAscii(
                  std::string(TrimAscii(line.substr(0, equals))));
               const bool replace_count =
                  section == ConfigSection::Settings && key == "virtualslotcount";
               const bool replace_slots =
                  section == ConfigSection::Character && key == "slots";
               if (replace_count || replace_slots)
               {
                  size_t value_begin = equals + 1;
                  while (value_begin < line.size() &&
                         (line[value_begin] == ' ' || line[value_begin] == '\t'))
                     ++value_begin;
                  size_t value_end = line.size();
                  while (value_end > value_begin &&
                         (line[value_end - 1] == ' ' || line[value_end - 1] == '\t'))
                     --value_end;
                  rewritten.append(line.substr(0, value_begin));
                  if (replace_count)
                  {
                     rewritten.append(std::to_string(target_slot_count));
                     setting_replaced = true;
                  }
                  else
                  {
                     rewritten.append(BuildResizedSlotList(
                        line.substr(value_begin, value_end - value_begin),
                        current_slot_count,
                        target_slot_count));
                  }
                  rewritten.append(line.substr(value_end));
                  replaced = true;
               }
            }
         }
      }
      if (!replaced)
         rewritten.append(line);
      if (has_carriage_return)
         rewritten.push_back('\r');
      if (newline != std::string_view::npos)
         rewritten.push_back('\n');
      if (newline == std::string_view::npos)
         break;
      offset = newline + 1;
   }
   return setting_replaced && ValidateConfigText(rewritten);
}

bool BackupResizeConfig(
   std::string_view original_bytes,
   std::filesystem::path& backup,
   DWORD& error)
{
   const std::wstring base =
      g_config_path.wstring() + BuildConfigBackupSuffix(L"resize");
   for (uint32_t index = 0; index < 1000; ++index)
   {
      backup = index == 0 ? base : base + L"." + std::to_wstring(index);
      if (WriteNewFile(backup, original_bytes, error))
         return true;
      if (error != ERROR_FILE_EXISTS && error != ERROR_ALREADY_EXISTS)
         return false;
   }
   error = ERROR_FILE_EXISTS;
   return false;
}

}

bool ApplyPendingVirtualSlotCount()
{
   std::scoped_lock lock(g_slot_count_request_mutex);
   int target_slot_count = 0;
   DWORD error = ERROR_SUCCESS;
   const PendingSlotCountState pending_state =
      ReadPendingSlotCount(target_slot_count, error);
   if (pending_state == PendingSlotCountState::Missing)
      return true;
   if (pending_state != PendingSlotCountState::Valid)
   {
      Log(
         pending_state == PendingSlotCountState::Invalid
            ? "The pending virtual-slot-count request is invalid and was left untouched."
            : "The pending virtual-slot-count request could not be read; Win32 error=" +
                 std::to_string(error) + ".");
      return false;
   }

   if (!EnsureValidConfigFile())
   {
      Log("The pending virtual-slot-count request was not applied because NumConfig is unavailable.");
      return false;
   }
   const ConfigFileInspection inspection = InspectConfigFile();
   int current_slot_count = 0;
   if (inspection.state != ConfigFileState::Valid ||
       !inspection.original_bytes_available ||
       !ReadConfiguredVirtualSlotCount(inspection.original_bytes, current_slot_count))
   {
      Log("The pending virtual-slot-count request was not applied because NumConfig could not be verified.");
      return false;
   }

   if (current_slot_count == target_slot_count)
   {
      if (!DeletePendingSlotCount(error))
      {
         Log(
            "The already-applied virtual-slot-count request could not be cleared; Win32 error=" +
            std::to_string(error) + ".");
         return false;
      }
      Log(
         "Pending virtual-slot-count request already matches NumConfig and was cleared: " +
         std::to_string(target_slot_count) + ".");
      return true;
   }

   std::string rewritten;
   if (!RewriteConfigForSlotCount(
          inspection.original_bytes, current_slot_count, target_slot_count, rewritten))
   {
      Log("The pending virtual-slot-count request failed its rewritten NumConfig validation.");
      return false;
   }

   std::filesystem::path backup;
   if (!BackupResizeConfig(inspection.original_bytes, backup, error))
   {
      Log(
         "The pending virtual-slot-count request was not applied because its NumConfig backup failed; Win32 error=" +
         std::to_string(error) + ".");
      return false;
   }
   if (!WriteFileAtomically(g_config_path, rewritten, true, error))
   {
      Log(
         "The pending virtual-slot-count request was not applied because the atomic NumConfig replacement failed; Win32 error=" +
         std::to_string(error) + ".");
      return false;
   }

   if (!DeletePendingSlotCount(error))
   {
      Log(
         "Virtual slot count was changed, but the completed pending request could not be removed; Win32 error=" +
         std::to_string(error) + ". It will be cleared on the next start.");
   }
   Log(
      "Virtual slot count changed transactionally from " +
      std::to_string(current_slot_count) + " to " +
      std::to_string(target_slot_count) + "; previous NumConfig backup=\"" +
      WideToUtf8(backup.wstring()) + "\".");
   return true;
}

int RequestPendingVirtualSlotCount(int slot_count)
{
   if (slot_count < 1 || slot_count > kVirtualSlotCapacity)
      return GBFR20_SLOT_COUNT_REQUEST_FAILED;

   std::scoped_lock lock(g_slot_count_request_mutex);
   DWORD error = ERROR_SUCCESS;
   if (slot_count == GetVirtualSlotCount())
   {
      return DeletePendingSlotCount(error)
         ? GBFR20_SLOT_COUNT_REQUEST_CLEARED
         : GBFR20_SLOT_COUNT_REQUEST_FAILED;
   }

   const std::string request =
      "VirtualSlotCount=" + std::to_string(slot_count) + "\r\n";
   if (!WriteFileAtomically(PendingSlotCountPath(), request, true, error))
   {
      Log(
         "Could not save the pending virtual-slot-count request; Win32 error=" +
         std::to_string(error) + ".");
      return GBFR20_SLOT_COUNT_REQUEST_FAILED;
   }
   return GBFR20_SLOT_COUNT_REQUEST_PENDING;
}

int GetPendingVirtualSlotCount()
{
   std::scoped_lock lock(g_slot_count_request_mutex);
   int slot_count = 0;
   DWORD error = ERROR_SUCCESS;
   return ReadPendingSlotCount(slot_count, error) == PendingSlotCountState::Valid
      ? slot_count
      : 0;
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

void LoadSettingsAndSelections(bool activate_selection_ownership)
{
   if (!EnsureValidConfigFile())
   {
      UiSettings defaults;
      g_virtual_slot_count.store(defaults.virtual_slot_count, std::memory_order_release);
      {
         std::scoped_lock lock(g_settings_mutex);
         g_settings = std::move(defaults);
      }
      std::unique_lock lock(g_selection_mutex);
      g_character_selections.clear();
      g_virtual_owner_by_slot_id.clear();
      return;
   }

   UiSettings settings;
   const int toggle_key = static_cast<int>(GetPrivateProfileIntW(
      L"Settings", L"ToggleKey", VK_F8, g_config_path.c_str()));
   settings.toggle_key = toggle_key;
   settings.show_equipped =
      GetPrivateProfileIntW(L"Settings", L"ShowEquipped", 0, g_config_path.c_str()) != 0;
   settings.auto_apply =
      GetPrivateProfileIntW(L"Settings", L"AutoApply", 1, g_config_path.c_str()) != 0;
   const std::string configured_language =
      WideToUtf8(ReadIniString(L"Settings", L"Language", L"zh-CN"));
   settings.language = configured_language == "en" ? "en" : "zh-CN";
   settings.virtual_slot_count = static_cast<int>(GetPrivateProfileIntW(
      L"Settings",
      L"VirtualSlotCount",
      kDefaultVirtualSlotCount,
      g_config_path.c_str()));
   g_virtual_slot_count.store(settings.virtual_slot_count, std::memory_order_release);
   {
      std::scoped_lock lock(g_settings_mutex);
      g_settings = std::move(settings);
   }

   std::vector<wchar_t> section_names(65536, L'\0');
   const DWORD copied = GetPrivateProfileSectionNamesW(
      section_names.data(), static_cast<DWORD>(section_names.size()), g_config_path.c_str());
   if (copied == 0)
   {
      std::unique_lock lock(g_selection_mutex);
      g_character_selections.clear();
      g_virtual_owner_by_slot_id.clear();
      return;
   }

   std::unique_lock lock(g_selection_mutex);
   g_character_selections.clear();
   g_virtual_owner_by_slot_id.clear();
   for (const wchar_t* section = section_names.data(); *section != L'\0';
        section += std::wcslen(section) + 1)
   {
      constexpr std::wstring_view prefix = L"Character_";
      const std::wstring_view name(section);
      if (name.size() != prefix.size() + 8 ||
          _wcsnicmp(name.data(), prefix.data(), prefix.size()) != 0)
         continue;
      wchar_t* end = nullptr;
      const uint32_t hash = static_cast<uint32_t>(
         std::wcstoul(section + prefix.size(), &end, 16));
      if (hash == 0 || end == section + prefix.size())
         continue;
      g_character_selections[hash] = ParseSlots(ReadIniString(section, L"Slots", L""));
   }

   if (!activate_selection_ownership)
      return;

   std::vector<uint32_t> character_hashes;
   character_hashes.reserve(g_character_selections.size());
   for (const auto& [hash, slots] : g_character_selections)
      character_hashes.push_back(hash);
   std::sort(character_hashes.begin(), character_hashes.end());

   std::unordered_set<uint32_t> claimed_slot_ids;
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
      }
   }
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
