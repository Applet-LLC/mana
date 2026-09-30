using System.Text.Json;

namespace Mana.Services;

public static class HiddenDeviceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static HashSet<string> Load()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            var json = File.ReadAllText(path);
            var items = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            return new HashSet<string>(items, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static void Save(IEnumerable<string> instancePaths)
    {
        Directory.CreateDirectory(GetDirectory());
        var list = instancePaths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        File.WriteAllText(GetPath(), JsonSerializer.Serialize(list, JsonOptions));
    }

    private static string GetDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mana");

    private static string GetPath() => Path.Combine(GetDirectory(), "hidden-devices.json");
}
