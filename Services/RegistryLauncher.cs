using System.Diagnostics;
using Microsoft.Win32;

namespace Mana.Services;

public static class AppSessionState
{
    public static bool RegistryChanged { get; set; }
    public static bool SuppressExitPrompt { get; set; }
}

public static class RegistryLauncher
{
    public const string GlobalParametersPath =
        @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\i8042prt\Parameters";

    public static string GetDeviceParametersPath(string instancePath) =>
        $@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\{instancePath}\Device Parameters";

    private const string RegeditAppletKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit";

    public static (bool Success, string Message) OpenKey(string hiveKeyPath)
    {
        try
        {
            EnsureDeviceParametersKeyExists(hiveKeyPath);

            using (var key = Registry.CurrentUser.CreateSubKey(RegeditAppletKeyPath))
            {
                // LastKey のルート名は OS の表示言語でローカライズされる（日本語版は「コンピューター」）。
                // 一致しないと regedit はパスを解釈できず、ルートで開いてしまう。
                var rootName = ResolveRegeditRootName(key?.GetValue("LastKey") as string);
                key?.SetValue("LastKey", $@"{rootName}\{hiveKeyPath}");
            }

            // -m: 既存の regedit があっても新しいインスタンスを開く（既存ウィンドウは LastKey を読み直さないため）。
            Process.Start(new ProcessStartInfo
            {
                FileName = "regedit.exe",
                Arguments = "-m",
                UseShellExecute = true
            });

            return (true, string.Format(Localization.Get("Status_RegistryOpenedFormat"), hiveKeyPath));
        }
        catch (Exception ex)
        {
            return (false, string.Format(Localization.Get("Status_ErrorFormat"), ex.Message));
        }
    }

    private static string ResolveRegeditRootName(string? existingLastKey)
    {
        // 既存の LastKey（例: "コンピューター\HKEY_..." や "コンピューター"）から実際のルート名を取得する。
        if (!string.IsNullOrWhiteSpace(existingLastKey))
        {
            var separator = existingLastKey.IndexOf('\\');
            var root = separator >= 0 ? existingLastKey[..separator] : existingLastKey;
            if (!string.IsNullOrWhiteSpace(root) &&
                !root.StartsWith("HKEY_", StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }
        }

        return System.Globalization.CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "ja"
            ? "コンピューター"
            : "Computer";
    }

    private static void EnsureDeviceParametersKeyExists(string hiveKeyPath)
    {
        const string prefix = @"HKEY_LOCAL_MACHINE\";
        if (!hiveKeyPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var subPath = hiveKeyPath[prefix.Length..];
        if (!subPath.Contains(@"\Enum\", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            using var _ = Registry.LocalMachine.CreateSubKey(subPath);
        }
        catch
        {
            // Opening regedit is still useful even if the subkey cannot be created.
        }
    }
}

public static class SystemReboot
{
    public static (bool Success, string Message) RequestImmediateReboot()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "shutdown.exe",
                Arguments = "/r /t 0",
                UseShellExecute = true,
                CreateNoWindow = true
            });
            return (true, Localization.Get("Status_RebootStarted"));
        }
        catch (Exception ex)
        {
            return (false, string.Format(Localization.Get("Status_ErrorFormat"), ex.Message));
        }
    }
}
