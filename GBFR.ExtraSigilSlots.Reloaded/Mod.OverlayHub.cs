using GBFR.OverlayHub.Contracts;

namespace GBFR.ExtraSigilSlots.Reloaded;

public sealed partial class Mod
{
    private OverlayWindowMessageResult ObserveHostedWindowMessage(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam)
    {
        MouseButtonStateTracker.ObserveWindowMessage(message, wParam);
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

    private void HandleBrokerUnavailable(string reason)
    {
        Volatile.Write(ref _renderStopping, 1);
        ForceReleaseInputCapture();
        _ui?.Close();
        Log(
            $"Overlay Broker became unavailable; the Extra Sigil frontend failed closed " +
            $"without installing a second graphics/input hook. Reason: {reason}.");
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
            HostedImguiBinding.TryBind(binding, _owner.Log);

        public void Render()
        {
            if (!HostedImguiBinding.EnsureCurrentContext())
            {
                _owner.HandleBrokerUnavailable(
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
