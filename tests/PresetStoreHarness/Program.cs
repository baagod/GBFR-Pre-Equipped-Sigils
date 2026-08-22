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
    (0x2A26B1B2, "古兰", "Gran"),
    (0xA4ACBA76, "姬塔", "Djeeta"),
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
uint[] knownCharacterHashes = (uint[])uiLocalization.GetField(
    "KnownCharacterHashes",
    staticFlags)!.GetValue(null)!;
if (!knownCharacterHashes.SequenceEqual(expectedCharacters.Select(character => character.Hash)))
    throw new InvalidOperationException(
        "UiLocalization.KnownCharacterHashes does not enumerate Djeeta (0xA4ACBA76).");
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
MethodInfo? isCharacterCompatibleMethod = uiLocalization.GetMethod(
    "IsCharacterCompatible",
    staticFlags);
if (isCharacterCompatibleMethod is null)
    throw new InvalidOperationException(
        "UiLocalization does not expose the captain compatibility rule.");
bool IsCharacterCompatible(uint requiredCharacterHash, uint characterHash) =>
    (bool)isCharacterCompatibleMethod.Invoke(
        null,
        [requiredCharacterHash, characterHash])!;
if (!IsCharacterCompatible(0, 0xA4ACBA76) ||
    !IsCharacterCompatible(0x2A26B1B2, 0x2A26B1B2) ||
    !IsCharacterCompatible(0x2A26B1B2, 0xA4ACBA76) ||
    !IsCharacterCompatible(0xA4ACBA76, 0x2A26B1B2) ||
    !IsCharacterCompatible(0xA4ACBA76, 0xA4ACBA76) ||
    IsCharacterCompatible(0x2A26B1B2, 0x18E2F9F9) ||
    IsCharacterCompatible(0x18E2F9F9, 0xA4ACBA76))
{
    throw new InvalidOperationException(
        "Gran and Djeeta do not share the captain sigil compatibility group.");
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
    if (getAbiVersion() != 13 || applyExport == IntPtr.Zero)
        throw new InvalidOperationException("Native ABI 13 preset exports are unavailable.");
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
MethodInfo deleteMethod = storeType.GetMethod("Delete", instanceFlags)!;
MethodInfo renameMethod = storeType.GetMethod("Rename", instanceFlags)!;
MethodInfo resolveSelectedPresetMethod = storeType.GetMethod(
    "ResolveSelectedPreset",
    instanceFlags)!;
MethodInfo selectPresetMethod = storeType.GetMethod("SelectPreset", instanceFlags)!;
MethodInfo markTemporaryMethod = storeType.GetMethod("MarkTemporary", instanceFlags)!;
MethodInfo isSelectedPresetMethod = storeType.GetMethod("IsSelectedPreset", instanceFlags)!;
PropertyInfo presetIdProperty = assembly.GetType(
    "GBFR.ExtraSigilSlots.Reloaded.SigilPreset",
    throwOnError: true)!.GetProperty("Id")!;
PropertyInfo presetNameProperty = presetIdProperty.DeclaringType!.GetProperty("Name")!;
PropertyInfo presetCharacterProperty = presetIdProperty.DeclaringType!.GetProperty("CharacterHash")!;
PropertyInfo presetSlotsProperty = presetIdProperty.DeclaringType!.GetProperty("Slots")!;

const uint sourceCharacterHash = 0x2A26B1B2u;
const uint djeetaCharacterHash = 0xA4ACBA76u;
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
    AssertV4Document(jsonPath, expectedPresetCount: 6);
    string[] v1Backups = Directory.GetFiles(
        configDirectory,
        "GBFR-ExtraSigilSlots.presets.json.pre-v4-*.bak");
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
            "GBFR-ExtraSigilSlots.presets.json.pre-v4-*.bak").Length != 1 ||
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
    AssertV4Document(v2Path, expectedPresetCount: 2);
    if (Slots(PresetNamed(v2Store, sourceCharacterHash, "V2"))[0] != 9001 ||
        Slots(PresetNamed(v2Store, targetCharacterHash, "V2 Empty")).Any(slotId => slotId != 0) ||
        Directory.GetFiles(v2Directory, "*.pre-v4-*.bak").Length != 1)
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
        "*.pre-v4-*.bak");
    if (normalizedDuplicateSlots[0] != 42 || normalizedDuplicateSlots[1] != 0 ||
        normalizationBackups.Length != 1 ||
        !File.ReadAllBytes(normalizationBackups[0]).SequenceEqual(duplicateOriginalBytes))
    {
        throw new InvalidOperationException("Malformed v3 duplicate slots were not safely normalized.");
    }

    string selectionDirectory = Path.Combine(testRoot, "SelectionConfig");
    Directory.CreateDirectory(selectionDirectory);
    object selectionStore = CreateStore(selectionDirectory);
    object persistedPresetA = createMethod.Invoke(selectionStore, [sourceCharacterHash, "PersistA"])!;
    if (!IsSelectedPreset(selectionStore, persistedPresetA))
        throw new InvalidOperationException("Create did not persist the new preset as selected.");

    object selectionReloaded = CreateStore(selectionDirectory);
    object? selectedAfterReload = ResolveSelectedPreset(
        selectionReloaded,
        sourceCharacterHash,
        Slots(persistedPresetA));
    if (selectedAfterReload is null || Id(selectedAfterReload) != Id(persistedPresetA))
        throw new InvalidOperationException("Created preset selection was not restored after reload.");
    Dictionary<uint, string?> persistedSelection =
        ReadSelectedPresetIds(Path.Combine(selectionDirectory, "GBFR-ExtraSigilSlots.presets.json"));
    if (!persistedSelection.TryGetValue(sourceCharacterHash, out string? persistedSelectedId) ||
        persistedSelectedId != Id(persistedPresetA))
    {
        throw new InvalidOperationException("Created preset selection was not written to the preset file.");
    }

    object persistedPresetB = createMethod.Invoke(selectionReloaded, [sourceCharacterHash, "PersistB"])!;
    object selectionReloadedB = CreateStore(selectionDirectory);
    selectedAfterReload = ResolveSelectedPreset(
        selectionReloadedB,
        sourceCharacterHash,
        Slots(persistedPresetB));
    if (selectedAfterReload is null || Id(selectedAfterReload) != Id(persistedPresetB))
        throw new InvalidOperationException("A later selection was not restored after reload.");

    MarkTemporary(selectionReloadedB, sourceCharacterHash);
    object selectionReloadedTemporary = CreateStore(selectionDirectory);
    selectedAfterReload = ResolveSelectedPreset(
        selectionReloadedTemporary,
        sourceCharacterHash,
        Slots(persistedPresetB));
    if (selectedAfterReload is not null)
        throw new InvalidOperationException("Explicit temporary selection was not restored after reload.");
    persistedSelection = ReadSelectedPresetIds(Path.Combine(selectionDirectory, "GBFR-ExtraSigilSlots.presets.json"));
    if (!persistedSelection.TryGetValue(sourceCharacterHash, out string? explicitTemporary) ||
        explicitTemporary is not null)
    {
        throw new InvalidOperationException("Explicit temporary was not persisted as null.");
    }
    MarkTemporary(selectionReloadedTemporary, 0);
    Console.WriteLine("PRESET_SELECTION_PERSISTENCE=PASS");

    string captainIsolationDirectory = Path.Combine(testRoot, "CaptainIsolationConfig");
    Directory.CreateDirectory(captainIsolationDirectory);
    object captainIsolationStore = CreateStore(captainIsolationDirectory);
    object captainGranPreset = createMethod.Invoke(
        captainIsolationStore,
        [sourceCharacterHash, "GranPreset"])!;
    object captainDjeetaPreset = createMethod.Invoke(
        captainIsolationStore,
        [djeetaCharacterHash, "DjeetaPreset"])!;
    if (!IsSelectedPreset(captainIsolationStore, captainGranPreset) ||
        !IsSelectedPreset(captainIsolationStore, captainDjeetaPreset))
    {
        throw new InvalidOperationException(
            "Gran and Djeeta presets were not both persisted as selected.");
    }

    object captainIsolationReloaded = CreateStore(captainIsolationDirectory);
    object? captainGranAfterReload = ResolveSelectedPreset(
        captainIsolationReloaded,
        sourceCharacterHash,
        Slots(captainGranPreset));
    object? captainDjeetaAfterReload = ResolveSelectedPreset(
        captainIsolationReloaded,
        djeetaCharacterHash,
        Slots(captainDjeetaPreset));
    if (captainGranAfterReload is null ||
        Id(captainGranAfterReload) != Id(captainGranPreset) ||
        captainDjeetaAfterReload is null ||
        Id(captainDjeetaAfterReload) != Id(captainDjeetaPreset))
    {
        throw new InvalidOperationException(
            "Gran and Djeeta selected presets were not restored independently after reload.");
    }

    string captainIsolationPath = Path.Combine(
        captainIsolationDirectory,
        "GBFR-ExtraSigilSlots.presets.json");
    Dictionary<uint, string?> captainIsolationSelection =
        ReadSelectedPresetIds(captainIsolationPath);
    if (!captainIsolationSelection.TryGetValue(
            sourceCharacterHash,
            out string? captainGranSelectedId) ||
        captainGranSelectedId != Id(captainGranPreset) ||
        !captainIsolationSelection.TryGetValue(
            djeetaCharacterHash,
            out string? captainDjeetaSelectedId) ||
        captainDjeetaSelectedId != Id(captainDjeetaPreset))
    {
        throw new InvalidOperationException(
            "The preset file did not keep Gran and Djeeta selection keys separate.");
    }

    MarkTemporary(captainIsolationReloaded, sourceCharacterHash);
    object captainIsolationAfterGranTemporary = CreateStore(captainIsolationDirectory);
    object? captainGranAfterTemporary = ResolveSelectedPreset(
        captainIsolationAfterGranTemporary,
        sourceCharacterHash,
        Slots(captainGranPreset));
    object? captainDjeetaAfterGranTemporary = ResolveSelectedPreset(
        captainIsolationAfterGranTemporary,
        djeetaCharacterHash,
        Slots(captainDjeetaPreset));
    if (captainGranAfterTemporary is not null ||
        captainDjeetaAfterGranTemporary is null ||
        Id(captainDjeetaAfterGranTemporary) != Id(captainDjeetaPreset))
    {
        throw new InvalidOperationException(
            "Marking Gran temporary changed Djeeta's selected preset.");
    }
    captainIsolationSelection = ReadSelectedPresetIds(captainIsolationPath);
    if (!captainIsolationSelection.TryGetValue(
            sourceCharacterHash,
            out string? captainGranTemporaryState) ||
        captainGranTemporaryState is not null ||
        !captainIsolationSelection.TryGetValue(
            djeetaCharacterHash,
            out string? captainDjeetaAfterTemporaryState) ||
        captainDjeetaAfterTemporaryState != Id(captainDjeetaPreset))
    {
        throw new InvalidOperationException(
            "Gran temporary state was not persisted without overwriting Djeeta's selection.");
    }
    Console.WriteLine("CAPTAIN_PRESET_SELECTION_ISOLATION=PASS");

    string activeTransferDirectory = Path.Combine(testRoot, "ActiveTransferConfig");
    Directory.CreateDirectory(activeTransferDirectory);
    object activeTransferStore = CreateStore(activeTransferDirectory);
    object targetActivePreset = createMethod.Invoke(activeTransferStore, [targetCharacterHash, "TargetActive"])!;
    object sourceActivePreset = createMethod.Invoke(activeTransferStore, [sourceCharacterHash, "SourceActive"])!;
    if (!IsSelectedPreset(activeTransferStore, sourceActivePreset))
        throw new InvalidOperationException("Create did not mark the active transfer source preset selected.");
    transferPresetMethod.Invoke(activeTransferStore, [sourceActivePreset, targetCharacterHash]);
    object activeTransferReloaded = CreateStore(activeTransferDirectory);
    object? sourceAfterActiveTransfer = ResolveSelectedPreset(
        activeTransferReloaded,
        sourceCharacterHash,
        Slots(sourceActivePreset));
    if (sourceAfterActiveTransfer is not null)
        throw new InvalidOperationException("Transferring the active preset did not make the source explicitly temporary.");
    object? targetAfterActiveTransfer = ResolveSelectedPreset(
        activeTransferReloaded,
        targetCharacterHash,
        Slots(targetActivePreset));
    if (targetAfterActiveTransfer is null || Id(targetAfterActiveTransfer) != Id(targetActivePreset))
        throw new InvalidOperationException("Transferring an active preset changed the target selection.");
    Dictionary<uint, string?> activeTransferSelection =
        ReadSelectedPresetIds(Path.Combine(activeTransferDirectory, "GBFR-ExtraSigilSlots.presets.json"));
    if (!activeTransferSelection.TryGetValue(sourceCharacterHash, out string? activeSourceState) ||
        activeSourceState is not null)
    {
        throw new InvalidOperationException("Active transfer source state was not persisted as explicit temporary.");
    }
    Console.WriteLine("PRESET_ACTIVE_TRANSFER_TEMPORARY=PASS");

    string inactiveTransferDirectory = Path.Combine(testRoot, "InactiveTransferConfig");
    Directory.CreateDirectory(inactiveTransferDirectory);
    object inactiveTransferStore = CreateStore(inactiveTransferDirectory);
    object targetInactivePreset = createMethod.Invoke(inactiveTransferStore, [targetCharacterHash, "TargetInactive"])!;
    object sourceInactiveActivePreset = createMethod.Invoke(inactiveTransferStore, [sourceCharacterHash, "SourceActiveInactive"])!;
    object sourceInactivePreset = createMethod.Invoke(inactiveTransferStore, [sourceCharacterHash, "SourceInactive"])!;
    SelectPreset(inactiveTransferStore, sourceInactiveActivePreset);
    transferPresetMethod.Invoke(inactiveTransferStore, [sourceInactivePreset, targetCharacterHash]);
    object inactiveTransferReloaded = CreateStore(inactiveTransferDirectory);
    object? sourceAfterInactiveTransfer = ResolveSelectedPreset(
        inactiveTransferReloaded,
        sourceCharacterHash,
        Slots(sourceInactiveActivePreset));
    if (sourceAfterInactiveTransfer is null || Id(sourceAfterInactiveTransfer) != Id(sourceInactiveActivePreset))
        throw new InvalidOperationException("Transferring a non-active preset changed the source selection.");
    object? targetAfterInactiveTransfer = ResolveSelectedPreset(
        inactiveTransferReloaded,
        targetCharacterHash,
        Slots(targetInactivePreset));
    if (targetAfterInactiveTransfer is null || Id(targetAfterInactiveTransfer) != Id(targetInactivePreset))
        throw new InvalidOperationException("Transferring a non-active preset changed the target selection.");
    Console.WriteLine("PRESET_INACTIVE_TRANSFER_PRESERVED=PASS");

    string legacyRecoveryDirectory = Path.Combine(testRoot, "LegacyRecoveryConfig");
    Directory.CreateDirectory(legacyRecoveryDirectory);
    string legacyRecoveryPath = Path.Combine(legacyRecoveryDirectory, "GBFR-ExtraSigilSlots.presets.json");
    File.WriteAllText(
        legacyRecoveryPath,
        $$"""
        {
          "Version": 3,
          "Presets": [
            {
              "Id": "legacy-first",
              "Name": "Legacy First",
              "CharacterHash": {{sourceCharacterHash}},
              "Slots": [11, 22]
            },
            {
              "Id": "legacy-duplicate",
              "Name": "Legacy Duplicate",
              "CharacterHash": {{sourceCharacterHash}},
              "Slots": [11, 22]
            }
          ]
        }
        """,
        new UTF8Encoding(false));
    object legacyRecoveryStore = CreateStore(legacyRecoveryDirectory);
    AssertV4Document(legacyRecoveryPath, expectedPresetCount: 2);
    if (ReadSelectedPresetIds(legacyRecoveryPath).Count != 0)
        throw new InvalidOperationException("Legacy migration seeded a selection map.");
    object? recoveredLegacyPreset = ResolveSelectedPreset(
        legacyRecoveryStore,
        sourceCharacterHash,
        [11, 22]);
    if (recoveredLegacyPreset is null || Id(recoveredLegacyPreset) != "legacy-first")
        throw new InvalidOperationException("Legacy unresolved selection did not choose the first matching preset.");
    Dictionary<uint, string?> legacySelection = ReadSelectedPresetIds(legacyRecoveryPath);
    if (!legacySelection.TryGetValue(sourceCharacterHash, out string? recoveredId) ||
        recoveredId != "legacy-first")
    {
        throw new InvalidOperationException("Legacy selection recovery was not persisted.");
    }
    object legacyRecoveryReloaded = CreateStore(legacyRecoveryDirectory);
    object? recoveredAfterReload = ResolveSelectedPreset(
        legacyRecoveryReloaded,
        sourceCharacterHash,
        [11, 22]);
    if (recoveredAfterReload is null || Id(recoveredAfterReload) != "legacy-first")
        throw new InvalidOperationException("Legacy selection recovery was not stable after reload.");
    if (Directory.GetFiles(legacyRecoveryDirectory, "*.pre-v4-*.bak").Length != 1)
        throw new InvalidOperationException("Legacy v3 recovery did not create a v4 migration backup.");
    Console.WriteLine("PRESET_SELECTION_LEGACY_RECOVERY=PASS");

    string legacyActiveTransferDirectory = Path.Combine(testRoot, "LegacyActiveTransferConfig");
    Directory.CreateDirectory(legacyActiveTransferDirectory);
    string legacyActiveTransferPath = Path.Combine(
        legacyActiveTransferDirectory,
        "GBFR-ExtraSigilSlots.presets.json");
    File.WriteAllText(
        legacyActiveTransferPath,
        $$"""
        {
          "Version": 3,
          "Presets": [
            {
              "Id": "legacy-active-transfer",
              "Name": "Legacy Active Transfer",
              "CharacterHash": {{sourceCharacterHash}},
              "Slots": [91, 92]
            }
          ]
        }
        """,
        new UTF8Encoding(false));
    object legacyActiveTransferStore = CreateStore(legacyActiveTransferDirectory);
    object? legacyActiveTransferPreset = ResolveSelectedPreset(
        legacyActiveTransferStore,
        sourceCharacterHash,
        [91, 92]);
    if (legacyActiveTransferPreset is null ||
        Id(legacyActiveTransferPreset) != "legacy-active-transfer")
    {
        throw new InvalidOperationException(
            "Legacy active preset was not resolved before transfer.");
    }
    transferPresetMethod.Invoke(
        legacyActiveTransferStore,
        [legacyActiveTransferPreset, targetCharacterHash]);
    object legacyActiveTransferReloaded = CreateStore(legacyActiveTransferDirectory);
    object? legacySourceAfterTransfer = ResolveSelectedPreset(
        legacyActiveTransferReloaded,
        sourceCharacterHash,
        [91, 92]);
    if (legacySourceAfterTransfer is not null)
    {
        throw new InvalidOperationException(
            "Resolved legacy active transfer did not leave the source explicitly temporary.");
    }
    Dictionary<uint, string?> legacyActiveTransferSelection =
        ReadSelectedPresetIds(legacyActiveTransferPath);
    if (!legacyActiveTransferSelection.TryGetValue(
            sourceCharacterHash,
            out string? legacyActiveTransferState) ||
        legacyActiveTransferState is not null)
    {
        throw new InvalidOperationException(
            "Resolved legacy active transfer did not persist explicit temporary state.");
    }
    Console.WriteLine("PRESET_SELECTION_LEGACY_ACTIVE_TRANSFER=PASS");

    string legacyActiveDeleteDirectory = Path.Combine(testRoot, "LegacyActiveDeleteConfig");
    Directory.CreateDirectory(legacyActiveDeleteDirectory);
    string legacyActiveDeletePath = Path.Combine(
        legacyActiveDeleteDirectory,
        "GBFR-ExtraSigilSlots.presets.json");
    File.WriteAllText(
        legacyActiveDeletePath,
        $$"""
        {
          "Version": 3,
          "Presets": [
            {
              "Id": "legacy-active-delete",
              "Name": "Legacy Active Delete",
              "CharacterHash": {{sourceCharacterHash}},
              "Slots": [93, 94]
            }
          ]
        }
        """,
        new UTF8Encoding(false));
    object legacyActiveDeleteStore = CreateStore(legacyActiveDeleteDirectory);
    object? legacyActiveDeletePreset = ResolveSelectedPreset(
        legacyActiveDeleteStore,
        sourceCharacterHash,
        [93, 94]);
    if (legacyActiveDeletePreset is null ||
        Id(legacyActiveDeletePreset) != "legacy-active-delete")
    {
        throw new InvalidOperationException(
            "Legacy active preset was not resolved before deletion.");
    }
    deleteMethod.Invoke(legacyActiveDeleteStore, [legacyActiveDeletePreset]);
    object legacyActiveDeleteReloaded = CreateStore(legacyActiveDeleteDirectory);
    object? legacySourceAfterDelete = ResolveSelectedPreset(
        legacyActiveDeleteReloaded,
        sourceCharacterHash,
        [93, 94]);
    if (legacySourceAfterDelete is not null)
    {
        throw new InvalidOperationException(
            "Resolved legacy active deletion did not leave the source explicitly temporary.");
    }
    Dictionary<uint, string?> legacyActiveDeleteSelection =
        ReadSelectedPresetIds(legacyActiveDeletePath);
    if (!legacyActiveDeleteSelection.TryGetValue(
            sourceCharacterHash,
            out string? legacyActiveDeleteState) ||
        legacyActiveDeleteState is not null)
    {
        throw new InvalidOperationException(
            "Resolved legacy active deletion did not persist explicit temporary state.");
    }
    Console.WriteLine("PRESET_SELECTION_LEGACY_ACTIVE_DELETE=PASS");

    string legacyTemporaryDirectory = Path.Combine(testRoot, "LegacyTemporaryConfig");
    Directory.CreateDirectory(legacyTemporaryDirectory);
    string legacyTemporaryPath = Path.Combine(legacyTemporaryDirectory, "GBFR-ExtraSigilSlots.presets.json");
    File.WriteAllText(
        legacyTemporaryPath,
        $$"""
        {
          "Version": 3,
          "Presets": [
            {
              "Id": "legacy-no-match",
              "Name": "Legacy No Match",
              "CharacterHash": {{sourceCharacterHash}},
              "Slots": [11, 22]
            }
          ]
        }
        """,
        new UTF8Encoding(false));
    object legacyTemporaryStore = CreateStore(legacyTemporaryDirectory);
    object? noMatchLegacyPreset = ResolveSelectedPreset(legacyTemporaryStore, sourceCharacterHash, [77]);
    if (noMatchLegacyPreset is not null)
        throw new InvalidOperationException("Legacy unresolved selection with no slot match was not made temporary.");
    Dictionary<uint, string?> noMatchSelection = ReadSelectedPresetIds(legacyTemporaryPath);
    if (!noMatchSelection.TryGetValue(sourceCharacterHash, out string? noMatchState) ||
        noMatchState is not null)
    {
        throw new InvalidOperationException("Legacy unresolved no-match state was not persisted as explicit null.");
    }
    object legacyTemporaryReloaded = CreateStore(legacyTemporaryDirectory);
    object? noMatchAfterReload = ResolveSelectedPreset(
        legacyTemporaryReloaded,
        sourceCharacterHash,
        [11, 22]);
    if (noMatchAfterReload is not null)
        throw new InvalidOperationException("Explicit temporary did not survive a later matching slot reload.");

    string deleteSelectionDirectory = Path.Combine(testRoot, "DeleteSelectionConfig");
    Directory.CreateDirectory(deleteSelectionDirectory);
    object deleteSelectionStore = CreateStore(deleteSelectionDirectory);
    object deleteKeepPreset = createMethod.Invoke(deleteSelectionStore, [sourceCharacterHash, "DeleteKeep"])!;
    object deleteActivePreset = createMethod.Invoke(deleteSelectionStore, [sourceCharacterHash, "DeleteActive"])!;
    deleteMethod.Invoke(deleteSelectionStore, [deleteActivePreset]);
    object deleteSelectionReloaded = CreateStore(deleteSelectionDirectory);
    object? sourceAfterDelete = ResolveSelectedPreset(
        deleteSelectionReloaded,
        sourceCharacterHash,
        Slots(deleteActivePreset));
    if (sourceAfterDelete is not null)
        throw new InvalidOperationException("Deleting the active preset did not make the source explicitly temporary.");
    Dictionary<uint, string?> deleteSelection =
        ReadSelectedPresetIds(Path.Combine(deleteSelectionDirectory, "GBFR-ExtraSigilSlots.presets.json"));
    if (!deleteSelection.TryGetValue(sourceCharacterHash, out string? deleteState) ||
        deleteState is not null)
    {
        throw new InvalidOperationException("Deleting the active preset did not persist explicit temporary state.");
    }
    object deleteInactivePreset = createMethod.Invoke(deleteSelectionReloaded, [sourceCharacterHash, "DeleteInactive"])!;
    SelectPreset(deleteSelectionReloaded, deleteKeepPreset);
    deleteMethod.Invoke(deleteSelectionReloaded, [deleteInactivePreset]);
    object deleteSelectionReloaded2 = CreateStore(deleteSelectionDirectory);
    object? sourceAfterDeleteInactive = ResolveSelectedPreset(
        deleteSelectionReloaded2,
        sourceCharacterHash,
        Slots(deleteKeepPreset));
    if (sourceAfterDeleteInactive is null || Id(sourceAfterDeleteInactive) != Id(deleteKeepPreset))
        throw new InvalidOperationException("Deleting a non-active preset changed the selected preset.");
    Console.WriteLine("PRESET_SELECTION_DELETE=PASS");

    string normalizeSelectionDirectory = Path.Combine(testRoot, "NormalizeSelectionConfig");
    Directory.CreateDirectory(normalizeSelectionDirectory);
    string normalizeSelectionPath = Path.Combine(normalizeSelectionDirectory, "GBFR-ExtraSigilSlots.presets.json");
    File.WriteAllText(
        normalizeSelectionPath,
        $$"""
        {
          "Version": 4,
          "Presets": [
            { "Id": "source-valid", "Name": "Source Valid", "CharacterHash": {{sourceCharacterHash}}, "Slots": [1] },
            { "Id": "target-valid", "Name": "Target Valid", "CharacterHash": {{targetCharacterHash}}, "Slots": [2] },
            { "Id": "duplicate", "Name": "Duplicate Source", "CharacterHash": {{sourceCharacterHash}}, "Slots": [3] },
            { "Id": "duplicate", "Name": "Duplicate Target", "CharacterHash": {{targetCharacterHash}}, "Slots": [4] }
          ],
          "SelectedPresetIdsByCharacter": {
            "0": "source-valid",
            "{{sourceCharacterHash}}": "source-valid",
            "{{targetCharacterHash}}": "duplicate",
            "{{thirdCharacterHash}}": "missing",
            "4294967295": "missing"
          }
        }
        """,
        new UTF8Encoding(false));
    object normalizeSelectionStore = CreateStore(normalizeSelectionDirectory);
    Dictionary<uint, string?> normalizedSelection = ReadSelectedPresetIds(normalizeSelectionPath);
    if (normalizedSelection.ContainsKey(0) ||
        !normalizedSelection.TryGetValue(sourceCharacterHash, out string? sourceValidState) ||
        sourceValidState != "source-valid" ||
        !normalizedSelection.TryGetValue(targetCharacterHash, out string? targetDuplicateState) ||
        targetDuplicateState is not null ||
        !normalizedSelection.TryGetValue(thirdCharacterHash, out string? missingState) ||
        missingState is not null ||
        !normalizedSelection.TryGetValue(4294967295u, out string? maxState) ||
        maxState is not null)
    {
        throw new InvalidOperationException("Selection normalization did not drop invalid keys and coerce invalid IDs to explicit null.");
    }
    if (Directory.GetFiles(normalizeSelectionDirectory, "*.pre-normalize-v4-*.bak").Length != 1)
        throw new InvalidOperationException("Selection normalization did not create a v4 backup.");
    Console.WriteLine("PRESET_SELECTION_NORMALIZATION=PASS");

