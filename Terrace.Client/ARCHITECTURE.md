# Terrace.Client 全体設計図

コードを全部読まなくても全体像がつかめるように、構造を図にまとめたもの。
詳しい使い方は [README.md](README.md)、4 リポジトリ全体の図は [../ARCHITECTURE.md](../ARCHITECTURE.md)。

## 1. ひとことで

「線分(フットホールド)の上を歩く 2D 横スクロール ARPG」の Unity クライアント。
ゲームの中身は **Unity に依存しない純 C#(Core)** に置き、Unity 側は **入力を渡して結果を描くだけ** にしてある。
だからロジックは EditMode テストで秒速に検証でき、後でサーバー権威に移すときも Core をそのまま使い回せる。

## 2. 層の図

```
                    ┌────────────────────────────────────────────────────────────┐
   キーボード ─────▶│  Unity 層  (Assets/Terrace/Runtime/Unity  Terrace.Client.Unity)│
   (Input System)   │                                                            │
                    │  GameBootstrap ── 起動役。読み込み → 組み立て → 毎フレーム Step │
                    │     │                                                      │
                    │     ├─ MapLoader(Newtonsoft) ──▶ StreamingAssets/maps/*.json│
                    │     ├─ MasterDataRepository ──▶ StreamingAssets/master.bytes│
                    │     │        (MasterMemory / MemoryDatabase)               │
                    │     ├─ KeyboardInputSource ──▶ InputFrame                  │
                    │     ├─ ArtLibrary(Resources) ──▶ Kenney の CC0 素材         │
                    │     │        無ければ SpriteFactory のコード生成スプライト     │
                    │     └─ 見た目: MapView / PlayerView / EnemyView / DropView  │
                    │                SpriteAnimator(こま送り) / ParallaxBackdrop  │
                    │                CameraRig / HudView(IMGUI, ハート)           │
                    └───────────────┬──────────────────────────▲─────────────────┘
                       InputFrame + dt                          │ 状態を読んで描く
                                    ▼                           │
                    ┌────────────────────────────────────────────────────────────┐
                    │  Core 層  (Assets/Terrace/Runtime/Core  Terrace.Client.Core)  │
                    │  UnityEngine 参照なし (noEngineReferences)                   │
                    │                                                            │
                    │   GameSimulation ─── 1 セッションのまとめ役                   │
                    │      ├─ CharacterMotor   歩く/跳ぶ/落ちる/はしご/ポータル      │
                    │      ├─ LocalWorld       敵の巡回・HP・死亡・復活・ドロップ     │
                    │      ├─ PlayerState      HP・キル数・持ち物・死亡と復活         │
                    │      └─ MessageLog       画面に流す文言                       │
                    └───────────────┬────────────────────────────────────────────┘
                                    │ 問い合わせ (FindFootholdBelow / GetYAt / GetNext ...)
                                    ▼
                    ┌────────────────────────────────────────────────────────────┐
                    │  共有層  (Assets/Terrace/Shared  … 他リポジトリからの複製)      │
                    │   Terrace.Map        フットホールド/はしご/ポータル/検証        │
                    │   Terrace.MasterData Item / Quest / Enemy のテーブル定義        │
                    │                      (+ Source Generator が MemoryDatabase 生成)│
                    └────────────────────────────────────────────────────────────┘
```

矢印は「誰が誰を知っているか」。上の層は下の層を知り、下の層は上を知らない。

## 3. 1 フレームの流れ

