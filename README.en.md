# mana as setting keyboard layout

A Windows 11 tool that lists connected keyboards and shows or changes the hardware layout (keyboard type and subtype), either per device or globally (`i8042prt`). The version shown to users is 1.00.

Source code is under the [MIT License](LICENSE). Use of the signed installer and binaries follows the license agreement shown during setup.

## Requirements

- 64-bit Windows 11 (build 22000 or later)

## Install

Run `Install-mana.exe` from the distribution folder. Choose Japanese or English, then install.

- Japanese: `manaSetup.ja-JP.msi`
- English: `manaSetup.en-US.msi`

The MSI for the language you choose must sit in the same folder as `Install-mana.exe`. Files are installed to `C:\Program Files\mana`. The Start menu shortcut is created inside the **Applet** group, named "mana as setting keyboard layout".

## Use

1. Start it from the Applet group in the Start menu. Start it with normal privileges.
2. Connected keyboards are listed. Use Hide to keep a row out of the list.
3. A device override writes type and subtype for the selected keyboard only.
4. The global override is `i8042prt`. Apply a preset (Japanese 106, English 101/102, Korean) or type the numbers yourself. A device override takes priority over the global override.
5. Writing the registry requires administrator privileges. Use Restart as Administrator inside the app.
6. A USB keyboard may need to be unplugged and plugged back in. A built-in keyboard or a global change may need a Windows restart. You can choose to restart when you close the app.

The app can also open Settings at Hardware keyboard layout. Clearing the global override is the same idea as "Use connected keyboard layout".

## Startup

**Do not right-click the executable and choose Run as administrator.** Starting elevated can make this WinUI 3 app exit in `Microsoft.UI.Xaml.dll`. Browse while running normally, and elevate only from inside the app when you need to write.

If startup fails, see `%LocalAppData%\mana\startup-crash.log`. UI language and window position are stored in the same folder.

## Building

Building needs Visual Studio 2022 MSBuild. `dotnet build` alone can fail to generate WinUI PRI resources.

```bat
publish.bat
```

That writes `publish\win-x64\mana.exe`. To build both installers, the language selector, and EV signatures:

```powershell
powershell -ExecutionPolicy Bypass -File installer\Build-Installer.ps1
```

Output is `installer\dist`. The internal version in `mana.csproj` is `1.0.0`. Distribution file names use `1.00`. If the EV certificate (subject: Applet LLC) is not present, signing is skipped and the build continues.

`installer\manaSetup.wixproj` (WiX v5) is not a project type Visual Studio loads by default ("incompatible"). It is not in the solution. Build it with `Build-Installer.ps1` from the command line. To edit it inside Visual Studio, install [HeatWave for Visual Studio 2022](https://marketplace.visualstudio.com/items?itemName=FireGiant.FireGiantHeatWaveDev17) and add the project to the solution. This is unrelated to the "Pre-MSBuild projects" note on Microsoft Learn.
