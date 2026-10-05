# Resolve Install-mana.exe path (prefer single-file publish, else largest build output).
param(
    [Parameter(Mandatory = $true)][string] $LauncherDir
)

$ErrorActionPreference = "Stop"
$root = $LauncherDir.Trim().TrimEnd('\', '/', '"')
$candidates = @(
    (Join-Path $root "Release\net9.0-windows\win-x64\publish\Install-mana.exe"),
    (Join-Path $root "x64\Release\net9.0-windows\win-x64\Install-mana.exe"),
    (Join-Path $root "Release\net9.0-windows\win-x64\Install-mana.exe")
)
$items = @()
foreach ($c in $candidates) {
    if (Test-Path -LiteralPath $c) {
        $items += Get-Item -LiteralPath $c
    }
}
if ($items.Count -eq 0) {
    throw "Install-mana.exe not found under launcher\bin. Build/Publish Install-mana first."
}
$pick = $items | Sort-Object Length -Descending | Select-Object -First 1
Write-Output $pick.FullName
exit 0
