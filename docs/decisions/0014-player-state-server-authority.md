---
status: 一部を置き換え済み(ADR 0015)
sources:
  - Terrace.Server/src/Terrace.Shared/Hubs/IGameHub.cs
  - Terrace.Server/src/Terrace.Server/Hubs/GameHub.cs
  - Terrace.Server/src/Terrace.Server/Accounts/AccountRegistry.cs
  - Terrace.Server/src/Terrace.Server/Rooms/Room.cs
  - Terrace.Server/src/Terrace.Server/Rooms/IMoveValidator.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Progression.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/KillRewardSystem.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/CombatSystem.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/PlayerLifeSystem.cs
---

# 0014 プレイヤーの状態と攻撃の結果は、段階を踏んで Server 権威へ移す

- 日付: 2026-11-04

## 背景

[ADR 0002](0002-authority-split.md) は、移動を Client、敵とドロップを Server に決めた。プレイヤーの HP・メソ・持ち物・経験値とレベル、攻撃の当たり判定とダメージ量は決めていなかった。

今はこれらをすべて Client が持つ。`AttackAsync(enemyInstanceId, damage)` は Client が送ったダメージ量をそのまま引き、撃破の報酬は倒した人が自分の Client で足す。Server にはプレイヤーのセッションが無く、`JoinAsync` の `self` で名乗った ID と名前を使う(ログインで発行した番号かどうかだけは確かめる)。

このままでも、店・アイテムの使用・装備・クエストは足せる。ただし「Client に状態を持つ」形で作ることになり、権威を移すときに作り直しになる。作る前に向きを決める。遊ぶ人が立てたサーバーで知らない人と遊ぶ形を採る場合も、改ざんした Client から他の人を守るには、Server が決める必要がある。

## 決定

プレイヤーの状態と攻撃の結果は Server が決める。次の段階で移し、段階ごとに別のタスクにする。

| 段階 | 移すもの | 形 |
|---|---|---|
| 1. セッション | 誰が繋いでいるか | Server にプレイヤーのセッションを置く。ログインで ID とトークン(推測できない乱数)を発行し、`JoinAsync` は `self` の代わりにトークンを送る。ID と名前は Server がセッションから埋める。あわせて、移動の NaN・無限大・マップの境界の外を受け取らない(`IMoveValidator` がマップを見られるようにする) |
| 2. 成長と持ち物 | 経験値・レベル・メソ・持ち物・店の売り買い | Server がセッションに持つ。撃破の報酬・拾得・売り買いを Server が決め、本人へ通知で送る。Client の `KillRewardSystem`・`LootingSystem`・`TradingSystem` は、オンラインでは通知を映す側に回る(敵の世界の `RoomMirror` と同じ構図) |
| 3. 攻撃の結果 | ダメージ量と、当たり判定の検査 | `AttackAsync` から `damage` を外し、Server がセッションのレベル(レベル表の `Attack`)から出す。狙う敵は今までどおり Client が選び、Server は報告された位置で距離と頻度を検査する |
| 4. HP と被弾 | プレイヤーの HP・被弾・死亡・復活 | Server が、報告された位置と敵の位置で接触を判定する |
| 5. 保存 | 上の状態の永続化 | 別の判断(アカウントと保存) |

- 移動(位置・速度・向き・動作)は Client 権威のまま(ADR 0002)。速度や到達可能性の検査は、手触りへの響きを見てから決める
- 段階 2 以降の規則(報酬・ダメージ・被弾)は、Client と Server の両方から使う純 C#(C# 9)に 1 か所だけ書く。置き場は、敵とドロップの規則を一本化するときに作る共有の規則のライブラリにし、規則を 2 か所に手書きで増やさない。今は Client の Core にある `PlayerProgression` も、そこへ移す(置き換え済み: ひとり遊びも Server で動かすので、規則は Server の `Room` にだけ書く。[ADR 0015](0015-player-hosted-servers.md))
- オフラインは、規則の一本化でオフラインもプロセスの中の `Room` で動くようになるまで、今の Client の係が決める(ひとりで遊ぶので、改ざんから守る相手がいない)。そうなった後は、オンラインと同じ形になる(置き換え済み: ひとり遊びが「誰も呼ばないワールド」に移るまで今の Client の係が決め、移った後は係を消す。[ADR 0015](0015-player-hosted-servers.md))
- 順番: 段階 1 は、チャットと再接続より前に行う。段階 2 は、アイテムの使用・装備・クエストより前に済ませ、これらは最初から Server 権威で作る。段階 3 と 4 は、段階 2 の後なら急がない

## 理由

- 段階 1 は Server と通信の定義だけで閉じ、軽い。なりすましが無くなり、チャットの送り主、再接続(同じ人として戻る)、保存の前提になる
- 段階 2 を段階 3 より先にするのは、ダメージ量を Server がレベルから出すには、レベルを Server が持っている必要があるため
- 狙う敵を Client が選ぶ形を残すのは、移動が Client 権威だから。位置を信じる以上、当たり判定を Server で作り直しても、防げる改ざんはほとんど増えない。距離と頻度の検査で、明らかな不正(遠くの敵を殴る、連打する)を止める
- 規則を共有の純 C# に置けば、オフラインとオンラインで規則がずれない(敵とドロップでは、2 か所に書いた規則が既にずれている)
- `PlayerProgression` は Unity と通信を知らない作りなので、そのまま移せる

## 引き換えにしたもの

- 段階 2 から、撃破の報酬やレベルアップの表示が、通知の往復の分だけ遅れる。Client で先に出して後で正す手もあるが、まずは待つ形にする
- 段階 2 以降は、規則の一本化(と、その前の共有コードの配り方の見直し)を先に要する。基盤の改修が続き、遊べる部分が増えるのが遅くなる
- 位置の改ざんには、今までどおり弱い(ADR 0002 の引き換えのまま)。段階 3 と 4 の検査も、報告された位置を信じた上での検査にとどまる

## 関連

- [architecture/authority.md](../architecture/authority.md)(状態ごとの権威と、どの段階で移すか)
- [ADR 0002](0002-authority-split.md)(移動は Client、敵とドロップは Server)
- [ADR 0012](0012-client-world-authority.md)(Client の敵の世界の、権威の差し替え)
