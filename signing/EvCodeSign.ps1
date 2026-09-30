# EV code signing for mana (USB token + subject name).
#
# Signs an explicit file list. Publish output DLLs are not included.
# Multiple paths travel as one semicolon-delimited argument so powershell -File
# does not bind extra tokens to the next parameter.
#
# Usage:
#   .\EvCodeSign.ps1 -Files "C:\path\mana.exe;C:\path\Install-mana.exe"
param(
    [string] $Files = ""
)

$ErrorActionPreference = "Stop"

$CertSubject = "Applet LLC"
$TimestampUrl = "http://timestamp.globalsign.com/tsa/r45standard"

function Get-SigntoolExe {
    $binRoot = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\bin"
    if (Test-Path $binRoot) {
        $verDirs = Get-ChildItem -LiteralPath $binRoot -Directory -ErrorAction SilentlyContinue |
            Sort-Object { $_.Name } -Descending
        foreach ($d in $verDirs) {
            $candidate = Join-Path $d.FullName "x64\signtool.exe"
            if (Test-Path -LiteralPath $candidate) {
                return (Resolve-Path -LiteralPath $candidate).Path
            }
        }
    }
    $ack = Join-Path ${env:ProgramFiles(x86)} "Windows Kits\10\App Certification Kit\signtool.exe"
    if (Test-Path -LiteralPath $ack) {
        return (Resolve-Path -LiteralPath $ack).Path
    }
    throw "signtool.exe not found under Windows Kits 10. Install the Windows SDK."
}

function Invoke-EvSign {
    param([string[]] $Paths)
    if ($Paths.Count -eq 0) {
        return
    }

    # USB トークン未接続など証明書が見つからない場合はスキップ（署名なし成果物を生成して続行）
    $cert = Get-ChildItem -Path @("Cert:\CurrentUser\My", "Cert:\LocalMachine\My") `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.Subject -like "*$CertSubject*" -and $_.NotAfter -gt (Get-Date) } |
        Select-Object -First 1
    if ($null -eq $cert) {
        Write-Warning "EV certificate '$CertSubject' not found (USB token disconnected?). Skipping code signing — artifacts will be UNSIGNED."
        return
    }

    $signtool = Get-SigntoolExe
    $baseArgs = @(
        "sign", "/v", "/a",
        "/n", $CertSubject,
        "/tr", $TimestampUrl,
        "/td", "sha256",
        "/fd", "sha256"
    )
    $chunkSize = 40
    Write-Host "Signing $($Paths.Count) file(s) with signtool..."
    for ($i = 0; $i -lt $Paths.Count; $i += $chunkSize) {
        $take = [Math]::Min($chunkSize, $Paths.Count - $i)
        $chunk = $Paths[$i..($i + $take - 1)]
        $batchNo = [int]($i / $chunkSize) + 1
        $batches = [int][Math]::Ceiling($Paths.Count / $chunkSize)
        Write-Host "  batch $batchNo / $batches ($($chunk.Count) file(s))"
        $toolArgs = $baseArgs + $chunk
        & $signtool @toolArgs
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed with exit code $LASTEXITCODE"
        }
    }
}

if ($Files -eq "") {
    throw "Specify -Files as a semicolon-delimited list of paths."
}

$requestedPaths = @($Files -split ';' | Where-Object { $_ -ne "" })
$fullPaths = @()
foreach ($path in $requestedPaths) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "File to sign not found: $path"
    }
    $fullPaths += (Resolve-Path -LiteralPath $path).Path
}

Invoke-EvSign -Paths $fullPaths
exit 0
