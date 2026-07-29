namespace GBFR.OverlayHub.Contracts;

public static class OverlayHubProtocol
{
    public const int ApiVersion = 2;
    public const int GraphicsBindingVersion = 1;
}

[Flags]
public enum OverlayInputDevices
{
    None = 0,
    Keyboard = 1 << 0,
    Mouse = 1 << 1,
    Text = 1 << 2,
}

public readonly struct OverlayWindowMessageResult
{
    public OverlayWindowMessageResult(bool handled, nint result)
    {
        Handled = handled;
        Result = result;
    }

    public bool Handled { get; }

    public nint Result { get; }

    public static OverlayWindowMessageResult Continue => default;

    public static OverlayWindowMessageResult HandledWith(nint result) => new(true, result);
}

public readonly struct OverlayGraphicsBinding
{
    public OverlayGraphicsBinding(
        int version,
        nint nativeLibraryHandle,
        nint contextPointer)
    {
        Version = version;
        NativeLibraryHandle = nativeLibraryHandle;
        ContextPointer = contextPointer;
    }

    public int Version { get; }

    public nint NativeLibraryHandle { get; }

    public nint ContextPointer { get; }

    public bool IsValid =>
        Version == OverlayHubProtocol.GraphicsBindingVersion &&
        NativeLibraryHandle != nint.Zero &&
        ContextPointer != nint.Zero;
}

public interface IGbfrOverlayClient
{
    string ModId { get; }

    bool WantsRender { get; }

    void Tick();

    void Render();

    OverlayWindowMessageResult ObserveWindowMessage(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam);

    void OnHostUnavailable(string reason);
}

/// <summary>
/// An ImGui guest that can bind its managed wrapper to the host's exact native
/// cimgui module and context. This is required when Reloaded loads identical
/// DearImguiSharp assemblies into separate AssemblyLoadContexts.
/// </summary>
public interface IGbfrOverlayGraphicsClient : IGbfrOverlayClient
{
    bool BindGraphics(OverlayGraphicsBinding binding);
}

public interface IGbfrOverlayRegistration : IDisposable
{
    Guid Token { get; }

    bool SetEnabled(bool enabled);

    bool SetInputCapture(OverlayInputDevices devices);
}

public interface IGbfrOverlayHub
{
    int ApiVersion { get; }

    string HostModId { get; }

    bool IsGraphicsReady { get; }

    OverlayInputDevices CapturedInputDevices { get; }

    /// <summary>
    /// Registers a peer. The caller must retain a strong reference to
    /// <paramref name="client"/> for at least as long as the returned registration;
    /// the Broker intentionally stores only a weak reference.
    /// </summary>
    IGbfrOverlayRegistration Register(IGbfrOverlayClient client);
}

/// <summary>
/// Optional recovery capability implemented by brokers that can transfer their
/// single graphics-writer lease after the previous host exits. Older compatible
/// brokers remain usable but continue to fail closed after host loss.
/// </summary>
public interface IRecoverableGbfrOverlayHub : IGbfrOverlayHub
{
    bool IsHostAvailable { get; }

    IOverlayBrokerHostControl? TryAcquireHost(string candidateModId);
}
