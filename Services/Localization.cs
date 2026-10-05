using Windows.System;

namespace Mana.Services;

public static class Localization
{
    private static readonly Dictionary<string, (string En, string Ja)> Strings = new(StringComparer.Ordinal)
    {
        ["AppTitle"] = ("mana as setting keyboard layout", "キーボードレイアウト設定ツール まな"),
        ["Button_Reload"] = ("Reload", "再読込"),
        ["Button_OpenSettings"] = ("Open language settings", "言語設定を開く"),
        ["Button_OpenDeviceRegistry"] = ("Open device registry", "デバイスのレジストリを開く"),
        ["Button_OpenGlobalRegistry"] = ("Open global registry", "グローバルのレジストリを開く"),
        ["Button_ShowHidden"] = ("Show hidden", "非表示も表示"),
        ["Button_HideHidden"] = ("Hide hidden", "非表示を隠す"),
        ["Button_ApplyDevice"] = ("Apply device override", "デバイス設定を適用"),
        ["Button_ClearDevice"] = ("Clear device override", "デバイス設定を削除"),
        ["Button_ApplyGlobalPreset"] = ("Apply global preset", "グローバルプリセット適用"),
        ["Button_ApplyGlobal"] = ("Save global values", "グローバル値を保存"),
        ["Button_ClearGlobal"] = ("Clear global overrides", "グローバル設定を削除"),
        ["Button_SaveHidden"] = ("Save visibility", "表示設定を保存"),
        ["Button_RestartElevated"] = ("Restart as Administrator", "管理者として再起動"),
        ["Title_AdminPrefix"] = ("Administrator: ", "管理者: "),
        ["Tooltip_HideDevice"] = ("Hide this device", "非表示にする"),
        ["Section_Devices"] = ("Connected keyboards", "接続中のキーボード"),
        ["Section_DeviceDetail"] = ("Device override", "デバイスごとのオーバーライド"),
        ["Section_Global"] = ("Global override (i8042prt)", "グローバル設定 (i8042prt)"),
        ["Section_GuidanceNeeded"] = (
            "Preparation needed: global OverrideKeyboardType/Subtype remain. Either press “Clear global overrides” below, or in Settings → Time & language → Language & region → Japanese → Language options, set Hardware keyboard layout to “Use connected keyboard layout” (same effect).",
            "事前準備が必要です: グローバルの OverrideKeyboardType / Subtype が残っています。下の「グローバル設定を削除」を押すか、設定 → 時刻と言語 → 言語と地域 → 日本語 → 言語のオプション で「ハードウェア キーボードのレイアウト」を「接続済みキーボード レイアウトを使用する」にしてください（どちらも同じ効果です）。"),
        ["Section_GuidanceReady"] = (
            "Prepared: Hardware keyboard layout is effectively “Use connected keyboard layout” (no global Type/Subtype override).",
            "事前準備済: 設定-言語のオプションにて、「接続済みキーボードレイアウトを使用する」指定済み（グローバルの Type/Subtype オーバーライドなし）。"),
        ["Dialog_DisableConnectedTitle"] = ("Disable connected keyboard layout?", "接続済みキーボードレイアウトを無効にしますか？"),
        ["Dialog_DisableConnectedMessage"] = (
            "Saving a global override writes i8042prt Type/Subtype values. This is equivalent to turning off “Use connected keyboard layout” in Settings and forcing a fixed hardware layout. Continue?",
            "グローバル設定を保存すると i8042prt の Type/Subtype が書き込まれます。これは設定の「接続済みキーボード レイアウトを使用する」が無効になり、固定のハードウェア配列が強制されるのと同等です。続行しますか？"),
        ["Dialog_ClearPrimaryTitle"] = ("Delete global overrides?", "グローバル設定を削除しますか？"),
        ["Dialog_ClearPrimaryMessageFormat"] = (
            "The following global values will be deleted:\n{0}\n\nLayerDriver JPN / KOR depend on these values and have no effect on their own.",
            "次のグローバル値が削除されます:\n{0}\n\nLayerDriver JPN / KOR はこれらの値に従属する設定で、単独では効果がありません。"),
        ["Dialog_ClearPrimaryConnectedNote"] = (
            "\n\nWith no OverrideKeyboardType/Subtype left, this is the same as “Use connected keyboard layout” in Settings.",
            "\n\nOverrideKeyboardType / Subtype が無くなるため、設定の「接続済みキーボード レイアウトを使用する」と同じ状態になります。"),
        ["Label_RegistryPath"] = ("Registry location", "レジストリ位置"),
        ["Dialog_DisableConnectedContinue"] = ("Continue", "続行"),
        ["Dialog_DisableConnectedCancel"] = ("Cancel", "キャンセル"),
        ["Label_InstancePath"] = ("Device instance path", "デバイスインスタンスパス"),
        ["Label_HardwareIds"] = ("Hardware IDs", "ハードウェア ID"),
        ["Label_ShellName"] = ("Device name", "デバイス名"),
        ["Label_DeviceManagerName"] = ("Device Manager name", "デバイスマネージャー名"),
        ["Label_Preset"] = ("Country / layout preset", "国・配列プリセット"),
        ["Label_Type"] = ("Type", "タイプ"),
        ["Label_Subtype"] = ("Subtype", "サブタイプ"),
        ["Label_Identifier"] = ("OverrideKeyboardIdentifier", "OverrideKeyboardIdentifier"),
        ["Label_LayerDriverJpn"] = ("LayerDriver JPN", "LayerDriver JPN"),
        ["Label_LayerDriverKor"] = ("LayerDriver KOR", "LayerDriver KOR"),
        ["Label_Effective"] = ("Effective", "実効値"),
        ["Source_Device"] = ("Device", "デバイス"),
        ["Source_Global"] = ("Global", "グローバル"),
        ["Source_Auto"] = ("Auto-detect", "自動検出"),
        ["Preset_US101"] = ("Enhanced 101/102 (US)", "拡張 101/102（US）"),
        ["Preset_JapaneseJIS"] = ("Japanese (JIS)", "日本語（JIS）"),
        ["Preset_Korean101Type1"] = ("Korean 101 Type 1", "韓国語 101 Type 1"),
        ["Preset_Korean101Type3"] = ("Korean 101 Type 3", "韓国語 101 Type 3"),
        ["Preset_Custom"] = ("Custom", "カスタム"),
        ["Layout_AutoDetect"] = ("Auto-detect", "自動検出"),
        ["Layout_CustomFormat"] = ("Custom (Type={0}, Subtype={1})", "カスタム (Type={0}, Subtype={1})"),
        ["Status_LoadedFormat"] = ("Loaded {0} keyboard device(s).", "{0} 件のキーボードデバイスを読み込みました。"),
        ["Status_DeviceApplied"] = ("Device override saved. Unplug/replug USB keyboards or reboot for built-in keyboards.", "デバイス設定を保存しました。USB は抜き差し、内蔵は再起動で反映されます。"),
        ["Status_DeviceCleared"] = ("Device override cleared. Reconnect or reboot to apply.", "デバイス設定を削除しました。再接続または再起動で反映されます。"),
        ["Status_GlobalPresetApplied"] = ("Global preset applied (including Identifier/LayerDriver as defined). Reboot required.", "グローバルプリセットを適用しました（Identifier / LayerDriver 含む）。再起動が必要です。"),
        ["Status_GlobalApplied"] = ("Global values saved. Reboot required.", "グローバル値を保存しました。再起動が必要です。"),
        ["Status_GlobalCleared"] = ("Global overrides cleared. Reboot required.", "グローバル設定を削除しました。再起動が必要です。"),
        ["Status_ErrorFormat"] = ("Error: {0}", "エラー: {0}"),
        ["Settings_Opened"] = ("Opened Language & region settings. Open Japanese → Language options → Hardware keyboard layout.", "「言語と地域」を開きました。日本語 → 言語のオプション → ハードウェア キーボードのレイアウト を確認してください。"),
        ["Settings_OpenFailed"] = ("Could not open Settings.", "設定アプリを開けませんでした。"),
        ["Settings_OpenErrorFormat"] = ("Could not open Settings: {0}", "設定アプリを開けませんでした: {0}"),
        ["Status_NotElevated"] = ("Running without Administrator. You can view settings; writing to HKLM requires elevation.", "管理者権限なしで起動中です。閲覧は可能ですが、HKLM への書き込みには昇格が必要です。"),
        ["Status_Elevated"] = ("Running as Administrator. Registry writes are allowed.", "管理者として実行中です。レジストリ書き込みが可能です。"),
        ["Status_ElevationNeeded"] = ("Administrator rights are required to write HKLM. Use “Restart as Administrator”.", "HKLM への書き込みには管理者権限が必要です。「管理者として再起動」を使ってください。"),
        ["Status_ElevationCancelled"] = ("Elevation was cancelled.", "昇格がキャンセルされました。"),
        ["Status_RegistryOpenedFormat"] = ("Opened a new Registry Editor window at:\n{0}", "レジストリ エディターを新しいウィンドウで開きました:\n{0}"),
        ["Status_NoDeviceSelected"] = ("Select a keyboard first.", "先にキーボードを選択してください。"),
        ["Status_RebootStarted"] = ("Reboot requested.", "再起動を要求しました。"),
        ["Dialog_ExitTitle"] = ("Restart required?", "再起動の確認"),
        ["Dialog_ExitMessage"] = ("Registry settings were changed. Restart Windows now to apply hardware keyboard recognition changes?", "レジストリ設定が変更されています。ハードウェア認識の反映のため、今すぐ Windows を再起動しますか？"),
        ["Dialog_ExitReboot"] = ("Restart now", "今すぐ再起動"),
        ["Dialog_ExitQuit"] = ("Exit without restart", "再起動せず終了"),
        ["Dialog_ExitCancel"] = ("Cancel", "キャンセル"),
        ["Button_PastKeyboards"] = ("Keyboard history", "過去のキーボード一覧"),
        ["Button_StorageSettings"] = ("Settings", "設定"),
        ["Button_OpenFolder"] = ("Open folder", "フォルダを開く"),
        ["Button_ChangeStorage"] = ("Change…", "変更…"),
        ["Button_StorageOk"] = ("OK", "OK"),
        ["Tab_MemoView"] = ("View", "表示"),
        ["Tab_MemoEdit"] = ("Edit", "編集"),
        ["Section_PastKeyboards"] = ("Keyboard history", "過去のキーボード一覧"),
        ["Badge_Connected"] = ("Connected", "接続中"),
        ["Stack_None"] = ("(none)", "(なし)"),
        ["Stack_NoImage"] = ("No image", "画像なし"),
        ["Label_DriverStack"] = ("Driver Stack", "Driver Stack"),
        ["Label_Memo"] = ("Memo", "Memo"),
        ["Tooltip_OpenImage"] = ("Open original image", "オリジナル画像を開く"),
        ["Dialog_StorageTitle"] = ("Data folder", "情報保存フォルダ"),
        ["Dialog_StorageMessage"] = (
            "Choose the folder used for keyboard notes, images, and history (a db subfolder is created under it).",
            "キーボードのメモ・画像・履歴を保存するフォルダを指定します（直下に db フォルダが作成されます）。"),
        ["Dialog_StoragePathLabel"] = ("Current folder", "現在のフォルダ"),
        ["Status_StorageSetFormat"] = ("Data folder: {0}", "情報保存フォルダ: {0}"),
        ["Status_NoHistory"] = (
            "No saved keyboard history yet. Connected keyboards are recorded when the list is loaded.",
            "保存済みのキーボード履歴はまだありません。接続中のキーボードは一覧の読み込み時に記録されます。"),
    };

    public static void Refresh()
    {
        // Strings are resolved from the in-memory table (unpackaged-safe).
    }

    public static string Get(string key)
    {
        if (Strings.TryGetValue(key, out var pair))
        {
            return LanguageService.IsJapanese ? pair.Ja : pair.En;
        }

        return key;
    }
}

public static class SettingsLauncher
{
    public static async Task<(bool Success, string Message)> OpenLanguageSettingsAsync()
    {
        try
        {
            var uri = new Uri("ms-settings:regionlanguage");
            var success = await Launcher.LaunchUriAsync(uri);
            return success
                ? (true, Localization.Get("Settings_Opened"))
                : (false, Localization.Get("Settings_OpenFailed"));
        }
        catch (Exception ex)
        {
            return (false, string.Format(Localization.Get("Settings_OpenErrorFormat"), ex.Message));
        }
    }
}
