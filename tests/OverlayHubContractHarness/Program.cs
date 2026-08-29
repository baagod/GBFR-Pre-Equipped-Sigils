using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

if (args.Length != 1)
    throw new ArgumentException("Pass the managed build output directory.");

string outputDirectory = Path.GetFullPath(args[0]);
string contractPath = Path.Combine(outputDirectory, "GBFR.OverlayHub.Contracts.dll");
string managedModPath = Path.Combine(outputDirectory, "GBFR.ExtraSigilSlots.Reloaded.dll");
string nativePath = Path.Combine(outputDirectory, "GBFR.ExtraSigilSlots.Native.dll");
string modConfigPath = Path.Combine(outputDirectory, "ModConfig.json");

Assert(File.Exists(contractPath), "The shared Overlay Hub contract DLL was not packaged.");
AssemblyName contractName = AssemblyName.GetAssemblyName(contractPath);
Assert(contractName.Name == "GBFR.OverlayHub.Contracts", "Unexpected contract assembly name.");
Assert(contractName.Version == new Version(1, 0, 0, 0), "Unexpected contract ABI version.");
Assembly contractAssembly = Assembly.LoadFile(contractPath);
Assert(contractAssembly.GetType("GBFR.OverlayHub.Contracts.IGbfrOverlayHub") is not null,
    "IGbfrOverlayHub is missing from the shared contract.");
Assert(contractAssembly.GetType("GBFR.OverlayHub.Contracts.IGbfrOverlayClient") is not null,
    "IGbfrOverlayClient is missing from the shared contract.");
Assert(contractAssembly.GetType("GBFR.OverlayHub.Contracts.IGbfrOverlayGraphicsClient") is not null,
    "IGbfrOverlayGraphicsClient is missing from the shared contract.");
Assert(contractAssembly.GetType("GBFR.OverlayHub.Contracts.OverlayGraphicsBinding") is not null,
    "OverlayGraphicsBinding is missing from the shared contract.");
Type hubType = contractAssembly.GetType(
    "GBFR.OverlayHub.Contracts.IGbfrOverlayHub",
    throwOnError: true)!;
Assert(hubType.GetProperty("HostModId") is not null,
    "Overlay Hub API v2 must expose its neutral bootstrap carrier identity.");
Assert(hubType.GetProperty("CapturedInputDevices") is not null,
    "Overlay Hub API v2 must expose the aggregate device capture policy.");
Assert(contractAssembly.GetType("GBFR.OverlayHub.Contracts.OverlayBrokerFactory") is not null,
    "The neutral Overlay Broker factory is missing from the shared contract.");
Type recoverableHubType = contractAssembly.GetType(
    "GBFR.OverlayHub.Contracts.IRecoverableGbfrOverlayHub",
    throwOnError: true)!;
Assert(hubType.IsAssignableFrom(recoverableHubType),
    "The optional recovery capability must extend the base Hub contract.");
Assert(recoverableHubType.GetProperty("IsHostAvailable") is not null &&
       recoverableHubType.GetMethod("TryAcquireHost") is not null,
    "The recovery capability is missing host availability or lease acquisition.");
Type hostControlType = contractAssembly.GetType(
    "GBFR.OverlayHub.Contracts.IOverlayBrokerHostControl",
    throwOnError: true)!;
Assert(typeof(IDisposable).IsAssignableFrom(hostControlType),
    "A host lease must be disposable.");
Console.WriteLine("OVERLAY_HUB_CONTRACT=PASS");

using (JsonDocument config = JsonDocument.Parse(File.ReadAllText(modConfigPath)))
{
    JsonElement root = config.RootElement;
    Assert(root.GetProperty("HasExports").GetBoolean(), "Extra must export the shared contract.");
    Assert(root.GetProperty("OptionalDependencies").EnumerateArray().Any(item =>
            item.GetString() == "gbfr.qol.chatoverlay"),
        "ChatOverlay must be declared as an optional peer.");
}
Console.WriteLine("OVERLAY_HUB_METADATA=PASS");

