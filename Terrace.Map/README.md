# Terrace.Map

フットホールド(線分)ベースの 2D 横スクロールアクション RPG 向けに、マップ構造を扱う純 C# ライブラリ。
UnityEngine にも物理エンジンにも依存しません。ルート名前空間は `Terrace.Map`。

- 座標系: X が右、Y が上。単位は無次元(1.0 = 1 ユニット)
- マップはタイルマップではなく「フットホールドのグラフ」。地面や床は線分の連なりで、線分の端が前後の線分に繋がる

## 構成

```
Terrace.Map/
  src/Terrace.Map/          … ライブラリ本体(C# 9 固定。将来 Unity へそのまま持ち込めるように)
  tests/Terrace.Map.Tests/  … xUnit。Fixtures/broken/ に壊れたマップ JSON
  samples/sample_map.json   … 坂道・段差・崖・はしご 1 本・ポータル 2 つを含むテスト用マップ
  Terrace.Map.sln
```

```bash
dotnet test
```

## データモデル

| 型 | フィールド | 補足 |
|---|---|---|
| `Foothold` | `Id, X1, Y1, X2, Y2, PrevId, NextId, Layer` | `PrevId` は (X1, Y1) 側、`NextId` は (X2, Y2) 側に繋がる相手。`0` ならチェーンの端(= 崖)。`Y1 != Y2` なら坂道、`X1 == X2` は壁 |
| `Ladder` | `Id, X, Y1, Y2, IsRope` | はしごとロープの区別は `IsRope` |
| `Portal` | `Id, Name, X, Y, Kind, TargetMapId, TargetPortalName` | `Kind` は `Portal`(入ると接続先へ移動)か `Spawn`(出現地点。入っても何も起きず、接続先も不要) |
| `SpawnPoint` | `Id, X, Y, EnemyId, RespawnSeconds` | |
| `Npc` | `Id, Name, X, Y, Kind, ShopId, Greeting, Sprite` | `Kind` は `talk` か `shop`。`shop` なら `ShopId` の店を開く |
| `Decoration` | `Id, Sprite, X, Y, Layer, Scale, FlipX` | 見た目だけの飾り。`Sprite` はクライアントの素材名 |
| `WorldBounds` | `Left, Right, Top, Bottom` | Y が上なので `Top > Bottom` |
| `MapData` | `Id, Name, Theme, Bounds, Footholds, Ladders, Portals, SpawnPoints, Npcs, Decorations` | マップ本体。型名は名前空間 `Terrace.Map` との衝突を避けて `MapData`。`Theme` は地面や背景の見た目の名前(grass / stone / sand など) |

`Position(X, Y)` は座標を渡すための readonly struct です。

### 向きの扱い

`Direction.Left / Right` はジオメトリで解決します。`X1 <= X2` なら Right 側の端は (X2, Y2) つまり `NextId`、`X1 > X2`(右から左へ定義された線分)なら Right 側の端は (X1, Y1) つまり `PrevId` です。定義の向きを気にせず「右へ歩いて端に着いたら `GetNext(foothold, Direction.Right)`」と書けます。

## 問い合わせ API(`MapData`)

コントローラ側が物理エンジンなしで移動を組めるだけの情報を返します。

| メソッド | 意味 |
|---|---|
| `FindFootholdBelow(x, y, layer?, tolerance)` | 指定座標の真下(同じ高さを含む)にある一番近い足場。壁は対象外。`layer` を指定するとそのレイヤーだけ |
| `GetYAt(foothold, x)` | 線分上の指定 X における Y(線形補間。斜面対応) |
| `IsWithinX(foothold, x)` | X が線分の範囲内か(両端を含む) |
| `GetNext(foothold, direction)` | 端に到達したとき繋がる次の足場。無ければ `null` |
| `IsEdge(foothold, direction)` | その方向が崖か |
| `FindLadderNear(x, y, range)` | つかまれるはしご/ロープ(X の差が `range` 以内、Y が区間内) |
| `FindPortalNear(x, y, range)` | 距離が `range` 以内で最も近い、入れるポータル(出現地点は除く) |
| `FindSpawnPortal()` | 出現地点(`Kind = Spawn`、無ければ名前が `spawn` のポータル) |
| `FindNpcNear(x, y, range)` | 距離が `range` 以内で最も近い NPC |
| `ClampToWorld(position)` | ワールド境界の内側へ丸める |
| `FindFoothold(id)` ほか | Id / 名前での検索 |

典型的な使い方:

```csharp
var map = MapData.Load("samples/sample_map.json");

// 落下中: 真下の足場を探して着地させる
var below = map.FindFootholdBelow(player.X, player.Y);
if (below != null && player.Y - map.GetYAt(below, player.X) < fallStepThisFrame)
{
    player.Y = map.GetYAt(below, player.X);
    player.Ground = below;
}

// 歩行中: 端に着いたら次の足場へ乗り換え、崖なら落下へ
if (!map.IsWithinX(player.Ground, nextX))
{
    var next = map.GetNext(player.Ground, Direction.Right);
    if (next == null) StartFalling(); else player.Ground = next;
}
```

## シリアライズ

マップはマスタデータではなく構造データなので JSON で読み書きします(System.Text.Json)。

```csharp
var map = MapData.Load(path);   // = MapSerializer.Load(path)
map.Save(path);                  // = MapSerializer.Save(map, path)
var json = MapSerializer.ToJson(map);
```

- プロパティ名は camelCase。読み込みは大文字小文字を区別せず、`//` コメントと末尾カンマを許容
- `Left` / `Right` / `IsVertical` などの計算プロパティは書き出さない

サンプル([samples/sample_map.json](samples/sample_map.json)):

```
 y
 12  ──────#6(layer1)──────
 10                        #4 ──────── #5 ────────   east(portal)
  5              #3 ─────┐ |ladder
  0  #1 ─────── /#2(坂)   └(崖)
     spawn(portal)
     x: 0      10      20      30      40      50
```

## バリデーション

`map.Validate(tolerance = 0.01f)` は問題の一覧(`MapValidationIssue`: `Code`, `Kind`, `ObjectId`, `Message`)を返します。空なら正常。

| Code | 内容 |
|---|---|
| `FootholdLinkTargetMissing` | `PrevId` / `NextId` が存在しない Id を指している |
| `FootholdLinkNotMutual` | A の Next が B なのに B の Prev が A でない(逆も) |
| `FootholdEndpointTooFar` | 繋がっている相手の端点と `tolerance` より離れている |
| `FootholdSelfLink` | 自分自身を指している |
| `PortalTargetEmpty` | ポータルの接続先(`TargetMapId <= 0` または名前が空) |
| `PortalTargetSelf` | 接続先が自分自身(同じマップの同じ名前) |
| `OutOfWorldBounds` | ワールド境界の外にあるオブジェクト(足場・はしご・ポータル・湧き点) |
| `DuplicateId` / `DuplicatePortalName` | Id / ポータル名の重複 |
| `InvalidWorldBounds` | 境界が `Left < Right`, `Bottom < Top` を満たさない(このとき境界チェックは行わない) |

## やらないこと

- 描画、Unity 連携、キャラクターコントローラそのものの実装
- 経路探索(後で別途)

## Unity へ持ち込むとき

`src/Terrace.Map` は C# 9 に固定してあり、`MapSerializer.cs` 以外は System.Text.Json に依存しません。Unity 側では `MapSerializer.cs` を除いて取り込み、同じ JSON 形式(camelCase)を読めるシリアライザで `MapData` を復元してください。
