# Terrace.Server

2D 横スクロール MORPG のゲームサーバーを [MagicOnion](https://github.com/Cysharp/MagicOnion) 7.10 で作るための土台。
ルームのロジックは MagicOnion に依存しない純 C# クラスで、Hub はそれを呼ぶだけです。

```
Terrace.Server/
  src/
    Terrace.Shared/       … インターフェース定義と DTO(netstandard2.1 / C# 9。後で Unity からも参照する)
    Terrace.Server/       … ASP.NET Core + MagicOnion。ルームロジック(Rooms/)もここ
    Terrace.TestClient/   … 動作確認用のコンソールクライアント(MagicOnion.Client)
  tests/
    Terrace.Server.Tests/ … xUnit。ルームロジックを直接叩く
  Terrace.Server.sln
```

## 1. サーバーをローカル起動する

```bash
dotnet run --project src/Terrace.Server
```

起動ログに接続先が出ます。

```
info: Terrace.Server[0]  Room map=1 を作成しました (敵 3 体)
info: Terrace.Server[0]  gRPC (h2c): http://localhost:5000   状態確認: http://localhost:5001/
```

| ポート | 用途 |
|---|---|
| `http://localhost:5000` | gRPC(MagicOnion)。**TLS なしの HTTP/2(h2c)**。テストクライアントも Unity(YetAnotherHttpHandler)もこの URL に繋ぐ |
| `http://localhost:5001/` | 状態確認用の HTTP/1.1。ブラウザや curl で開くと接続先 URL とルーム状況(プレイヤー・敵)が JSON で見える |

接続先 URL の確認方法:

```bash
curl http://localhost:5001/
```

```json
{"status":"ok","grpc":"http://localhost:5000","rooms":[{"mapId":1,"players":[],"enemies":[{"instanceId":1,"enemyId":1,"hp":30,"maxHp":30,"isDead":false,"x":25,"y":5}, ...]}]}
```

平文では HTTP/1.1 と HTTP/2 を同じポートに同居させられないため、gRPC 用ポートは HTTP/2 専用です(ブラウザで 5000 を開いても応答しません)。ポートは `appsettings.json` の `Terrace` セクション、または起動引数で変えられます。

```bash
dotnet run --project src/Terrace.Server -- --Terrace:GrpcPort=6000 --Terrace:HttpPort=6001
```

LAN 内の別端末から繋ぐときは `--Terrace:ListenAnyIP=true`。

Windows の Smart App Control がサーバーの DLL を止める(`0x800711C7`)ときは、Docker のコンテナで動かします(`pwsh tools/server-docker.ps1`。手順は [docs/guides/run-and-test.md](../docs/guides/run-and-test.md) の「Docker で動かす」)。

## 2. テストクライアントを 2 つ立ち上げて、移動が流れてくるのを確認する

ターミナルを 3 つ開き、順に実行します(コピペで動きます)。

ターミナル 1: サーバー

```bash
dotnet run --project src/Terrace.Server
```

ターミナル 2: alice(左右に往復して MoveAsync を送り続ける)

```bash
dotnet run --project src/Terrace.TestClient -- --name alice --map 1 --server http://localhost:5000
```

ターミナル 3: bob

```bash
dotnet run --project src/Terrace.TestClient -- --name bob --map 1 --server http://localhost:5000
```

bob 側の標準出力に、alice の移動が流れてきます(alice 側にも bob の移動が流れます)。

```
12:34:56.001 [login] name=bob => playerId=2 character=bob lv=1 map=1 (2.0, 0.0)
12:34:56.120 [join] map=1 as bob#2
12:34:56.121 [recv] OnSnapshot map=1 players=1 enemies=3
12:34:56.121 [recv]   player=alice x=5.0 y=0.0 state=Walk
12:34:56.121 [recv]   enemy#1 (id=1) hp=30/30 at (25.0, 5.0)
12:34:56.121 [recv]   enemy#2 (id=1) hp=30/30 at (25.0, 5.0)
12:34:56.121 [recv]   enemy#3 (id=2) hp=80/80 at (45.0, 10.0)
12:34:56.500 [recv] OnMove player=alice x=6.0 y=0.0 state=Walk
12:34:57.000 [recv] OnMove player=alice x=7.0 y=0.0 state=Walk
```

Ctrl+C を押すと `LeaveAsync` を送ってから終了し、相手側には `[recv] OnLeave player=alice` が届きます。

### 手動モード(矢印キーで移動、スペースで攻撃)

```bash
dotnet run --project src/Terrace.TestClient -- --name carol --map 1 --server http://localhost:5000 --mode manual
```

| キー | 動作 |
|---|---|
| ← → | X を ±1 動かして `MoveAsync`(Walk) |
| ↑ ↓ | Y を ±1 動かして `MoveAsync`(Jump / Ladder) |
| Space | 最寄りの生きている敵に `AttackAsync`(ダメージ 10) |
| q | 終了 |

敵の HP はサーバー権威なので、攻撃すると全員に `OnEnemyDamaged` が届き、HP が 0 になると `OnEnemyDead`(ドロップ抽選つき)、N 秒後に `OnEnemySpawn` で復活します。

引数一覧: `--name <表示名>`(必須)、`--map <mapId>`(既定 1)、`--server <URL>`(既定 http://localhost:5000)、`--mode patrol|manual`(既定 patrol)、`--interval <ms>`(patrol の送信間隔、既定 500)、`--duration <秒>`(指定秒数で自動退室、既定 0 = Ctrl+C まで)、`--attack`(patrol 中に 4 回に 1 回、最寄りの敵を攻撃)。

### 手を触れずに一通り確認する(自動退室)

`--duration` と `--attack` を使うと、キー入力なしで攻撃・死亡・ドロップ・リスポーン・退室まで確認できます。サーバーを起動した状態で、別々のターミナルから:

```bash
dotnet run --project src/Terrace.TestClient -- --name alice --map 1 --server http://localhost:5000 --interval 300 --duration 16 --attack
```

```bash
dotnet run --project src/Terrace.TestClient -- --name bob --map 1 --server http://localhost:5000 --interval 300 --duration 7
```

alice 側には、bob の移動、`OnEnemyDamaged` → `OnEnemyDead ... drops=[4]`、10 秒後の `OnEnemySpawn enemy#1`、bob の `OnLeave` が順に流れ、16 秒後に `[leave] ok` で終了コード 0 になります。両者が退室すると `curl http://localhost:5001/` の `rooms` は空になります(空になったルームは破棄される)。

## テスト

```bash
dotnet test
```

ルームロジック(`Room` / `RoomManager`)を Hub を介さず直接叩きます。

- プレイヤー 2 人が Join して、片方の Move がもう片方に届く形で状態が更新される
- Leave すると他方から見えなくなる
- 敵にダメージを与えて HP が 0 になると死亡状態になり、Tick を進めるとリスポーンする
- 後から Join したプレイヤーの Snapshot に、既存プレイヤーと敵の現在状態が含まれる
- 検証フックが拒否した移動は反映しない、空になったルームは破棄される、など

## 設計

### Shared(`Terrace.Shared`)

| 種別 | 定義 |
|---|---|
| Unary | `IAccountService.LoginAsync(string name)` → `LoginResult { PlayerId, Character }` |
| StreamingHub | `IGameHub`: `JoinAsync(mapId, self)` / `LeaveAsync()` / `MoveAsync(MoveState)` / `AttackAsync(enemyInstanceId, damage)` |
| Receiver | `IGameHubReceiver`: `OnJoin` / `OnLeave` / `OnMove` / `OnEnemySpawn` / `OnEnemyDamaged` / `OnEnemyDead` / `OnSnapshot` |
| DTO | `PlayerInfo`, `MoveState`(座標・速度・向き・状態 Stand/Walk/Jump/Ladder), `EnemyState`, `RoomSnapshot`, `LoginResult`。すべて `[MessagePackObject]` |

### 割り切り(確定事項)

- **移動はクライアント権威**。クライアントが計算した `MoveState` をそのまま受け取り、他のクライアントへ中継する。ただし後で権威をサーバーへ移せるよう、座標を受け取る箇所に検証フック `IMoveValidator` を切ってある(初期実装 `NullMoveValidator` は常に true)
- **モンスターの HP・死亡判定・リスポーン・ドロップ抽選はサーバー権威**(`Room.Attack` / `Room.Tick`)

### ルームロジック(純 C#、`src/Terrace.Server/Rooms/`)

| 型 | 役割 |
|---|---|
| `Room` | 1 マップ = 1 ルーム。プレイヤー辞書、Join / Leave、位置更新の保持、Snapshot、敵の管理、`Tick(deltaSeconds)` でリスポーンタイマーを進める |
| `RoomManager` | mapId でルームを取得・生成し、空になったら破棄する |
| `IRoomEventSink` | ルームで起きた状態変化の通知先。Room は MagicOnion を知らず、この口だけを呼ぶ |
| `EnemySpawnConfig` / `ISpawnConfigProvider` | スポーン設定。今は `DummySpawnConfigProvider`(どのマップにも同じ 2 か所) |
| `RoomTickService` | `BackgroundService`。100 ms ごとに全ルームの `Tick` を呼ぶ |

MagicOnion 側(`src/Terrace.Server/Hubs/`)は薄い橋渡しだけです。

- `GameHub`: `StreamingHubBase`。接続ごとの playerId / mapId を持ち、`RoomManager` を呼ぶ。切断時は自動で退室
- `RoomGroupRegistry`: mapId ごとの配信グループ(Multicaster の `IMulticastSyncGroup<int, IGameHubReceiver>`、キーは PlayerId)
- `GroupRoomEventSink`: `IRoomEventSink` をグループ配信に変換する。Join / Move は本人以外へ、敵のイベントは全員へ

配信経路: `Hub → Room(純 C#) → IRoomEventSink → グループ → 各クライアントの Receiver`。BackgroundService からのリスポーン通知も同じ経路なので、Hub の外からでも配信できます。

## やらないこと

- インベントリ、スキル、レベルアップ、パーティ、チャット
- データベース、認証、暗号化(すべてインメモリ。サーバーを再起動すると消える)
- 配備(本番のコンテナ構成やレジストリ。Docker は開発する PC でサーバーを動かすためだけに使う)
- Unity クライアント側の実装
- 移動のサーバー側物理シミュレーション

## Unity から繋ぐとき(将来)

- `Terrace.Shared` は netstandard2.1 / C# 9 なので、ソースをそのまま Unity プロジェクトへ取り込める
- 通信は YetAnotherHttpHandler で `http://<host>:5000` へ平文 HTTP/2 で繋ぐ。サーバー側の設定変更は不要
- IL2CPP 向けには MessagePack と MagicOnion のコード生成(Source Generator)が別途必要
