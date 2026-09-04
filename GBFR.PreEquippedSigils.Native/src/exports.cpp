#include "../native_internal.h"

using namespace gbfr::native;

uint32_t GBFR20_CALL GBFR20_GetAbiVersion()
{
   return GBFR20_ABI_VERSION;
}

void GBFR20_CALL GBFR20_SetLogCallback(GBFR20_LogCallback callback)
{
   g_log_callback.store(callback, std::memory_order_release);
}

int32_t GBFR20_CALL GBFR20_Initialize()
{
   if (g_shutting_down.load(std::memory_order_acquire))
      return 0;
   EnsureInitialized();
   return g_hooks_ready.load(std::memory_order_acquire) ? 1 : 0;
}

void GBFR20_CALL GBFR20_Tick()
{
   if (g_shutting_down.load(std::memory_order_acquire))
      return;
   EnsureInitialized();
   if (!g_hooks_ready.load(std::memory_order_acquire) ||
       !g_layout_ready.load(std::memory_order_acquire))
      return;
   UpdateEditSessionState();
   ValidateAuthorizedStatuses();
   ScheduleSelectedStatusRebind();
   ProcessPendingHotApply();
   ConsumeApplyResult();
}

void GBFR20_CALL GBFR20_Shutdown()
{
   if (g_shutdown_complete.exchange(true, std::memory_order_acq_rel))
      return;
   ShutdownHooks();
}

uint32_t GBFR20_CALL GBFR20_CopyRuntimeMessage(char* buffer, uint32_t buffer_size)
{
   std::string message;
   {
      std::scoped_lock lock(g_message_mutex);
      message = g_runtime_message;
   }
   const size_t required_size = message.size() + 1;
   if (buffer != nullptr && buffer_size != 0)
   {
      const size_t copy_size = std::min<size_t>(message.size(), buffer_size - 1);
      std::memcpy(buffer, message.data(), copy_size);
      buffer[copy_size] = '\0';
   }
   return required_size > UINT32_MAX ? UINT32_MAX : static_cast<uint32_t>(required_size);
}

int32_t GBFR20_CALL GBFR20_SetCustomLoadout(
   const GBFR20_TemplateSlot* slots, uint32_t count)
{
   if (g_shutting_down.load(std::memory_order_acquire))
      return 0;
   EnsureInitialized();
   if (!g_hooks_ready.load(std::memory_order_acquire))
      return 0;
   // GBFR20_TemplateSlot is layout-identical to the native TemplateGemSlot
   // (packed 1, same field order, 0x18 bytes); only read, never modified.
   const bool applied = ApplyCustomLoadout(
      reinterpret_cast<const TemplateGemSlot*>(slots),
      static_cast<int32_t>(count));
   return applied ? 1 : 0;
}