var probeContext = new AssemblyLoadContext("GBFR.ExtraSigilSlots.ExportProbe", isCollectible: true);
probeContext.Resolving += (_, assemblyName) =>
{
    string candidate = Path.Combine(outputDirectory, $"{assemblyName.Name}.dll");
    if (File.Exists(candidate))
        return probeContext.LoadFromAssemblyPath(candidate);

    candidate = Path.Combine(AppContext.BaseDirectory, $"{assemblyName.Name}.dll");
    return File.Exists(candidate) ? probeContext.LoadFromAssemblyPath(candidate) : null;
};
try
{
    Assembly managedModAssembly = probeContext.LoadFromAssemblyPath(managedModPath);
    Type modType = managedModAssembly.GetType("GBFR.ExtraSigilSlots.Reloaded.Mod", throwOnError: true)!;
    object mod = Activator.CreateInstance(modType)!;
    Type[] exports = (Type[])modType.GetMethod("GetTypes")!.Invoke(mod, null)!;
    Assert(exports.Length == 1, "The mod must export only the dependency-free Hub contract.");
    Assert(exports[0].FullName == "GBFR.OverlayHub.Contracts.IGbfrOverlayHub",
        "The mod exported an unexpected type.");
    Assert(exports[0].Assembly.GetName().Name == "GBFR.OverlayHub.Contracts",
        "The exported type must come from the dependency-free contract assembly.");
    Assert(!exports.Any(type => type.Assembly.GetName().Name is "DearImguiSharp" or "Reloaded.Imgui.Hook"),
        "Native-backed ImGui assemblies must never enter Reloaded-II's temporary export ALC.");
}
finally
{
    probeContext.Unload();
}
Console.WriteLine("OVERLAY_HUB_EXPORT_SAFETY=PASS");

nint nativeLibrary = NativeLibrary.Load(nativePath);
try
{
    var getAbiVersion = Marshal.GetDelegateForFunctionPointer<GetAbiVersionDelegate>(
        NativeLibrary.GetExport(nativeLibrary, "GBFR20_GetAbiVersion"));
    var setInputHooksEnabled = Marshal.GetDelegateForFunctionPointer<SetInputHooksEnabledDelegate>(
        NativeLibrary.GetExport(nativeLibrary, "GBFR20_SetInputHooksEnabled"));
    var setInputCapture = Marshal.GetDelegateForFunctionPointer<SetInputCaptureDelegate>(
        NativeLibrary.GetExport(nativeLibrary, "GBFR20_SetInputCapture"));
    var setInputCaptureDevices = Marshal.GetDelegateForFunctionPointer<SetInputCaptureDevicesDelegate>(
        NativeLibrary.GetExport(nativeLibrary, "GBFR20_SetInputCaptureDevices"));
    var getInputCaptureDevices = Marshal.GetDelegateForFunctionPointer<GetInputCaptureDevicesDelegate>(
        NativeLibrary.GetExport(nativeLibrary, "GBFR20_GetInputCaptureDevices"));
    var getInputCaptureActive = Marshal.GetDelegateForFunctionPointer<GetInputCaptureActiveDelegate>(
        NativeLibrary.GetExport(nativeLibrary, "GBFR20_GetInputCaptureActive"));
    Assert(getAbiVersion() == 14, "Managed/native ABI 14 was not built.");
    Assert(setInputHooksEnabled(0) != 0,
        "Guest mode must be selectable before native initialization.");
    Assert(setInputHooksEnabled(1) != 0,
        "Standalone input mode must remain selectable before native initialization.");
    Assert(setInputCaptureDevices(1u) != 0 &&
           getInputCaptureDevices() == 1u &&
           getInputCaptureActive() != 0,
        "The Broker must be able to capture keyboard input without requesting mouse capture.");
    Assert(setInputCaptureDevices(4u) != 0 && getInputCaptureDevices() == 1u,
        "Text capture must project onto the native keyboard gate.");
    Assert(setInputCapture(-1) != 0 &&
           getInputCaptureDevices() == 0u &&
           getInputCaptureActive() == 0,
        "The legacy force-release path must clear every device capture bit immediately.");
}
finally
{
    NativeLibrary.Free(nativeLibrary);
}
Console.WriteLine("NATIVE_INPUT_HOOK_MODE=PASS");

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate uint GetAbiVersionDelegate();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int SetInputHooksEnabledDelegate(int enabled);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int SetInputCaptureDelegate(int requested);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int SetInputCaptureDevicesDelegate(uint requestedDevices);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate uint GetInputCaptureDevicesDelegate();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
delegate int GetInputCaptureActiveDelegate();
