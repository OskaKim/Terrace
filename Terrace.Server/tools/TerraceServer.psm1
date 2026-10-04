# サーバーを立てる・止める共通の処理。e2e-testclients.ps1、Terrace.Client/tools/e2e-online.ps1、server-docker.ps1 が使う。
# 手元の dotnet で立てるか、Docker の Linux コンテナで立てる(Windows の Smart App Control がサーバーの DLL を止める PC 向け。
# 理由は docs/decisions/0011-server-in-docker.md)。

$script:Repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent   # Terrace/(作業場ならその作業場)
$script:Image = 'terrace-server'
$script:ImageLabel = 'org.opencontainers.image.title=terrace-server'

function Test-DockerAvailable {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { return $false }
    & docker info --format '{{.ServerVersion}}' *> $null
    return $LASTEXITCODE -eq 0
}

# 状態確認の HTTP が応答するまで待つ。$Alive が false を返したら(プロセスやコンテナが落ちたら)待たずに諦める
function Wait-ServerReady([int]$HttpPort, [scriptblock]$Alive, [int]$TimeoutSeconds) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (-not (& $Alive)) { return $false }
        try { Invoke-RestMethod -Uri "http://localhost:$HttpPort/" -TimeoutSec 1 | Out-Null; return $true }
        catch { Start-Sleep -Milliseconds 500 }
    }
    return $false
}

# 像を作ってコンテナを立て、応答するまで待つ。同じ名前のコンテナがあれば作り直す。
#   -Lan: 別の PC からも繋げるように全インターフェースで開ける(既定はこの PC の 127.0.0.1 だけ)
#   -NoBuild: 前に作った terrace-server:latest をそのまま使う
function Start-DockerServer {
    param(
        [int]$GrpcPort = 5000,
        [int]$HttpPort = 5001,
        [string]$Name = "terrace-server-$GrpcPort",
        [switch]$Lan,
        [switch]$NoBuild
    )
    $image = "$($script:Image):latest"
    if (-not $NoBuild) {
        Write-Host "docker build: $image(初回は .NET の像を取ってくるので数分かかる)"
        # 並列の作業場が同じ名札を付け直しても混ざらないよう、作った像は ID で動かす
        $idFile = Join-Path ([IO.Path]::GetTempPath()) "terrace-server-$GrpcPort.iid"
        $buildArgs = @('build', '-f', (Join-Path $script:Repo 'Terrace.Server/Dockerfile'), '--iidfile', $idFile, '-t', $image, $script:Repo)
        & docker @buildArgs -q 2>$null | Out-Null
        if ($LASTEXITCODE -ne 0) {
            # 静かに作ると失敗の中身が見えないので、経過を出してもう一度作る(済んだ段はキャッシュから)
            & docker @buildArgs --progress=plain 2>&1 | Select-Object -Last 40 | Out-Host
            throw 'サーバーの像を作れませんでした(上の docker build の出力を見てください)'
        }
        $image = (Get-Content -LiteralPath $idFile -Raw).Trim()
        Remove-Item -LiteralPath $idFile -ErrorAction SilentlyContinue
        # 作り直して名札の外れた古い像を消す(動いているコンテナが使っている像は消えない)
        & docker image prune -f --filter "label=$($script:ImageLabel)" *> $null
    }

    & docker rm -f $Name *> $null
    # 既定はこの PC の中からだけ繋げる。Windows は閉じたポートへの接続を 2 秒ほど粘るので、localhost(::1 が先)で
    # 待たされないよう ::1 にも開ける
    $hosts = if ($Lan) { @('') } elseif ($IsWindows) { @('127.0.0.1:', '[::1]:') } else { @('127.0.0.1:') }
    $publish = foreach ($h in $hosts) { '-p', "$h${GrpcPort}:5000", '-p', "$h${HttpPort}:5001" }
    & docker run -d --name $Name @publish $image | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "コンテナ $Name を起動できませんでした。ポート $GrpcPort / $HttpPort をほかで使っていないか確かめてください" }

    $alive = { (& docker inspect -f '{{.State.Running}}' $Name 2>$null) -eq 'true' }.GetNewClosure()
    if (-not (Wait-ServerReady $HttpPort $alive 60)) {
        & docker logs --tail 20 $Name 2>&1 | Out-Host
        & docker rm -f $Name *> $null
        throw "コンテナ $Name のサーバーが応答しませんでした"
    }
}

