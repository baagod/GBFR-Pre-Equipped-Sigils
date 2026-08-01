using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DearImguiSharp;
using GBFR.OverlayHub.Contracts;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Imgui.Hook;
using Reloaded.Imgui.Hook.Implementations;

namespace GBFR.OverlayHub.Runtime;

/// <summary>
/// Owns the process's only Reloaded ImGui frame, Present hook and WndProc. It has
/// no Chat or Extra-Sigil business state; both are ordinary broker peers.
/// </summary>
internal sealed class OverlayBrokerHost : IDisposable
{
    private static OverlayBrokerHost? s_activeHost;
    private static int s_hasOriginalWndProc;
    private static WndProcHook.WndProc s_originalWndProc;

    private readonly IOverlayBrokerHostControl _control;
    private readonly Action<string> _log;
    private readonly Action<OverlayInputDevices>? _setNativeInputCapture;
    private readonly Action? _forceNativeInputRelease;
    private readonly Action<bool>? _setNativeCursorRelease;
    private readonly object _lifecycleSync = new();
    private readonly SemaphoreSlim _initializationGate = new(1, 1);
    private int _capturedInputDevices;
    private int _initialized;
    private int _disposed;
    private int _permanentFailureHandled;
    private int _wndProcFailureLogged;
    private int _carrierUpkeepFailureLogged;
    private bool _hasSavedClipRect;
    private NativeRect _savedClipRect;
    private nint _savedCaptureWindow;
    private Action? _renderCallback;
    private Action? _carrierUpkeep;

