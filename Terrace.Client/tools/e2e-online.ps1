#Requires -Version 7
# サーバーを立てて、Unity の PlayMode テストを「オンラインあり」で走らせる。
#   tools/e2e-online.ps1            サーバーを起動 → TERRACE_SERVER を渡して PlayMode テスト → サーバーを止める
#   tools/e2e-online.ps1 -Mirror    エディタでこのプロジェクトを開いているとき
# サーバーのログは Logs/e2e-server.log に残る。
param(
    [switch]$Mirror,
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

"build: $serverProject"
& dotnet build $serverProject -c Debug -v q --nologo
if ($LASTEXITCODE -ne 0) { throw "サーバーのビルドに失敗しました (exit $LASTEXITCODE)" }

$dll = Join-Path $serverProject 'bin\Debug\net10.0\Terrace.Server.dll'
$serverArgs = @($dll, "--Terrace:GrpcPort=$GrpcPort", "--Terrace:HttpPort=$HttpPort")
$server = Start-Process -FilePath 'dotnet' -ArgumentList $serverArgs -WorkingDirectory (Split-Path $dll) -PassThru -NoNewWindow `
    -RedirectStandardOutput $serverLog -RedirectStandardError "$serverLog.err"

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
        Get-Content $serverLog, "$serverLog.err" -ErrorAction SilentlyContinue | Select-Object -Last 20
        throw "サーバーが起動しませんでした(ログ: $serverLog)"
    }
    "server: ready ($status)"

    $env:TERRACE_SERVER = "http://localhost:$GrpcPort"
    $unityArgs = @{ PlayMode = $true }
    if ($Mirror) { $unityArgs.Mirror = $true }
    & (Join-Path $PSScriptRoot 'unity.ps1') @unityArgs
}
finally {
    Remove-Item Env:TERRACE_SERVER -ErrorAction SilentlyContinue
    if (-not $server.HasExited) { Stop-Process -Id $server.Id -Force -Confirm:$false }
    "server: stopped (log: $serverLog)"
}
