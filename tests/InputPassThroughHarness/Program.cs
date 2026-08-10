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

MethodInfo shouldCapture = classifierType.GetMethod(
    "ShouldCapture",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(classifierType.FullName, "ShouldCapture");
MethodInfo shouldCaptureRawInputType = classifierType.GetMethod(
    "ShouldCaptureRawInputType",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(classifierType.FullName, "ShouldCaptureRawInputType");
MethodInfo shouldCaptureWithoutRawInput = classifierType.GetMethod(
    "ShouldCaptureWithoutRawInput",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(
        classifierType.FullName,
        "ShouldCaptureWithoutRawInput");
Type brokerHostType = assembly.GetType(
    "GBFR.OverlayHub.Runtime.OverlayBrokerHost",
    throwOnError: true)!;
MethodInfo shouldSuppressWindowMessage = brokerHostType.GetMethod(
    "ShouldSuppressWindowMessage",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(
        brokerHostType.FullName,
        "ShouldSuppressWindowMessage");
MethodInfo resolveEffectiveInputDevices = brokerHostType.GetMethod(
    "ResolveEffectiveInputDevices",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(
        brokerHostType.FullName,
        "ResolveEffectiveInputDevices");
MethodInfo shouldRouteWindowMessageToImGui = brokerHostType.GetMethod(
    "ShouldRouteWindowMessageToImGui",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(
        brokerHostType.FullName,
        "ShouldRouteWindowMessageToImGui");
MethodInfo requiresDefaultRawInputCleanup = brokerHostType.GetMethod(
    "RequiresDefaultRawInputCleanup",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(
        brokerHostType.FullName,
        "RequiresDefaultRawInputCleanup");
Type safeImguiHookType = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.SafeImguiHookDx11",
    throwOnError: true)!;
MethodInfo isFrontendWakeFrame = safeImguiHookType.GetMethod(
    "IsFrontendWakeFrame",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(safeImguiHookType.FullName, "IsFrontendWakeFrame");
Type inputResetGateType = assembly.GetType(
    "GBFR.OverlayHub.Runtime.ImGuiInputResetGate",
    throwOnError: true)!;
MethodInfo requestInputReset = inputResetGateType.GetMethod(
    "Request",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(inputResetGateType.FullName, "Request");
MethodInfo consumeInputReset = inputResetGateType.GetMethod(
    "Consume",
    BindingFlags.NonPublic | BindingFlags.Static)
    ?? throw new MissingMethodException(inputResetGateType.FullName, "Consume");

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

(uint Message, int Devices, bool Capture, string Name)[] deviceCases =
[
    (0x0100, 1, true, "keyboard key"),
    (0x0101, 1, true, "keyboard key release"),
    (0x0104, 1, true, "system key"),
    (0x0201, 1, false, "keyboard does not capture mouse"),
    (0x0200, 2, true, "mouse movement"),
    (0x0201, 2, true, "mouse button"),
    (0x00AB, 2, true, "non-client XBUTTON"),
    (0x0100, 2, false, "mouse does not capture keyboard"),
    (0x0102, 4, true, "text character"),
    (0x010F, 4, true, "IME composition"),
    (0x0286, 4, true, "IME character"),
    (0x0100, 4, false, "text does not capture key state"),
    (0x0240, 7, false, "touch remains outside keyboard/mouse/text capture"),
    (0x000F, 7, false, "paint remains outside input capture"),
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
    bool fallbackActual = (bool)(shouldCaptureWithoutRawInput.Invoke(
        null,
        [message, Devices(devices)]) ?? false);
    if (fallbackActual != expected)
    {
        throw new InvalidOperationException(
            $"Exception fallback policy ({name}): expected capture={expected}, " +
            $"got {fallbackActual}.");
    }
}
Assert(!(bool)(shouldCaptureWithoutRawInput.Invoke(
        null,
        [0x00FFu, Devices(3)]) ?? true),
    "The exception fallback must fail open for unclassified WM_INPUT.");
Console.WriteLine("BROKER_DEVICE_WINDOW_INPUT_CLASSIFICATION=PASS");

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

(int Requested, int Previous, int Native, int Effective, string Name)[] releaseCases =
[
    (0, 7, 3, 7, "held keyboard and mouse keep the closing drain active"),
    (0, 7, 1, 5, "mouse release restores only mouse input"),
    (0, 7, 2, 2, "keyboard release restores keyboard and text input"),
    (0, 7, 0, 0, "full native release ends the closing drain"),
    (1, 7, 3, 3, "keyboard request clears text while mouse is still draining"),
    (4, 7, 3, 6, "text request clears key messages while mouse is still draining"),
    (7, 0, 3, 7, "capture additions become effective immediately"),
];
foreach ((int requested, int previous, int native, int expected, string name) in releaseCases)
{
    object actualValue = resolveEffectiveInputDevices.Invoke(
        null,
        [Devices(requested), Devices(previous), Devices(native)])
        ?? throw new InvalidOperationException($"Input release policy ({name}) returned null.");
    int actual = Convert.ToInt32(actualValue);
    if (actual != expected)
    {
        throw new InvalidOperationException(
            $"Input release policy ({name}): expected effective={expected}, got {actual}.");
    }
}
Console.WriteLine("BROKER_TWO_PHASE_INPUT_RELEASE=PASS");

Assert(!(bool)(shouldRouteWindowMessageToImGui.Invoke(
        null,
        [false, Devices(0)]) ?? true),
    "A sleeping frontend must not enqueue closed-period messages into ImGui.");
Assert((bool)(shouldRouteWindowMessageToImGui.Invoke(
        null,
        [true, Devices(0)]) ?? false),
    "A renderable peer must continue receiving ImGui Win32 input.");
Assert((bool)(shouldRouteWindowMessageToImGui.Invoke(
        null,
        [false, Devices(2)]) ?? false),
    "An explicit input request must keep ImGui input routing active.");
Assert((bool)(isFrontendWakeFrame.Invoke(null, [false, true]) ?? false),
    "The first rendered frame after sleep must flush queued ImGui input.");
Assert(!(bool)(isFrontendWakeFrame.Invoke(null, [true, true]) ?? true),
    "Continuous rendering must not repeatedly disable ImGui input trickling.");
Assert(!(bool)(isFrontendWakeFrame.Invoke(null, [false, false]) ?? true),
    "A sleeping frame is not an ImGui frontend wake.");
Console.WriteLine("BROKER_CLOSED_INPUT_QUEUE_POLICY=PASS");

Assert(!(bool)(consumeInputReset.Invoke(null, null) ?? true),
    "The frontend input reset gate must start empty.");
requestInputReset.Invoke(null, null);
requestInputReset.Invoke(null, null);
Assert((bool)(consumeInputReset.Invoke(null, null) ?? false),
    "Multiple reset requests must coalesce into one Present-thread reset.");
Assert(!(bool)(consumeInputReset.Invoke(null, null) ?? true),
    "Consuming a frontend input reset must clear the pending bit.");
Console.WriteLine("BROKER_INPUT_RESET_GATE=PASS");

Assert((bool)(requiresDefaultRawInputCleanup.Invoke(
        null,
        [0x00FFu, IntPtr.Zero]) ?? false),
    "Foreground WM_INPUT suppression must still run DefWindowProc cleanup.");
Assert(!(bool)(requiresDefaultRawInputCleanup.Invoke(
        null,
        [0x00FFu, new IntPtr(1)]) ?? true),
    "RIM_INPUTSINK does not use the foreground WM_INPUT cleanup path.");
Assert(!(bool)(requiresDefaultRawInputCleanup.Invoke(
        null,
        [0x0200u, IntPtr.Zero]) ?? true),
    "Only WM_INPUT can require raw-input cleanup.");
Console.WriteLine("BROKER_RAW_INPUT_CLEANUP_POLICY=PASS");

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
        return File.Exists(harnessDependency)
            ? LoadFromAssemblyPath(harnessDependency)
            : null;
    }
}
