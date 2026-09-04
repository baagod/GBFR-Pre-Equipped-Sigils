using System.Diagnostics;
using System.Runtime.InteropServices;

namespace GBFR.PreEquippedSigils;

/// <summary>
/// Polls the configured hotkey on the mod upkeep tick (250 ms) and launches
/// the loadout editor tool when pressed while the game is in the foreground.
/// </summary>
internal static class Hotkey
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    private static bool _wasDown;
    private static int _virtualKey = -1;
    private static string _modDirectory = "";

    internal static void Configure(string modDirectory, int virtualKey)
    {
        _modDirectory = modDirectory;
        _virtualKey = virtualKey;
        _wasDown = false;
    }

    internal static void UpdateHotkey(int virtualKey)
    {
        _virtualKey = virtualKey;
        _wasDown = false;
    }

    internal static void Tick(Action<string> log)
    {
        if (_virtualKey < 0 || _modDirectory.Length == 0)
            return;

        bool down = (GetAsyncKeyState(_virtualKey) & 0x8000) != 0;
        if (down && !_wasDown && IsGameForeground())
            TryLaunchTool(log);
        _wasDown = down;
    }

    private static bool IsGameForeground()
    {
        IntPtr foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero)
            return false;
        GetWindowThreadProcessId(foreground, out uint processId);
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return string.Equals(
                process.ProcessName, "granblue_fantasy_relink",
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static void TryLaunchTool(Action<string> log)
    {
        try
        {
            string toolPath = Path.Combine(_modDirectory, "LoadoutTool.exe");
            if (!File.Exists(toolPath))
            {
                log("LoadoutTool.exe not found in the mod directory.");
                return;
            }
            using var process = Process.Start(new ProcessStartInfo(
                toolPath, $"--mod-dir \"{_modDirectory}\"")
            {
                UseShellExecute = true,
            });
            log("Launched loadout editor tool.");
        }
        catch (Exception exception)
        {
            log($"Failed to launch loadout tool: {exception.Message}");
        }
    }
}
