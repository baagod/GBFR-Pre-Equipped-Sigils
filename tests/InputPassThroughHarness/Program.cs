using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 1)
    throw new ArgumentException("Pass the managed build output directory.");

string outputDirectory = Path.GetFullPath(args[0]);
string assemblyPath = Path.Combine(outputDirectory, "GBFR.ExtraSigilSlots.Reloaded.dll");
PluginLoadContext context = new(assemblyPath);
Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
Type classifierType = assembly.GetType(
    "GBFR.OverlayHub.Runtime.OverlayWindowInputClassifier",
    throwOnError: true)!;

MethodInfo alwaysCaptured = classifierType.GetMethod(
    "IsAlwaysCaptured",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(classifierType.FullName, "IsAlwaysCaptured");
MethodInfo shouldCapture = classifierType.GetMethod(
    "ShouldCapture",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(classifierType.FullName, "ShouldCapture");
MethodInfo shouldCaptureRawInputType = classifierType.GetMethod(
    "ShouldCaptureRawInputType",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(classifierType.FullName, "ShouldCaptureRawInputType");
Type brokerHostType = assembly.GetType(
    "GBFR.OverlayHub.Runtime.OverlayBrokerHost",
    throwOnError: true)!;
MethodInfo shouldSuppressWindowMessage = brokerHostType.GetMethod(
    "ShouldSuppressWindowMessage",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(
        brokerHostType.FullName,
        "ShouldSuppressWindowMessage");

Type devicesType = shouldCapture.GetParameters()[2].ParameterType;
object Devices(int value) => Enum.ToObject(devicesType, value);

(int Type, int Devices, bool Capture, string Name)[] rawCases =
[
    (0, 2, true, "mouse captured"),
    (0, 1, false, "mouse passed through"),
    (1, 1, true, "keyboard captured"),
    (1, 2, false, "keyboard passed through"),
    (2, 3, false, "HID/controller"),
    (3, 7, false, "unknown/future"),
];
foreach ((int type, int devices, bool expected, string name) in rawCases)
{
    bool actual = (bool)(shouldCaptureRawInputType.Invoke(
        null,
        [type, Devices(devices)]) ?? false);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"Raw input type {type} ({name}): expected capture={expected}, got {actual}.");
    }
    Console.WriteLine(
        $"RAW_TYPE={type} DEVICES={devices} NAME={name} CAPTURE={actual}");
}
Console.WriteLine("BROKER_RAW_INPUT_CLASSIFICATION=PASS");

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
    bool actual = (bool)(alwaysCaptured.Invoke(null, [message]) ?? false);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"Window message 0x{message:X4} ({name}): expected capture={expected}, got {actual}.");
    }
    Console.WriteLine($"WINDOW_MESSAGE=0x{message:X4} NAME={name} CAPTURE={actual}");
}
Console.WriteLine("BROKER_WINDOW_INPUT_CLASSIFICATION=PASS");

(uint Message, int Devices, bool Capture, string Name)[] deviceCases =
[
    (0x0100, 1, true, "keyboard key"),
    (0x0201, 1, false, "keyboard does not capture mouse"),
    (0x0201, 2, true, "mouse button"),
    (0x0100, 2, false, "mouse does not capture keyboard"),
    (0x0102, 4, true, "text character"),
    (0x0100, 4, false, "text does not capture key state"),
];
foreach ((uint message, int devices, bool expected, string name) in deviceCases)
{
    bool actual = (bool)(shouldCapture.Invoke(
        null,
        [message, IntPtr.Zero, Devices(devices)]) ?? false);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"Device policy ({name}): expected capture={expected}, got {actual}.");
    }
}
Console.WriteLine("BROKER_DEVICE_CAPTURE_POLICY=PASS");

(uint Message, int Devices, bool Suppress, string Name)[] brokerCases =
[
    (0x0006, 3, false, "WM_ACTIVATE must reach the game"),
    (0x0008, 3, false, "WM_KILLFOCUS must reach the game"),
    (0x001C, 3, false, "WM_ACTIVATEAPP must reach the game"),
    (0x001F, 3, false, "WM_CANCELMODE must reach the game"),
    (0x0215, 3, false, "WM_CAPTURECHANGED must reach the game"),
    (0x0100, 1, true, "captured keyboard key"),
    (0x0201, 2, true, "captured mouse button"),
    (0x0100, 0, false, "no active capture"),
];
foreach ((uint message, int devices, bool expected, string name) in brokerCases)
{
    bool actual = (bool)(shouldSuppressWindowMessage.Invoke(
        null,
        [message, IntPtr.Zero, Devices(devices)]) ?? false);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"Broker WndProc policy ({name}): expected suppress={expected}, got {actual}.");
    }
}
Console.WriteLine("BROKER_FOCUS_AND_CAPTURE_FORWARDING=PASS");

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
        return File.Exists(harnessDependency)
            ? LoadFromAssemblyPath(harnessDependency)
            : null;
    }
}
