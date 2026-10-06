# mana アーキテクチャ（実装仕様）

キーボードレイアウト設定ツール mana のうち、接続キーボードの列挙・表示名・デバイスごとレジストリ位置の実装仕様。

LayerDriver / kbdlayer の DeviceId ハッシュ算出は本ドキュメントの対象外（LayerDriver 側 `docs/DEVICE-ID.md` を参照）。

---

## 1. 接続キーボードの列挙

### 呼び出し経路

| タイミング | 経路 |
|---|---|
| 起動時 | `MainPage.MainPage_Loaded` → `MainViewModel.Reload()` → `KeyboardEnumerator.Enumerate()` |
| 「最新の情報に更新」ボタン | 同上（`ReloadButton_Click` → `Reload()`） |

実装の本体は `Services/KeyboardEnumerator.cs` の `Enumerate()`。

### 使用 API（SetupAPI）

接続中（Present）の **Keyboard クラス** デバイスノードを列挙する。

| 順序 | Win32 API | 役割 |
|---|---|---|
| 1 | `SetupDiGetClassDevs` | クラス GUID `GUID_DEVCLASS_KEYBOARD`（`4d36e96b-e325-11ce-bfc1-08002be10318`）かつフラグ `DIGCF_PRESENT` でデバイス情報セットを取得 |
| 2 | `SetupDiEnumDeviceInfo` | セット内の各デバイスを順に列挙 |
| 3 | `SetupDiGetDeviceInstanceId` | デバイスインスタンス ID（一覧のパス表示・レジストリキー組み立てに使用） |
| 4 | `SetupDiGetDeviceRegistryProperty` | FriendlyName / DeviceDesc / HardwareID / Mfg など |
| 5 | `SetupDiDestroyDeviceInfoList` | セット破棄 |

関連コード位置:

- 列挙ループ: `KeyboardEnumerator.Enumerate()`（おおよそ L49–115）
- P/Invoke 宣言: 同ファイル末尾の `SetupDi*` 群

起動後の挿抜は自動監視していない。再列挙はユーザーが「最新の情報に更新」を押したときに行う。

---

## 2. キーボード名称（デバイス名）の取得アーキテクチャ

一覧には **2 系統の名前** がある。

| フィールド | UI | 意味 |
|---|---|---|
| `FriendlyName` | 一覧の上段（下線・タップで詳細） | デバイスマネージャー相当 |
| `ShellFriendlyName` | その直下（あるときだけ表示） | コントロールパネルの「デバイスとプリンター」寄りの製品名 |

モデル: `Models/KeyboardDeviceInfo.cs`  
解決ロジック: `KeyboardEnumerator.ResolveDeviceName()`（および関連 private メソッド）

### 2.1 デバイスマネージャー名（`FriendlyName`）

キーボードデバイスノード自身から取る。

1. `SetupDiGetDeviceRegistryProperty(..., SPDRP_FRIENDLYNAME)`
2. 無ければ `SPDRP_DEVICEDESC`
3. それも無ければデバイスインスタンス ID

多くの HID キーボードでは「HID キーボード デバイス」などの汎用名になる。

### 2.2 製品寄りの名前（`ShellFriendlyName`）— フォールバック列

`ResolveDeviceName` は、デバイスマネージャー名より具体的な候補を次の順で試し、**最初に「有用」と判定されたもの**を返す。どれもダメなら `null`（UI では Shell 名行を出さない）。

「有用」の条件（`IsUsefulDeviceName`）:

- 空でない
- デバイスマネージャー名と同一でない
- 汎用名リスト（`GenericDeviceNames`）に含まれない  
  （例: `HID Keyboard Device` / `Bluetooth HID Device` / `USB Input Device` など）

#### 優先順位

```
1. Bluetooth 事前索引（ContainerId）
2. Bluetooth 事前索引（BD アドレス）
3. 親方向に辿った BTHENUM / BTHLE / BTHLEDEVICE の Enum\...\FriendlyName
4. 親方向に辿った USB\VID_* の BusReportedDeviceDesc（→ FriendlyName）
5. 自身の DEVPKEY_Device_BusReportedDeviceDesc
```

#### Bluetooth（コントロールパネルと同系統になりやすい経路）

列挙開始前に `LoadBluetoothFriendlyNames()` がレジストリを走査する。

- 走査根: `HKLM\SYSTEM\CurrentControlSet\Enum\BTHENUM` および `...\Enum\BTHLE`
- 各キーの `FriendlyName`（汎用名は除外）を、次のキーで索引化  
  - `ContainerID` → `ByContainerId`  
  - パス中の `Dev_xxxxxxxxxxxx` / `BluetoothDevice_...` 等から抽出した BD アドレス → `ByAddress`

