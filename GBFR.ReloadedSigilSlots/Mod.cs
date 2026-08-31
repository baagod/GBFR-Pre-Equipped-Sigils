using System.Diagnostics;
using Reloaded.Mod.Interfaces;
using Reloaded.Mod.Interfaces.Internal;

namespace GBFR.ReloadedSigilSlots;

/// <summary>
/// Thin Reloaded-II shell for GBFR.ReloadedSigilSlots.
/// There is no overlay UI, input capture, preset store or Overlay Broker:
/// the native core installs its own hooks via SafetyHook and applies the
/// built-in template loadout automatically. This shell only hosts the
/// native module, forwards logs, and drives the upkeep tick.
/// </summary>
public sealed class Mod : IMod
{
    private const string ModId = "GBFR.ReloadedSigilSlots";
    private const int TickIntervalMilliseconds = 250;

    private readonly object _logLock = new();
    private ILogger? _logger;
    private StreamWriter? _fileLog;
    private System.Threading.Timer? _tickTimer;
    private bool _nativeCoreActive;
    private bool _disposed;

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
                    Path.Combine(modDirectory, "ReloadedSigilSlots.Reloaded.log"),
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

            _nativeCoreActive = true;
            _tickTimer = new System.Threading.Timer(
                _ =>
                {
                    try
                    {
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

    private void Log(string message)
    {
        string line = $"[{DateTime.Now:HH:mm:ss.fff}] [{ModId}] {message}";
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
