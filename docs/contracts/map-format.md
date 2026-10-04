---
status: 実装済み
sources:
  - Terrace.Map/src/Terrace.Map/MapData.cs
  - Terrace.Map/src/Terrace.Map/Foothold.cs
  - Terrace.Map/src/Terrace.Map/Ladder.cs
  - Terrace.Map/src/Terrace.Map/Portal.cs
  - Terrace.Map/src/Terrace.Map/SpawnPoint.cs
  - Terrace.Map/src/Terrace.Map/Npc.cs
  - Terrace.Map/src/Terrace.Map/Decoration.cs
  - Terrace.Map/src/Terrace.Map/WorldBounds.cs
  - Terrace.Map/src/Terrace.Map/MapValidation.cs
  - Terrace.Map/src/Terrace.Map/MapSerializer.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/MapLoader.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/AudioLibrary.cs
  - docs/contracts/map.schema.json
---

# マップ JSON の形

型の正は `Terrace.Map/src/Terrace.Map/*.cs`。エディタの補完と検査には [map.schema.json](map.schema.json) を使う。VS Code で `Terrace/` フォルダを開くと、ルートの `.vscode/settings.json` が `Terrace.Map/maps/`、`Terrace.Map/samples/`、`Terrace.Client/Assets/StreamingAssets/maps/` の JSON に割り当てる。

## 読み手

| 読み手 | 読み方 |
|---|---|
| Terrace.Map / Terrace.Server | `MapSerializer`(System.Text.Json) |
| Terrace.Client | `MapLoader`(Newtonsoft.Json)。Unity に System.Text.Json が無いため |

**2 つのシリアライザの両方で読める形に留める。** 形を変えたら Client の EditMode テスト(`MapAssetsTests`)で確かめる。

## ファイルの決まり

- UTF-8(BOM なし)、1 ファイル 1 マップ
- プロパティ名は camelCase。読み込みは大文字小文字を区別しない
- `//` コメントと末尾カンマは System.Text.Json では読めるが、補完・検査が効かなくなるので使わない
- 計算で出す値(`left`、`isVertical` など)は書かない
- ファイル名が `sample` で始まるものは、Client と Server の読み込み対象外
- マップ ID はすべてのファイルを通して一意

## 座標

X が右、Y が上。単位はユニット(1.0 = 1 ユニット)。オブジェクトの `x`, `y` は足元の位置。

## オブジェクト

### マップ(ルート)

| プロパティ | 型 | 必須 | 意味 |
|---|---|---|---|
| `id` | int | 必須 | マップ ID。1 以上 |
| `name` | string | | 表示名 |
| `theme` | string | | 見た目のテーマ。既定 `grass`([spec/map-travel-npc.md](../spec/map-travel-npc.md)) |
| `bgm` | string | | 流す曲。Client の `Assets/Resources/Terrace/Audio/Bgm/` からの `/` 区切りの相対パスで、拡張子を付けない(例 `town1_home_town`)。省略か空なら無音([spec/map-travel-npc.md](../spec/map-travel-npc.md)) |
| `bounds` | object | 必須 | ワールド境界 `{ left, right, top, bottom }`。`left < right`、`bottom < top` |
| `footholds` | array | | 足場 |
| `ladders` | array | | はしご・ロープ |
| `portals` | array | | ポータルと出現地点 |
| `spawnPoints` | array | | 敵の湧き点 |
| `npcs` | array | | NPC |
| `decorations` | array | | 飾り |

### 足場 `footholds[]`

| プロパティ | 型 | 意味 |
|---|---|---|
| `id` | int | マップ内で一意。0 は「繋がり無し」の意味に使うので 1 以上 |
| `x1`, `y1`, `x2`, `y2` | number | 線分の両端 |
| `prevId` | int | (x1, y1) 側に繋がる足場の ID。0 なら端(崖) |
| `nextId` | int | (x2, y2) 側に繋がる足場の ID。0 なら端(崖) |
| `layer` | int | 重なった足場の区別 |

- `y1 != y2` なら坂道。`x1 == x2` なら壁(歩いて当たると止まる。着地の対象にならない)
- 繋がりは相互に書く: A の `nextId` が B なら、B の `prevId`(または B の向きに応じた側)が A
- 左右の向きは座標で決まる。`x1 <= x2` なら右側の端は `nextId`、`x1 > x2` なら右側の端は `prevId`

### はしご・ロープ `ladders[]`

