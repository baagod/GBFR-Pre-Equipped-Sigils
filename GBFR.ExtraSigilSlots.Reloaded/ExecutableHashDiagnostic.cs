using System.Diagnostics;
using System.Security.Cryptography;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal static class ExecutableHashDiagnostic
{
    internal const string KnownExecutableSha256 =
        "F827F3C13CAA90B290FAB2FE7E28165A80448FDE0A3F7A96D79DAC6B8343FF2A";

    internal static Task Start(
        string? executablePath,
        Action<string> log,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(log);
        return StartCore(
            token => ComputeSha256Async(executablePath, token),
            log,
            cancellationToken,
            executablePath);
    }

    internal static Task StartCore(
        Func<CancellationToken, Task<string>> computeHash,
        Action<string> log,
        CancellationToken cancellationToken,
        string? executablePath = null)
    {
        ArgumentNullException.ThrowIfNull(computeHash);
        ArgumentNullException.ThrowIfNull(log);

        // The native byte/RVA preflight and hook installation have already
        // completed synchronously before this method is called. Keep the
        // diagnostic on the thread pool so full-file I/O cannot hold up the
        // loader or let the game race ahead of the hooks.
        return Task.Run(async () =>
        {
            long startedAt = Stopwatch.GetTimestamp();
            string pathDetail = string.IsNullOrWhiteSpace(executablePath)
                ? string.Empty
                : $" path=\"{Sanitize(executablePath)}\"";
            SafeLog(
                log,
                "Startup phase=executable-sha256 state=begin " +
                $"mode=deferred-diagnostic diagnostic_only=true{pathDetail}.");
            try
            {
                string sha256 = await computeHash(cancellationToken).ConfigureAwait(false);
                bool knownHashMatch = string.Equals(
                    sha256,
                    KnownExecutableSha256,
                    StringComparison.OrdinalIgnoreCase);
                SafeLog(
                    log,
                    "Startup phase=executable-sha256 state=complete " +
                    $"elapsed_ms={ElapsedMilliseconds(startedAt)} sha256={sha256} " +
                    $"known_hash_match={(knownHashMatch ? "true" : "false")} " +
                    "diagnostic_only=true.");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                SafeLog(
                    log,
                    "Startup phase=executable-sha256 state=failed " +
                    $"elapsed_ms={ElapsedMilliseconds(startedAt)} reason=cancelled " +
                    "diagnostic_only=true.");
            }
            catch (Exception exception)
            {
                SafeLog(
                    log,
                    "Startup phase=executable-sha256 state=failed " +
                    $"elapsed_ms={ElapsedMilliseconds(startedAt)} " +
                    $"error={exception.GetType().Name} message=\"{Sanitize(exception.Message)}\" " +
                    "diagnostic_only=true.");
            }
        });
    }

    internal static async Task<string> ComputeSha256Async(
        string? executablePath,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new FileNotFoundException("The current executable path is unavailable.");

        await using FileStream stream = new(
            executablePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(digest);
    }

    private static long ElapsedMilliseconds(long startedAt) =>
        (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Replace('"', '\'');

    private static void SafeLog(Action<string> log, string message)
    {
        try
        {
            log(message);
        }
        catch
        {
            // Diagnostics must never affect loader or hook lifetime.
        }
    }
}
