# Terrace.Client 全体設計図

コードを全部読まなくても全体像がつかめるように、構造を図にまとめたもの。
詳しい使い方は [README.md](README.md)、4 プロジェクト全体の図は [../ARCHITECTURE.md](../ARCHITECTURE.md)。

## 1. ひとことで

「線分(フットホールド)の上を歩く 2D 横スクロール ARPG」の Unity クライアント。
ゲームの中身は **Unity に依存しない純 C#(Core)** に置き、Unity 側は **入力を渡して結果を描くだけ** にしてある。
だからロジックは EditMode テストで秒速に検証できる。

ひとり(オフライン)でも、Terrace.Server に繋いで複数人(オンライン)でも動く。
オンラインでは **自分の動きは自分で計算し、敵と落とし物はサーバーが決める**。
Core は通信のやり方を知らず、「送る口(IOnlineChannel)」と「届いた通知の受け箱(OnlineInbox)」だけを知っている。

## 2. 層の図

```
                    ┌────────────────────────────────────────────────────────────────┐
   キーボード ─────▶│  Unity 層  (Runtime/Unity  Terrace.Client.Unity)                 │
   マウス           │                                                                │
                    │  GameBootstrap ── 起動役。起動のしかたを選び → 組み立て → 毎フレーム Step│
                    │     ├─ LoginWindow(uGUI) ── 名前と接続先。オンライン / ひとりを選ぶ   │
                    │     ├─ OnlineSettings ─── PlayerPrefs と起動引数(-terraceName など)  │
                    │     ├─ MapRegistry(Newtonsoft) ──▶ StreamingAssets/maps/*.json      │
                    │     ├─ MasterDataRepository ──▶ StreamingAssets/master.bytes        │
                    │     ├─ KeyboardInputSource ──▶ InputFrame、Mouse ──▶ NPC クリック     │
                    │     ├─ ArtLibrary(Resources) ──▶ Kenney の CC0 素材                 │
                    │     ├─ AudioDirector ── Core のイベントを見て効果音を鳴らす。M で消音 │
                    │     │     └─ AudioLibrary(Resources) ──▶ CC0 の効果音               │
                    │     ├─ 見た目: MapView / PlayerView / EnemyView / DropView / NpcView  │
                    │     │          RemotePlayerView(他の人。色違い)                      │
                    │     │          CameraRig / ParallaxBackdrop                          │
                    │     ├─ HudView(IMGUI) ── Lv・HP・EXP・メソ・敵 HP・名札・接続状態・文言│
                    │     └─ ShopWindow(uGUI) ── ShopSession を読んで買う / 売る / 閉じる   │
                    └───────┬───────────────────────────────▲────────────────────────┘
      InputFrame + dt / Interact / Buy / Sell │            │ 状態を読んで描く
                            ▼                               │
   ┌───────────────────────────────────────────────┐   ┌──────────────────────────────────┐
   │  Core 層  (Runtime/Core  Terrace.Client.Core)  │   │  Online 層 (Runtime/Online)        │
   │  UnityEngine 参照なし (noEngineReferences)     │   │  Terrace.Client.Online           │
   │                                               │   │                                  │
   │  GameSimulation ── 1 セッションのまとめ役        │   │  MagicOnionConnection            │
   │    ├─ CharacterMotor  歩く/跳ぶ/はしご/ポータル  │   │   ├─ YetAnotherHttpHandler(h2c) │
   │    ├─ IWorldAuthority 敵とドロップを決める側     │   │   ├─ LoginAsync → PlayerId       │
   │    │   OfflineRoom : 自分で決める(ひとり)        │◀──│   ├─ IGameHub に接続              │
   │    │   RoomMirror  : 通知を映すだけ(オンライン)  │   │   └─ 切断を見張る                 │
   │    ├─ OnlineSession   オンラインの接続(あれば)   │   │  IOnlineChannel を実装(送るだけ) │
   │    │   参加と移動の送信(MoveSender で間引く)     │   └──────────────▲───────────────────┘
   │    │   OnlineInbox の通知を RoomMirror と        │                  │ gRPC / MessagePack
   │    │   RemotePlayers(他の人)に映す               │                  ▼
   │    ├─ PlayerState     HP・メソ・キル数・持ち物   │            Terrace.Server
   │    │   └─ PlayerProgression 経験値とレベル       │
   │    ├─ ShopSession     店の勘定                 │
   │    └─ MessageLog      画面に流す文言             │
   └───────────────┬───────────────────────────────┘
                   │ 問い合わせ・DTO
                   ▼
   ┌───────────────────────────────────────────────────────────────────────────┐
   │  共有層  (Assets/Terrace/Shared  … 他のプロジェクトからの複製。手で編集しない)          │
   │   Terrace.Map         フットホールド / はしご / ポータル / NPC / 検証               │
   │   Terrace.MasterData  Item / Quest / Enemy / PlayerLevel の表(+ MemoryDatabase) │
   │   Terrace.Shared      通信の定義(IGameHub / Receiver / DTO)                      │
   └───────────────────────────────────────────────────────────────────────────┘
```

