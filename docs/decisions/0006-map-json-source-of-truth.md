---
status: 採用
sources:
  - Terrace.Map/maps
  - Terrace.Client/Assets/StreamingAssets/maps
  - Terrace.Client/tools/sync-shared.ps1
  - Terrace.Server/src/Terrace.Server/Terrace.Server.csproj
---

# 0006 マップ JSON の正は Terrace.Map/maps に置き、Client へ同期する

- 日付: 2026-09-16(提案)、2026-09-28(採用)

## 背景

遊ぶためのマップ(`town01.json`、`field01.json`、`field02.json`)が 2 か所に同じ内容で置かれている。

| 場所 | 状態 | 読む側 |
|---|---|---|
| `Terrace.Map/maps/` | git に未追跡 | Server(csproj がビルド時に `content/maps/` へ複製) |
| `Terrace.Client/Assets/StreamingAssets/maps/` | `field01.json` だけ追跡済み。残りは未追跡 | Client |

`sync-shared.ps1` は `samples/sample_map.json` だけを写し、`maps/` は写さない。片方だけ直すと Client と Server でマップが食い違う。

## 提案

- 正は `Terrace.Map/maps/` とし、Terrace.Map の git で管理する
- `sync-shared.ps1` に `maps/*.json` → `StreamingAssets/maps/` の複製を足す
- Client の `StreamingAssets/maps/` は複製物として扱い、手で編集しない

## 理由

- マップの形(型と検証)を持つリポジトリに中身も置けば、形の変更と中身の直しを同じコミットにできる
- Server はすでに `Terrace.Map/maps/` を読んでいる
- 共有コードと同じ「元リポジトリが正、Client へは複製」の規則に揃う([ADR 0004](0004-share-code-by-copy-csharp9.md))

## 他の案

- Client を正にする: Unity で見ながら直す流れには合うが、Server が Client リポジトリに依存することになる
- マップ専用のリポジトリを作る: 今の規模では過剰

## 採用のときにしたこと

1. `Terrace.Map/maps/` をコミットした(GitHub へ上げる準備のとき。Server を単独で取ってきてもマップが揃うように)
2. `sync-shared.ps1` に `maps/*.json` の複製を足した
3. Client の `StreamingAssets/maps/` の遊び用マップを、同期で置き換えた(中身は同じだった)
4. [guides/add-map.md](../guides/add-map.md)、[guides/sync.md](../guides/sync.md)、[roadmap.md](../roadmap.md) を直した
