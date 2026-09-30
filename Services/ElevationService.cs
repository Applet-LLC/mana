using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace Mana.Services;

public static class ElevationService
{
    public static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool RelaunchElevated(string? extraArgs = null)
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(exe))
            {
                return false;
            }

            var start = new ProcessStartInfo
            {
                FileName = exe,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = AppContext.BaseDirectory
            };
            if (!string.IsNullOrWhiteSpace(extraArgs))
            {
                start.Arguments = extraArgs;
            }

            Process.Start(start);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

public static class StartupCrashLog
{
    public static string LogPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "mana",
            "startup-crash.log");

    public static void Write(string message, Exception? ex = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var sb = new StringBuilder();
            sb.AppendLine($"[{DateTime.Now:O}] {message}");
            if (ex is not null)
            {
                sb.AppendLine(ex.ToString());
            }

            File.AppendAllText(LogPath, sb.ToString());
        }
        catch
        {
        }
    }

    public static void ShowNativeMessage(string text, string caption)
    {
        MessageBoxW(IntPtr.Zero, text, caption, 0x00000010);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
