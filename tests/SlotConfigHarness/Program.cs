using System.Runtime.InteropServices;
using System.Text;

if (args.Length != 1)
    throw new ArgumentException("Pass the native build output directory.");

string outputDirectory = Path.GetFullPath(args[0]);
string nativeSource = Path.Combine(outputDirectory, "GBFR.ExtraSigilSlots.Native.dll");
(string Label, string? Raw, int Expected)[] cases =
[
    ("missing", null, 8),
    ("empty", string.Empty, 8),
    ("text", "abc", 8),
    ("negative", "-1", 8),
    ("plus", "+8", 8),
    ("decimal", "1.5", 8),
    ("zero", "0", 1),
    ("one", "1", 1),
    ("default", "8", 8),
    ("trimmed", " 8 ", 8),
    ("maximum", "24", 24),
    ("over-maximum", "25", 24),
    ("huge", "999999999999999999999999999", 24),
    ("huge-with-junk", "999999999999x", 8),
    ("overlong-number", new string('0', 1500) + "25", 24),
    ("overlong-junk", new string('0', 1500) + "x", 8),
];

foreach ((string label, string? raw, int expected) in cases)
{
    string testDirectory = Path.Combine(
        Path.GetTempPath(),
        "GBFRES-slot-config-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(testDirectory);
    try
    {
        string nativePath = Path.Combine(testDirectory, "GBFR.ExtraSigilSlots.Native.dll");
        File.Copy(nativeSource, nativePath);
        string iniPath = Path.Combine(testDirectory, "GBFR-ExtraSigilSlotsNumConfig.ini");
        StringBuilder ini = new();
        ini.AppendLine("[Settings]");
        ini.AppendLine("ConfigVersion=1");
        ini.AppendLine("ToggleKey=119");
        ini.AppendLine("AutoApply=1");
        ini.AppendLine("Language=zh-CN");
        if (raw is not null)
            ini.AppendLine("VirtualSlotCount=" + raw);
        File.WriteAllText(iniPath, ini.ToString(), new UTF8Encoding(false));

        int runtimeSlotCount;
        int runtimeSlotCapacity;
        List<string> nativeLogs = [];
        IntPtr library = NativeLibrary.Load(nativePath);
        SetNativeLogCallback? setLogCallback = null;
        NativeLogCallback? nativeLogCallback = null;
        try
        {
            setLogCallback = Marshal.GetDelegateForFunctionPointer<SetNativeLogCallback>(
                NativeLibrary.GetExport(library, "GBFR20_SetLogCallback"));
            nativeLogCallback = message =>
            {
                string? text = Marshal.PtrToStringUTF8(message);
                if (!string.IsNullOrEmpty(text))
                    nativeLogs.Add(text);
            };
            setLogCallback(Marshal.GetFunctionPointerForDelegate(nativeLogCallback));
            InitializeNative initialize = Marshal.GetDelegateForFunctionPointer<InitializeNative>(
                NativeLibrary.GetExport(library, "GBFR20_Initialize"));
            _ = initialize();
            GetNativeState getState = Marshal.GetDelegateForFunctionPointer<GetNativeState>(
                NativeLibrary.GetExport(library, "GBFR20_GetState"));
            IntPtr state = Marshal.AllocHGlobal(276);
            try
            {
                if (getState(state, 276) == 0)
                    throw new InvalidOperationException("Native runtime state was unavailable.");
                runtimeSlotCount = Marshal.ReadInt32(state, 268);
                runtimeSlotCapacity = Marshal.ReadInt32(state, 272);
            }
            finally
            {
                Marshal.FreeHGlobal(state);
            }
        }
        finally
        {
            setLogCallback?.Invoke(IntPtr.Zero);
            GC.KeepAlive(nativeLogCallback);
            NativeLibrary.Free(library);
        }

        if (label == "missing")
        {
            string[] requiredMarkers =
            [
                "Startup phase=native-initialize state=begin.",
                "Startup phase=settings-and-selections state=complete",
                "Startup phase=compatibility-table state=failed",
            ];
            foreach (string marker in requiredMarkers)
            {
                if (!nativeLogs.Any(line => line.Contains(marker, StringComparison.Ordinal)))
                    throw new InvalidOperationException($"Missing native startup marker: {marker}");
            }
            if (nativeLogs.Any(line => line.Contains("SHA-256", StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Startup unexpectedly performed SHA-256 diagnostics.");
            Console.WriteLine("STARTUP_PHASE_CALLBACK=True");
            Console.WriteLine("NATIVE_STARTUP_FULL_EXE_HASH_SCAN=False");
        }

        int actual = ReadIniInt(iniPath, "VirtualSlotCount");
        int configVersion = ReadIniInt(iniPath, "ConfigVersion");
        if (actual != expected || configVersion != 2 ||
            runtimeSlotCount != expected || runtimeSlotCapacity != 24)
        {
            throw new InvalidOperationException(
                $"{label}: expected slot count {expected}/version 2/capacity 24, " +
                $"got INI {actual}/{configVersion}, runtime {runtimeSlotCount}/{runtimeSlotCapacity}.");
        }
        Console.WriteLine($"CASE={label} NORMALIZED={actual}");
    }
    finally
    {
        Directory.Delete(testDirectory, recursive: true);
    }
}

string shrinkDirectory = Path.Combine(
    Path.GetTempPath(),
    "GBFRES-slot-shrink-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(shrinkDirectory);
try
{
    string nativePath = Path.Combine(shrinkDirectory, "GBFR.ExtraSigilSlots.Native.dll");
    File.Copy(nativeSource, nativePath);
    string iniPath = Path.Combine(shrinkDirectory, "GBFR-ExtraSigilSlotsNumConfig.ini");
    File.WriteAllText(
        iniPath,
        """
        [Settings]
        ConfigVersion=2
        ToggleKey=119
        AutoApply=1
        Language=zh-CN
        VirtualSlotCount=3

        [Character_2A26B1B2]
        Slots=00000001,00000002,00000003,00000004,00000005
        """,
        new UTF8Encoding(false));
    IntPtr library = NativeLibrary.Load(nativePath);
    try
    {
        InitializeNative initialize = Marshal.GetDelegateForFunctionPointer<InitializeNative>(
            NativeLibrary.GetExport(library, "GBFR20_Initialize"));
        _ = initialize();
    }
    finally
    {
        NativeLibrary.Free(library);
    }
    string slots = ReadIniValue(iniPath, "Slots");
    if (!string.Equals(slots, "00000001,00000002,00000003", StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Shrink cleanup did not remove inactive selections: {slots}");
    Console.WriteLine("SHRINK_ACTIVE_SLOTS=3");
    Console.WriteLine("SHRINK_INACTIVE_SELECTIONS_CLEARED=True");
}
finally
{
    Directory.Delete(shrinkDirectory, recursive: true);
}

Console.WriteLine("SLOT_CONFIG_TEST=PASS");

static int ReadIniInt(string path, string key)
{
    string text = ReadIniValue(path, key);
    if (!int.TryParse(text, out int value))
        throw new InvalidOperationException($"Invalid normalized key {key}.");
    return value;
}

static string ReadIniValue(string path, string key)
{
    string prefix = key + "=";
    string? line = File.ReadLines(path)
        .FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    if (line is null)
        throw new InvalidOperationException($"Missing normalized key {key}.");
    return line[prefix.Length..];
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int InitializeNative();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void SetNativeLogCallback(IntPtr callback);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void NativeLogCallback(IntPtr message);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int GetNativeState(IntPtr state, uint stateSize);