function Stop-DockerServer([string]$Name, [string]$LogPath) {
    if ($LogPath) { & docker logs $Name > $LogPath 2> "$LogPath.err" }
    & docker rm -f $Name *> $null
}

# 試験のためにサーバーを立てる。戻り値を Stop-TerraceServer に渡す。
#   -Mode Auto: 手元の dotnet で立て、Smart App Control に止められたら Docker で立て直す / Local / Docker
#   戻り値の Kind: 'local' / 'docker' / 'blocked'(どちらでも立てられなかった。呼び出し側は終了コード 2 にする)
#   -NoBuild: 手元のサーバーを作り直さない(動いている別のサーバーがビルド出力を掴んでいるとき)。Docker の像は常に作る
function Start-TerraceServer {
    param(
        [int]$GrpcPort = 5000,
        [int]$HttpPort = 5001,
        [Parameter(Mandatory)][string]$LogPath,
        [ValidateSet('Auto', 'Local', 'Docker')][string]$Mode = 'Auto',
        [switch]$NoBuild
    )
    $container = "terrace-e2e-$GrpcPort"
    $url = "http://localhost:$GrpcPort"
    if ($Mode -ne 'Docker') {
        $project = Join-Path $script:Repo 'Terrace.Server/src/Terrace.Server'
        if (-not $NoBuild) {
            & dotnet build $project -c Debug -v q --nologo | Out-Host
            if ($LASTEXITCODE -ne 0) { throw "サーバーのビルドに失敗しました (exit $LASTEXITCODE)。別のサーバーが動いているなら -NoBuild とほかのポートで" }
        }
        $dir = Join-Path $project 'bin/Debug/net10.0'
        $process = Start-Process -FilePath 'dotnet' -ArgumentList @((Join-Path $dir 'Terrace.Server.dll'), "--Terrace:GrpcPort=$GrpcPort", "--Terrace:HttpPort=$HttpPort") `
            -WorkingDirectory $dir -PassThru -NoNewWindow -RedirectStandardOutput $LogPath -RedirectStandardError "$LogPath.err"
        if (Wait-ServerReady $HttpPort { -not $process.HasExited }.GetNewClosure() 30) {
            return [pscustomobject]@{ Kind = 'local'; Url = $url; Process = $process; LogPath = $LogPath }
        }
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -Force -Confirm:$false }
        $text = (Get-Content $LogPath, "$LogPath.err" -Raw -ErrorAction SilentlyContinue) -join "`n"
        $text.Split("`n") | Select-Object -Last 10 | Out-Host
        if ($text -notmatch '0x800711C7') { throw "サーバーが起動しませんでした(ログ: $LogPath)" }
        Write-Host 'blocked: Windows の Smart App Control がサーバーの DLL を止めました'
        if ($Mode -eq 'Local') { return [pscustomobject]@{ Kind = 'blocked' } }
        if (-not (Test-DockerAvailable)) {
            Write-Host 'Docker も使えないので、サーバーの要る確かめは省きます(Docker Desktop を起動すれば、次からはコンテナで立てます)'
            return [pscustomobject]@{ Kind = 'blocked' }
        }
        Write-Host '→ Docker のコンテナで立て直します'
    }
    elseif (-not (Test-DockerAvailable)) { throw 'Docker が使えません(Docker Desktop を起動してください)' }

    Start-DockerServer -GrpcPort $GrpcPort -HttpPort $HttpPort -Name $container
    return [pscustomobject]@{ Kind = 'docker'; Url = $url; Container = $container; LogPath = $LogPath }
}

function Stop-TerraceServer($Server) {
    if (-not $Server) { return }
    switch ($Server.Kind) {
        'local' { if (-not $Server.Process.HasExited) { Stop-Process -Id $Server.Process.Id -Force -Confirm:$false } }
        'docker' { Stop-DockerServer -Name $Server.Container -LogPath $Server.LogPath }
    }
}

Export-ModuleMember -Function Test-DockerAvailable, Start-DockerServer, Stop-DockerServer, Start-TerraceServer, Stop-TerraceServer
