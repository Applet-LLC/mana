namespace Mana.Models;

public sealed class LayoutPreset
{
    public required string Id { get; init; }
    public required string DisplayNameResourceKey { get; init; }
    public required int Type { get; init; }
    public required int Subtype { get; init; }
    public string? Identifier { get; init; }
    public string? LayerDriverValue { get; init; }
    public string? LayerDriverValueName { get; init; }
    public bool ClearGlobalExtras { get; init; }
    public bool IsCustom { get; init; }
}
