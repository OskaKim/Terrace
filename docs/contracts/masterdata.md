---
status: 実装済み
sources:
  - Terrace.MasterData/src/Shared/Tables
  - Terrace.MasterData/src/Terrace.MasterData.Builder/Build/BuildPipeline.cs
  - Terrace.MasterData/src/Terrace.MasterData.Builder/Cli/BuildCommand.cs
  - Terrace.MasterData/src/Terrace.MasterData.Builder/Output/Manifest.cs
  - Terrace.MasterData/samples/csv
  - Terrace.Client/Assets/Terrace/Runtime/Unity/MasterDataRepository.cs
  - Terrace.Server/src/Terrace.Server/Content/ServerContent.cs
---

# マスタデータ

CSV → `masterdata-build` → `master.bytes`(MasterMemory / MessagePack)+ `manifest.json`。
CLI の使い方とエラー出力の読み方は `Terrace.MasterData/README.md`。

## テーブル

列の正は `Terrace.MasterData/src/Shared/Tables/*.cs`。ここには関係だけを書く。

| テーブル(= CSV 名) | 型 | 主キー | 参照 | 使うところ |
|---|---|---|---|---|
| `item` | `Item` | `ItemId` | | Client: アイテム名・値段・持ち物タブ・店 |
| `enemy` | `Enemy` | `EnemyId` | `DropItemIds[]` → item | Client: 敵の定義(`Exp` は倒した人が得る経験値。Client だけが使う)。Server: 湧かせる敵の値 |
| `quest` | `Quest` | `QuestId` | `RewardItemId` → item | まだどこからも使っていない |
| `player_level` | `PlayerLevel` | `Level` | | Client: レベル表(`LevelTable`)。1 行が 1 レベルで、`Level` は 1 から欠けなく続く([spec/progression.md](../spec/progression.md)) |

列挙型 `ItemCategory` は `Weapon` / `Armor` / `Consumable` / `Material`(と `None`)。Client での扱いは [spec/economy-shop.md](../spec/economy-shop.md)。

## テーブル定義の決まり

- `[MemoryTable("名前"), MessagePackObject(true)]` を付けたクラスを `src/Shared/Tables/` に置く。**テーブル名 = CSV のファイル名(拡張子なし)**
- Builder は `[MemoryTable]` の付いた型をリフレクションで自動で見つける。一覧ファイルや設定ファイルは作らない
- 主キーに `[PrimaryKey]`
- 検証は `IValidatable<T>` で書く。メッセージは `"[列名] 本文"` の形にすると、列名が出力に載る
- C# 9 の範囲で書く(Unity と Server からも使うため)

## CSV の決まり

- UTF-8(BOM は問わない)。改行は LF / CRLF どちらでもよい
- コメント行(`#` で始まる)と空行を除いた最初の行がヘッダ。ヘッダ名 = プロパティ名(大文字小文字は区別しない)。列の順は自由
- コメント行と空行も行番号には数える(エディタの行番号と合わせるため)
- `"` で囲むとカンマを含められる。`""` は `"`。セルは行をまたげない。囲まないセルは前後の空白を取る
- bool: `true` / `false`(大文字小文字を問わない)/ `1` / `0`
- 配列: `|` 区切り(例 `1|2|3`)。空セルは空配列
- enum: 名前(大文字小文字を問わない)または数値。定義に無い数値はエラー
- 空セル: その型の既定値(`int` → 0、`string` → 空文字、`int?` → null)
- 未知の列 → 警告(続行)。定義にある列が CSV に無い → エラー
- テーブル定義があるのに CSV が無い → エラー。CSV があるのに定義が無い → 警告

## 変換と検証

1. CSV を読んで型に変換する(ここで行番号と列名が決まる)
2. MasterMemory のバイナリにし、読み戻して `Validate()`(主キー重複、`IValidatable<T>` の参照・範囲チェック)
3. 失敗を主キー値から CSV の行へ逆引きし、`ファイル:行 [列名] 本文` の形で出す

- エラーは途中で止めずに全件集める
- 1 でエラーがあれば 2 は行わない(壊れた行が抜けた状態で参照チェックをすると偽のエラーが出るため)
- 終了コード: `0` 成功 / `1` エラーあり(出力ファイルは書かない)/ `2` 引数の誤り

## manifest.json

| プロパティ | 意味 |
|---|---|
| `formatVersion` | manifest 自体の形の版 |
| `generatedAt` | 生成日時(UTC、ISO 8601) |
| `sha256` | master.bytes の SHA256(小文字 16 進)。Client と Server で比べればマスタの版の一致が分かる |
| `byteLength` | master.bytes のバイト数 |
| `tables[]` | `name`、`type`、`count` |

## 使う側

| 使う側 | 読み方 |
|---|---|
| Client | `StreamingAssets/master.bytes` と `master.manifest.json`(`sync-shared.ps1` が置く)。`MasterDataRepository` が読んでゲームの型に変換する。無ければ `FallbackEnemies` / `FallbackItems`(samples/csv と同じ値の埋め込み) |
| Server | `content/master.bytes` があれば読む。無ければ `content/csv/`(ビルド時に `samples/csv` を複製)を Builder のパイプラインで組み立てる(`ServerContent`) |

Unity での注意:

- 生成される `MemoryDatabase` の名前空間を `Terrace.MasterData` に揃えるため、Client 側に `[assembly: MasterMemoryGeneratorOptions(Namespace = "Terrace.MasterData")]` を置いている(`Assets/Terrace/Shared/MasterData/AssemblyInfo.cs`。これは複製物ではない)
- IL2CPP では MessagePack / MasterMemory の生成済みリゾルバの登録が要る(未対応)

## テーブルを足す・列を変えるとき

手順は [guides/add-master-table.md](../guides/add-master-table.md)。列を変えたら、Client の `MasterDataRepository` と `Fallback*`、Server の `ServerContent` の変換も確認する。
