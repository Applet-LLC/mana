# mana as setting keyboard layout

A Windows 11 tool that shows which hardware layout (keyboard type and subtype) each connected keyboard is recognized as, and lets you change it. Changes can apply to a single keyboard or to the whole PC (`i8042prt`). The version shown to users is 1.00.

Typical situations where it helps:

- You plug a US-layout USB keyboard into a laptop with a Japanese (JIS) keyboard, and the symbols come out in the wrong places (depending on how the built-in keyboard is connected, there is a limitation; see [Limitations](#limitations))
- You want the whole PC to treat your keyboard as a US layout
- You want to keep notes and photos about each keyboard and how it is configured

Source code is under the [MIT License](LICENSE). Use of the signed installer and binaries follows the license agreement shown during setup. For the licenses of third-party software included with the app (Markdig, Windows App SDK, WebView2, and others), see [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt). This file is also installed in the installation folder.

## Requirements

- 64-bit Windows 11 (build 22000 or later)
- [.NET Desktop Runtime 9](https://dotnet.microsoft.com/download/dotnet/9.0) (x64)

  The app is distributed without the .NET runtime (framework-dependent). You need **.NET Desktop Runtime 9** (x64) on the PC. The ASP.NET Core Runtime or the SDK will not work instead. If it is missing, install it from the link above.

  If the app asks you to install .NET but the .NET installer says it is already installed, the cause may be an inconsistent package rather than a missing runtime. Reinstall with the latest MSI.

## Install

Run `Install-mana.exe` from the distribution folder, choose Japanese or English, and install. The MSI used depends on the language you choose:

| Language | MSI |
|---|---|
| 日本語 | `manaSetup-1.00-ja-JP.msi` |
| English | `manaSetup-1.00-en-US.msi` |

The MSI for the language you choose must be in the same folder as `Install-mana.exe`.

- Install location: `C:\Program Files\applet\mana`
- Start menu: "mana as setting keyboard layout" is added to the **Applet** group

The **Applet** group may not appear in the Start menu list. If you cannot find it, search for "mana" or "まな" in the Start menu.

## Use

### Basic workflow

1. Start the app from the Start menu **with normal privileges** (see [Startup](#startup)).
2. Connected keyboards are listed. For each one, you can see its current layout and where that layout comes from (Device / Global / Auto-detect).
3. To change settings, click **Restart as Administrator** in the app. Writing to the registry (HKLM) requires administrator rights.
4. Save a device override or a global override.
5. Apply the change. Unplug and replug a USB keyboard; a built-in keyboard or a global change needs a Windows restart. When you close the app, you can choose to restart right away.

### Device override vs. global override

| | Device override | Global override |
|---|---|---|
| Scope | The keyboard selected in the list | The whole PC |
| Registry key | `HKLM\SYSTEM\CurrentControlSet\Enum\<device instance path>\Device Parameters` | `HKLM\SYSTEM\CurrentControlSet\Services\i8042prt\Parameters` |
| Values | `KeyboardTypeOverride`, `KeyboardSubtypeOverride` | `OverrideKeyboardType`, `OverrideKeyboardSubtype` (some presets also write `OverrideKeyboardIdentifier` and `LayerDriver JPN` / `LayerDriver KOR`) |
| How to apply | USB: unplug and replug. Built-in: restart | Restart |

If both are set, the device override takes priority for that keyboard.

The global override is the same setting as **Hardware keyboard layout** under Windows Settings → Time & language → Language & region → language options. Saving a global override turns off "Use connected keyboard layout" and fixes the layout for the whole PC. Clearing the global override returns to the same state as "Use connected keyboard layout". The app also has a button that opens this Windows settings page.

### Presets

Both the device override and the global override let you pick one of the following under **Country / layout preset**. Choose **Custom** to type the type and subtype numbers yourself. The values in the "Also written" column are written only when you apply a preset to the global override. A device override writes only the type and subtype.

| Preset | Type | Subtype | Also written |
|---|---|---|---|
| Enhanced 101/102 (US) | 4 | 0 | None (`OverrideKeyboardIdentifier` and `LayerDriver JPN` / `KOR` are removed) |
| Japanese (JIS) | 7 | 2 | `OverrideKeyboardIdentifier` = `PCAT_106KEY`, `LayerDriver JPN` = `kbd106.dll` |
| Korean 101 Type 1 | 8 | 3 | `OverrideKeyboardIdentifier` = `STANDARD`, `LayerDriver KOR` = `kbd101a.dll` |
| Korean 101 Type 3 | 8 | 5 | `OverrideKeyboardIdentifier` = `STANDARD`, `LayerDriver KOR` = `kbd101c.dll` |
| Custom | Any | Any | Type and subtype entered directly |

When nothing is set, the layout is shown as "Auto-detect" and Windows decides based on the keyboard.

## Examples

### Example 1: Use a US-layout USB keyboard with a JIS laptop

Keep the built-in keyboard as Japanese and make only the external keyboard behave as US layout.

> [!WARNING]
> On many laptops, the built-in keyboard is connected as a PS/2 keyboard through ACPI. In that case, clearing the global override in step 2 makes **the built-in keyboard be recognized as US layout, and a device override cannot change it back to Japanese (JIS)** (see [Limitations](#limitations)).
>
> Before you start, select the built-in keyboard in the list and check its **Device instance path**. If it starts with `ACPI\`, it is a PS/2 keyboard. If you want to keep the built-in keyboard as Japanese layout, do not follow these steps, and leave the global override set to **Japanese (JIS)**.

1. Start mana and click **Restart as Administrator**.
2. If the top of the window says "Preparation needed", a global override is still set. Click **Clear global overrides**. (If it says "Prepared", skip this step.)
3. Select the external USB keyboard in the list.
4. Under **Device override**, choose **Enhanced 101/102 (US)** in **Country / layout preset** (this fills in type `4` and subtype `0`), then click **Apply device override**.
5. Unplug and replug the USB keyboard. If you cleared the global override in step 2, also restart Windows.
6. Check that symbols such as `@` and `[` on the external keyboard match the printed keycaps.

### Example 2: Fix the whole PC to US layout

For example, on a desktop PC that only uses a US-layout keyboard.

1. Start mana and click **Restart as Administrator**.
2. Under **Global override (i8042prt)**, choose **Enhanced 101/102 (US)** in **Country / layout preset**, then click **Apply global preset**.
3. When asked "Disable connected keyboard layout?", click **Continue**.
4. When you close the app, choose **Restart now** to restart Windows.

To fix the PC to Japanese layout instead, choose **Japanese (JIS)** in step 2.

### Example 3: Undo the changes

- **Undo for one keyboard**: Select the keyboard in the list, click **Clear device override**, then replug it or restart.
- **Stop fixing the whole PC**: Click **Clear global overrides** and restart Windows. This is the same state as "Use connected keyboard layout".

When both are cleared, the layout returns to "Auto-detect".

### Example 4: Keep notes about a keyboard

1. Click a keyboard name in the list to open its detail window.
2. You can view the driver stack, write a memo in Markdown, and view images. For example:

   ```markdown
   ## Company-issued US keyboard
   - Purchased: 2026-04
   - Setting: Type 4 / Subtype 0 (device override)
   - [ ] Replace keycaps
   ```

3. **Open folder** opens the folder for that keyboard. Put a JPG or PNG image there, and it appears as the thumbnail in the list and in the detail window.

The Markdown syntax supported in memos is described in [docs/MEMO-MARKDOWN.md](docs/MEMO-MARKDOWN.md) (Japanese). Memos for keyboards that are not currently connected can be opened from **Keyboard history** (without the driver stack).

## Other features

- **Hide from the list**: Use the "Hide this device" check box on each row to hide keyboards you do not use (such as virtual devices). Turn on "Show hidden keyboards in the list" under **Settings** to show them again.
- **Open the registry directly**: **Open device registry** and **Open global registry** open the corresponding key in Registry Editor.
- **Locale layout file**: For Japanese (`00000411`), English (`00000409`), and Korean (`00000412`), you can change `Layout File` under `HKLM\SYSTEM\CurrentControlSet\Control\Keyboard Layouts\<locale>` (for example `KBDJPN.DLL`, `kbd106.dll`, or `KBDUS.DLL`). This changes standard Windows behavior and is intended for advanced users. Write down the original value before changing it.
- **Display language**: Switch between 日本語 and English under **Settings**.

## Where data is stored

| Data | Location |
|---|---|
| Memos, images, connection history | `%LocalAppData%\mana\db\<folder per keyboard>` (can be changed under **Settings**) |
| Display language, window position, hidden keyboards | `%LocalAppData%\mana` |
| Startup failure log | `%LocalAppData%\mana\startup-crash.log` |

The folder name for each keyboard is its device instance path with `\` replaced by `_`. For example, the memo for `HID\VID_046D&PID_C31C&MI_00\7&1A2B3C4D&0&0000` is saved as `%LocalAppData%\mana\db\HID_VID_046D&PID_C31C&MI_00_7&1A2B3C4D&0&0000\memo.md`.

Uninstalling the app does not delete this data. Delete it manually if you no longer need it.

## Startup

**Do not right-click the executable and choose Run as administrator.** Starting the app elevated can make WinUI 3 crash in `Microsoft.UI.Xaml.dll`. Check settings with normal privileges, and use **Restart as Administrator** inside the app only when you need to write.

## Limitations

### PS/2 keyboards follow only the global override

For HID keyboards connected over USB or Bluetooth, you can set the layout with a device override. For a PS/2 keyboard connected through ACPI (common for built-in laptop keyboards), however, the layout is determined only by the global override (`i8042prt`). Device overrides have no effect.

In addition, a PS/2 keyboard cannot report its keyboard type through Plug and Play. So when you clear the global override to return to "Use connected keyboard layout", a PS/2 keyboard is recognized as US layout (Enhanced 101/102).

As a result, the following combination is not possible:

- The laptop's built-in keyboard (PS/2) uses Japanese (JIS) layout
- An external USB or Bluetooth keyboard uses US layout

If you clear the global override so you can set the external keyboard per device, the built-in keyboard becomes US layout. The only way to make the built-in keyboard Japanese (JIS) is to set the global override to **Japanese (JIS)**.

To check whether a keyboard is PS/2, select it in the list and see whether its **Device instance path** starts with `ACPI\`.

This limitation comes from how Windows works. It is expected to go away if a future version of Windows improves this behavior.

## Troubleshooting

| Symptom | What to check |
|---|---|
| Cannot find the app in the Start menu | Search for "mana" or "まな" in the Start menu. |
| The app does not start or closes immediately | Make sure you did not start it with Run as administrator. The cause is recorded in `%LocalAppData%\mana\startup-crash.log`. |
| "Install .NET" appears | Install .NET Desktop Runtime 9 (x64). If it appears even though the runtime is installed, reinstall with the latest MSI. |
| Saving does nothing | Administrator rights are required. Click **Restart as Administrator**. |
| The layout does not change after saving | A USB keyboard needs to be unplugged and replugged; a built-in keyboard or a global change needs a Windows restart. If a device override has no effect, also check that no global override remains (the top of the window should not say "Preparation needed"). |
| After clearing the global override, the laptop's built-in keyboard became US layout | This is the limitation for PS/2 keyboards (see [Limitations](#limitations)). Apply the **Japanese (JIS)** preset to the global override and restart Windows to restore it. |
| Symbols are in the wrong places | Follow [Example 3: Undo the changes](#example-3-undo-the-changes) to clear the settings, then restart. |

## Building

You can build the whole solution in Visual Studio 2022 (app, language selector launcher, installers, and EV signing). Building the installer project `manaSetup` (WiX v5) requires [HeatWave for Visual Studio 2022](https://marketplace.visualstudio.com/items?itemName=FireGiant.FireGiantHeatWaveDev17).

- Output folder: `installer\dist`
- Version: the internal version in `mana.csproj` is `1.0.0`; distribution file names use `1.00`
- Signing: if the EV certificate (subject: Applet LLC) is not found, signing is skipped and the build continues

When you update a NuGet package, also update [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

See [ARCHITECTURE.md](ARCHITECTURE.md) for design details.

---

Last updated: 7 October 2026
