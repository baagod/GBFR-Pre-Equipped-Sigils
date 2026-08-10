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
setToggleKey.Invoke(null, [0x77]);
Assert(!(bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]) ?? true),
    "Refreshing the same configured key must not release its held latch.");

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

Assert((bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]) ?? false),
    "A keydown before an inactive WM_ACTIVATE must queue normally.");
Assert((bool)(consume.Invoke(null, null) ?? false),
    "The pre-activation toggle must be consumed.");
observe.Invoke(null, [0x0006u, IntPtr.Zero, IntPtr.Zero]);
Assert((bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]) ?? false),
    "Inactive WM_ACTIVATE must release the toggle-key latch.");
Assert((bool)(consume.Invoke(null, null) ?? false),
    "The post-activation toggle must be consumed.");
observe.Invoke(null, [0x0101u, new IntPtr(0x77), IntPtr.Zero]);

Assert((bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]) ?? false),
    "A keydown before WM_CANCELMODE must queue normally.");
Assert((bool)(consume.Invoke(null, null) ?? false),
    "The pre-cancel toggle must be consumed.");
observe.Invoke(null, [0x001Fu, IntPtr.Zero, IntPtr.Zero]);
Assert((bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]) ?? false),
    "WM_CANCELMODE must release the toggle-key latch.");
Assert((bool)(consume.Invoke(null, null) ?? false),
    "The post-cancel toggle must be consumed.");
observe.Invoke(null, [0x0101u, new IntPtr(0x77), IntPtr.Zero]);

forceClosed.Invoke(null, null);
Assert((bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]) ?? false),
    "A closed frontend must queue a foreground toggle.");
observe.Invoke(null, [0x0008u, IntPtr.Zero, IntPtr.Zero]);
Assert(!(bool)(consume.Invoke(null, null) ?? true),
    "Losing focus while closed must cancel an unconsumed background-open toggle.");
Assert(!ReadBool(shouldRender),
    "A canceled background-open toggle must return the frontend to sleep.");
observe.Invoke(null, [0x0101u, new IntPtr(0x77), IntPtr.Zero]);

setOpen.Invoke(null, [true]);
Assert((bool)(observe.Invoke(null, [0x0100u, new IntPtr(0x77), IntPtr.Zero]) ?? false),
    "An open frontend must queue its close toggle.");
observe.Invoke(null, [0x0008u, IntPtr.Zero, IntPtr.Zero]);
Assert((bool)(consume.Invoke(null, null) ?? false),
    "Losing focus while open must preserve an already queued close toggle.");
setOpen.Invoke(null, [false]);
observe.Invoke(null, [0x0101u, new IntPtr(0x77), IntPtr.Zero]);

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
MethodInfo synchronizePhysicalState = GetStaticMethod(
    buttonTrackerType,
    "SynchronizePhysicalState");
MethodInfo buildPressedButtons = GetStaticMethod(buttonTrackerType, "BuildPressedButtons");
MethodInfo synchronizeState = GetStaticMethod(buttonTrackerType, "SynchronizeState");
MethodInfo requiresPhysicalStateSynchronization = GetStaticMethod(
    buttonTrackerType,
    "RequiresPhysicalStateSynchronization");
MethodInfo observeMouseMessage = GetStaticMethod(buttonTrackerType, "ObserveWindowMessage");
PropertyInfo pressedButtons = GetStaticProperty(buttonTrackerType, "PressedButtons");
PropertyInfo buttonEventSequence = GetStaticProperty(buttonTrackerType, "ButtonEventSequence");

resetButtons.Invoke(null, null);
long sequenceBeforeSynchronization = ReadLong(buttonEventSequence);
long synchronizedSequence = (long)(synchronizePhysicalState.Invoke(null, null) ?? 0L);
Assert(synchronizedSequence == sequenceBeforeSynchronization + 1,
    "Physical mouse synchronization must return its exact new event boundary.");
Assert(synchronizedSequence == ReadLong(buttonEventSequence),
    "The returned physical mouse boundary must match the published sequence.");
Func<int, bool> simulatedPhysicalState = virtualKey => virtualKey is 0x01 or 0x05;
uint builtPressedButtons = (uint)(buildPressedButtons.Invoke(
    null,
    [simulatedPhysicalState]) ?? 0u);
