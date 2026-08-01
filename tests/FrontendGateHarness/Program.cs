using System.Reflection;
using System.Runtime.Loader;

if (args.Length != 1)
    throw new ArgumentException("Pass the managed build output directory.");

string outputDirectory = Path.GetFullPath(args[0]);
string assemblyPath = Path.Combine(outputDirectory, "GBFR.ExtraSigilSlots.Reloaded.dll");
PluginLoadContext context = new(assemblyPath);
Assembly assembly = context.LoadFromAssemblyPath(assemblyPath);
Type gate = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.FrontendOverlayGate",
    throwOnError: true)!;

MethodInfo forceClosed = GetMethod("ForceClosed");
MethodInfo setOpen = GetMethod("SetOpen");
MethodInfo setToggleKey = GetMethod("SetToggleKey");
MethodInfo observe = GetMethod("ObserveWindowMessage");
MethodInfo consume = GetMethod("ConsumeToggleRequest");
PropertyInfo shouldRender = GetProperty("ShouldRenderFrame");
PropertyInfo isOpen = GetProperty("IsOpen");
PropertyInfo currentKey = GetProperty("CurrentToggleKey");

forceClosed.Invoke(null, null);
setToggleKey.Invoke(null, [0x77]);
Assert(!ReadBool(shouldRender), "A closed frontend must not render.");
Assert(!ReadBool(isOpen), "The frontend must start closed.");

bool queued = (bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]) ?? false);
Assert(queued, "The first F8 keydown must queue a toggle.");
Assert(ReadBool(shouldRender), "A pending toggle must wake one frontend frame.");
Assert(!(bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]) ?? true),
    "A duplicate keydown without key-up must not queue another toggle.");

bool repeated = (bool)(observe.Invoke(
    null,
    [0x0100u, new IntPtr(0x77), new IntPtr(1L << 30)]) ?? false);
Assert(!repeated, "An autorepeated keydown must not queue another toggle.");
Assert((bool)(consume.Invoke(null, null) ?? false), "The queued toggle must be consumed.");
setOpen.Invoke(null, [true]);
Assert(ReadBool(isOpen) && ReadBool(shouldRender), "An open frontend must render.");
observe.Invoke(null, [0x0101u, new IntPtr(0x77), IntPtr.Zero]);

observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]);
Assert((bool)(consume.Invoke(null, null) ?? false), "The close toggle must be consumed.");
setOpen.Invoke(null, [false]);
Assert(!ReadBool(shouldRender), "Closing must put the frontend back to sleep.");

setToggleKey.Invoke(null, [0]);
Assert((int)(currentKey.GetValue(null) ?? 0) == 0x77, "Invalid keys must fall back to F8.");
Assert(!(bool)(observe.Invoke(null, [0x0101u, new IntPtr(0x77), IntPtr.Zero]) ?? true),
    "Key-up must not queue a toggle.");

forceClosed.Invoke(null, null);
setToggleKey.Invoke(null, [0x75]);
Assert((int)(currentKey.GetValue(null) ?? 0) == 0x75,
    "A Reloaded-II hotkey change must update the frontend gate.");
Assert(!(bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]) ?? true),
    "F8 must stop toggling after the hotkey changes to F6.");
Assert((bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x75), IntPtr.Zero]) ?? false),
    "The configured F6 key must queue a toggle.");
Assert((bool)(consume.Invoke(null, null) ?? false),
    "The configured-key toggle must be consumed.");
forceClosed.Invoke(null, null);
setToggleKey.Invoke(null, [0x77]);

observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]);
observe.Invoke(null, [0x0101u, new IntPtr(0x77), IntPtr.Zero]);
observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]);
Assert(!(bool)(consume.Invoke(null, null) ?? true),
    "Two physical toggles before a frame must cancel by parity.");
Assert(!ReadBool(shouldRender), "An even toggle count must return the frontend to sleep.");

Console.WriteLine("FRONTEND_EVENT_GATE=PASS");

Type buttonTrackerType = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.MouseButtonStateTracker",
    throwOnError: true)!;
MethodInfo resetButtons = GetStaticMethod(buttonTrackerType, "Reset");
MethodInfo observeMouseMessage = GetStaticMethod(buttonTrackerType, "ObserveWindowMessage");
PropertyInfo pressedButtons = GetStaticProperty(buttonTrackerType, "PressedButtons");
PropertyInfo buttonEventSequence = GetStaticProperty(buttonTrackerType, "ButtonEventSequence");

resetButtons.Invoke(null, null);
observeMouseMessage.Invoke(null, [0x0201u, IntPtr.Zero]);
Assert(ReadUInt(pressedButtons) == 1u, "Left-button down must be tracked.");
observeMouseMessage.Invoke(null, [0x0204u, IntPtr.Zero]);
Assert(ReadUInt(pressedButtons) == 3u, "Multiple held mouse buttons must be tracked.");
observeMouseMessage.Invoke(null, [0x0202u, IntPtr.Zero]);
Assert(ReadUInt(pressedButtons) == 2u, "Left-button up must preserve other buttons.");
observeMouseMessage.Invoke(null, [0x0205u, IntPtr.Zero]);
Assert(ReadUInt(pressedButtons) == 0u, "Button-up messages must clear the tracker.");
observeMouseMessage.Invoke(null, [0x020Bu, new IntPtr(1L << 16)]);
Assert(ReadUInt(pressedButtons) == 8u, "XBUTTON1 down must be tracked.");
observeMouseMessage.Invoke(null, [0x020Cu, new IntPtr(1L << 16)]);
Assert(ReadUInt(pressedButtons) == 0u, "XBUTTON1 up must be tracked.");

