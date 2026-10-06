using System.Drawing.Text;
using System.Text.Json;

namespace Mana.Services;

public sealed class KeyboardMemoUiSettings
{
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
}

public static class KeyboardMemoUiStore
{
    private const string FileName = "memo-ui.json";
    public const string DefaultFontFamily = "Yu Gothic UI";
    public const double DefaultFontSize = 14;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static KeyboardMemoUiSettings Load(string instancePath)
    {
        try
        {
            var path = GetPath(instancePath);
            if (!File.Exists(path))
            {
                return new KeyboardMemoUiSettings();
            }

            return JsonSerializer.Deserialize<KeyboardMemoUiSettings>(File.ReadAllText(path), JsonOptions)
                   ?? new KeyboardMemoUiSettings();
        }
        catch
        {
            return new KeyboardMemoUiSettings();
        }
    }

    public static void Save(string instancePath, KeyboardMemoUiSettings settings)
    {
        var path = GetPath(instancePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public static string ResolveFontFamily(KeyboardMemoUiSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.FontFamily))
        {
            return settings.FontFamily.Trim();
        }

        var global = GlobalMemoUiStore.Load();
        return string.IsNullOrWhiteSpace(global.FontFamily)
            ? DefaultFontFamily
            : global.FontFamily.Trim();
    }

    public static double ResolveFontSize(KeyboardMemoUiSettings settings)
    {
        var size = settings.FontSize
                   ?? GlobalMemoUiStore.Load().FontSize
                   ?? DefaultFontSize;
        if (size < 8)
        {
            return 8;
        }

        if (size > 48)
        {
            return 48;
        }

        return size;
    }

    public static IReadOnlyList<string> GetInstalledFontFamilies()
    {
        try
        {
            using var collection = new InstalledFontCollection();
            return collection.Families
                .Select(f => f.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }
        catch
        {
            return
            [
                "Yu Gothic UI",
                "Meiryo UI",
                "Segoe UI",
                "Consolas",
                "Cascadia Mono"
            ];
        }
    }

    private static string GetPath(string instancePath) =>
        Path.Combine(StorageRootStore.GetKeyboardFolder(instancePath), FileName);
}
