---
status: 採用
sources:
  - Terrace.Client/Assets/Terrace/Runtime/Core/WorldState.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/IWorldAuthority.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/OfflineRoom.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Online/RoomMirror.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/GameSimulation.cs
---

# 0012 Client の敵と落とし物の世界は、入れ物と権威に分け、権威を差し替える

- 日付: 2026-10-04

## 背景

Client の敵と落とし物の世界は `LocalWorld` の 1 クラスで、`WorldAuthority` のフラグで振る舞いを分けていた([ADR 0008](0008-online-client-inbox.md))。オフラインでは湧き・巡回・HP・撃破・ドロップの抽選・復活を自分で決め、オンラインでは Server の通知を `Apply*` で映し、表示位置を補間する。

1 クラスに 2 つの振る舞いが同居した結果、次のことが起きていた。

- 攻撃の結果に「どちらでもある」型が要った(`AttackOutcome.Pending`)。`GameSimulation` の攻撃と拾うの処理に、オンラインかどうかの分岐が散らばった
- 撃破の報酬と拾った物は、オフラインでは攻撃・拾うの戻り値から、オンラインでは通知の受け取りから、と 2 つの道で動いていた
- 敵 1 体の値に、オンラインでは使わないもの(湧き点・足場・復活待ち)と、オフラインでは使わないもの(サーバーから届いた位置)が混ざっていた

## 決定

- 世界を、入れ物(`WorldState`: 敵と落とし物と、その問い合わせ)と、誰が決めるか(`IWorldAuthority`)に分ける
- 権威は 2 つ。オフラインは `OfflineRoom`(Server の `Room` と同じ規則で決める)、オンラインは `RoomMirror`(頼みを送り、Server の通知を映す)。`GameSimulation` が今の権威を差し替える。オフラインの `OfflineRoom` はマップごとに覚えておき、オンラインの `RoomMirror` はマップに入るたびに作る
- 攻撃と拾うは権威に「頼む」だけにする。結果(敵が傷ついた・倒れた・湧いた、落とし物が出た・消えた)は、どちらの権威でも同じイベントで、した人(自分・他のプレイヤー・誰でもない)を添えて出す。`OfflineRoom` は頼まれたその場で、`RoomMirror` は通知が届いたときに出す
- 報酬と持ち物は、結果のイベントのうち、した人が自分のものだけを見て動かす
- 敵 1 体(`EnemyEntity`)には両方で使う値だけを残し、片方でしか使わない値はそれぞれの権威の内側に持つ

## 理由

- 結果の道が 1 つになり、報酬・持ち物・これから足す機能(経験値、音、文言)がオンラインかどうかを知らずに済む
- `OfflineRoom` が Server の `Room` と対になるクラスになり、「同じ規則が 2 か所にある」の Client 側が 1 か所にまとまって比べやすい
- `RoomMirror` を作り直すだけでマップ移動と切断の切り替えができ、前のマップの状態を持ち越す心配が無い
- テストは権威ごとに書ける。`OfflineRoom` は規則だけ、`RoomMirror` は偽の送り口と通知だけで確かめられる

## 引き換えにしたもの

- オフラインで攻撃が当たったかどうかと、倒したかどうかが、別の所(攻撃のイベントと権威の結果のイベント)で分かるようになった。1 か所で両方を知りたい呼び出し側は、両方を購読する
- `GameSimulation` は権威を差し替えるたびに結果のイベントを付け替える
- 通知の受け取り(今は `OnlineSession`)は、スナップショット待ちの判断を持ったまま、世界の通知を `RoomMirror` に渡すだけになった

## 関連

- [ADR 0002](0002-authority-split.md)
- [ADR 0008](0008-online-client-inbox.md)(「`LocalWorld` をサーバー権威の形で作る」をこの ADR で置き換えた)
- [architecture/authority.md](../architecture/authority.md)
- [spec/enemy-drop.md](../spec/enemy-drop.md)
