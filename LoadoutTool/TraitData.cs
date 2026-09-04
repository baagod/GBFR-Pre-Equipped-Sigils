using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace LoadoutTool;

internal static class TraitData
{
    public static IReadOnlyList<string> Names { get; private set; } = Array.Empty<string>();

    public static void Load(string traitsPath)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(traitsPath));
            var names = new List<string>();
            foreach (JsonElement entry in doc.RootElement.GetProperty("traits").EnumerateArray())
            {
                string nameZh = entry.GetProperty("nameZh").GetString() ?? "";
                if (nameZh.Length > 0)
                    names.Add(nameZh);
            }
            names.Sort();
            Names = names;
        }
        catch
        {
            Names = Array.Empty<string>();
        }
    }
}
