# Require versioned MSIs already staged in dist by manaSetup (both cultures).
# Do not treat existing dist files as a rebuild source — that re-signed stale CAB content.
param(
    [Parameter(Mandatory = $true)][string] $InstallerDir,
    [Parameter(Mandatory = $true)][string] $DistDir,
    [Parameter(Mandatory = $true)][string] $DisplayVersion
)

$ErrorActionPreference = "Stop"

function Normalize-Dir([string] $Path) {
    $clean = $Path.Trim().TrimEnd('\', '/', '"')
    return [System.IO.Path]::GetFullPath($clean)
}

$installerRoot = Normalize-Dir $InstallerDir
$distRoot = Normalize-Dir $DistDir
New-Item -ItemType Directory -Force -Path $distRoot | Out-Null

$destEn = Join-Path $distRoot "manaSetup-$DisplayVersion-en-US.msi"
$destJa = Join-Path $distRoot "manaSetup-$DisplayVersion-ja-JP.msi"

foreach ($path in @($destEn, $destJa)) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw @"
MSI not found: $path
Build manaSetup (Release|x64) so both en-US and ja-JP are staged to dist, then rebuild mana.Sign.Msi.
Installer root: $installerRoot
"@
    }
    $item = Get-Item -LiteralPath $path
    Write-Host "mana.Sign.Msi: staging OK $($item.FullName) ($($item.LastWriteTime), $($item.Length) bytes)"
}

# Drop stale non-x64 bin outputs when x64 exists.
$x64En = Join-Path $installerRoot "bin\x64\Release\en-US\manaSetup.msi"
$oldEn = Join-Path $installerRoot "bin\Release\en-US\manaSetup.msi"
if ((Test-Path -LiteralPath $x64En) -and (Test-Path -LiteralPath $oldEn)) {
    Remove-Item -LiteralPath $oldEn -Force
}
$x64Ja = Join-Path $installerRoot "bin\x64\Release\ja-JP\manaSetup.msi"
$oldJa = Join-Path $installerRoot "bin\Release\ja-JP\manaSetup.msi"
if ((Test-Path -LiteralPath $x64Ja) -and (Test-Path -LiteralPath $oldJa)) {
    Remove-Item -LiteralPath $oldJa -Force
}

# Drop legacy unversioned names if present.
foreach ($legacy in @(
        (Join-Path $distRoot "manaSetup.en-US.msi"),
        (Join-Path $distRoot "manaSetup.ja-JP.msi")
    )) {
    if (Test-Path -LiteralPath $legacy) {
        Remove-Item -LiteralPath $legacy -Force
    }
}

Write-Output "ok"
exit 0
