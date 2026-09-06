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
}
