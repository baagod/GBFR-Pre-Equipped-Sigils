using DearImguiSharp;

namespace GBFR.OverlayHub.Runtime;

internal static unsafe class ImGuiInputResetGate
{
    private static int s_pending;

    internal static void Request() => Volatile.Write(ref s_pending, 1);

    internal static bool Consume() => Interlocked.Exchange(ref s_pending, 0) != 0;

    internal static void ResetWin32MouseButtons(nint windowHandle)
    {
        if (windowHandle == nint.Zero)
            return;

        ImGui.ImplWin32_WndProcHandler((void*)windowHandle, 0x0202, nint.Zero, nint.Zero);
        ImGui.ImplWin32_WndProcHandler((void*)windowHandle, 0x0205, nint.Zero, nint.Zero);
        ImGui.ImplWin32_WndProcHandler((void*)windowHandle, 0x0208, nint.Zero, nint.Zero);
        ImGui.ImplWin32_WndProcHandler(
            (void*)windowHandle,
            0x020C,
            (nint)(1 << 16),
            nint.Zero);
        ImGui.ImplWin32_WndProcHandler(
            (void*)windowHandle,
            0x020C,
            (nint)(2 << 16),
            nint.Zero);
    }
}
