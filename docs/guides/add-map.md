---
status: 手順(マップの置き場が未決のため暫定)
sources:
  - Terrace.Map/maps
  - Terrace.Client/Assets/StreamingAssets/maps
  - Terrace.Client/Assets/Tests/EditMode/MapAssetsTests.cs
  - docs/contracts/map.schema.json
---

# マップを足す

形の決まりは [contracts/map-format.md](../contracts/map-format.md)。正の置き場は `Terrace.Map/maps/`([ADR 0006](../decisions/0006-map-json-source-of-truth.md))。

## 手順

1. `Terrace.Map/maps/` に JSON を作る
   - VS Code でこのフォルダ(`Terrace/`)を開いていれば、`map.schema.json` で補完と検査が効く
   - マップ ID はすべてのマップで一意
   - 出現地点(`kind: "Spawn"`、名前 `spawn`)を 1 つ置く
2. 足場を繋ぐ
   - 繋がる足場同士の `prevId` / `nextId` を相互に書き、端点の座標を揃える
   - 崖にしたい端は 0
3. 門を繋ぐ
   - 行き来する 2 枚のマップの両方に、互いを指すポータルを置く(`targetMapId` と `targetPortalName`)
   - 行き先のマップとポータルの実在は `Validate()` では検査されない。目で確かめる
4. 湧き点を置く
   - `enemyId` はマスタの `enemy.csv` にある ID
   - `respawnSeconds` は 0 より大きく
5. NPC と飾りを置く
   - 店の `shopId` は今ある店(`general` / `potion` / `equip`)から選ぶ
   - 飾りの `sprite` は `Terrace.Client/Assets/Resources/Terrace/Art/Kenney/` にある絵
6. Client へ複製する: `Terrace.Client` で `pwsh tools/sync-shared.ps1 -SkipMasterData`(`Assets/StreamingAssets/maps/` は手で編集しない)
7. 確かめる
   - Client: `pwsh tools/unity.ps1 -EditMode`。`MapAssetsTests` が全マップの `Validate()` と飾りの素材の有無を見る
   - Server: `dotnet run --project src/Terrace.Server` の起動ログと `GET /` の `warnings` に問題が出ないこと
   - 遊んで、門を通って行き来できること
8. [spec/map-travel-npc.md](../spec/map-travel-npc.md) のマップの繋がりの図を直す

## 注意

- ファイル名を `sample` で始めると読み込まれない
- `theme` は `grass` / `stone` / `sand` / `castle` / `snow` から選ぶ
