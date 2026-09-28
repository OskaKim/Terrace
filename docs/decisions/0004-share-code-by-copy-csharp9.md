---
status: 採用
sources:
  - Terrace.Client/tools/sync-shared.ps1
  - Terrace.Server/src/Terrace.Server/Terrace.Server.csproj
  - Terrace.Server/src/Terrace.Shared/Terrace.Shared.csproj
---

# 0004 共有コードは C# 9 に留め、Client へは複製、Server へはプロジェクト参照で渡す

- 日付: 2026-09-04(C# 9 の制約)、2026-09-14(Client への複製)。Server からのプロジェクト参照は実装中(2026-09-16 時点で未コミット)

## 背景

Map のデータ構造、マスタのテーブル定義、通信の DTO は、Client(Unity)と Server(.NET 10)の両方で使う。4 つは独立したリポジトリで、パッケージの配布基盤は無い。

## 決定

- 共有コード(`Terrace.Map/src/Terrace.Map`、`Terrace.MasterData/src/Shared`、`Terrace.Server/src/Terrace.Shared`)は C# 9 の文法に留める
- Client へは `tools/sync-shared.ps1` でソースを複製する。複製先(`Assets/Terrace/Shared/`)は手で編集しない
- System.Text.Json に依存する `MapSerializer.cs` は複製しない。Client は同じ JSON を Newtonsoft で読む
- Server へは兄弟リポジトリを `ProjectReference` で直接参照する。4 リポジトリが `Terrace/` の直下に並ぶことが前提
- MasterData の Shared は独立した csproj にせず、使う側がグロブで取り込む

## 理由

- Unity の言語バージョンに合わせれば、同じソースがそのまま動く
- NuGet パッケージ化や git submodule より手間が少なく、開発初期の変更の速さに付いていける
- Unity は csproj を参照できないので複製、Server は参照できるので直接参照にした

## 引き換えにしたもの

- 同期を忘れると Client が古いコードのまま動く
- 4 リポジトリの置き場所が固定される
- C# 10 以降の書き方が共有コードで使えない

## 関連

- [architecture/system.md](../architecture/system.md)
- [guides/sync.md](../guides/sync.md)
