namespace Mana.Launcher;

internal static class Strings
{
    public static bool IsJapanese { get; set; }

    public static string WindowTitle => IsJapanese
        ? "キーボードレイアウト設定ツール まな"
        : "mana as setting keyboard layout";

    public static string LanguageSwitchLabel => IsJapanese ? "English" : "日本語";

    public static string Description => IsJapanese
        ? "インストールする言語を選んでください。日本語では日本語のインストーラー、English では英語のインストーラーが起動します。"
        : "Choose the installer language. Japanese starts the Japanese installer. English starts the English installer.";

    public static string InstallButton => IsJapanese ? "インストール" : "Install";

    public static string CancelButton => IsJapanese ? "キャンセル" : "Cancel";

    public static string InstallingStatus => IsJapanese ? "インストールしています…" : "Installing…";

    public static string MsiNotFoundStatus(string fileName) => IsJapanese
        ? $"同じフォルダに {fileName} がありません。"
        : $"{fileName} was not found in the same folder.";

    public static string MsiNotFoundDialog(string fileName) => IsJapanese
        ? $"インストーラーが見つかりません。\n\n{fileName} をこのプログラムと同じフォルダに置いてください。"
        : $"The installer was not found.\n\nPlace {fileName} in the same folder as this program.";

    public static string ErrorTitle => IsJapanese ? "インストールできません" : "Cannot install";

    public static string FailedMessage(int exitCode) => IsJapanese
        ? $"インストールは完了しませんでした。終了コード: {exitCode}"
        : $"Installation did not finish. Exit code: {exitCode}";

    public static string RebootTitle => IsJapanese ? "再起動" : "Restart";

    public static string RebootMessage => IsJapanese
        ? "インストールは完了しました。変更を完全に反映するには、Windows の再起動が必要な場合があります。"
        : "Installation finished. You may need to restart Windows before every change takes effect.";

    public static string StartFailed => IsJapanese
        ? "msiexec.exe を起動できませんでした。"
        : "Could not start msiexec.exe.";
}
