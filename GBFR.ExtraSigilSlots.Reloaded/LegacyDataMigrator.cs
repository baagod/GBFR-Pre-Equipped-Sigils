using System.Security.Cryptography;
using System.Text.Json;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal static class LegacyDataMigrator
{
    private const long MaximumPresetFileBytes = 16L * 1024L * 1024L;
    private const string LegacyModDirectoryName = "GBFR.ExtraSigilSlots20.Reloaded";
    private const string PresetFileName = "GBFR-ExtraSigilSlots.presets.json";
    private const string LegacyPresetFileName = "GBFR-ExtraSigilSlots20.presets.json";
    private static readonly JsonSerializerOptions PresetJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static void Migrate(
        string modDirectory,
        string configDirectory,
        Action<string> log)
    {
        try
        {
            string currentDirectory = Path.GetFullPath(modDirectory);
            string persistentDirectory = Path.GetFullPath(configDirectory);
            Directory.CreateDirectory(persistentDirectory);
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

            MigratePresets(
                currentDirectory,
                persistentDirectory,
                legacyDirectory,
                log);
        }
        catch (Exception exception)
        {
            log($"Legacy data migration was skipped after an error: {exception}");
        }
    }

    private static void MigratePresets(
        string currentDirectory,
        string persistentDirectory,
        string? legacyDirectory,
        Action<string> log)
    {
        string destination = Path.Combine(persistentDirectory, PresetFileName);
        bool destinationExists = File.Exists(destination);
        if (destinationExists &&
            TryReadPresetData(destination, out _, out _, out _))
        {
            return;
        }

        foreach (string source in PresetCandidates(currentDirectory, legacyDirectory))
        {
            if (!File.Exists(source) || PathsEqual(source, destination))
                continue;
            if (!TryReadPresetData(
                    source,
                    out byte[] presetData,
                    out int presetCount,
                    out string validationError))
            {
                log($"Skipped invalid preset migration candidate {source}: {validationError}.");
                continue;
            }

            if (destinationExists)
                BackupInvalidPresetFile(destination, log);

            WriteAtomically(presetData, destination);
            log(
                $"Migrated {presetCount} named presets from {source} " +
                $"to persistent Reloaded-II storage {destination}.");
            return;
        }

        if (destinationExists)
        {
            BackupInvalidPresetFile(destination, log);
            log(
                $"The persistent preset file at {destination} is invalid and no valid " +
                "legacy copy was found; it was left untouched for manual recovery.");
        }
    }

    private static IEnumerable<string> PresetCandidates(
        string currentDirectory,
        string? legacyDirectory)
    {
        yield return Path.Combine(currentDirectory, PresetFileName);
        yield return Path.Combine(currentDirectory, LegacyPresetFileName);
        if (legacyDirectory is not null && Directory.Exists(legacyDirectory))
        {
            yield return Path.Combine(legacyDirectory, LegacyPresetFileName);
            yield return Path.Combine(legacyDirectory, PresetFileName);
        }
    }

    private static bool TryReadPresetData(
        string path,
        out byte[] data,
        out int presetCount,
        out string error)
    {
        data = [];
        presetCount = 0;
        error = string.Empty;
        try
        {
            FileInfo file = new(path);
            if (file.Length <= 0 || file.Length > MaximumPresetFileBytes)
            {
                error = $"file size {file.Length} is outside the accepted range";
                return false;
            }

            data = File.ReadAllBytes(path);
            if (data.LongLength != file.Length)
            {
                error = "the file changed while it was being read";
                return false;
            }

            using JsonDocument document = JsonDocument.Parse(data);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error = "the JSON root is not an object";
                return false;
            }

            JsonElement presets = default;
            bool foundPresets = false;
            foreach (JsonProperty property in document.RootElement.EnumerateObject())
            {
                if (!string.Equals(property.Name, "Presets", StringComparison.OrdinalIgnoreCase))
                    continue;
                presets = property.Value;
                foundPresets = true;
                break;
            }
            if (!foundPresets || presets.ValueKind != JsonValueKind.Array)
            {
                error = "the JSON does not contain a Presets array";
                return false;
            }

            PresetDocument? model = JsonSerializer.Deserialize<PresetDocument>(
                data,
                PresetJsonOptions);
            if (model?.Presets is null)
            {
                error = "the JSON could not be read as a preset document";
                return false;
            }

            presetCount = model.Presets.Count;
            return true;
        }
        catch (Exception exception)
        {
            error = $"{exception.GetType().Name}: {exception.Message}";
            return false;
        }
    }

    private static void BackupInvalidPresetFile(
        string destination,
        Action<string> log)
    {
        string temporaryPath = destination + $".backup-{Guid.NewGuid():N}.tmp";
        try
        {
            File.Copy(destination, temporaryPath, overwrite: false);
            string digest;
            using (FileStream stream = File.OpenRead(temporaryPath))
            {
                digest = Convert.ToHexString(SHA256.HashData(stream))
                    .ToLowerInvariant()[..16];
            }

            string backupPath = destination + $".invalid-{digest}.bak";
            if (File.Exists(backupPath))
                return;
            File.Move(temporaryPath, backupPath, overwrite: false);
            log($"Backed up an invalid persistent preset file to {backupPath}.");
        }
        finally
        {
            try { File.Delete(temporaryPath); } catch { }
        }
    }

    private static void WriteAtomically(byte[] data, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string temporaryPath = destination + $".migrate-{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(temporaryPath, data);
            File.Move(temporaryPath, destination, overwrite: true);
        }
        finally
        {
            try { File.Delete(temporaryPath); } catch { }
        }
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
}