Assert(builtPressedButtons == 9u,
    "Physical-state sampling must map left button and XBUTTON1 to the tracker mask.");
long knownStateSequence = (long)(synchronizeState.Invoke(null, [9u]) ?? 0L);
Assert(ReadUInt(pressedButtons) == 9u,
    "Known mouse synchronization must preserve held left and XBUTTON1 state.");
Assert(knownStateSequence == ReadLong(buttonEventSequence),
    "Known mouse synchronization must publish one atomic event boundary.");
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
observeMouseMessage.Invoke(null, [0x00ABu, new IntPtr(1L << 16)]);
Assert(ReadUInt(pressedButtons) == 8u, "Non-client XBUTTON1 down must be tracked.");
observeMouseMessage.Invoke(null, [0x00ACu, new IntPtr(1L << 16)]);
Assert(ReadUInt(pressedButtons) == 0u, "Non-client XBUTTON1 up must be tracked.");
observeMouseMessage.Invoke(null, [0x00ADu, new IntPtr(1L << 16)]);
Assert(ReadUInt(pressedButtons) == 8u, "Non-client XBUTTON1 double-click must be tracked.");
observeMouseMessage.Invoke(null, [0x00ACu, new IntPtr(1L << 16)]);
Assert(ReadUInt(pressedButtons) == 0u,
    "Non-client XBUTTON1 up must release a tracked double-click.");
long sequenceBeforeDoubleClick = ReadLong(buttonEventSequence);
observeMouseMessage.Invoke(null, [0x0203u, IntPtr.Zero]);
Assert(ReadUInt(pressedButtons) == 1u, "Left-button double-click must be tracked as held.");
Assert(ReadLong(buttonEventSequence) == sequenceBeforeDoubleClick + 1,
    "A double-click message must advance the mouse event boundary.");
observeMouseMessage.Invoke(null, [0x0202u, IntPtr.Zero]);
Assert(ReadUInt(pressedButtons) == 0u, "Left-button up must release a tracked double-click.");

(uint Message, IntPtr WParam, string Name)[] synchronizationBoundaries =
[
    (0x0006u, new IntPtr(1), "active WM_ACTIVATE"),
    (0x0007u, IntPtr.Zero, "WM_SETFOCUS"),
    (0x0008u, IntPtr.Zero, "WM_KILLFOCUS"),
    (0x001Cu, new IntPtr(1), "active WM_ACTIVATEAPP"),
    (0x001Cu, IntPtr.Zero, "inactive WM_ACTIVATEAPP"),
    (0x001Fu, IntPtr.Zero, "WM_CANCELMODE"),
    (0x0006u, IntPtr.Zero, "inactive WM_ACTIVATE"),
    (0x0215u, IntPtr.Zero, "WM_CAPTURECHANGED"),
];
foreach ((uint message, IntPtr wParam, string name) in synchronizationBoundaries)
{
    Assert((bool)(requiresPhysicalStateSynchronization.Invoke(
            null,
            [message, wParam]) ?? false),
        $"{name} must be a physical mouse synchronization boundary.");
    synchronizeState.Invoke(null, [1u]);
    long sequenceBeforeBoundary = ReadLong(buttonEventSequence);
    observeMouseMessage.Invoke(null, [message, wParam]);
    Assert(ReadLong(buttonEventSequence) == sequenceBeforeBoundary + 1,
        $"{name} must publish a fresh physical mouse snapshot.");
}
Assert(!(bool)(requiresPhysicalStateSynchronization.Invoke(
        null,
        [0x000Fu, IntPtr.Zero]) ?? true),
    "Unrelated window messages must not reset the mouse boundary.");
resetButtons.Invoke(null, null);

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
openMouseGate.Invoke(mouseGate, [ReadLong(buttonEventSequence)]);
Assert(!ReadInstanceBool(mouseGate, mouseGateArmed),
    "A popup boundary must be able to rearm an already-open interaction gate.");
observeButtons.Invoke(mouseGate, [0u, ReadLong(buttonEventSequence)]);
observeButtons.Invoke(mouseGate, [0u, ReadLong(buttonEventSequence)]);
Assert(ReadInstanceBool(mouseGate, mouseGateArmed),
    "Two clean frames after a popup boundary must rearm pointer interaction.");
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
