using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal sealed class SigilPresetStore
{
    private const int CurrentVersion = 3;
    internal const int MaximumNameLength = 48;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly Action<string> _log;
    private PresetDocument _document = new();
    private Dictionary<(uint CharacterHash, uint SlotId), string[]> _presetNamesBySlot = [];
    private bool _loadFailed;

    internal SigilPresetStore(string modDirectory, Action<string> log)
    {
        _path = Path.Combine(modDirectory, "GBFR-ExtraSigilSlots.presets.json");
        _log = log;
        Load();
    }

    internal IReadOnlyList<SigilPreset> Presets => _document.Presets;

    internal IReadOnlyList<SigilPreset> GetPresetsForCharacter(uint characterHash)
    {
        if (characterHash == 0)
            return Array.Empty<SigilPreset>();
        return _document.Presets
            .Where(preset => preset.CharacterHash == characterHash)
            .ToArray();
    }

    internal int GetPresetCount(uint characterHash)
    {
        return _document.Presets.Count(preset => preset.CharacterHash == characterHash);
    }

    internal SigilPreset? FindById(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        return _document.Presets.FirstOrDefault(
            preset => string.Equals(preset.Id, id, StringComparison.Ordinal));
    }

    internal bool NameExists(uint characterHash, string name, string? exceptId = null)
    {
        return _document.Presets.Any(preset =>
            preset.CharacterHash == characterHash &&
            !string.Equals(preset.Id, exceptId, StringComparison.Ordinal) &&
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    internal SigilPreset Create(uint characterHash, string name)
    {
        if (characterHash == 0)
            throw new InvalidOperationException("A preset must belong to a valid character.");
        name = NormalizeName(name);
        if (NameExists(characterHash, name))
            throw new InvalidOperationException("A preset with that name already exists for this character.");

        SigilPreset created = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            CharacterHash = characterHash,
            Slots = NativeCore.GetSelection(characterHash),
        };
        Mutate(() => _document.Presets.Add(created));
        return created;
    }

    internal void Overwrite(SigilPreset preset)
    {
        if (preset.CharacterHash == 0)
            throw new InvalidOperationException("The preset has no owning character.");
        Mutate(() => preset.Slots = NativeCore.GetSelection(preset.CharacterHash));
    }

    internal void Rename(SigilPreset preset, string name)
    {
        name = NormalizeName(name);
        if (NameExists(preset.CharacterHash, name, preset.Id))
            throw new InvalidOperationException("A preset with that name already exists for this character.");
        Mutate(() => preset.Name = name);
    }

    internal void Delete(SigilPreset preset)
    {
        Mutate(() => _document.Presets.RemoveAll(candidate =>
            string.Equals(candidate.Id, preset.Id, StringComparison.Ordinal)));
    }

    internal void TransferPreset(SigilPreset preset, uint targetCharacterHash)
    {
        if (preset.CharacterHash == 0 || targetCharacterHash == 0 ||
            preset.CharacterHash == targetCharacterHash)
        {
            throw new InvalidOperationException(
                "Source and target characters must be different valid characters.");
        }
        if (NameExists(targetCharacterHash, preset.Name, preset.Id))
        {
            throw new InvalidOperationException(
                "The target character already has a preset with that name.");
        }

        Mutate(() => preset.CharacterHash = targetCharacterHash);
    }

    internal IReadOnlyDictionary<uint, uint[]> GetSelections(SigilPreset preset)
    {
        if (preset.CharacterHash == 0)
            return new Dictionary<uint, uint[]>();
        return new Dictionary<uint, uint[]>
        {
            [preset.CharacterHash] = NormalizeSlots(preset.Slots),
        };
    }

    internal IReadOnlyList<string> GetPresetNamesForSlot(uint slotId)
    {
        if (slotId == 0)
            return Array.Empty<string>();
        return _presetNamesBySlot
            .Where(pair => pair.Key.SlotId == slotId)
            .SelectMany(pair => pair.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    internal IReadOnlyList<string> GetPresetNamesForSlot(uint characterHash, uint slotId)
    {
        if (characterHash == 0 || slotId == 0 ||
            !_presetNamesBySlot.TryGetValue((characterHash, slotId), out string[]? names))
        {
            return Array.Empty<string>();
        }
        return names;
    }

    internal IReadOnlyList<string> RemoveSlotReferences(uint characterHash, uint slotId)
    {
        if (characterHash == 0 || slotId == 0)
            return Array.Empty<string>();

        List<string> affectedNames = [];
        Mutate(() => affectedNames = ClearSlotReferencesInMemory(characterHash, slotId));
        return affectedNames;
    }

    internal bool ClearSlotReferencesAndRun(
        uint characterHash,
        uint slotId,
        Func<bool> action,
        out IReadOnlyList<string> affectedPresetNames)
    {
        PresetDocument backup = CloneDocument(_document);
        List<string> affectedNames = ClearSlotReferencesInMemory(characterHash, slotId);
        affectedPresetNames = affectedNames;
        if (affectedNames.Count == 0)
            return action();

        bool clearedFilePersisted = false;
        try
        {
            NormalizeDocument(_document);
            Save();
            clearedFilePersisted = true;
            RebuildReferenceIndex();
            if (action())
                return true;

            _document = backup;
            Save();
            RebuildReferenceIndex();
            return false;
        }
        catch
        {
            _document = backup;
            RebuildReferenceIndex();
            if (clearedFilePersisted)
                Save();
            throw;
        }
    }

    private List<string> ClearSlotReferencesInMemory(uint characterHash, uint slotId)
    {
        List<string> affectedNames = [];
        if (characterHash == 0 || slotId == 0)
            return affectedNames;
        foreach (SigilPreset preset in _document.Presets)
        {
            if (preset.CharacterHash != characterHash)
                continue;
            bool affected = false;
            for (int slot = 0; slot < preset.Slots.Length; ++slot)
            {
                if (preset.Slots[slot] != slotId)
                    continue;
                preset.Slots[slot] = 0;
                affected = true;
            }
            if (affected)
                affectedNames.Add(preset.Name);
        }
        return affectedNames;
    }

    private void Mutate(Action mutation)
    {
        if (_loadFailed)
        {
            throw new InvalidOperationException(
                "Preset storage could not be loaded, so it will not be overwritten.");
        }
        PresetDocument backup = CloneDocument(_document);
        try
        {
            mutation();
            NormalizeDocument(_document);
            Save();
            RebuildReferenceIndex();
        }
        catch
        {
            _document = backup;
            RebuildReferenceIndex();
            throw;
        }
    }

    private void Load()
    {
        if (!File.Exists(_path))
        {
            RebuildReferenceIndex();
            _log($"No sigil preset file found at {_path}; starting with an empty preset list.");
            return;
        }

        try
        {
            byte[] originalBytes = File.ReadAllBytes(_path);
            PresetDocument? loaded = JsonSerializer.Deserialize<PresetDocument>(
                originalBytes,
                JsonOptions);
            loaded ??= new PresetDocument();
            bool needsMigration = loaded.Version < CurrentVersion ||
                (loaded.Presets?.Any(preset => preset?.Characters is not null) ?? false);
            string beforeNormalization = JsonSerializer.Serialize(loaded, JsonOptions);
            _document = needsMigration ? MigrateLegacyDocument(loaded) : loaded;
            NormalizeDocument(_document);
            bool needsNormalizationSave = !needsMigration &&
                !string.Equals(
                    beforeNormalization,
                    JsonSerializer.Serialize(_document, JsonOptions),
                    StringComparison.Ordinal);

            if (needsMigration || needsNormalizationSave)
            {
                string backupPath = BackupOriginal(
                    originalBytes,
                    needsMigration ? "pre-v3" : "pre-normalize-v3");
                Save();
                _log(needsMigration
                    ? $"Migrated sigil presets to per-character format v{CurrentVersion}; " +
                        $"the previous file was backed up at {backupPath}."
                    : $"Normalized sigil preset data; " +
                        $"the previous file was backed up at {backupPath}.");
            }

            _loadFailed = false;
            RebuildReferenceIndex();
            _log($"Loaded {_document.Presets.Count} sigil presets from {_path}.");
        }
        catch (Exception exception)
        {
            _document = new PresetDocument();
            _loadFailed = true;
            RebuildReferenceIndex();
            _log(
                $"Could not load sigil presets from {_path}; " +
                $"the existing file was left untouched: {exception}");
        }
    }

    private string BackupOriginal(byte[] originalBytes, string reason)
    {
        string digest = Convert.ToHexString(SHA256.HashData(originalBytes))
            .ToLowerInvariant()[..16];
        string backupPath = _path + $".{reason}-{digest}.bak";
        if (File.Exists(backupPath))
        {
            if (File.ReadAllBytes(backupPath).SequenceEqual(originalBytes))
                return backupPath;
            throw new IOException($"Existing preset backup does not match {_path}.");
        }

        string temporaryPath = backupPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllBytes(temporaryPath, originalBytes);
            File.Move(temporaryPath, backupPath, false);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
        return backupPath;
    }

    private void Save()
    {
        _document.Version = CurrentVersion;
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = _path + ".tmp";
        string json = JsonSerializer.Serialize(_document, JsonOptions);
        try
        {
            File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
            File.Move(temporaryPath, _path, true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private void RebuildReferenceIndex()
    {
        Dictionary<(uint CharacterHash, uint SlotId), List<string>> namesBySlot = [];
        foreach (SigilPreset preset in _document.Presets)
        {
            HashSet<uint> seenInPreset = [];
            foreach (uint slotId in preset.Slots)
            {
                if (slotId == 0 || !seenInPreset.Add(slotId))
                    continue;
                var key = (preset.CharacterHash, slotId);
                if (!namesBySlot.TryGetValue(key, out List<string>? names))
                {
                    names = [];
                    namesBySlot[key] = names;
                }
                names.Add(preset.Name);
            }
        }
        _presetNamesBySlot = namesBySlot.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray());
    }

    private static PresetDocument MigrateLegacyDocument(PresetDocument legacy)
    {
        PresetDocument migrated = new() { Version = CurrentVersion };
        foreach (SigilPreset legacyPreset in legacy.Presets ?? [])
        {
            if (legacyPreset is null)
                continue;

            if (legacyPreset.Characters is null)
            {
                if (legacyPreset.CharacterHash != 0)
                {
                    migrated.Presets.Add(new SigilPreset
                    {
                        Id = legacyPreset.Id,
                        Name = legacyPreset.Name,
                        CharacterHash = legacyPreset.CharacterHash,
                        Slots = NormalizeSlots(legacyPreset.Slots),
                    });
                }
                continue;
            }

            Dictionary<uint, uint[]> byCharacter = [];
            foreach (SigilPresetCharacter character in legacyPreset.Characters)
            {
                if (character is null || character.CharacterHash == 0)
                    continue;
                byCharacter[character.CharacterHash] = NormalizeSlots(character.Slots);
            }

            HashSet<uint> claimedSlotIds = [];
            bool preservedLegacyId = false;
            KeyValuePair<uint, uint[]>[] orderedCharacters = byCharacter
                .OrderBy(pair => pair.Key)
                .ToArray();
            foreach ((uint characterHash, uint[] slots) in orderedCharacters)
            {
                for (int slot = 0; slot < slots.Length; ++slot)
                {
                    uint slotId = slots[slot];
                    if (slotId != 0 && !claimedSlotIds.Add(slotId))
                        slots[slot] = 0;
                }
                if (!slots.Any(slotId => slotId != 0))
                    continue;

                migrated.Presets.Add(new SigilPreset
                {
                    Id = !preservedLegacyId ? legacyPreset.Id : Guid.NewGuid().ToString("N"),
                    Name = legacyPreset.Name,
                    CharacterHash = characterHash,
                    Slots = slots,
                });
                preservedLegacyId = true;
            }
            if (!preservedLegacyId && orderedCharacters.Length != 0)
            {
                migrated.Presets.Add(new SigilPreset
                {
                    Id = legacyPreset.Id,
                    Name = legacyPreset.Name,
                    CharacterHash = orderedCharacters[0].Key,
                    Slots = orderedCharacters[0].Value,
                });
            }
        }
        return migrated;
    }

    private static void NormalizeDocument(PresetDocument document)
    {
        document.Version = CurrentVersion;
        document.Presets ??= [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        Dictionary<uint, HashSet<string>> namesByCharacter = [];
        List<SigilPreset> normalizedPresets = [];

        for (int presetIndex = 0; presetIndex < document.Presets.Count; ++presetIndex)
        {
            SigilPreset? preset = document.Presets[presetIndex];
            if (preset is null || preset.CharacterHash == 0)
                continue;
            if (string.IsNullOrWhiteSpace(preset.Id) || !ids.Add(preset.Id))
            {
                do
                {
                    preset.Id = Guid.NewGuid().ToString("N");
                } while (!ids.Add(preset.Id));
            }

            string baseName = string.IsNullOrWhiteSpace(preset.Name)
                ? $"Preset {presetIndex + 1}"
                : preset.Name.Trim();
            if (baseName.Length > MaximumNameLength)
                baseName = baseName[..MaximumNameLength];
            if (!namesByCharacter.TryGetValue(
                    preset.CharacterHash,
                    out HashSet<string>? characterNames))
            {
                characterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                namesByCharacter[preset.CharacterHash] = characterNames;
            }
            preset.Name = MakeUniqueName(baseName, characterNames);
            preset.Slots = NormalizeSlots(preset.Slots);
            preset.Characters = null;
            normalizedPresets.Add(preset);
        }
        document.Presets = normalizedPresets;
    }

    private static string MakeUniqueName(string baseName, HashSet<string> names)
    {
        if (names.Add(baseName))
            return baseName;
        for (int suffix = 2; ; ++suffix)
        {
            string suffixText = $" ({suffix})";
            int baseLength = Math.Min(baseName.Length, MaximumNameLength - suffixText.Length);
            string candidate = baseName[..baseLength] + suffixText;
            if (names.Add(candidate))
                return candidate;
        }
    }

    private static uint[] NormalizeSlots(uint[]? slots)
    {
        uint[] normalized = new uint[NativeCore.VirtualSlotCapacity];
        if (slots is not null)
            Array.Copy(slots, normalized, Math.Min(slots.Length, normalized.Length));
        HashSet<uint> seenSlotIds = [];
        for (int slot = 0; slot < normalized.Length; ++slot)
        {
            uint slotId = normalized[slot];
            if (slotId != 0 && !seenSlotIds.Add(slotId))
                normalized[slot] = 0;
        }
        return normalized;
    }

    private static string NormalizeName(string name)
    {
        name = name.Trim();
        if (name.Length == 0)
            throw new InvalidOperationException("Preset name cannot be empty.");
        if (name.Length > MaximumNameLength)
            throw new InvalidOperationException("Preset name is too long.");
        return name;
    }

    private static PresetDocument CloneDocument(PresetDocument source)
    {
        return new PresetDocument
        {
            Version = source.Version,
            Presets = source.Presets.Select(preset => new SigilPreset
            {
                Id = preset.Id,
                Name = preset.Name,
                CharacterHash = preset.CharacterHash,
                Slots = NormalizeSlots(preset.Slots),
                Characters = preset.Characters?.Select(character => new SigilPresetCharacter
                {
                    CharacterHash = character.CharacterHash,
                    Slots = NormalizeSlots(character.Slots),
                }).ToList(),
            }).ToList(),
        };
    }
}

internal sealed class PresetDocument
{
    public int Version { get; set; } = 2;
    public List<SigilPreset> Presets { get; set; } = [];
}

internal sealed class SigilPreset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public uint CharacterHash { get; set; }
    public uint[] Slots { get; set; } = new uint[NativeCore.VirtualSlotCapacity];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<SigilPresetCharacter>? Characters { get; set; }
}

internal sealed class SigilPresetCharacter
{
    public uint CharacterHash { get; set; }
    public uint[] Slots { get; set; } = new uint[NativeCore.VirtualSlotCapacity];
}