Console.WriteLine("PRESET_STORE_TEST=PASS");
    Console.WriteLine("PRESET_SCHEMA=4");
    Console.WriteLine("PER_CHARACTER_PRESETS=PASS");
    Console.WriteLine("PRESET_SINGLE_TRANSFER=PASS");
    Console.WriteLine("PRESET_EMPTY_TRANSFER=PASS");
    Console.WriteLine("PRESET_REFERENCE_SCOPE=PASS");
    Console.WriteLine("PRESET_V1_V2_MIGRATION=PASS");
    Console.WriteLine("PRESET_MIGRATION_BACKUP=PASS");
    Console.WriteLine("PRESET_V3_NORMALIZATION=PASS");
    Console.WriteLine("PRESET_HIGH_SLOT_RETENTION=PASS");
    Console.WriteLine($"CHARACTER_NAME_MAP={expectedCharacters.Length}/{expectedCharacters.Length}");
    Console.WriteLine("CAPTAIN_SIGIL_COMPATIBILITY=PASS");
    Console.WriteLine("MANAGED_NUMCONFIG_CREATION=False");
    Console.WriteLine("ABI_VERSION=13");
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

object? ResolveSelectedPreset(object store, uint characterHash, uint[] currentSlots) =>
    resolveSelectedPresetMethod.Invoke(store, [characterHash, currentSlots]);
