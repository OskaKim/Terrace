---
status: 方針は採用済み・オンラインも実装済み / プレイヤーの HP・メソ・経験値とレベル・当たり判定の権威は未決(今は Client)
sources:
  - Terrace.Server/src/Terrace.Server/Rooms/Room.cs
  - Terrace.Server/src/Terrace.Server/Rooms/IMoveValidator.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/IWorldAuthority.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/OfflineRoom.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Online/RoomMirror.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/GameSimulation.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Online/OnlineSession.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Progression.cs
---

# 権威の分担

権威 = その状態を最終的に決める側。判断の理由は [ADR 0002](../decisions/0002-authority-split.md)。

## 状態ごとの権威

| 状態 | 権威 | オフライン(Client がひとりで) | オンライン(Server) |
|---|---|---|---|
| プレイヤーの位置・速度・向き・動作 | **Client** | `CharacterMotor` | `MoveAsync` で受け取り、`IMoveValidator` を通れば保持して他へ中継する。検証は今は常に通す(`NullMoveValidator`) |
| マップ移動(どのマップにいるか) | **Client** | `GameSimulation.ChangeMap` | Client が `JoinAsync(新しい mapId)` を呼ぶ。Server は前のルームから自動で退出させる |
| 敵の湧き・HP・死亡・復活 | **Server** | `OfflineRoom` が同じ規則で肩代わり | `Room.Attack` / `Room.Tick` |
| 敵の巡回位置 | **Server** | `OfflineRoom` | `Room.Tick` で動かし、一定間隔で位置をまとめて配信 |
| ドロップの抽選・寿命 | **Server** | `OfflineRoom` | `Room` |
| ドロップを拾う | **Server**(早い者勝ち) | `OfflineRoom.RequestPickup` がその場で拾う | `Room.Pickup` |
| 攻撃の当たり判定(どの敵に当たったか) | 未決(今は Client) | `GameSimulation` が `WorldState.FindAttackTarget` で選ぶ | Client が `WorldState.FindAttackTarget` で選んだ敵とダメージ量を `AttackAsync` で送り、Server はそのまま受け取る。距離の検査は無い |
| プレイヤーの HP・被弾・死亡 | 未決(今は Client) | `GameSimulation` | 無い。Client が Server の敵の位置との接触で計算する |
| メソ・持ち物・店 | 未決(今は Client) | `GameSimulation`、`ShopSession` | 無い。撃破の報酬は `OnEnemyDead` の倒した人が自分の Client で足し、拾った物は `OnDropRemoved` の拾った人が自分の持ち物に足す。保存はしない |
| 経験値・レベル | 未決(今は Client) | `PlayerProgression`(`GameSimulation` が撃破の報酬として足す) | 無い。メソと同じく `OnEnemyDead` の倒した人が自分の Client で足す。保存はしない([spec/progression.md](../spec/progression.md)) |

## オフラインとオンラインの対応

Client の敵と落とし物の世界は、入れ物(`WorldState`)と、誰が決めるか(`IWorldAuthority`)に分かれている([ADR 0012](../decisions/0012-client-world-authority.md))。オフラインの権威は `OfflineRoom`、オンラインの権威は `RoomMirror` で、`GameSimulation` が今の権威を差し替える。

攻撃と拾うはどちらの権威にも「頼む」だけで、結果(敵が傷ついた・倒れた・湧いた、落とし物が出た・消えた)は同じイベントで届く。`OfflineRoom` は頼まれたその場で決めてすぐイベントを出す。`RoomMirror` は自分では湧かせず動かさず、頼みを送るだけで、Server からの通知を `Apply*` で映したときにイベントを出す。表示位置はサーバー位置へ滑らかに寄せる。対応は次のとおり。

| Client オフライン(`OfflineRoom`) | Client オンライン(`RoomMirror`)と Server(`Room`) |
|---|---|
| `SpawnFromMap()` | ルーム生成時に湧かせる。参加時は `OnSnapshot` で全状態が届く(`ApplySnapshot`) |
| `Tick(dt)` の巡回 | `Room.Tick` → `OnEnemyMove`(`ApplyEnemyMove`) |
| `Tick(dt)` の復活 → `EnemySpawned` | `Room.Tick` → `OnEnemySpawn`(`ApplyEnemySpawn` → `EnemySpawned`) |
| `Tick(dt)` のドロップ寿命 → `DropRemoved`(誰でもない) | `Room.Tick` → `OnDropRemoved(dropId, 0)`(`ApplyDropRemoved` → `DropRemoved`) |
| `RequestAttack(...)` → `EnemyDamaged` / `DropSpawned` / `EnemyKilled`(自分) | `AttackAsync(instanceId, damage)` → `OnEnemyDamaged` / `OnDropSpawn` / `OnEnemyDead`(`Apply*` → 同じイベント。した人は通知の PlayerId) |
| `RequestPickup(...)` → `DropRemoved`(自分) | `PickupAsync(dropId)` → `OnDropRemoved(dropId, playerId)`(`ApplyDropRemoved` → `DropRemoved`) |
| 他のプレイヤー(オフラインでは居ない) | `OnJoin` / `OnMove` / `OnLeave` → `GameSimulation.RemotePlayers` |

`GameSimulation` は結果のイベントのうち、した人が自分のものだけで報酬(`EnemyKilled`)と持ち物(`DropRemoved`)を動かす。

接続が切れたら、Client はその場でオフラインの `OfflineRoom` に差し替え、今いるマップの湧き点から敵を湧かせ直して続ける。

## 同じ規則が 2 か所にある

`Room`(Server)と `OfflineRoom`(Client)は、どちらも「湧く → 巡回 → HP → 死亡 → ドロップ抽選 → N 秒後に復活」を持つ。規則そのものは [spec/enemy-drop.md](../spec/enemy-drop.md) を正とする。

片方を変えるときの手順:

1. `spec/enemy-drop.md` を直す
2. `Terrace.Server/src/Terrace.Server/Rooms/`(`Room`、`RoomEnemy`、`ISpawnConfigProvider`)を直し、`Terrace.Server.Tests` を足す
3. `Terrace.Client/Assets/Terrace/Runtime/Core/`(`OfflineRoom`、`Enemies`)を直し、EditMode テストを足す。通知の映し方が変わるなら `Online/RoomMirror` も

数値(ドロップ寿命・ドロップ率・巡回速度)は今は両方に直書きされている。マスタへ移すかは未決([roadmap.md](../roadmap.md))。

## 移動の権威を Server へ移すとき

`IMoveValidator.IsAcceptable(player, proposed)` に速度や到達可能性の検査を入れる。false を返した移動は保持も中継もしない。Server が Map を参照できるので(`Room.Map`)、足場との整合も検査できる。
