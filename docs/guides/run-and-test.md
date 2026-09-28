---
status: 手順
sources:
  - Terrace.Client/tools/unity.ps1
  - Terrace.Server/src/Terrace.Server/appsettings.json
  - Terrace.Server/src/Terrace.TestClient/ClientOptions.cs
  - Terrace.Client/tools/e2e-online.ps1
  - Terrace.Client/Assets/Terrace/Runtime/Unity/OnlineSettings.cs
  - tools/check-docs.ps1
---

# 動かす・テストする

詳しい出力例は各リポジトリの README にある。

## テスト

| リポジトリ | コマンド(そのリポジトリのフォルダで) | 何を確かめるか |
|---|---|---|
| Terrace.MasterData | `dotnet test` | CSV の読み込み・型変換・行トレース・CLI |
| Terrace.Map | `dotnet test` | 足場の問い合わせ・JSON 入出力・検証(壊れたマップのフィクスチャ) |
| Terrace.Server | `dotnet test` | ルームの規則(Hub を通さず `Room` / `RoomManager` を直接) |
| Terrace.Client | `pwsh tools/unity.ps1 -EditMode` | Core の移動・戦闘・敵・店、マップと素材とマスタの読み込み |
| Terrace.Client | `pwsh tools/unity.ps1 -PlayMode` | 実際に起動して歩き・倒し・拾う。ログイン窓、接続の失敗。画面を `Logs/smoke.png` に保存 |
| Terrace.Client + Server | `pwsh tools/e2e-online.ps1` | Server を起動して PlayMode を走らせる。2 人で繋いで互いに見え、移動と攻撃が届き、退出が伝わるか。画面を `Logs/online.png`、Server のログを `Logs/e2e-server.log` に保存 |

- Client の結果は `Logs/*-results.xml` と `Logs/*.log`
- エディタで同じプロジェクトを開いているときは `-Mirror` を付ける。一時フォルダへ複製して走らせ、結果を `Logs/mirror/` に写す

## Client を遊ぶ

Unity で `Assets/Scenes/Main.unity` を再生する。名前と接続先を入れる窓が出るので「ひとりで遊ぶ」か「オンラインで遊ぶ」を選ぶ。操作は `Terrace.Client/README.md`。

## 複数人で遊ぶ

1. Server を起動する(下の節)
2. Client を 2 つ以上動かし、それぞれ「オンラインで遊ぶ」。動かし方は次のどれか
   - エディタの Multiplayer Play Mode(`Window > Multiplayer > Multiplayer Play Mode`)で仮想プレイヤーを足す
   - `pwsh tools/unity.ps1 -Build` で `Build/Windows/Terrace.exe` を作って複数起動する。起動引数は `OnlineSettings`(`-terraceName`、`-terraceServer`、`-terraceOnline`、`-terraceOffline`)
   - Unity の Client とテストクライアントを並べる
3. 別の PC から繋ぐときは Server の `ListenAnyIP` を有効にし、ファイアウォールで gRPC のポートを開ける

## Server を動かす

```bash
cd Terrace.Server
dotnet run --project src/Terrace.Server
```

- gRPC(h2c)と状態確認の HTTP のポートは `appsettings.json` の `Terrace` セクション。起動引数 `--Terrace:GrpcPort=...` でも変えられる
- 状態確認: ブラウザか `curl` で HTTP のポートの `/` を開く。読み込んだマップとマスタ、警告、ルームの状況が JSON で出る
- 起動ログに、読み込んだマップ・マスタと警告が出る

## テストクライアントで通信を確かめる

Server を起動したまま、別のターミナルで 2 つ起動する。片方の移動がもう片方に流れてくれば経路は通っている。

```bash
cd Terrace.Server
dotnet run --project src/Terrace.TestClient -- --name alice --map 1 --interval 300 --duration 16 --attack
```

```bash
cd Terrace.Server
dotnet run --project src/Terrace.TestClient -- --name bob --map 1 --interval 300 --duration 7
```

引数の正は `src/Terrace.TestClient/ClientOptions.cs`。`--mode manual` で矢印キーの手動操作になる。

## 文書を検査する

```bash
pwsh tools/check-docs.ps1
```

`docs/` の front matter の `sources` が実在するか、文書内の相対リンクが切れていないかを見る。
