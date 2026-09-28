#Requires -Version 7
# 5 つのリポジトリ(文書のルート Terrace と、4 つの Terrace.*)を GitHub に作り、main を push する。
#
#   pwsh tools/github-publish.ps1 -DryRun                      何をするかだけ表示する(最初にこれで確かめる)
#   pwsh tools/github-publish.ps1                              gh でログイン中のアカウントに private で作って push
#   pwsh tools/github-publish.ps1 -Owner my-org -Visibility public
#
# - GitHub CLI(gh)でログインしておくこと(gh auth login)
# - すでに GitHub にある同名のリポジトリは作らずに使う。origin が既にあればその URL へ push する
# - 未コミットの変更があるリポジトリがあれば、何もせずに止まる
param(
    [string]$Owner,
    [ValidateSet('private', 'public')]
    [string]$Visibility = 'private',
    [ValidateSet('ssh', 'https')]
    [string]$Protocol,
    [switch]$DryRun
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$repos = @(
    @{ Name = 'Terrace'; Path = $root; Description = 'Terrace: MapleStory-like 2D side-scrolling MORPG - docs and repository map' },
    @{ Name = 'Terrace.MasterData'; Path = (Join-Path $root 'Terrace.MasterData'); Description = 'Terrace: CSV to MasterMemory master data builder with row tracing' },
    @{ Name = 'Terrace.Map'; Path = (Join-Path $root 'Terrace.Map'); Description = 'Terrace: foothold (line segment) based 2D map library' },
    @{ Name = 'Terrace.Server'; Path = (Join-Path $root 'Terrace.Server'); Description = 'Terrace: MagicOnion game server and test client' },
    @{ Name = 'Terrace.Client'; Path = (Join-Path $root 'Terrace.Client'); Description = 'Terrace: Unity 6 client (offline and online)' }
)

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) が見つかりません。https://cli.github.com/ から入れてください' }
& gh auth status *> $null
if ($LASTEXITCODE -ne 0) { throw 'gh にログインしていません。先に gh auth login を実行してください' }
if (-not $Owner) { $Owner = (& gh api user --jq .login).Trim() }
if (-not $Protocol) {
    # gh auth login で選んだ方式は github.com ごとの設定に入る。無ければ全体の設定
    $Protocol = (& gh config get git_protocol -h github.com 2>$null)
    if ([string]::IsNullOrWhiteSpace($Protocol)) { $Protocol = (& gh config get git_protocol 2>$null) }
    if ([string]::IsNullOrWhiteSpace($Protocol)) { $Protocol = 'https' }
    $Protocol = $Protocol.Trim()
}
"owner=$Owner visibility=$Visibility protocol=$Protocol$(if ($DryRun) { ' (dry run)' })"

# 先に全部を検査して、途中で止まって半端な状態にならないようにする
foreach ($repo in $repos) {
    if (-not (Test-Path (Join-Path $repo.Path '.git'))) { throw "$($repo.Name): git リポジトリではありません ($($repo.Path))" }
    $branch = (& git -C $repo.Path branch --show-current).Trim()
    if ($branch -ne 'main') { throw "$($repo.Name): 今のブランチが main ではありません ($branch)" }
    $dirty = & git -C $repo.Path status --porcelain
    if ($dirty) { throw "$($repo.Name): 未コミットの変更があります。コミットしてからやり直してください" }
}

foreach ($repo in $repos) {
    $full = "$Owner/$($repo.Name)"
    $url = if ($Protocol -eq 'ssh') { "git@github.com:$full.git" } else { "https://github.com/$full.git" }
    "--- $($repo.Name)"

    & gh repo view $full *> $null
    if ($LASTEXITCODE -eq 0) {
        "  GitHub: $full はもうあるので作らない"
    }
    elseif ($DryRun) {
        "  GitHub: $full を $Visibility で作る"
    }
    else {
        & gh repo create $full "--$Visibility" --description $repo.Description | Out-Null
        if ($LASTEXITCODE -ne 0) { throw "$full を作れませんでした" }
        "  GitHub: $full を作った ($Visibility)"
    }

    $origin = & git -C $repo.Path remote get-url origin 2>$null
    if ($LASTEXITCODE -eq 0 -and $origin) {
        "  origin: $origin(そのまま使う)"
    }
    elseif ($DryRun) {
        "  origin: $url を足す"
    }
    else {
        & git -C $repo.Path remote add origin $url
        "  origin: $url を足した"
    }

    if ($DryRun) {
        "  push: main"
    }
    else {
        & git -C $repo.Path push -u origin main
        if ($LASTEXITCODE -ne 0) { throw "$($repo.Name) を push できませんでした" }
    }
}

if (-not $DryRun) {
    ""
    "完了: https://github.com/$Owner?tab=repositories"
    "別の PC では、Terrace を clone してから pwsh tools/clone-all.ps1 で残りの 4 つを並べる"
}
