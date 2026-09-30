# Terrace.MasterData

CSV で書いたマスタデータを [MasterMemory](https://github.com/Cysharp/MasterMemory) のバイナリ(`master.bytes`)へ変換する、Unity 非依存の純 .NET CLI ツール `masterdata-build` と、そのテスト。

このツールの一番の価値は **行トレース** です。MasterMemory の検証結果はオブジェクト単位(`Unique failed: ItemId, value = 2`)ですが、本ツールはそれを必ず「どのファイルの何行目・どの列か」まで逆引きして出力します。

```
item.csv:5  [ItemId] 主キーが重複しています (value = 2, 初出 item.csv:3)
quest.csv:3  [RewardItemId] item.csv に存在しないIDを参照しています (value = 999)
quest.csv:4  [RequiredLevel] 1〜99 の範囲で指定してください (value = 0)
item.csv:3  [Price] int として解釈できません (value = abc)
```

## 構成

```
Terrace.MasterData/
  src/
    Shared/                          … テーブル定義(.cs)。唯一の正。将来 Unity とサーバーからも同じファイルを参照する
      Tables/Item.cs, Quest.cs, Enemy.cs, ItemCategory.cs
    Terrace.MasterData.Builder/      … CLI 本体。Shared を <Compile Include="../Shared/**/*.cs" /> でグロブして取り込む
  tests/
    Terrace.MasterData.Tests/        … xUnit。Fixtures/ に正常系と異常系(主キー重複・参照切れ・型不正 など)の CSV
  samples/csv/                       … 動作確認用の CSV 一式
  Terrace.MasterData.sln
```

Shared は独立した csproj ではありません。Builder の csproj からグロブで取り込むだけなので、Unity やサーバー側も同じ形で取り込めます。

## 必要なもの

- .NET SDK 10.0
- NuGet(復元時に自動取得): MasterMemory 3.0.4(Source Generator 同梱)、MessagePack 3.1.8

## 使い方

```bash
dotnet run --project src/Terrace.MasterData.Builder -- --input samples/csv --output out
```

ビルドした実行ファイルを直接使う場合:

```bash
dotnet build -c Release
src/Terrace.MasterData.Builder/bin/Release/net10.0/masterdata-build.exe --input samples/csv --output out --verbose
```

| 引数 | 意味 |
|---|---|
| `--input`, `-i` | テーブル名と同名の CSV(`item.csv` など)を置いたフォルダ。直下のみ探索 |
| `--output`, `-o` | `master.bytes` と `manifest.json` の出力先 |
| `--verbose`, `-v` | テーブル一覧、各 CSV の行数、MasterMemory の生の検証結果も表示 |

終了コード: `0` = 成功、`1` = エラーあり(出力ファイルは書き出さない)、`2` = 引数エラー。

テスト:

```bash
dotnet test
```

## 新しいマスタを 1 つ追加する手順

1. `src/Shared/Tables/` にクラスを 1 つ足す。
2. CSV フォルダに、MemoryTable 名と同じ名前の CSV を置く。

以上です。一覧ファイルの編集も、マッピング用の設定ファイルも要りません。

```csharp
using MasterMemory;
using MessagePack;

namespace Terrace.MasterData.Tables
{
    [MemoryTable("skill"), MessagePackObject(true)]   // ← "skill" == skill.csv
    public sealed class Skill
    {
        [PrimaryKey]
        public int SkillId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Power { get; set; }
        public int[] RequiredItemIds { get; set; } = System.Array.Empty<int>();
    }
}
```

```csv
SkillId,Name,Power,RequiredItemIds
1,Slash,10,
2,Fire Ball,25,3|4
```

仕組み: MasterMemory の Source Generator がビルド時に `DatabaseBuilder` / `MemoryDatabase` を再生成し、Builder は実行時に `[MemoryTable]` の付いた型をリフレクションで拾い、同名の CSV を探します。

検証を付けたい場合(任意)は `IValidatable<T>` を実装します。独自メッセージは `"[列名] 本文"` の形にすると、列名として出力に載ります。参照チェックは `GetReferenceSet<T>().Exists(...)` をそのまま使えば、本ツールが `item.csv に存在しないIDを参照しています` に翻訳します。

```csharp
void IValidatable<Skill>.Validate(IValidator<Skill> validator)
{
    validator.Validate(x => x.Power >= 0, $"[Power] 0 以上で指定してください (value = {Power})");
    var items = validator.GetReferenceSet<Item>();
    foreach (var id in RequiredItemIds)
    {
        var captured = id;
        validator.Validate(x => items.TableData.Any(i => i.ItemId == captured),
            $"[RequiredItemIds] item.csv に存在しないIDを参照しています (value = {captured})");
    }
}
```

Shared のクラスは将来 Unity からも読むため、C# 9 相当の文法(ブロック形式の namespace、`get; set;` プロパティ)に留めています。

## CSV 仕様

- 文字コードは UTF-8(BOM の有無は問わない)。改行は LF / CRLF どちらでも可
- 1 行目(コメント・空行を除く最初の行)がヘッダ。ヘッダ名 == プロパティ名(大文字小文字は区別しない)。列の順序は自由
- `#` で始まる行はコメントとして読み飛ばす。空行も読み飛ばす。どちらも行番号には数える(エディタの行番号と一致させるため)
- セルは `"` で囲むとカンマを含められる。`""` は `"` のエスケープ。セルは行をまたげない
- `"` で囲まないセルは前後の空白を取り除く
- bool は `true` / `false`(大文字小文字を問わない)/ `1` / `0`
- 配列は `|` 区切り(例: `1|2|3`)。空セルは空配列
- enum は名前(大文字小文字を問わない)または数値。定義にない数値はエラー
- 空セルはその型の default(`int` → 0、`bool` → false、`string` → 空文字、`int?` → null)
- 対応する型: int / long / short / byte / uint / ulong / ushort / float / double / decimal / bool / string / char / enum / DateTime / DateTimeOffset / TimeSpan / Guid、それらの配列と Nullable
- 未知のヘッダ列 → 警告として報告し、処理は続行
- 型定義にあるのに CSV に列がない → エラー
- テーブル定義があるのに CSV ファイルがない → エラー。CSV があるのにテーブル定義がない → 警告

## エラー出力の読み方

エラーは fail-fast にせず全件集めてから出力します。

```
=== 警告 (1 件) ===
item.csv:1  [Memo] Item に対応するプロパティがないため無視します
=== エラー (3 件) ===
quest.csv:3  [RewardItemId] item.csv に存在しないIDを参照しています (value = 999)
quest.csv:4  [RequiredLevel] 1〜99 の範囲で指定してください (value = 0)
enemy.csv:2  [DropItemIds] item.csv に存在しないIDを参照しています (value = 999)
失敗: エラー 3 件、警告 1 件 (120 ms)
```

処理は 2 段階です。

1. CSV の読み込み・型変換(行番号・列名はこの時点で確定)
2. MasterMemory の `Validate()`(主キー重複、`IValidatable<T>` の参照・範囲チェック)

1 でエラーがあった場合は 2 を実行しません(壊れた行が抜けた状態で参照チェックをすると偽のエラーが出るため)。出力の末尾にその旨を表示します。

行トレースの仕組み: CSV から生成した各オブジェクトについて「ファイル・行番号」と「主キー値 → 行番号」を台帳に記録します。MasterMemory の検証はバイナリから読み戻した別インスタンスに対して走るため、失敗結果に付いてくるオブジェクトの主キー値で台帳を引いて元の行へ戻します。主キー重複はまさにこの主キーが複数行に対応するケースなので、重複した行すべてを報告できます。

## manifest.json

```json
{
  "formatVersion": 1,
  "generatedAt": "2026-09-04T09:30:00Z",
  "sha256": "9f2c…",
  "byteLength": 1234,
  "tables": [
    { "name": "enemy", "type": "Terrace.MasterData.Tables.Enemy", "count": 3 },
    { "name": "item",  "type": "Terrace.MasterData.Tables.Item",  "count": 5 },
    { "name": "quest", "type": "Terrace.MasterData.Tables.Quest", "count": 3 }
  ]
}
```

`sha256` は `master.bytes` のハッシュです。クライアントとサーバーで同じ値を持っているかを比べれば、マスタのバージョン一致を確認できます。

## 設計判断(確定事項)

1. テーブルの発見は `[MemoryTable]` 属性の付いた型をアセンブリからリフレクションで自動収集する。一覧ファイルは作らない
2. CSV ファイル名(拡張子なし)== MemoryTable 名。マッピング用の設定ファイルは作らない
3. CSV → オブジェクトのマッピングはリフレクション。Builder はビルド時にしか走らないオフライン CLI なので速度は要件ではない
4. データ検証は MasterMemory の Validator(`IValidatable<T>` / `IValidator<T>` / `GetReferenceSet` / `Exists` / `Unique` / `CallOnce`)をそのまま使う。独自の検証エンジンは持たない。本ツールがしているのは、検証結果を行番号へ逆引きして日本語に翻訳することだけ
5. エラーは fail-fast にせず全件収集してからまとめて出力する

## やらないこと

- Unity への依存(UnityEngine の参照は一切なし)。Unity 側のローダは別タスク
- Google スプレッドシート連携
- 暗号化、難読化、差分パッチ、CDN 配信
- GUI エディタ

## Unity から Shared を参照するときの注意(将来)

- MasterMemory 3 系は Unity 2022.3.12f1 以降で、NuGetForUnity 経由で導入する(Source Generator が同梱されているので、旧 `MasterMemory.Generator` は不要)
- Shared の .cs をそのまま Unity プロジェクトへ取り込む(またはリンクする)。生成される `MemoryDatabase` の名前空間は取り込んだ側のルート名前空間になるため、揃えたい場合は `[assembly: MasterMemoryGeneratorOptions(Namespace = "Terrace.MasterData")]` を付ける
