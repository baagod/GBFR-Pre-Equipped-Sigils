using DearImguiSharp;
using System.Runtime.InteropServices;
using GBFR.OverlayHub.Contracts;

namespace GBFR.ExtraSigilSlots.Reloaded;

public sealed partial class Mod
{
    private void SetInputCapture(bool capture)
    {
        if (_frontendFailClosed)
        {
            Volatile.Write(ref _hostedInputCapture, 0);
            ClearTextInputState();
            return;
        }

        var devices = capture
            ? OverlayInputDevices.Keyboard |
              OverlayInputDevices.Mouse |
              OverlayInputDevices.Text
            : OverlayInputDevices.None;
        bool accepted = _overlayRegistration?.SetInputCapture(devices) == true;
        Volatile.Write(ref _hostedInputCapture, capture && accepted ? 1 : 0);
        ClearTextInputState();
    }

    private void ForceReleaseInputCapture()
    {
        if (_frontendFailClosed)
        {
            Volatile.Write(ref _hostedInputCapture, 0);
            ClearTextInputState();
            return;
        }

        _overlayRegistration?.SetInputCapture(OverlayInputDevices.None);
        Volatile.Write(ref _hostedInputCapture, 0);
        ClearTextInputState();
    }

    private static unsafe bool TryHandleCapturedTextInput(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam,
        out IntPtr result)
    {
        const uint WmChar = 0x0102;
        const uint WmUniChar = 0x0109;
        const uint UnicodeNoChar = 0xFFFF;
        const uint WmImeStartComposition = 0x010D;
        const uint WmImeEndComposition = 0x010E;
        const uint WmImeComposition = 0x010F;
        const uint WmImeChar = 0x0286;
        const long GcsResultStr = 0x0800;

        result = IntPtr.Zero;
        switch (message)
        {
            case WmChar:
                if (Volatile.Read(ref s_imeResultInjected) != 0)
                    return true;
                return TryHandleWindowChar(hWnd, wParam);

            case WmUniChar:
            {
                uint codePoint = unchecked((uint)(nuint)wParam);
                if (codePoint == UnicodeNoChar)
                {
                    result = new IntPtr(1);
                    return true;
                }
                if (codePoint <= 0x10FFFF)
                    ImGui.ImGuiIO_AddInputCharacter(ImGui.GetIO(), codePoint);
                return true;
            }

            case WmImeStartComposition:
                ClearPendingAnsiCharacter();
                Volatile.Write(ref s_imeCompositionActive, 1);
                Volatile.Write(ref s_imeResultInjected, 0);
                result = DefWindowProcW(hWnd, message, wParam, lParam);
                return true;

            case WmImeEndComposition:
                ClearPendingAnsiCharacter();
                Volatile.Write(ref s_imeCompositionActive, 0);
                Volatile.Write(ref s_imeResultInjected, 0);
                result = DefWindowProcW(hWnd, message, wParam, lParam);
                return true;

            case WmImeComposition:
                if ((lParam.ToInt64() & GcsResultStr) != 0 && TryInjectImeResult(hWnd))
                {
                    Volatile.Write(ref s_imeResultInjected, 1);
                    return true;
                }

                Volatile.Write(ref s_imeResultInjected, 0);
                result = DefWindowProcW(hWnd, message, wParam, lParam);
                return true;

            case WmImeChar:
                if (Volatile.Read(ref s_imeResultInjected) != 0)
                {
                    result = new IntPtr(1);
                    return true;
                }
                if (IsWindowUnicode(hWnd))
                {
                    result = DefWindowProcW(hWnd, message, wParam, lParam);
                    return true;
                }

                ClearPendingAnsiCharacter();
                if (TryDecodeAnsiCharacter(
                        GetKeyboardCodePage(hWnd),
                        unchecked((ushort)(nuint)wParam),
                        out ushort character))
                {
                    ImGui.ImGuiIO_AddInputCharacterUTF16(ImGui.GetIO(), character);
                }
                result = new IntPtr(1);
                return true;

            default:
                return false;
        }
    }

    private static bool TryHandleWindowChar(IntPtr hWnd, IntPtr wParam)
    {
        uint value = unchecked((uint)(nuint)wParam);
        uint codePage = GetKeyboardCodePage(hWnd);
        if (value == 0 || value > ushort.MaxValue)
            return true;
        if (value > byte.MaxValue)
        {
            ClearPendingAnsiCharacter();
            if (IsWindowUnicode(hWnd))
            {
                ImGui.ImGuiIO_AddInputCharacterUTF16(
                    ImGui.GetIO(),
                    unchecked((ushort)value)
                );
                return true;
            }
            return InjectDecodedAnsiCharacter(codePage, unchecked((ushort)value));
        }

        byte current = unchecked((byte)value);
        if (TryDecodeWindowByte(codePage, current, out ushort character))
            ImGui.ImGuiIO_AddInputCharacterUTF16(ImGui.GetIO(), character);
        return true;
    }

