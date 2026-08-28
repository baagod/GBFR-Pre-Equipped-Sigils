#include "../../GBFR.ExtraSigilSlots.Native/native_internal.h"

#include <fstream>
#include <iostream>
#include <stdexcept>

namespace gbfr::native
{
uintptr_t g_image_base = 0;
std::atomic_bool g_layout_ready{false};
ResolvedGameLayout g_game_layout{};
std::mutex g_message_mutex;
std::string g_runtime_message;
bool g_runtime_message_is_error = false;

void SetRuntimeMessage(std::string message, bool is_error)
{
   std::scoped_lock lock(g_message_mutex);
   g_runtime_message = std::move(message);
   g_runtime_message_is_error = is_error;
}

void Log(const std::string& message)
{
   std::cout << message << '\n';
}
}

namespace
{
using gbfr::native::ResolvedGameLayout;

struct MappedImage
{
   std::vector<uint8_t> bytes;

   explicit MappedImage(const std::filesystem::path& path)
   {
      std::ifstream stream(path, std::ios::binary | std::ios::ate);
      if (!stream)
         throw std::runtime_error("Could not open executable.");
      const auto file_size = stream.tellg();
      if (file_size < static_cast<std::streamoff>(sizeof(IMAGE_DOS_HEADER)))
         throw std::runtime_error("Executable is too small for a DOS header.");
      stream.seekg(0);
      std::vector<uint8_t> file(static_cast<size_t>(file_size));
      if (!stream.read(reinterpret_cast<char*>(file.data()), file_size))
         throw std::runtime_error("Could not read executable.");

      const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(file.data());
      if (dos->e_magic != IMAGE_DOS_SIGNATURE || dos->e_lfanew < 0)
         throw std::runtime_error("Invalid DOS header.");
      const size_t nt_offset = static_cast<size_t>(dos->e_lfanew);
      if (nt_offset > file.size() || sizeof(IMAGE_NT_HEADERS64) > file.size() - nt_offset)
         throw std::runtime_error("NT headers exceed the executable.");
      const auto* nt = reinterpret_cast<const IMAGE_NT_HEADERS64*>(file.data() + nt_offset);
      if (nt->Signature != IMAGE_NT_SIGNATURE ||
          nt->OptionalHeader.Magic != IMAGE_NT_OPTIONAL_HDR64_MAGIC)
         throw std::runtime_error("Invalid PE32+ headers.");
      if (nt->OptionalHeader.SizeOfImage < nt->OptionalHeader.SizeOfHeaders ||
          nt->OptionalHeader.SizeOfHeaders > file.size())
         throw std::runtime_error("Invalid PE image/header sizes.");

      const size_t section_table_offset = nt_offset +
         offsetof(IMAGE_NT_HEADERS64, OptionalHeader) + nt->FileHeader.SizeOfOptionalHeader;
      const size_t section_table_size = static_cast<size_t>(nt->FileHeader.NumberOfSections) *
         sizeof(IMAGE_SECTION_HEADER);
      if (section_table_offset > file.size() || section_table_size > file.size() - section_table_offset)
         throw std::runtime_error("PE section table exceeds the executable.");

      bytes.resize(nt->OptionalHeader.SizeOfImage);
      std::memcpy(bytes.data(), file.data(), nt->OptionalHeader.SizeOfHeaders);
      const auto* section = reinterpret_cast<const IMAGE_SECTION_HEADER*>(
         file.data() + section_table_offset);
      for (uint16_t index = 0; index < nt->FileHeader.NumberOfSections; ++index)
      {
         const size_t raw_offset = section[index].PointerToRawData;
         const size_t copy_size = section[index].SizeOfRawData;
         const size_t virtual_offset = section[index].VirtualAddress;
         if (raw_offset > file.size() || copy_size > file.size() - raw_offset)
            throw std::runtime_error("PE section raw data exceeds the executable.");
         if (virtual_offset > bytes.size() || copy_size > bytes.size() - virtual_offset)
            throw std::runtime_error("PE section exceeds SizeOfImage.");
         std::memcpy(
            bytes.data() + virtual_offset,
            file.data() + raw_offset,
            copy_size);
      }
   }
};

void Require(bool condition, const char* message)
{
   if (!condition)
      throw std::runtime_error(message);
}

void RequireLayout(
   const std::filesystem::path& path,
   uint32_t timestamp,
   uintptr_t getter,
   uintptr_t protection,
   uintptr_t system_data,
   uintptr_t status_manager,
   uintptr_t ui_manager)
{
   MappedImage image(path);
   gbfr::native::g_image_base = reinterpret_cast<uintptr_t>(image.bytes.data());
   Require(gbfr::native::ResolveGameLayout(), "Production resolver rejected known executable.");
   const ResolvedGameLayout& layout = gbfr::native::g_game_layout;
   Require(layout.pe_timestamp == timestamp, "Unexpected PE timestamp.");
   Require(layout.get_gem_data_by_index_rva == getter, "Unexpected getter RVA.");
   Require(layout.set_gem_protection_rva == protection, "Unexpected protection RVA.");
   Require(layout.system_data_global_rva == system_data, "Unexpected SystemData RVA.");
   Require(layout.status_manager_global_rva == status_manager, "Unexpected StatusManager RVA.");
   Require(layout.ui_manager_global_rva == ui_manager, "Unexpected UiManager RVA.");
   Require(gbfr::native::RevalidateGameLayout(), "Resolved layout failed exact-byte revalidation.");

   image.bytes[layout.trait_fetch_path_rva] ^= 0x01;
   Require(!gbfr::native::RevalidateGameLayout(), "Mutated hook bytes did not fail closed.");
   gbfr::native::ResetGameLayout();
}
}

int wmain(int argc, wchar_t** argv)
{
   try
   {
      if (argc != 3)
         throw std::runtime_error("Pass the 2.0.4 and 2.0.5 executable paths.");
      RequireLayout(argv[1], 0x6A6FFBA5, 0xA26D10, 0x33D580, 0x7C1EB80, 0x7C22BC0, 0x7C48380);
      std::cout << "NATIVE_LAYOUT_2_0_4=PASS\n";
      RequireLayout(argv[2], 0x6A7DA26E, 0xA26720, 0x33D550, 0x7C1EE00, 0x7C22E40, 0x7C48600);
      std::cout << "NATIVE_LAYOUT_2_0_5=PASS\n";
      std::cout << "NATIVE_LAYOUT_FAIL_CLOSED=PASS\n";
      return 0;
   }
   catch (const std::exception& exception)
   {
      std::cerr << exception.what() << '\n';
      return 1;
   }
}