矢印は「誰が誰を知っているか」。上の層は下の層を知り、下の層は上を知らない。
Core は Online 層を知らない(IOnlineChannel と OnlineInbox は Core 側にある)。
敵とドロップの世界は、入れ物(WorldState。見た目と問い合わせが読む)と、誰が決めるか(IWorldAuthority)に分かれる。
GameSimulation はひとりなら OfflineRoom、オンラインなら RoomMirror を今の権威にし、攻撃と拾うは権威に頼むだけにする
([../docs/decisions/0012-client-world-authority.md](../docs/decisions/0012-client-world-authority.md))。

## 3. 1 フレームの流れ

```
  Update()                                      (GameBootstrap)
    │
    ├─ InputSource.ReadMuteToggle()             M なら消音を切り替える(AudioDirector)
    ├─ input = InputSource.Read()               ←→↑↓ / Jump / Attack / Pickup を InputFrame に
    ├─ dt    = min(Time.deltaTime, 0.05)
    │
    └─ Simulation.Step(input, dt)               (GameSimulation)
         ├─ [オンライン] OnlineSession.Pump: 受け箱の通知を古い順に反映     ← 4c
         │      切断の知らせがあればオフラインへ切り替え
         ├─ WorldAuthority.Tick(dt)              ひとり(OfflineRoom): 敵の巡回・復活・ドロップの寿命
         │                                       オンライン(RoomMirror): 敵の表示位置をサーバー位置へ寄せる
         ├─ RemotePlayers.Tick(dt)               他の人の表示位置を寄せる
         ├─ 死亡中なら復活待ち → RespawnPlayer
         ├─ events = Motor.Step(input, dt)       地上 / 空中 / はしご の 3 モード(下の状態機械)
         ├─ events.AttackStarted → World.FindAttackTarget → WorldAuthority.RequestAttack
         │      ひとり: その場で HP を減らし、結果のイベントを出す
         │      オンライン: 当たった敵とダメージを送るだけ(結果のイベントは通知を映したとき)
         │      結果のイベントのうち自分が倒したもの → メソ・経験値(GrantKillReward)
         ├─ events.EnteredPortal → 同じマップ内ならテレポート、別マップなら ChangeMap
         ├─ events.FellOutOfWorld → 落死
         ├─ 店を開いている間は入力を None に差し替える(その場に立ち止まる)
         ├─ PickupPressed → WorldAuthority.RequestPickup(ひとり: その場で拾う / オンライン: 拾いたいと送る)
         │      結果のイベントのうち自分が拾ったもの → 持ち物へ
         ├─ 敵と接触 (無敵中でなければ) → HP 減 / ノックバック / 0 で死亡
         └─ [オンライン] OnlineSession: MoveSender が「今送るべき」と言えば自分の MoveState を送る
    │
    └─ 見た目を同期  PlayerView / WorldViewSync(敵とドロップを突き合わせて作る・消す)/ RemotePlayersViewSync
         CameraRig.LateUpdate  (追従とワールド境界クランプ)
         HudView.OnGUI         (レベル・HP・経験値の棒・敵 HP・名札・接続状態・メッセージ)
```

## 4. 移動の状態機械 (CharacterMotor)

```
                 ↑ でつかむ (地上/空中)                 登り切る / 降り切る
        ┌────────────────────────────────┐    ┌───────────────────────────┐
        │                                ▼    │                           ▼
   ┌──────────┐   跳ぶ / 崖から出る / ↓+跳ぶ   ┌──────────┐  ←→+跳ぶ   ┌──────────┐
   │  Ground  │ ─────────────────────────▶   │   Air    │ ◀────────  │  Ladder  │
   │ 足場の上 │ ◀─────────────────────────   │  落下中  │            │ はしご   │
   └──────────┘        着地 (足場を横切った)  └──────────┘            └──────────┘
        │
        ├─ 歩く: X を進め Y = foothold.GetYAt(X)   (坂道は自動で追従)
        ├─ 端で GetNext(direction): 次の足場に乗り換え / 壁なら止まる / 無ければ崖 → Air
        ├─ 攻撃中は歩けない
        └─ ↓ でしゃがみ
```

