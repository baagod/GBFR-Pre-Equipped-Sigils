using GBFR.OverlayHub.Contracts;
using GBFR.OverlayHub.Runtime;
using Reloaded.Hooks.ReloadedII.Interfaces;

namespace GBFR.ExtraSigilSlots.Reloaded;

public sealed partial class Mod
{
    private OverlayWindowMessageResult ObserveHostedWindowMessage(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam)
    {
        bool resetImGuiMouseState =
            MouseButtonStateTracker.RequiresPhysicalStateSynchronization(message, wParam);
        MouseButtonStateTracker.ObserveWindowMessage(message, wParam);
        if (resetImGuiMouseState)
            ImGuiInputResetGate.Request();
        FrontendOverlayGate.ObserveWindowMessage(message, wParam, lParam);
        if (Volatile.Read(ref _hostedInputCapture) == 0)
            return OverlayWindowMessageResult.Continue;

        if (message == 0x0051)
            ClearTextInputState();
        else if (message is 0x0100 or 0x0101 or 0x0104 or 0x0105)
        {
            ClearPendingAnsiCharacter();
            if ((message == 0x0100 || message == 0x0104) &&
                Volatile.Read(ref s_imeResultInjected) != 0)
            {
                Volatile.Write(ref s_imeCompositionActive, 0);
                Volatile.Write(ref s_imeResultInjected, 0);
            }
        }

        if (!TryHandleCapturedTextInput(
                windowHandle,
                message,
                wParam,
                lParam,
                out var result))
        {
            return OverlayWindowMessageResult.Continue;
        }

        return OverlayWindowMessageResult.HandledWith(result);
    }

    private void HandleOverlayPeerFailure(string reason)
    {
        Volatile.Write(ref _renderStopping, 1);
        ForceReleaseInputCapture();
        _ui?.Close();
        _frontendFailClosed = true;
        Log(
            $"The Extra Sigil overlay frontend failed closed " +
            $"without installing a second graphics/input hook. Reason: {reason}.");
    }

    private void HandleBrokerUnavailable(string reason)
    {
        HandleOverlayPeerFailure(reason);
        if (reason.StartsWith("peer-local failure", StringComparison.Ordinal))
            return;

        Volatile.Write(ref _awaitingBrokerRebind, 1);
        _ownsOverlayBroker = false;
        RequestOverlayBrokerRecovery(reason);
    }

    private bool BindOverlayGraphics(OverlayGraphicsBinding binding)
    {
        if (!HostedImguiBinding.TryBind(binding, Log))
            return false;
        if (Interlocked.Exchange(ref _awaitingBrokerRebind, 0) == 0)
            return true;

        lock (_imguiOperationLock)
        {
            if (Volatile.Read(ref _disposing) != 0)
                return true;
            _frontendFailClosed = false;
            Volatile.Write(ref _renderStopping, 0);
        }
        Log("Extra Sigil rebound to the replacement Overlay Broker host.");
        return true;
    }

