namespace GBFR.OverlayHub.Contracts;

internal sealed class OverlayBroker : IRecoverableGbfrOverlayHub
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, Registration> _registrations = [];
    private readonly Action<string> _log;
    private Registration[] _enabledRegistrations = [];
    private Action<OverlayInputDevices>? _inputCaptureChanged;
    private string _hostModId;
    private OverlayGraphicsBinding _graphicsBinding;
    private long _nextHostGeneration;
    private long _activeHostGeneration;
    private int _graphicsReady;

    internal OverlayBroker(string hostModId, Action<string> log)
    {
        _hostModId = hostModId;
        _log = log;
    }

    public int ApiVersion => OverlayHubProtocol.ApiVersion;

    public string HostModId
    {
        get
        {
            lock (_sync)
                return _hostModId;
        }
    }

    public bool IsHostAvailable
    {
        get
        {
            lock (_sync)
                return _activeHostGeneration != 0;
        }
    }

    public bool IsGraphicsReady => Volatile.Read(ref _graphicsReady) != 0;

    public OverlayInputDevices CapturedInputDevices
    {
        get
        {
            lock (_sync)
                return CapturedInputDevicesLocked();
        }
    }

    public IOverlayBrokerHostControl? TryAcquireHost(string candidateModId)
    {
        if (string.IsNullOrWhiteSpace(candidateModId))
            throw new ArgumentException("A Broker host candidate id is required.", nameof(candidateModId));

        long generation;
        lock (_sync)
        {
            if (_activeHostGeneration != 0)
                return null;
            generation = ++_nextHostGeneration;
            _activeHostGeneration = generation;
            _hostModId = candidateModId;
            _graphicsBinding = default;
            Volatile.Write(ref _graphicsReady, 0);
            _inputCaptureChanged = null;
            foreach (var registration in _registrations.Values)
                registration.ClearHostBindingLocked();
            RebuildEnabledRegistrationsLocked();
        }
        TryLog($"Overlay Broker granted graphics-writer generation {generation} to '{candidateModId}'.");
        return new OverlayBrokerFactory.HostControl(this, generation);
    }

    public IGbfrOverlayRegistration Register(IGbfrOverlayClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (client is not IGbfrOverlayGraphicsClient)
        {
            throw new NotSupportedException(
                $"Overlay peer '{client.ModId}' does not implement the shared graphics binding contract.");
        }

        Registration registration;
        OverlayGraphicsBinding graphicsBinding;
        long hostGeneration;
        lock (_sync)
        {
            if (_registrations.Values.Any(existing =>
                    string.Equals(existing.ModId, client.ModId, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"Overlay peer '{client.ModId}' is already registered.");
            }

            registration = new Registration(this, client);
            _registrations.Add(registration.Token, registration);
            graphicsBinding = _graphicsBinding;
            hostGeneration = _activeHostGeneration;
            RebuildEnabledRegistrationsLocked();
        }

        if (graphicsBinding.IsValid &&
            hostGeneration != 0 &&
            !registration.BindGraphics(graphicsBinding, hostGeneration))
        {
            if (IsCurrentHost(hostGeneration))
            {
                Remove(registration);
                throw new InvalidOperationException(
                    $"Overlay peer '{client.ModId}' rejected the Broker graphics binding.");
            }
        }

        lock (_sync)
            RebuildEnabledRegistrationsLocked();

        TryLog($"Overlay Broker registered peer '{client.ModId}' ({registration.Token}).");
        return registration;
    }

    internal void SetInputCaptureChangedCallback(
        long generation,
        Action<OverlayInputDevices> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        OverlayInputDevices current;
        lock (_sync)
        {
            ThrowIfNotCurrentHostLocked(generation);
            _inputCaptureChanged = callback;
            current = CapturedInputDevicesLocked();
        }
        InvokeInputCaptureChanged(callback, current);
    }

    internal void PublishGraphicsBinding(long generation, OverlayGraphicsBinding binding)
    {
        if (!binding.IsValid)
            throw new ArgumentException("The shared ImGui graphics binding is invalid.", nameof(binding));

        Registration[] registrations;
        Action<OverlayInputDevices>? callback;
        OverlayInputDevices previous;
        lock (_sync)
        {
            ThrowIfNotCurrentHostLocked(generation);
            previous = CapturedInputDevicesLocked();
            Volatile.Write(ref _graphicsReady, 0);
            _graphicsBinding = binding;
            registrations = _registrations.Values.ToArray();
            callback = _inputCaptureChanged;
        }
        NotifyInputTransition(callback, previous, OverlayInputDevices.None);

        foreach (var registration in registrations)
        {
            ThrowIfNotCurrentHost(generation);
            if (!registration.TryGetClient(out var client))
            {
                Remove(registration);
                continue;
            }
            if (!registration.BindGraphics(binding, generation))
            {
                ThrowIfNotCurrentHost(generation);
                FaultPeer(registration, client, "graphics binding", null);
            }
        }
        lock (_sync)
        {
            ThrowIfNotCurrentHostLocked(generation);
            RebuildEnabledRegistrationsLocked();
        }
        TryLog("Overlay Broker published one shared cimgui module and ImGui context.");
    }

    internal void MarkGraphicsReady(long generation)
    {
        Action<OverlayInputDevices>? callback;
        OverlayInputDevices previous;
        OverlayInputDevices current;
        lock (_sync)
        {
            ThrowIfNotCurrentHostLocked(generation);
            if (!_graphicsBinding.IsValid)
                throw new InvalidOperationException("The Broker graphics binding was not published.");
            previous = CapturedInputDevicesLocked();
            Volatile.Write(ref _graphicsReady, 1);
            current = CapturedInputDevicesLocked();
            callback = _inputCaptureChanged;
        }
        NotifyInputTransition(callback, previous, current);
        TryLog($"Overlay Broker graphics writer is ready (bootstrap peer '{HostModId}').");
    }

    internal void MarkGraphicsSuspended(long generation)
    {
        Action<OverlayInputDevices>? callback;
        OverlayInputDevices previous;
        lock (_sync)
        {
            ThrowIfNotCurrentHostLocked(generation);
            previous = CapturedInputDevicesLocked();
            Volatile.Write(ref _graphicsReady, 0);
            callback = _inputCaptureChanged;
        }
        NotifyInputTransition(callback, previous, OverlayInputDevices.None);
    }

    internal void ReleaseHost(long generation, string reason)
    {
        reason = string.IsNullOrWhiteSpace(reason) ? "unknown graphics writer failure" : reason;
        Registration[] registrations;
        Action<OverlayInputDevices>? callback;
        OverlayInputDevices previous;
        lock (_sync)
        {
            if (_activeHostGeneration != generation)
                return;
            previous = CapturedInputDevicesLocked();
            _activeHostGeneration = 0;
            Volatile.Write(ref _graphicsReady, 0);
            _graphicsBinding = default;
            registrations = _registrations.Values.ToArray();
            foreach (var registration in registrations)
                registration.ClearHostBindingLocked();
            callback = _inputCaptureChanged;
            _inputCaptureChanged = null;
            RebuildEnabledRegistrationsLocked();
        }

        NotifyInputTransition(callback, previous, OverlayInputDevices.None);
        foreach (var registration in registrations)
            registration.NotifyHostUnavailable(reason);
        TryLog($"Overlay Broker graphics writer generation {generation} released: {reason}");
    }

    internal void TickClients(long generation)
    {
        if (!IsCurrentHostReady(generation))
            return;
        foreach (var registration in SnapshotEnabledRegistrations())
        {
            if (!registration.TryGetClient(out var client))
            {
                Remove(registration);
                continue;
            }
            try
            {
                client.Tick();
            }
            catch (Exception exception)
            {
                FaultPeer(registration, client, "tick callback", exception);
            }
        }
    }

    internal bool HasRenderableClients(long generation)
    {
        if (!IsCurrentHostReady(generation))
            return false;
        var any = false;
        foreach (var registration in SnapshotEnabledRegistrations())
        {
            if (!registration.TryGetClient(out var client))
            {
                Remove(registration);
                continue;
            }
            try
            {
                any |= client.WantsRender;
            }
            catch (Exception exception)
            {
                FaultPeer(registration, client, "render-intent callback", exception);
            }
        }
        return any;
    }

    internal void RenderClients(long generation)
    {
        if (!IsCurrentHostReady(generation))
            return;
        foreach (var registration in SnapshotEnabledRegistrations())
        {
            if (!registration.TryGetClient(out var client))
            {
                Remove(registration);
                continue;
            }
            try
            {
                if (client.WantsRender)
                    client.Render();
            }
            catch (Exception exception)
            {
                FaultPeer(registration, client, "render callback", exception);
            }
        }
    }

    internal OverlayWindowMessageResult ObserveWindowMessage(
        long generation,
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam)
    {
        if (!IsCurrentHostReady(generation))
            return OverlayWindowMessageResult.Continue;

        var result = OverlayWindowMessageResult.Continue;
        foreach (var registration in SnapshotEnabledRegistrations())
        {
            if (!registration.TryGetClient(out var client))
            {
                Remove(registration);
                continue;
            }
            try
            {
                var candidate = client.ObserveWindowMessage(windowHandle, message, wParam, lParam);
                if (!result.Handled && candidate.Handled)
                    result = candidate;
            }
            catch (Exception exception)
            {
                FaultPeer(registration, client, "WndProc callback", exception);
            }
        }
        return result;
    }

    private Registration[] SnapshotEnabledRegistrations()
        => Volatile.Read(ref _enabledRegistrations);

    private bool SetEnabled(Registration registration, bool enabled)
    {
        Action<OverlayInputDevices>? callback;
        OverlayInputDevices previous;
        OverlayInputDevices current;
        lock (_sync)
        {
            if (!_registrations.TryGetValue(registration.Token, out var currentRegistration) ||
                !ReferenceEquals(currentRegistration, registration))
            {
                return false;
            }
            previous = CapturedInputDevicesLocked();
            registration.SetEnabledLocked(enabled);
            RebuildEnabledRegistrationsLocked();
            current = CapturedInputDevicesLocked();
            callback = _inputCaptureChanged;
        }
        NotifyInputTransition(callback, previous, current);
        return true;
    }

    private bool SetInputCapture(Registration registration, OverlayInputDevices devices)
    {
        Action<OverlayInputDevices>? callback;
        OverlayInputDevices previous;
        OverlayInputDevices current;
        lock (_sync)
        {
            if (!_registrations.TryGetValue(registration.Token, out var currentRegistration) ||
                !ReferenceEquals(currentRegistration, registration) ||
                !registration.IsEnabled)
            {
                return false;
            }
            previous = CapturedInputDevicesLocked();
            registration.SetInputDevicesLocked(devices);
            current = CapturedInputDevicesLocked();
            callback = _inputCaptureChanged;
        }
        NotifyInputTransition(callback, previous, current);
        return true;
    }

    private void Remove(Registration registration)
    {
        Action<OverlayInputDevices>? callback;
        OverlayInputDevices previous;
        OverlayInputDevices current;
        lock (_sync)
        {
            previous = CapturedInputDevicesLocked();
            if (!_registrations.Remove(registration.Token))
                return;
            registration.DisableLocked();
            RebuildEnabledRegistrationsLocked();
            current = CapturedInputDevicesLocked();
            callback = _inputCaptureChanged;
        }
        NotifyInputTransition(callback, previous, current);
        TryLog($"Overlay Broker removed peer '{registration.ModId}' ({registration.Token}).");
    }

    private void FaultPeer(
        Registration registration,
        IGbfrOverlayClient client,
        string stage,
        Exception? exception)
    {
        SetEnabled(registration, false);
        var detail = exception is null
            ? "the peer rejected the shared ImGui context"
            : $"{exception.GetType().Name}: {exception.Message}";
        TryLog($"Overlay Broker isolated peer '{client.ModId}' during {stage}: {detail}.");
        registration.NotifyHostUnavailable($"peer-local failure during {stage}");
    }

    private bool IsCurrentHostReady(long generation)
    {
        lock (_sync)
            return _activeHostGeneration == generation && Volatile.Read(ref _graphicsReady) != 0;
    }

    private bool IsCurrentHost(long generation)
    {
        lock (_sync)
            return _activeHostGeneration == generation;
    }

    private void ThrowIfNotCurrentHost(long generation)
    {
        lock (_sync)
            ThrowIfNotCurrentHostLocked(generation);
    }

    private void ThrowIfNotCurrentHostLocked(long generation)
    {
        if (_activeHostGeneration != generation)
        {
            throw new InvalidOperationException(
                $"Overlay Broker rejected stale graphics-writer generation {generation}.");
        }
    }

    private void RebuildEnabledRegistrationsLocked()
    {
        if (_registrations.Count == 0)
        {
            Volatile.Write(ref _enabledRegistrations, Array.Empty<Registration>());
            return;
        }

        var enabled = new List<Registration>(_registrations.Count);
        foreach (var registration in _registrations.Values)
        {
            if (registration.IsEnabled &&
                registration.IsGraphicsBoundFor(_activeHostGeneration))
                enabled.Add(registration);
        }
        Volatile.Write(ref _enabledRegistrations, enabled.ToArray());
    }

    private OverlayInputDevices CapturedInputDevicesLocked()
    {
        if (Volatile.Read(ref _graphicsReady) == 0)
            return OverlayInputDevices.None;
        var devices = OverlayInputDevices.None;
        foreach (var registration in _registrations.Values)
        {
            if (registration.IsEnabled)
                devices |= registration.InputDevices;
        }
        return devices;
    }

    private void NotifyInputTransition(
        Action<OverlayInputDevices>? callback,
        OverlayInputDevices previous,
        OverlayInputDevices current)
    {
        if (callback is not null && previous != current)
            InvokeInputCaptureChanged(callback, current);
    }

    private void InvokeInputCaptureChanged(
        Action<OverlayInputDevices> callback,
        OverlayInputDevices devices)
    {
        try
        {
            callback(devices);
        }
        catch (Exception exception)
        {
            TryLog(
                "Overlay Broker contained an input-capture transition failure: " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private void TryLog(string message)
    {
        try
        {
            _log(message);
        }
        catch
        {
            // No logger or peer may unwind into Present/WndProc.
        }
    }

    private sealed class Registration : IGbfrOverlayRegistration
    {
        private readonly OverlayBroker _owner;
        private readonly WeakReference<IGbfrOverlayClient> _client;
        private readonly object _graphicsSync = new();
        private OverlayGraphicsBinding _boundGraphics;
        private long _boundHostGeneration;
        private int _disposed;

        internal Registration(OverlayBroker owner, IGbfrOverlayClient client)
        {
            _owner = owner;
            _client = new WeakReference<IGbfrOverlayClient>(client);
            ModId = client.ModId;
            Token = Guid.NewGuid();
        }

        public Guid Token { get; }

        internal string ModId { get; }

        internal bool IsEnabled { get; private set; }

        internal bool IsGraphicsBoundFor(long generation) =>
            generation != 0 && Volatile.Read(ref _boundHostGeneration) == generation;

        internal OverlayInputDevices InputDevices { get; private set; }

        public bool SetEnabled(bool enabled) =>
            Volatile.Read(ref _disposed) == 0 && _owner.SetEnabled(this, enabled);

        public bool SetInputCapture(OverlayInputDevices devices) =>
            Volatile.Read(ref _disposed) == 0 && _owner.SetInputCapture(this, devices);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _owner.Remove(this);
        }

        internal bool TryGetClient(out IGbfrOverlayClient client) =>
            _client.TryGetTarget(out client!);

        internal bool BindGraphics(OverlayGraphicsBinding binding, long generation)
        {
            lock (_graphicsSync)
            {
                if (Volatile.Read(ref _boundHostGeneration) == generation &&
                    _boundGraphics.IsValid &&
                    _boundGraphics.NativeLibraryHandle == binding.NativeLibraryHandle &&
                    _boundGraphics.ContextPointer == binding.ContextPointer)
                {
                    return true;
                }
                if (!TryGetClient(out var client) ||
                    client is not IGbfrOverlayGraphicsClient graphicsClient)
                {
                    return false;
                }
                if (!_owner.IsCurrentHost(generation))
                    return false;
                try
                {
                    if (!graphicsClient.BindGraphics(binding))
                        return false;
                    if (!_owner.IsCurrentHost(generation))
                        return false;
                    _boundGraphics = binding;
                    Volatile.Write(ref _boundHostGeneration, generation);
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        internal void SetEnabledLocked(bool enabled)
        {
            IsEnabled = enabled;
            if (!enabled)
                InputDevices = OverlayInputDevices.None;
        }

        internal void SetInputDevicesLocked(OverlayInputDevices devices) =>
            InputDevices = devices &
                (OverlayInputDevices.Keyboard | OverlayInputDevices.Mouse | OverlayInputDevices.Text);

        internal void ClearHostBindingLocked()
        {
            Volatile.Write(ref _boundHostGeneration, 0);
            InputDevices = OverlayInputDevices.None;
        }

        internal void DisableLocked()
        {
            IsEnabled = false;
            InputDevices = OverlayInputDevices.None;
        }

        internal void NotifyHostUnavailable(string reason)
        {
            if (!TryGetClient(out var client))
                return;
            try
            {
                client.OnHostUnavailable(reason);
            }
            catch
            {
                // The peer is already isolated.
            }
        }
    }
}