Type mouseGateType = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.MouseInteractionGate",
    throwOnError: true)!;
object mouseGate = Activator.CreateInstance(mouseGateType, nonPublic: true) ??
    throw new InvalidOperationException("Mouse interaction gate could not be created.");
MethodInfo openMouseGate = GetInstanceMethod(mouseGateType, "Open");
MethodInfo closeMouseGate = GetInstanceMethod(mouseGateType, "Close");
MethodInfo observeButtons = GetInstanceMethod(mouseGateType, "Observe");
PropertyInfo mouseGateArmed = GetInstanceProperty(mouseGateType, "IsArmed");

openMouseGate.Invoke(mouseGate, [ReadLong(buttonEventSequence)]);
Assert(!ReadInstanceBool(mouseGate, mouseGateArmed),
    "Opening must disarm pointer interaction immediately.");
observeButtons.Invoke(mouseGate, [1u, ReadLong(buttonEventSequence)]);
Assert(!ReadInstanceBool(mouseGate, mouseGateArmed),
    "A held mouse button must keep pointer interaction disarmed.");
observeButtons.Invoke(mouseGate, [0u, ReadLong(buttonEventSequence)]);
Assert(!ReadInstanceBool(mouseGate, mouseGateArmed),
    "The release frame itself must remain non-interactive.");
observeButtons.Invoke(mouseGate, [2u, ReadLong(buttonEventSequence)]);
Assert(!ReadInstanceBool(mouseGate, mouseGateArmed),
    "A button pressed before the clean boundary must restart arming.");
observeButtons.Invoke(mouseGate, [0u, ReadLong(buttonEventSequence)]);
Assert(!ReadInstanceBool(mouseGate, mouseGateArmed),
    "The restarted release frame must remain non-interactive.");
observeButtons.Invoke(mouseGate, [0u, ReadLong(buttonEventSequence)]);
Assert(ReadInstanceBool(mouseGate, mouseGateArmed),
    "Two clean released frames must arm pointer interaction.");
closeMouseGate.Invoke(mouseGate, null);
Assert(!ReadInstanceBool(mouseGate, mouseGateArmed),
    "Closing must disarm and reset pointer interaction.");
openMouseGate.Invoke(mouseGate, [ReadLong(buttonEventSequence)]);
observeButtons.Invoke(mouseGate, [0u, ReadLong(buttonEventSequence)]);
Assert(!ReadInstanceBool(mouseGate, mouseGateArmed),
    "Reopening must not inherit the previous armed state.");

observeMouseMessage.Invoke(null, [0x0201u, IntPtr.Zero]);
observeMouseMessage.Invoke(null, [0x0202u, IntPtr.Zero]);
Assert(ReadUInt(pressedButtons) == 0u,
    "A complete click between frames must finish with no held button.");
observeButtons.Invoke(mouseGate, [0u, ReadLong(buttonEventSequence)]);
Assert(!ReadInstanceBool(mouseGate, mouseGateArmed),
    "A complete click between frames must restart the clean-input boundary.");
observeButtons.Invoke(mouseGate, [0u, ReadLong(buttonEventSequence)]);
Assert(!ReadInstanceBool(mouseGate, mouseGateArmed),
    "The first stable frame after a click must remain non-interactive.");
observeButtons.Invoke(mouseGate, [0u, ReadLong(buttonEventSequence)]);
Assert(ReadInstanceBool(mouseGate, mouseGateArmed),
    "Two stable frames after the last mouse event must arm interaction.");

resetButtons.Invoke(null, null);
Console.WriteLine("MOUSE_INTERACTION_LIFECYCLE=PASS");

MethodInfo GetMethod(string name) => gate.GetMethod(
    name,
    BindingFlags.NonPublic | BindingFlags.Static) ??
    throw new MissingMethodException(gate.FullName, name);

PropertyInfo GetProperty(string name) => gate.GetProperty(
    name,
    BindingFlags.NonPublic | BindingFlags.Static) ??
    throw new MissingMemberException(gate.FullName, name);

static bool ReadBool(PropertyInfo property) => (bool)(property.GetValue(null) ?? false);

static uint ReadUInt(PropertyInfo property) => (uint)(property.GetValue(null) ?? 0u);

static long ReadLong(PropertyInfo property) => (long)(property.GetValue(null) ?? 0L);

static bool ReadInstanceBool(object instance, PropertyInfo property) =>
    (bool)(property.GetValue(instance) ?? false);

static MethodInfo GetStaticMethod(Type type, string name) => type.GetMethod(
    name,
    BindingFlags.NonPublic | BindingFlags.Static) ??
    throw new MissingMethodException(type.FullName, name);

static PropertyInfo GetStaticProperty(Type type, string name) => type.GetProperty(
    name,
    BindingFlags.NonPublic | BindingFlags.Static) ??
    throw new MissingMemberException(type.FullName, name);

static MethodInfo GetInstanceMethod(Type type, string name) => type.GetMethod(
    name,
    BindingFlags.NonPublic | BindingFlags.Instance) ??
    throw new MissingMethodException(type.FullName, name);

static PropertyInfo GetInstanceProperty(Type type, string name) => type.GetProperty(
    name,
    BindingFlags.NonPublic | BindingFlags.Instance) ??
    throw new MissingMemberException(type.FullName, name);

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
