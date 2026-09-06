using System.Text.Json;

namespace GBFR.PreEquippedSigils;

/// <summary>
/// Reads the optional loadout.json (written by the player/editor tool) and
/// pushes it into the native runtime template table through the ABI.
/// No config file keeps the built-in 9-slot template; invalid files are
/// reported and the last valid configuration stays active.
///
/// Data model (2026 会话改版; mod parses only the fields it needs):
///   sigils.json  : { sigils: [ { gem, skill } ] } (name/sec/pool/special are tool-side only)
///   skills.json  : { traits: [ { hash, zh, en, cap } ] }
///   loadout.json : [ { items: [ {hash, level, zh, en}, {hash, level, zh, en}? ], enabled } ]
///                  items[0] = sigil (item), items[1] = secondary trait (optional).
/// Soft validation: any combination is accepted (no secondaries check yet);
/// hard validation only: unknown hashes / bad levels / too many slots.
/// </summary>
internal static class LoadoutConfig
{
    private const uint UnwornCharacterHash = 0x887AE0B0;
    private const int MaxSlots = 12; // conservative cap (more slots risk instability)
    private const int DefaultLevel = 15;

    private sealed class SigilInfo
    {
        public required uint Hash { get; init; }
        public required uint Skill { get; init; }
    }

    private sealed class TraitInfo
    {
        public int MaxLevel { get; init; } = DefaultLevel;
    }

    private sealed class ExclusiveRow
    {
        public required uint Hash { get; init; }
        public required string Player { get; init; } // PL code (e.g. PL1400)
        public required string Name { get; init; }
        public required string Zh { get; init; }
        public required uint T1 { get; init; }
        public required uint T2 { get; init; }
        public required uint War { get; init; }
    }

    private static readonly Dictionary<string, SigilInfo> Sigils = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, TraitInfo> Traits = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, List<ExclusiveRow>> ExclusiveByPlayer = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ExclusiveRow> ExclusiveByName = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<uint, ExclusiveRow> ExclusiveByHash = new();
    private static DateTime _lastAppliedUtc = DateTime.MinValue;
    private static DateTime _lastAttemptUtc = DateTime.MinValue;
    private static bool _hadConfigFile;
    private static string _loadoutPath = "";
    private static string _sigilsPath = "";
    private static string _traitsPath = "";

    internal static void Initialize(string modDirectory, Action<string> log)
    {
        // Player config lives in the user directory so mod updates (which
        // replace the mod folder) never wipe it. No config -> built-in template.
        _loadoutPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GBFRPreEquippedSigils", "loadout.json");
        _sigilsPath = Path.Combine(modDirectory, "sigils.json");
        _traitsPath = Path.Combine(modDirectory, "skills.json");
        LoadExclusiveTable(modDirectory, log);
        if (LoadTables(log))
            TryApply(log);
        else
            log("Custom loadout disabled: sigil/trait data files are missing or invalid.");
    }

    internal static void Tick(Action<string> log)
    {
        if (Traits.Count == 0)
            return;
        // A deletion must reach TryApply too: it restores the built-in template
        // (the file may be removed manually; the tool's reset now writes an
        // empty config instead of deleting). The old File.Exists gate made the
        // deletion branch in TryApply unreachable.
        if (File.Exists(_loadoutPath) &&
            File.GetLastWriteTimeUtc(_loadoutPath) == _lastAppliedUtc)
            return;
        TryApply(log);
    }

