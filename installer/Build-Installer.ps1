<#
  mana の配布物を作る。
    1. VS の MSBuild でアプリを publish（self-contained, win-x64）→ publish\win-x64
    2. 言語セレクター Install-mana.exe を publish
    3. mana.Sign.Binaries で mana.exe / mana.dll / Install-mana.exe に EV 署名
    4. WiX v5 で ja-JP / en-US の MSI をカルチャ別にビルド（使用許諾 RTF を切り替え）
    5. installer\dist にセレクター、版付き MSI、README を配置
    6. mana.Sign.Msi で両 MSI に EV 署名（無版付きファイル名は残さない）
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
$launcherProj = Join-Path $installerDir 'launcher\Install-mana.csproj'
$signBinariesProj = Join-Path $projectDir 'signing\mana.Sign.Binaries.csproj'
$signMsiProj = Join-Path $projectDir 'signing\mana.Sign.Msi.csproj'

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
$appDll = Join-Path $publishDir 'mana.dll'
$appPri = Join-Path $publishDir 'mana.pri'
$appXbf = Join-Path $publishDir 'MainWindow.xbf'
foreach ($required in @($appExe, $appDll, $appPri, $appXbf)) {
    if (-not (Test-Path $required)) {
        throw "Required publish output missing: $required. Re-run without -SkipPublish (VS MSBuild publish.bat)."
    }
}

& dotnet publish $launcherProj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -nologo -v:m
if ($LASTEXITCODE -ne 0) { throw "Launcher publish failed ($LASTEXITCODE)." }

$launcherExe = Join-Path $installerDir 'launcher\bin\Release\net9.0-windows\win-x64\publish\Install-mana.exe'
if (-not (Test-Path $launcherExe)) {
    # VS の Platform=x64 ビルド出力（publish していない場合）
    $launcherExe = Join-Path $installerDir 'launcher\bin\x64\Release\net9.0-windows\win-x64\Install-mana.exe'
}
if (-not (Test-Path $launcherExe)) {
    throw "Install-mana.exe not found. Publish or build Install-mana first."
}

& dotnet msbuild $signBinariesProj /t:Build /nologo /v:m
if ($LASTEXITCODE -ne 0) { throw "Binary code signing failed ($LASTEXITCODE)." }

foreach ($signed in @($appExe, $appDll)) {
    $status = (Get-AuthenticodeSignature -LiteralPath $signed).Status
    if ($status -ne 'Valid') {
        Write-Warning "Authenticode Status for $(Split-Path $signed -Leaf) is '$status' (EV token missing?). MSI will embed this file as-is."
    } else {
        Write-Host "Signed OK: $(Split-Path $signed -Leaf)"
    }
}

# 署名後も WinUI リソースが残っていること（途中の掃除で消えるとインストール後に XAML 失敗する）
foreach ($required in @($appPri, $appXbf)) {
    if (-not (Test-Path $required)) {
        throw "WinUI resource disappeared after signing: $required"
    }
}

$cultures = @(
    @{ Name = 'en-US'; Rtf = Join-Path $installerDir 'License\License.en-US.rtf' },
    @{ Name = 'ja-JP'; Rtf = Join-Path $installerDir 'License\License.ja-JP.rtf' }
)
New-Item -ItemType Directory -Force $distDir | Out-Null
# Install-mana.exe は mana.Sign.Binaries が署名後に dist へコピーする
# README / License は mana.Sign.Msi の StageDistDocs でも同期するが、CLI 単体実行時も揃える
$distDocs = @(
    (Join-Path $projectDir 'README.md'),
    (Join-Path $projectDir 'README.en.md'),
    (Join-Path $projectDir 'THIRD-PARTY-NOTICES.txt'),
    (Join-Path $installerDir 'License\License.en-US.rtf'),
    (Join-Path $installerDir 'License\License.ja-JP.rtf')
)
foreach ($doc in $distDocs) {
    if (-not (Test-Path -LiteralPath $doc)) { throw "Distribution doc missing: $doc" }
    Copy-Item -LiteralPath $doc -Destination (Join-Path $distDir (Split-Path $doc -Leaf)) -Force
    Write-Host "Staged doc: $(Split-Path $doc -Leaf)"
}

# 次のカルチャのビルドが bin を掃除することがあるので、できた MSI はすぐ版付き名で dist へ退避する。
# WiX 増分 CAB が古いまま残らないよう、各カルチャは Rebuild する（wixproj 側でも中間出力を破棄する）。
foreach ($culture in $cultures) {
    if (-not (Test-Path $culture.Rtf)) { throw "License RTF not found: $($culture.Rtf)" }
    # SkipOtherCulture: このスクリプトが両カルチャを順にビルドするため、wixproj 側の連鎖ビルドは不要
    & dotnet msbuild (Join-Path $installerDir 'manaSetup.wixproj') `
        -t:Rebuild `
        -p:Configuration=Release `
        "-p:PublishDir=$publishDir\" `
        "-p:Cultures=$($culture.Name)" `
        "-p:LicenseRtfFile=$($culture.Rtf)" `
        -p:SkipOtherCulture=true `
        -nologo -v:m
    if ($LASTEXITCODE -ne 0) { throw "Installer build failed for $($culture.Name) ($LASTEXITCODE)." }

    $built = Join-Path $installerDir "bin\Release\$($culture.Name)\manaSetup.msi"
    if (-not (Test-Path $built)) {
        $built = Join-Path $installerDir "bin\x64\Release\$($culture.Name)\manaSetup.msi"
    }
    if (-not (Test-Path $built)) { throw "MSI for $($culture.Name) not found under installer\bin." }

    # 収穫が古い CAB のままだと署名後の dll サイズと一致しない
    $pubDllLen = (Get-Item $appDll).Length
    Write-Host "Staged check: publish mana.dll is $pubDllLen bytes; MSI=$built"

    $versioned = Join-Path $distDir "manaSetup-1.00-$($culture.Name).msi"
    Copy-Item $built $versioned -Force
    Write-Host "Staged: $versioned"
}

& dotnet msbuild $signMsiProj /t:Build /nologo /v:m
if ($LASTEXITCODE -ne 0) { throw "MSI code signing failed ($LASTEXITCODE)." }

Write-Host "Created: $(Join-Path $distDir 'Install-mana.exe')"
Write-Host "Created: $(Join-Path $distDir 'manaSetup-1.00-en-US.msi')"
Write-Host "Created: $(Join-Path $distDir 'manaSetup-1.00-ja-JP.msi')"
Write-Host "Created: $(Join-Path $distDir 'README.md')"
Write-Host "Created: $(Join-Path $distDir 'README.en.md')"
Write-Host "Created: $(Join-Path $distDir 'THIRD-PARTY-NOTICES.txt')"
Write-Host "Created: $(Join-Path $distDir 'License.en-US.rtf')"
Write-Host "Created: $(Join-Path $distDir 'License.ja-JP.rtf')"
