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
        var host = broker.TryAcquireHost(hostModId) ??
            throw new InvalidOperationException("The initial Overlay Broker host lease could not be acquired.");
        return new OverlayBrokerEndpoints(broker, host);
    }

    internal sealed class HostControl : IOverlayBrokerHostControl
    {
        private readonly OverlayBroker _broker;
        private readonly long _generation;
        private int _released;

        internal HostControl(OverlayBroker broker, long generation)
        {
            _broker = broker;
            _generation = generation;
        }

        public void SetInputCaptureChangedCallback(Action<OverlayInputDevices> callback) =>
            _broker.SetInputCaptureChangedCallback(_generation, callback);

        public void PublishGraphicsBinding(OverlayGraphicsBinding binding) =>
            _broker.PublishGraphicsBinding(_generation, binding);

        public void MarkGraphicsReady() => _broker.MarkGraphicsReady(_generation);

        public void MarkGraphicsSuspended() => _broker.MarkGraphicsSuspended(_generation);

        public void MarkHostUnavailable(string reason)
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
                _broker.ReleaseHost(_generation, reason);
        }

        public void TickClients() => _broker.TickClients(_generation);

        public bool HasRenderableClients() => _broker.HasRenderableClients(_generation);

        public void RenderClients() => _broker.RenderClients(_generation);

        public OverlayWindowMessageResult ObserveWindowMessage(
            nint windowHandle,
            uint message,
            nint wParam,
            nint lParam) =>
            _broker.ObserveWindowMessage(_generation, windowHandle, message, wParam, lParam);

        public void Dispose() => MarkHostUnavailable("host lease disposed");
    }
}

/// <summary>
/// Capability retained only by the first peer. It is deliberately separate from
/// <see cref="IGbfrOverlayHub"/> so ordinary peers cannot become graphics writers.
/// </summary>
public interface IOverlayBrokerHostControl : IDisposable
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
