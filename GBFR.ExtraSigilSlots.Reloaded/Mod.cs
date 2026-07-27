using System.Diagnostics;
using Reloaded.Hooks.ReloadedII.Interfaces;
using Reloaded.Imgui.Hook;
using Reloaded.Imgui.Hook.Direct3D11;
using Reloaded.Imgui.Hook.Implementations;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace GBFR.ExtraSigilSlots.Reloaded;

public sealed partial class Mod : IMod
{
    private const string ModId = "GBFR.ExtraSigilSlots.Reloaded";

    private readonly object _lifecycleLock = new();
    private readonly object _imguiOperationLock = new();
    private readonly object _logLock = new();
    private ILogger? _logger;
    private StreamWriter? _fileLog;
    private SigilOverlayUi? _ui;
    private bool _starting;
    private bool _started;
    private bool _imguiCreated;
    private bool _nativeCoreActive;
    private bool _disposed;
    private CancellationTokenSource? _executableHashCancellation;
    private Task? _executableHashTask;
    private int _renderThreadLogged;
    private int _inputCaptureLogged;
    private int _renderStopping;
    private int _activeRenderCallbacks;
    private static int s_captureInput;
    private static int s_hasOriginalWndProc;
    private static int s_capturedRawInputMessages;
    private static int s_capturedMouseKeyboardMessages;
    private static int s_pendingAnsiLeadByte;
    private static int s_pendingAnsiCodePage;
    private static int s_imeCompositionActive;
    private static int s_imeResultInjected;
    private static WndProcHook.WndProc s_originalWndProc;

    public Action Disposing => Dispose;

    public void Start(IModLoaderV1 loader) => QueueStart(loader, ModId);

    public void StartEx(IModLoaderV1 loader, IModConfigV1 _) =>
        QueueStart(loader, ModId);

    public void Suspend()
    {
        lock (_imguiOperationLock)
        {
            lock (_lifecycleLock)
            {
                if (!_started || !_imguiCreated || _disposed)
                    return;
            }
            ForceReleaseInputCapture();
            Volatile.Write(ref _renderStopping, 1);
            _ui?.Close();
            ImguiHook.Disable();
        }
    }

    public void Resume()
    {
        lock (_imguiOperationLock)
        {
            lock (_lifecycleLock)
            {
                if (!_started || !_imguiCreated || _disposed)
                    return;
            }
            Volatile.Write(ref _renderStopping, 0);
            ImguiHook.Enable();
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

        _ = StartCoreAsync(loaderApi, modId);
    }

    private async Task StartCoreAsync(IModLoaderV1 loaderApi, string modId)
    {
        long managedStartupStarted = Stopwatch.GetTimestamp();
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
            CompleteStartupPhase("reloaded-hooks-controller", hooksControllerStarted);

            long nativeStarted = BeginStartupPhase("native-core");
            NativeCore.Configure(modDirectory);
            bool hooksReady = NativeCore.Initialize(Log);
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

            long imguiSdkStarted = BeginStartupPhase("imgui-sdk");
            SDK.Init(hooks, message =>
            {
                if (!message.Contains(
                        "Discarding via Recursion Lock",
                        StringComparison.Ordinal))
                {
                    Log($"Reloaded.Imgui.Hook: {message}");
                }
            });
            CompleteStartupPhase("imgui-sdk", imguiSdkStarted);

            long imguiHookStarted = BeginStartupPhase("imgui-hook-create");
            await ImguiHook.Create(
                    Render,
                    new ImguiHookOptions
                    {
                        // The selector lives inside the game window. Extra platform
                        // windows add another DXGI/compositor path and can leave
                        // transient black trails while the mouse is moving.
                        EnableViewports = false,
                        CustomWndProcHandlerPointer = GetWndProcHandlerPointer(),
                        Implementations = new List<IImguiHook>
                        {
                            new CjkConfiguredDx11Hook(
                                modDirectory,
                                NativeCore.Tick,
                                static () => FrontendOverlayGate.ShouldRenderFrame,
                                Log),
                        },
                    }
                )
                .ConfigureAwait(false);
            CompleteStartupPhase("imgui-hook-create", imguiHookStarted);

            long overlayUiStarted = BeginStartupPhase("overlay-ui");
            SigilOverlayUi ui = new(modDirectory, SetInputCapture, Log);
            bool initialized = false;
            lock (_imguiOperationLock)
            {
                lock (_lifecycleLock)
                {
                    _starting = false;
                    if (!_disposed)
                    {
                        _ui = ui;
                        _imguiCreated = true;
                        _started = true;
                        Volatile.Write(ref _renderStopping, 0);
                        initialized = true;
                    }
                }

                if (!initialized)
                {
                    ImguiHook.Destroy();
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
            Log("Direct3D11 Reloaded ImGui frontend initialized; ReShade and Luma are not used by this mod.");
            Log($"Press {GetConfiguredHotkeyName()} to open the extra-sigil selector.");
            CompleteStartupPhase("managed-initialize", managedStartupStarted);
        }
        catch (Exception exception)
        {
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
            if (Interlocked.CompareExchange(ref _renderThreadLogged, 1, 0) == 0)
                Log($"First Direct3D11 Present callback: OS TID {GetCurrentThreadId()}.");
            _ui?.RenderFrame();
            if (Volatile.Read(ref s_capturedRawInputMessages) +
                    Volatile.Read(ref s_capturedMouseKeyboardMessages) > 0 &&
                Interlocked.CompareExchange(ref _inputCaptureLogged, 1, 0) == 0)
            {
                Log(
                    $"GUI input capture confirmed: raw={Volatile.Read(ref s_capturedRawInputMessages)}, " +
                    $"mouse/keyboard={Volatile.Read(ref s_capturedMouseKeyboardMessages)}."
                );
            }
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
        SigilOverlayUi? ui;
        HotkeyConfig? hotkeyConfiguration;
        CancellationTokenSource? executableHashCancellation;
        bool destroyImgui;
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
                destroyImgui = _imguiCreated;
                shutdownCore = _nativeCoreActive;
                hotkeyConfiguration = _hotkeyConfiguration;
                _hotkeyConfiguration = null;
                executableHashCancellation = _executableHashCancellation;
                _executableHashCancellation = null;
                _executableHashTask = null;
                _imguiCreated = false;
                _nativeCoreActive = false;
                _started = false;
            }

            ForceReleaseInputCapture();
            Volatile.Write(ref _renderStopping, 1);
            if (destroyImgui)
                ImguiHook.Disable();
        }
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
                if (destroyImgui)
                    ImguiHook.Destroy();
                ui?.Dispose();
            }
            if (shutdownCore)
                NativeCore.Shutdown();
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
