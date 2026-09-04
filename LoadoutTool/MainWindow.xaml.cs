using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using Wpf.Ui.Controls;

namespace LoadoutTool;

public partial class MainWindow : FluentWindow
{
    private readonly ObservableCollection<LoadoutSlot> _slots = new();
    private readonly string _modDir;
    private const int MaxSlots = 24;

    public MainWindow(string modDir)
    {
        InitializeComponent();
        _modDir = modDir;
        SlotItems.ItemsSource = _slots;
        LoadExisting();
    }

    private void LoadExisting()
    {
        try
        {
            string path = Path.Combine(_modDir, "loadout.json");
            if (!File.Exists(path))
                return;
            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (JsonElement slot in doc.RootElement.GetProperty("slots").EnumerateArray())
            {
                var model = new LoadoutSlot
                {
                    Enabled = !slot.TryGetProperty("enabled", out JsonElement enabled) || enabled.GetBoolean(),
                    Trait1 = slot.TryGetProperty("trait1", out JsonElement t1) ? t1.GetString() ?? "" : "",
                    Level1 = slot.TryGetProperty("level1", out JsonElement l1) ? l1.GetInt32().ToString() : "15",
                    Trait2 = slot.TryGetProperty("trait2", out JsonElement t2) ? t2.GetString() ?? "" : "",
                    Level2 = slot.TryGetProperty("level2", out JsonElement l2) ? l2.GetInt32().ToString() : "15",
                };
                _slots.Add(model);
            }
            RefreshIndexes();
        }
        catch
        {
            // No existing config or unreadable: start with the built-in example.
        }
        if (_slots.Count == 0)
            AddSlot();
    }

    private void RefreshIndexes()
    {
        for (int i = 0; i < _slots.Count; i++)
            _slots[i].SlotIndex = (i + 1).ToString();
    }

    private void AddSlot_Click(object sender, RoutedEventArgs e)
    {
        if (_slots.Count >= MaxSlots)
            return;
        AddSlot();
    }

    private void AddSlot()
    {
        _slots.Add(new LoadoutSlot());
        RefreshIndexes();
    }

    private void RemoveSlot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LoadoutSlot slot })
        {
            _slots.Remove(slot);
            RefreshIndexes();
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var slots = _slots.Select(slot => new
        {
            trait1 = slot.Trait1.Trim(),
            level1 = ParseLevel(slot.Level1),
            trait2 = slot.Trait2.Trim(),
            level2 = ParseLevel(slot.Level2),
            enabled = slot.Enabled,
        }).ToList();

        // Validate against the dictionary before writing.
        var unknown = slots
            .Where(s => s.enabled && s.trait1.Length > 0 && !TraitData.Names.Contains(s.trait1))
            .Select(s => s.trait1)
            .Concat(slots
                .Where(s => s.enabled && s.trait2.Length > 0 && !TraitData.Names.Contains(s.trait2))
                .Select(s => s.trait2))
            .Distinct()
            .ToList();
        if (unknown.Count > 0)
        {
            StatusText.Text = $"未知词条：{string.Join("、", unknown)}";
            return;
        }

        string json = JsonSerializer.Serialize(new { slots },
            new JsonSerializerOptions { WriteIndented = true });
        try
        {
            File.WriteAllText(Path.Combine(_modDir, "loadout.json"), json);
            StatusText.Text = $"已保存 {slots.Count} 槽 → mod 目录（游戏内 5 秒内生效）";
        }
        catch (Exception exception)
        {
            StatusText.Text = $"保存失败：{exception.Message}";
        }
    }

    private static int ParseLevel(string text)
    {
        return int.TryParse(text.Trim(), out int value) ? Math.Clamp(value, 0, 20) : 15;
    }
}
