using System.Text.Json;
using Windows.Graphics;

namespace Mana.Services;

public sealed class WindowBoundsState
{
    public int Width { get; set; } = 1100;
    public int Height { get; set; } = 900;
    public int X { get; set; } = 80;
    public int Y { get; set; } = 40;
    public bool HasPosition { get; set; }
}

public static class WindowStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static WindowBoundsState Load()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return new WindowBoundsState();
            }

            return JsonSerializer.Deserialize<WindowBoundsState>(File.ReadAllText(path), JsonOptions)
                   ?? new WindowBoundsState();
        }
        catch
        {
            return new WindowBoundsState();
        }
    }

    public static void Save(SizeInt32 size, PointInt32 position)
    {
        try
        {
            Directory.CreateDirectory(GetDirectory());
            var state = new WindowBoundsState
            {
                Width = Math.Max(800, size.Width),
                Height = Math.Max(700, size.Height),
                X = position.X,
                Y = position.Y,
                HasPosition = true
            };
            File.WriteAllText(GetPath(), JsonSerializer.Serialize(state, JsonOptions));
        }
        catch
        {
        }
    }

    private static string GetDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mana");

    private static string GetPath() => Path.Combine(GetDirectory(), "window-bounds.json");
}
