namespace GBFR.ExtraSigilSlots.Reloaded;

internal static class MouseButtonStateTracker
{
    internal const uint Left = 1u << 0;
    internal const uint Right = 1u << 1;
    internal const uint Middle = 1u << 2;
    internal const uint Extra1 = 1u << 3;
    internal const uint Extra2 = 1u << 4;

    private static int s_pressedButtons;
    private static long s_buttonEventSequence;

    internal static uint PressedButtons =>
        unchecked((uint)Volatile.Read(ref s_pressedButtons));

    internal static long ButtonEventSequence =>
        Volatile.Read(ref s_buttonEventSequence);

    internal static void ObserveWindowMessage(uint message, IntPtr wParam)
    {
        uint setMask = 0;
        uint clearMask = 0;
        switch (message)
        {
            case 0x00A1: // WM_NCLBUTTONDOWN
            case 0x00A3: // WM_NCLBUTTONDBLCLK
            case 0x0201: // WM_LBUTTONDOWN
            case 0x0203: // WM_LBUTTONDBLCLK
                setMask = Left;
                break;
            case 0x00A2: // WM_NCLBUTTONUP
            case 0x0202: // WM_LBUTTONUP
                clearMask = Left;
                break;

            case 0x00A4: // WM_NCRBUTTONDOWN
            case 0x00A6: // WM_NCRBUTTONDBLCLK
            case 0x0204: // WM_RBUTTONDOWN
            case 0x0206: // WM_RBUTTONDBLCLK
                setMask = Right;
                break;
            case 0x00A5: // WM_NCRBUTTONUP
            case 0x0205: // WM_RBUTTONUP
                clearMask = Right;
                break;

            case 0x00A7: // WM_NCMBUTTONDOWN
            case 0x00A9: // WM_NCMBUTTONDBLCLK
            case 0x0207: // WM_MBUTTONDOWN
            case 0x0209: // WM_MBUTTONDBLCLK
                setMask = Middle;
                break;
            case 0x00A8: // WM_NCMBUTTONUP
            case 0x0208: // WM_MBUTTONUP
                clearMask = Middle;
                break;

            case 0x00AB: // WM_NCXBUTTONUP
            case 0x020C: // WM_XBUTTONUP
                clearMask = ExtraButtonMask(wParam);
                break;
            case 0x00AA: // WM_NCXBUTTONDOWN
            case 0x00AC: // WM_NCXBUTTONDBLCLK
            case 0x020B: // WM_XBUTTONDOWN
            case 0x020D: // WM_XBUTTONDBLCLK
                setMask = ExtraButtonMask(wParam);
                break;
            default:
                return;
        }

        while (true)
        {
            int observed = Volatile.Read(ref s_pressedButtons);
            uint updated = (unchecked((uint)observed) | setMask) & ~clearMask;
            if (Interlocked.CompareExchange(
                    ref s_pressedButtons,
                    unchecked((int)updated),
                    observed) == observed)
            {
                Interlocked.Increment(ref s_buttonEventSequence);
                return;
            }
        }
    }

    internal static void Reset()
    {
        Volatile.Write(ref s_pressedButtons, 0);
        Interlocked.Increment(ref s_buttonEventSequence);
    }

    private static uint ExtraButtonMask(IntPtr wParam)
    {
        uint button = unchecked((uint)(nuint)wParam) >> 16;
        return button switch
        {
            1 => Extra1,
            2 => Extra2,
            _ => 0,
        };
    }
}
