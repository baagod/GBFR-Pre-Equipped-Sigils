using System.Diagnostics;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;
using GBFR.OverlayHub.Contracts;
using GBFR.OverlayHub.Runtime;

namespace GBFR.ExtraSigilSlots.Reloaded;

public sealed partial class Mod : IMod, IExports
{
    private const string ModId = "GBFR.ExtraSigilSlots.Reloaded";

    private readonly object _lifecycleLock = new();
    private readonly object _imguiOperationLock = new();
    private readonly object _logLock = new();
    private readonly object _brokerRecoverySync = new();
    private ILogger? _logger;
    private StreamWriter? _fileLog;
    private SigilOverlayUi? _ui;
    private bool _starting;
    private bool _started;
    private bool _nativeCoreActive;
    private bool _disposed;
    private CancellationTokenSource? _executableHashCancellation;
    private Task? _executableHashTask;
    private IModLoader? _modLoader;
    private IReloadedHooks? _reloadedHooks;
    private IGbfrOverlayHub? _overlayHub;
    private IGbfrOverlayRegistration? _overlayRegistration;
    private OverlayHubClient? _overlayHubClient;
    private volatile bool _ownsOverlayBroker;
    private bool _overlayHubControllerRegistered;
    private OverlayBrokerHost? _overlayBrokerHost;
    private volatile bool _frontendFailClosed;
    private int _hostedInputCapture;
    private int _renderStopping;
    private int _activeRenderCallbacks;
    private int _brokerRecoveryInProgress;
    private int _disposing;
    private int _awaitingBrokerRebind;
    private static int s_pendingAnsiLeadByte;
    private static int s_pendingAnsiCodePage;
    private static int s_imeCompositionActive;
    private static int s_imeResultInjected;

    public Action Disposing => Dispose;

    public Type[] GetTypes() =>
    [
        typeof(IGbfrOverlayHub),
    ];

    public void Start(IModLoaderV1 loader) => QueueStart(loader, ModId);

    public void StartEx(IModLoaderV1 loader, IModConfigV1 _) =>
        QueueStart(loader, ModId);

    public void Suspend()
    {
        lock (_imguiOperationLock)
        {
            lock (_lifecycleLock)
            {
                if (!_started || _disposed)
                    return;
            }
            ForceReleaseInputCapture();
            Volatile.Write(ref _renderStopping, 1);
            _ui?.Close();
            _overlayRegistration?.SetEnabled(false);
        }
    }

    public void Resume()
    {
        lock (_imguiOperationLock)
        {
            lock (_lifecycleLock)
            {
                if (!_started || _disposed)
                    return;
            }
            Volatile.Write(ref _renderStopping, 0);
            _overlayRegistration?.SetEnabled(true);
        }
    }

    public void Unload() => Dispose();

    public bool CanUnload() => false;

    public bool CanSuspend() => true;

    private void QueueStart(IModLoaderV1 loaderApi, string modId)
    {
        lock (_lifecycleLock)
        {
            if (_starting || _started || _disposed)
                return;
            _starting = true;
        }

        IModLoader loader = (IModLoader)loaderApi;
        _modLoader = loader;
        lock (_logLock)
            _logger = (ILogger)loader.GetLogger();
        try
        {
            var election = OverlayBrokerElectionService.Elect(loader, this, modId, Log);
            _overlayHub = election.Hub;
            _ownsOverlayBroker = election.IsHost;
            _overlayHubControllerRegistered = election.IsHost;
            _ = StartCoreAsync(loaderApi, modId, election);
        }
        catch (Exception exception)
        {
            lock (_lifecycleLock)
                _starting = false;
            Log($"Overlay Broker election failed closed: {exception}");
        }
    }

