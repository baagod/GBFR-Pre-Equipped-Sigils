using System.Runtime.InteropServices;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal static class MouseButtonStateTracker
{
    private const uint WmActivate = 0x0006;
    private const uint WmSetFocus = 0x0007;
    private const uint WmKillFocus = 0x0008;
    private const uint WmActivateApp = 0x001C;
    private const uint WmCancelMode = 0x001F;
    private const uint WmCaptureChanged = 0x0215;
    private const int VkLeftButton = 0x01;
    private const int VkRightButton = 0x02;
    private const int VkMiddleButton = 0x04;
    private const int VkExtraButton1 = 0x05;
    private const int VkExtraButton2 = 0x06;

    internal const uint Left = 1u << 0;
    internal const uint Right = 1u << 1;
    internal const uint Middle = 1u << 2;
    internal const uint Extra1 = 1u << 3;
    internal const uint Extra2 = 1u << 4;
    private const uint AllButtons = Left | Right | Middle | Extra1 | Extra2;

    private static readonly object s_stateLock = new();
    private static uint s_pressedButtons;
    private static long s_buttonEventSequence;

    internal static uint PressedButtons
    {
        get
        {
            lock (s_stateLock)
                return s_pressedButtons;
        }
    }

    internal static long ButtonEventSequence
    {
        get
        {
            lock (s_stateLock)
                return s_buttonEventSequence;
        }
    }

    internal static void ObserveWindowMessage(uint message, IntPtr wParam)
    {
        if (RequiresPhysicalStateSynchronization(message, wParam))
        {
            SynchronizePhysicalState();
            return;
        }

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

            case 0x00AC: // WM_NCXBUTTONUP
            case 0x020C: // WM_XBUTTONUP
                clearMask = ExtraButtonMask(wParam);
                break;
            case 0x00AB: // WM_NCXBUTTONDOWN
            case 0x00AD: // WM_NCXBUTTONDBLCLK
            case 0x020B: // WM_XBUTTONDOWN
            case 0x020D: // WM_XBUTTONDBLCLK
                setMask = ExtraButtonMask(wParam);
                break;
            default:
                return;
        }

        lock (s_stateLock)
        {
            s_pressedButtons = (s_pressedButtons | setMask) & ~clearMask;
            ++s_buttonEventSequence;
        }
    }

    internal static void Reset()
    {
        lock (s_stateLock)
        {
            s_pressedButtons = 0;
            ++s_buttonEventSequence;
        }
    }

    internal static long SynchronizePhysicalState()
    {
        return SynchronizeState(BuildPressedButtons(IsKeyPressed));
    }

    internal static uint BuildPressedButtons(Func<int, bool> isKeyPressed)
    {
        ArgumentNullException.ThrowIfNull(isKeyPressed);
        uint pressedButtons = 0;
        if (isKeyPressed(VkLeftButton))
            pressedButtons |= Left;
        if (isKeyPressed(VkRightButton))
            pressedButtons |= Right;
        if (isKeyPressed(VkMiddleButton))
            pressedButtons |= Middle;
        if (isKeyPressed(VkExtraButton1))
            pressedButtons |= Extra1;
        if (isKeyPressed(VkExtraButton2))
            pressedButtons |= Extra2;
        return pressedButtons;
    }

    internal static long SynchronizeState(uint pressedButtons)
    {
        lock (s_stateLock)
        {
            s_pressedButtons = pressedButtons & AllButtons;
            return ++s_buttonEventSequence;
        }
    }

    internal static bool RequiresPhysicalStateSynchronization(uint message, IntPtr wParam) =>
        message is WmActivate or WmSetFocus or WmKillFocus or
            WmActivateApp or WmCancelMode or WmCaptureChanged;

    internal static void ReadSnapshot(out uint pressedButtons, out long buttonEventSequence)
    {
        lock (s_stateLock)
        {
            pressedButtons = s_pressedButtons;
            buttonEventSequence = s_buttonEventSequence;
        }
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

    private static bool IsKeyPressed(int virtualKey) =>
        (GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int virtualKey);
}
