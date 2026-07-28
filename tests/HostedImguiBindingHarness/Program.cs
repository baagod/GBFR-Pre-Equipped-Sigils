using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using DearImguiSharp;

if (args.Length != 1)
    throw new ArgumentException("Pass the managed build output directory.");

string outputDirectory = Path.GetFullPath(args[0]);
string managedModPath = Path.Combine(outputDirectory, "GBFR.ExtraSigilSlots.Reloaded.dll");
string contractPath = Path.Combine(outputDirectory, "GBFR.OverlayHub.Contracts.dll");
Assert(File.Exists(managedModPath), "The managed Extra Sigil build output is missing.");
Assert(File.Exists(contractPath), "The Overlay Hub contract build output is missing.");

using ImGuiContext hostContext = ImGui.CreateContext(null);
nint hostNativeLibrary = NativeLibrary.Load("cimgui", typeof(ImGui).Assembly, searchPath: null);
var guestContext = new ProbeLoadContext(outputDirectory);
try
{
    Assembly managedAssembly = guestContext.LoadFromAssemblyPath(managedModPath);
    Assembly contractAssembly = guestContext.LoadFromAssemblyPath(contractPath);
    Type bindingType = managedAssembly
        .GetType("GBFR.ExtraSigilSlots.Reloaded.HostedImguiBinding", throwOnError: true)!;
    Type graphicsBindingType = contractAssembly
        .GetType("GBFR.OverlayHub.Contracts.OverlayGraphicsBinding", throwOnError: true)!;
    object graphicsBinding = Activator.CreateInstance(
        graphicsBindingType,
        [1, hostNativeLibrary, hostContext.__Instance])!;

    var messages = new List<string>();
    MethodInfo tryBind = bindingType.GetMethod(
        "TryBind",
        BindingFlags.Static | BindingFlags.NonPublic)!;
    bool bound = (bool)tryBind.Invoke(null, [graphicsBinding, (Action<string>)messages.Add])!;
    Assert(bound, "The guest failed to bind to the host cimgui module/context.");

    MethodInfo ensureCurrentContext = bindingType.GetMethod(
        "EnsureCurrentContext",
        BindingFlags.Static | BindingFlags.NonPublic)!;
    Assert((bool)ensureCurrentContext.Invoke(null, null)!,
        "The guest could not restore the shared ImGui context before rendering.");

    Assembly guestDearImgui = guestContext.Assemblies.Single(assembly =>
        assembly.GetName().Name == "DearImguiSharp");
    Type guestImGui = guestDearImgui.GetType("DearImguiSharp.ImGui", throwOnError: true)!;
    object guestIo = guestImGui.GetMethod("GetIO")!.Invoke(null, null) ??
        throw new InvalidOperationException("The guest ImGui.GetIO call returned null after binding.");
    guestIo.GetType().GetProperty("MouseDrawCursor")!.SetValue(guestIo, true);
    Assert(ImGui.GetIO().MouseDrawCursor,
        "The host and guest did not observe the same ImGui IO/context state.");

    Console.WriteLine("HOSTED_IMGUI_CROSS_ALC_BINDING=PASS");
    Console.WriteLine($"HOSTED_IMGUI_BIND_LOG={string.Join(" | ", messages)}");
}
finally
{
    ImGui.GetIO().MouseDrawCursor = false;
    guestContext.Unload();
    NativeLibrary.Free(hostNativeLibrary);
    ImGui.DestroyContext(hostContext);
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

sealed class ProbeLoadContext(string outputDirectory)
    : AssemblyLoadContext("GBFR.ExtraSigilSlots.HostedImguiProbe", isCollectible: true)
{
    protected override Assembly? Load(AssemblyName assemblyName)
    {
        string candidate = Path.Combine(outputDirectory, $"{assemblyName.Name}.dll");
        return File.Exists(candidate) ? LoadFromAssemblyPath(candidate) : null;
    }
}