    private async Task StartCoreAsync(
        IModLoaderV1 loaderApi,
        string modId,
        OverlayBrokerElection election)
    {
        IGbfrOverlayHub overlayHub = election.Hub;
        long managedStartupStarted = Stopwatch.GetTimestamp();
        IGbfrOverlayRegistration? pendingRegistration = null;
        OverlayHubClient? pendingClient = null;
        OverlayBrokerHost? pendingBrokerHost = null;
        bool frontendFailClosed = false;
        bool brokerHostFailed = false;
        try
        {
            IModLoader loader = (IModLoader)loaderApi;
            lock (_logLock)
                _logger = (ILogger)loader.GetLogger();

            string modDirectory = loader.GetDirectoryForModId(modId);
            Directory.CreateDirectory(modDirectory);
            lock (_logLock)
            {
                _fileLog?.Dispose();
                _fileLog = new StreamWriter(
                    Path.Combine(modDirectory, "ExtraSigilSlots.Reloaded.log"),
                    append: false
                )
                {
                    AutoFlush = true,
                };
            }
            Log("Startup phase=managed-initialize state=begin.");

            long injectionSourceStarted = BeginStartupPhase("reloaded-injection-source");
            ReloadedInjectionSource injectionSource = ReloadedInjectionSourceDetector.Detect();
            CompleteStartupPhase(
                "reloaded-injection-source",
                injectionSourceStarted,
                injectionSource.Kind != ReloadedInjectionKind.Unknown);
            Log(ReloadedInjectionSourceDetector.FormatLogMessage(injectionSource));

            long migrationStarted = BeginStartupPhase("legacy-data-migration");
            LegacyDataMigrator.Migrate(modDirectory, Log);
            CompleteStartupPhase("legacy-data-migration", migrationStarted);

            long hooksControllerStarted = BeginStartupPhase("reloaded-hooks-controller");
            if (loader.GetController<IReloadedHooks>() is not { } hooksController ||
                !hooksController.TryGetTarget(out IReloadedHooks? hooks) ||
                hooks is null)
            {
                throw new InvalidOperationException(
                    "Reloaded.Hooks controller is unavailable. Enable reloaded.sharedlib.hooks."
                );
            }
            _reloadedHooks = hooks;
            CompleteStartupPhase("reloaded-hooks-controller", hooksControllerStarted);

            try
            {
                pendingClient = new OverlayHubClient(this);
                pendingRegistration = overlayHub.Register(pendingClient);
                if (!pendingRegistration.SetEnabled(false))
                    throw new InvalidOperationException("Overlay Broker rejected peer initialization.");
                Log(
                    $"Extra Sigil registered as a normal Overlay Broker peer; " +
                    $"bootstrap='{overlayHub.HostModId}', local_bootstrap={election.IsHost}.");
            }
            catch (Exception exception)
            {
                pendingRegistration?.Dispose();
                pendingRegistration = null;
                pendingClient = null;
                frontendFailClosed = true;
                Log(
                    "Overlay Broker could not accept the Extra Sigil peer; its frontend failed " +
                    $"closed while gameplay hooks continue: {exception.GetType().Name}: {exception.Message}");
            }

            long nativeStarted = BeginStartupPhase("native-core");
            NativeCore.Configure(modDirectory);
            bool hooksReady = NativeCore.Initialize(
                Log,
                enableInputHooks: election.IsHost);
            CompleteStartupPhase("native-core", nativeStarted, hooksReady);
            bool shutdownImmediately;
            lock (_lifecycleLock)
            {
                shutdownImmediately = _disposed;
                if (!shutdownImmediately)
                    _nativeCoreActive = true;
                else
                    _starting = false;
            }
            if (shutdownImmediately)
            {
                pendingRegistration?.Dispose();
                election.HostControl?.MarkHostUnavailable("bootstrap startup was cancelled");
                NativeCore.Shutdown();
                return;
            }

            string nativeInitializationMessage = NativeCore.GetRuntimeMessage();
            Log(
                hooksReady
                    ? "ReShade-free native core initialized and hooks passed preflight. " +
                        nativeInitializationMessage
                    : $"Native core loaded without hooks: {nativeInitializationMessage}"
            );
            QueueExecutableHashDiagnostic();

            FrontendOverlayGate.ForceClosed();
            int initialToggleKey = (int)OverlayHotkey.F8;
            if (NativeCore.TryGetState(out NativeCore.RuntimeState initialState))
                initialToggleKey = initialState.ToggleKey;
            FrontendOverlayGate.SetToggleKey(initialToggleKey);
            long hotkeyStarted = BeginStartupPhase("hotkey-configuration");
            InitializeHotkeyConfiguration(loader, modId, initialToggleKey);
            CompleteStartupPhase("hotkey-configuration", hotkeyStarted);

            if (election.IsHost)
            {
                try
                {
                    long brokerStarted = BeginStartupPhase("overlay-broker-host");
                    pendingBrokerHost = new OverlayBrokerHost(
                        election.HostControl!,
                        Log,
                        carrierUpkeep: NativeCore.Tick,
                        setNativeInputCapture: devices =>
                        {
                            if (!NativeCore.SetInputCaptureDevices((uint)devices))
                                throw new InvalidOperationException("Native input writer rejected Broker capture state.");
                        },
                        forceNativeInputRelease: NativeCore.ForceReleaseInput);
                    await pendingBrokerHost.InitializeAsync(
                            hooks,
                            (tick, shouldRender, permanentFailure) =>
                                new CjkConfiguredDx11Hook(
                                    modDirectory,
                                    tick,
                                    shouldRender,
                                    Log,
                                    permanentFailure))
                        .ConfigureAwait(false);
                    CompleteStartupPhase("overlay-broker-host", brokerStarted);
                }
                catch (Exception exception)
                {
                    brokerHostFailed = true;
                    frontendFailClosed = true;
                    Log(
                        "Neutral Overlay Broker graphics initialization failed closed; gameplay " +
                        $"hooks remain active: {exception.GetType().Name}: {exception.Message}");
                }
            }

            long overlayUiStarted = BeginStartupPhase("overlay-ui");
            SigilOverlayUi ui = new(
                modDirectory,
                SetInputCapture,
                Log,
                brokerOwnsMouseCapture: true);
            if (!frontendFailClosed && pendingRegistration?.SetEnabled(true) != true)
            {
                pendingRegistration?.Dispose();
                pendingRegistration = null;
                pendingClient = null;
                frontendFailClosed = true;
                Log(
                    "Overlay Broker could not activate the Extra Sigil peer; it was disabled fail-closed.");
            }
            bool initialized = false;
            lock (_imguiOperationLock)
            {
                lock (_lifecycleLock)
                {
                    _starting = false;
                    if (!_disposed)
                    {
                        _ui = ui;
                        _overlayHub = overlayHub;
                        _overlayRegistration = pendingRegistration;
                        _overlayHubClient = pendingClient;
                        _ownsOverlayBroker =
                            election.IsHost && pendingBrokerHost?.IsInitialized == true;
                        _overlayBrokerHost = pendingBrokerHost;
                        _frontendFailClosed = frontendFailClosed;
                        _started = true;
                        Volatile.Write(ref _renderStopping, 0);
                        initialized = true;
                    }
                }

                if (!initialized)
                {
                    pendingRegistration?.Dispose();
                    pendingBrokerHost?.Dispose();
                    ui.Dispose();
                }
            }

            if (!initialized)
            {
                CompleteStartupPhase("overlay-ui", overlayUiStarted, false);
                CompleteStartupPhase("managed-initialize", managedStartupStarted, false);
                return;
            }

            CompleteStartupPhase("overlay-ui", overlayUiStarted);
            Log(
                frontendFailClosed
                    ? "Extra-sigil gameplay core initialized, but its Broker peer frontend is unavailable."
                    : election.IsHost
                        ? "Extra Sigil bootstrapped the neutral Overlay Broker and registered itself as an ordinary peer."
                        : "Extra Sigil joined the existing Overlay Broker as an ordinary peer; no second Present, WndProc or DirectInput writer was installed.");
            if (!frontendFailClosed)
                Log($"Press {GetConfiguredHotkeyName()} to open the extra-sigil selector.");
            CompleteStartupPhase("managed-initialize", managedStartupStarted);
            if (brokerHostFailed)
                RequestOverlayBrokerRecovery("initial graphics writer failed to initialize");
        }
        catch (Exception exception)
        {
            pendingRegistration?.Dispose();
            pendingBrokerHost?.Dispose();
            election.HostControl?.MarkHostUnavailable(
                $"bootstrap startup failed: {exception.GetType().Name}");
            bool shutdownCore;
            lock (_imguiOperationLock)
            {
                lock (_lifecycleLock)
                {
                    _starting = false;
                    shutdownCore = _nativeCoreActive;
                    _nativeCoreActive = false;
                }
            }
            if (shutdownCore)
            {
                try
                {
                    NativeCore.Shutdown();
                }
                catch
                {
                    // Preserve the original initialization failure in the log.
                }
            }
            DetachHotkeyConfiguration()?.DisposeEvents();
            CompleteStartupPhase("managed-initialize", managedStartupStarted, false);
            Log($"Initialization failed: {exception}");
        }
    }