```
  Update()                                      (GameBootstrap)
    │
    ├─ input = InputSource.Read()               ←→↑↓ / Jump / Attack / Pickup を InputFrame に
    ├─ dt    = min(Time.deltaTime, 0.05)
    │
    └─ Simulation.Step(input, dt)               (GameSimulation)
         ├─ World.Tick(dt)                       敵の巡回・復活タイマー・ドロップの寿命
         ├─ 死亡中なら復活待ち → RespawnPlayer
         ├─ events = Motor.Step(input, dt)       ┐ 地上 / 空中 / はしご の 3 モード
         │     Ground: 歩く・坂・崖・壁・跳ぶ・   │   (下の「移動の状態機械」)
         │             ↓+跳ぶで飛び降り・↑でつかむ/入る
         │     Air   : 重力・着地判定・空中で↑つかむ
         │     Ladder: 登り降り・端で乗る/落ちる・←→+跳ぶで飛び降り
         ├─ events.AttackStarted → World.PlayerAttack(前方 1.6, 高さ 1.2, ダメージ 10)
         │                          → 倒したら Kills++ / ドロップ生成 / メッセージ
         ├─ events.EnteredPortal → 同じマップ内ならテレポート
         ├─ events.FellOutOfWorld → 落死
         ├─ PickupPressed → World.TryPickup → Inventory
         └─ 敵と接触 (無敵中でなければ) → HP 減 / ノックバック / 無敵 1 秒 / 0 で死亡
    │
    └─ 見た目を同期  PlayerView.Sync / WorldViewSync.Sync (敵・ドロップ)
         CameraRig.LateUpdate  (追従とワールド境界クランプ)
         HudView.OnGUI         (HP バー・敵 HP・メッセージ・操作説明)
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
        ├─ 攻撃中 (0.35 s) は歩けない
        └─ ↓ でしゃがみ
```

メイプルストーリー準拠の割り切り: 空中では横方向を変えられない、崖から歩き出すと落ちる、↓+ジャンプで飛び降りる。

## 5. データの出どころ

```
  Terrace.MasterData/samples/csv/*.csv ──masterdata-build──▶ master.bytes ─┐
  (item / quest / enemy)                                    master.manifest.json │ tools/sync-shared.ps1
                                                                                ├──▶ Assets/StreamingAssets/
  Terrace.Map/samples/sample_map.json ─────────────────────────────────────────┘        maps/*.json, master.bytes
  Assets/StreamingAssets/maps/field01.json  (このリポジトリで作った遊び用マップ)

  Terrace.Map/src/Terrace.Map/*.cs (MapSerializer.cs 以外) ──sync──▶ Assets/Terrace/Shared/Map/
  Terrace.MasterData/src/Shared/**/*.cs                    ──sync──▶ Assets/Terrace/Shared/MasterData/Tables/
```

敵の HP・攻撃力・ドロップは master.bytes の enemy テーブルから、アイテム名は item テーブルから引く。
master.bytes が無ければ FallbackEnemies(同じ値の埋め込み)で動く。

## 6. アセンブリと依存

```
  Terrace.Client.Tests.EditMode ──▶ Terrace.Client.Core, Terrace.Client.Unity, Terrace.Map
  Terrace.Client.Tests.PlayMode ──▶ Terrace.Client.Unity, Terrace.Client.Core
  Terrace.Client.Editor         ──▶ Terrace.Client.Unity          (Main シーン生成)
  Terrace.Client.Unity          ──▶ Core, Map, MasterData, Unity.InputSystem, Newtonsoft.Json
  Terrace.Client.Core           ──▶ Terrace.Map                   (UnityEngine なし)
  Terrace.MasterData            ──▶ MasterMemory, MessagePack     (NuGetForUnity で復元)
  Terrace.Map                   ──▶ (なし)
```

## 7. フォルダ

```
  Terrace.Client/
    Assets/
      Terrace/
        Shared/Map, Shared/MasterData   … 同期した共有コード (手で編集しない)
        Runtime/Core                    … ゲームの中身 (純 C#)
        Runtime/Unity                   … MonoBehaviour と読み込み
        Editor                          … ProjectSetup (Main シーン生成)
      Tests/EditMode, Tests/PlayMode    … Unity Test Framework
      StreamingAssets/maps, master.bytes
      Scenes/Main.unity                 … ProjectSetup が生成
      Packages/                         … NuGetForUnity の復元先 (git 管理外)
    tools/
      sync-shared.ps1   共有コードと master.bytes を同期
      nuget-restore.ps1 NuGet 復元 + アナライザ meta の修正
      unity.ps1         -Setup / -EditMode / -PlayMode / -Open
```
