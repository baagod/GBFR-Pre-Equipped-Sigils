using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace GBFR.ReloadedSigilSlots;

/// <summary>
/// Minimal native-core facade. Only the functions the thin mod shell actually
/// uses are kept: ABI check, log sink, input-hook disable, initialize, upkeep
/// tick, shutdown and runtime-message readback. All selector/inventory/preset/
/// input/present APIs of the derived original were removed.
/// </summary>
internal static unsafe partial class NativeCore
{
    internal const int AbiVersion = 14;

    private const string LibraryName = "GBFR.ReloadedSigilSlots.Native.dll";
    private static readonly object ResolverLock = new();
    private static readonly object NativeLogLock = new();
    private static string? _libraryPath;
    private static IntPtr _libraryHandle;
    private static int _resolverConfigured;
    private static Action<string>? _nativeLogSink;

    internal static void Configure(string modDirectory)
    {
        string path = Path.GetFullPath(Path.Combine(modDirectory, LibraryName));
        lock (ResolverLock)
        {
            if (_libraryPath is not null &&
                !string.Equals(_libraryPath, path, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Native core was already bound to a different path: {_libraryPath}"
                );
            }
            _libraryPath = path;
            if (Interlocked.Exchange(ref _resolverConfigured, 1) == 0)
            {
                NativeLibrary.SetDllImportResolver(
                    typeof(NativeCore).Assembly,
                    ResolveLibrary
                );
            }
        }
    }

    internal static bool Initialize(Action<string> log, bool enableInputHooks = false)
    {
        ArgumentNullException.ThrowIfNull(log);
        lock (NativeLogLock)
            _nativeLogSink = log;

        long nativeLibraryStarted = Stopwatch.GetTimestamp();
        bool nativeLibraryCompleted = false;
        try
        {
            log("Startup phase=native-library-load state=begin.");
            NativeSetLogCallback(
                (IntPtr)(delegate* unmanaged[Cdecl]<sbyte*, void>)&ForwardNativeLog
            );
            uint abiVersion = NativeGetAbiVersion();
            if (abiVersion != AbiVersion)
            {
                throw new InvalidOperationException(
                    $"Native ABI mismatch: managed {AbiVersion}, native {abiVersion}."
                );
            }
            if (NativeSetInputHooksEnabled(enableInputHooks ? 1 : 0) == 0)
            {
                throw new InvalidOperationException(
                    "Native input-hook mode could not be selected before initialization."
                );
            }
            log(
                "Startup phase=native-library-load state=complete " +
                $"elapsed_ms={(long)Stopwatch.GetElapsedTime(nativeLibraryStarted).TotalMilliseconds}."
            );
            nativeLibraryCompleted = true;
            return NativeInitialize() != 0;
        }
        catch
        {
            if (!nativeLibraryCompleted)
            {
                log(
                    "Startup phase=native-library-load state=failed " +
                    $"elapsed_ms={(long)Stopwatch.GetElapsedTime(nativeLibraryStarted).TotalMilliseconds}."
                );
            }
            DetachNativeLogSink();
            throw;
        }
    }

    internal static void Tick() => NativeTick();

    internal static void Shutdown()
    {
        try
        {
            NativeShutdown();
        }
        finally
        {
            DetachNativeLogSink();
        }
    }

    internal static string GetRuntimeMessage()
    {
        uint required = NativeCopyRuntimeMessage(null, 0);
        if (required <= 1)
            return string.Empty;
        if (required > 64 * 1024)
            required = 64 * 1024;
        byte[] bytes = new byte[required];
        fixed (byte* buffer = bytes)
            NativeCopyRuntimeMessage((sbyte*)buffer, required);
        int length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
            length = bytes.Length;
        return Encoding.UTF8.GetString(bytes, 0, length);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void ForwardNativeLog(sbyte* message)
    {
        try
        {
            string? text = Marshal.PtrToStringUTF8((IntPtr)message);
            if (string.IsNullOrEmpty(text))
                return;
            Action<string>? sink;
            lock (NativeLogLock)
                sink = _nativeLogSink;
            sink?.Invoke("Native: " + text);
        }
        catch
        {
            // A diagnostic callback must never unwind into native hook code.
        }
    }

    private static void DetachNativeLogSink()
    {
        try
        {
            if (_libraryHandle != IntPtr.Zero)
                NativeSetLogCallback(IntPtr.Zero);
        }
        catch
        {
            // The native module may already be unavailable during process teardown.
        }
        lock (NativeLogLock)
            _nativeLogSink = null;
    }

    private static IntPtr ResolveLibrary(
        string libraryName,
        Assembly assembly,
        DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, LibraryName, StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;
        lock (ResolverLock)
        {
            if (_libraryHandle != IntPtr.Zero)
                return _libraryHandle;
            if (_libraryPath is null || !File.Exists(_libraryPath))
                throw new DllNotFoundException($"Native core not found: {_libraryPath}");
            _libraryHandle = NativeLibrary.Load(_libraryPath);
            return _libraryHandle;
        }
    }
}
