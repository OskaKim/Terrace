#Requires -Version 7
# 文書のリポジトリ(Terrace)を clone したあとに、残りの 4 つを同じフォルダの直下へ clone する。
# Server の csproj と Client の同期スクリプトが兄弟の位置を前提にしているので、場所と名前を変えないこと。
#
#   git clone https://github.com/<owner>/Terrace.git
#   cd Terrace
#   pwsh tools/clone-all.ps1               # Terrace の origin と同じ持ち主・同じ方式(https / ssh)で取る
#   pwsh tools/clone-all.ps1 -Owner someone -Protocol ssh
param(
    [string]$Owner,
    [ValidateSet('ssh', 'https')]
    [string]$Protocol
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

# 既定は、このリポジトリの origin から持ち主と方式を読む
$origin = & git -C $root remote get-url origin 2>$null
if (-not $Owner -and $origin -match 'github\.com[:/]([^/]+)/') { $Owner = $Matches[1] }
if (-not $Protocol) { $Protocol = if ($origin -like 'git@*') { 'ssh' } else { 'https' } }
if (-not $Owner) { throw '-Owner を指定してください(origin から読めませんでした)' }

foreach ($name in 'Terrace.MasterData', 'Terrace.Map', 'Terrace.Server', 'Terrace.Client') {
    $path = Join-Path $root $name
    if (Test-Path (Join-Path $path '.git')) {
        "${name}: もうある(git -C $name pull で更新できる)"
        continue
    }
    $url = if ($Protocol -eq 'ssh') { "git@github.com:$Owner/$name.git" } else { "https://github.com/$Owner/$name.git" }
    "${name}: $url"
    & git clone $url $path
    if ($LASTEXITCODE -ne 0) { throw "$name を clone できませんでした" }
}

""
"次は docs/guides/setup.md の「初回の手順」"
