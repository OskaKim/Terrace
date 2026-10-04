#Requires -Version 7
# Client に置いている複製(Terrace.Client/tools/sync-shared.ps1 が写すもの)が、元と同じかを確かめる。
#   pwsh tools/check-sync.ps1               コードとマップを比べる
#   pwsh tools/check-sync.ps1 -MasterData   master.bytes も、CSV から作り直して比べる(masterdata-build を走らせる)
# 食い違いがあれば一覧を出して終了コード 1。直し方は「Terrace.Client で pwsh tools/sync-shared.ps1」。
param([switch]$MasterData)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$client = Join-Path $root 'Terrace.Client'
$problems = [System.Collections.Generic.List[string]]::new()

function Get-Text([string]$Path) {
    # 改行の違いは見ない
    return [IO.File]::ReadAllText($Path).Replace("`r`n", "`n")
}

function Get-Relative([string]$Path) {
    return [IO.Path]::GetRelativePath($root, $Path).Replace('\', '/')
}

# 元のファイル群と複製先のフォルダを突き合わせる。複製先にだけあるファイルも古い複製として数える
function Compare-Copies([string]$Label, [IO.FileInfo[]]$Sources, [string]$CopyDir, [string]$CopyFilter, [string[]]$CopyKeep = @()) {
    $expected = @{}
    foreach ($source in $Sources) {
        $copy = Join-Path $CopyDir $source.Name
        $expected[$source.Name] = $true
        if (-not (Test-Path -LiteralPath $copy)) {
            $problems.Add("${Label}: 複製がありません: $(Get-Relative $copy)")
            continue
        }
        if ((Get-Text $source.FullName) -ne (Get-Text $copy)) {
            $problems.Add("${Label}: 元と違います: $(Get-Relative $copy)(元: $(Get-Relative $source.FullName))")
        }
    }
    if (Test-Path -LiteralPath $CopyDir) {
        foreach ($copy in Get-ChildItem -LiteralPath $CopyDir -File -Filter $CopyFilter) {
            if (-not $expected.ContainsKey($copy.Name) -and $CopyKeep -notcontains $copy.Name) {
                $problems.Add("${Label}: 元に無い複製があります: $(Get-Relative $copy.FullName)")
            }
        }
    }
}

# Map のコード(MapSerializer.cs は写さない)
$mapSources = @(Get-ChildItem (Join-Path $root 'Terrace.Map/src/Terrace.Map') -File -Filter *.cs | Where-Object Name -ne 'MapSerializer.cs')
Compare-Copies 'Map' $mapSources (Join-Path $client 'Assets/Terrace/Shared/Map') '*.cs'

# MasterData のテーブル定義(サブフォルダは平らにして写す)
$mdSources = @(Get-ChildItem (Join-Path $root 'Terrace.MasterData/src/Shared') -Recurse -File -Filter *.cs)
Compare-Copies 'MasterData' $mdSources (Join-Path $client 'Assets/Terrace/Shared/MasterData/Tables') '*.cs'

# 通信の定義
foreach ($folder in 'Dto', 'Hubs', 'Services') {
    $sources = @(Get-ChildItem (Join-Path $root "Terrace.Server/src/Terrace.Shared/$folder") -File -Filter *.cs)
    Compare-Copies "Protocol/$folder" $sources (Join-Path $client "Assets/Terrace/Shared/Protocol/$folder") '*.cs'
}

# マップ JSON(遊び用 + サンプル)
$mapJson = @(Get-ChildItem (Join-Path $root 'Terrace.Map/maps') -File -Filter *.json) + @(Get-Item (Join-Path $root 'Terrace.Map/samples/sample_map.json'))
Compare-Copies 'maps' $mapJson (Join-Path $client 'Assets/StreamingAssets/maps') '*.json'

# master.bytes(CSV から作り直して SHA256 を比べる。作り直しは決定的)
if ($MasterData) {
    $out = Join-Path ([IO.Path]::GetTempPath()) "terrace-check-sync-$PID"
    try {
        & dotnet run --project (Join-Path $root 'Terrace.MasterData/src/Terrace.MasterData.Builder') -- --input (Join-Path $root 'Terrace.MasterData/samples/csv') --output $out | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $problems.Add('master.bytes: masterdata-build が失敗しました(CSV の検証エラー。Terrace.MasterData で確かめる)')
        }
        else {
            $built = (Get-FileHash (Join-Path $out 'master.bytes') -Algorithm SHA256).Hash
            $committed = (Get-FileHash (Join-Path $client 'Assets/StreamingAssets/master.bytes') -Algorithm SHA256).Hash
            if ($built -ne $committed) {
                $problems.Add('master.bytes: CSV から作り直したものと違います(Terrace.Client/Assets/StreamingAssets/master.bytes)')
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
    }
}

if ($problems.Count -gt 0) {
    "Client の複製が元と食い違っています。Terrace.Client で pwsh tools/sync-shared.ps1 を実行してください。"
    $problems | ForEach-Object { "  $_" }
    exit 1
}
"ok: Client の複製は元と同じです$(if ($MasterData) { '(master.bytes を含む)' })"