    private static bool TryDecodeWindowByte(
        uint codePage,
        byte current,
        out ushort character)
    {
        character = 0;
        int pendingLead = Interlocked.Exchange(ref s_pendingAnsiLeadByte, 0);
        if (pendingLead != 0)
        {
            uint pendingCodePage = unchecked((uint)Interlocked.Exchange(
                ref s_pendingAnsiCodePage,
                0
            ));
            if (pendingCodePage != 0)
            {
                ushort packed = (ushort)((pendingLead << 8) | current);
                if (TryDecodeAnsiCharacter(pendingCodePage, packed, out character))
                    return true;
            }
        }

        uint leadCodePage = codePage;
        if (!IsDBCSLeadByteEx(leadCodePage, current) &&
            leadCodePage != 936 &&
            Volatile.Read(ref s_imeCompositionActive) != 0 &&
            IsDBCSLeadByteEx(936, current))
        {
            leadCodePage = 936;
        }
        if (IsDBCSLeadByteEx(leadCodePage, current))
        {
            Volatile.Write(ref s_pendingAnsiCodePage, unchecked((int)leadCodePage));
            Volatile.Write(ref s_pendingAnsiLeadByte, current);
            return false;
        }

        if (current < 0x80)
        {
            character = current;
            return true;
        }
        return TryDecodeAnsiCharacter(codePage, current, out character);
    }

    private static unsafe bool TryInjectImeResult(IntPtr hWnd)
    {
        const uint GcsResultStr = 0x0800;
        IntPtr inputContext = ImmGetContext(hWnd);
        if (inputContext == IntPtr.Zero)
            return false;

        try
        {
            int requiredBytes = ImmGetCompositionStringW(
                inputContext,
                GcsResultStr,
                null,
                0
            );
            if (requiredBytes <= 0 || (requiredBytes & 1) != 0 || requiredBytes > 8192)
                return false;

            byte[] buffer = new byte[requiredBytes];
            fixed (byte* bufferPointer = buffer)
            {
                int copiedBytes = ImmGetCompositionStringW(
                    inputContext,
                    GcsResultStr,
                    bufferPointer,
                    unchecked((uint)buffer.Length)
                );
                if (copiedBytes <= 0 || (copiedBytes & 1) != 0)
                    return false;

                ushort* characters = (ushort*)bufferPointer;
                int characterCount = copiedBytes / sizeof(ushort);
                for (int index = 0; index < characterCount; ++index)
                {
                    ImGui.ImGuiIO_AddInputCharacterUTF16(
                        ImGui.GetIO(),
                        characters[index]
                    );
                }
                return true;
            }
        }
        finally
        {
            ImmReleaseContext(hWnd, inputContext);
        }
    }

    private static void ClearPendingAnsiCharacter()
    {
        Interlocked.Exchange(ref s_pendingAnsiLeadByte, 0);
        Interlocked.Exchange(ref s_pendingAnsiCodePage, 0);
    }

    private static void ClearTextInputState()
    {
        ClearPendingAnsiCharacter();
        Volatile.Write(ref s_imeCompositionActive, 0);
        Volatile.Write(ref s_imeResultInjected, 0);
    }

    private static bool InjectDecodedAnsiCharacter(uint codePage, ushort packed)
    {
        if (!TryDecodeAnsiCharacter(codePage, packed, out ushort character))
            return true;
        ImGui.ImGuiIO_AddInputCharacterUTF16(ImGui.GetIO(), character);
        return true;
    }

    internal static unsafe bool TryDecodeAnsiCharacter(
        uint codePage,
        ushort packed,
        out ushort character)
    {
        byte* bytes = stackalloc byte[2];
        byte first = unchecked((byte)(packed >> 8));
        int byteCount;
        if (first == 0)
        {
            bytes[0] = unchecked((byte)packed);
            byteCount = 1;
        }
        else
        {
            bytes[0] = first;
            bytes[1] = unchecked((byte)packed);
            byteCount = 2;
        }

        char decoded = '\0';
        int count = MultiByteToWideChar(
            codePage,
            0x00000001,
            bytes,
            byteCount,
            &decoded,
            1
        );
        character = decoded;
        return count == 1 && decoded != '\0';
    }

    private static uint GetKeyboardCodePage(IntPtr hWnd)
    {
        uint threadId = GetWindowThreadProcessId(hWnd, out _);
        ulong layout = unchecked((ulong)GetKeyboardLayout(threadId).ToInt64());
        ushort lowLanguage = unchecked((ushort)layout);
        if (TryGetAnsiCodePage(lowLanguage, out uint codePage))
            return codePage;
        return GetACP();
    }

    private static bool TryGetAnsiCodePage(ushort language, out uint codePage)
    {
        const uint LocaleReturnNumber = 0x20000000;
        const uint LocaleDefaultAnsiCodePage = 0x00001004;
        codePage = 0;
        return language != 0 &&
            GetLocaleInfoA(
                language,
                LocaleReturnNumber | LocaleDefaultAnsiCodePage,
                out codePage,
                sizeof(uint)
            ) != 0 &&
            codePage != 0;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProcW(
        IntPtr hWnd,
        uint message,
        IntPtr wParam,
        IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowUnicode(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern int GetLocaleInfoA(
        uint locale,
        uint localeType,
        out uint localeData,
        int localeDataLength);

    [DllImport("kernel32.dll")]
    private static extern uint GetACP();

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsDBCSLeadByteEx(uint codePage, byte testChar);

    [DllImport("kernel32.dll")]
    private static extern unsafe int MultiByteToWideChar(
        uint codePage,
        uint flags,
        byte* multiByteText,
        int multiByteCount,
        char* wideText,
        int wideCount);

    [DllImport("imm32.dll")]
    private static extern IntPtr ImmGetContext(IntPtr hWnd);

    [DllImport("imm32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ImmReleaseContext(IntPtr hWnd, IntPtr inputContext);

    [DllImport("imm32.dll", ExactSpelling = true)]
    private static extern unsafe int ImmGetCompositionStringW(
        IntPtr inputContext,
        uint index,
        void* buffer,
        uint bufferLength);

}
