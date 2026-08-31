using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace GBFR.ReloadedSigilSlots;

internal enum ReloadedInjectionKind
{
    Unknown = 0,
    Launcher = 1,
    AsiBootstrapper = 2,
}

internal sealed record ReloadedInjectionSource(
    ReloadedInjectionKind Kind,
    string? ModulePath,
    string Detail);

internal static class ReloadedInjectionSourceDetector
{
    private const string BootstrapperBaseName = "Reloaded.Mod.Loader.Bootstrapper";
    private const string BootstrapperDllName = BootstrapperBaseName + ".dll";
    private const string BootstrapperAsiName = BootstrapperBaseName + ".asi";

    internal static ReloadedInjectionSource Detect()
    {
        List<string> paths = [];
        List<bool> exports = [];
        try
        {
            using Process process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
            {
                string moduleName;
                try
                {
                    moduleName = module.ModuleName;
                }
                catch
                {
                    continue;
                }
                if (!IsBootstrapperFileName(moduleName))
                    continue;

                string modulePath;
                try
                {
                    modulePath = module.FileName;
                }
                catch
                {
                    modulePath = moduleName;
                }
                paths.Add(modulePath);
                exports.Add(GetProcAddress(module.BaseAddress, "InitializeASI") != IntPtr.Zero);
            }
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or NotSupportedException or
                System.ComponentModel.Win32Exception)
        {
            // Fall through to exact GetModuleHandleW probes below.
        }

        AddModuleHandleFallback(BootstrapperAsiName, paths, exports);
        AddModuleHandleFallback(BootstrapperDllName, paths, exports);
        return ClassifyCandidates(paths.ToArray(), exports.ToArray());
    }

    internal static ReloadedInjectionSource ClassifyCandidates(
        string[] modulePaths,
        bool[] initializeAsiExports)
    {
        ArgumentNullException.ThrowIfNull(modulePaths);
        ArgumentNullException.ThrowIfNull(initializeAsiExports);
        if (modulePaths.Length != initializeAsiExports.Length)
            throw new ArgumentException("Module paths and export flags must have equal lengths.");

        string? asiPath = null;
        string? launcherPath = null;
        bool bootstrapperWithoutExport = false;
        for (int index = 0; index < modulePaths.Length; ++index)
        {
            string path = modulePaths[index] ?? string.Empty;
            string fileName;
            try
            {
                fileName = Path.GetFileName(path);
            }
            catch
            {
                continue;
            }
            if (!IsBootstrapperFileName(fileName))
                continue;
            if (!initializeAsiExports[index])
            {
                bootstrapperWithoutExport = true;
                continue;
            }
            if (string.Equals(fileName, BootstrapperAsiName, StringComparison.OrdinalIgnoreCase))
                asiPath ??= path;
            else if (string.Equals(fileName, BootstrapperDllName, StringComparison.OrdinalIgnoreCase))
                launcherPath ??= path;
        }

        if (bootstrapperWithoutExport && (asiPath is not null || launcherPath is not null))
        {
            return new ReloadedInjectionSource(
                ReloadedInjectionKind.Unknown,
                null,
                "valid and similarly named bootstrapper modules conflict");
        }
        if (asiPath is not null && launcherPath is null)
        {
            return new ReloadedInjectionSource(
                ReloadedInjectionKind.AsiBootstrapper,
                asiPath,
                "Reloaded-II Deploy ASI module layout with InitializeASI export");
        }
        if (launcherPath is not null && asiPath is null)
        {
            return new ReloadedInjectionSource(
                ReloadedInjectionKind.Launcher,
                launcherPath,
                "Reloaded-II launcher bootstrapper DLL with InitializeASI export");
        }
        if (asiPath is not null && launcherPath is not null)
        {
            return new ReloadedInjectionSource(
                ReloadedInjectionKind.Unknown,
                null,
                "both Launcher and .asi bootstrapper modules are loaded");
        }
        return new ReloadedInjectionSource(
            ReloadedInjectionKind.Unknown,
            null,
            bootstrapperWithoutExport
                ? "a similarly named module lacks the Reloaded InitializeASI export"
                : "no official Reloaded-II bootstrapper module was visible");
    }

    internal static string FormatLogMessage(ReloadedInjectionSource source)
    {
        string module = string.IsNullOrWhiteSpace(source.ModulePath)
            ? string.Empty
            : $" module=\"{Sanitize(source.ModulePath)}\"";
        return source.Kind switch
        {
            ReloadedInjectionKind.Launcher =>
                $"Reloaded-II load source=launcher (由 Launcher 注入);{module} evidence={source.Detail}.",
            ReloadedInjectionKind.AsiBootstrapper =>
                $"Reloaded-II load source=asi-bootstrapper (由 .asi Bootstrapper 加载);{module} evidence={source.Detail}.",
            _ =>
                $"Reloaded-II load source=unknown; evidence={source.Detail}.",
        };
    }

    private static void AddModuleHandleFallback(
        string moduleName,
        List<string> paths,
        List<bool> exports)
    {
        IntPtr module = GetModuleHandleW(moduleName);
        if (module == IntPtr.Zero)
            return;
        StringBuilder path = new(32768);
        uint length = GetModuleFileNameW(module, path, path.Capacity);
        string resolvedPath = length is > 0 and < 32768 ? path.ToString() : moduleName;
        if (paths.Any(candidate =>
                string.Equals(candidate, resolvedPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        paths.Add(resolvedPath);
        exports.Add(GetProcAddress(module, "InitializeASI") != IntPtr.Zero);
    }

    private static bool IsBootstrapperFileName(string pathOrName)
    {
        string fileName;
        try
        {
            fileName = Path.GetFileName(pathOrName);
        }
        catch
        {
            return false;
        }
        return string.Equals(fileName, BootstrapperDllName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(fileName, BootstrapperAsiName, StringComparison.OrdinalIgnoreCase);
    }

    private static string Sanitize(string value) =>
        value.Replace('\r', ' ').Replace('\n', ' ').Replace('"', '\'');

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr GetModuleHandleW(string lpModuleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetModuleFileNameW(
        IntPtr hModule,
        StringBuilder lpFilename,
        int nSize);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);
}
