# Keyboard Layout Tool — 開発プラン（確定版＋実装追記）

最終更新: 2026-09-11

本ドキュメントは、要件合意時の開発プランと、実装・デバッグ過程で確定した仕様差分をまとめたものです。

---

## 1. 目的

Windows 11 において、接続中のキーボードを列挙し、デバイスごと／グローバル（`i8042prt`）のキーボード Type・Subtype オーバーライドを GUI で表示・変更する WinUI 3 アプリを提供する。

参考:

- https://note.com/ikarga/n/n6ed4df6f5709
- Gemini 共有内容（ローカル保存: `## Windowsキーボードハードウェアオーバーライドに関するレジス.txt`）

---

## 2. 確定要件（合意事項）

| # | 項目 | 決定 |
|---|------|------|
| 1 | レジストリモデル | **Gemini 方式**（デバイス＝`KeyboardTypeOverride` / `KeyboardSubtypeOverride`、グローバル＝`OverrideKeyboardType` / `OverrideKeyboardSubtype`） |
| 2 | スコープ | デバイスごと ＋ **グローバル（i8042prt）** |
| 3 | OS | **Windows 11 のみ** |
| 4 | 配布 | **フォルダ配置**（self-contained publish。MSIX 等は後回し） |
| 5 | 列挙 | **すべて列挙**。行ごとの非表示フラグ＋「非表示を隠す／全部表示」 |
| 6 | プリセット | 既知の Type/Subtype のみ。未知は数値カスタム |
| 7 | 言語設定（ハードウェア キーボードのレイアウト） | 設定アプリを開けるなら**案内＋リンク**。開けない場合のみ自前 |
| 8 | グローバル CJK 付帯値 | Type/Subtype/Identifier/LayerDriver を**セット適用**し、**個別編集も可能** |
| 9 | US プリセット時の付帯値 | **方針 C**: プリセット定義に従い、US（4,0）では Identifier / LayerDriver JPN・KOR を**クリア** |
| 10 | UI 言語 | OS 表示言語が日本語なら日本語、それ以外は English を既定。クリックで日英切替・選択を記憶 |

---

## 3. レジストリ契約

### 3.1 優先順位

1. デバイスごとのオーバーライド（最優先）
2. グローバル（`i8042prt`）
3. PnP 自動検出

### 3.2 デバイスごと

パス:

`HKLM\SYSTEM\CurrentControlSet\Enum\<DeviceInstancePath>\Device Parameters`

| 値 | 型 |
|----|-----|
| `KeyboardTypeOverride` | DWORD |
| `KeyboardSubtypeOverride` | DWORD |

### 3.3 グローバル

パス:

`HKLM\SYSTEM\CurrentControlSet\Services\i8042prt\Parameters`

| 値 | 型 | 例 |
|----|-----|----|
| `OverrideKeyboardType` | DWORD | 4 / 7 / 8 |
| `OverrideKeyboardSubtype` | DWORD | 0 / 2 / 3 / 5 |
| `OverrideKeyboardIdentifier` | SZ | `PCAT_106KEY` |
| `LayerDriver JPN` | SZ | `kbd106.dll` |
| `LayerDriver KOR` | SZ | `kbd101a.dll` など |

### 3.4 初期プリセット表（方針 C）

| プリセット | Type | Subtype | Identifier | LayerDriver |
|------------|------|---------|------------|-------------|
| 拡張 101/102（US） | 4 | 0 | **削除** | **JPN/KOR を削除** |
| 日本語（JIS） | 7 | 2 | `PCAT_106KEY` | `LayerDriver JPN`=`kbd106.dll` |
| 韓国語 101 Type1 | 8 | 3 | `STANDARD` | `LayerDriver KOR`=`kbd101a.dll` |
| 韓国語 101 Type3 | 8 | 5 | `STANDARD` | `LayerDriver KOR`=`kbd101c.dll` |
| カスタム | 任意 | 任意 | 手入力 | 手入力 |

※ コミュニティ慣用の `Type=7, Subtype=0` は初期プリセットに含めない。

---

## 4. 技術スタック

