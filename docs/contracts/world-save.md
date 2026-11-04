---
status: 実装済み(キャラクターの ID・名前・トークンの sha256 だけ。成長と持ち物はまだ Client が持つ)
sources:
  - Terrace.Server/src/Terrace.Server/Saves/WorldSave.cs
  - Terrace.Server/src/Terrace.Server/Accounts/AccountRegistry.cs
  - Terrace.Server/src/Terrace.Server/Hosting/ParentProcessWatcher.cs
  - Terrace.Server/src/Terrace.Server/Hosting/AdminKey.cs
  - Terrace.Client/Assets/Terrace/Runtime/Online/LocalServer.cs
  - Terrace.Server/src/Terrace.Server/Configuration/TerraceServerOptions.cs
  - Terrace.Server/src/Terrace.Server/Program.cs
---

# ワールドの保存

ワールド([ADR 0015](../decisions/0015-player-hosted-servers.md))をフォルダに残す形。ゲームから開くワールドでも専用サーバーでも同じ形にする(ゲームから開いたワールドを、そのまま専用サーバーへ引っ越せるように)。

## いつ使うか

- `TerraceServerOptions.WorldDirectory` にフォルダを指定したときだけ。空なら保存せず、起動のたびに新しいワールドになる
- 起動のとき: フォルダの `world.json` を読む。無ければ新しいワールドを作り、すぐ書く。ワールドの名前は、作るときだけ `TerraceServerOptions.WorldName` を使い、後はファイルの名前を使う
- 書くとき: 新しいキャラクターを作ったとき、Server が止まるとき(親のプロセスがいなくなって止まるときを含む)

## world.json

1 つの JSON。名前は camelCase。

| 項目 | 中身 |
|---|---|
| `version` | 形式の版(`WorldSaveFile.CurrentVersion`) |
| `worldId` | ワールドの ID。Client はトークンをこの ID ごとに覚える |
| `worldName` | ワールドの名前(表示用) |
| `lastPlayerId` | 最後に発行したプレイヤー ID。次は 1 つ大きい番号を出す |
| `accounts[]` | キャラクター: `playerId`、`name`、`tokenSha256` |

- トークンそのものは書かない。sha256(16 進の小文字)だけを書く。ファイルを人に渡しても、それでなりすませないように
- レベル・経験値・メソ・持ち物・最後にいたマップは、まだ書かない。Client が持っているため([ADR 0014](../decisions/0014-player-state-server-authority.md) の段階 2 の後)

## 壊さないための決まり

- 書くときは `world.json.tmp` に書いてから置き換える。書いている途中で落ちても、前の `world.json` が残る
- 知らない版のファイルや、壊れたファイル・ワールドの ID の無いファイルなら、起動を止める。上書きして消さない
- 形を変えたら `CurrentVersion` を上げ、前の版からの移し方を `WorldSave` に足す(遊ぶ人はセーブを持ったまま更新する)

## 親のプロセスの見張りと閉じる口

ゲームから開いたワールドでは、Client(`LocalServer`)が Server を子プロセスで起動し、次の 2 つを起動引数で渡す。

- `TerraceServerOptions.ParentProcessId`: そのプロセス(Client)を `ParentProcessWatcher.IntervalSeconds` ごとに見て、いなくなったら止まる。ゲームが落ちても、Server のプロセスが残らないようにするため
- `TerraceServerOptions.AdminKey`: 閉じる口の鍵(Client が起動のたびに作る乱数)。状態確認のポートの `POST /admin/shutdown` は、ヘッダー `X-Terrace-Admin-Key`(`AdminKey.HeaderName`)にこの鍵があるときだけ受ける。鍵の設定が空なら、口そのものが無い。gRPC のポートでは受けない。Client はワールドを閉じるときにこれを叩き、待ちすぎたら強制的に終わらせる

どちらで止まるときも、ふつうに止めるときと同じく保存する。
