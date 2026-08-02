using System.Globalization;

namespace GBFR.ExtraSigilSlots.Reloaded;

internal static class VirtualSlotCountInput
{
    internal static int Normalize(string input, int capacity)
    {
        return int.TryParse(
                input,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsed) &&
            parsed is >= 1 &&
            parsed <= capacity
                ? parsed
                : 1;
    }
}
