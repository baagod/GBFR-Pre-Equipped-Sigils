namespace LoadoutTool;

public class LoadoutSlot
{
    public bool Enabled { get; set; } = true;
    public string SlotIndex { get; set; } = "1";
    public string Trait1 { get; set; } = "";
    public string Level1 { get; set; } = "15";
    public string Trait2 { get; set; } = "";
    public string Level2 { get; set; } = "15";

    public IReadOnlyList<string> TraitNames => TraitData.Names;
}