キーボード側では:

1. `SetupDiGetDeviceProperty(..., DEVPKEY_Device_ContainerId)` で ContainerId を取り、索引を引く
2. キーボードの InstanceId から BD アドレスを抽出し、索引を引く
3. だめなら `CM_Get_Parent` で親を最大 12 段辿り、`BTHENUM\` / `BTHLE\` / `BTHLEDEVICE\` のノードで `Enum\...\FriendlyName` を読む（`FindBluetoothRegistryFriendlyName`）

これが「デバイスとプリンター」で見えるメーカー名・製品名に近い文字列になることが多い。

#### USB

`CM_Get_Parent` で親を辿り、`USB\VID_` で始まるノードを探す（`FindUsbProductName`）。

1. 優先: `CM_Get_DevNode_Property(..., DEVPKEY_Device_BusReportedDeviceDesc)`  
   （USB 記述子の iProduct 相当。USBView と同じ系統）
2. まれなケース: 同ノードの FriendlyName / DeviceDesc（`DEVPKEY_Device_FriendlyName` または `CM_Get_DevNode_Registry_Property`）

PC 本体コンテナ名などは、汎用名判定と「デバイスマネージャー名と同一なら捨てる」規則で実質除外する方針。

#### 最後の手段

キーボードノード自身の `DEVPKEY_Device_BusReportedDeviceDesc`。

### 2.3 UI での使い分け

- 一覧上段: 常に `FriendlyName`（デバイスマネージャー名）
- 一覧下段: `ShellFriendlyName` があるときだけ表示（製品名）
- 詳細ウィンドウのタイトル等: Shell 名があればそちらを優先（`MainPage.DeviceName_Tapped`）

---

## 3. デバイスごとのキーボードレイアウト用レジストリ位置

### パスの組み立て

デバイスインスタンス ID（列挙時の `SetupDiGetDeviceInstanceId` の戻り値）を `instancePath` として、次を連結する。

```
HKLM\SYSTEM\CurrentControlSet\Enum\<instancePath>\Device Parameters
```

コード上の相対パス（`Registry.LocalMachine` 配下）:

```
SYSTEM\CurrentControlSet\Enum\{instancePath}\Device Parameters
```

| 用途 | 実装 |
|---|---|
| 読み書き | `Services/DeviceOverrideStore.cs` の `OpenDeviceParameters` |
| UI 表示・regedit 起動用のフルパス文字列 | `Services/RegistryLauncher.GetDeviceParametersPath` |

`instancePath` は例えば `HID\VID_xxxx&PID_yyyy\...` の形。追加の正規化や別キーへの変換は行わない（SetupAPI が返した文字列をそのまま Enum 配下のパスに使う）。

書き込み時にキーが無い場合は `CreateSubKey` で `Device Parameters` を作成する。

### 書き込む値（デバイスオーバーライド）

| 値名 | 型 |
|---|---|
| `KeyboardTypeOverride` | DWORD |
| `KeyboardSubtypeOverride` | DWORD |

実効レイアウトの優先順位（アプリ側の解釈）:

1. 上記デバイスオーバーライド
2. グローバル `HKLM\SYSTEM\CurrentControlSet\Services\i8042prt\Parameters` の Type/Subtype
3. PnP 自動検出（オーバーライドなし）

グローバル側の詳細は `PLAN.md` および `GlobalOverrideStore` を参照。

---

## 4. ロケール別レイアウトファイル

| 項目 | 内容 |
|---|---|
| レジストリ | `HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layouts\{LCID}` の `Layout File`（REG_SZ） |
| 選択可能な LCID | `00000411`（日本語） / `00000409`（英語） / `00000412`（韓国語） |
| UI 既定 | 日本語モード → `00000411`、English モード → `00000409` |
| 実装 | `Services/LocaleLayoutStore.cs`、メイン画面のグローバル設定の上 |

---

## 5. キーボード詳細ウィンドウのメモ

| 項目 | 内容 |
|---|---|
| 本文 | 各キーボードフォルダの `memo.md` |
| フォント設定 | 同フォルダの `memo-ui.json`（`FontFamily` / `FontSize`）。キーボード単位 |
| Markdown 仕様 | `docs/MEMO-MARKDOWN.md`（Markdig `UseAdvancedExtensions`） |
| 表示/編集切替 | 「表示 / 編集」1ボタンでトグル。現在モード側を強調表示 |