    private void RequestOverlayBrokerRecovery(string reason)
    {
        lock (_brokerRecoverySync)
        {
            if (Volatile.Read(ref _disposing) != 0 ||
                _modLoader is not { } loader ||
                _reloadedHooks is not { } hooks)
            {
                return;
            }

            lock (_lifecycleLock)
            {
                if (!_started || _disposed || !_nativeCoreActive)
                    return;
            }

            if (!NativeCore.TryGetState(out var nativeState) || nativeState.InputIatHooksReady == 0)
            {
                Log(
                    "Overlay Broker recovery is waiting for a surviving peer that owns a complete " +
                    "native keyboard/mouse writer.");
                return;
            }
            if (Interlocked.CompareExchange(ref _brokerRecoveryInProgress, 1, 0) != 0)
                return;

            IOverlayBrokerHostControl? claimedHost = null;
            try
            {
                Log($"Overlay Broker recovery requested: {reason}.");
                var election = OverlayBrokerElectionService.Elect(loader, this, ModId, Log);
                _overlayHub = election.Hub;
                if (!election.IsHost)
                {
                    Interlocked.Exchange(ref _brokerRecoveryInProgress, 0);
                    return;
                }

                _overlayHubControllerRegistered = true;
                claimedHost = election.HostControl;
                string modDirectory = loader.GetDirectoryForModId(ModId);
                var recoveredHost = new OverlayBrokerHost(
                    claimedHost!,
                    Log,
                    carrierUpkeep: NativeCore.Tick,
                    setNativeInputCapture: devices =>
                    {
                        if (!NativeCore.SetInputCaptureDevices((uint)devices))
                        {
                            throw new InvalidOperationException(
                                "Native input writer rejected Broker capture state.");
                        }
                    },
                    getNativeInputCapture: () =>
                        (OverlayInputDevices)NativeCore.GetInputCaptureDevices(),
                    forceNativeInputRelease: NativeCore.ForceReleaseInput);
                claimedHost = null;
                _ownsOverlayBroker = true;
                Interlocked.Exchange(ref _overlayBrokerHost, recoveredHost)?.Dispose();
                _ = InitializeRecoveredBrokerAsync(recoveredHost, hooks, modDirectory);
            }
            catch (Exception exception)
            {
                _ownsOverlayBroker = false;
                claimedHost?.MarkHostUnavailable(
                    $"recovery bootstrap failed: {exception.GetType().Name}");
                Interlocked.Exchange(ref _brokerRecoveryInProgress, 0);
                Log($"Overlay Broker recovery failed closed: {exception}");
            }
        }
    }

    private async Task InitializeRecoveredBrokerAsync(
        OverlayBrokerHost host,
        IReloadedHooks hooks,
        string modDirectory)
    {
        try
        {
            await host.InitializeAsync(
                    hooks,
                    (tick, shouldRender, permanentFailure) =>
                        new CjkConfiguredDx11Hook(
                            modDirectory,
                            tick,
                            shouldRender,
                            Log,
                            permanentFailure))
                .ConfigureAwait(false);

            lock (_brokerRecoverySync)
            {
                if (Volatile.Read(ref _disposing) != 0 ||
                    !ReferenceEquals(Volatile.Read(ref _overlayBrokerHost), host))
                {
                    host.Dispose();
                    return;
                }

                lock (_imguiOperationLock)
                {
                    lock (_lifecycleLock)
                    {
                        if (_disposed)
                        {
                            host.Dispose();
                            return;
                        }
                        _ownsOverlayBroker = true;
                        _frontendFailClosed = false;
                        Volatile.Write(ref _renderStopping, 0);
                    }
                    _overlayRegistration?.SetEnabled(true);
                }
            }
            Log("Overlay Broker recovery completed with one coordinated graphics/input writer.");
        }
        catch (Exception exception)
        {
            Interlocked.CompareExchange(ref _overlayBrokerHost, null, host);
            _ownsOverlayBroker = false;
            Log($"Overlay Broker recovery initialization failed closed: {exception}");
        }
        finally
        {
            Interlocked.Exchange(ref _brokerRecoveryInProgress, 0);
        }
    }

    private sealed class OverlayHubClient : IGbfrOverlayGraphicsClient
    {
        private readonly Mod _owner;

        internal OverlayHubClient(Mod owner)
        {
            _owner = owner;
        }

        public string ModId => Mod.ModId;

        public bool WantsRender =>
            Volatile.Read(ref _owner._renderStopping) == 0 &&
            FrontendOverlayGate.ShouldRenderFrame;

        public void Tick()
        {
            if (!_owner._ownsOverlayBroker &&
                Volatile.Read(ref _owner._renderStopping) == 0)
                NativeCore.Tick();
        }

        public bool BindGraphics(OverlayGraphicsBinding binding) =>
            _owner.BindOverlayGraphics(binding);

        public void Render()
        {
            if (!HostedImguiBinding.EnsureCurrentContext())
            {
                _owner.HandleOverlayPeerFailure(
                    "the Broker did not provide a usable shared ImGui context");
                return;
            }
            _owner.Render();
        }

        public OverlayWindowMessageResult ObserveWindowMessage(
            nint windowHandle,
            uint message,
            nint wParam,
            nint lParam) =>
            _owner.ObserveHostedWindowMessage(
                windowHandle,
                message,
                wParam,
                lParam);

        public void OnHostUnavailable(string reason) =>
            _owner.HandleBrokerUnavailable(reason);
    }
}
