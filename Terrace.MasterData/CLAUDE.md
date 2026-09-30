# Terrace.MasterData — AI 向けの決まり

全体の決まりは親フォルダの `../CLAUDE.md`、形の約束は `../docs/contracts/masterdata.md`。

## このリポジトリ

CSV → master.bytes + manifest.json の変換 CLI `masterdata-build` と、そのテスト。テーブル定義(`src/Shared/Tables/`)の正でもある。

## 決まり

- `src/Shared/` は Unity と Server からも使う。C# 9 の範囲で書き、UnityEngine を参照しない
- テーブルの追加は「`src/Shared/Tables/` にクラス 1 つ + 同名の CSV」だけで済ませる。一覧ファイル・設定ファイルを作らない
- 検証は MasterMemory の Validator(`IValidatable<T>`)で書く。独自の検証エンジンを作らない
- 検証エラーは必ず `ファイル:行 [列名] 本文` まで逆引きして出す。逆引きできない出力を足さない
- エラーは fail-fast にせず全件集める

## 検証

```bash
dotnet test
dotnet run --project src/Terrace.MasterData.Builder -- --input samples/csv --output out
```

## 変えたら

- テーブル定義や `samples/csv` を変えた → Client で `pwsh tools/sync-shared.ps1`、Server は再ビルド
- テーブルの関係・CSV の決まり・manifest を変えた → `../docs/contracts/masterdata.md`
