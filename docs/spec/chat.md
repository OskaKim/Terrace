---
status: 実装済み(通信・Server・Client の Core)。画面の入力欄と吹き出しはまだ
sources:
  - Terrace.Server/src/Terrace.Shared/Dto/Chat.cs
  - Terrace.Server/src/Terrace.Shared/Dto/Notifications.cs
  - Terrace.Server/src/Terrace.Server/Chat/ChatGate.cs
  - Terrace.Server/src/Terrace.Server/Hubs/GameHub.cs
  - Terrace.Server/src/Terrace.Server/Configuration/TerraceServerOptions.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Online/OnlineSession.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/GameNarrator.cs
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
