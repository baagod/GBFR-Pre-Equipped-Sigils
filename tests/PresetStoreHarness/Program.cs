using System.Collections;
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
MethodInfo getPresetsForCharacterMethod = storeType.GetMethod(
    "GetPresetsForCharacter",
    instanceFlags)!;
MethodInfo findByIdMethod = storeType.GetMethod("FindById", instanceFlags)!;
MethodInfo getSelectionsMethod = storeType.GetMethod("GetSelections", instanceFlags)!;
MethodInfo createMethod = storeType.GetMethod("Create", instanceFlags)!;
MethodInfo transferPresetMethod = storeType.GetMethod("TransferPreset", instanceFlags)!;
MethodInfo scopedReferencesMethod = storeType.GetMethods(instanceFlags).Single(method =>
    method.Name == "GetPresetNamesForSlot" && method.GetParameters().Length == 2);
MethodInfo aggregateReferencesMethod = storeType.GetMethods(instanceFlags).Single(method =>
    method.Name == "GetPresetNamesForSlot" && method.GetParameters().Length == 1);
MethodInfo clearReferencesMethod = storeType.GetMethod(
    "ClearSlotReferencesAndRun",
    instanceFlags)!;
PropertyInfo presetIdProperty = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.SigilPreset",
    throwOnError: true)!.GetProperty("Id")!;
PropertyInfo presetNameProperty = presetIdProperty.DeclaringType!.GetProperty("Name")!;
PropertyInfo presetCharacterProperty = presetIdProperty.DeclaringType!.GetProperty("CharacterHash")!;
PropertyInfo presetSlotsProperty = presetIdProperty.DeclaringType!.GetProperty("Slots")!;

