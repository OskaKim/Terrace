# Terrace.Server を自己完結の版(.NET の実行環境ごと)で書き出す。ゲームに同梱する(ADR 0015 のゲームから開くワールド)。
#
#   pwsh tools/publish-server.ps1 -Output <フォルダ> [-Runtime win-x64] [-MasterBytes <master.bytes>]
#
# マップと CSV は content/ に入る(Terrace.Server.csproj)。-MasterBytes を渡すと content/master.bytes に置き、
# 起動のときに CSV から組み立てずにそれを読む(同梱する Client の master.bytes と同じものにして、版の握手でずれないように)。
param(
    [Parameter(Mandatory = $true)][string]$Output,
    [string]$Runtime = 'win-x64',
    [string]$MasterBytes = ''
)
$ErrorActionPreference = 'Stop'
$csproj = Join-Path $PSScriptRoot '..' 'src' 'Terrace.Server' 'Terrace.Server.csproj'
$Output = [System.IO.Path]::GetFullPath($Output)

& dotnet publish $csproj -c Release -r $Runtime --self-contained true -o $Output -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "dotnet publish に失敗しました (exit $LASTEXITCODE)" }

if ($MasterBytes) {
    if (-not (Test-Path $MasterBytes)) { throw "master.bytes がありません: $MasterBytes" }
    Copy-Item $MasterBytes (Join-Path $Output 'content' 'master.bytes') -Force
}

$maps = @(Get-ChildItem (Join-Path $Output 'content' 'maps') -Filter *.json -ErrorAction SilentlyContinue).Count
if ($maps -eq 0) { throw "書き出した版に content/maps がありません: $Output" }
"server published: $Output ($Runtime, マップ $maps 枚$(if ($MasterBytes) { '、master.bytes つき' }))"
