using System.Reflection;
using System.Runtime.InteropServices;
using DearImguiSharp;
using GBFR.OverlayHub.Contracts;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal static class HostedImguiBinding
{
    private static readonly object Sync = new();
    private static nint s_nativeLibraryHandle;
    private static nint s_contextPointer;
    private static ImGuiContext? s_context;
    private static bool s_resolverInstalled;
    private static int s_bound;

    internal static bool IsBound => Volatile.Read(ref s_bound) != 0;

    internal static bool TryBind(OverlayGraphicsBinding binding, Action<string> log)
    {
        ArgumentNullException.ThrowIfNull(log);
        if (!binding.IsValid)
        {
            log("Overlay Hub supplied an invalid ImGui graphics binding.");
            return false;
        }

        lock (Sync)
        {
            if (Volatile.Read(ref s_bound) != 0)
            {
                bool sameBinding =
                    s_nativeLibraryHandle == binding.NativeLibraryHandle &&
                    s_contextPointer == binding.ContextPointer;
                if (!sameBinding)
                    log("Overlay Hub attempted to replace an active ImGui graphics binding.");
                return sameBinding;
            }

            if (s_nativeLibraryHandle != nint.Zero &&
                s_nativeLibraryHandle != binding.NativeLibraryHandle)
            {
                log("The Extra Sigil ImGui assembly is already pinned to another cimgui module.");
                return false;
            }

            s_nativeLibraryHandle = binding.NativeLibraryHandle;
            if (!s_resolverInstalled)
            {
                try
                {
                    NativeLibrary.SetDllImportResolver(
                        typeof(ImGui).Assembly,
                        ResolveHostedNativeLibrary);
                    s_resolverInstalled = true;
                }
                catch (InvalidOperationException)
                {
                    // Another resolver may already own this exact assembly. Validation
                    // below accepts it only when it resolves to the host's context.
                    log(
                        "DearImguiSharp already had a native resolver; validating that it " +
                        "targets the Overlay Hub context.");
                }
            }

            try
            {
                ImGuiContext? current = ImGui.GetCurrentContext();
                ImGuiIO? io = ImGui.GetIO();
                if (current is null ||
                    current.__Instance != binding.ContextPointer ||
                    io is null ||
                    io.__Instance == nint.Zero)
                {
                    log(
                        "The Extra Sigil DearImguiSharp wrapper did not resolve to the host's " +
                        "published ImGui context.");
                    return false;
                }

                s_context = current;
                s_contextPointer = binding.ContextPointer;
                Volatile.Write(ref s_bound, 1);
                log(
                    "Shared ImGui graphics binding accepted; Extra Sigil now renders through " +
                    "the Broker's cimgui module and context.");
                return true;
            }
            catch (Exception exception)
            {
                log(
                    "Shared ImGui graphics binding failed: " +
                    $"{exception.GetType().Name}: {exception.Message}");
                return false;
            }
        }
    }

    internal static bool EnsureCurrentContext()
    {
        if (Volatile.Read(ref s_bound) == 0)
            return false;

        lock (Sync)
        {
            if (s_context is null || s_contextPointer == nint.Zero)
                return false;

            ImGuiContext? current = ImGui.GetCurrentContext();
            if (current is null || current.__Instance != s_contextPointer)
                ImGui.SetCurrentContext(s_context);

            ImGuiIO? io = ImGui.GetIO();
            return io is not null && io.__Instance != nint.Zero;
        }
    }

    private static nint ResolveHostedNativeLibrary(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath) =>
        string.Equals(libraryName, "cimgui", StringComparison.OrdinalIgnoreCase)
            ? s_nativeLibraryHandle
            : nint.Zero;
}