const uint sourceCharacterHash = 0x2A26B1B2u;
const uint targetCharacterHash = 0x18E2F9F9u;
const uint thirdCharacterHash = 0x4D0A60C3u;
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
        $$"""
        {
          "Version": 1,
          "Presets": [
            {
              "Id": "existing",
              "Name": "方案甲",
              "Characters": [
                { "CharacterHash": {{sourceCharacterHash}}, "Slots": [123] },
                { "CharacterHash": {{targetCharacterHash}}, "Slots": [456] }
              ]
            },
            {
              "Id": "second",
              "Name": "方案乙",
              "Characters": [
                { "CharacterHash": {{sourceCharacterHash}}, "Slots": [0, 123, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 777] },
                { "CharacterHash": {{targetCharacterHash}}, "Slots": [457] }
              ]
            },
            {
              "Id": "third",
              "Name": "方案丙",
              "Characters": [
                { "CharacterHash": {{sourceCharacterHash}}, "Slots": [888] },
                { "CharacterHash": {{targetCharacterHash}}, "Slots": [123] }
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
        legacyJsonPath,
        "{\"Version\":2,\"Presets\":[]}",
        new UTF8Encoding(false));
    migratorType.GetMethod("Migrate", staticFlags)!.Invoke(
        null,
        [testDirectory, configDirectory, new Action<string>(migrationLogs.Add)]);
    if (File.ReadAllText(jsonPath) != migratedPresets)
        throw new InvalidOperationException("Migration overwrote canonical user data.");

    string currentDirectoryPreset = Path.Combine(
        testDirectory,
        "GBFR-ExtraSigilSlots.presets.json");
    File.WriteAllText(currentDirectoryPreset, migratedPresets, new UTF8Encoding(false));
    const string invalidPresetJson = "{ invalid";
    File.WriteAllText(jsonPath, invalidPresetJson, new UTF8Encoding(false));
    migratorType.GetMethod("Migrate", staticFlags)!.Invoke(
        null,
        [testDirectory, configDirectory, new Action<string>(migrationLogs.Add)]);
    string[] invalidBackups = Directory.GetFiles(
        configDirectory,
        "GBFR-ExtraSigilSlots.presets.json.invalid-*.bak");
    if (File.ReadAllText(jsonPath) != migratedPresets ||
        invalidBackups.Length != 1 ||
        File.ReadAllText(invalidBackups[0], Encoding.UTF8) != invalidPresetJson)
    {
        throw new InvalidOperationException(
            "Invalid persistent presets were not backed up and recovered.");
    }

    byte[] originalV1Bytes = File.ReadAllBytes(jsonPath);
    object store = CreateStore(configDirectory);
    AssertV3Document(jsonPath, expectedPresetCount: 6);
    string[] v1Backups = Directory.GetFiles(
        configDirectory,
        "GBFR-ExtraSigilSlots.presets.json.pre-v3-*.bak");
    if (v1Backups.Length != 1 ||
        !File.ReadAllBytes(v1Backups[0]).SequenceEqual(originalV1Bytes))
    {
        throw new InvalidOperationException("The v1 preset file was not backed up before migration.");
    }

    List<object> sourcePresets = PresetsFor(store, sourceCharacterHash);
    List<object> targetPresets = PresetsFor(store, targetCharacterHash);
    if (sourcePresets.Count != 3 || targetPresets.Count != 3 ||
        sourcePresets.Any(preset => Owner(preset) != sourceCharacterHash) ||
        targetPresets.Any(preset => Owner(preset) != targetCharacterHash))
    {
        throw new InvalidOperationException("Legacy global presets were not split by character.");
    }

    object reloadedWithoutMigration = CreateStore(configDirectory);
    if (Directory.GetFiles(
            configDirectory,
            "GBFR-ExtraSigilSlots.presets.json.pre-v3-*.bak").Length != 1 ||
        PresetsFor(reloadedWithoutMigration, sourceCharacterHash).Count != 3)
    {
        throw new InvalidOperationException("A v3 reload repeated migration or lost presets.");
    }
    store = reloadedWithoutMigration;

    int SourceReferences() => ScopedReferences(store, sourceCharacterHash, 123).Count;
    int TargetReferences() => ScopedReferences(store, targetCharacterHash, 123).Count;
    if (SourceReferences() != 2 || TargetReferences() != 1)
        throw new InvalidOperationException("Per-character preset reference indexing is incorrect.");
    IReadOnlyList<string> aggregateReferences =
        (IReadOnlyList<string>)aggregateReferencesMethod.Invoke(store, [123u])!;
    if (aggregateReferences.Count != 3)
        throw new InvalidOperationException("Aggregate preset reference indexing is incorrect.");

    object?[] rollbackArgs =
        [sourceCharacterHash, 123u, new Func<bool>(() => false), null];
    bool rollbackResult = (bool)clearReferencesMethod.Invoke(store, rollbackArgs)!;
    if (rollbackResult || SourceReferences() != 2 || TargetReferences() != 1 ||
        !JsonContainsSlotForCharacter(jsonPath, sourceCharacterHash, 123))
    {
        throw new InvalidOperationException("Failed transfer did not restore scoped preset references.");
    }

    object?[] commitArgs =
        [sourceCharacterHash, 123u, new Func<bool>(() => true), null];
    bool commitResult = (bool)clearReferencesMethod.Invoke(store, commitArgs)!;
    if (!commitResult || SourceReferences() != 0 || TargetReferences() != 1 ||
        JsonContainsSlotForCharacter(jsonPath, sourceCharacterHash, 123) ||
        !JsonContainsSlotForCharacter(jsonPath, targetCharacterHash, 123))
    {
        throw new InvalidOperationException("Preset reference clearing crossed character ownership.");
    }

    object highPreset = PresetNamed(store, sourceCharacterHash, "方案乙");
    string highPresetId = Id(highPreset);
    if (Slots(highPreset).Length != 24 || Slots(highPreset)[23] != 777)
        throw new InvalidOperationException("Migration discarded a higher inactive slot.");
    transferPresetMethod.Invoke(store, [highPreset, thirdCharacterHash]);
    if (Owner(highPreset) != thirdCharacterHash ||
        PresetsFor(store, sourceCharacterHash).Count != 2 ||
        PresetsFor(store, thirdCharacterHash).Count != 1 ||
        Slots(highPreset)[23] != 777 ||
        ScopedReferences(store, sourceCharacterHash, 777).Count != 0 ||
        ScopedReferences(store, thirdCharacterHash, 777).Count != 1)
    {
        throw new InvalidOperationException("A preset was not transferred as one owned item.");
    }

    IReadOnlyDictionary<uint, uint[]> transferredSelections =
        (IReadOnlyDictionary<uint, uint[]>)getSelectionsMethod.Invoke(store, [highPreset])!;
    if (transferredSelections.Count != 1 ||
        !transferredSelections.TryGetValue(thirdCharacterHash, out uint[]? transferredSlots) ||
        transferredSlots[23] != 777)
    {
        throw new InvalidOperationException("Transferred preset selections target the wrong character.");
    }

    object reloadedStore = CreateStore(configDirectory);
    object reloadedHighPreset = findByIdMethod.Invoke(reloadedStore, [highPresetId])!;
    if (Owner(reloadedHighPreset) != thirdCharacterHash || Slots(reloadedHighPreset)[23] != 777)
        throw new InvalidOperationException("Transferred preset ownership was not persisted.");
    store = reloadedStore;

    object sourceShared = createMethod.Invoke(store, [sourceCharacterHash, "共享名"])!;
    object targetShared = createMethod.Invoke(store, [targetCharacterHash, "共享名"])!;
    if (Owner(sourceShared) != sourceCharacterHash || Owner(targetShared) != targetCharacterHash ||
        Slots(sourceShared).Length != 24)
    {
        throw new InvalidOperationException("Character-owned preset creation failed.");
    }
    ExpectInvalidOperation(
        () => createMethod.Invoke(store, [sourceCharacterHash, "共享名"]),
        "Same-character duplicate preset names were accepted.");
    createMethod.Invoke(store, [sourceCharacterHash, "CaseName"]);
    ExpectInvalidOperation(
        () => createMethod.Invoke(store, [sourceCharacterHash, "casename"]),
        "Case-insensitive duplicate preset names were accepted.");
    ExpectInvalidOperation(
        () => transferPresetMethod.Invoke(store, [sourceShared, targetCharacterHash]),
        "Transfer overwrote a same-name target preset.");
    if (Owner(sourceShared) != sourceCharacterHash)
        throw new InvalidOperationException("Failed transfer changed preset ownership.");

    string emptyDirectory = Path.Combine(testRoot, "EmptyConfig");
    Directory.CreateDirectory(emptyDirectory);
    string emptyJsonPath = Path.Combine(emptyDirectory, "GBFR-ExtraSigilSlots.presets.json");
    File.WriteAllText(
        emptyJsonPath,
        $$"""
        {
          "Version": 3,
          "Presets": [
            {
              "Id": "empty",
              "Name": "空预设",
              "CharacterHash": {{sourceCharacterHash}},
              "Slots": []
            }
          ]
        }
        """,
        new UTF8Encoding(false));
    object emptyStore = CreateStore(emptyDirectory);
    object emptyPreset = findByIdMethod.Invoke(emptyStore, ["empty"])!;
    transferPresetMethod.Invoke(emptyStore, [emptyPreset, targetCharacterHash]);
    object reloadedEmptyStore = CreateStore(emptyDirectory);
    object reloadedEmptyPreset = findByIdMethod.Invoke(reloadedEmptyStore, ["empty"])!;
    if (Owner(reloadedEmptyPreset) != targetCharacterHash ||
        Slots(reloadedEmptyPreset).Length != 24 ||
        Slots(reloadedEmptyPreset).Any(slotId => slotId != 0))
    {
        throw new InvalidOperationException("An empty preset could not be transferred.");
    }

    string v2Directory = Path.Combine(testRoot, "V2Config");
    Directory.CreateDirectory(v2Directory);
    string v2Path = Path.Combine(v2Directory, "GBFR-ExtraSigilSlots.presets.json");
    File.WriteAllText(
        v2Path,
        $$"""
        {
          "Version": 2,
          "Presets": [
            {
              "Id": "v2",
              "Name": "V2",
              "Characters": [
                { "CharacterHash": {{sourceCharacterHash}}, "Slots": [9001] }
              ]
            },
            {
              "Id": "v2-empty",
              "Name": "V2 Empty",
              "Characters": [
                { "CharacterHash": {{targetCharacterHash}}, "Slots": [] }
              ]
            }
          ]
        }
        """,
        new UTF8Encoding(false));
    object v2Store = CreateStore(v2Directory);
    AssertV3Document(v2Path, expectedPresetCount: 2);
    if (Slots(PresetNamed(v2Store, sourceCharacterHash, "V2"))[0] != 9001 ||
        Slots(PresetNamed(v2Store, targetCharacterHash, "V2 Empty")).Any(slotId => slotId != 0) ||
        Directory.GetFiles(v2Directory, "*.pre-v3-*.bak").Length != 1)
    {
        throw new InvalidOperationException("V2 presets were not migrated with a backup.");
    }

    string duplicateDirectory = Path.Combine(testRoot, "DuplicateV3Config");
    Directory.CreateDirectory(duplicateDirectory);
    string duplicatePath = Path.Combine(
        duplicateDirectory,
        "GBFR-ExtraSigilSlots.presets.json");
    File.WriteAllText(
        duplicatePath,
        $$"""
        {
          "Version": 3,
          "Presets": [
            {
              "Id": "duplicate",
              "Name": "Duplicate",
              "CharacterHash": {{sourceCharacterHash}},
              "Slots": [42, 42]
            }
          ]
        }
        """,
        new UTF8Encoding(false));
    byte[] duplicateOriginalBytes = File.ReadAllBytes(duplicatePath);
    List<string> duplicateLogs = [];
    object duplicateStore = CreateStore(duplicateDirectory, duplicateLogs.Add);
    List<object> duplicatePresets = PresetsFor(duplicateStore, sourceCharacterHash);
    if (duplicatePresets.Count != 1)
    {
        throw new InvalidOperationException(
            "Malformed v3 preset failed to load: " + string.Join(" | ", duplicateLogs));
    }
    uint[] normalizedDuplicateSlots = Slots(duplicatePresets[0]);
    string[] normalizationBackups = Directory.GetFiles(
        duplicateDirectory,
        "*.pre-normalize-v3-*.bak");
    if (normalizedDuplicateSlots[0] != 42 || normalizedDuplicateSlots[1] != 0 ||
        normalizationBackups.Length != 1 ||
        !File.ReadAllBytes(normalizationBackups[0]).SequenceEqual(duplicateOriginalBytes))
    {
        throw new InvalidOperationException("Malformed v3 duplicate slots were not safely normalized.");
    }

    Console.WriteLine("PRESET_STORE_TEST=PASS");
    Console.WriteLine("PRESET_SCHEMA=3");
    Console.WriteLine("PER_CHARACTER_PRESETS=PASS");
    Console.WriteLine("PRESET_SINGLE_TRANSFER=PASS");
    Console.WriteLine("PRESET_EMPTY_TRANSFER=PASS");
    Console.WriteLine("PRESET_REFERENCE_SCOPE=PASS");
    Console.WriteLine("PRESET_V1_V2_MIGRATION=PASS");
    Console.WriteLine("PRESET_MIGRATION_BACKUP=PASS");
    Console.WriteLine("PRESET_V3_NORMALIZATION=PASS");
    Console.WriteLine("PRESET_HIGH_SLOT_RETENTION=PASS");
    Console.WriteLine("CHARACTER_NAME_MAP=28/28");
    Console.WriteLine("MANAGED_NUMCONFIG_CREATION=False");
    Console.WriteLine("ABI_VERSION=12");
    Console.WriteLine("PRESET_SELECTION_SIZE=100");
    Console.WriteLine("PRESET_RESULT_SIZE=20");
}
finally
{
    Directory.Delete(testRoot, recursive: true);
}

object CreateStore(string directory, Action<string>? log = null)
{
    return Activator.CreateInstance(
        storeType,
        instanceFlags,
        binder: null,
        args: [directory, log ?? new Action<string>(_ => { })],
        culture: null)!;
}

List<object> PresetsFor(object store, uint characterHash)
{
    IEnumerable presets =
        (IEnumerable)getPresetsForCharacterMethod.Invoke(store, [characterHash])!;
    return presets.Cast<object>().ToList();
}

object PresetNamed(object store, uint characterHash, string name)
{
    return PresetsFor(store, characterHash).Single(preset => Name(preset) == name);
}

IReadOnlyList<string> ScopedReferences(object store, uint characterHash, uint slotId)
{
    return (IReadOnlyList<string>)scopedReferencesMethod.Invoke(
        store,
        [characterHash, slotId])!;
}

string Id(object preset) => (string)presetIdProperty.GetValue(preset)!;
string Name(object preset) => (string)presetNameProperty.GetValue(preset)!;
uint Owner(object preset) => (uint)presetCharacterProperty.GetValue(preset)!;
uint[] Slots(object preset) => (uint[])presetSlotsProperty.GetValue(preset)!;

static void ExpectInvalidOperation(Action action, string failureMessage)
{
    try
    {
        action();
    }
    catch (TargetInvocationException exception)
        when (exception.InnerException is InvalidOperationException)
    {
        return;
    }
    throw new InvalidOperationException(failureMessage);
}

static void AssertV3Document(string path, int expectedPresetCount)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
    JsonElement root = document.RootElement;
    if (root.GetProperty("Version").GetInt32() != 3)
        throw new InvalidOperationException("Preset document was not upgraded to schema v3.");
    JsonElement presets = root.GetProperty("Presets");
    if (presets.GetArrayLength() != expectedPresetCount)
        throw new InvalidOperationException("Unexpected v3 preset count.");
    foreach (JsonElement preset in presets.EnumerateArray())
    {
        if (!preset.TryGetProperty("CharacterHash", out _) ||
            !preset.TryGetProperty("Slots", out JsonElement slots) ||
            slots.GetArrayLength() != 24 ||
            preset.TryGetProperty("Characters", out _))
        {
            throw new InvalidOperationException("A v3 preset retained the legacy global shape.");
        }
    }
}

static bool JsonContainsSlotForCharacter(string path, uint characterHash, uint slotId)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
    foreach (JsonElement preset in document.RootElement.GetProperty("Presets").EnumerateArray())
    {
        if (preset.GetProperty("CharacterHash").GetUInt32() != characterHash)
            continue;
        foreach (JsonElement slot in preset.GetProperty("Slots").EnumerateArray())
        {
            if (slot.GetUInt32() == slotId)
                return true;
        }
    }
    return false;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate uint GetAbiVersion();
