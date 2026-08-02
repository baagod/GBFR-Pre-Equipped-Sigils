using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

if (args.Length != 1)
    throw new ArgumentException("Pass the managed build output directory.");

string modDirectory = Path.GetFullPath(args[0]);
string managedPath = Path.Combine(modDirectory, "GBFR.ExtraSigilSlots.Reloaded.dll");
Assembly assembly = Assembly.LoadFrom(managedPath);
BindingFlags staticFlags = BindingFlags.Static | BindingFlags.NonPublic;
BindingFlags instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;

Type uiLocalization = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.UiLocalization",
    throwOnError: true)!;
MethodInfo characterNameMethod = uiLocalization.GetMethod("CharacterName", staticFlags)!;
(uint Hash, string Chinese, string English)[] expectedCharacters =
[
    (0x2A26B1B2, "主角（格兰/姬塔）", "Captain (Gran/Djeeta)"),
    (0x18E2F9F9, "卡塔莉娜", "Katalina"),
    (0x079DF0CC, "拉卡姆", "Rackam"),
    (0x4D0A60C3, "伊欧", "Io"),
    (0xDD7A151E, "欧根", "Eugen"),
    (0xC8616284, "萝赛塔", "Rosetta"),
    (0xC3FFD418, "菲莉", "Ferry"),
    (0x22E437E5, "兰斯洛特", "Lancelot"),
    (0x2EBE91D5, "巴恩", "Vane"),
    (0xBDEF7181, "珀西瓦尔", "Percival"),
    (0x627BCB0D, "齐格飞", "Siegfried"),
    (0xFD3BE362, "夏洛特", "Charlotta"),
    (0xFC6CDF7B, "尤达拉哈", "Yodarha"),
    (0xE7053919, "娜露梅", "Narmaya"),
    (0x978E4B18, "冈达葛萨", "Ghandagoza"),
    (0x0D21B430, "塞达", "Zeta"),
    (0xF0EB77EF, "巴萨拉卡", "Vaseraga"),
    (0xAA66178A, "卡莉奥丝特罗", "Cagliostro"),
    (0xA3A3CB2F, "伊德", "Id"),
    (0x718E1A14, "圣德芬", "Sandalphon"),
    (0x296471BE, "希耶提", "Seofon"),
    (0xBAD16E3B, "索恩", "Tweyen"),
    (0x1BB37EF0, "伽兰查", "Gallanza"),
    (0x25D46F4B, "玛琪拉菲菈", "Maglielle"),
    (0x9A8AF295, "贝阿朵丽丝", "Beatrix"),
    (0x9B15CFB1, "尤斯提斯", "Eustace"),
    (0x646C3168, "芙劳", "Fraux"),
    (0x74DD4C79, "菲迪埃尔", "Fediel"),
];
foreach ((uint hash, string chinese, string english) in expectedCharacters)
{
    string actualChinese = (string)characterNameMethod.Invoke(null, [hash, false])!;
    string actualEnglish = (string)characterNameMethod.Invoke(null, [hash, true])!;
    if (actualChinese != chinese || actualEnglish != english)
    {
        throw new InvalidOperationException(
            $"Character mapping mismatch for 0x{hash:X8}: " +
            $"'{actualChinese}'/'{actualEnglish}'.");
    }
}

Type nativeCore = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.NativeCore",
    throwOnError: true)!;
nativeCore.GetMethod("Configure", staticFlags)!.Invoke(null, [modDirectory]);

Type nativeSelectionType = nativeCore.GetNestedType(
    "PresetCharacterSelection",
    BindingFlags.NonPublic)!;
Type nativeResultType = nativeCore.GetNestedType(
    "PresetSlotResult",
    BindingFlags.NonPublic)!;
if (Marshal.SizeOf(nativeSelectionType) != 100 || Marshal.SizeOf(nativeResultType) != 20)
    throw new InvalidOperationException("Managed preset ABI struct sizes are incorrect.");

IntPtr nativeLibrary = NativeLibrary.Load(
    Path.Combine(modDirectory, "GBFR.ExtraSigilSlots.Native.dll"));
try
{
    IntPtr abiExport = NativeLibrary.GetExport(nativeLibrary, "GBFR20_GetAbiVersion");
    IntPtr applyExport = NativeLibrary.GetExport(nativeLibrary, "GBFR20_ApplyPreset");
    GetAbiVersion getAbiVersion = Marshal.GetDelegateForFunctionPointer<GetAbiVersion>(abiExport);
    if (getAbiVersion() != 12 || applyExport == IntPtr.Zero)
        throw new InvalidOperationException("Native ABI 12 preset exports are unavailable.");
}
finally
{
    NativeLibrary.Free(nativeLibrary);
}

Type storeType = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.SigilPresetStore",
    throwOnError: true)!;
Type migratorType = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.LegacyDataMigrator",
    throwOnError: true)!;
