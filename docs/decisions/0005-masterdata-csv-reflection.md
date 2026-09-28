---
status: 採用
sources:
  - Terrace.MasterData/src/Terrace.MasterData.Builder/Discovery/TableDiscovery.cs
  - Terrace.MasterData/src/Terrace.MasterData.Builder/Mapping/RowMapper.cs
  - Terrace.MasterData/src/Terrace.MasterData.Builder/Tracing/RowTraceRegistry.cs
---

# 0005 マスタは CSV から MasterMemory へ。テーブルはリフレクションで自動発見

- 日付: 2026-09-04

## 背景

敵・アイテム・クエストなどの表データを、人が編集しやすい形で書き、ゲームでは速く読みたい。検証エラーが「どのファイルの何行目か」分からないと直せない。

## 決定

1. 書く形式は CSV、ゲームが読む形式は MasterMemory のバイナリ(MessagePack)
2. テーブルは `[MemoryTable]` の付いた型をリフレクションで自動収集する。一覧ファイルは作らない
3. CSV のファイル名 = テーブル名。対応付けの設定ファイルは作らない
4. CSV → オブジェクトの写しはリフレクションで行う(Builder はオフラインの CLI なので速さは要らない)
5. 検証は MasterMemory の Validator をそのまま使い、独自の検証エンジンは持たない。Builder は結果を CSV の行へ逆引きして日本語にするだけ
6. エラーは途中で止めず全件集めてから出す

## 理由

- テーブルを足す手順が「クラス 1 つ + CSV 1 つ」で済む。手順が増えるなら設計が間違っている、という基準を置いた
- 行トレースがあれば、エラーからすぐ CSV の該当行を開ける

## 引き換えにしたもの

- CSV はセルに改行を持てない、型の表現力が低い
- スプレッドシート連携・暗号化・差分配信は持たない

## 関連

- [contracts/masterdata.md](../contracts/masterdata.md)
- [guides/add-master-table.md](../guides/add-master-table.md)
