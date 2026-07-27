using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 1)
    throw new ArgumentException("Pass the managed build output directory.");

string outputDirectory = Path.GetFullPath(args[0]);
string assemblyPath = Path.Combine(outputDirectory, "GBFR.ExtraSigilSlots.Reloaded.dll");
PluginLoadContext context = new(assemblyPath);
Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
Type classifierType = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.RawInputClassifier",
    throwOnError: true)!;
MethodInfo classifier = classifierType.GetMethod(
    "IsKeyboardOrMouse",
    BindingFlags.NonPublic | BindingFlags.Static
) ?? throw new MissingMethodException(classifierType.FullName, "IsKeyboardOrMouse");

(uint Type, bool Capture, string Name)[] cases =
[
    (0, true, "mouse"),
    (1, true, "keyboard"),
    (2, false, "HID/controller"),
    (3, false, "unknown/future"),
];
foreach ((uint type, bool expected, string name) in cases)
{
    bool actual = (bool)(classifier.Invoke(null, [type]) ?? false);
    if (actual != expected)
        throw new InvalidOperationException(
            $"Raw input type {type} ({name}): expected capture={expected}, got {actual}."
        );
    Console.WriteLine($"RAW_TYPE={type} NAME={name} CAPTURE={actual}");
}

Console.WriteLine("RAW_INPUT_CLASSIFICATION=PASS");

Type windowClassifierType = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.WindowInputClassifier",
    throwOnError: true
)!;
MethodInfo windowClassifier = windowClassifierType.GetMethod(
    "IsAlwaysCaptured",
    BindingFlags.NonPublic | BindingFlags.Static
) ?? throw new MissingMethodException(windowClassifierType.FullName, "IsAlwaysCaptured");

(uint Message, bool Capture, string Name)[] windowCases =
[
    (0x0100, true, "WM_KEYDOWN"),
    (0x0101, true, "WM_KEYUP"),
    (0x0102, true, "WM_CHAR"),
    (0x0104, true, "WM_SYSKEYDOWN"),
    (0x0109, true, "WM_UNICHAR"),
    (0x010F, true, "WM_IME_COMPOSITION"),
    (0x00A1, true, "WM_NCLBUTTONDOWN"),
    (0x00AB, true, "WM_NCXBUTTONDOWN"),
    (0x0200, true, "WM_MOUSEMOVE"),
    (0x0201, true, "WM_LBUTTONDOWN"),
    (0x0207, true, "WM_MBUTTONDOWN"),
    (0x020B, true, "WM_XBUTTONDOWN"),
    (0x0286, true, "WM_IME_CHAR"),
    (0x0119, false, "WM_GESTURE"),
    (0x0240, false, "WM_TOUCH"),
    (0x0241, false, "WM_POINTERUPDATE"),
    (0x0312, false, "WM_HOTKEY"),
    (0x0319, false, "WM_APPCOMMAND"),
    (0x00FF, false, "WM_INPUT requires device classification"),
    (0x000F, false, "WM_PAINT"),
];
foreach ((uint message, bool expected, string name) in windowCases)
{
    bool actual = (bool)(windowClassifier.Invoke(null, [message]) ?? false);
    if (actual != expected)
        throw new InvalidOperationException(
            $"Window message 0x{message:X4} ({name}): expected capture={expected}, got {actual}."
        );
    Console.WriteLine($"WINDOW_MESSAGE=0x{message:X4} NAME={name} CAPTURE={actual}");
}

Console.WriteLine("WINDOW_INPUT_CLASSIFICATION=PASS");

Type capturePolicyType = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.InputCapturePolicy",
    throwOnError: true
)!;
MethodInfo capturePolicy = capturePolicyType.GetMethod(
    "ShouldCaptureWindowMessages",
    BindingFlags.NonPublic | BindingFlags.Static
) ?? throw new MissingMethodException(
    capturePolicyType.FullName,
    "ShouldCaptureWindowMessages"
);

(bool MenuOpen, bool NativeActive, bool Capture, string Name)[] captureCases =
[
    (true, true, true, "open menu with native barrier"),
    (true, false, true, "open menu without native barrier"),
    (false, false, false, "closed menu after native release"),
    (false, true, false, "closed menu while native barrier drains"),
];
foreach ((bool menuOpen, bool nativeActive, bool expected, string name) in captureCases)
{
    bool actual = (bool)(capturePolicy.Invoke(null, [menuOpen, nativeActive]) ?? false);
    if (actual != expected)
        throw new InvalidOperationException(
            $"Window capture policy ({name}): expected capture={expected}, got {actual}."
        );
    Console.WriteLine(
        $"MENU_OPEN={menuOpen} NATIVE_ACTIVE={nativeActive} WINDOW_CAPTURE={actual}"
    );
}

Console.WriteLine("WINDOW_CAPTURE_LIFECYCLE=PASS");

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
            assemblyName.Name + ".dll"
        );
        return File.Exists(harnessDependency) ? LoadFromAssemblyPath(harnessDependency) : null;
    }
}
