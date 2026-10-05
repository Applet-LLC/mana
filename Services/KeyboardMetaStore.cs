using System.Text.Json;
using Mana.Models;

namespace Mana.Services;

public sealed class KeyboardMetaInfo
{
    public string InstancePath { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public string? ShellFriendlyName { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
}

public sealed class KeyboardHistoryEntry
{
    public required string InstancePath { get; init; }
    public required string DisplayName { get; init; }
    public required string FolderPath { get; init; }
    public string? ThumbnailPath { get; init; }
    public DateTimeOffset LastSeenUtc { get; init; }
    public bool IsConnected { get; init; }
}

public static class KeyboardMetaStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private const string MetaFileName = "meta.json";

    public static void Upsert(KeyboardDeviceInfo info)
    {
        try
        {
            var folder = StorageRootStore.GetKeyboardFolder(info.InstancePath);
            var meta = new KeyboardMetaInfo
            {
                InstancePath = info.InstancePath,
                FriendlyName = info.FriendlyName,
                ShellFriendlyName = info.ShellFriendlyName,
                LastSeenUtc = DateTimeOffset.UtcNow
            };
            File.WriteAllText(Path.Combine(folder, MetaFileName), JsonSerializer.Serialize(meta, JsonOptions));
        }
        catch
        {
        }
    }

    public static void UpsertMany(IEnumerable<KeyboardDeviceInfo> devices)
    {
        foreach (var device in devices)
        {
            Upsert(device);
        }
    }

    public static IReadOnlyList<KeyboardHistoryEntry> ListEntries(IReadOnlySet<string> connectedInstancePaths)
    {
        var results = new List<KeyboardHistoryEntry>();
        var db = StorageRootStore.EnsureDbDirectory();
        if (!Directory.Exists(db))
        {
            return results;
        }

        foreach (var folder in Directory.EnumerateDirectories(db))
        {
            try
            {
                var metaPath = Path.Combine(folder, MetaFileName);
                KeyboardMetaInfo? meta = null;
                if (File.Exists(metaPath))
                {
                    meta = JsonSerializer.Deserialize<KeyboardMetaInfo>(File.ReadAllText(metaPath), JsonOptions);
                }

                var instancePath = meta?.InstancePath;
                if (string.IsNullOrWhiteSpace(instancePath))
                {
                    instancePath = Path.GetFileName(folder) ?? folder;
                }

                var displayName = !string.IsNullOrWhiteSpace(meta?.ShellFriendlyName)
                    ? meta!.ShellFriendlyName!
                    : !string.IsNullOrWhiteSpace(meta?.FriendlyName)
                        ? meta!.FriendlyName
                        : Path.GetFileName(folder) ?? instancePath;

                results.Add(new KeyboardHistoryEntry
                {
                    InstancePath = instancePath,
                    DisplayName = displayName,
                    FolderPath = folder,
                    ThumbnailPath = KeyboardNoteStore.FindFirstImage(folder),
                    LastSeenUtc = meta?.LastSeenUtc ?? DateTimeOffset.MinValue,
                    IsConnected = connectedInstancePaths.Contains(instancePath)
                });
            }
            catch
            {
            }
        }

        return results
            .OrderByDescending(e => e.IsConnected)
            .ThenByDescending(e => e.LastSeenUtc)
            .ThenBy(e => e.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }
}
