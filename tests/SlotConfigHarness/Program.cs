using System.Runtime.InteropServices;
using System.Text;
using GBFR.ExtraSigilSlots.Reloaded;

if (args.Length != 1)
    throw new ArgumentException("Pass the native build output directory.");

string outputDirectory = Path.GetFullPath(args[0]);
string nativeSource = Path.Combine(outputDirectory, "GBFR.ExtraSigilSlots.Native.dll");
const string ConfigFileName = "GBFR-ExtraSigilSlotsNumConfig.ini";
const string PendingFileName = "GBFR-ExtraSigilSlotsNumConfig.pending";
byte[] defaultConfig = Encoding.UTF8.GetBytes(
    "[Settings]\r\n" +
    "ConfigVersion=2\r\n" +
    "ToggleKey=119\r\n" +
    "ShowEquipped=0\r\n" +
    "AutoApply=1\r\n" +
    "Language=zh-CN\r\n" +
    "VirtualSlotCount=8\r\n");

RunMissingCase();

(string Label, string Text, int ExpectedSlotCount, uint[] ExpectedSelection)[] validCases =
[
    ("default", Encoding.UTF8.GetString(defaultConfig), 8, []),
    ("minimum-with-selection",
        "[Settings]\r\n" +
        "ConfigVersion = 2\r\n" +
        "ToggleKey = 118\r\n" +
        "ShowEquipped = 1\r\n" +
        "AutoApply = 1\r\n" +
        "Language = en\r\n" +
        "VirtualSlotCount = 1\r\n" +
        "\r\n" +
        "[Character_2A26B1B2]\r\n" +
        "Slots = 00000001\r\n",
        1,
        [1]),
    ("maximum-case-insensitive-and-forward-compatible",
        "; Preserve comments and unknown future keys byte-for-byte.\r\n" +
        "[settings]\r\n" +
        "ConfigVersion=2\r\n" +
        "ToggleKey=119\r\n" +
        "ShowEquipped=0\r\n" +
        "AutoApply=1\r\n" +
        "Language=zh-CN\r\n" +
        "VirtualSlotCount=24\r\n" +
        "FutureSetting=kept\r\n" +
        "\r\n" +
        "[character_2a26b1b2]\r\n" +
        "Slots=00000001, 00000002\r\n" +
        "FutureCharacterValue=kept\r\n" +
        "\r\n" +
        "[FutureSection]\r\n" +
        "FutureValue=kept\r\n",
        24,
        [1, 2]),
    ("inactive-selections-preserved",
        "[Settings]\r\n" +
        "ConfigVersion=2\r\n" +
        "ToggleKey=119\r\n" +
        "ShowEquipped=0\r\n" +
        "AutoApply=1\r\n" +
        "Language=zh-CN\r\n" +
        "VirtualSlotCount=3\r\n" +
        "\r\n" +
        "[Character_2A26B1B2]\r\n" +
        "Slots=00000001,00000002,00000003,00000004,00000005\r\n",
        3,
        [1, 2, 3, 4, 5]),
];

foreach ((string label, string text, int expectedSlotCount, uint[] expectedSelection) in validCases)
    RunValidCase(label, Encoding.UTF8.GetBytes(text), expectedSlotCount, expectedSelection);

