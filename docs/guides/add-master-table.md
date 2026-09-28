---
status: 手順
sources:
  - Terrace.MasterData/src/Shared/Tables
  - Terrace.MasterData/samples/csv
  - Terrace.MasterData/README.md
---

# マスタのテーブルを足す

形の決まりは [contracts/masterdata.md](../contracts/masterdata.md)。

## 手順

1. `Terrace.MasterData/src/Shared/Tables/` にクラスを 1 つ足す
   - `[MemoryTable("名前"), MessagePackObject(true)]` と `[PrimaryKey]`
   - C# 9 の範囲(ブロック形式の namespace、`get; set;`)
   - 検証が要れば `IValidatable<T>` を実装し、メッセージを `"[列名] 本文"` の形にする。参照チェックは `GetReferenceSet<T>()` を使う
2. `Terrace.MasterData/samples/csv/` に `名前.csv` を置く(1 行目がヘッダ、ヘッダ名 = プロパティ名)
3. `Terrace.MasterData` で `dotnet test`。必要なら `tests/Terrace.MasterData.Tests/Fixtures/` に正常系・異常系の CSV を足す

ここまでで Builder は新しいテーブルを変換できる。一覧ファイルや設定ファイルは作らない。作る必要が出たら設計を疑う。

## 使う側へ届ける

4. Client: `Terrace.Client` で `pwsh tools/sync-shared.ps1`(テーブル定義の複製と master.bytes の再生成)
5. Client でテーブルを読む: `Runtime/Unity/MasterDataRepository.cs` で Core の型に変換する。master.bytes が無いときの `Fallback*` も要るか考える
6. Server でテーブルを読む: `Content/ServerContent.cs` の `Master`(`MemoryDatabase`)から引く
7. [contracts/masterdata.md](../contracts/masterdata.md) のテーブル表に 1 行足す

## 列を足す・変えるとき

- CSV に列が無いとエラーになるので、既存の CSV(samples と tests の Fixtures)にも列を足す
- Client の `MasterDataRepository` / `Fallback*`、Server の `ServerContent` の変換を確認する