- VS2022 / C# / WinUI 3（Windows App SDK 1.7）
- ターゲット: `net9.0-windows10.0.26100.0`（MinVersion Win11）
- Unpackaged（`WindowsPackageType=None`）
- Self-contained + `WindowsAppSDKSelfContained=true`
- ビルド: **Visual Studio の MSBuild**（`dotnet build` 単体だと PRI タスク不足で失敗しうる）
- 配布スクリプト: `publish.bat` → `publish\win-x64\`

---

## 5. 当初フェーズ計画

| Phase | 内容 |
|-------|------|
| 0 | Unpackaged プロジェクト、管理者マニフェスト、i18n 骨組、publish |
| 1 | キーボード列挙＋デバイス/グローバル読取 |
| 2 | LayoutCatalog、数値/プリセット編集、非表示、言語切替 |
| 3 | デバイス Override 書込・削除 |
| 4 | グローバル セット適用＋個別編集 |
| 5 | 設定リンク、エラー処理、README |

---

## 6. 実装で確定・追加した仕様（プラン後の調整）

### 6.1 管理者権限まわり（重要）

**当初案**: `app.manifest` で `requireAdministrator`。

**実装後の確定**:

- マニフェストは **`asInvoker`（通常権限起動）**。
- Unpackaged WinUI 3 を最初から管理者強制起動すると、起動直後に `Microsoft.UI.Xaml.dll` で無言終了することがある（WER: `0xc000027b` / `0x80073D54`）。
- 閲覧は通常権限で可能。HKLM 書き込み時はアプリ内「管理者として再起動」で昇格する。
- 昇格再起動時は終了確認ダイアログを出さない（`SuppressExitPrompt`）。

診断方法（README にも記載）:

1. イベント ビューアー → Application Error
2. 終了コード確認（PowerShell）
3. `%LocalAppData%\mana\startup-crash.log`

### 6.2 UI 言語実装

**当初案**: `ApplicationLanguages.PrimaryLanguageOverride` + `.resw`。

**実装後の確定**:

- Unpackaged では `PrimaryLanguageOverride` 設定が失敗しうる（`InvalidOperationException`）。
- **自前の言語状態**（`LanguageService` + `%LocalAppData%\mana\ui-language.json`）と、**インメモリ日英辞書**（`Localization`）を正とする。
- `.resw` は残置しているが、実行時表示の正本はインメモリ辞書。

### 6.3 ウィンドウ UI

**当初案**: `TitleBar` + `MicaBackdrop`。

**実装後の確定**: シンプルな `Window` + `Frame` 構成（起動安定性優先）。

### 6.4 エントリポイントとクラッシュ可視化

- `DISABLE_XAML_GENERATED_MAIN` + 自前 `Program.cs`
- 起動失敗時は `startup-crash.log` とネイティブ MessageBox
- `Application.UnhandledException` でもログ／通知

### 6.5 ビジー表示（後追い要件）

- 一覧選択・再読込・適用などの処理中は待ちカーソル（Wait）を表示
- 処理中は一覧を一時無効化
- 完了後にカーソルと操作を復元

### 6.6 レジストリ エディター起動（後追い要件）

- 「言語設定を開く」の右: **デバイスのレジストリを開く**  
  → 選択中デバイスの `...\Enum\<InstancePath>\Device Parameters`
- 「グローバル設定を削除」の右: **グローバルのレジストリを開く**  
  → `...\Services\i8042prt\Parameters`
- 実装: `HKCU\...\Applets\Regedit\LastKey` を設定して `regedit.exe` を起動
- Regedit が既に開いているとジャンプしないことがある（案内メッセージで再試行を促す）

### 6.7 終了時の再起動確認（後追い要件）

- レジストリ書き込み／削除に成功したら `AppSessionState.RegistryChanged = true`
- ウィンドウ終了時（`AppWindow.Closing`）に変更があればダイアログ:
  - 今すぐ再起動（`shutdown /r /t 0`）
  - 再起動せず終了
  - キャンセル（終了しない）

### 6.8 OS 設定案内

- `ms-settings:regionlanguage` を開く
- 画面上に「ハードウェア キーボードのレイアウト → 接続済みキーボード レイアウトを使用する」手順を併記

### 6.9 その他の実装詳細

- キーボード列挙: SetupAPI（`GUID_DEVCLASS_KEYBOARD`）
- 非表示デバイス: `%LocalAppData%\mana\hidden-devices.json`
- 適用元表示: デバイス / グローバル / 自動検出
- USB 反映は抜き差し、内蔵・グローバルは再起動が必要な旨をステータスに表示

### 6.10 キーボード詳細ウィンドウ・履歴（後追い要件）

- 一覧の名前クリックで非モーダル詳細ウィンドウ（複数可。同一デバイスは前面表示）
- 接続中詳細: UpperFilters → kbdclass → LowerFilters のスタック図示＋サムネイル
- メモ: `memo.md`（表示／編集タブ、閉じる時に dirty なら保存）
- 情報保存フォルダ: ツールバー「設定」で変更可（起動時には聞かない）。既定 `%LocalAppData%\mana\`、データは `{Root}\db\{sanitizedInstanceId}\`
- 設定パス自体は `%LocalAppData%\mana\storage-root.json`
- 「過去のキーボード一覧」: `db` の全件（接続中はバッジ表示）。クリックでメモ詳細（スタックなし）
- 列挙時に `meta.json` を upsert

---

## 7. モジュール構成（実装）

| 領域 | 主なファイル |
|------|----------------|
| UI | `MainWindow`, `MainPage`, `KeyboardDetailWindow`, `PastKeyboardsWindow` |
| ViewModel | `ViewModels/MainViewModel.cs` |
| 列挙 | `Services/KeyboardEnumerator.cs` |
| レジストリ | `DeviceOverrideStore`, `GlobalOverrideStore`, `RegistryLauncher` |
| 詳細・履歴 | `StorageRootStore`, `KeyboardMetaStore`, `KeyboardNoteStore`, `DeviceStackService`, `KeyboardDetailWindowManager` |
| プリセット | `LayoutCatalog` |
| 言語 | `LanguageService`, `Localization` |
| 昇格／再起動 | `ElevationService`, `SystemReboot`, `AppSessionState` |
| 配布 | `publish.bat`, `README.md` |

---

## 8. 未着手・将来候補

- MSIX / Store 配布
- デバイス着脱の自動再列挙
- `.reg` エクスポート
- note 方式（デバイス配下の `OverrideKeyboard*`）の読み取り互換
- 昇格専用の軽量ヘルパープロセス（UI を昇格させずに書き込む方式）

---

## 9. 使い方（実装時点）

1. `publish.bat` で `publish\win-x64\` を生成
2. `mana.exe` を**通常起動**
3. 必要ならアプリ内「管理者として再起動」で書き込み可能にする
4. 変更後、終了時ダイアログで OS 再起動を選択可能

詳細は `README.md` を参照。

---

## 10. 仕様 Q&A・後追い調整（2026-09-14）

### Q. グローバル設定削除で「接続済みキーボードレイアウトを使用する」になるなら、事前準備は不要では？

**回答（実装上の扱い）:**

Windows の設定 UI「ハードウェア キーボードのレイアウト」と、`i8042prt\Parameters` の `OverrideKeyboardType` / `OverrideKeyboardSubtype` は**実質的に対応**する。

| 設定 UI | レジストリ状態（検出ヒューリスティック） |
|---------|------------------------------------------|
| 接続済みキーボードレイアウトを使用する | Type/Subtype オーバーライドが**無い** |
| 日本語／英語など固定レイアウト | Type/Subtype（＋付帯値）が**書いてある** |

したがって:

- **グローバル削除** ≈ 「接続済み…を使用する」状態に寄せる操作
- **グローバル保存／プリセット適用** ≈ 「接続済み…」をやめて固定配列を強制する操作

アプリの事前準備バナーは、Type/Subtype の有無で「準備済／必要」を切り替える。設定アプリでの操作は必須ではないが、UI と状態を揃える案内として残す。

### 追加 UI（同日）

1. 管理者実行時はキャプションに `管理者: ` / `Administrator: ` を付与
2. 接続済みレイアウト相当なら案内文を「指定済み」に変更
3. グローバル保存／プリセット適用前に、接続済みレイアウトが無効化される旨の確認ダイアログ
4. 非表示チェックに ToolTip「非表示にする」
5. デバイスマネージャー名に加え、USB/Bluetooth 由来のデバイス名を併記（PC 本体コンテナ名は除外）。ラベルは「デバイス名」
6. 既定ウィンドウ高さを拡大し、サイズ・位置を `%LocalAppData%\mana\window-bounds.json` に記憶

### デバイス名の取得方針（追記・再改訂）

実機で確認した取得元:

| 接続 | 取得元 | 例 |
|------|--------|----|
| Bluetooth | `HKLM\...\Enum\BTHENUM` または `BTHLE` の `FriendlyName`（ContainerID / アドレスで紐付け） | Ewin BT5.1 Keyboard, Free 2 |
| USB | 親 `USB\VID_*&PID_*` の `DEVPKEY_Device_BusReportedDeviceDesc`（USB 記述子 iProduct 相当。USBView と同じ） | ELECOM 10KEYBOARD |

- PC 本体コンテナ名（APS2174 等）は使わない
- HID 子デバイス自体には製品名が無いことが多いため、親ノードを辿る
