using Mana.Models;

namespace Mana.Services;

public static class LayoutCatalog
{
    public static IReadOnlyList<LayoutPreset> Presets { get; } =
    [
        new LayoutPreset
        {
            Id = "us-101",
            DisplayNameResourceKey = "Preset_US101",
            Type = 4,
            Subtype = 0,
            ClearGlobalExtras = true
        },
        new LayoutPreset
        {
            Id = "ja-jis",
            DisplayNameResourceKey = "Preset_JapaneseJIS",
            Type = 7,
            Subtype = 2,
            Identifier = "PCAT_106KEY",
            LayerDriverValueName = "LayerDriver JPN",
            LayerDriverValue = "kbd106.dll"
        },
        new LayoutPreset
        {
            Id = "ko-101-type1",
            DisplayNameResourceKey = "Preset_Korean101Type1",
            Type = 8,
            Subtype = 3,
            Identifier = "STANDARD",
            LayerDriverValueName = "LayerDriver KOR",
            LayerDriverValue = "kbd101a.dll"
        },
        new LayoutPreset
        {
            Id = "ko-101-type3",
            DisplayNameResourceKey = "Preset_Korean101Type3",
            Type = 8,
            Subtype = 5,
            Identifier = "STANDARD",
            LayerDriverValueName = "LayerDriver KOR",
            LayerDriverValue = "kbd101c.dll"
        },
        new LayoutPreset
        {
            Id = "custom",
            DisplayNameResourceKey = "Preset_Custom",
            Type = 0,
            Subtype = 0,
            IsCustom = true
        }
    ];

    public static LayoutPreset? FindByTypeSubtype(int? type, int? subtype)
    {
        if (!type.HasValue || !subtype.HasValue)
        {
            return null;
        }

        return Presets.FirstOrDefault(p =>
            !p.IsCustom && p.Type == type.Value && p.Subtype == subtype.Value);
    }

    public static string ResolveDisplayName(int? type, int? subtype, Func<string, string> localize)
    {
        var preset = FindByTypeSubtype(type, subtype);
        if (preset is not null)
        {
            return localize(preset.DisplayNameResourceKey);
        }

        if (!type.HasValue && !subtype.HasValue)
        {
            return localize("Layout_AutoDetect");
        }

        return string.Format(
            localize("Layout_CustomFormat"),
            type?.ToString() ?? "-",
            subtype?.ToString() ?? "-");
    }
}
