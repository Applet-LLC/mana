using System.Text.Json;
using Windows.System.UserProfile;

namespace Mana.Services;

public static class LanguageService
{
    public const string Japanese = "ja-JP";
    public const string English = "en-US";

    private static string _current = English;

    public static void Initialize()
    {
        var saved = LoadSavedLanguage();
        if (!string.IsNullOrWhiteSpace(saved))
        {
            _current = Normalize(saved);
            return;
        }

        var osLanguage = GlobalizationPreferences.Languages.FirstOrDefault() ?? English;
        _current = osLanguage.StartsWith("ja", StringComparison.OrdinalIgnoreCase)
            ? Japanese
            : English;
    }

    public static string CurrentLanguage => _current;

    public static bool IsJapanese => _current.StartsWith("ja", StringComparison.OrdinalIgnoreCase);

    public static string Toggle()
    {
        _current = IsJapanese ? English : Japanese;
        SaveLanguage(_current);
        return _current;
    }

    public static string ToggleButtonLabel => IsJapanese ? "English" : "日本語";

    private static string Normalize(string language) =>
        language.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? Japanese : English;

    private static string? LoadSavedLanguage()
    {
        try
        {
            var path = GetPath();
            if (!File.Exists(path))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            if (doc.RootElement.TryGetProperty("language", out var language))
            {
                return language.GetString();
            }
        }
        catch
        {
        }

        return null;
    }

    private static void SaveLanguage(string language)
    {
        Directory.CreateDirectory(GetDirectory());
        var json = JsonSerializer.Serialize(new { language }, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(GetPath(), json);
    }

    private static string GetDirectory() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mana");

    private static string GetPath() => Path.Combine(GetDirectory(), "ui-language.json");
}
