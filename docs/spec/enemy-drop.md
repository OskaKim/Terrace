---
status: 実装済み(Client オフライン、Server、Client のオンラインでの反映)
sources:
  - Terrace.Client/Assets/Terrace/Runtime/Core/LocalWorld.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Enemies.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/MasterDataRepository.cs
  - Terrace.Server/src/Terrace.Server/Rooms/Room.cs
  - Terrace.Server/src/Terrace.Server/Rooms/RoomEnemy.cs
  - Terrace.Server/src/Terrace.Server/Rooms/ISpawnConfigProvider.cs
  - Terrace.Server/src/Terrace.Server/Rooms/EnemySpawnConfig.cs
---

# 敵とドロップ

この規則は Client(`LocalWorld`)と Server(`Room`)の両方にある。変えるときは両方を直す([architecture/authority.md](../architecture/authority.md))。

## 敵の値の出どころ

| 値 | 出どころ |
|---|---|
| 名前・最大 HP・攻撃力・ドロップ候補 | マスタの enemy テーブル(`Name`、`Hp`、`Attack`、`DropItemIds`) |
| 復活までの秒数 | マップの湧き点(`SpawnPoint.RespawnSeconds`) |
| ドロップ率・巡回速度 | コードの既定値(Client: `EnemyDefinition`、Server: `MapSpawnConfigProvider`)。マスタに無い |
| 大きさ(当たり判定) | Client の `EnemyDefinition.Width` / `Height` の既定値。Server には無い |
| メソ報酬 | Client の `EnemyDefinition.MesoReward`。マスタに無いので、実際は常に最大 HP と同じ |
| 経験値(倒した人が得る) | マスタの enemy テーブル(`Exp`)。Client だけが使う([progression.md](progression.md)) |

マスタに無い敵 ID のとき:

- Client: master.bytes に無ければ `FallbackEnemies`(samples/csv と同じ値の埋め込み)を引き、そこにも無ければ `EnemyDefinition` の既定値の敵
- Server: 既定値(`MapSpawnConfigProvider` を参照)

## 湧く

- マップの湧き点 1 つにつき 1 体湧く(Server の `EnemySpawnConfig.Count` は複数も表せるが、マップから作るときは 1)
- 立つ足場: 湧き点の少し上から真下を探した足場。X は湧き点の X、Y は足場の上
- 最初は左を向く
- Client: マップに初めて入ったときに湧く。マップごとに世界を覚えていて、離れている間は時間が止まる
- Server: ルームを作ったときに湧く。参加者には `OnSnapshot` で届く。ルームは空になると破棄され、次に作るときは湧き直す

## 巡回

- 自分の足場の上だけを、巡回速度で往復する。足場の端で向きを変える。隣の足場へは移らない
- 足場が無い、足場の幅が 0、巡回速度が 0 なら動かない
- Server は生きていて動く敵の位置を、プレイヤーがいるときだけ `Room.EnemyMoveBroadcastInterval` ごとにまとめて配信する(`OnEnemyMove`)

## 死亡

- HP が 0 になると死亡する。死亡中は攻撃も接触ダメージも無い
- 復活タイマー = 湧き点の `RespawnSeconds`
  - Server は 0 以下のとき既定の秒数にする。Client はそのまま使う(既知の不整合、[roadmap.md](../roadmap.md))
- ドロップを抽選する(下)
- Server の通知順: `OnEnemyDamaged` → ドロップがあれば `OnDropSpawn` → `OnEnemyDead`

## 復活

- タイマーが 0 以下になったら、HP 全快で湧き点の X、足場の上に戻る
- Server は `OnEnemySpawn` を全員に送る

## ドロップの抽選

- ドロップ候補(`DropItemIds`)の**それぞれについて独立に**、ドロップ率で落とすか決める。0 個のことも全部落ちることもある
- 落ちたアイテムは、敵の足元を中心に横へ少しずつずらして並べる
- 寿命はドロップ寿命の定数(Client: `LocalWorld.DropLifetimeSeconds`、Server: `Room.DropLifetimeSeconds`)。切れたら消える(Server は `OnDropRemoved(dropId, 0)`)

## 拾う

| | Client(オフライン) | Server |
|---|---|---|
| 指定 | Pickup を押した位置の近く | `PickupAsync(dropId)` で ID を指定 |
| 条件 | 横の距離が `PlayerConfig.PickupRange` 以内、高さの差が小さい。横に一番近いもの | そのドロップがまだある。距離の検査は無い |
| 競合 | 無い | 早い者勝ち。消えたら全員に `OnDropRemoved(dropId, playerId)` |
| 結果 | 持ち物に追加 | Server は持ち物を持たない(未決) |

オンラインの Client は、近くのドロップを選んで `PickupAsync` を送る。返事(`OnDropRemoved`)が来るまで同じドロップを重ねて要求しない。`OnDropRemoved` の拾った人が自分なら持ち物に足し、他の人なら消すだけ。

## オンラインの Client での敵

- 自分では湧かせず、巡回も復活もさせない。`OnSnapshot` / `OnEnemySpawn` / `OnEnemyDamaged` / `OnEnemyDead` / `OnEnemyMove` をそのまま映す(`LocalWorld` の `Apply*`)
- 表示位置は届いた位置へ滑らかに寄せる(`LocalWorld.NetFollowRate`)。`LocalWorld.NetSnapDistance` より離れていたら瞬間移動する
- 最大 HP は Server が送る値を使う