string testRoot = Path.Combine(
    Path.GetTempPath(),
    "GBFRES-preset-test-" + Guid.NewGuid().ToString("N"));
string testDirectory = Path.Combine(testRoot, "GBFR.ExtraSigilSlots.Reloaded");
string legacyDirectory = Path.Combine(testRoot, "GBFR.ExtraSigilSlots20.Reloaded");
string configDirectory = Path.Combine(testRoot, "ReloadedConfig");
Directory.CreateDirectory(testDirectory);
Directory.CreateDirectory(legacyDirectory);
string jsonPath = Path.Combine(configDirectory, "GBFR-ExtraSigilSlots.presets.json");
string legacyJsonPath = Path.Combine(legacyDirectory, "GBFR-ExtraSigilSlots20.presets.json");
string configPath = Path.Combine(testDirectory, "GBFR-ExtraSigilSlotsNumConfig.ini");
string legacyConfigPath = Path.Combine(legacyDirectory, "GBFR-ExtraSigilSlotsNumConfig.ini");

try
{
    File.WriteAllText(
        legacyConfigPath,
        "[Settings]\nConfigVersion=2\nToggleKey=119\nShowEquipped=1\n" +
        "AutoApply=1\nLanguage=en\nVirtualSlotCount=12\n",
        new UTF8Encoding(false));
    File.WriteAllText(
        legacyJsonPath,
        """
        {
          "Version": 1,
          "Presets": [
            {
              "Id": "existing",
              "Name": "方案甲",
              "Characters": [
                {
                  "CharacterHash": 707178930,
                  "Slots": [123, 0, 0, 0, 0, 0, 0, 0]
                }
              ]
            },
            {
              "Id": "second",
              "Name": "方案乙",
              "Characters": [
                {
                  "CharacterHash": 417542649,
                  "Slots": [0, 123, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 777]
                }
              ]
            }
          ]
        }
        """,
        new UTF8Encoding(false));

    List<string> migrationLogs = [];
    migratorType.GetMethod("Migrate", staticFlags)!.Invoke(
        null,
        [testDirectory, configDirectory, new Action<string>(migrationLogs.Add)]);
    if (!File.Exists(jsonPath) ||
        File.Exists(configPath) ||
        !File.Exists(legacyJsonPath) ||
        migrationLogs.Count(message => message.StartsWith("Migrated", StringComparison.Ordinal)) != 1)
    {
        throw new InvalidOperationException(
            "Legacy preset migration or runtime-owned NumConfig creation policy failed.");
    }

    string migratedPresets = File.ReadAllText(jsonPath);
    File.WriteAllText(
        legacyConfigPath,
        "[Settings]\nConfigVersion=2\nVirtualSlotCount=16\n",
        new UTF8Encoding(false));
    File.WriteAllText(
        legacyJsonPath,
        "{\"Version\":2,\"Presets\":[]}",
        new UTF8Encoding(false));
    migratorType.GetMethod("Migrate", staticFlags)!.Invoke(
        null,
        [testDirectory, configDirectory, new Action<string>(migrationLogs.Add)]);
    if (File.Exists(configPath) ||
        File.ReadAllText(jsonPath) != migratedPresets)
    {
        throw new InvalidOperationException("Migration overwrote canonical user data.");
    }

    string currentDirectoryPreset = Path.Combine(
        testDirectory,
        "GBFR-ExtraSigilSlots.presets.json");
    File.WriteAllText(currentDirectoryPreset, migratedPresets, new UTF8Encoding(false));
    File.WriteAllText(jsonPath, "{ invalid", new UTF8Encoding(false));
    migratorType.GetMethod("Migrate", staticFlags)!.Invoke(
        null,
        [testDirectory, configDirectory, new Action<string>(migrationLogs.Add)]);
    if (File.ReadAllText(jsonPath) != migratedPresets ||
        Directory.GetFiles(
            configDirectory,
            "GBFR-ExtraSigilSlots.presets.json.invalid-*.bak").Length != 1)
    {
        throw new InvalidOperationException(
            "Invalid persistent presets were not backed up and recovered from the prior mod directory.");
    }

    const string structurallyInvalidPresets = "{\"Version\":2,\"Presets\":[1]}";
    File.WriteAllText(jsonPath, structurallyInvalidPresets, new UTF8Encoding(false));
    File.WriteAllText(currentDirectoryPreset, structurallyInvalidPresets, new UTF8Encoding(false));
    File.Delete(legacyJsonPath);
    migratorType.GetMethod("Migrate", staticFlags)!.Invoke(
        null,
        [testDirectory, configDirectory, new Action<string>(migrationLogs.Add)]);
    if (File.ReadAllText(jsonPath) != structurallyInvalidPresets ||
        Directory.GetFiles(
            configDirectory,
            "GBFR-ExtraSigilSlots.presets.json.invalid-*.bak").Length != 2)
    {
        throw new InvalidOperationException(
            "A structurally invalid candidate was accepted or its destination evidence was not preserved.");
    }

    File.WriteAllText(currentDirectoryPreset, migratedPresets, new UTF8Encoding(false));
    migratorType.GetMethod("Migrate", staticFlags)!.Invoke(
        null,
        [testDirectory, configDirectory, new Action<string>(migrationLogs.Add)]);
    if (File.ReadAllText(jsonPath) != migratedPresets)
        throw new InvalidOperationException("A valid prior preset file did not recover persistent storage.");

    object store = Activator.CreateInstance(
        storeType,
        instanceFlags,
        binder: null,
        args: [configDirectory, new Action<string>(_ => { })],
        culture: null)!;

    MethodInfo referencesMethod = storeType.GetMethod("GetPresetNamesForSlot", instanceFlags)!;
    MethodInfo transferMethod = storeType.GetMethod("ClearSlotReferencesAndRun", instanceFlags)!;
    MethodInfo createMethod = storeType.GetMethod("Create", instanceFlags)!;
    MethodInfo findByIdMethod = storeType.GetMethod("FindById", instanceFlags)!;
    MethodInfo getSelectionsMethod = storeType.GetMethod("GetSelections", instanceFlags)!;

    int InitialReferenceCount() =>
        ((System.Collections.ICollection)referencesMethod.Invoke(store, [123u])!).Count;

    if (InitialReferenceCount() != 2)
        throw new InvalidOperationException("Expected two preset references before transfer.");

    object?[] rollbackArgs = [123u, new Func<bool>(() => false), null];
    bool rollbackResult = (bool)transferMethod.Invoke(store, rollbackArgs)!;
    if (rollbackResult || InitialReferenceCount() != 2 || !JsonContainsSlot(jsonPath, 123))
        throw new InvalidOperationException("Failed transfer did not restore preset references.");

    object?[] commitArgs = [123u, new Func<bool>(() => true), null];
    bool commitResult = (bool)transferMethod.Invoke(store, commitArgs)!;
    if (!commitResult || InitialReferenceCount() != 0 || JsonContainsSlot(jsonPath, 123))
        throw new InvalidOperationException("Successful transfer did not clear every reference.");

    object retainedPreset = findByIdMethod.Invoke(store, ["second"])!;
    IReadOnlyDictionary<uint, uint[]> retainedSelections =
        (IReadOnlyDictionary<uint, uint[]>)getSelectionsMethod.Invoke(store, [retainedPreset])!;
    if (!retainedSelections.TryGetValue(417542649u, out uint[]? retainedSlots) ||
        retainedSlots.Length != 24 ||
        retainedSlots[23] != 777 ||
        !JsonContainsSlot(jsonPath, 777))
    {
        throw new InvalidOperationException(
            "Preset normalization or transfer discarded a higher inactive slot.");
    }

    object created = createMethod.Invoke(store, ["中文自定义预设"])!;
    Type presetType = created.GetType();
    string createdName = (string)presetType.GetProperty("Name")!.GetValue(created)!;
    int characterCount = ((System.Collections.ICollection)presetType
        .GetProperty("Characters")!
        .GetValue(created)!).Count;
    if (createdName != "中文自定义预设" || characterCount != 28)
        throw new InvalidOperationException("Named full-character preset capture failed.");

    Console.WriteLine("PRESET_STORE_TEST=PASS");
    Console.WriteLine("ROLLBACK_REFERENCES=2");
    Console.WriteLine("COMMITTED_REFERENCES=0");
    Console.WriteLine("CAPTURED_CHARACTERS=28");
    Console.WriteLine("LEGACY_PRESET_MIGRATION=PASS");
    Console.WriteLine("PERSISTENT_PRESET_RECOVERY=PASS");
    Console.WriteLine("CHARACTER_NAME_MAP=28/28");
    Console.WriteLine("PRESET_HIGH_SLOT_RETENTION=PASS");
    Console.WriteLine("MANAGED_NUMCONFIG_CREATION=False");
    Console.WriteLine("ABI_VERSION=12");
    Console.WriteLine("PRESET_SELECTION_SIZE=100");
    Console.WriteLine("PRESET_RESULT_SIZE=20");
}
finally
{
    Directory.Delete(testRoot, recursive: true);
}

static bool JsonContainsSlot(string path, uint slotId)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
    foreach (JsonElement preset in document.RootElement.GetProperty("Presets").EnumerateArray())
    {
        foreach (JsonElement character in preset.GetProperty("Characters").EnumerateArray())
        {
            foreach (JsonElement slot in character.GetProperty("Slots").EnumerateArray())
            {
                if (slot.GetUInt32() == slotId)
                    return true;
            }
        }
    }
    return false;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate uint GetAbiVersion();
