#Requires -Version 7
# 他リポジトリ(Terrace.Map / Terrace.MasterData / Terrace.Server)の共有ソースと成果物を Unity プロジェクトへ複製する。
#   Terrace.Map/src/Terrace.Map/*.cs (MapSerializer.cs を除く)   → Assets/Terrace/Shared/Map/
#   Terrace.Server/src/Terrace.Shared/**/*.cs (通信の約束事)      → Assets/Terrace/Shared/Protocol/
#   Terrace.Map/maps/*.json (町・狩場)                            → Assets/StreamingAssets/maps/
#   Terrace.MasterData/src/Shared/**/*.cs                          → Assets/Terrace/Shared/MasterData/Tables/
#   Terrace.Map/samples/sample_map.json                            → Assets/StreamingAssets/maps/
#   masterdata-build の出力 master.bytes / manifest.json           → Assets/StreamingAssets/
param([switch]$SkipMasterData)
$ErrorActionPreference = 'Stop'
$client = Split-Path $PSScriptRoot -Parent
$terrace = Split-Path $client -Parent
$mapSrc = Join-Path $terrace 'Terrace.Map\src\Terrace.Map'
$mdShared = Join-Path $terrace 'Terrace.MasterData\src\Shared'
$mdBuilder = Join-Path $terrace 'Terrace.MasterData\src\Terrace.MasterData.Builder'
$mdCsv = Join-Path $terrace 'Terrace.MasterData\samples\csv'
$protocolSrc = Join-Path $terrace 'Terrace.Server\src\Terrace.Shared'
$sharedMap = Join-Path $client 'Assets\Terrace\Shared\Map'
$sharedProtocol = Join-Path $client 'Assets\Terrace\Shared\Protocol'
$sharedMd = Join-Path $client 'Assets\Terrace\Shared\MasterData\Tables'
$streaming = Join-Path $client 'Assets\StreamingAssets'
New-Item -ItemType Directory -Force $sharedMap, $sharedMd, $sharedProtocol, (Join-Path $streaming 'maps') | Out-Null

Get-ChildItem $mapSrc -Filter *.cs | Where-Object Name -ne 'MapSerializer.cs' | Copy-Item -Destination $sharedMap -Force
Get-ChildItem $mdShared -Recurse -Filter *.cs | Copy-Item -Destination $sharedMd -Force
foreach ($folder in 'Dto', 'Hubs', 'Services') {
    $destination = Join-Path $sharedProtocol $folder
    New-Item -ItemType Directory -Force $destination | Out-Null
    Get-ChildItem (Join-Path $protocolSrc $folder) -Filter *.cs | Copy-Item -Destination $destination -Force
}
Copy-Item (Join-Path $terrace 'Terrace.Map\samples\sample_map.json') (Join-Path $streaming 'maps\sample_map.json') -Force
Get-ChildItem (Join-Path $terrace 'Terrace.Map\maps') -Filter *.json | Copy-Item -Destination (Join-Path $streaming 'maps') -Force
"copied: Map -> $sharedMap, Protocol -> $sharedProtocol, MasterData -> $sharedMd, maps/*.json -> StreamingAssets/maps"

if (-not $SkipMasterData) {
    $out = Join-Path ([IO.Path]::GetTempPath()) 'terrace-masterdata-out'
    & dotnet run --project $mdBuilder -- --input $mdCsv --output $out
    if ($LASTEXITCODE -ne 0) { throw "masterdata-build が失敗しました (exit $LASTEXITCODE)" }
    Copy-Item (Join-Path $out 'master.bytes') (Join-Path $streaming 'master.bytes') -Force
    Copy-Item (Join-Path $out 'manifest.json') (Join-Path $streaming 'master.manifest.json') -Force
    "copied: master.bytes / master.manifest.json -> StreamingAssets"
}
