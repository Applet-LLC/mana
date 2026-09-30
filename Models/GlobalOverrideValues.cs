namespace Mana.Models;

public sealed class GlobalOverrideValues
{
    public int? Type { get; set; }
    public int? Subtype { get; set; }
    public string? Identifier { get; set; }
    public string? LayerDriverJpn { get; set; }
    public string? LayerDriverKor { get; set; }

    public bool HasAny =>
        Type.HasValue ||
        Subtype.HasValue ||
        !string.IsNullOrEmpty(Identifier) ||
        !string.IsNullOrEmpty(LayerDriverJpn) ||
        !string.IsNullOrEmpty(LayerDriverKor);

    /// <summary>
    /// Type / Subtype / Identifier のうち、current に存在し next で消えるものの値名。
    /// LayerDriver JPN / KOR はこれら 3 値に従属するため判定対象外。
    /// </summary>
    public static IReadOnlyList<string> GetClearedPrimaryValueNames(GlobalOverrideValues current, GlobalOverrideValues next)
    {
        var names = new List<string>();
        if (current.Type.HasValue && !next.Type.HasValue)
        {
            names.Add("OverrideKeyboardType");
        }

        if (current.Subtype.HasValue && !next.Subtype.HasValue)
        {
            names.Add("OverrideKeyboardSubtype");
        }

        if (!string.IsNullOrWhiteSpace(current.Identifier) && string.IsNullOrWhiteSpace(next.Identifier))
        {
            names.Add("OverrideKeyboardIdentifier");
        }

        return names;
    }
}
