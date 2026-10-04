#Requires -Version 7
# サーバーを立てて、Unity の PlayMode テストを「オンラインあり」で走らせる。
#   tools/e2e-online.ps1            サーバーを起動 → TERRACE_SERVER を渡して PlayMode テスト → サーバーを止める
#   tools/e2e-online.ps1 -Mirror    エディタでこのプロジェクトを開いているとき
#   tools/e2e-online.ps1 -NoBuild -GrpcPort 5100 -HttpPort 5101
#                                   別のサーバーを動かしたまま試すとき(動いているサーバーがビルド出力を掴んでいて再ビルドできないため)
# サーバーのログは Logs/e2e-server.log に残る。
# 終了コード 2: サーバーが起動できなかった(Windows の Smart App Control が DLL を止めたなど)。
#   そのときもサーバーの要らない PlayMode テストは走らせ、それが落ちたら終了コード 1
param(
    [switch]$Mirror,
    [switch]$NoBuild,
    [int]$GrpcPort = 5000,
    [int]$HttpPort = 5001
)
$ErrorActionPreference = 'Stop'
$client = Split-Path $PSScriptRoot -Parent
$terrace = Split-Path $client -Parent
$serverProject = Join-Path $terrace 'Terrace.Server\src\Terrace.Server'
$logs = Join-Path $client 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$serverLog = Join-Path $logs 'e2e-server.log'

if (-not $NoBuild) {
    "build: $serverProject"
    & dotnet build $serverProject -c Debug -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "サーバーのビルドに失敗しました (exit $LASTEXITCODE)。別のサーバーが動いているなら -NoBuild とほかのポートで" }
}

$dll = Join-Path $serverProject 'bin\Debug\net10.0\Terrace.Server.dll'
$serverArgs = @($dll, "--Terrace:GrpcPort=$GrpcPort", "--Terrace:HttpPort=$HttpPort")
$server = Start-Process -FilePath 'dotnet' -ArgumentList $serverArgs -WorkingDirectory (Split-Path $dll) -PassThru -NoNewWindow `
    -RedirectStandardOutput $serverLog -RedirectStandardError "$serverLog.err"
$blocked = $false
$blockedButFailed = $false

try {
    $status = "http://localhost:$HttpPort/"
    $ready = $false
    for ($i = 0; $i -lt 60; $i++) {
        if ($server.HasExited) { break }
        try {
            Invoke-RestMethod -Uri $status -TimeoutSec 1 | Out-Null
            $ready = $true
            break
        }
        catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) {
        $text = (Get-Content $serverLog, "$serverLog.err" -Raw -ErrorAction SilentlyContinue) -join "`n"
        $text.Split("`n") | Select-Object -Last 20
        if ($text -notmatch '0x800711C7') { throw "サーバーが起動しませんでした(ログ: $serverLog)" }
        'blocked: Windows の Smart App Control がサーバーの DLL を止めました。2 人で繋ぐ試験は省き、ほかの PlayMode テストだけ走らせます'
        $blocked = $true
        $unityArgs = @{ PlayMode = $true }
        if ($Mirror) { $unityArgs.Mirror = $true }
        $out = & (Join-Path $PSScriptRoot 'unity.ps1') @unityArgs
        $out
        if (-not ($out -match 'failed=0')) { $blockedButFailed = $true }
    }
    else {
        "server: ready ($status)"
        $env:TERRACE_SERVER = "http://localhost:$GrpcPort"
        $unityArgs = @{ PlayMode = $true }
        if ($Mirror) { $unityArgs.Mirror = $true }
        & (Join-Path $PSScriptRoot 'unity.ps1') @unityArgs
    }
}
finally {
    Remove-Item Env:TERRACE_SERVER -ErrorAction SilentlyContinue
    if (-not $server.HasExited) { Stop-Process -Id $server.Id -Force -Confirm:$false }
    "server: stopped (log: $serverLog)"
}
if ($blockedButFailed) { exit 1 }
if ($blocked) { exit 2 }
