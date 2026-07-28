using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal static unsafe partial class NativeCore
{
    internal const int AbiVersion = 11;
    internal const int DefaultVirtualSlotCount = 8;
    internal const int VirtualSlotCapacity = 24;
    internal const int OwnerCharacterCapacity = 4;
    internal const int PresetCharacterCapacity = 32;

    private const string LibraryName = "GBFR.ExtraSigilSlots.Native.dll";
    private static readonly object ResolverLock = new();
    private static readonly object NativeLogLock = new();
    private static string? _libraryPath;
    private static IntPtr _libraryHandle;
    private static int _resolverConfigured;
    private static Action<string>? _nativeLogSink;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct GemData
    {
        internal uint Trait1;
        internal int Trait1Level;
        internal uint Trait2;
        internal int Trait2Level;
        internal uint GemId;
        internal uint WornBy;
        internal int SigilLevel;
        internal uint SlotId;
        internal uint Flags;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct InventoryItem
    {
        internal GemData Gem;
        internal uint Equipped;
        internal uint RequiredCharacterHash;
        internal uint VirtualOwnerCharacterHash;
        internal int VirtualOwnerSlot;
    }

    internal enum PresetSlotStatus : int
    {
        Empty = 0,
        Applied = 1,
        Missing = -1,
        Equipped = -2,
        Disabled = -3,
        CharacterRestricted = -4,
        Duplicate = -5,
    }

    internal enum HookChainResolveStatus : uint
    {
        Ok = 0,
        InvalidArgument = 1,
        Unreadable = 2,
        NonExecutable = 3,
        Cycle = 4,
        DepthExceeded = 5,
        UnsupportedJump = 6,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PresetCharacterSelection
    {
        internal uint CharacterHash;
        internal fixed uint Slots[VirtualSlotCapacity];
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct PresetSlotResult
    {
        internal uint CharacterHash;
        internal int VirtualSlot;
        internal uint RequestedSlotId;
        internal uint OwnerCharacterHash;
        internal PresetSlotStatus Status;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct RuntimeState
    {
        internal uint AbiVersion;
        internal uint StructSize;
        internal int Initialized;
        internal int HooksReady;
        internal int ShuttingDown;
        internal int RuntimeMessageIsError;
        internal uint UiSelectedCharacterHash;
        internal uint EffectiveCharacterHash;
        internal uint LastRebuiltCharacterHash;
        internal int LastContextMode;
        internal uint OwnerThreadId;
        internal uint OverlayThreadId;
        internal ulong OwnerTickCount;
        internal ulong OverlayFrameCount;
        internal uint OwnerCharacterCount;
        internal fixed uint OwnerCharacterHashes[OwnerCharacterCapacity];
        internal ulong LastApplyGeneration;
        internal uint LastApplyCharacterHash;
        internal uint LastApplyExpectedCount;
        internal uint LastApplyInjectedCount;
        internal int LastApplyResult;
        internal int AutoApply;
        internal int ShowEquipped;
        internal int ToggleKey;
        internal int Language;
        internal uint AuthorizedStatusCount;
        internal uint AuthorizedCharacterHash;
        internal ulong AuthorizedStatusAddress;
        internal ulong InventoryRevision;
        internal int InventoryDirty;
        internal int EditAllowed;
        internal int UiMode;
        internal int SourceMode;
        internal int EditSessionState;
        internal uint ObservedCharacterHash;
        internal ulong ObservedStatusAddress;
        internal int ObservedStatusContext;
        internal uint LifecycleRebindAttempts;
        internal int InputCaptureRequested;
        internal int InputCaptureEffective;
        internal int InputIatHooksReady;
        internal int DirectInputHookReady;
        internal ulong NaturalBindAttempts;
        internal ulong NaturalBindSuccesses;
        internal ulong NaturalBindStatusAddress;
        internal uint NaturalBindCharacterHash;
        internal int NaturalBindContext;
        internal uint NaturalBindExpectedCount;
        internal uint NaturalBindInjectedCount;
        internal int NaturalBindResult;
        internal ulong OwnerManagerAddress;
        internal uint NaturalBindOwnerKey;
        internal ulong NaturalBindOwnerStatusAddress;
        internal uint VirtualSlotCount;
        internal uint VirtualSlotCapacity;
    }

    internal sealed record InventoryView(
        GemData Gem,
        bool Equipped,
        uint RequiredCharacterHash,
        uint VirtualOwnerCharacterHash,
        int VirtualOwnerSlot,
        string Label)
    {
        internal string Searchable { get; } = Label.ToLowerInvariant();
    }

    internal sealed record PresetApplySummary(PresetSlotResult[] SlotResults);

    internal static void Configure(string modDirectory)
    {
        string path = Path.GetFullPath(Path.Combine(modDirectory, LibraryName));
        lock (ResolverLock)
        {
            if (_libraryPath is not null &&
                !string.Equals(_libraryPath, path, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Native core was already bound to a different path: {_libraryPath}"
                );
            }
            _libraryPath = path;
            if (Interlocked.Exchange(ref _resolverConfigured, 1) == 0)
            {
                NativeLibrary.SetDllImportResolver(
                    typeof(NativeCore).Assembly,
                    ResolveLibrary
                );
            }
        }
    }

    internal static bool Initialize(Action<string> log, bool enableInputHooks = true)
    {
        ArgumentNullException.ThrowIfNull(log);
        lock (NativeLogLock)
            _nativeLogSink = log;

        long nativeLibraryStarted = Stopwatch.GetTimestamp();
        bool nativeLibraryCompleted = false;
        try
        {
            log("Startup phase=native-library-load state=begin.");
            NativeSetLogCallback(
                (IntPtr)(delegate* unmanaged[Cdecl]<sbyte*, void>)&ForwardNativeLog
            );
            uint abiVersion = NativeGetAbiVersion();
            if (abiVersion != AbiVersion)
            {
                throw new InvalidOperationException(
                    $"Native ABI mismatch: managed {AbiVersion}, native {abiVersion}."
                );
            }
            if (NativeSetInputHooksEnabled(enableInputHooks ? 1 : 0) == 0)
            {
                throw new InvalidOperationException(
                    "Native input-hook mode could not be selected before initialization."
                );
            }
            log(
                "Startup phase=native-library-load state=complete " +
                $"elapsed_ms={(long)Stopwatch.GetElapsedTime(nativeLibraryStarted).TotalMilliseconds}."
            );
            nativeLibraryCompleted = true;
            return NativeInitialize() != 0;
        }
        catch
        {
            if (!nativeLibraryCompleted)
            {
                log(
                    "Startup phase=native-library-load state=failed " +
                    $"elapsed_ms={(long)Stopwatch.GetElapsedTime(nativeLibraryStarted).TotalMilliseconds}."
                );
            }
            DetachNativeLogSink();
            throw;
        }
    }

    internal static void Tick() => NativeTick();

    internal static void Shutdown()
    {
        try
        {
            NativeShutdown();
        }
        finally
        {
            DetachNativeLogSink();
        }
    }

    internal static int InvokeOriginalPresent(
        ulong originalFunctionAddress,
        IntPtr swapChain,
        int syncInterval,
        uint presentFlags,
        out uint exceptionCode) =>
        NativeInvokeOriginalPresent(
            originalFunctionAddress,
            swapChain,
            unchecked((uint)syncInterval),
            presentFlags,
            out exceptionCode);

    internal static ulong ResolveHookChainTarget(
        ulong functionAddress,
        uint maxJumpCount,
        out uint jumpCount,
        out HookChainResolveStatus status) =>
        NativeResolveHookChainTarget(
            functionAddress,
            maxJumpCount,
            out jumpCount,
            out status);

    internal static bool TryGetState(out RuntimeState state)
    {
        return NativeGetState(out state, (uint)sizeof(RuntimeState)) != 0 &&
            state.AbiVersion == AbiVersion &&
            state.StructSize == sizeof(RuntimeState) &&
            state.VirtualSlotCapacity == VirtualSlotCapacity &&
            state.VirtualSlotCount is >= 1 and <= VirtualSlotCapacity;
    }

    internal static string GetRuntimeMessage()
    {
        uint required = NativeCopyRuntimeMessage(null, 0);
        if (required <= 1)
            return string.Empty;
        if (required > 64 * 1024)
            required = 64 * 1024;
        byte[] bytes = new byte[required];
        fixed (byte* buffer = bytes)
            NativeCopyRuntimeMessage((sbyte*)buffer, required);
        int length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
            length = bytes.Length;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    internal static bool RefreshInventory() => NativeRefreshInventory() != 0;

    internal static uint GetInventoryCount() => NativeGetInventoryCount();

    internal static bool TryCopyInventoryItem(
        uint index,
        byte[] labelBuffer,
        out InventoryView? view)
    {
        InventoryItem item;
        fixed (byte* buffer = labelBuffer)
        {
            if (NativeCopyInventoryItem(
                    index,
                    out item,
                    (uint)sizeof(InventoryItem),
                    (sbyte*)buffer,
                    (uint)labelBuffer.Length) == 0)
            {
                view = null;
                return false;
            }
        }
        int length = Array.IndexOf(labelBuffer, (byte)0);
        if (length < 0)
            length = labelBuffer.Length;
        string label = Encoding.UTF8.GetString(labelBuffer, 0, length);
        view = new InventoryView(
            item.Gem,
            item.Equipped != 0,
            item.RequiredCharacterHash,
            item.VirtualOwnerCharacterHash,
            item.VirtualOwnerSlot,
            label
        );
        return true;
    }

    internal static uint[] GetSelection(uint characterHash)
    {
        uint[] slots = new uint[VirtualSlotCapacity];
        fixed (uint* slotPointer = slots)
        {
            if (NativeGetSelection(characterHash, slotPointer, VirtualSlotCapacity) == 0)
                Array.Clear(slots);
        }
        return slots;
    }

    internal static bool SetSelection(uint characterHash, int virtualSlot, uint inventorySlotId)
    {
        return NativeSetSelection(characterHash, virtualSlot, inventorySlotId) != 0;
    }

    internal static PresetApplySummary? ApplyPreset(
        IReadOnlyDictionary<uint, uint[]> selections,
        int virtualSlotCount)
    {
        if (selections.Count == 0 || selections.Count > PresetCharacterCapacity)
            return null;
        virtualSlotCount = Math.Clamp(virtualSlotCount, 1, VirtualSlotCapacity);

        KeyValuePair<uint, uint[]>[] ordered = selections
            .OrderBy(pair => pair.Key)
            .ToArray();
        PresetCharacterSelection[] nativeSelections =
            new PresetCharacterSelection[ordered.Length];
        for (int index = 0; index < ordered.Length; ++index)
        {
            PresetCharacterSelection selection = default;
            selection.CharacterHash = ordered[index].Key;
            uint[] sourceSlots = ordered[index].Value;
            for (int slot = 0; slot < VirtualSlotCapacity; ++slot)
            {
                selection.Slots[slot] = slot < sourceSlots.Length
                    ? sourceSlots[slot]
                    : 0;
            }
            nativeSelections[index] = selection;
        }

        PresetSlotResult[] results =
            new PresetSlotResult[ordered.Length * virtualSlotCount];
        uint resultCount = 0;
        fixed (PresetCharacterSelection* selectionPointer = nativeSelections)
        fixed (PresetSlotResult* resultPointer = results)
        {
            if (NativeApplyPreset(
                    selectionPointer,
                    (uint)nativeSelections.Length,
                    resultPointer,
                    (uint)results.Length,
                    &resultCount) == 0 ||
                resultCount > (uint)results.Length)
            {
                return null;
            }
        }
        if (resultCount != results.Length)
            Array.Resize(ref results, (int)resultCount);
        return new PresetApplySummary(results);
    }

    internal static uint RequestApply(uint characterHash) => NativeRequestApply(characterHash);

    internal static bool SetAutoApply(bool enabled) => NativeSetAutoApply(enabled ? 1 : 0) != 0;

    internal static bool SetShowEquipped(bool enabled) =>
        NativeSetShowEquipped(enabled ? 1 : 0) != 0;

    internal static bool SetToggleKey(int virtualKey) => NativeSetToggleKey(virtualKey) != 0;

    internal static bool SetLanguage(int language) => NativeSetLanguage(language) != 0;

    internal static bool SetInputCapture(bool requested) =>
        NativeSetInputCapture(requested ? 1 : 0) != 0;

    internal static bool SetInputCaptureDevices(uint requestedDevices) =>
        NativeSetInputCaptureDevices(requestedDevices) != 0;

    internal static void ForceReleaseInput() => NativeSetInputCapture(-1);

    internal static bool IsInputCaptureActive() => NativeGetInputCaptureActive() != 0;

    internal static bool IsInventoryDirty() => NativeIsInventoryDirty() != 0;

    internal static bool CanEditCharacter(uint characterHash) =>
        NativeCanEditCharacter(characterHash) != 0;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void ForwardNativeLog(sbyte* message)
    {
        try
        {
            string? text = Marshal.PtrToStringUTF8((IntPtr)message);
            if (string.IsNullOrEmpty(text))
                return;
            Action<string>? sink;
            lock (NativeLogLock)
                sink = _nativeLogSink;
            sink?.Invoke("Native: " + text);
        }
        catch
        {
            // A diagnostic callback must never unwind into native hook code.
        }
    }

    private static void DetachNativeLogSink()
    {
        try
        {
            if (_libraryHandle != IntPtr.Zero)
                NativeSetLogCallback(IntPtr.Zero);
        }
        catch
        {
            // The native module may already be unavailable during process teardown.
        }
        lock (NativeLogLock)
            _nativeLogSink = null;
    }

    private static IntPtr ResolveLibrary(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;
        lock (ResolverLock)
        {
            if (_libraryHandle != IntPtr.Zero)
                return _libraryHandle;
            if (_libraryPath is null || !File.Exists(_libraryPath))
                throw new DllNotFoundException($"Native core not found: {_libraryPath}");
            _libraryHandle = NativeLibrary.Load(_libraryPath);
            return _libraryHandle;
        }
    }

}