    private void Render()
    {
        Interlocked.Increment(ref _activeRenderCallbacks);
        try
        {
            if (Volatile.Read(ref _renderStopping) != 0)
                return;
            _ui?.RenderFrame();
        }
        catch (Exception exception)
        {
            SetInputCapture(false);
            _ui?.Close();
            Log($"Render callback recovered from an exception: {exception}");
        }
        finally
        {
            Interlocked.Decrement(ref _activeRenderCallbacks);
        }
    }

    private void Log(string message)
    {
        string line = $"[{ModId}] {message}";
        ILogger? logger;
        lock (_logLock)
        {
            logger = _logger;
            try
            {
                _fileLog?.WriteLine(line);
            }
            catch
            {
                // File logging must never tear down the render callback.
            }
        }
        try
        {
            logger?.WriteLine(line);
        }
        catch
        {
            // External logger failures must not affect the mod lifecycle.
        }
    }

    private long BeginStartupPhase(string phase)
    {
        Log($"Startup phase={phase} state=begin.");
        return Stopwatch.GetTimestamp();
    }

    private void CompleteStartupPhase(string phase, long startedAt, bool succeeded = true)
    {
        long elapsedMilliseconds =
            (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        Log(
            $"Startup phase={phase} state={(succeeded ? "complete" : "failed")} " +
            $"elapsed_ms={elapsedMilliseconds}."
        );
    }

    private void QueueExecutableHashDiagnostic()
    {
        CancellationTokenSource cancellation = new();
        Task diagnosticTask;
        lock (_lifecycleLock)
        {
            if (_disposed || _executableHashTask is not null)
            {
                cancellation.Dispose();
                return;
            }
            _executableHashCancellation = cancellation;
            diagnosticTask = ExecutableHashDiagnostic.Start(
                Environment.ProcessPath,
                Log,
                cancellation.Token);
            _executableHashTask = diagnosticTask;
        }

        _ = diagnosticTask.ContinueWith(
            _ =>
            {
                lock (_lifecycleLock)
                {
                    if (ReferenceEquals(_executableHashTask, diagnosticTask))
                        _executableHashTask = null;
                    if (ReferenceEquals(_executableHashCancellation, cancellation))
                        _executableHashCancellation = null;
                }
                cancellation.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private void Dispose()
    {
        if (Interlocked.Exchange(ref _disposing, 1) != 0)
            return;
        lock (_brokerRecoverySync)
        {
            DisposeCore();
        }
    }

    private void DisposeCore()
    {
        SigilOverlayUi? ui;
        HotkeyConfig? hotkeyConfiguration;
        CancellationTokenSource? executableHashCancellation;
        IGbfrOverlayRegistration? overlayRegistration;
        OverlayBrokerHost? brokerHost;
        IModLoader? modLoader;
        bool removeBrokerController;
        bool shutdownCore;
        lock (_imguiOperationLock)
        {
            lock (_lifecycleLock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                ui = _ui;
                _ui = null;
                shutdownCore = _nativeCoreActive;
                hotkeyConfiguration = _hotkeyConfiguration;
                _hotkeyConfiguration = null;
                executableHashCancellation = _executableHashCancellation;
                overlayRegistration = _overlayRegistration;
                brokerHost = _overlayBrokerHost;
                _overlayBrokerHost = null;
                modLoader = _modLoader;
                removeBrokerController = _overlayHubControllerRegistered;
                _overlayHubControllerRegistered = false;
                _executableHashCancellation = null;
                _executableHashTask = null;
                _nativeCoreActive = false;
                _started = false;
            }

            ForceReleaseInputCapture();
            Volatile.Write(ref _renderStopping, 1);
        }
        overlayRegistration?.SetEnabled(false);
        overlayRegistration?.Dispose();
        hotkeyConfiguration?.DisposeEvents();
        try
        {
            executableHashCancellation?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // A synchronously completed diagnostic may already own disposal.
        }

        bool renderDrained = SpinWait.SpinUntil(
            () => Volatile.Read(ref _activeRenderCallbacks) == 0,
            TimeSpan.FromSeconds(5)
        );
        if (renderDrained)
        {
            lock (_imguiOperationLock)
            {
                ui?.Dispose();
            }
            brokerHost?.Dispose();
            if (shutdownCore)
                NativeCore.Shutdown();
            var recoveredElsewhere = _overlayHub is IRecoverableGbfrOverlayHub recoverable &&
                                     recoverable.IsHostAvailable;
            if (removeBrokerController && modLoader is not null && !recoveredElsewhere)
                modLoader.RemoveController<IGbfrOverlayHub>();
        }
        else
        {
            Log(
                "Unload cleanup was deferred because a render callback did not drain in five seconds; " +
                "the mod is marked non-unloadable and its modules remain resident until process exit."
            );
        }

        lock (_logLock)
        {
            _fileLog?.Dispose();
            _fileLog = null;
        }
    }
}