メイプルストーリー準拠の割り切り: 空中では横方向を変えられない、崖から歩き出すと落ちる、↓+ジャンプで飛び降りる。

## 4b. 町・NPC・店の流れ

```
  マウス左クリック (GameBootstrap.HandlePointer)
    │  Camera.ScreenToWorldPoint → NpcView.Contains(world) で NPC を当てる
    ▼
  GameSimulation.Interact(npc)
    ├─ kind = talk  → MessageLog に一言
    └─ kind = shop  → IShopCatalog.Get(shopId) → ShopSession を作り ActiveShop に → ShopOpened
                        │
                        ▼
                  ShopWindow (uGUI, Screen Space - Camera)
                    左: 商品の行 (icon / 名前 / 価格)  → クリックで選択 → 「アイテムを買う」 → Simulation.Buy
                    右: 持ち物の行 (装備/消費/その他タブ) → クリックで選択 → 「アイテムを売る」 → Simulation.Sell
                    「店を出る」/ Esc → Simulation.CloseShop → ShopClosed → 窓を隠す

  マップ移動 (門のポータルで ↑)
    GameSimulation.EnterPortal → _mapLookup(TargetMapId) → ChangeMap(next, TargetPortalName)
      → 着いた門で ↑ を押しっぱなしでも引き返さないよう、ポータルの待ち時間を入れる
      → MapChanged(previous, next) → GameBootstrap が Map/World/Npc/Backdrop の GameObject を作り直す
```

## 4c. オンラインの流れ

```
  起動 ─ LoginWindow ─「オンラインで遊ぶ」
    │
    ▼
  MagicOnionConnection.ConnectAsync(接続先, 名前)
    ├─ GrpcChannel(YetAnotherHttpHandler, HTTP/2 平文)
    ├─ IAccountService.LoginAsync(名前) ──▶ PlayerId(ログインのたびに新しい番号)
    └─ IGameHub に接続。受信者は OnlineInbox
    │                                     失敗 → 窓に理由を出す(「ひとりで遊ぶ」も選べる)
    ▼
  GameSimulation(online: 接続, inbox: 接続.Inbox) ─ OnlineSession を作る
    └─ OnlineSession.JoinMap: 今のマップの RoomMirror を作り、JoinAsync(mapId, 自分, 今の位置) ─── スナップショット待ちになる

   Client A                          Server (Room = 1 マップ)                 Client B
   ────────                          ───────────────────────                ────────
   JoinAsync ────────────────────▶   参加                                   ◀── OnJoin(A)
             ◀── OnSnapshot(他の人・敵・落とし物)   ※ 届くまでは他の通知を捨てる
   MoveAsync(間引いて) ──────────▶   位置を保持 ──────────────────────────▶ OnMove(A)
   AttackAsync(敵, ダメージ) ────▶   HP を減らす ─▶ 全員に OnEnemyDamaged
                                     0 なら        ─▶ OnDropSpawn → OnEnemyDead(倒した人)
                                                      倒した人だけがメソを得る
   PickupAsync(落とし物) ────────▶   早い者勝ち ──▶ 全員に OnDropRemoved(拾った人)
                                                      拾った人だけ持ち物に入る
                                     Tick ────────▶ OnEnemyMove(敵の位置)/ OnEnemySpawn(復活)

  ポータルで別マップへ ─▶ OnlineSession.JoinMap で新しい mapId に JoinAsync し直す(新しい RoomMirror。前のマップの人は消し、スナップショット待ちに戻る)
  切断(サーバー停止・回線断) ─▶ 受け箱に「切れた」印 ─▶ 次の Step で GameSimulation が OnlineSession を手放してオフラインへ(敵は OfflineRoom が湧かせ直す)
```

受け箱を挟む理由: 通信の通知はどのスレッドで届くか分からない。受け箱は積むだけにして、
反映は必ず Step の頭(メインスレッド)で行う。テストでは受け箱に直接通知を積めば、通信なしで Core を確かめられる。

## 4d. 音の流れ

```
  GameSimulation のイベント(Core。音を知らない。規則は変えず、起きたことを知らせるだけ)
    Jumped / Attacked(振る・Hit なら当たる)/ EnemyKilled(自分が倒した)/ PlayerDamaged / PlayerDied
    ItemPickedUp / PortalUsed / ShopOpened / ShopClosed / ShopTraded(売り買いの結果)
    │
    ▼
  AudioDirector(Unity 層。GameBootstrap が作って GameSimulation に繋ぐ)
    ├─ きっかけ → SoundEffect(音の種類)
    ├─ AudioLibrary.Get(SoundEffect) ──▶ Resources/Terrace/Audio/Se/(種類 → ファイル名の対応はここだけ)
    │      ファイルが無ければ null → 黙って飛ばす
    └─ AudioSource.PlayOneShot(音量は AudioConfig)
         消音(M、GameBootstrap.Update で読む)なら鳴らさない。消音は PlayerPrefs に覚える

  耳(AudioListener)はカメラに 1 つ。シーンのカメラは ProjectSetup が、無ければ GameBootstrap が付ける
```

