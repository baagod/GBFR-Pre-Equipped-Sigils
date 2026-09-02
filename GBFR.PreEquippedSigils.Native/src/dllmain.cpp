#include "../native_internal.h"

BOOL APIENTRY DllMain(HMODULE module, DWORD reason, LPVOID)
{
   if (reason == DLL_PROCESS_ATTACH)
   {
      gbfr::native::g_module = module;
      DisableThreadLibraryCalls(module);
   }
   else if (reason == DLL_PROCESS_DETACH)
   {
      gbfr::native::g_shutting_down.store(true, std::memory_order_release);
   }
   return TRUE;
}
