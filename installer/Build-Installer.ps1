<#
  mana の配布物を作る。
    1. VS の MSBuild でアプリを publish（self-contained, win-x64）→ publish\win-x64
    2. 言語セレクター Install-mana.exe を publish
    3. mana.exe と Install-mana.exe に EV 署名（MSI に収穫する前）
    4. WiX v5 で ja-JP / en-US の MSI をカルチャ別にビルド（使用許諾 RTF を切り替え）
    5. installer\dist にセレクター、MSI、README を配置
    6. 両 MSI に EV 署名し、版付きファイル名へコピーする
  内部バージョンは exe の FileVersion（1.0.0）。外向けのファイル名は 1.00。
  -SkipPublish: 既存の publish\win-x64 をそのまま使う。
#>
param(
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$installerDir = $PSScriptRoot
$projectDir = Split-Path $installerDir -Parent
$publishDir = Join-Path $projectDir 'publish\win-x64'
$distDir = Join-Path $installerDir 'dist'
$displayVersion = '1.00'
$launcherProj = Join-Path $installerDir 'launcher\Install-mana.csproj'
$signProj = Join-Path $projectDir 'signing\mana.Sign.csproj'

function Invoke-EvSign {
    param([string[]]$Paths)
    # MSBuild のコマンドラインは ';' をスイッチ区切りとみなすため %3B にする。
    $joined = (($Paths -join ';') -replace ';','%3B')
    & dotnet msbuild $signProj /t:SignFiles /nologo /v:m "-p:SignFiles=$joined"
    if ($LASTEXITCODE -ne 0) { throw "Code signing failed ($LASTEXITCODE)." }
}

if (-not $SkipPublish) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $msbuild = & $vswhere -latest -prerelease -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (-not $msbuild) { throw 'MSBuild not found. Install Visual Studio 2022 with the WinUI / .NET desktop workload.' }

    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

    & $msbuild (Join-Path $projectDir 'mana.csproj') `
        /t:Restore`;Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 `
        "/p:PublishDir=$publishDir\" /p:SelfContained=true /p:WindowsPackageType=None `
        /p:WindowsAppSDKSelfContained=true /p:PublishTrimmed=false /p:PublishReadyToRun=false /v:m /nologo
    if ($LASTEXITCODE -ne 0) { throw "Publish failed ($LASTEXITCODE)." }
}

$appExe = Join-Path $publishDir 'mana.exe'
if (-not (Test-Path $appExe)) {
    throw "mana.exe not found in $publishDir."
}

& dotnet publish $launcherProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -nologo -v:m
if ($LASTEXITCODE -ne 0) { throw "Launcher publish failed ($LASTEXITCODE)." }

$launcherExe = Join-Path $installerDir 'launcher\bin\Release\net9.0-windows\win-x64\publish\Install-mana.exe'
if (-not (Test-Path $launcherExe)) {
    throw "Install-mana.exe not found at $launcherExe."
}

Invoke-EvSign -Paths @($appExe, $launcherExe)

$cultures = @(
    @{ Name = 'en-US'; Rtf = Join-Path $installerDir 'License\License.en-US.rtf' },
    @{ Name = 'ja-JP'; Rtf = Join-Path $installerDir 'License\License.ja-JP.rtf' }
)
New-Item -ItemType Directory -Force $distDir | Out-Null
Copy-Item $launcherExe (Join-Path $distDir 'Install-mana.exe') -Force
Copy-Item (Join-Path $projectDir 'README.md') (Join-Path $distDir 'README.md') -Force
Copy-Item (Join-Path $projectDir 'README.en.md') (Join-Path $distDir 'README.en.md') -Force

# 次のカルチャのビルドが bin を掃除することがあるので、できた MSI はすぐ dist へ退避する。
$stableMsis = @()
foreach ($culture in $cultures) {
    if (-not (Test-Path $culture.Rtf)) { throw "License RTF not found: $($culture.Rtf)" }
    & dotnet build (Join-Path $installerDir 'manaSetup.wixproj') -c Release `
        "-p:PublishDir=$publishDir\" `
        "-p:Cultures=$($culture.Name)" `
        "-p:LicenseRtfFile=$($culture.Rtf)" `
        -nologo -v:m
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed for $($culture.Name) ($LASTEXITCODE)." }

    $built = Join-Path $installerDir "bin\Release\$($culture.Name)\manaSetup.msi"
    if (-not (Test-Path $built)) { throw "MSI for $($culture.Name) not found at $built." }
    $stable = Join-Path $distDir "manaSetup.$($culture.Name).msi"
    Copy-Item $built $stable -Force
    $stableMsis += $stable
}

Invoke-EvSign -Paths $stableMsis

foreach ($culture in $cultures) {
    $stable = Join-Path $distDir "manaSetup.$($culture.Name).msi"
    $versioned = Join-Path $distDir "manaSetup-$displayVersion-$($culture.Name).msi"
    Copy-Item $stable $versioned -Force
    Write-Host "Created: $stable"
    Write-Host "Created: $versioned"
}

Write-Host "Created: $(Join-Path $distDir 'Install-mana.exe')"
