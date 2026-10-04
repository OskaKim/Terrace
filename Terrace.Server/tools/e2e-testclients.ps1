#Requires -Version 7
# サーバーを立て、テストクライアントを 2 つ繋いで、通信の経路を確かめる(Unity は使わない。Windows でも Linux の CI でも動く)。
#   pwsh tools/e2e-testclients.ps1
#   pwsh tools/e2e-testclients.ps1 -GrpcPort 5102 -HttpPort 5103 -NoBuild
#
# 確かめること(alice が先に入り、bob が後から入って先に抜ける):
#   - bob の参加時のスナップショットに alice がいる
#   - alice に bob の参加・移動・退出が届く
#   - alice の攻撃が敵に届く(OnEnemyDamaged)
# 終了コード: 0 = 合格、1 = 不合格、2 = サーバーが起動できなかった(Windows の Smart App Control が DLL を止めたなど)
# ログは Terrace.Server/Logs/e2e-*.log
param(
    [int]$GrpcPort = 5000,
    [int]$HttpPort = 5001,
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$serverDir = Join-Path $project 'src/Terrace.Server/bin/Debug/net10.0'
$clientDll = Join-Path $project 'src/Terrace.TestClient/bin/Debug/net10.0/Terrace.TestClient.dll'
$serverLog = Join-Path $logs 'e2e-server.log'
$aliceLog = Join-Path $logs 'e2e-alice.log'
$bobLog = Join-Path $logs 'e2e-bob.log'

if (-not $NoBuild) {
    & dotnet build (Join-Path $project 'Terrace.Server.sln') -c Debug -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "ビルドに失敗しました (exit $LASTEXITCODE)" }
}

$server = Start-Process -FilePath 'dotnet' -ArgumentList @((Join-Path $serverDir 'Terrace.Server.dll'), "--Terrace:GrpcPort=$GrpcPort", "--Terrace:HttpPort=$HttpPort") `
    -WorkingDirectory $serverDir -PassThru -NoNewWindow -RedirectStandardOutput $serverLog -RedirectStandardError "$serverLog.err"

$exitCode = 0
try {
    $ready = $false
    for ($i = 0; $i -lt 60 -and -not $server.HasExited; $i++) {
        try { Invoke-RestMethod -Uri "http://localhost:$HttpPort/" -TimeoutSec 1 | Out-Null; $ready = $true; break }
        catch { Start-Sleep -Milliseconds 500 }
    }
    if (-not $ready) {
        $text = (Get-Content $serverLog, "$serverLog.err" -Raw -ErrorAction SilentlyContinue) -join "`n"
        $text.Split("`n") | Select-Object -Last 10
        if ($text -match '0x800711C7') {
            'blocked: Windows の Smart App Control がサーバーの DLL を止めました。この PC ではサーバー側の確かめができません(CI の server-e2e が確かめる)'
            $exitCode = 2
        }
        else {
            throw "サーバーが起動しませんでした(ログ: $serverLog)"
        }
    }
}
finally {
    if ($exitCode -ne 0 -and -not $server.HasExited) { Stop-Process -Id $server.Id -Force -Confirm:$false }
}
if ($exitCode -ne 0) { exit $exitCode }

try {
    "server: ready (gRPC $GrpcPort / HTTP $HttpPort)"

    $url = "http://localhost:$GrpcPort"
    $alice = Start-Process -FilePath 'dotnet' -ArgumentList @($clientDll, '--name', 'alice', '--map', '1', '--server', $url, '--x', '10', '--interval', '200', '--duration', '12', '--attack') `
        -PassThru -NoNewWindow -RedirectStandardOutput $aliceLog -RedirectStandardError "$aliceLog.err"
    Start-Sleep -Seconds 3
    $bob = Start-Process -FilePath 'dotnet' -ArgumentList @($clientDll, '--name', 'bob', '--map', '1', '--server', $url, '--x', '12', '--interval', '300', '--duration', '5') `
        -PassThru -NoNewWindow -RedirectStandardOutput $bobLog -RedirectStandardError "$bobLog.err"
    $bob.WaitForExit(30000) | Out-Null
    $alice.WaitForExit(30000) | Out-Null

    $aliceText = Get-Content $aliceLog -Raw -ErrorAction SilentlyContinue
    $bobText = Get-Content $bobLog -Raw -ErrorAction SilentlyContinue
    $checks = [ordered]@{
        'bob の参加時のスナップショットに alice がいる' = ($bobText -match 'OnSnapshot map=1 players=1') -and ($bobText -match 'player=alice')
        'alice に bob の参加が届く'                     = $aliceText -match 'OnJoin player=bob'
        'alice に bob の移動が届く'                     = $aliceText -match 'OnMove player=bob'
        'alice に bob の退出が届く'                     = $aliceText -match 'OnLeave player=bob'
        'alice の攻撃が敵に届く'                         = $aliceText -match 'OnEnemyDamaged .* by=alice'
    }
    foreach ($check in $checks.GetEnumerator()) {
        "$(if ($check.Value) { 'ok    ' } else { 'FAILED' }) $($check.Key)"
        if (-not $check.Value) { $exitCode = 1 }
    }
    if ($exitCode -ne 0) {
        '--- alice'; ($aliceText -split "`n") | Select-Object -Last 15
        '--- bob'; ($bobText -split "`n") | Select-Object -Last 15
    }
}
finally {
    if (-not $server.HasExited) { Stop-Process -Id $server.Id -Force -Confirm:$false }
}
exit $exitCode
