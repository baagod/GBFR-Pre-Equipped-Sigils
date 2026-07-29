using GBFR.OverlayHub.Contracts;

var log = new List<string>();
OverlayBrokerEndpoints endpoints = OverlayBrokerFactory.Create("host-a", log.Add);
var recoverable = endpoints.Hub as IRecoverableGbfrOverlayHub ??
    throw new InvalidOperationException("The Broker must expose its optional recovery capability.");
Assert(recoverable.IsHostAvailable, "The initial host lease was not active.");

var client = new ProbeClient("peer-a");
using IGbfrOverlayRegistration registration = endpoints.Hub.Register(client);
Assert(registration.SetEnabled(true), "The peer could not be enabled.");

var firstBinding = new OverlayGraphicsBinding(
    OverlayHubProtocol.GraphicsBindingVersion,
    (nint)0x1000,
    (nint)0x2000);
endpoints.Host.PublishGraphicsBinding(firstBinding);
endpoints.Host.MarkGraphicsReady();
Assert(client.BindCount == 1, "The initial graphics binding was not delivered.");
Assert(registration.SetInputCapture(OverlayInputDevices.Keyboard | OverlayInputDevices.Mouse),
    "The peer could not request input capture.");
Assert(endpoints.Hub.CapturedInputDevices ==
       (OverlayInputDevices.Keyboard | OverlayInputDevices.Mouse),
    "The aggregate input policy did not include the enabled peer.");

endpoints.Host.TickClients();
endpoints.Host.RenderClients();
Assert(client.TickCount == 1 && client.RenderCount == 1,
    "The initial host did not dispatch peer callbacks.");

endpoints.Host.MarkHostUnavailable("simulated host loss");
Assert(!recoverable.IsHostAvailable, "The released host lease remained active.");
Assert(!endpoints.Hub.IsGraphicsReady, "Graphics remained ready after host loss.");
Assert(endpoints.Hub.CapturedInputDevices == OverlayInputDevices.None,
    "Input capture was not cleared after host loss.");
Assert(client.UnavailableReasons.Count == 1,
    "The surviving peer was not notified about host loss.");
Assert(registration.SetEnabled(true),
    "A surviving registration could not remain enabled for recovery.");

using IOverlayBrokerHostControl recoveredHost =
    recoverable.TryAcquireHost("host-b") ??
    throw new InvalidOperationException("The replacement host lease was not granted.");
Assert(recoverable.TryAcquireHost("host-c") is null,
    "A second concurrent host lease was granted.");
Assert(endpoints.Hub.HostModId == "host-b", "The host identity was not transferred.");

var secondBinding = new OverlayGraphicsBinding(
    OverlayHubProtocol.GraphicsBindingVersion,
    (nint)0x3000,
    (nint)0x4000);
recoveredHost.PublishGraphicsBinding(secondBinding);
recoveredHost.MarkGraphicsReady();
Assert(client.BindCount == 2, "The surviving peer was not rebound to the replacement host.");

endpoints.Host.TickClients();
Assert(client.TickCount == 1, "A stale host generation dispatched a peer callback.");
endpoints.Host.RenderClients();
Assert(client.RenderCount == 1, "A stale host generation rendered a peer.");
Assert(!endpoints.Host.HasRenderableClients(),
    "A stale host generation reported renderable peers.");
Assert(!endpoints.Host.ObserveWindowMessage(nint.Zero, 0x0100, nint.Zero, nint.Zero).Handled,
    "A stale host generation handled a window message.");
AssertThrows<InvalidOperationException>(() => endpoints.Host.PublishGraphicsBinding(firstBinding),
    "A stale host generation published a graphics binding.");
AssertThrows<InvalidOperationException>(endpoints.Host.MarkGraphicsReady,
    "A stale host generation marked graphics ready.");
AssertThrows<InvalidOperationException>(
    () => endpoints.Host.SetInputCaptureChangedCallback(_ => { }),
    "A stale host generation installed an input callback.");

recoveredHost.TickClients();
recoveredHost.RenderClients();
Assert(client.TickCount == 2 && client.RenderCount == 2,
    "The replacement host did not resume peer callbacks.");

