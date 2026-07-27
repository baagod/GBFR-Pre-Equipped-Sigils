using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;

if (args.Length != 1)
    throw new ArgumentException("Pass the managed build output directory.");

string outputDirectory = Path.GetFullPath(args[0]);
string assemblyPath = Path.Combine(outputDirectory, "GBFR.ExtraSigilSlots.Reloaded.dll");
PluginLoadContext context = new(assemblyPath);
Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);

Type hashDiagnostic = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.ExecutableHashDiagnostic",
    throwOnError: true)!;
MethodInfo computeSha256 = GetStaticMethod(hashDiagnostic, "ComputeSha256Async");
MethodInfo startCore = GetStaticMethod(hashDiagnostic, "StartCore");

string hashInputPath = Path.Combine(
    Path.GetTempPath(),
    "GBFRES-hash-" + Guid.NewGuid().ToString("N") + ".bin");
byte[] hashInput = Encoding.UTF8.GetBytes("GBFR deferred executable hash diagnostic");
await File.WriteAllBytesAsync(hashInputPath, hashInput);
try
{
    Task<string> productHashTask = (Task<string>)(computeSha256.Invoke(
        null,
        [hashInputPath, CancellationToken.None]) ??
        throw new InvalidOperationException("Hash task was not returned."));
    string productHash = await productHashTask;
    string expectedHash = Convert.ToHexString(SHA256.HashData(hashInput));
    Assert(productHash == expectedHash, "The deferred worker must compute exact SHA-256 bytes.");
}
finally
{
    File.Delete(hashInputPath);
}
Console.WriteLine("DEFERRED_SHA256_CORRECTNESS=PASS");

TaskCompletionSource<string> releaseWorker = new(
    TaskCreationOptions.RunContinuationsAsynchronously);
ManualResetEventSlim workerEntered = new(false);
ConcurrentQueue<string> hashLogs = new();
Func<CancellationToken, Task<string>> blockedWorker = _ =>
{
    workerEntered.Set();
    return releaseWorker.Task;
};
Task deferredTask = (Task)(startCore.Invoke(
    null,
    [blockedWorker, (Action<string>)hashLogs.Enqueue, CancellationToken.None, @"C:\game\granblue_fantasy_relink.exe"]) ??
    throw new InvalidOperationException("Deferred diagnostic task was not returned."));
Assert(workerEntered.Wait(TimeSpan.FromSeconds(5)), "The deferred worker did not start.");
Assert(!deferredTask.IsCompleted, "The caller must not wait for the full-file hash worker.");
Assert(hashLogs.Any(line =>
        line.Contains("phase=executable-sha256 state=begin", StringComparison.Ordinal) &&
        line.Contains("diagnostic_only=true", StringComparison.Ordinal)),
    "The deferred diagnostic must log its begin marker.");

string knownHash = (string)(hashDiagnostic.GetField(
    "KnownExecutableSha256",
    BindingFlags.NonPublic | BindingFlags.Static)?.GetRawConstantValue() ??
    throw new MissingFieldException(hashDiagnostic.FullName, "KnownExecutableSha256"));
releaseWorker.SetResult(knownHash);
await deferredTask;
string[] completedHashLogs = hashLogs.ToArray();
int beginIndex = Array.FindIndex(completedHashLogs, line =>
    line.Contains("phase=executable-sha256 state=begin", StringComparison.Ordinal));
int completeIndex = Array.FindIndex(completedHashLogs, line =>
    line.Contains("phase=executable-sha256 state=complete", StringComparison.Ordinal));
Assert(beginIndex >= 0 && completeIndex > beginIndex,
    "The deferred hash phase must complete after its begin marker.");
Assert(completedHashLogs.Any(line =>
        line.Contains("phase=executable-sha256 state=complete", StringComparison.Ordinal) &&
        line.Contains("known_hash_match=true", StringComparison.Ordinal) &&
        line.Contains("elapsed_ms=", StringComparison.Ordinal)),
    "The deferred diagnostic must log completion, elapsed time, and match status.");
Console.WriteLine("DEFERRED_SHA256_NONBLOCKING=PASS");

ConcurrentQueue<string> failedHashLogs = new();
Func<CancellationToken, Task<string>> failedWorker = _ =>
    Task.FromException<string>(new InvalidDataException("synthetic hash failure"));
Task failedTask = (Task)(startCore.Invoke(
    null,
    [failedWorker, (Action<string>)failedHashLogs.Enqueue, CancellationToken.None, null]) ??
    throw new InvalidOperationException("Failed diagnostic task was not returned."));
await failedTask;
Assert(failedHashLogs.Any(line =>
        line.Contains("phase=executable-sha256 state=failed", StringComparison.Ordinal) &&
        line.Contains("error=InvalidDataException", StringComparison.Ordinal)),
    "Hash failures must be logged and contained.");

using CancellationTokenSource cancelled = new();
cancelled.Cancel();
ConcurrentQueue<string> cancelledHashLogs = new();
Func<CancellationToken, Task<string>> cancelledWorker = token =>
    Task.FromCanceled<string>(token);
