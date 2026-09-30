# Terrace.Map — AI 向けの決まり

全体の決まりは親フォルダの `../CLAUDE.md`、JSON の形は `../docs/contracts/map-format.md`。

## このプロジェクト

足場(線分)のグラフでマップを表すライブラリ。問い合わせ API、検証(`Validate`)、JSON 入出力。`maps/` に遊び用のマップ、`samples/` にテスト用のサンプルを置く。

## 決まり

- `src/Terrace.Map` は C# 9 の範囲で書き、UnityEngine にも物理エンジンにも依存しない
- System.Text.Json に依存してよいのは `MapSerializer.cs` だけ(Client へは複製しないため)。他のファイルに持ち込まない
- マップはタイルマップにしない。`Foothold` の `PrevId` / `NextId` による繋がりを保つ
- 型にプロパティを足すときは既定値を付け、既存の JSON が読めるままにする
- `maps/` と `samples/` の JSON は `Validate()` が空になる状態を保つ
- 描画・キャラクターコントローラ・経路探索はこのプロジェクトでやらない

## 検証

```bash
dotnet test
```

## 変えたら

- `src/` を変えた → Client で `pwsh tools/sync-shared.ps1`、Server は再ビルド
- JSON の形・検証コードを変えた → `../docs/contracts/map-format.md` と `../docs/contracts/map.schema.json`
- `maps/` を変えた → Client で `pwsh tools/sync-shared.ps1 -SkipMasterData`(`maps/` が正。`../docs/decisions/0006-map-json-source-of-truth.md`)、Server は再ビルド
