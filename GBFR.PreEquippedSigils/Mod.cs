using System.Diagnostics;
using GBFR.PreEquippedSigils.Configuration;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace GBFR.PreEquippedSigils;

/// <summary>
/// Thin Reloaded-II shell for GBFR.PreEquippedSigils.
/// There is no overlay UI, input capture, preset store or Overlay Broker:
/// the native core installs its own hooks via SafetyHook and applies the
/// built-in template loadout automatically. This shell only hosts the
/// native module, forwards logs, and drives the upkeep tick.
/// </summary>
public sealed class Mod : IMod
{
    private const string ModId = "GBFR.PreEquippedSigils";
    private const string LogTag = "GBFR Pre-Equipped Sigils"; // user-facing log prefix only (ModId stays technical)
    private const int TickIntervalMilliseconds = 250;

    private readonly object _logLock = new();
    private ILogger? _logger;
    private StreamWriter? _fileLog;
    private System.Threading.Timer? _tickTimer;
    private bool _nativeCoreActive;
    private bool _disposed;
    private HotkeyConfig? _hotkeyConfiguration;

    public Action Disposing => Dispose;

    public void Start(IModLoaderV1 loader) => QueueStart(loader);

    public void StartEx(IModLoaderV1 loader, IModConfigV1 _) => QueueStart(loader);

    public void Suspend()
    {
        // No frontend to suspend; the native core keeps running.
    }

    public void Resume()
    {
        // No frontend to resume.
    }

    public void Unload() => Dispose();

    public bool CanUnload() => false;

    public bool CanSuspend() => true;

    private void QueueStart(IModLoaderV1 loaderApi)
    {
        long started = Stopwatch.GetTimestamp();
        try
        {
            IModLoader loader = (IModLoader)loaderApi;
            lock (_logLock)
                _logger = (ILogger)loader.GetLogger();

            string modDirectory = loader.GetDirectoryForModId(ModId);
            Directory.CreateDirectory(modDirectory);
            lock (_logLock)
            {
                _fileLog?.Dispose();
                _fileLog = new StreamWriter(
                    Path.Combine(modDirectory, "GBFR.PreEquippedSigils.Reloaded.log"),
                    append: false)
                {
                    AutoFlush = true,
                };
            }
            long nativeStarted = BeginStartupPhase("native-core");
            NativeCore.Configure(modDirectory);
            bool hooksReady = NativeCore.Initialize(Log);
            CompleteStartupPhase("native-core", nativeStarted, hooksReady);
            if (!hooksReady)
            {
                Log($"Native core loaded without hooks: {NativeCore.GetRuntimeMessage()}");
            }
            LoadoutConfig.Initialize(modDirectory, Log);
            InitializeHotkeyConfiguration(loader, modDirectory);

            _nativeCoreActive = true;
            _tickTimer = new System.Threading.Timer(
                _ =>
                {
                    try
                    {
                        LoadoutConfig.Tick(Log);
                        Hotkey.Tick(Log);
                        NativeCore.Tick();
                    }
                    catch
                    {
                        // The upkeep tick must never tear down the process.
                    }
                },
                null,
                TickIntervalMilliseconds,
                TickIntervalMilliseconds);

            CompleteStartupPhase("managed-initialize", started);
        }
        catch (Exception exception)
        {
            Log($"Initialization failed: {exception}");
            Dispose();
        }
    }

    private void InitializeHotkeyConfiguration(IModLoader loader, string modDirectory)
    {
        try
        {
            string configDirectory = loader.GetModConfigDirectory(ModId);
            HotkeyConfig configuration =
                new Configurator(configDirectory).GetConfiguration<HotkeyConfig>(0);
            configuration.ConfigurationUpdated += OnHotkeyConfigurationUpdated;
            _hotkeyConfiguration = configuration;
            Hotkey.Configure(modDirectory, configuration.VirtualKey, Log);
        }
        catch (Exception exception)
        {
            Log($"Hotkey configuration unavailable: {exception.Message}");
        }
    }

    private void OnHotkeyConfigurationUpdated(IUpdatableConfigurable configurable)
    {
        if (configurable is HotkeyConfig configuration)
            Hotkey.UpdateHotkey(configuration.VirtualKey);
    }

    private void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] [{LogTag}] {message}";
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
                // File logging must never affect the mod lifecycle.
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
        // Phases log once on completion; see CompleteStartupPhase.
        return Stopwatch.GetTimestamp();
    }

    private void CompleteStartupPhase(string phase, long startedAt, bool succeeded = true)
    {
        long elapsedMilliseconds = (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        Log(
            $"Startup phase={phase} state={(succeeded ? "complete" : "failed")} " +
            $"elapsed_ms={elapsedMilliseconds}."
        );
    }

    private void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        _tickTimer?.Dispose();
        _tickTimer = null;
        Hotkey.Shutdown();
        if (_nativeCoreActive)
        {
            _nativeCoreActive = false;
            try
            {
                NativeCore.Shutdown();
            }
            catch
            {
                // Preserve the original shutdown path.
            }
        }

        lock (_logLock)
        {
            _fileLog?.Dispose();
            _fileLog = null;
        }
    }
}