(string Label, byte[] Bytes)[] invalidCases =
[
    ("empty", []),
    ("missing-setting", Config("VirtualSlotCount=8", includeLanguage: false)),
    ("old-version", Config("VirtualSlotCount=8", configVersion: "1")),
    ("bad-toggle-low", Config("VirtualSlotCount=8", toggleKey: "0")),
    ("bad-toggle-high", Config("VirtualSlotCount=8", toggleKey: "256")),
    ("bad-show-equipped", Config("VirtualSlotCount=8", showEquipped: "2")),
    ("disabled-auto-apply", Config("VirtualSlotCount=8", autoApply: "0")),
    ("bad-language", Config("VirtualSlotCount=8", language: "fr")),
    ("zero-slots", Config("VirtualSlotCount=0")),
    ("over-maximum", Config("VirtualSlotCount=25")),
    ("negative", Config("VirtualSlotCount=-1")),
    ("decimal", Config("VirtualSlotCount=1.5")),
    ("junk", Config("VirtualSlotCount=abc")),
    ("overflow", Config("VirtualSlotCount=999999999999999999999999999")),
    ("duplicate-setting", Config("VirtualSlotCount=8\r\nVirtualSlotCount=8")),
    ("character-missing-slots", Append(Config("VirtualSlotCount=8"),
        "\r\n[Character_2A26B1B2]\r\nFutureValue=1\r\n")),
    ("malformed-slot", Append(Config("VirtualSlotCount=8"),
        "\r\n[Character_2A26B1B2]\r\nSlots=0000000G\r\n")),
    ("duplicate-physical-slot", Append(Config("VirtualSlotCount=8"),
        "\r\n[Character_2A26B1B2]\r\nSlots=00000001\r\n" +
        "[Character_2A26B1B3]\r\nSlots=00000001\r\n")),
    ("nul-byte", [.. Config("VirtualSlotCount=8"), 0]),
];

foreach ((string label, byte[] bytes) in invalidCases)
    RunInvalidCase(label, bytes);

RunInputNormalizationCases();
RunPendingResizeCases();

Console.WriteLine("SLOT_CONFIG_TEST=PASS");

void RunInputNormalizationCases()
{
    (string Input, int Expected)[] cases =
    [
        ("1", 1),
        ("24", 24),
        ("", 1),
        ("0", 1),
        ("25", 1),
        ("-1", 1),
        ("1.5", 1),
        ("abc", 1),
        (" 8 ", 1),
        ("999999999999999999999999", 1),
    ];
    foreach ((string input, int expected) in cases)
    {
        int actual = VirtualSlotCountInput.Normalize(input, 24);
        if (actual != expected)
            throw new InvalidOperationException(
                $"input '{input}' expected {expected}, got {actual}.");
    }
    Console.WriteLine("SLOT_COUNT_INPUT_NORMALIZATION=True");
}

