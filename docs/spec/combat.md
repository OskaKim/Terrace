---
status: 実装済み(Client のオフラインとオンライン)。Server は敵へのダメージだけを持つ
sources:
  - Terrace.Client/Assets/Terrace/Runtime/Core/GameSimulation.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/LocalWorld.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Player.cs
  - Terrace.Server/src/Terrace.Server/Rooms/Room.cs
---

# 戦闘・被弾・死亡

数値は `PlayerConfig`(Client)と `MotorConfig.AttackLockSeconds` のプロパティ名だけを書く。

## プレイヤーの攻撃

- 始まる条件: Attack を押した瞬間、はしごの上でない、攻撃硬直中でない([movement.md](movement.md))
- 当たる敵: 生きている敵のうち、次を満たして**一番近い 1 体だけ**
  - 向いている方向の前方 `AttackRange` 以内(敵の幅の半分まで広げて判定)
  - 高さの差が `AttackHeight` 以内
- ダメージ: `AttackDamage`(固定)。HP は 0 未満にならない
- 当たった敵は短く光る(`LocalWorld.HitFlashSeconds`、見た目用)
- HP が 0 になった敵は死亡する。死亡以降は [enemy-drop.md](enemy-drop.md)
- 倒したとき(Client): キル数 +1、メソ加算([economy-shop.md](economy-shop.md))、メッセージ

## 接触ダメージ

- 判定する条件: 生きている、無敵中でない、はしごの上でない
- 当たり判定: プレイヤーは足元基準の箱(半幅 `HalfWidth`、高さ `Height`)、敵は足元基準の箱(`EnemyDefinition.Width` / `Height`)。重なった最初の敵
- ダメージ: `max(1, 敵の Attack)`
- 被弾すると:
  - 無敵 `InvulnerableSeconds`
  - 敵と反対側へ吹き飛ぶ(`KnockbackVelocityX`、`KnockbackVelocityY`)。空中モードになる
  - HP が 0 なら死亡

## 死亡と復活

- 死亡する原因: HP が 0、またはワールドの下へ落ちた(`FellOutOfWorld`)
- 死亡すると: HP 0、開いている店を閉じる、`RespawnSeconds` の間は何もできない
- 復活: HP 全快、無敵 `InvulnerableSeconds`、今いるマップの出現地点に立つ([map-travel-npc.md](map-travel-npc.md))
- 死亡による持ち物やメソの損失は無い

## 無敵が付く場面

被弾、復活、マップ移動の直後。

## Server 側(オンライン)

- `AttackAsync(enemyInstanceId, damage)` を受けた `Room.Attack` は、敵が存在して生きていれば、受け取ったダメージをそのまま引く(負の値は 0 とする)
- 距離や向きの検査は無い。当たり判定とダメージ量をどちらが決めるかは未決([architecture/authority.md](../architecture/authority.md))
- プレイヤーの HP・被弾・死亡は Server に無い

## Client 側(オンライン)

- 攻撃はオフラインと同じ規則で当たる敵を選び(`LocalWorld.FindAttackTarget`)、その敵とダメージを `AttackAsync` で送る。自分では HP を減らさず、被弾の表示だけ先に出す
- HP・撃破は `OnEnemyDamaged` / `OnEnemyDead` で反映する。撃破の報酬(キル数・メソ)は `OnEnemyDead` の倒した人が自分なら足す。他の人が倒したときは「誰々が倒した」と流すだけ
- 敵との接触による被弾・死亡・復活はオフラインと同じ(Client が Server の敵の位置で計算する)