void SelectPreset(object store, object preset) =>
    selectPresetMethod.Invoke(store, [preset]);
void MarkTemporary(object store, uint characterHash) =>
    markTemporaryMethod.Invoke(store, [characterHash]);
bool IsSelectedPreset(object store, object preset) =>
    (bool)isSelectedPresetMethod.Invoke(store, [preset])!;

static Dictionary<uint, string?> ReadSelectedPresetIds(string path)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
    JsonElement root = document.RootElement;
    if (!root.TryGetProperty("SelectedPresetIdsByCharacter", out JsonElement selection))
        return [];
    Dictionary<uint, string?> result = [];
    foreach (JsonProperty property in selection.EnumerateObject())
    {
        result[uint.Parse(property.Name)] =
            property.Value.ValueKind == JsonValueKind.Null ? null : property.Value.GetString();
    }
    return result;
}
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

static void AssertV4Document(string path, int expectedPresetCount)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path, Encoding.UTF8));
    JsonElement root = document.RootElement;
    if (root.GetProperty("Version").GetInt32() != 4)
        throw new InvalidOperationException("Preset document was not upgraded to schema v4.");
    JsonElement presets = root.GetProperty("Presets");
    if (presets.GetArrayLength() != expectedPresetCount)
        throw new InvalidOperationException("Unexpected v4 preset count.");
    foreach (JsonElement preset in presets.EnumerateArray())
    {
        if (!preset.TryGetProperty("CharacterHash", out _) ||
            !preset.TryGetProperty("Slots", out JsonElement slots) ||
            slots.GetArrayLength() != 24 ||
            preset.TryGetProperty("Characters", out _))
        {
            throw new InvalidOperationException("A v4 preset retained the legacy global shape.");
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
