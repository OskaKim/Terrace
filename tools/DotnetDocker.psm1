# dotnet の実行(dotnet test、masterdata-build)を手元で回し、Windows の Smart App Control に DLL を止められたら
# 同じことを Docker の .NET SDK の像(Linux)で回し直す共通の処理。task.ps1 verify、check-sync.ps1、
# Terrace.Client/tools/sync-shared.ps1 が使う。サーバーを立てる方は Terrace.Server/tools/TerraceServer.psm1。
# 理由は docs/decisions/0011-server-in-docker.md。

$script:Repo = Split-Path $PSScriptRoot -Parent   # Terrace/(作業場ならその作業場)
$script:SdkImage = 'mcr.microsoft.com/dotnet/sdk:10.0'
$script:NuGetVolume = 'terrace-dotnet-nuget'       # NuGet の取り置き。回すたびに取り直さない
# コンテナへ写さないもの(git、ビルドの出力、Unity の作業用フォルダ)
$script:Excludes = @('.git', '*/bin', '*/obj', 'Terrace.Client/Library', 'Terrace.Client/Temp', 'Terrace.Client/Logs', 'Terrace.Client/UserSettings', 'Terrace.Client/Build')

Import-Module (Join-Path $script:Repo 'Terrace.Server/tools/TerraceServer.psm1') -Function Test-DockerAvailable

function Test-SmartAppControlBlocked([string]$Text) {
    return $Text -match '0x800711C7'
}

# dotnet を手元で回し、Smart App Control に止められたら同じことをコンテナの中で回し直す。
#   -Arguments: 手元で dotnet に渡す引数
#   -DockerCommand: コンテナの中で回す bash のコマンド。/work がリポジトリ(作業場)の写しで、そこから回す
#   -OutputDirectory: コンテナの /out に差し込む書き込み先(masterdata-build の出力など)
# 戻り値: Result = 'ok' / 'failed' / 'blocked'(止められ、Docker も使えない)、Where = 'local' / 'docker'、Output = 出力の行
function Invoke-DotnetWithDockerFallback {
    param(
        [Parameter(Mandatory)][string[]]$Arguments,
        [Parameter(Mandatory)][string]$DockerCommand,
        [string]$OutputDirectory
    )
    $output = [System.Collections.Generic.List[string]]::new()
    & dotnet @Arguments 2>&1 | ForEach-Object { $output.Add("$_") }
    $code = $LASTEXITCODE
    $result = { param($r, $w) [pscustomobject]@{ Result = $r; Where = $w; Output = $output.ToArray() } }
    if ($code -eq 0) { return & $result 'ok' 'local' }
    if (-not (Test-SmartAppControlBlocked ($output -join "`n"))) { return & $result 'failed' 'local' }

    $output.Add('blocked: Windows の Smart App Control が DLL を止めました')
    if (-not (Test-DockerAvailable)) {
        $output.Add('Docker も使えないので、ここは確かめられません(Docker Desktop を起動すれば、次からはコンテナで回します)')
        return & $result 'blocked' 'local'
    }
    $output.Add("→ Docker のコンテナ($($script:SdkImage))で回し直します(初回は像を取ってくるので数分かかる)")

    # 作業場は読み取り専用で差し込み、コンテナの中へ写してから回す(手元の bin/obj と混ざらないように)
    $excludes = ($script:Excludes | ForEach-Object { "--exclude='$_'" }) -join ' '
    $bash = "mkdir -p /work && cd /src && tar $excludes -cf - . | (cd /work && tar -xf -) && cd /work && $DockerCommand"
    $mounts = @('-v', "$($script:Repo):/src:ro", '-v', "$($script:NuGetVolume):/root/.nuget/packages")
    if ($OutputDirectory) {
        New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
        $mounts += @('-v', "$((Resolve-Path -LiteralPath $OutputDirectory).Path):/out")
    }
    & docker run --rm @mounts $script:SdkImage bash -c $bash 2>&1 | ForEach-Object { $output.Add("$_") }
    if ($LASTEXITCODE -eq 0) { return & $result 'ok' 'docker' }
    return & $result 'failed' 'docker'
}

Export-ModuleMember -Function Test-SmartAppControlBlocked, Invoke-DotnetWithDockerFallback
