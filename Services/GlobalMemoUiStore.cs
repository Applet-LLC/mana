using System.Text.Json;

namespace Mana.Services;

/// <summary>メモ表示のアプリ全体既定フォント（キーボード個別未設定時のフォールバック）。</summary>
public static class GlobalMemoUiStore
{
    private const string FileName = "memo-ui-default.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static KeyboardMemoUiSettings Load()
    {
        try
        {
            var path = GetPath();
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

    public static void Save(KeyboardMemoUiSettings settings)
    {
        Directory.CreateDirectory(StorageRootStore.AppSettingsDirectory);
        File.WriteAllText(GetPath(), JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static string GetPath() =>
        Path.Combine(StorageRootStore.AppSettingsDirectory, FileName);
}
