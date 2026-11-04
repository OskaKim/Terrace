---
status: 実装済み(通信・Server・Client の Core・入力欄と吹き出し)
sources:
  - Terrace.Server/src/Terrace.Shared/Dto/Chat.cs
  - Terrace.Server/src/Terrace.Shared/Dto/Notifications.cs
  - Terrace.Server/src/Terrace.Server/Chat/ChatGate.cs
  - Terrace.Server/src/Terrace.Server/Hubs/GameHub.cs
  - Terrace.Server/src/Terrace.Server/Configuration/TerraceServerOptions.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Online/OnlineSession.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/GameNarrator.cs
  - Terrace.Client/Assets/Terrace/Runtime/Presentation/ChatPresenter.cs
  - Terrace.Client/Assets/Terrace/Runtime/Presentation/ChatBubbleLayout.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/ChatView.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/ChatBubbles.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/GameBootstrap.cs
---

# チャット

通信の形(`SendChat`、`ChatNotification`、`ChatRejectedNotification`)は [contracts/protocol.md](../contracts/protocol.md)。ここには規則を書く。

## 届く範囲

- 今はワールド全体の 1 本(`ChatChannel.World`)。ワールド([ADR 0015](../decisions/0015-player-hosted-servers.md))のどこかのマップに参加している全員に届く。違うマップにいる人にも届く
- 送った本人にも届く(echo)。Client は自分の発言を手元では先に出さず、この echo で出す。全員で並びが揃い、二重に出ない
- 通知には送った人がいたマップ(`MapId`)が入る。同じマップの人の発言にだけ吹き出しを出すため
- マップ内のチャット・囁き・パーティのチャットは、まだ無い

## 送り主

- 送り主の ID と名前は、Server がセッション(ログインのトークンから引いたキャラクター)で埋める。Client が名乗った名前は使わない
- 参加していない接続(参加の前、同じ ID の後から来た接続に置き換えられた接続)のチャットは無視する

## 本文の扱い(Server、`ChatGate`)

順に確かめる。

1. 前後の空白を落とし、制御文字(改行・タブを含む)、行と段落の区切り、書式文字(右から左への上書き、ゼロ幅の文字など)を取り除く。絵文字をつなぐ ZWJ だけは残す
2. 空になったら何もしない(誰にも配らず、本人にも知らせない)
3. `ChatLimits.MaxLength` より長ければ断る(`TooLong`)。長さは取り除いた後の UTF-16 の文字数
4. 連投の間引き: プレイヤーごとのトークンバケツ。続けて `TerraceServerOptions.ChatBurst` 回まで送れ、`ChatRefillSeconds` ごとに 1 回分戻る(戻る秒数が 0 以下なら間引かない)。尽きていれば断る(`TooFast`)。長すぎて断った本文と、空の本文は回数を減らさない

断ったときは、送った本人にだけ `ChatRejectedNotification` で理由と本文を返す(入力欄に戻せるように)。

Server のログには本文を出さない(長さだけ)。

## Client

- 送る前に前後の空白を落とし、空なら送らない(`OnlineSession.SendChat`)
- 届いたチャットは、メッセージ欄に「名前: 本文」で流す。断られたら理由を流す(`GameNarrator`)
- チャットはワールド全体のものなので、マップを移ってスナップショットを待っている間に届いても捨てない
- ひとりで遊んでいる間(オフライン)は送れない(送り口が無い)

## 入力欄(`ChatPresenter`)

- Enter で入力欄を開き、もう一度 Enter で送って閉じる。空なら送らずに閉じる。Esc(とフォーカスが外れたとき)は送らずに閉じる
- 開けるのはオンラインのときだけ。店を開いている間も開けない。オフラインに切り替わったら閉じる
- 入力中は、移動の入力を止め(`InputFrame.None`)、消音(M)とミニマップ(N)の切り替えも効かない(起動役の `GameBootstrap` が止める)
- 文字数は `ChatLimits.MaxLength` で切る
- 断られたら、その本文を入力欄に戻す(勝手には開かない。次に開いたときに直して送れる)

## 吹き出し

- 同じマップにいる人(自分も)の発言は、その人の頭の上に吹き出しで `ChatBubbleConfig.Seconds` の間出す。別のマップの人の発言は、メッセージ欄にだけ出る
- 1 行の幅は `ChatBubbleConfig.WrapColumns`(半角を 1、全角と絵文字を 2 と数える)。行数は `ChatBubbleConfig.MaxLines` までで、超えたら切って「…」を付ける(`ChatBubbleLayout`)
- 新しい発言が来たら、前の吹き出しを置き換える
