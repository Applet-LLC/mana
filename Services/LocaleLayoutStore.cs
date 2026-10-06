using Microsoft.Win32;

namespace Mana.Services;

public sealed class LocaleLayoutInfo
{
    public required string LocaleId { get; init; }
    public required string DisplayNameResourceKey { get; init; }
}

public static class LocaleLayoutStore
{
    public const string ValueName = "Layout File";
    private const string KeyRoot = @"SYSTEM\CurrentControlSet\Control\Keyboard Layouts";

    public static readonly IReadOnlyList<LocaleLayoutInfo> Locales =
    [
        new LocaleLayoutInfo { LocaleId = "00000411", DisplayNameResourceKey = "Locale_Japanese" },
        new LocaleLayoutInfo { LocaleId = "00000409", DisplayNameResourceKey = "Locale_English" },
        new LocaleLayoutInfo { LocaleId = "00000412", DisplayNameResourceKey = "Locale_Korean" }
    ];

    /// <summary>Layout File の候補（ユーザー指定どおり。個数表記と一覧の差は一覧を正とする）。</summary>
    public static readonly IReadOnlyList<string> LayoutFileCandidates =
    [
        "kbd101.dll",
        "KBDUS.DLL",
        "KBDJPN.DLL",
        "kbd106.dll",
        "KBDKOR.DLL"
    ];

    public static string GetDefaultLocaleId() =>
        LanguageService.IsJapanese ? "00000411" : "00000409";

    public static string GetRelativeKeyPath(string localeId) =>
        $@"{KeyRoot}\{NormalizeLocaleId(localeId)}";

    public static string GetHivePath(string localeId) =>
        $@"HKEY_LOCAL_MACHINE\{GetRelativeKeyPath(localeId)}";

    public static string? ReadLayoutFile(string localeId)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(GetRelativeKeyPath(localeId), writable: false);
            return key?.GetValue(ValueName) as string;
        }
        catch
        {
            return null;
        }
    }

    public static void WriteLayoutFile(string localeId, string layoutFile)
    {
        if (string.IsNullOrWhiteSpace(layoutFile))
        {
            throw new ArgumentException("Layout File is empty.", nameof(layoutFile));
        }

        using var key = Registry.LocalMachine.OpenSubKey(GetRelativeKeyPath(localeId), writable: true)
            ?? throw new InvalidOperationException($"Unable to open Keyboard Layouts key: {localeId}");

        key.SetValue(ValueName, layoutFile.Trim(), RegistryValueKind.String);
    }

    private static string NormalizeLocaleId(string localeId)
    {
        var id = (localeId ?? string.Empty).Trim();
        if (id.Length == 0)
        {
            return GetDefaultLocaleId();
        }

        return id.ToUpperInvariant();
    }
}
