using System.Runtime.InteropServices;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal static unsafe partial class NativeCore
{
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern uint GBFR20_GetAbiVersion();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void GBFR20_SetLogCallback(IntPtr callback);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_Initialize();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void GBFR20_Tick();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void GBFR20_Shutdown();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_InvokeOriginalPresent(
        ulong originalFunctionAddress,
        IntPtr swapChain,
        uint syncInterval,
        uint presentFlags,
        out uint exceptionCode);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern ulong GBFR20_ResolveHookChainTarget(
        ulong functionAddress,
        uint maxJumpCount,
        out uint jumpCount,
        out HookChainResolveStatus status);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_GetState(out RuntimeState state, uint stateSize);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern uint GBFR20_CopyRuntimeMessage(sbyte* buffer, uint bufferSize);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_RefreshInventory();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern uint GBFR20_GetInventoryCount();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_CopyInventoryItem(
        uint index,
        out InventoryItem item,
        uint itemSize,
        sbyte* labelBuffer,
        uint labelBufferSize);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_GetSelection(
        uint characterHash,
        uint* slots,
        uint slotCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_SetSelection(
        uint characterHash,
        int virtualSlot,
        uint inventorySlotId);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_ApplyPreset(
        PresetCharacterSelection* selections,
        uint selectionCount,
        PresetSlotResult* slotResults,
        uint slotResultCapacity,
        uint* slotResultCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern uint GBFR20_RequestApply(uint characterHash);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_SetAutoApply(int enabled);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_SetShowEquipped(int enabled);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_SetToggleKey(int virtualKey);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_SetLanguage(int language);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_RequestVirtualSlotCount(int slotCount);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_GetPendingVirtualSlotCount();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_SetInputHooksEnabled(int enabled);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_SetInputCapture(int requested);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_SetInputCaptureDevices(uint requestedDevices);

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_GetInputCaptureActive();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_IsInventoryDirty();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_CanEditCharacter(uint characterHash);

    private static uint NativeGetAbiVersion() => GBFR20_GetAbiVersion();
    private static void NativeSetLogCallback(IntPtr callback) => GBFR20_SetLogCallback(callback);
    private static int NativeInitialize() => GBFR20_Initialize();
    private static void NativeTick() => GBFR20_Tick();
    private static void NativeShutdown() => GBFR20_Shutdown();
    private static int NativeInvokeOriginalPresent(
        ulong originalFunctionAddress,
        IntPtr swapChain,
        uint syncInterval,
        uint presentFlags,
        out uint exceptionCode) =>
        GBFR20_InvokeOriginalPresent(
            originalFunctionAddress,
            swapChain,
            syncInterval,
            presentFlags,
            out exceptionCode);
    private static ulong NativeResolveHookChainTarget(
        ulong functionAddress,
        uint maxJumpCount,
        out uint jumpCount,
        out HookChainResolveStatus status) =>
        GBFR20_ResolveHookChainTarget(
            functionAddress,
            maxJumpCount,
            out jumpCount,
            out status);
    private static int NativeGetState(out RuntimeState state, uint size) =>
        GBFR20_GetState(out state, size);
    private static uint NativeCopyRuntimeMessage(sbyte* buffer, uint size) =>
        GBFR20_CopyRuntimeMessage(buffer, size);
    private static int NativeRefreshInventory() => GBFR20_RefreshInventory();
    private static uint NativeGetInventoryCount() => GBFR20_GetInventoryCount();
    private static int NativeCopyInventoryItem(
        uint index,
        out InventoryItem item,
        uint itemSize,
        sbyte* labelBuffer,
        uint labelBufferSize) =>
        GBFR20_CopyInventoryItem(index, out item, itemSize, labelBuffer, labelBufferSize);
    private static int NativeGetSelection(uint hash, uint* slots, uint count) =>
        GBFR20_GetSelection(hash, slots, count);
    private static int NativeSetSelection(uint hash, int slot, uint id) =>
        GBFR20_SetSelection(hash, slot, id);
    private static int NativeApplyPreset(
        PresetCharacterSelection* selections,
        uint selectionCount,
        PresetSlotResult* slotResults,
        uint slotResultCapacity,
        uint* slotResultCount) =>
        GBFR20_ApplyPreset(
            selections,
            selectionCount,
            slotResults,
            slotResultCapacity,
            slotResultCount);
    private static uint NativeRequestApply(uint hash) => GBFR20_RequestApply(hash);
    private static int NativeSetAutoApply(int enabled) => GBFR20_SetAutoApply(enabled);
    private static int NativeSetShowEquipped(int enabled) => GBFR20_SetShowEquipped(enabled);
    private static int NativeSetToggleKey(int key) => GBFR20_SetToggleKey(key);
    private static int NativeSetLanguage(int language) => GBFR20_SetLanguage(language);
    private static int NativeRequestVirtualSlotCount(int slotCount) =>
        GBFR20_RequestVirtualSlotCount(slotCount);
    private static int NativeGetPendingVirtualSlotCount() =>
        GBFR20_GetPendingVirtualSlotCount();
    private static int NativeSetInputHooksEnabled(int enabled) => GBFR20_SetInputHooksEnabled(enabled);
    private static int NativeSetInputCapture(int requested) => GBFR20_SetInputCapture(requested);
    private static int NativeSetInputCaptureDevices(uint requestedDevices) =>
        GBFR20_SetInputCaptureDevices(requestedDevices);
    private static int NativeGetInputCaptureActive() => GBFR20_GetInputCaptureActive();
    private static int NativeIsInventoryDirty() => GBFR20_IsInventoryDirty();
    private static int NativeCanEditCharacter(uint hash) => GBFR20_CanEditCharacter(hash);
}
