using Mana.Models;

namespace Mana.Services;

/// <summary>
/// Windows の「ハードウェア キーボードのレイアウト」と i8042prt グローバル Override は実質対応する。
/// 「接続済みキーボード レイアウトを使用する」≈ OverrideKeyboardType/Subtype が未設定。
/// 固定レイアウト選択 ≈ グローバル Type/Subtype（および付帯値）が書き込まれた状態。
/// </summary>
public static class ConnectedKeyboardLayoutStatus
{
    public static bool IsUsingConnectedKeyboardLayout()
    {
        var values = GlobalOverrideStore.Read();
        return !values.Type.HasValue && !values.Subtype.HasValue;
    }

    public static string GetGuidanceText() =>
        IsUsingConnectedKeyboardLayout()
            ? Localization.Get("Section_GuidanceReady")
            : Localization.Get("Section_GuidanceNeeded");
}
