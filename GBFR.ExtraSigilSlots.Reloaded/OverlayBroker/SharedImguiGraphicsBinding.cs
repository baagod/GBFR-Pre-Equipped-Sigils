using System.Runtime.InteropServices;
using DearImguiSharp;
using GBFR.OverlayHub.Contracts;

namespace GBFR.OverlayHub.Runtime;

/// <summary>
/// Publishes the exact cimgui module and context owned by the neutral Broker.
/// Peers use this binding instead of loading a private ImGui context.
/// </summary>
internal static class SharedImguiGraphicsBinding
{
    private static readonly object Sync = new();
    private static nint s_nativeLibraryHandle;

    internal static OverlayGraphicsBinding Capture()
    {
        var context = ImGui.GetCurrentContext();
        if (context is null || context.__Instance == nint.Zero)
            throw new InvalidOperationException("The Broker ImGui context is unavailable.");

        nint handle;
        lock (Sync)
        {
            handle = s_nativeLibraryHandle;
            if (handle == nint.Zero)
            {
                handle = NativeLibrary.Load("cimgui", typeof(ImGui).Assembly, searchPath: null);
                if (handle == nint.Zero ||
                    !NativeLibrary.TryGetExport(handle, "igGetCurrentContext", out _))
                {
                    if (handle != nint.Zero)
                        NativeLibrary.Free(handle);
                    throw new InvalidOperationException("The Broker cimgui module could not be resolved.");
                }
                s_nativeLibraryHandle = handle;
            }
        }

        return new OverlayGraphicsBinding(
            OverlayHubProtocol.GraphicsBindingVersion,
            handle,
            context.__Instance);
    }
}