Task cancelledTask = (Task)(startCore.Invoke(
    null,
    [cancelledWorker, (Action<string>)cancelledHashLogs.Enqueue, cancelled.Token, null]) ??
    throw new InvalidOperationException("Cancelled diagnostic task was not returned."));
await cancelledTask;
Assert(cancelledHashLogs.Any(line =>
        line.Contains("phase=executable-sha256 state=failed", StringComparison.Ordinal) &&
        line.Contains("reason=cancelled", StringComparison.Ordinal)),
    "Hash cancellation must be logged and contained.");

Func<CancellationToken, Task<string>> immediateWorker = _ => Task.FromResult(knownHash);
Task throwingLoggerTask = (Task)(startCore.Invoke(
    null,
    [immediateWorker, (Action<string>)(_ => throw new InvalidOperationException("logger failure")),
     CancellationToken.None, null]) ??
    throw new InvalidOperationException("Logger-containment task was not returned."));
await throwingLoggerTask;
Console.WriteLine("DEFERRED_SHA256_FAILURE_CONTAINMENT=PASS");

Type sourceDetector = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.ReloadedInjectionSourceDetector",
    throwOnError: true)!;
MethodInfo classify = GetStaticMethod(sourceDetector, "ClassifyCandidates");
MethodInfo format = GetStaticMethod(sourceDetector, "FormatLogMessage");

object asi = Classify(
    [@"D:\Game\scripts\Reloaded.Mod.Loader.Bootstrapper.asi"],
    [true]);
Assert(ReadKind(asi) == "AsiBootstrapper", "Official Deploy ASI must classify as ASI.");
string asiLog = (string)(format.Invoke(null, [asi]) ?? string.Empty);
Assert(asiLog.Contains("由 .asi Bootstrapper 加载", StringComparison.Ordinal),
    "ASI logs must state the load source explicitly.");
Console.WriteLine("OFFICIAL_DEPLOY_ASI_CLASSIFICATION=PASS");

object launcher = Classify(
    [@"C:\Reloaded-II\Loader\x64\Bootstrapper\Reloaded.Mod.Loader.Bootstrapper.dll"],
    [true]);
Assert(ReadKind(launcher) == "Launcher", "Launcher injection must classify as Launcher.");
string launcherLog = (string)(format.Invoke(null, [launcher]) ?? string.Empty);
Assert(launcherLog.Contains("由 Launcher 注入", StringComparison.Ordinal),
    "Launcher logs must state the injection source explicitly.");
Console.WriteLine("LAUNCHER_INJECTION_CLASSIFICATION=PASS");

Assert(ReadKind(Classify(
        [@"D:\Game\scripts\Reloaded.Mod.Loader.Bootstrapper.asi"],
        [false])) == "Unknown",
    "A similarly named ASI without InitializeASI must not be trusted.");
Assert(ReadKind(Classify(
        [@"D:\Game\scripts\Reloaded.Mod.Loader.Bootstrapper.asi",
         @"C:\Reloaded-II\Reloaded.Mod.Loader.Bootstrapper.dll"],
        [true, true])) == "Unknown",
    "Conflicting simultaneous bootstrapper sources must not be guessed.");
Assert(ReadKind(Classify(
        [@"D:\Game\scripts\Reloaded.Mod.Loader.Bootstrapper.asi",
         @"D:\Other\Reloaded.Mod.Loader.Bootstrapper.dll"],
        [true, false])) == "Unknown",
    "A valid module mixed with a similarly named invalid module must remain unknown.");
Assert(ReadKind(Classify([@"D:\Game\scripts\unrelated.asi"], [true])) == "Unknown",
    "Unrelated ASI modules must not be misclassified.");
Assert(ReadKind(Classify(
        [@"D:\GAME\SCRIPTS\RELOADED.MOD.LOADER.BOOTSTRAPPER.ASI"],
        [true])) == "AsiBootstrapper",
    "Official module matching must be case-insensitive on Windows.");
Console.WriteLine("INJECTION_SOURCE_FALSE_POSITIVES=PASS");
Console.WriteLine("STARTUP_DIAGNOSTICS_TEST=PASS");

object Classify(string[] paths, bool[] exports) =>
    classify.Invoke(null, [paths, exports]) ??
    throw new InvalidOperationException("Source classification returned null.");

static string ReadKind(object source) =>
    source.GetType().GetProperty("Kind")?.GetValue(source)?.ToString() ?? string.Empty;

static MethodInfo GetStaticMethod(Type type, string name) => type.GetMethod(
    name,
    BindingFlags.NonPublic | BindingFlags.Static) ??
    throw new MissingMethodException(type.FullName, name);

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class PluginLoadContext(string pluginPath) : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver = new(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string? path = _resolver.ResolveAssemblyToPath(assemblyName);
        if (path is not null)
            return LoadFromAssemblyPath(path);
        string harnessDependency = Path.Combine(
            AppContext.BaseDirectory,
            assemblyName.Name + ".dll");
        return File.Exists(harnessDependency) ? LoadFromAssemblyPath(harnessDependency) : null;
    }
}
