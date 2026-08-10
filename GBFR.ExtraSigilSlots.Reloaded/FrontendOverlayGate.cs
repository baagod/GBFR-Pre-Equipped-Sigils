using System.Threading;

namespace GBFR.ExtraSigilSlots.Reloaded;

/// <summary>
/// Bridges Win32 input events to the Present thread without touching ImGui.
/// The graphics hook stays installed, but a closed overlay does not start an
/// ImGui frame unless a toggle request is waiting to be consumed.
/// </summary>
internal static class FrontendOverlayGate
{
    private const int DefaultToggleKey = 0x77; // F8
    private const uint WmKeyDown = 0x0100;
    private const uint WmKeyUp = 0x0101;
    private const uint WmSysKeyDown = 0x0104;
    private const uint WmSysKeyUp = 0x0105;
    private const uint WmActivate = 0x0006;
    private const uint WmKillFocus = 0x0008;
    private const uint WmActivateApp = 0x001C;
    private const uint WmCancelMode = 0x001F;
    private const long PreviousKeyStateMask = 1L << 30;

    private static int s_windowOpen;
    private static int s_pendingToggleCount;
    private static int s_toggleKey = DefaultToggleKey;
    private static int s_toggleKeyHeld;

    internal static bool ShouldRenderFrame =>
        Volatile.Read(ref s_windowOpen) != 0 ||
        Volatile.Read(ref s_pendingToggleCount) != 0;

    internal static bool IsOpen => Volatile.Read(ref s_windowOpen) != 0;

    internal static int CurrentToggleKey => Volatile.Read(ref s_toggleKey);

    internal static void SetToggleKey(int virtualKey)
    {
        int normalizedKey = virtualKey is >= 1 and <= 255
            ? virtualKey
            : DefaultToggleKey;
        int previousKey = Interlocked.Exchange(ref s_toggleKey, normalizedKey);
        if (previousKey != normalizedKey)
            Volatile.Write(ref s_toggleKeyHeld, 0);
    }

    internal static void SetOpen(bool open) =>
        Volatile.Write(ref s_windowOpen, open ? 1 : 0);

    internal static bool ObserveWindowMessage(
        uint message,
        IntPtr wParam,
        IntPtr lParam)
    {
        if (message == WmKillFocus ||
            message == WmCancelMode ||
            message == WmActivateApp && wParam == IntPtr.Zero ||
            message == WmActivate && IsInactiveWindowActivation(wParam))
        {
            Volatile.Write(ref s_toggleKeyHeld, 0);
            if (Volatile.Read(ref s_windowOpen) == 0)
                Interlocked.Exchange(ref s_pendingToggleCount, 0);
            return false;
        }

        if (unchecked((int)wParam.ToInt64()) != CurrentToggleKey)
            return false;
        if (message is WmKeyUp or WmSysKeyUp)
        {
            Volatile.Write(ref s_toggleKeyHeld, 0);
            return false;
        }
        if (message is not WmKeyDown and not WmSysKeyDown ||
            (lParam.ToInt64() & PreviousKeyStateMask) != 0 ||
            Interlocked.Exchange(ref s_toggleKeyHeld, 1) != 0)
        {
            return false;
        }

        Interlocked.Increment(ref s_pendingToggleCount);
        return true;
    }

    internal static bool ConsumeToggleRequest() =>
        (Interlocked.Exchange(ref s_pendingToggleCount, 0) & 1) != 0;

    private static bool IsInactiveWindowActivation(IntPtr wParam) =>
        (unchecked((nuint)wParam) & (nuint)0xFFFF) == 0;

    internal static void ForceClosed()
    {
        Volatile.Write(ref s_windowOpen, 0);
        Interlocked.Exchange(ref s_pendingToggleCount, 0);
        Volatile.Write(ref s_toggleKeyHeld, 0);
    }
}
