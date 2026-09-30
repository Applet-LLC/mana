namespace Mana.Models;

public sealed class DeviceOverrideValues
{
    public int? Type { get; init; }
    public int? Subtype { get; init; }

    public bool HasAny => Type.HasValue || Subtype.HasValue;
}
