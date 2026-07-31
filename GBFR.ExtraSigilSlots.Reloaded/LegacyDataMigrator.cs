namespace GBFR.ExtraSigilSlots.Reloaded;

internal static class LegacyDataMigrator
{
    private const string LegacyModDirectoryName = "GBFR.ExtraSigilSlots20.Reloaded";
    private const string PresetFileName = "GBFR-ExtraSigilSlots.presets.json";
    private const string LegacyPresetFileName = "GBFR-ExtraSigilSlots20.presets.json";

    internal static void Migrate(string modDirectory, Action<string> log)
    {
        try
        {
            string currentDirectory = Path.GetFullPath(modDirectory);
            string? modsDirectory = Directory.GetParent(currentDirectory)?.FullName;
            string? legacyDirectory = modsDirectory is null
                ? null
                : Path.Combine(modsDirectory, LegacyModDirectoryName);

            if (legacyDirectory is not null && Directory.Exists(legacyDirectory))
            {
                log(
                    $"Legacy mod directory detected at {legacyDirectory}; " +
                    "disable or remove its old ModId after migration to prevent double loading.");
            }

            MigratePresets(currentDirectory, legacyDirectory, log);
        }
        catch (Exception exception)
        {
            log($"Legacy data migration was skipped after an error: {exception}");
        }
    }

    private static void MigratePresets(
        string currentDirectory,
        string? legacyDirectory,
        Action<string> log)
    {
        string destination = Path.Combine(currentDirectory, PresetFileName);
        if (File.Exists(destination))
            return;

        foreach (string source in PresetCandidates(currentDirectory, legacyDirectory))
        {
            if (!File.Exists(source) || PathsEqual(source, destination))
                continue;

            File.Copy(source, destination, overwrite: false);
            log($"Migrated legacy named presets from {source}.");
            return;
        }
    }

    private static IEnumerable<string> PresetCandidates(
        string currentDirectory,
        string? legacyDirectory)
    {
        if (legacyDirectory is not null && Directory.Exists(legacyDirectory))
        {
            yield return Path.Combine(legacyDirectory, LegacyPresetFileName);
            yield return Path.Combine(legacyDirectory, PresetFileName);
        }
        yield return Path.Combine(currentDirectory, LegacyPresetFileName);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
}
