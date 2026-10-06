# Resolve Install-mana.exe path: the single-file publish output only.
# The plain build output (apphost) does not run standalone, and picking by size can select a stale
# publish left over from an earlier run, so only the publish output is accepted; if it is missing, stop.
param(
    [Parameter(Mandatory = $true)][string] $LauncherDir
)

$ErrorActionPreference = "Stop"
$root = $LauncherDir.Trim().TrimEnd('\', '/', '"')
$publishExe = Join-Path $root "Release\net9.0-windows\win-x64\publish\Install-mana.exe"
if (-not (Test-Path -LiteralPath $publishExe)) {
    throw "Install-mana.exe (single-file publish) not found: $publishExe. Run Build-Installer.ps1, or: dotnet publish installer\launcher\Install-mana.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true"
}
Write-Output (Get-Item -LiteralPath $publishExe).FullName
exit 0
