using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mana.Services;

public sealed class StorageRootState
{
    public string RootPath { get; set; } = string.Empty;
}

public static class StorageRootStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly char[] InvalidFolderChars = Path.GetInvalidFileNameChars();
    private static string? _cachedRoot;

    public static string AppSettingsDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "mana");

    public static string DefaultRootPath => AppSettingsDirectory;

    public static string GetRootPath()
    {
        if (!string.IsNullOrWhiteSpace(_cachedRoot))
        {
            return _cachedRoot;
        }

        try
        {
            var path = GetSettingsPath();
            if (File.Exists(path))
            {
                var state = JsonSerializer.Deserialize<StorageRootState>(File.ReadAllText(path), JsonOptions);
                if (!string.IsNullOrWhiteSpace(state?.RootPath))
                {
                    _cachedRoot = state.RootPath;
                    return _cachedRoot;
                }
            }
        }
        catch
        {
        }

        _cachedRoot = DefaultRootPath;
        return _cachedRoot;
    }

    public static void SetRootPath(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("Root path is empty.", nameof(rootPath));
        }

        Directory.CreateDirectory(AppSettingsDirectory);
        Directory.CreateDirectory(rootPath);
        var state = new StorageRootState { RootPath = rootPath };
        File.WriteAllText(GetSettingsPath(), JsonSerializer.Serialize(state, JsonOptions));
        _cachedRoot = rootPath;
        EnsureDbDirectory();
    }

    public static string EnsureDbDirectory()
    {
        var db = GetDbDirectory();
        Directory.CreateDirectory(db);
        return db;
    }

    public static string GetDbDirectory() => Path.Combine(GetRootPath(), "db");

    public static string SanitizeInstanceId(string instancePath)
    {
        if (string.IsNullOrWhiteSpace(instancePath))
        {
            return "_unknown";
        }

        var sanitized = instancePath;
        foreach (var ch in InvalidFolderChars)
        {
            sanitized = sanitized.Replace(ch, '_');
        }

        sanitized = sanitized.Replace('\\', '_').Replace('/', '_');
        sanitized = Regex.Replace(sanitized, @"_+", "_");
        return sanitized.Trim('_');
    }

    public static string GetKeyboardFolder(string instancePath)
    {
        var folder = Path.Combine(EnsureDbDirectory(), SanitizeInstanceId(instancePath));
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string GetSettingsPath() => Path.Combine(AppSettingsDirectory, "storage-root.json");
}
