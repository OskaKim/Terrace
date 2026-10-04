---
status: 一部を置き換え済み(ADR 0012)
sources:
  - Terrace.Client/Assets/Terrace/Runtime/Core/GameSimulation.Online.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Online/OnlineInbox.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Online/IOnlineChannel.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/OfflineRoom.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Online/RoomMirror.cs
  - Terrace.Client/Assets/Terrace/Runtime/Online/MagicOnionConnection.cs
---

# 0008 オンラインの Client は、通知を受け箱に積んで Step で反映し、切れたらオフラインに戻る

- 日付: 2026-09-28

## 背景

段階 3 で Unity の Client を Server に繋いだ。Client のゲーム規則は純 C# の Core にあり([ADR 0003](0003-pure-csharp-rules.md))、オフラインでは `LocalWorld` が Server の `Room` の代わりを務めている。繋ぐにあたって次を決める必要があった。

- 通信(MagicOnion)を Core にどう見せるか
- どのスレッドで届くか分からない通知を、いつ反映するか
- マップ移動の直後に、前のマップの通知が遅れて届く問題
- 回線が切れたときにどうなるか

## 決定

- Core は通信の実装を知らない。送るのは `IOnlineChannel`(結果を待たずに送るだけ)、受けるのは `OnlineInbox`(`IGameHubReceiver` を実装し、通知を列に積むだけ)。MagicOnion と YetAnotherHttpHandler を使う実装は別アセンブリ(`Terrace.Client.Online`)に置く
- 通知の反映は `GameSimulation.Step` の頭でだけ行う。古い順に取り出して世界に映す
- オンラインでは `LocalWorld` をサーバー権威の形で作る。自分では湧かせず、動かさず、HP を減らさない。攻撃は当たった敵とダメージを送り、表示だけ先に出す(置き換え済み: 今は世界を入れ物と権威に分け、オンラインでは `RoomMirror` に差し替える。[ADR 0012](0012-client-world-authority.md))
- `JoinAsync` を送ったら、そのマップの `OnSnapshot` が届くまでは他の通知を捨てる
- 切断を知ったら、その場でオフラインの世界(今は `OfflineRoom`)に差し替えて遊び続けられるようにする
- MagicOnion のクライアントは実行時の動的生成を使う

## 理由

- 受け箱を挟むと、Core のテストは受け箱に通知を直接積むだけで書ける。通信もスレッドも要らない
- 反映の場所を 1 か所にすると、Unity のメインスレッド以外から Core の状態に触れることが無くなる
- スナップショットは「その時点の全部」なので、それより前の通知を捨てても失うものが無い
- 切断でゲームが止まるより、ひとり遊びに落ちて続けられる方が、開発中の確認がしやすい
- 動的生成なら Source Generator が要らない。Windows の Smart App Control が未署名の生成器 DLL を読ませないことがあり、生成に頼ると Unity でのコンパイルが環境次第で壊れる

## 引き換えにしたもの

- IL2CPP ビルドでは動的生成が使えない。IL2CPP にするときは `[MagicOnionClientGeneration]` による事前生成に切り替える
- スナップショットを作ってから届くまでの間に起きた変化は取りこぼしうる。HP は次の `OnEnemyDamaged` が絶対値で上書きするので戻るが、その間の撃破は復活の通知まで見えない
- オフラインに落ちたあと、自動で繋ぎ直すことはしない。繋ぎ直すには Client を起動し直す
- プレイヤーの HP・メソ・持ち物は Client に残した。権威をどちらに置くかは未決のまま([architecture/authority.md](../architecture/authority.md))

## 関連

- [ADR 0002](0002-authority-split.md)
- [contracts/protocol.md](../contracts/protocol.md)
- [architecture/authority.md](../architecture/authority.md)
