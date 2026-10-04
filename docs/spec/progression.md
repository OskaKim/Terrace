---
status: 実装済み(Client のオフラインとオンライン)。Server には無い
sources:
  - Terrace.Client/Assets/Terrace/Runtime/Core/Progression.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Player.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/GameSimulation.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/GameSimulation.Online.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/MasterDataRepository.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/HudView.cs
  - Terrace.MasterData/src/Shared/Tables/PlayerLevel.cs
---

# 経験値とレベル

敵を倒すと経験値が溜まり、レベルが上がると最大 HP と攻撃力が伸びる。規則は Core の `PlayerProgression` 1 つにまとめてあり、Unity・通信・`GameSimulation` を知らない(後で権威を Server へ移すとき、そのまま持っていくため)。

## 値の出どころ

| 値 | 出どころ |
|---|---|
| 敵を倒して得る経験値 | マスタの enemy テーブルの `Exp`([enemy-drop.md](enemy-drop.md) の「敵の値の出どころ」) |
| レベルごとの必要経験値・最大 HP・攻撃力 | マスタの player_level テーブル(`ExpToNext`、`MaxHp`、`Attack`)。1 行が 1 レベル、`ExpToNext` が 0 の行が最高レベル([contracts/masterdata.md](../contracts/masterdata.md)) |

master.bytes が無いときは、敵は `FallbackEnemies`、レベル表は `LevelTable.Fallback`(どちらも samples/csv と同じ値の埋め込み。レベル表は先頭の数行だけで、その最後の行を最高レベルにしてある)。

## 経験値を得る

- 自分が敵を倒すと、その敵の `Exp` だけ経験値を得る。倒した人だけが得る(ダメージを与えただけの人は得ない)
  - オフライン: 自分の攻撃で倒したとき
  - オンライン: `OnEnemyDead` の倒した人が自分のとき。同じ撃破の通知が重ねて届いても 1 度だけ
- 得た経験値は、撃破のメッセージに メソと並べて出す
- 最高レベルでは経験値は増えない(メッセージにも出さない)

## レベルが上がる

- 経験値が今のレベルの `ExpToNext` に届いたら、その分を差し引いてレベルを 1 上げる。余りは持ち越す。1 度に複数上がることもある
- 最高レベル(`ExpToNext` が 0 の行、または表の最後の行)に届いたら、余りは捨てて経験値は 0 にする
- 上がると:
  - 最大 HP と攻撃力が、新しいレベルの行の `MaxHp` と `Attack` になる
  - HP が新しい最大 HP まで全快する(倒れている間に上がったときは全快せず、復活で全快する)
  - メッセージ欄で知らせる
  - `GameSimulation.LeveledUp` が上がった数だけ 1 つずつ起きる(引数は上がった先のレベル)。音や光を付けるためのきっかけ

## 能力が効く所

- 最大 HP: HUD、復活したときの全快([combat.md](combat.md))
- 攻撃力: 攻撃で敵に与えるダメージ。オンラインでも `AttackAsync` で送るダメージ量になる(Server は受け取った量をそのまま引く)

## 保たれる・失われる

- マップ移動、オンラインとオフラインの切り替えでは保つ
- 死んでも経験値とレベルは減らない
- 保存しない。起動するとレベル 1(表の一番小さいレベル)、経験値 0

## HUD

- HP の左にレベル(`Lv. 3` のように)
- 画面の下に経験値の棒と、`EXP 12 / 40` のような数字。最高レベルでは `EXP MAX`
- 他のプレイヤーのレベルは出さない

## 権威

今は Client が決める(メソと同じ)。通信の定義は変えていない。Server へ移すかは未決([architecture/authority.md](../architecture/authority.md)、[roadmap.md](../roadmap.md))。
