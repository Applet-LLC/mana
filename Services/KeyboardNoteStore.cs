namespace Mana.Services;

public static class KeyboardNoteStore
{
    private const string MemoFileName = "memo.md";
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png"];

    public static string GetMemoPath(string instancePath) =>
        Path.Combine(StorageRootStore.GetKeyboardFolder(instancePath), MemoFileName);

    public static string LoadMemo(string instancePath)
    {
        try
        {
            var path = GetMemoPath(instancePath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static void SaveMemo(string instancePath, string markdown)
    {
        var path = GetMemoPath(instancePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, markdown ?? string.Empty);
    }

    public static string? FindFirstImage(string folderPath)
    {
        try
        {
            if (!Directory.Exists(folderPath))
            {
                return null;
            }

            return Directory.EnumerateFiles(folderPath)
                .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    public static string? FindFirstImageForDevice(string instancePath) =>
        FindFirstImage(StorageRootStore.GetKeyboardFolder(instancePath));

    public static void OpenKeyboardFolder(string instancePath)
    {
        var folder = StorageRootStore.GetKeyboardFolder(instancePath);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{folder}\"",
            UseShellExecute = true
        });
    }

    public static void OpenImage(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            return;
        }

        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = imagePath,
            UseShellExecute = true
        });
    }
}
