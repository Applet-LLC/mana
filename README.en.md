# mana as setting keyboard layout

A Windows 11 tool that lists connected keyboards and shows or changes the hardware layout (keyboard type and subtype), either per device or globally (`i8042prt`). The version shown to users is 1.00.

Source code is under the [MIT License](LICENSE). Use of the signed installer and binaries follows the license agreement shown during setup.

## Requirements

- 64-bit Windows 11 (build 22000 or later)

## Install

Run `Install-mana.exe` from the distribution folder. Choose Japanese or English, then install.

- Japanese: `manaSetup-1.00-ja-JP.msi`
- English: `manaSetup-1.00-en-US.msi`

The MSI for the language you choose must sit in the same folder as `Install-mana.exe`. Files are installed to `C:\Program Files\mana`. The Start menu shortcut is created inside the **Applet** group, named "mana as setting keyboard layout".

## Use

1. Start it from the Applet group in the Start menu. Start it with normal privileges.
2. Connected keyboards are listed. Use Hide to keep a row out of the list.
3. A device override writes type and subtype for the selected keyboard only.
4. The global override is `i8042prt`. Apply a preset (Japanese 106, English 101/102, Korean) or type the numbers yourself. A device override takes priority over the global override.
5. Writing the registry requires administrator privileges. Use Restart as Administrator inside the app.
6. A USB keyboard may need to be unplugged and plugged back in. A built-in keyboard or a global change may need a Windows restart. You can choose to restart when you close the app.
7. Click a keyboard name in the list to open a detail window with the driver stack, a Markdown memo, and images.
8. Keyboard history lists previously seen keyboards so you can open their memos (without the driver stack).
9. Change the notes/images/history folder under Settings. The default is `%LocalAppData%\mana` (a `db` subfolder is created under it).

The app can also open Settings at Hardware keyboard layout. Clearing the global override is the same idea as "Use connected keyboard layout".

## Startup

**Do not right-click the executable and choose Run as administrator.** Starting elevated can make this WinUI 3 app exit in `Microsoft.UI.Xaml.dll`. Browse while running normally, and elevate only from inside the app when you need to write.

If startup fails, see `%LocalAppData%\mana\startup-crash.log`. UI language, window position, and the default folder for keyboard notes/history are stored in the same place.

## Building

You can build the whole solution in Visual Studio 2022 (app, language selector, installers, and EV signing). `manaSetup` (WiX v5) requires [HeatWave for Visual Studio 2022](https://marketplace.visualstudio.com/items?itemName=FireGiant.FireGiantHeatWaveDev17).

Output is `installer\dist`. The internal version in `mana.csproj` is `1.0.0`. Distribution file names use `1.00`. If the EV certificate (subject: Applet LLC) is not present, signing is skipped and the build continues.
