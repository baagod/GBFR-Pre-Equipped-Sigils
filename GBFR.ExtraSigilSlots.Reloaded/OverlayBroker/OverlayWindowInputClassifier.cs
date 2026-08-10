using System.Runtime.InteropServices;
using GBFR.OverlayHub.Contracts;

namespace GBFR.OverlayHub.Runtime;

/// <summary>
/// Applies the Broker's aggregate device policy to Win32 and raw-input messages.
/// Controller/HID messages are deliberately outside this classifier.
/// </summary>
internal static class OverlayWindowInputClassifier
{
    internal static bool ShouldCapture(uint message, nint lParam, OverlayInputDevices devices)
    {
        if (ShouldCaptureWithoutRawInput(message, devices))
            return true;
        if (message != 0x00FF)
            return false;

        return ShouldCaptureRawInputType(GetRawInputType(lParam), devices);
    }

    internal static bool ShouldCaptureWithoutRawInput(
        uint message,
        OverlayInputDevices devices)
    {
        if ((devices & OverlayInputDevices.Mouse) != 0 &&
            (message is >= 0x00A0 and <= 0x00AD || message is >= 0x0200 and <= 0x020E))
        {
            return true;
        }
        if ((devices & OverlayInputDevices.Keyboard) != 0 &&
            message is 0x0100 or 0x0101 or 0x0104 or 0x0105)
        {
            return true;
        }
        if ((devices & OverlayInputDevices.Text) != 0 &&
            message is 0x0102 or 0x0106 or 0x0109 or 0x010D or 0x010E or 0x010F or 0x0286)
        {
            return true;
        }
        return false;
    }

    internal static bool ShouldCaptureRawInputType(int type, OverlayInputDevices devices) =>
        type switch
        {
            0 => (devices & OverlayInputDevices.Mouse) != 0,
            1 => (devices & OverlayInputDevices.Keyboard) != 0,
            _ => false,
        };

    private static int GetRawInputType(nint handle)
    {
        if (handle == nint.Zero)
            return -1;
        var headerSize = (uint)Marshal.SizeOf<RawInputHeader>();
        var dataSize = headerSize;
        var copied = GetRawInputData(handle, 0x10000005, out var header, ref dataSize, headerSize);
        return copied != uint.MaxValue && copied >= headerSize
            ? unchecked((int)header.Type)
            : -1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputHeader
    {
        internal uint Type;
        internal uint Size;
        internal nint Device;
        internal nint WParam;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(
        nint rawInputHandle,
        uint command,
        out RawInputHeader data,
        ref uint dataSize,
        uint headerSize);
}