| プロパティ | 型 | 意味 |
|---|---|---|
| `id` | int | マップ内で一意 |
| `x` | number | X 位置 |
| `y1`, `y2` | number | 区間の両端(順不同) |
| `isRope` | bool | true ならロープ(見た目だけの違い) |

上端・下端に足場があれば、登り切り・降り切りでその足場に乗る。

### ポータル `portals[]`

| プロパティ | 型 | 意味 |
|---|---|---|
| `id` | int | マップ内で一意 |
| `name` | string | マップ内で一意。他のマップから `targetPortalName` で指される |
| `x`, `y` | number | 位置 |
| `kind` | `"Portal"` / `"Spawn"` | 省略時 `Portal` |
| `targetMapId` | int | 行き先のマップ ID(`Portal` のとき必須) |
| `targetPortalName` | string | 行き先のポータル名(`Portal` のとき必須) |

- `Spawn` は出現地点。1 マップに 1 つ、名前は `spawn` にする慣習
- 行き来できる門は、両方のマップに互いを指すポータルを置く

### 湧き点 `spawnPoints[]`

| プロパティ | 型 | 意味 |
|---|---|---|
| `id` | int | マップ内で一意 |
| `x`, `y` | number | 位置。少し上から真下の足場を探して立たせる |
| `enemyId` | int | マスタ enemy テーブルの `EnemyId` |
| `respawnSeconds` | number | 死亡から復活までの秒数。0 より大きくする([spec/enemy-drop.md](../spec/enemy-drop.md)) |

### NPC `npcs[]`

| プロパティ | 型 | 意味 |
|---|---|---|
| `id` | int | マップ内で一意 |
| `name` | string | 表示名 |
| `x`, `y` | number | 位置 |
| `kind` | `"talk"` / `"shop"` | 省略時 `talk` |
| `shopId` | string | `shop` のときの店 ID |
| `greeting` | string | 話しかけたときの一言 |
| `sprite` | string | 見た目の名前(Client が解釈)。空なら既定 |

### 飾り `decorations[]`

| プロパティ | 型 | 意味 |
|---|---|---|
| `id` | int | マップ内で一意 |
| `sprite` | string | 素材フォルダからの相対パス。例 `Buildings/houseBeige` |
| `x`, `y` | number | 位置 |
| `layer` | int | 描画順。小さいほど奥。既定 -10 |
| `scale` | number | 拡大率。既定 1 |
| `flipX` | bool | 左右反転 |

## 検証(`MapData.Validate()`)

戻り値の一覧が空なら正常。各問題は `Code`、`Kind`(オブジェクトの種類)、`ObjectId`、`Message` を持つ。

| Code | 内容 |
|---|---|
| `InvalidWorldBounds` | 境界が `left < right`、`bottom < top` を満たさない |
| `DuplicateId` | 同じ種類のオブジェクトで ID が重複 |
| `DuplicatePortalName` | ポータル名が重複 |
| `FootholdSelfLink` | `prevId` / `nextId` が自分自身 |
| `FootholdLinkTargetMissing` | `prevId` / `nextId` が存在しない ID |
| `FootholdLinkNotMutual` | 繋がりが相互になっていない |
| `FootholdEndpointTooFar` | 繋がった相手の端点と許容誤差より離れている |
| `PortalTargetEmpty` | `Portal` の行き先が空 |
| `PortalTargetSelf` | 行き先が自分自身 |
| `OutOfWorldBounds` | 足場・はしご・ポータル・湧き点・NPC が境界の外 |
| `InvalidBgmPath` | `bgm` が `/` 区切りの相対パスでない(`\`・先頭の `/`・空の区切り・`.` や `..`・拡張子を含む) |

検証しないこと(書き手が守る): 行き先のマップやポータルが実在するか、`enemyId` がマスタにあるか、`shopId` の店があるか、`sprite` の素材や `bgm` の曲のファイルがあるか。飾りの素材と曲のファイルは Client の EditMode テストが確かめる。

## 形を変えるとき

1. `Terrace.Map/src/Terrace.Map/` の型を変える。足すプロパティには既定値を付け、既存の JSON が読めるままにする
2. `Terrace.Map` のテストを直す(`dotnet test`)
3. この文書と `map.schema.json` を直す
4. `Terrace.Client/tools/sync-shared.ps1` で Client へ複製し、`MapLoader` で読めるか EditMode テストで確かめる
5. Server は `ProjectReference` なので再ビルドで反映される