EnemyKilled は報酬を得るのと同じ所(GrantKillReward)で起きるので、オフラインでも、オンラインで撃破の通知の
倒した人が自分のときでも 1 体につき 1 度だけ。他の人が倒した敵や、他のプレイヤーの動作には音を付けない。
世界の権威の結果のイベントは GameSimulation だけが購読し、権威を差し替えるときに付け替える。音は GameSimulation のイベントだけを使うので、
世界が差し替わっても(WorldReplaced・マップ移動)購読し直す物は無い。

## 5. データの出どころ

```
  Terrace.MasterData/samples/csv/*.csv ──masterdata-build──▶ master.bytes, master.manifest.json ─┐
  Terrace.Map/maps/*.json (町・草原・砂の崖) ──────────────────────────────────────────────────┤ tools/sync-shared.ps1
  Terrace.Map/samples/sample_map.json ───────────────────────────────────────────────────────┘──▶ Assets/StreamingAssets/

  Terrace.Map/src/Terrace.Map/*.cs (MapSerializer.cs 以外) ──sync──▶ Assets/Terrace/Shared/Map/
  Terrace.MasterData/src/Shared/**/*.cs                    ──sync──▶ Assets/Terrace/Shared/MasterData/Tables/
  Terrace.Server/src/Terrace.Shared/**/*.cs                ──sync──▶ Assets/Terrace/Shared/Protocol/
```

敵の HP・攻撃力・ドロップは master.bytes の enemy テーブルから、アイテム名は item テーブルから引く。
master.bytes が無ければ FallbackEnemies(同じ値の埋め込み)で動く。サーバーも同じ CSV とマップ JSON を読むので、
オンラインでも敵の名前・大きさ・HP の見え方は揃う。

## 6. アセンブリと依存

```
  Terrace.Client.Tests.EditMode ──▶ Core, Unity, Map, Shared
  Terrace.Client.Tests.PlayMode ──▶ Core, Unity, Online, Map, Shared
  Terrace.Client.Editor         ──▶ Unity                         (Main シーン生成、Windows ビルド)
  Terrace.Client.Unity          ──▶ Core, Online, Map, MasterData, Shared, Unity.InputSystem, UGUI
  Terrace.Client.Online         ──▶ Core, Shared, YetAnotherHttpHandler, MagicOnion.Client(NuGet)
  Terrace.Client.Core           ──▶ Map, Shared                   (UnityEngine なし)
  Terrace.Shared                ──▶ MagicOnion.Abstractions, MessagePack(NuGet)
  Terrace.MasterData            ──▶ MasterMemory, MessagePack     (NuGetForUnity で復元)
  Terrace.Map                   ──▶ (なし)
```

MagicOnion のクライアントは実行時の動的生成で作る(エディタと Mono ビルドで動く)。IL2CPP にするときは事前生成が要る。

## 7. フォルダ

```
  Terrace.Client/
    Assets/
      Terrace/
        Shared/Map, Shared/MasterData, Shared/Protocol … 同期した共有コード (手で編集しない)
        Runtime/Core                    … ゲームの中身 (純 C#)。Online/ に通信の受け口
        Runtime/Online                  … MagicOnion で Server に繋ぐ
        Runtime/Unity                   … MonoBehaviour、読み込み、描画、ログイン窓、店の窓
        Editor                          … ProjectSetup (Main シーン生成、Windows ビルド)
      Tests/EditMode, Tests/PlayMode    … Unity Test Framework
      StreamingAssets/maps, master.bytes
      Scenes/Main.unity                 … ProjectSetup が生成(起動するとログイン窓)
      Packages/                         … NuGetForUnity の復元先
    tests/Terrace.Client.Core.Tests     … Core と EditMode テストの大半を Unity なしで回す .NET プロジェクト(CI 用)
    tools/
      sync-shared.ps1   共有コード・マップ・master.bytes を同期
      nuget-restore.ps1 NuGet 復元 + アナライザ meta の修正
      unity.ps1         -Setup / -EditMode / -PlayMode / -Build / -Open(-Mirror でエディタを開いたまま)
      e2e-online.ps1    サーバーを立てて PlayMode(2 人で繋ぐテストを含む)
```