    private static bool LoadTables(Action<string> log)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(_sigilsPath));
            int count = 0;
            foreach (JsonElement entry in doc.RootElement.GetProperty("sigils").EnumerateArray())
            {
                try
                {
                    string gem = Hx(entry.GetProperty("gem"));
                    if (gem.Length == 0)
                        continue;
                    var info = new SigilInfo
                    {
                        Hash = PU(gem),
                        Skill = PU(Hx(entry.GetProperty("skill"))),
                    };
                    Sigils[gem] = info;
                    count++;
                }
                catch
                {
                    // one bad entry must not disable the whole table
                }
            }
            log($"Loaded {count} sigil entries.");
        }
        catch (Exception exception)
        {
            log($"Failed to load sigil table: {exception.Message}");
            return false;
        }

        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(_traitsPath));
            int count = 0;
            foreach (JsonElement entry in doc.RootElement.GetProperty("traits").EnumerateArray())
            {
                try
                {
                    string hash = Hx(entry.GetProperty("hash"));
                    if (hash.Length == 0)
                        continue;
                    // cap 与 extract/skills.json 的字段名保持一致（词条等级上限）。
                    int maxLevel = entry.TryGetProperty("cap", out JsonElement ml) &&
                                   ml.TryGetInt32(out int m)
                        ? m
                        : DefaultLevel;
                    Traits[hash] = new TraitInfo { MaxLevel = maxLevel };
                    count++;
                }
                catch
                {
                    // one bad entry must not disable the whole table
                }
            }
            log($"Loaded {count} trait dictionary entries.");
            return Traits.Count > 0 && Sigils.Count > 0;
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
                NativeCore.ApplyExclusiveOverrides(null); // everything enabled again
                if (NativeCore.ApplyCustomLoadout(null))
                    log("loadout.json removed; restored the built-in exclusive template.");
            }
            return;
        }
        _hadConfigFile = true;

        DateTime mtime = File.GetLastWriteTimeUtc(_loadoutPath);
        if (mtime == _lastAppliedUtc || mtime == _lastAttemptUtc)
            return;

        try
        {
            string json = File.ReadAllText(_loadoutPath);
            if (json.Length > 1024 * 1024)
                throw new InvalidDataException("loadout.json exceeds 1 MB");
            var overrides = ParseExclusiveOverrides(json);
            var slots = ParseAndValidate(json);
            bool ok;
            if (!NativeCore.ApplyExclusiveOverrides(overrides))
                throw new InvalidDataException("native rejected the exclusive overrides");
            if (slots.Count == 0)
            {
                // An existing (even empty) config means "no built-in general
                // slots": only the per-character exclusives stay active.
                log("loadout.json has no general slots; built-in exclusive template active.");
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
            _lastAttemptUtc = mtime;
        }
    }

    /// <summary>
    /// Loads character-exclusives.json (the tool's per-character exclusive
    /// table) so "exclusive" keys can be PL codes / character names as well as
    /// raw character hashes. Table missing or invalid only disables that
    /// convenience: raw hashes and the legacy t1/t2/war shape still work.
    /// </summary>
    private static void LoadExclusiveTable(string modDirectory, Action<string> log)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(modDirectory, "character-exclusives.json")));
            int count = 0;
            foreach (JsonElement entry in doc.RootElement.GetProperty("exclusives").EnumerateArray())
            {
                try
                {
                    var row = new ExclusiveRow
                    {
                        Hash = PU(Hx(entry.GetProperty("hash"))),
                        Player = Hx(entry.GetProperty("player")),
                        Name = Hx(entry.GetProperty("name")),
                        Zh = Hx(entry.GetProperty("zh")),
                        T1 = PU(Hx(entry.GetProperty("t1"))),
                        T2 = PU(Hx(entry.GetProperty("t2"))),
                        War = PU(Hx(entry.GetProperty("war"))),
                    };
                    if (row.Hash == 0 || row.Player.Length == 0)
                        continue;
                    if (ExclusiveByPlayer.TryGetValue(row.Player, out var playerRows))
                        playerRows.Add(row);
                    else
                        ExclusiveByPlayer[row.Player] = new List<ExclusiveRow> { row };
                    ExclusiveByHash[row.Hash] = row;
                    if (row.Name.Length > 0)
                        ExclusiveByName[row.Name] = row;
                    if (row.Zh.Length > 0)
                        ExclusiveByName[row.Zh] = row;
                    count++;
                }
                catch
                {
                    // one bad entry must not disable the whole table
                }
            }
            log($"Loaded {count} character exclusive entries.");
        }
        catch (Exception exception)
        {
            log($"Failed to load character-exclusive table: {exception.Message}");
        }
    }

    /// Returns every row matching the key; player codes may be shared
    /// (Gran/Djeeta are both "PL0000"). Empty = key not in the table.
    private static List<ExclusiveRow> ResolveCharacters(string key)
    {
        if (ExclusiveByPlayer.TryGetValue(key, out var playerRows))
            return playerRows;
        if (ExclusiveByHash.TryGetValue(PU(key), out ExclusiveRow? row))
            return new List<ExclusiveRow> { row };
        if (ExclusiveByName.TryGetValue(key, out row))
            return new List<ExclusiveRow> { row };
        return new List<ExclusiveRow>();
    }

    private static void AddExclusiveOverride(
        JsonElement fields, ExclusiveRow? row, uint hash,
        List<NativeCore.ExclusiveOverrideNative> result)
    {
        bool t1 = true;
        bool t2 = true;
        bool war = true;
        foreach (JsonProperty field in fields.EnumerateObject())
        {
            if (field.Value.ValueKind != JsonValueKind.True &&
                field.Value.ValueKind != JsonValueKind.False)
                continue;
            bool value = field.Value.GetBoolean();
            uint traitHash = PU(field.Name);
            if (row != null && traitHash == row.T1)
                t1 = value;
            else if (row != null && traitHash == row.T2)
                t2 = value;
            else if (row != null && traitHash == row.War)
                war = value;
            else
            {
                switch (field.Name)
                {
                    case "t1": t1 = value; break;
                    case "t2": t2 = value; break;
                    case "war": war = value; break;
                }
            }
        }
        result.Add(new NativeCore.ExclusiveOverrideNative
        {
            CharacterHash = hash,
            DisableT1 = t1 ? (byte)0 : (byte)1,
            DisableT2 = t2 ? (byte)0 : (byte)1,
            DisableWar = war ? (byte)0 : (byte)1,
            Reserved = 0,
        });
    }

    /// <summary>
    /// Parses the optional "exclusive" object
    /// ({ PL码/name/hash: { 词条hash(T1/T2/War): bool } }) into native overrides
    /// (disable bits). Missing entries stay enabled; the legacy shape
    /// ({ characterHashHex: { t1, t2, war } }) is still accepted; absent
    /// "exclusive" yields null (all enabled).
    /// </summary>
    private static NativeCore.ExclusiveOverrideNative[]? ParseExclusiveOverrides(string json)
    {
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("exclusive", out JsonElement exclusive) ||
            exclusive.ValueKind != JsonValueKind.Object)
            return null;

        var result = new List<NativeCore.ExclusiveOverrideNative>();
        foreach (JsonProperty property in exclusive.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.Object)
                continue;
            // A player key may match several rows (Gran/Djeeta share "PL0000"):
            // emit one override per character. Unknown keys fall back to a raw
            // character hash (legacy configs).
            List<ExclusiveRow> rows = ResolveCharacters(property.Name);
            if (rows.Count == 0)
            {
                uint bareHash = PU(property.Name);
                if (bareHash == 0)
                    continue;
                AddExclusiveOverride(property.Value, null, bareHash, result);
                continue;
            }
            foreach (ExclusiveRow row in rows)
                AddExclusiveOverride(property.Value, row, row.Hash, result);
        }
        return result.Count == 0 ? null : result.ToArray();
    }

    private static List<NativeCore.TemplateSlotNative> ParseAndValidate(string json)
    {
        var result = new List<NativeCore.TemplateSlotNative>();
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;
        if (root.ValueKind == JsonValueKind.Object)
        {
            // new config shape: { lang, slots: [...] } — lang is tool-side only
            if (!root.TryGetProperty("slots", out JsonElement slotsEl) ||
                slotsEl.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("missing 'slots' array");
            root = slotsEl;
        }
        else if (root.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("expected an array of slots");
        }

        int index = 0;
        foreach (JsonElement slot in root.EnumerateArray())
        {
            index++;
            bool enabled = !slot.TryGetProperty("enabled", out JsonElement enabledElement) ||
                           enabledElement.GetBoolean();
            if (!enabled)
                continue;
            if (result.Count >= MaxSlots)
                throw new InvalidDataException($"more than {MaxSlots} enabled slots");

            if (!slot.TryGetProperty("items", out JsonElement items) ||
                items.ValueKind != JsonValueKind.Array || items.GetArrayLength() < 1)
                throw new InvalidDataException($"slot {index}: missing 'items' array");

            JsonElement main = items[0];
            string mainGem = Hx(main.GetProperty("gem"));
            if (!Sigils.TryGetValue(mainGem, out SigilInfo? sigil))
                throw new InvalidDataException($"slot {index}: unknown sigil '{mainGem}'");
            int mainCap = Traits.TryGetValue($"{sigil.Skill:X8}", out TraitInfo? mt)
                ? mt.MaxLevel
                : DefaultLevel;
            int level1 = GetLevel(main, "level", index, mainCap);

            if (items.GetArrayLength() >= 2)
            {
                JsonElement sec = items[1];
                string secHash = Hx(sec.GetProperty("hash"));
                if (!Traits.TryGetValue(secHash, out TraitInfo? trait))
                    throw new InvalidDataException($"slot {index}: unknown trait '{secHash}'");
                int level2 = GetLevel(sec, "level", index, trait.MaxLevel);
                result.Add(new NativeCore.TemplateSlotNative
                {
                    GemId = sigil.Hash,
                    Trait1 = sigil.Skill,
                    Trait1Level = level1,
                    Trait2 = PU(secHash),
                    Trait2Level = level2,
                    SigilLevel = level1,
                });
            }
            else
            {
                result.Add(new NativeCore.TemplateSlotNative
                {
                    GemId = sigil.Hash,
                    Trait1 = sigil.Skill,
                    Trait1Level = level1,
                    Trait2 = UnwornCharacterHash, // "not selected" sentinel, never 0
                    Trait2Level = 0,
                    SigilLevel = level1,
                });
            }
        }
        return result;
    }

    private static int GetLevel(JsonElement item, string propertyName, int index, int maxLevel)
    {
        if (!item.TryGetProperty(propertyName, out JsonElement element))
            return DefaultLevel;
        int level = element.GetInt32();
        if (level < 0 || level > maxLevel)
            throw new InvalidDataException($"slot {index}: {propertyName} out of range 0-{maxLevel}");
        return level;
    }

    private static string Hx(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? (e.GetString() ?? "").Trim().ToUpperInvariant() : "";

    private static uint PU(string hex)
    {
        try { return Convert.ToUInt32(hex, 16); }
        catch { return 0; }
    }
}
