namespace GBFR.OverlayHub.Contracts;

/// <summary>
/// The two capabilities created for the process-local overlay broker. Only the
/// bootstrap carrier receives <see cref="Host"/>; every peer receives <see cref="Hub"/>.
/// </summary>
public sealed class OverlayBrokerEndpoints
{
    internal OverlayBrokerEndpoints(IGbfrOverlayHub hub, IOverlayBrokerHostControl host)
    {
        Hub = hub;
        Host = host;
    }

    public IGbfrOverlayHub Hub { get; }

    public IOverlayBrokerHostControl Host { get; }
}

/// <summary>
/// Creates the neutral registry and its unadvertised single-writer capability.
/// The host capability must never be registered as a Reloaded-II controller.
/// </summary>
public static class OverlayBrokerFactory
{
    public static OverlayBrokerEndpoints Create(string hostModId, Action<string> log)
    {
        if (string.IsNullOrWhiteSpace(hostModId))
            throw new ArgumentException("A broker host mod id is required.", nameof(hostModId));
        ArgumentNullException.ThrowIfNull(log);

        var broker = new OverlayBroker(hostModId, log);
        return new OverlayBrokerEndpoints(broker, new HostControl(broker));
    }

    private sealed class HostControl : IOverlayBrokerHostControl
    {
        private readonly OverlayBroker _broker;

        internal HostControl(OverlayBroker broker) => _broker = broker;

        public void SetInputCaptureChangedCallback(Action<OverlayInputDevices> callback) =>
            _broker.SetInputCaptureChangedCallback(callback);

        public void PublishGraphicsBinding(OverlayGraphicsBinding binding) =>
            _broker.PublishGraphicsBinding(binding);

        public void MarkGraphicsReady() => _broker.MarkGraphicsReady();

        public void MarkGraphicsSuspended() => _broker.MarkGraphicsSuspended();

        public void MarkHostUnavailable(string reason) => _broker.MarkHostUnavailable(reason);

        public void TickClients() => _broker.TickClients();

        public bool HasRenderableClients() => _broker.HasRenderableClients();

        public void RenderClients() => _broker.RenderClients();

        public OverlayWindowMessageResult ObserveWindowMessage(
            nint windowHandle,
            uint message,
            nint wParam,
            nint lParam) =>
            _broker.ObserveWindowMessage(windowHandle, message, wParam, lParam);
    }
}

/// <summary>
/// Capability retained only by the first peer. It is deliberately separate from
/// <see cref="IGbfrOverlayHub"/> so ordinary peers cannot become graphics writers.
/// </summary>
public interface IOverlayBrokerHostControl
{
    void SetInputCaptureChangedCallback(Action<OverlayInputDevices> callback);

    void PublishGraphicsBinding(OverlayGraphicsBinding binding);

    void MarkGraphicsReady();

    void MarkGraphicsSuspended();

    void MarkHostUnavailable(string reason);

    void TickClients();

    bool HasRenderableClients();

    void RenderClients();

    OverlayWindowMessageResult ObserveWindowMessage(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam);
}
