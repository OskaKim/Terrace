---
status: 手順
sources:
  - Terrace.Client/tools/sync-shared.ps1
  - Terrace.Client/tools/nuget-restore.ps1
  - Terrace.Server/src/Terrace.Server/Terrace.Server.csproj
---

# 同期

理由は [ADR 0004](../decisions/0004-share-code-by-copy-csharp9.md)。

## Client への同期(`Terrace.Client/tools/sync-shared.ps1`)

| 元 | 先 | いつ実行するか |
|---|---|---|
| `Terrace.Map/src/Terrace.Map/*.cs`(`MapSerializer.cs` 以外) | `Assets/Terrace/Shared/Map/` | Map の型や問い合わせを変えたとき |
| `Terrace.MasterData/src/Shared/**/*.cs` | `Assets/Terrace/Shared/MasterData/Tables/` | テーブル定義を変えたとき |
| `Terrace.Server/src/Terrace.Shared/{Dto,Hubs,Services}/*.cs` | `Assets/Terrace/Shared/Protocol/` | 通信の定義を変えたとき |
| `Terrace.Map/maps/*.json`、`Terrace.Map/samples/sample_map.json` | `Assets/StreamingAssets/maps/` | マップを変えたとき |
| `masterdata-build` の出力 | `Assets/StreamingAssets/master.bytes`、`master.manifest.json` | CSV かテーブル定義を変えたとき |

```bash
cd Terrace.Client
pwsh tools/sync-shared.ps1                   # 全部
pwsh tools/sync-shared.ps1 -SkipMasterData   # コードとマップだけ(masterdata-build を走らせない)
```

- 複製先は手で編集しない。元を直して同期し直す
- 複製先の asmdef と csc.rsp は Client のもの(同期で上書きしない)
- 同期の後は `pwsh tools/unity.ps1 -EditMode` で確かめる
- 同期を忘れていないかは、ルートで `pwsh tools/check-sync.ps1 -MasterData` が調べる(CI でも回る)

## NuGet の復元(`Terrace.Client/tools/nuget-restore.ps1`)

`Assets/packages.config` を変えたときに実行する。`Assets/Packages/` はコミットしてあるので、取ってきただけなら要らない。

```bash
cd Terrace.Client
pwsh tools/nuget-restore.ps1
```

復元の後、Source Generator の DLL の meta を「全プラットフォーム無効・明示参照のみ」に書き換える(型の二重定義 CS0433 を防ぐ)。翻訳リソース、Roslyn 3.x 向けの重複、使っていない MagicOnion のクライアント生成器はアナライザとしても読ませない。

NuGetForUnity の復元は `packages.config` に書いたものだけを入れ、依存を辿らない。パッケージを足すときは、netstandard2.1 向けの依存をすべて `packages.config` に並べる(Unity に同梱の System.Memory などは要らない)。

## Server への反映

同期の操作は要らない。

- コード: `Terrace.Map` と `Terrace.MasterData.Builder` を `ProjectReference` で参照しているので、ビルドで反映される
- マップ: ビルド時に `Terrace.Map/maps/*.json` を出力フォルダの `content/maps/` へ複製する
- マスタ: ビルド時に `Terrace.MasterData/samples/csv/*.csv` を `content/csv/` へ複製し、起動時に組み立てる(`content/master.bytes` を置けばそちらを優先)
