# Sync newest mana build output into publish\win-x64 for WiX harvest + signing.
# Always prefer bin\...\win-x64 over publish (publish is destination only).
# runtimeconfig.json / deps.json must be copied — excluding them leaves a broken hybrid:
# old self-contained "includedFrameworks" config after /MIR deleted coreclr.dll.
param(
    [Parameter(Mandatory = $true)][string] $RepoRoot
)

$ErrorActionPreference = "Stop"

function Normalize-Dir([string] $Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw "Path is empty."
    }
    # MSBuild 経由で末尾 \" が付くと無効文字になるため除去
    $clean = $Path.Trim().TrimEnd('\', '/', '"')
    return [System.IO.Path]::GetFullPath($clean)
}

function Test-PublishPayloadHealthy([string] $Dir) {
    $cfgPath = Join-Path $Dir "mana.runtimeconfig.json"
    if (-not (Test-Path -LiteralPath $cfgPath)) {
        return $false
    }
    $cfg = Get-Content -LiteralPath $cfgPath -Raw
    $hasFramework = $cfg -match '"framework"\s*:'
    $hasIncluded = $cfg -match '"includedFrameworks"\s*:'
    $hasCoreClr = Test-Path -LiteralPath (Join-Path $Dir "coreclr.dll")
    # Framework-dependent: must declare framework. Self-contained: must ship coreclr.
    if ($hasFramework) {
        return $true
    }
    if ($hasIncluded -and $hasCoreClr) {
        return $true
    }
    return $false
}

$repo = Normalize-Dir $RepoRoot
$publishDir = Join-Path $repo "publish\win-x64"
# publish は同期先のみ。署名で時刻が新しくなっても bin を上書きソースにしない。
$candidates = @(
    (Join-Path $repo "bin\x64\Release\net9.0-windows10.0.26100.0\win-x64"),
    (Join-Path $repo "bin\Release\net9.0-windows10.0.26100.0\win-x64")
)

$dllItems = @()
foreach ($dir in $candidates) {
    $dll = Join-Path $dir "mana.dll"
    if (Test-Path -LiteralPath $dll) {
        $dllItems += Get-Item -LiteralPath $dll
    }
}
if ($dllItems.Count -eq 0) {
    # Fall back to existing publish only if it is already a healthy payload (e.g. publish.bat SCD).
    if ((Test-Path -LiteralPath (Join-Path $publishDir "mana.dll")) -and (Test-PublishPayloadHealthy $publishDir)) {
        Write-Host "mana.Sign.Binaries: no bin output; using healthy publish ($publishDir)"
        Write-Output ([System.IO.Path]::GetFullPath($publishDir))
        exit 0
    }
    throw "mana.dll not found under bin. Build mana (Release|x64) first."
}

$srcDll = $dllItems | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
$srcDir = $srcDll.Directory.FullName

$required = @(
    "mana.exe",
    "mana.dll",
    "mana.pri",
    "MainWindow.xbf",
    "MainPage.xbf",
    "KeyboardDetailWindow.xbf",
    "PastKeyboardsWindow.xbf",
    "mana.runtimeconfig.json",
    "mana.deps.json"
)
foreach ($name in $required) {
    $path = Join-Path $srcDir $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Missing $name in $srcDir. Build mana Release|x64 so WinUI resources are generated."
    }
}

$pubFull = [System.IO.Path]::GetFullPath($publishDir)
$mustSync = $true
if ((Test-Path -LiteralPath $pubFull) -and (Test-PublishPayloadHealthy $pubFull)) {
    $pubDll = Get-Item -LiteralPath (Join-Path $pubFull "mana.dll")
    # Skip only when publish already mirrors this bin build (same length) and is healthy.
    # Do not use LastWriteTime — signing makes publish look newer and freezes a bad payload.
    if ($pubDll.Length -eq $srcDll.Length -and (Test-PublishPayloadHealthy $srcDir)) {
        # Still refresh runtimeconfig/deps in case they were previously excluded.
        Copy-Item -LiteralPath (Join-Path $srcDir "mana.runtimeconfig.json") -Destination (Join-Path $pubFull "mana.runtimeconfig.json") -Force
        Copy-Item -LiteralPath (Join-Path $srcDir "mana.deps.json") -Destination (Join-Path $pubFull "mana.deps.json") -Force
        if (Test-PublishPayloadHealthy $pubFull) {
            Write-Host "mana.Sign.Binaries: publish payload OK; refreshed runtimeconfig/deps from $srcDir"
            $mustSync = $false
        }
    }
}

if ($mustSync) {
    Write-Host "mana.Sign.Binaries: syncing $srcDir -> $pubFull"
    New-Item -ItemType Directory -Force -Path $pubFull | Out-Null
    # Do NOT exclude *.deps.json / *.runtimeconfig.json (that created the broken SCD/FDD hybrid).
    & robocopy $srcDir $pubFull /MIR /NFL /NDL /NJH /NJS /nc /ns /np `
        /XF *.pdb *.xml | Out-Null
    if ($LASTEXITCODE -ge 8) {
        throw "robocopy failed with exit $LASTEXITCODE"
    }
}

foreach ($name in $required) {
    $path = Join-Path $pubFull $name
    if (-not (Test-Path -LiteralPath $path)) {
        throw "After sync, missing $path"
    }
}
if (-not (Test-PublishPayloadHealthy $pubFull)) {
    throw @"
Publish payload is unhealthy after sync: $pubFull
mana.runtimeconfig.json must either declare framework-dependent 'framework', or be self-contained with coreclr.dll present.
"@
}

Write-Output $pubFull
exit 0
