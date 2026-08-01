using System.Text;
using System.Text.Json;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal sealed class SigilPresetStore
{
    private const int CurrentVersion = 2;
    internal const int MaximumNameLength = 48;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private readonly string _path;
    private readonly Action<string> _log;
    private PresetDocument _document = new();
    private Dictionary<uint, string[]> _presetNamesBySlot = [];

    internal SigilPresetStore(string modDirectory, Action<string> log)
    {
        _path = Path.Combine(modDirectory, "GBFR-ExtraSigilSlots.presets.json");
        _log = log;
        Load();
    }

    internal IReadOnlyList<SigilPreset> Presets => _document.Presets;

    internal SigilPreset? FindById(string? id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        return _document.Presets.FirstOrDefault(
            preset => string.Equals(preset.Id, id, StringComparison.Ordinal));
    }

    internal bool NameExists(string name, string? exceptId = null)
    {
        return _document.Presets.Any(preset =>
            !string.Equals(preset.Id, exceptId, StringComparison.Ordinal) &&
            string.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    internal SigilPreset Create(string name)
    {
        name = NormalizeName(name);
        if (NameExists(name))
            throw new InvalidOperationException("A preset with that name already exists.");

        SigilPreset created = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Characters = CaptureCurrentSelections(),
        };
        Mutate(() => _document.Presets.Add(created));
        return created;
    }

    internal void Overwrite(SigilPreset preset)
    {
        Mutate(() => preset.Characters = CaptureCurrentSelections());
    }

    internal void Rename(SigilPreset preset, string name)
    {
        name = NormalizeName(name);
        if (NameExists(name, preset.Id))
            throw new InvalidOperationException("A preset with that name already exists.");
        Mutate(() => preset.Name = name);
    }

    internal void Delete(SigilPreset preset)
    {
        Mutate(() => _document.Presets.RemoveAll(candidate =>
            string.Equals(candidate.Id, preset.Id, StringComparison.Ordinal)));
    }

    internal IReadOnlyDictionary<uint, uint[]> GetSelections(SigilPreset preset)
    {
        Dictionary<uint, uint[]> result = [];
        foreach (SigilPresetCharacter character in preset.Characters)
        {
            if (character.CharacterHash == 0)
                continue;
            result[character.CharacterHash] = NormalizeSlots(character.Slots);
        }
        return result;
    }

    internal IReadOnlyList<string> GetPresetNamesForSlot(uint slotId)
    {
        if (slotId == 0 || !_presetNamesBySlot.TryGetValue(slotId, out string[]? names))
            return Array.Empty<string>();
        return names;
    }

    internal IReadOnlyList<string> RemoveSlotReferences(uint slotId)
    {
        if (slotId == 0)
            return Array.Empty<string>();

        List<string> affectedNames = [];
        Mutate(() =>
        {
            foreach (SigilPreset preset in _document.Presets)
            {
                bool affected = false;
                foreach (SigilPresetCharacter character in preset.Characters)
                {
                    for (int slot = 0; slot < character.Slots.Length; ++slot)
                    {
                        if (character.Slots[slot] != slotId)
                            continue;
                        character.Slots[slot] = 0;
                        affected = true;
                    }
                }
                if (affected)
                    affectedNames.Add(preset.Name);
            }
        });
        return affectedNames;
    }

    internal bool ClearSlotReferencesAndRun(
        uint slotId,
        Func<bool> action,
        out IReadOnlyList<string> affectedPresetNames)
    {
        PresetDocument backup = CloneDocument(_document);
        List<string> affectedNames = ClearSlotReferencesInMemory(slotId);
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

    private List<SigilPresetCharacter> CaptureCurrentSelections()
    {
        List<SigilPresetCharacter> characters =
            new(UiLocalization.KnownCharacterHashes.Length);
        foreach (uint characterHash in UiLocalization.KnownCharacterHashes)
        {
            characters.Add(new SigilPresetCharacter
            {
                CharacterHash = characterHash,
                Slots = NativeCore.GetSelection(characterHash),
            });
        }
        return characters;
    }

    private List<string> ClearSlotReferencesInMemory(uint slotId)
    {
        List<string> affectedNames = [];
        if (slotId == 0)
            return affectedNames;
        foreach (SigilPreset preset in _document.Presets)
        {
            bool affected = false;
            foreach (SigilPresetCharacter character in preset.Characters)
            {
                for (int slot = 0; slot < character.Slots.Length; ++slot)
                {
                    if (character.Slots[slot] != slotId)
                        continue;
                    character.Slots[slot] = 0;
                    affected = true;
                }
            }
            if (affected)
                affectedNames.Add(preset.Name);
        }
        return affectedNames;
    }

    private void Mutate(Action mutation)
    {
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
            PresetDocument? loaded = JsonSerializer.Deserialize<PresetDocument>(
                File.ReadAllText(_path, Encoding.UTF8),
                JsonOptions);
            _document = loaded ?? new PresetDocument();
            NormalizeDocument(_document);
            RebuildReferenceIndex();
            _log($"Loaded {_document.Presets.Count} sigil presets from {_path}.");
        }
        catch (Exception exception)
        {
            _document = new PresetDocument();
            RebuildReferenceIndex();
            _log(
                $"Could not load sigil presets from {_path}; " +
                $"the existing file was left untouched: {exception}");
        }
    }

    private void Save()
    {
        _document.Version = CurrentVersion;
        string directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        string temporaryPath = _path + ".tmp";
        string json = JsonSerializer.Serialize(_document, JsonOptions);
        File.WriteAllText(temporaryPath, json, new UTF8Encoding(false));
        File.Move(temporaryPath, _path, true);
    }

    private void RebuildReferenceIndex()
    {
        Dictionary<uint, List<string>> namesBySlot = [];
        foreach (SigilPreset preset in _document.Presets)
        {
            HashSet<uint> seenInPreset = [];
            foreach (SigilPresetCharacter character in preset.Characters)
            {
                foreach (uint slotId in character.Slots)
                {
                    if (slotId == 0 || !seenInPreset.Add(slotId))
                        continue;
                    if (!namesBySlot.TryGetValue(slotId, out List<string>? names))
                    {
                        names = [];
                        namesBySlot[slotId] = names;
                    }
                    names.Add(preset.Name);
                }
            }
        }
        _presetNamesBySlot = namesBySlot.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray());
    }

    private static void NormalizeDocument(PresetDocument document)
    {
        document.Version = CurrentVersion;
        document.Presets ??= [];
        HashSet<string> ids = new(StringComparer.Ordinal);
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        for (int presetIndex = 0; presetIndex < document.Presets.Count; ++presetIndex)
        {
            SigilPreset preset = document.Presets[presetIndex] ?? new SigilPreset();
            document.Presets[presetIndex] = preset;
            if (string.IsNullOrWhiteSpace(preset.Id) || !ids.Add(preset.Id))
            {
                preset.Id = Guid.NewGuid().ToString("N");
                ids.Add(preset.Id);
            }

            string baseName = string.IsNullOrWhiteSpace(preset.Name)
                ? $"Preset {presetIndex + 1}"
                : preset.Name.Trim();
            if (baseName.Length > MaximumNameLength)
                baseName = baseName[..MaximumNameLength];
            string uniqueName = baseName;
            int suffix = 2;
            while (!names.Add(uniqueName))
                uniqueName = $"{baseName} ({suffix++})";
            preset.Name = uniqueName;

            preset.Characters ??= [];
            Dictionary<uint, SigilPresetCharacter> byCharacter = [];
            foreach (SigilPresetCharacter character in preset.Characters)
            {
                if (character is null || character.CharacterHash == 0)
                    continue;
                byCharacter[character.CharacterHash] = new SigilPresetCharacter
                {
                    CharacterHash = character.CharacterHash,
                    Slots = NormalizeSlots(character.Slots),
                };
            }

            HashSet<uint> claimedSlotIds = [];
            preset.Characters = byCharacter.Values
                .OrderBy(character => character.CharacterHash)
                .Take(NativeCore.PresetCharacterCapacity)
                .ToList();
            foreach (SigilPresetCharacter character in preset.Characters)
            {
                for (int slot = 0; slot < character.Slots.Length; ++slot)
                {
                    uint slotId = character.Slots[slot];
                    if (slotId != 0 && !claimedSlotIds.Add(slotId))
                        character.Slots[slot] = 0;
                }
            }
        }
    }

    private static uint[] NormalizeSlots(uint[]? slots)
    {
        uint[] normalized = new uint[NativeCore.VirtualSlotCapacity];
        if (slots is not null)
            Array.Copy(slots, normalized, Math.Min(slots.Length, normalized.Length));
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
                Characters = preset.Characters.Select(character => new SigilPresetCharacter
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
    public List<SigilPresetCharacter> Characters { get; set; } = [];
}

internal sealed class SigilPresetCharacter
{
    public uint CharacterHash { get; set; }
    public uint[] Slots { get; set; } = new uint[NativeCore.VirtualSlotCapacity];
}