recoveredHost.MarkHostUnavailable("test complete");
Assert(!recoverable.IsHostAvailable, "The replacement host lease was not released.");
VerifyConcurrentGenerationFence();
Console.WriteLine("OVERLAY_BROKER_RECOVERY=PASS");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static void AssertThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static void VerifyConcurrentGenerationFence()
{
    OverlayBrokerEndpoints endpoints = OverlayBrokerFactory.Create("race-host-a", _ => { });
    var recoverable = (IRecoverableGbfrOverlayHub)endpoints.Hub;
    using var client = new BlockingProbeClient("race-peer");
    using IGbfrOverlayRegistration registration = endpoints.Hub.Register(client);
    Assert(registration.SetEnabled(true), "The race peer could not be enabled.");

    var firstBinding = new OverlayGraphicsBinding(
        OverlayHubProtocol.GraphicsBindingVersion,
        (nint)0x5000,
        (nint)0x6000);
    var secondBinding = new OverlayGraphicsBinding(
        OverlayHubProtocol.GraphicsBindingVersion,
        (nint)0x7000,
        (nint)0x8000);

    Task<bool> stalePublish = Task.Run(() =>
    {
        try
        {
            endpoints.Host.PublishGraphicsBinding(firstBinding);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    });
    Assert(client.FirstBindStarted.Wait(TimeSpan.FromSeconds(5)),
        "The stale graphics binding did not enter the peer callback.");

    endpoints.Host.MarkHostUnavailable("replace during bind");
    using IOverlayBrokerHostControl replacement =
        recoverable.TryAcquireHost("race-host-b") ??
        throw new InvalidOperationException("The race replacement lease was not granted.");
    Task replacementPublish = Task.Run(() => replacement.PublishGraphicsBinding(secondBinding));

    client.ReleaseFirstBind.Set();
    Assert(stalePublish.GetAwaiter().GetResult(),
        "The in-flight stale generation completed as the current host.");
    replacementPublish.GetAwaiter().GetResult();
    replacement.MarkGraphicsReady();
    Assert(client.BindCount == 2 && client.LastContext == secondBinding.ContextPointer,
        "The stale in-flight binding replaced the current generation binding.");
}

sealed class ProbeClient(string modId) : IGbfrOverlayGraphicsClient
{
    internal int BindCount { get; private set; }
    internal int TickCount { get; private set; }
    internal int RenderCount { get; private set; }
    internal List<string> UnavailableReasons { get; } = [];

    public string ModId { get; } = modId;
    public bool WantsRender => true;

    public bool BindGraphics(OverlayGraphicsBinding binding)
    {
        if (!binding.IsValid)
            return false;
        BindCount++;
        return true;
    }

    public void Tick() => TickCount++;
    public void Render() => RenderCount++;

    public OverlayWindowMessageResult ObserveWindowMessage(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam) => OverlayWindowMessageResult.Continue;

    public void OnHostUnavailable(string reason) => UnavailableReasons.Add(reason);
}

sealed class BlockingProbeClient(string modId) : IGbfrOverlayGraphicsClient, IDisposable
{
    private long _lastContext;
    private int _bindCount;

    internal ManualResetEventSlim FirstBindStarted { get; } = new(false);
    internal ManualResetEventSlim ReleaseFirstBind { get; } = new(false);
    internal int BindCount => Volatile.Read(ref _bindCount);
    internal nint LastContext => (nint)Interlocked.Read(ref _lastContext);

    public string ModId { get; } = modId;
    public bool WantsRender => false;

    public bool BindGraphics(OverlayGraphicsBinding binding)
    {
        int call = Interlocked.Increment(ref _bindCount);
        if (call == 1)
        {
            FirstBindStarted.Set();
            if (!ReleaseFirstBind.Wait(TimeSpan.FromSeconds(5)))
                return false;
        }
        Interlocked.Exchange(ref _lastContext, binding.ContextPointer.ToInt64());
        return true;
    }

    public void Tick() { }
    public void Render() { }

    public OverlayWindowMessageResult ObserveWindowMessage(
        nint windowHandle,
        uint message,
        nint wParam,
        nint lParam) => OverlayWindowMessageResult.Continue;

    public void OnHostUnavailable(string reason) { }

    public void Dispose()
    {
        FirstBindStarted.Dispose();
        ReleaseFirstBind.Dispose();
    }
}
