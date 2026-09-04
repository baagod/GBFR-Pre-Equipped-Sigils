using System.Text.Json;

namespace GBFR.PreEquippedSigils;

/// <summary>
/// Reads the optional loadout.json (written by the player/editor tool) and
/// pushes it into the native runtime template table through the ABI.
/// No config file keeps the built-in 9-slot template; invalid files are
/// reported and the last valid configuration stays active.
/// </summary>
internal static class LoadoutConfig
{
    private const uint UnwornCharacterHash = 0x887AE0B0;
    private const uint FallbackGem = 0x335DA2A5; // Guts V+ (known-good display item)
    private const int MaxSlots = 12; // conservative cap (more slots risk instability)
    private const int DefaultLevel = 15;

    private sealed class TraitInfo
    {
        public required uint Hash { get; init; }
        public required uint Gem { get; init; }
        public int MaxLevel { get; init; } = 15;
    }

    private static readonly Dictionary<string, TraitInfo> Traits = new(StringComparer.Ordinal);
    private static DateTime _lastAppliedUtc = DateTime.MinValue;
    private static DateTime _lastAttemptUtc = DateTime.MinValue;
    private static bool _hadConfigFile;
    private static string _loadoutPath = "";
    private static string _traitsPath = "";

    internal static void Initialize(string modDirectory, Action<string> log)
    {
        _loadoutPath = Path.Combine(modDirectory, "loadout.json");
        _traitsPath = Path.Combine(modDirectory, "traits.json");
        if (LoadTraits(log))
            TryApply(log);
        else
            log("Custom loadout disabled: trait dictionary is missing or invalid.");
    }

    internal static void Tick(Action<string> log)
    {
        if (Traits.Count == 0)
            return;
        if (File.Exists(_loadoutPath) &&
            File.GetLastWriteTimeUtc(_loadoutPath) != _lastAppliedUtc)
            TryApply(log);
    }

    private static bool LoadTraits(Action<string> log)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(_traitsPath));
            int count = 0, skipped = 0;
            foreach (JsonElement entry in doc.RootElement.GetProperty("traits").EnumerateArray())
            {
                try
                {
                    string nameZh = entry.GetProperty("zh").GetString() ?? "";
                    if (nameZh.Length == 0)
                        continue;
                    uint hash = Convert.ToUInt32(entry.GetProperty("hash").GetString() ?? "0", 16);
                    uint gem = Convert.ToUInt32(entry.GetProperty("gem").GetString() ?? "0", 16);
                    int maxLevel = entry.TryGetProperty("maxLevel", out JsonElement ml) &&
                                   ml.TryGetInt32(out int m)
                        ? m
                        : DefaultLevel;
                    Traits[nameZh] = new TraitInfo
                    {
                        Hash = hash,
                        Gem = gem == 0 ? FallbackGem : gem,
                        MaxLevel = maxLevel,
                    };
                    count++;
                }
                catch
                {
                    skipped++; // one bad entry must not disable the whole dictionary
                }
            }
            log($"Loaded {count} trait dictionary entries{(skipped > 0 ? $" ({skipped} skipped)" : "")}.");
            return Traits.Count > 0;
        }
        catch (Exception exception)
        {
            log($"Failed to load trait dictionary: {exception.Message}");
            return false;
        }
    }

    private static void TryApply(Action<string> log)
    {
        if (!File.Exists(_loadoutPath))
        {
            if (_hadConfigFile)
            {
                _hadConfigFile = false;
                _lastAppliedUtc = DateTime.MinValue;
                if (NativeCore.ApplyCustomLoadout(null))
                    log("loadout.json removed; restored the built-in template.");
            }
            return;
        }
        _hadConfigFile = true;

        DateTime mtime = File.GetLastWriteTimeUtc(_loadoutPath);
        if (mtime == _lastAppliedUtc || mtime == _lastAttemptUtc)
            return; // already applied, or already tried for this mtime

        try
        {
            var slots = ParseAndValidate(File.ReadAllText(_loadoutPath), log);
            bool ok;
            if (slots.Count == 0)
            {
                log("loadout.json has no enabled slots; restoring the built-in template.");
                ok = NativeCore.ApplyCustomLoadout(null);
            }
            else
            {
                ok = NativeCore.ApplyCustomLoadout(slots.ToArray());
                if (ok)
                    log($"Applied custom loadout with {slots.Count} slot(s).");
            }
            if (ok)
            {
                _lastAppliedUtc = mtime;
                _lastAttemptUtc = DateTime.MinValue;
            }
            else
            {
                log("Native rejected the custom loadout; kept previous configuration.");
                _lastAttemptUtc = mtime;
            }
        }
        catch (Exception exception)
        {
            log($"Invalid loadout.json; kept previous configuration: {exception.Message}");
            _lastAttemptUtc = mtime; // no log flood; retried only when the file changes
        }
    }

    private static List<NativeCore.TemplateSlotNative> ParseAndValidate(string json, Action<string> log)
    {
        var result = new List<NativeCore.TemplateSlotNative>();
        using JsonDocument doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("slots", out JsonElement slotsElement) ||
            slotsElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("missing 'slots' array");

        int index = 0;
        foreach (JsonElement slot in slotsElement.EnumerateArray())
        {
            index++;
            bool enabled = !slot.TryGetProperty("enabled", out JsonElement enabledElement) ||
                           enabledElement.GetBoolean();
            if (!enabled)
                continue;
            if (result.Count >= MaxSlots)
                throw new InvalidDataException($"more than {MaxSlots} enabled slots");

            string trait1 = slot.TryGetProperty("trait1", out JsonElement t1)
                ? t1.GetString() ?? ""
                : "";
            if (!Traits.TryGetValue(trait1, out TraitInfo? info1))
                throw new InvalidDataException($"slot {index}: unknown trait '{trait1}'");

            int level1 = GetLevel(slot, "level1", index, info1.MaxLevel);
            string trait2 = slot.TryGetProperty("trait2", out JsonElement t2)
                ? t2.GetString() ?? ""
                : "";
            if (trait2.Length == 0)
            {
                result.Add(new NativeCore.TemplateSlotNative
                {
                    GemId = info1.Gem,
                    Trait1 = info1.Hash,
                    Trait1Level = level1,
                    Trait2 = UnwornCharacterHash, // "not selected" sentinel, never 0
                    Trait2Level = 0,
                    SigilLevel = level1,
                });
            }
            else
            {
                if (!Traits.TryGetValue(trait2, out TraitInfo? info2))
                    throw new InvalidDataException($"slot {index}: unknown trait '{trait2}'");
                int level2 = GetLevel(slot, "level2", index, info2.MaxLevel);
                result.Add(new NativeCore.TemplateSlotNative
                {
                    GemId = info1.Gem,
                    Trait1 = info1.Hash,
                    Trait1Level = level1,
                    Trait2 = info2.Hash,
                    Trait2Level = level2,
                    SigilLevel = level1,
                });
            }
        }
        return result;
    }

    private static int GetLevel(JsonElement slot, string propertyName, int index, int maxLevel)
    {
        if (!slot.TryGetProperty(propertyName, out JsonElement element))
            return DefaultLevel;
        int level = element.GetInt32();
        if (level < 0 || level > maxLevel)
            throw new InvalidDataException($"slot {index}: {propertyName} out of range 0-{maxLevel}");
        return level;
    }
}