void RunMissingCase()
{
    string testDirectory = CreateTestDirectory("missing");
    try
    {
        NativeResult result = RunNative(testDirectory);
        string iniPath = Path.Combine(testDirectory, ConfigFileName);
        AssertBytesEqual(defaultConfig, File.ReadAllBytes(iniPath), "missing: generated default");
        AssertNoBackups(testDirectory, "missing");
        AssertRuntime(result, 8, "missing");

        string[] requiredMarkers =
        [
            "NumConfig was missing and a complete default INI was created.",
            "Startup phase=native-initialize state=begin.",
            "Startup phase=settings-and-selections state=complete",
            "Startup phase=executable-validation state=failed",
        ];
        foreach (string marker in requiredMarkers)
        {
            if (!result.Logs.Any(line => line.Contains(marker, StringComparison.Ordinal)))
                throw new InvalidOperationException($"Missing native startup marker: {marker}");
        }
        if (result.Logs.Any(line => line.Contains("SHA-256", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Startup unexpectedly performed SHA-256 diagnostics.");
        Console.WriteLine("CASE=missing CREATED_DEFAULT=True BACKUP=False");
        Console.WriteLine("STARTUP_PHASE_CALLBACK=True");
        Console.WriteLine("NATIVE_STARTUP_FULL_EXE_HASH_SCAN=False");
    }
    finally
    {
        Directory.Delete(testDirectory, recursive: true);
    }
}

void RunValidCase(
    string label,
    byte[] original,
    int expectedSlotCount,
    uint[] expectedSelection)
{
    string testDirectory = CreateTestDirectory(label);
    try
    {
        string iniPath = Path.Combine(testDirectory, ConfigFileName);
        File.WriteAllBytes(iniPath, original);
        NativeResult result = RunNative(testDirectory);
        AssertBytesEqual(original, File.ReadAllBytes(iniPath), $"{label}: valid file changed");
        AssertNoBackups(testDirectory, label);
        AssertRuntime(result, expectedSlotCount, label);
        for (int index = 0; index < expectedSelection.Length; ++index)
        {
            if (result.Selection[index] != expectedSelection[index])
            {
                throw new InvalidOperationException(
                    $"{label}: selection {index} expected {expectedSelection[index]:X8}, " +
                    $"got {result.Selection[index]:X8}.");
            }
        }
        Console.WriteLine($"CASE={label} VALID_UNCHANGED=True BACKUP=False");
    }
    finally
    {
        Directory.Delete(testDirectory, recursive: true);
    }
}

void RunInvalidCase(string label, byte[] original)
{
    string testDirectory = CreateTestDirectory(label);
    try
    {
        string iniPath = Path.Combine(testDirectory, ConfigFileName);
        File.WriteAllBytes(iniPath, original);
        NativeResult result = RunNative(testDirectory);
        AssertBytesEqual(defaultConfig, File.ReadAllBytes(iniPath), $"{label}: replacement default");
        AssertRuntime(result, 8, label);

        string[] backups = Directory.GetFiles(
            testDirectory,
            ConfigFileName + ".invalid-*.bak*",
            SearchOption.TopDirectoryOnly);
        if (backups.Length != 1)
            throw new InvalidOperationException($"{label}: expected one backup, got {backups.Length}.");
        AssertBytesEqual(original, File.ReadAllBytes(backups[0]), $"{label}: backup bytes");
        if (!result.Logs.Any(line => line.Contains(
                "Invalid NumConfig was backed up", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException($"{label}: replacement log was not emitted.");
        }
        Console.WriteLine($"CASE={label} BACKUP_EXACT=True REPLACED_DEFAULT=True");
    }
    finally
    {
        Directory.Delete(testDirectory, recursive: true);
    }
}

void RunPendingResizeCases()
{
    byte[] shrinkOriginal = Encoding.UTF8.GetBytes(
        "[Settings]\r\n" +
        "ConfigVersion=2\r\n" +
        "ToggleKey=119\r\n" +
        "ShowEquipped=0\r\n" +
        "AutoApply=1\r\n" +
        "Language=zh-CN\r\n" +
        "VirtualSlotCount=12\r\n\r\n" +
        "[Character_2A26B1B2]\r\n" +
        "Slots=00000001,00000002,00000003,00000004,00000005,00000006," +
        "00000007,00000008,00000009,0000000A,0000000B,0000000C\r\n");
    RunPendingResizeCase(
        "pending-shrink",
        shrinkOriginal,
        8,
        [1, 2, 3, 4, 5, 6, 7, 8],
        "Slots=00000001,00000002,00000003,00000004,00000005,00000006,00000007,00000008",
        expectBackup: true,
        expectConfigUnchanged: false);

    byte[] expandOriginal = Encoding.UTF8.GetBytes(
        "[Settings]\r\n" +
        "ConfigVersion=2\r\n" +
        "ToggleKey=119\r\n" +
        "ShowEquipped=0\r\n" +
        "AutoApply=1\r\n" +
        "Language=zh-CN\r\n" +
        "VirtualSlotCount=8\r\n\r\n" +
        "[Character_2A26B1B2]\r\n" +
        "Slots=00000001,00000002,00000003,00000004,00000005,00000006," +
        "00000007,00000008,00000009,0000000A,0000000B,0000000C\r\n");
    RunPendingResizeCase(
        "pending-expand",
        expandOriginal,
        12,
        [1, 2, 3, 4, 5, 6, 7, 8, 0, 0, 0, 0],
        "Slots=00000001,00000002,00000003,00000004,00000005,00000006," +
        "00000007,00000008,00000000,00000000,00000000,00000000",
        expectBackup: true,
        expectConfigUnchanged: false);

    RunPendingResizeCase(
        "pending-no-op",
        defaultConfig,
        8,
        [],
        "VirtualSlotCount=8",
        expectBackup: false,
        expectConfigUnchanged: true);

    RunInvalidPendingCase();
}

void RunInvalidPendingCase()
{
    string testDirectory = CreateTestDirectory("pending-invalid");
    try
    {
        string iniPath = Path.Combine(testDirectory, ConfigFileName);
        string pendingPath = Path.Combine(testDirectory, PendingFileName);
        byte[] invalidPending = Encoding.UTF8.GetBytes("VirtualSlotCount=abc\r\n");
        File.WriteAllBytes(iniPath, defaultConfig);
        File.WriteAllBytes(pendingPath, invalidPending);

        NativeResult result = RunNative(testDirectory);
        AssertRuntime(result, 8, "pending-invalid");
        AssertBytesEqual(defaultConfig, File.ReadAllBytes(iniPath), "pending-invalid: config");
        AssertBytesEqual(invalidPending, File.ReadAllBytes(pendingPath), "pending-invalid: request");
        if (!result.Logs.Any(line => line.Contains(
                "pending virtual-slot-count request is invalid", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("pending-invalid: diagnostic log was missing.");
        }
        Console.WriteLine("CASE=pending-invalid CONFIG_UNCHANGED=True REQUEST_PRESERVED=True");
    }
    finally
    {
        Directory.Delete(testDirectory, recursive: true);
    }
}

void RunPendingResizeCase(
    string label,
    byte[] original,
    int targetSlotCount,
    uint[] expectedSelection,
    string expectedConfigFragment,
    bool expectBackup,
    bool expectConfigUnchanged)
{
    string testDirectory = CreateTestDirectory(label);
    try
    {
        string iniPath = Path.Combine(testDirectory, ConfigFileName);
        string pendingPath = Path.Combine(testDirectory, PendingFileName);
        File.WriteAllBytes(iniPath, original);
        File.WriteAllText(
            pendingPath,
            $"VirtualSlotCount={targetSlotCount}\r\n",
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        NativeResult result = RunNative(testDirectory);
        AssertRuntime(result, targetSlotCount, label);
        if (File.Exists(pendingPath))
            throw new InvalidOperationException($"{label}: completed pending request was not removed.");
        byte[] actualConfig = File.ReadAllBytes(iniPath);
        if (expectConfigUnchanged)
            AssertBytesEqual(original, actualConfig, $"{label}: no-op config");
        string actualText = Encoding.UTF8.GetString(actualConfig);
        if (!actualText.Contains(expectedConfigFragment, StringComparison.Ordinal))
            throw new InvalidOperationException($"{label}: rewritten config fragment was missing.");
        for (int index = 0; index < expectedSelection.Length; ++index)
        {
            if (result.Selection[index] != expectedSelection[index])
            {
                throw new InvalidOperationException(
                    $"{label}: selection {index} expected {expectedSelection[index]:X8}, " +
                    $"got {result.Selection[index]:X8}.");
            }
        }
        for (int index = expectedSelection.Length; index < result.Selection.Length; ++index)
        {
            if (result.Selection[index] != 0)
                throw new InvalidOperationException($"{label}: inactive selection {index} was not cleared.");
        }

        string[] backups = Directory.GetFiles(
            testDirectory,
            ConfigFileName + ".resize-*.bak*",
            SearchOption.TopDirectoryOnly);
        if (backups.Length != (expectBackup ? 1 : 0))
            throw new InvalidOperationException($"{label}: unexpected resize backup count {backups.Length}.");
        if (expectBackup)
            AssertBytesEqual(original, File.ReadAllBytes(backups[0]), $"{label}: resize backup bytes");
        Console.WriteLine(
            $"CASE={label} TARGET={targetSlotCount} BACKUP={expectBackup} TRANSACTION=True");
    }
    finally
    {
        Directory.Delete(testDirectory, recursive: true);
    }
}

string CreateTestDirectory(string label)
{
    string directory = Path.Combine(
        Path.GetTempPath(),
        $"GBFRES-slot-config-{label}-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(directory);
    File.Copy(nativeSource, Path.Combine(directory, "GBFR.ExtraSigilSlots.Native.dll"));
    return directory;
}

NativeResult RunNative(string testDirectory)
{
    string nativePath = Path.Combine(testDirectory, "GBFR.ExtraSigilSlots.Native.dll");
    Console.WriteLine($"NATIVE_CASE_BEGIN={Path.GetFileName(testDirectory)}");
    List<string> nativeLogs = [];
    int runtimeSlotCount;
    int runtimeSlotCapacity;
    uint[] selection = new uint[24];
    IntPtr library = NativeLibrary.Load(nativePath);
    Console.WriteLine("NATIVE_LIBRARY_LOADED=True");
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
        Console.WriteLine("NATIVE_INITIALIZE_RETURNED=True");
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

        GetSelection getSelection = Marshal.GetDelegateForFunctionPointer<GetSelection>(
            NativeLibrary.GetExport(library, "GBFR20_GetSelection"));
        IntPtr selectionBuffer = Marshal.AllocHGlobal(selection.Length * sizeof(uint));
        try
        {
            if (getSelection(0x2A26B1B2u, selectionBuffer, (uint)selection.Length) == 0)
                throw new InvalidOperationException("Native selection was unavailable.");
            for (int index = 0; index < selection.Length; ++index)
                selection[index] = unchecked((uint)Marshal.ReadInt32(selectionBuffer, index * sizeof(uint)));
        }
        finally
        {
            Marshal.FreeHGlobal(selectionBuffer);
        }
    }
    finally
    {
        setLogCallback?.Invoke(IntPtr.Zero);
        GC.KeepAlive(nativeLogCallback);
        NativeLibrary.Free(library);
    }
    return new(runtimeSlotCount, runtimeSlotCapacity, selection, nativeLogs);
}

static byte[] Config(
    string tail,
    string configVersion = "2",
    string toggleKey = "119",
    string showEquipped = "0",
    string autoApply = "1",
    string language = "zh-CN",
    bool includeLanguage = true)
{
    StringBuilder text = new();
    text.Append("[Settings]\r\n");
    text.Append("ConfigVersion=").Append(configVersion).Append("\r\n");
    text.Append("ToggleKey=").Append(toggleKey).Append("\r\n");
    text.Append("ShowEquipped=").Append(showEquipped).Append("\r\n");
    text.Append("AutoApply=").Append(autoApply).Append("\r\n");
    if (includeLanguage)
        text.Append("Language=").Append(language).Append("\r\n");
    text.Append(tail).Append("\r\n");
    return Encoding.UTF8.GetBytes(text.ToString());
}

static byte[] Append(byte[] left, string right) =>
    [.. left, .. Encoding.UTF8.GetBytes(right)];

static void AssertRuntime(NativeResult result, int expectedSlotCount, string label)
{
    if (result.SlotCount != expectedSlotCount || result.SlotCapacity != 24)
    {
        throw new InvalidOperationException(
            $"{label}: expected runtime {expectedSlotCount}/24, " +
            $"got {result.SlotCount}/{result.SlotCapacity}.");
    }
}

static void AssertNoBackups(string directory, string label)
{
    string[] backups = Directory.GetFiles(
        directory,
        ConfigFileName + ".invalid-*.bak*",
        SearchOption.TopDirectoryOnly);
    if (backups.Length != 0)
        throw new InvalidOperationException($"{label}: unexpected invalid-config backup.");
}

static void AssertBytesEqual(byte[] expected, byte[] actual, string label)
{
    if (!expected.AsSpan().SequenceEqual(actual))
        throw new InvalidOperationException($"{label}: byte sequences differ.");
}

internal sealed record NativeResult(
    int SlotCount,
    int SlotCapacity,
    uint[] Selection,
    List<string> Logs);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int InitializeNative();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void SetNativeLogCallback(IntPtr callback);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate void NativeLogCallback(IntPtr message);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int GetNativeState(IntPtr state, uint stateSize);

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int GetSelection(uint characterHash, IntPtr slots, uint slotCount);
