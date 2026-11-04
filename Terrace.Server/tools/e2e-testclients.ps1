#Requires -Version 7
# サーバーを立て、テストクライアントを 2 つ繋いで、通信の経路を確かめる(Unity は使わない。Windows でも Linux の CI でも動く)。
#   pwsh tools/e2e-testclients.ps1
#   pwsh tools/e2e-testclients.ps1 -GrpcPort 5102 -HttpPort 5103 -NoBuild
#   pwsh tools/e2e-testclients.ps1 -Server Docker     サーバーを Docker のコンテナで立てる(既定 Auto は手元で立て、止められたら Docker)
#
# 確かめること(alice と carol が先に入り、bob が後から alice と同じマップに入って先に抜ける。carol は別のマップ):
#   - bob の参加時のスナップショットに alice がいる
#   - alice に bob の参加・移動・退出が届く
#   - alice の攻撃が敵に届く(EnemyDamaged)
#   - bob のチャットが、bob 自身・alice・別のマップの carol に届く(ワールド全体のチャット)
# 終了コード: 0 = 合格、1 = 不合格、2 = この PC では確かめられなかった(Smart App Control が DLL を止め、Docker も使えないなど)
# ログは Terrace.Server/Logs/e2e-*.log
param(
    [int]$GrpcPort = 5000,
    [int]$HttpPort = 5001,
    [switch]$NoBuild,
    [ValidateSet('Auto', 'Local', 'Docker')]
    [string]$Server = 'Auto'
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TerraceServer.psm1') -Force
$project = Split-Path $PSScriptRoot -Parent
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null
$clientDll = Join-Path $project 'src/Terrace.TestClient/bin/Debug/net10.0/Terrace.TestClient.dll'
$serverLog = Join-Path $logs 'e2e-server.log'
$aliceLog = Join-Path $logs 'e2e-alice.log'
$bobLog = Join-Path $logs 'e2e-bob.log'
$carolLog = Join-Path $logs 'e2e-carol.log'

# テストクライアントは手元で動かすので、サーバーを Docker で立てるときもソリューションごと作る
if (-not $NoBuild) {
    & dotnet build (Join-Path $project 'Terrace.Server.sln') -c Debug -v q --nologo
    if ($LASTEXITCODE -ne 0) { throw "ビルドに失敗しました (exit $LASTEXITCODE)" }
}

$handle = Start-TerraceServer -GrpcPort $GrpcPort -HttpPort $HttpPort -LogPath $serverLog -Mode $Server -NoBuild
if ($handle.Kind -eq 'blocked') { 'この PC ではサーバー側の確かめができません(CI の server-e2e が確かめる)'; exit 2 }

$exitCode = 0
try {
    "server: ready ($(if ($handle.Kind -eq 'docker') { 'Docker のコンテナ' } else { '手元の dotnet' }) / gRPC $GrpcPort / HTTP $HttpPort)"

    $url = $handle.Url
    $alice = Start-Process -FilePath 'dotnet' -ArgumentList @($clientDll, '--name', 'alice', '--map', '1', '--server', $url, '--x', '10', '--interval', '200', '--duration', '12', '--attack') `
        -PassThru -NoNewWindow -RedirectStandardOutput $aliceLog -RedirectStandardError "$aliceLog.err"
    $carol = Start-Process -FilePath 'dotnet' -ArgumentList @($clientDll, '--name', 'carol', '--map', '2', '--server', $url, '--interval', '500', '--duration', '12') `
        -PassThru -NoNewWindow -RedirectStandardOutput $carolLog -RedirectStandardError "$carolLog.err"
    Start-Sleep -Seconds 3
    $bob = Start-Process -FilePath 'dotnet' -ArgumentList @($clientDll, '--name', 'bob', '--map', '1', '--server', $url, '--x', '12', '--interval', '300', '--duration', '5', '--chat', 'hello-e2e') `
        -PassThru -NoNewWindow -RedirectStandardOutput $bobLog -RedirectStandardError "$bobLog.err"
    $bob.WaitForExit(30000) | Out-Null
    $alice.WaitForExit(30000) | Out-Null
    $carol.WaitForExit(30000) | Out-Null

    $aliceText = Get-Content $aliceLog -Raw -ErrorAction SilentlyContinue
    $bobText = Get-Content $bobLog -Raw -ErrorAction SilentlyContinue
    $carolText = Get-Content $carolLog -Raw -ErrorAction SilentlyContinue
    $checks = [ordered]@{
        'bob の参加時のスナップショットに alice がいる' = ($bobText -match 'RoomSnapshot map=1 players=1') -and ($bobText -match 'player=alice')
        'alice に bob の参加が届く'                     = $aliceText -match 'PlayerJoined player=bob'
        'alice に bob の移動が届く'                     = $aliceText -match 'PlayerMoved player=bob'
        'alice に bob の退出が届く'                     = $aliceText -match 'PlayerLeft player=bob'
        'alice の攻撃が敵に届く'                         = $aliceText -match 'EnemyDamaged .* by=alice'
        'bob のチャットが bob 自身に届く'               = $bobText -match 'Chat from=bob map=1 channel=World: hello-e2e'
        'bob のチャットが alice に届く'                 = $aliceText -match 'Chat from=bob map=1 channel=World: hello-e2e'
        'bob のチャットが別のマップの carol に届く'     = $carolText -match 'Chat from=bob map=1 channel=World: hello-e2e'
    }
    foreach ($check in $checks.GetEnumerator()) {
        "$(if ($check.Value) { 'ok    ' } else { 'FAILED' }) $($check.Key)"
        if (-not $check.Value) { $exitCode = 1 }
    }
    if ($exitCode -ne 0) {
        '--- alice'; ($aliceText -split "`n") | Select-Object -Last 15
        '--- bob'; ($bobText -split "`n") | Select-Object -Last 15
        '--- carol'; ($carolText -split "`n") | Select-Object -Last 15
        $clientErrors = (Get-Content "$aliceLog.err", "$bobLog.err", "$carolLog.err" -Raw -ErrorAction SilentlyContinue) -join "`n"
        if ($clientErrors -match '0x800711C7') {
            'blocked: Windows の Smart App Control がテストクライアントの DLL を止めました'
            $exitCode = 2
        }
    }
}
finally {
    Stop-TerraceServer $handle
}
exit $exitCode
