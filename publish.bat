@echo off
setlocal
cd /d "%~dp0"

set OUTDIR=%~dp0publish\win-x64
set MSBUILD="%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"

for /f "usebackq tokens=*" %%i in (`%MSBUILD% -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe`) do set MSB=%%i

if not defined MSB (
  echo MSBuild not found. Install Visual Studio 2022 with MSBuild / WinUI workload.
  exit /b 1
)

if exist "%OUTDIR%" rmdir /s /q "%OUTDIR%"

"%MSB%" mana.csproj /t:Restore;Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:PublishDir="%OUTDIR%\\" /p:SelfContained=true /p:WindowsPackageType=None /p:WindowsAppSDKSelfContained=true /p:PublishTrimmed=false /p:PublishReadyToRun=false /v:m

if errorlevel 1 (
  echo Publish failed.
  exit /b 1
)

echo.
echo Published to: %OUTDIR%
echo Run mana.exe normally (do NOT right-click Run as administrator).
echo Use the in-app "Restart as Administrator" button only when writing registry values.
endlocal
