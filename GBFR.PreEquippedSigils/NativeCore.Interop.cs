using System.Runtime.InteropServices;

namespace GBFR.PreEquippedSigils;

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
    private static extern uint GBFR20_CopyRuntimeMessage(sbyte* buffer, uint bufferSize);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct TemplateSlotNative
    {
        public uint GemId;
        public uint Trait1;
        public int Trait1Level;
        public uint Trait2;
        public int Trait2Level;
        public int SigilLevel;
    }

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int GBFR20_SetCustomLoadout(TemplateSlotNative[]? slots, uint count);

    private static uint NativeGetAbiVersion() => GBFR20_GetAbiVersion();
    private static void NativeSetLogCallback(IntPtr callback) => GBFR20_SetLogCallback(callback);
    private static int NativeInitialize() => GBFR20_Initialize();
    private static void NativeTick() => GBFR20_Tick();
    private static void NativeShutdown() => GBFR20_Shutdown();
    private static uint NativeCopyRuntimeMessage(sbyte* buffer, uint size) =>
        GBFR20_CopyRuntimeMessage(buffer, size);
    private static int NativeSetCustomLoadout(TemplateSlotNative[]? slots, uint count) =>
        GBFR20_SetCustomLoadout(slots, count);
}