    internal OverlayBrokerHost(
        IOverlayBrokerHostControl control,
        Action<string> log,
        Action? carrierUpkeep = null,
        Action<OverlayInputDevices>? setNativeInputCapture = null,
        Action? forceNativeInputRelease = null,
        Action<bool>? setNativeCursorRelease = null)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _log = log ?? throw new ArgumentNullException(nameof(log));
        _carrierUpkeep = carrierUpkeep;
        _setNativeInputCapture = setNativeInputCapture;
        _forceNativeInputRelease = forceNativeInputRelease;
        _setNativeCursorRelease = setNativeCursorRelease;
        _control.SetInputCaptureChangedCallback(OnInputCaptureChanged);
    }

    internal bool IsInitialized => Volatile.Read(ref _initialized) != 0;

    internal void SetCarrierUpkeep(Action carrierUpkeep) =>
        Interlocked.Exchange(
            ref _carrierUpkeep,
            carrierUpkeep ?? throw new ArgumentNullException(nameof(carrierUpkeep)));

    internal static bool IsSharedImguiHookClaimed() =>
        ImguiHook.Context is not null ||
        ImguiHook.Render is not null ||
        ImguiHook.Implementations is not null;

    internal async Task InitializeAsync(
        IReloadedHooks hooks,
        Func<Action, Func<bool>, Action, IImguiHook> implementationFactory)
    {
        ArgumentNullException.ThrowIfNull(hooks);
        ArgumentNullException.ThrowIfNull(implementationFactory);
        await _initializationGate.WaitAsync().ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);

            if (Interlocked.CompareExchange(ref s_activeHost, this, null) is not null)
            {
                Interlocked.Exchange(ref _disposed, 1);
                _control.MarkHostUnavailable("another process-local graphics writer is already active");
                throw new InvalidOperationException("A process-local Overlay Broker graphics writer is already active.");
            }
            if (IsSharedImguiHookClaimed())
            {
                Interlocked.CompareExchange(ref s_activeHost, null, this);
                Interlocked.Exchange(ref _disposed, 1);
                _control.MarkHostUnavailable("Reloaded.Imgui.Hook is already owned by an uncoordinated overlay");
                throw new InvalidOperationException(
                    "Reloaded.Imgui.Hook is already owned by an uncoordinated overlay; " +
                    "the Broker refused to install a second writer.");
            }

            _renderCallback = RenderClients;
            IImguiHook? implementation = null;
            try
            {
                SDK.Init(hooks, message => TryLog($"ImGui: {message}"));
                implementation = implementationFactory(
                    PresentTick,
                    _control.HasRenderableClients,
                    HandlePermanentGraphicsFailure);
                await ImguiHook.Create(
                    _renderCallback,
                    new ImguiHookOptions
                    {
                        EnableViewports = false,
                        IgnoreWindowUnactivate = true,
                        CustomWndProcHandlerPointer = GetCustomWndProcPointer(),
                        Implementations = new List<IImguiHook> { implementation },
                    })
                    .ConfigureAwait(false);
                ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                if (!ReferenceEquals(ImguiHook.Render, _renderCallback))
                    throw new InvalidOperationException("Overlay Broker ImGui ownership changed during initialization.");

                _control.PublishGraphicsBinding(SharedImguiGraphicsBinding.Capture());
                lock (_lifecycleSync)
                {
                    ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
                    Volatile.Write(ref _initialized, 1);
                    _control.MarkGraphicsReady();
                }
                TryLog("Neutral Overlay Broker initialized one Present/WndProc writer for all registered peers.");
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _initialized, 0);
                if (_renderCallback is not null && ReferenceEquals(ImguiHook.Render, _renderCallback))
                {
                    try { ImguiHook.Destroy(); }
                    catch (Exception cleanupException)
                    {
                        TryLog($"Overlay Broker initialization cleanup was contained: {cleanupException.Message}");
                    }
                }
                else
                {
                    try { implementation?.Dispose(); } catch { }
                }
                Interlocked.CompareExchange(ref s_activeHost, null, this);
                ForceReleaseInputCapture();
                Interlocked.Exchange(ref _disposed, 1);
                _control.MarkHostUnavailable($"graphics initialization failed: {exception.GetType().Name}");
                throw;
            }
        }
        finally
        {
            _initializationGate.Release();
        }
    }

    private void RenderClients()
    {
        try
        {
            _control.RenderClients();
            var devices = (OverlayInputDevices)Volatile.Read(ref _capturedInputDevices);
            var mouseCaptured = (devices & OverlayInputDevices.Mouse) != 0;
            ImGui.GetIO().MouseDrawCursor = mouseCaptured;
        }
        catch (Exception exception)
        {
            TryLog($"Overlay Broker frame boundary contained an exception: {exception}");
        }
    }

    private void PresentTick()
    {
        var carrierUpkeep = Volatile.Read(ref _carrierUpkeep);
        try { carrierUpkeep?.Invoke(); }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref _carrierUpkeep, null, carrierUpkeep);
            if (Interlocked.Exchange(ref _carrierUpkeepFailureLogged, 1) == 0)
                TryLog($"Overlay Broker isolated its bootstrap upkeep callback: {exception}");
        }
        _control.TickClients();
    }

    private void OnInputCaptureChanged(OverlayInputDevices devices)
    {
        var previous = (OverlayInputDevices)Interlocked.Exchange(
            ref _capturedInputDevices,
            (int)devices);
        if (previous == devices)
            return;
        ApplyInputCapture(devices);
    }

    private void ApplyInputCapture(OverlayInputDevices devices)
    {
        var captureMouse = (devices & OverlayInputDevices.Mouse) != 0;
        try { _setNativeInputCapture?.Invoke(devices); }
        catch (Exception exception) { TryLog($"Broker native input transition was contained: {exception.Message}"); }
        try { _setNativeCursorRelease?.Invoke(captureMouse); }
        catch (Exception exception) { TryLog($"Broker native cursor transition was contained: {exception.Message}"); }

        if (captureMouse)
        {
            BeginReleasedMouse();
            ResetImGuiMouseState();
        }
        else
        {
            ResetImGuiMouseState();
            RestoreMouseCapture();
        }
    }

    private void BeginReleasedMouse()
    {
        if (_hasSavedClipRect || _savedCaptureWindow != nint.Zero)
            return;
        _hasSavedClipRect = GetClipCursor(out _savedClipRect);
        _savedCaptureWindow = GetCapture();
        if (_savedCaptureWindow != nint.Zero)
            ReleaseCapture();
        _ = ClipCursor(nint.Zero);
    }

    private void RestoreMouseCapture()
    {
        if (!_hasSavedClipRect && _savedCaptureWindow == nint.Zero)
            return;
        ReleaseCapture();
        if (_hasSavedClipRect)
            _ = ClipCursorRect(ref _savedClipRect);
        if (_savedCaptureWindow != nint.Zero && IsWindow(_savedCaptureWindow))
            _ = SetCapture(_savedCaptureWindow);
        _hasSavedClipRect = false;
        _savedCaptureWindow = nint.Zero;
    }

    private static void ResetImGuiMouseState()
    {
        try
        {
            var io = ImGui.GetIO();
            if (io is null || io.__Instance == nint.Zero)
                return;
            ImGui.ImGuiIO_ClearInputKeys(io);
            for (var button = 0; button < 5; button++)
                ImGui.ImGuiIO_AddMouseButtonEvent(io, button, false);
            ImGui.ClearActiveID();
        }
        catch
        {
            // The context may not exist during early startup or late teardown.
        }
    }

    private void HandlePermanentGraphicsFailure()
    {
        if (Interlocked.Exchange(ref _permanentFailureHandled, 1) != 0)
            return;
        Volatile.Write(ref _initialized, 0);
        ForceReleaseInputCapture();
        _ = Task.Run(() => ShutdownWriter("permanent graphics backend failure"));
        TryLog("Overlay Broker graphics writer failed closed and scheduled coordinated lease recovery.");
    }

    private static unsafe nint GetCustomWndProcPointer() =>
        (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&CustomWndProc;

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static unsafe nint CustomWndProc(nint hWnd, uint message, nint wParam, nint lParam)
    {
        var host = s_activeHost;
        var originalStarted = false;
        try
        {
            if (host is not null && host.IsInitialized)
            {
                var peerResult = host._control.ObserveWindowMessage(hWnd, message, wParam, lParam);
                if (peerResult.Handled)
                    return peerResult.Result;

                ImGui.ImplWin32_WndProcHandler((void*)hWnd, message, wParam, lParam);
                var devices = (OverlayInputDevices)Volatile.Read(ref host._capturedInputDevices);
                if (ShouldSuppressWindowMessage(message, lParam, devices))
                    return nint.Zero;
            }

            var hook = WndProcHook.Instance;
            if (hook is not null)
            {
                var original = hook.Hook.OriginalFunction;
                s_originalWndProc = original;
                Volatile.Write(ref s_hasOriginalWndProc, 1);
                originalStarted = true;
                return original.Value.Invoke(hWnd, message, wParam, lParam);
            }
            if (Volatile.Read(ref s_hasOriginalWndProc) != 0)
            {
                originalStarted = true;
                return s_originalWndProc.Value.Invoke(hWnd, message, wParam, lParam);
            }
            return CallDefaultWindowProc(hWnd, message, wParam, lParam);
        }
        catch (Exception exception)
        {
            host?.LogWndProcFailure(exception);
            var devices = host is null
                ? OverlayInputDevices.None
                : (OverlayInputDevices)Volatile.Read(ref host._capturedInputDevices);
            if (devices != OverlayInputDevices.None &&
                OverlayWindowInputClassifier.IsAlwaysCaptured(message))
            {
                return nint.Zero;
            }
            if (!originalStarted && Volatile.Read(ref s_hasOriginalWndProc) != 0)
            {
                try { return s_originalWndProc.Value.Invoke(hWnd, message, wParam, lParam); }
                catch { }
            }
            return CallDefaultWindowProc(hWnd, message, wParam, lParam);
        }
    }

    internal static bool ShouldSuppressWindowMessage(
        uint message,
        nint lParam,
        OverlayInputDevices devices) =>
        devices != OverlayInputDevices.None &&
        OverlayWindowInputClassifier.ShouldCapture(message, lParam, devices);

    private void LogWndProcFailure(Exception exception)
    {
        if (Interlocked.Exchange(ref _wndProcFailureLogged, 1) == 0)
            TryLog($"Overlay Broker WndProc recovered after {exception.GetType().Name}: {exception.Message}");
    }

    private void TryLog(string message)
    {
        try { _log(message); } catch { }
    }

    private void ForceReleaseInputCapture()
    {
        Interlocked.Exchange(ref _capturedInputDevices, (int)OverlayInputDevices.None);
        try
        {
            if (_forceNativeInputRelease is not null)
                _forceNativeInputRelease();
            else
                _setNativeInputCapture?.Invoke(OverlayInputDevices.None);
        }
        catch (Exception exception)
        {
            TryLog($"Broker native input force-release was contained: {exception.Message}");
        }
        try { _setNativeCursorRelease?.Invoke(false); }
        catch (Exception exception) { TryLog($"Broker native cursor force-release was contained: {exception.Message}"); }
        ResetImGuiMouseState();
        RestoreMouseCapture();
    }

    public void Dispose()
        => ShutdownWriter("bootstrap peer disposed");

    private void ShutdownWriter(string reason)
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        _initializationGate.Wait();
        try
        {
            lock (_lifecycleSync)
            {
                Volatile.Write(ref _initialized, 0);
                ForceReleaseInputCapture();
                if (_renderCallback is not null && ReferenceEquals(ImguiHook.Render, _renderCallback))
                {
                    try { ImguiHook.Destroy(); } catch (Exception exception) { TryLog($"Broker teardown was contained: {exception.Message}"); }
                }
                Interlocked.CompareExchange(ref s_activeHost, null, this);
            }
        }
        finally
        {
            _initializationGate.Release();
        }
        _control.MarkHostUnavailable(reason);
    }

    private static nint CallDefaultWindowProc(nint hWnd, uint message, nint wParam, nint lParam) =>
        IsWindowUnicode(hWnd)
            ? DefWindowProcW(hWnd, message, wParam, lParam)
            : DefWindowProcA(hWnd, message, wParam, lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        internal int Left;
        internal int Top;
        internal int Right;
        internal int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();
    [DllImport("user32.dll")]
    private static extern bool GetClipCursor(out NativeRect rectangle);
    [DllImport("user32.dll")]
    private static extern bool ClipCursor(nint rectangle);
    [DllImport("user32.dll", EntryPoint = "ClipCursor")]
    private static extern bool ClipCursorRect(ref NativeRect rectangle);
    [DllImport("user32.dll")]
    private static extern nint GetCapture();
    [DllImport("user32.dll")]
    private static extern nint SetCapture(nint window);
    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")]
    private static extern bool IsWindowUnicode(nint window);
    [DllImport("user32.dll")]
    private static extern nint DefWindowProcW(nint hWnd, uint message, nint wParam, nint lParam);
    [DllImport("user32.dll")]
    private static extern nint DefWindowProcA(nint hWnd, uint message, nint wParam, nint lParam);
}
