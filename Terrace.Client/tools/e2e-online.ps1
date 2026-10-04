#Requires -Version 7
# サーバーを立てて、Unity の PlayMode テストを「オンラインあり」で走らせる。
#   tools/e2e-online.ps1            サーバーを起動 → TERRACE_SERVER を渡して PlayMode テスト → サーバーを止める
#   tools/e2e-online.ps1 -Mirror    エディタでこのプロジェクトを開いているとき
#   tools/e2e-online.ps1 -NoBuild -GrpcPort 5100 -HttpPort 5101
#                                   別のサーバーを動かしたまま試すとき(動いているサーバーがビルド出力を掴んでいて再ビルドできないため)
#   tools/e2e-online.ps1 -Server Docker
#                                   サーバーを Docker のコンテナで立てる(既定 Auto は手元で立て、Smart App Control に止められたら Docker)
# サーバーのログは Logs/e2e-server.log に残る。立て方の共通処理は ../Terrace.Server/tools/TerraceServer.psm1。
# 終了コード 2: サーバーを立てられなかった(Smart App Control が DLL を止め、Docker も使えないなど)。
#   そのときもサーバーの要らない PlayMode テストは走らせ、それが落ちたら終了コード 1
param(
    [switch]$Mirror,
    [switch]$NoBuild,
    [int]$GrpcPort = 5000,
    [int]$HttpPort = 5001,
    [ValidateSet('Auto', 'Local', 'Docker')]
    [string]$Server = 'Auto'
)
$ErrorActionPreference = 'Stop'
$client = Split-Path $PSScriptRoot -Parent
$terrace = Split-Path $client -Parent
Import-Module (Join-Path $terrace 'Terrace.Server/tools/TerraceServer.psm1') -Force
$logs = Join-Path $client 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$serverLog = Join-Path $logs 'e2e-server.log'

$handle = Start-TerraceServer -GrpcPort $GrpcPort -HttpPort $HttpPort -LogPath $serverLog -Mode $Server -NoBuild:$NoBuild
$blocked = $handle.Kind -eq 'blocked'
$blockedButFailed = $false
$unityArgs = @{ PlayMode = $true }
if ($Mirror) { $unityArgs.Mirror = $true }

try {
    if ($blocked) {
        '2 人で繋ぐ試験は省き、ほかの PlayMode テストだけ走らせます'
        $out = & (Join-Path $PSScriptRoot 'unity.ps1') @unityArgs
        $out
        if (-not ($out -match 'failed=0')) { $blockedButFailed = $true }
    }
    else {
        "server: ready ($(if ($handle.Kind -eq 'docker') { 'Docker のコンテナ' } else { '手元の dotnet' }) / $($handle.Url))"
        $env:TERRACE_SERVER = $handle.Url
        & (Join-Path $PSScriptRoot 'unity.ps1') @unityArgs
    }
}
finally {
    Remove-Item Env:TERRACE_SERVER -ErrorAction SilentlyContinue
    if (-not $blocked) {
        Stop-TerraceServer $handle
        "server: stopped (log: $serverLog)"
    }
}
if ($blockedButFailed) { exit 1 }
if ($blocked) { exit 2 }
