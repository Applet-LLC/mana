# EV code signing for mana (USB token + subject name).
#
# Signs an explicit file list (e.g. mana.exe, mana.dll, Install-mana.exe, MSI).
# Publish output DLLs other than those listed are not included.
# Multiple paths travel as one semicolon-delimited argument so powershell -File
# does not bind extra tokens to the next parameter.
#
# signtool は呼び出し演算子 & で同期実行し、渡されたファイルを1回の起動でまとめて署名する。
# 完了後、各ファイルの Authenticode が Valid になるまで確認してから戻る。
# （Start-Process -ArgumentList は /fd などが欠落することがあるため使わない）
#
# Usage:
#   .\EvCodeSign.ps1 -Files "C:\path\mana.exe;C:\path\Install-mana.exe"
param(
    [string] $Files = ""
)

$ErrorActionPreference = "Stop"

$CertSubject = "Applet LLC"
$TimestampUrl = "http://timestamp.globalsign.com/tsa/r45standard"
$VerifyTimeout = [TimeSpan]::FromMinutes(10)

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

function Wait-AuthenticodeValid {
    param(
        [Parameter(Mandatory = $true)][string] $Path,
        [Parameter(Mandatory = $true)][TimeSpan] $Timeout
    )
    $deadline = (Get-Date) + $Timeout
    $lastStatus = $null
    do {
        $sig = Get-AuthenticodeSignature -LiteralPath $Path
        $lastStatus = $sig.Status
        if ($lastStatus -eq "Valid") {
            Write-Host "  verified: $(Split-Path $Path -Leaf) (Valid)"
            return
        }
        Start-Sleep -Milliseconds 500
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for Authenticode Valid on '$Path' (last status: $lastStatus)."
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

    $names = @($Paths | ForEach-Object { Split-Path $_ -Leaf })
    Write-Host "Signing $($Paths.Count) file(s) in one signtool invocation: $($names -join ', ')"
    # 複数ファイルを一度に渡す（トークン PIN も1回）。& で同期実行し、/fd 欠落を避ける。
    $toolArgs = $baseArgs + $Paths
    & $signtool @toolArgs
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "signtool failed with exit code $exitCode for: $($names -join ', ')."
    }

    Write-Host "Verifying Authenticode on $($Paths.Count) file(s)..."
    foreach ($path in $Paths) {
        Wait-AuthenticodeValid -Path $path -Timeout $VerifyTimeout
    }
    Write-Host "All $($Paths.Count) file(s) signed and verified."
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
