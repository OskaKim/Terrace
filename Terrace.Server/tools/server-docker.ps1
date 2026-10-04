#Requires -Version 7
# サーバーを Docker の Linux コンテナで動かす。Windows の Smart App Control がサーバーの DLL を止める PC でも遊べる(Docker Desktop が要る)。
#   pwsh tools/server-docker.ps1               像を作ってコンテナを立てる(gRPC 5000 / HTTP 5001)。Unity からは http://localhost:5000
#   pwsh tools/server-docker.ps1 down          止めて消す
#   pwsh tools/server-docker.ps1 logs -Follow  ログを追う(Ctrl+C で抜ける。サーバーは止まらない)
#   pwsh tools/server-docker.ps1 -Lan          別の PC からも繋げるように開ける(既定はこの PC からだけ)
#   pwsh tools/server-docker.ps1 -GrpcPort 6000 -HttpPort 6001
# コンテナ名は terrace-server-<gRPC のポート>。同じ名前のコンテナがあれば、今のソースで作り直す。
param(
    [Parameter(Position = 0)]
    [ValidateSet('up', 'down', 'logs')]
    [string]$Command = 'up',
    [int]$GrpcPort = 5000,
    [int]$HttpPort = 5001,
    [switch]$Lan,
    [switch]$NoBuild,
    [switch]$Follow
)
$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'TerraceServer.psm1') -Force
$name = "terrace-server-$GrpcPort"
if (-not (Test-DockerAvailable)) { throw 'Docker が使えません。Docker Desktop を起動してから、もう一度実行してください' }

switch ($Command) {
    'up' {
        Start-DockerServer -GrpcPort $GrpcPort -HttpPort $HttpPort -Name $name -Lan:$Lan -NoBuild:$NoBuild
        $portArgs = if ($GrpcPort -ne 5000) { " -GrpcPort $GrpcPort" } else { '' }
        "server: ready(コンテナ $name)"
        "  繋ぎ先(Unity・テストクライアント): http://localhost:$GrpcPort"
        "  状態確認: http://localhost:$HttpPort/"
        if ($Lan) { "  別の PC からは http://<この PC の IP アドレス>:$GrpcPort" }
        "ログ: pwsh tools/server-docker.ps1 logs -Follow$portArgs"
        "止める: pwsh tools/server-docker.ps1 down$portArgs"
    }
    'down' {
        Stop-DockerServer -Name $name
        "server: stopped(コンテナ $name)"
    }
    'logs' {
        if ($Follow) { & docker logs -f $name } else { & docker logs $name }
    }
}
