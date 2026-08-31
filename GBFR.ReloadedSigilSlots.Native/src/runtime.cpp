#include "../native_internal.h"

#include <sstream>

namespace gbfr::native
{
void Initialize()
{
   const uint64_t initialization_started = BeginStartupPhase("native-initialize");
   const auto finish_initialization = [initialization_started](bool succeeded) {
      g_initialized.store(true, std::memory_order_release);
      CompleteStartupPhase("native-initialize", initialization_started, succeeded);
   };

   g_image_base = reinterpret_cast<uintptr_t>(GetModuleHandleW(nullptr));
   std::vector<wchar_t> module_path(32768, L'\0');
   const DWORD module_length = GetModuleFileNameW(
      g_module, module_path.data(), static_cast<DWORD>(module_path.size()));
   if (module_length == 0 || module_length >= module_path.size())
   {
      SetRuntimeMessage("Could not resolve the native core directory.", true);
      finish_initialization(false);
      return;
   }

   g_module_directory = std::filesystem::path(module_path.data()).parent_path();
   g_config_path = g_module_directory / L"GBFR-ReloadedSigilSlotsConfig.ini";
   g_compatibility_path =
      g_module_directory / L"GBFR-ReloadedSigilSlots.compatibility.tsv";

   const uint64_t settings_started = BeginStartupPhase("settings-and-selections");
   LoadSettingsAndSelections(false);
   CompleteStartupPhase("settings-and-selections", settings_started, true);

   const uint64_t executable_started = BeginStartupPhase("executable-validation");
   std::vector<wchar_t> executable_path(32768, L'\0');
   const DWORD executable_length = GetModuleFileNameW(
      nullptr, executable_path.data(), static_cast<DWORD>(executable_path.size()));
   if (executable_length == 0 || executable_length >= executable_path.size())
   {
      CompleteStartupPhase("executable-validation", executable_started, false);
      SetRuntimeMessage("Could not resolve the game executable path.", true);
      finish_initialization(false);
      return;
   }

   const std::filesystem::path executable(executable_path.data());
   if (_wcsicmp(executable.filename().c_str(), L"granblue_fantasy_relink.exe") != 0)
   {
      CompleteStartupPhase("executable-validation", executable_started, false);
      SetRuntimeMessage("This native core only supports granblue_fantasy_relink.exe.", true);
      finish_initialization(false);
      return;
   }
   CompleteStartupPhase("executable-validation", executable_started, true);

   const uint64_t compatibility_started = BeginStartupPhase("compatibility-table");
   const bool compatibility_loaded = LoadCompatibilityTable(g_compatibility_path);
   CompleteStartupPhase(
      "compatibility-table", compatibility_started, compatibility_loaded);
   if (!compatibility_loaded)
   {
      SetRuntimeMessage(
         "The character-compatibility table is missing or incomplete; gameplay hooks were not installed.",
         true);
      finish_initialization(false);
      return;
   }

   const uint64_t layout_started = BeginStartupPhase("semantic-layout-resolution");
   const bool layout_ready = ResolveGameLayout();
   CompleteStartupPhase("semantic-layout-resolution", layout_started, layout_ready);
   if (!layout_ready)
   {
      finish_initialization(false);
      return;
   }

   const uint64_t activation_started =
      BeginStartupPhase("verified-settings-activation");
   LoadSettingsAndSelections(true);
   InstallDefaultTemplateSelections();
   CompleteStartupPhase(
      "verified-settings-activation", activation_started, true);

   const uint64_t hooks_started = BeginStartupPhase("native-hook-install");
   const bool hooks_installed = InstallHooks();
   CompleteStartupPhase("native-hook-install", hooks_started, hooks_installed);
   finish_initialization(hooks_installed);
}

void EnsureInitialized()
{
   std::call_once(g_initialize_once, &Initialize);
}

void ConsumeApplyResult()
{
   const int result = g_apply_result.exchange(ApplyResultNone, std::memory_order_acq_rel);
   if (result == ApplyResultNone)
      return;
   g_last_consumed_apply_result.store(result, std::memory_order_release);
   const uint64_t generation = g_last_apply_generation.load(std::memory_order_acquire);
   const uint32_t character_hash =
      g_last_apply_character_hash.load(std::memory_order_acquire);
   const uint32_t expected = g_last_apply_expected_count.load(std::memory_order_acquire);
   const uint32_t injected = g_last_apply_injected_count.load(std::memory_order_acquire);
   std::ostringstream prefix;
   prefix << "Generation " << generation << " for 0x" << ToUpperHex(character_hash) << ": ";
   switch (result)
   {
   case ApplyResultAppliedDuringNativeRebuild:
      SetRuntimeMessage(
         prefix.str() + "equipment/test rebuild copied " + std::to_string(injected) + "/" +
            std::to_string(expected) +
            " selected virtual sigils. Combat reads the same saved selection directly from the native Trait loop.",
         false);
      break;
   case ApplyResultSavedNoStatus:
      SetRuntimeMessage(prefix.str() + "no valid equipment-selected character was available.", true);
      break;
   case ApplyResultVirtualCopyFailed:
      SetRuntimeMessage(
         prefix.str() + "native trait build ran, but only " + std::to_string(injected) + "/" +
            std::to_string(expected) + " sigils were valid, unequipped, and copied.",
         true);
      break;
   case ApplyResultOwnerThreadMismatch:
      SetRuntimeMessage(
         prefix.str() + "overlay callback was not on the verified native status owner thread.", true);
      break;
   case ApplyResultStatusLookupFailed:
      SetRuntimeMessage(prefix.str() + "the native character status map had no matching status.", true);
      break;
   case ApplyResultNativeRebuildFailed:
      SetRuntimeMessage(prefix.str() + "the synchronous native status rebuild failed.", true);
      break;
   case ApplyResultNativeTraitLoopMissing:
      SetRuntimeMessage(
         prefix.str() + "the native status rebuild returned without completing virtual trait slots 13 through " +
            std::to_string(GetExpandedInternalSlotCount() - 1) + ".",
         true);
      break;
   case ApplyResultNotifierFailed:
      SetRuntimeMessage(
         prefix.str() + "traits rebuilt, but the post-rebuild native UI notifier failed.", true);
      break;
   default:
      break;
   }
}
}
