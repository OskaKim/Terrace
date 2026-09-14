# Terrace.Client

Terrace の Unity クライアント(Unity 6000.3.6f1)。線分(フットホールド)の上を歩く 2D 横スクロール ARPG の、
「操作して動き、マップを移動し、敵を倒す」ところまでをオフラインで動かす。

全体像は [ARCHITECTURE.md](ARCHITECTURE.md)(アスキーアートの設計図)。

## 遊び方

1. Unity Hub で `Terrace.Client` を開く(6000.3.6f1)。初回は NuGetForUnity が `Assets/packages.config` から NuGet パッケージを復元する
2. `Assets/Scenes/Main.unity` を開く。無ければメニュー `Terrace > Create Main Scene`
3. 再生する

| キー | 動作 |
|---|---|
| ← → | 移動(坂道は自動で追従。崖の端から歩き出すと落ちる) |
| ↑ | はしご・ロープをつかむ / ポータルに入る |
| ↓ | しゃがむ / はしごを降り始める |
| Space または Alt | ジャンプ(空中では方向を変えられない) |
| ↓ + ジャンプ | 下の足場へ飛び降りる |
| Ctrl または X | 攻撃(前方 1.6 ユニット、ダメージ 10) |
| Z | 足元のアイテムを拾う |

メイプルストーリーの操作(Alt ジャンプ、Ctrl 攻撃、Z 拾う、↑ でポータル)を基本に、
Windows のエディタでは Alt 単押しがメニューにフォーカスを奪われることがあるので Space も割り当ててある。

敵に触れると HP が減って吹き飛ぶ(無敵 1 秒)。HP が 0 になると 2 秒後に `spawn` ポータルで復活する。
敵は倒すと `enemy.csv` の DropItemIds からドロップを落とし、湧き点の `respawnSeconds` 後に復活する。

## マップ

`Assets/StreamingAssets/maps/` の JSON(形式は Terrace.Map と同じ)。`GameBootstrap` の `Map File Name` で切り替える。

- `field01.json` … 遊び用。坂道 2 本、上段の足場 3 枚と最上段 1 枚、ロープ 3 本とはしご 1 本、
  同じマップ内で行き来するポータル `spawn` ⇔ `top`、湧き点 5 か所(Slime ×2、Goblin ×2、Ghost ×1)
- `sample_map.json` … Terrace.Map のサンプル(同期で複製)

```
 y
 13                       ==== #20 (top) ====   [top]
  9              ==== #11 ====  | rope4
  6   == #10 ==     | ladder2      == #12 ==
  4            /‾‾‾‾ #3 ‾‾‾‾\        | rope3
  0  [spawn]==#1==/  #2  #4  \==== #5 =========
       | rope1
      x: 0    10   20   28    44 52       80
```

## テスト

Unity を開かずに、バッチで回せる。

```bash
pwsh tools/unity.ps1 -EditMode     # 純 C# の移動・戦闘・敵・アセット読み込み
pwsh tools/unity.ps1 -PlayMode     # 実際に起動して歩き、敵を倒し、拾う。Logs/smoke.png に画面を保存
pwsh tools/unity.ps1 -Setup        # Main シーンの生成(コンパイル確認も兼ねる)
pwsh tools/unity.ps1 -Open         # エディタで開く
```

結果は `Logs/*-results.xml` と `Logs/*.log`。

## 他のリポジトリとの同期

共有コードと成果物は手で編集せず、同期スクリプトで複製する。

```bash
pwsh tools/sync-shared.ps1        # Map / MasterData の .cs、sample_map.json、master.bytes(masterdata-build を実行)
pwsh tools/nuget-restore.ps1      # NuGet パッケージの復元(NuGetForUnity CLI)+ アナライザ meta の修正
```

| 元 | 先 |
|---|---|
| `../Terrace.Map/src/Terrace.Map/*.cs`(MapSerializer.cs 以外) | `Assets/Terrace/Shared/Map/` |
| `../Terrace.MasterData/src/Shared/**/*.cs` | `Assets/Terrace/Shared/MasterData/Tables/` |
| `masterdata-build` の出力 | `Assets/StreamingAssets/master.bytes`, `master.manifest.json` |
| `../Terrace.Map/samples/sample_map.json` | `Assets/StreamingAssets/maps/` |

## 依存

| 種別 | 名前 | 用途 |
|---|---|---|
| UPM | com.unity.inputsystem | キーボード入力 |
| UPM | com.unity.nuget.newtonsoft-json | マップ JSON の読み込み |
| UPM | com.unity.test-framework | テスト |
| UPM | NuGetForUnity(git, v4.5.0) | NuGet パッケージの管理 |
| NuGet | MasterMemory 3.0.4 / MessagePack 3.1.8 | master.bytes の読み込み(Source Generator が MemoryDatabase を生成) |

NuGetForUnity の CLI は .NET 9 向けなので、.NET 10 で動かすときは `DOTNET_ROLL_FORWARD=Major` を付ける(`tools/nuget-restore.ps1` が付ける)。

## 見た目(素材)

[Kenney Platformer Art Deluxe](https://kenney.nl/assets/platformer-art-deluxe)(CC0)から使う絵だけを
`Assets/Resources/Terrace/Art/Kenney/` に置いてある(出典と一覧は同フォルダの README.md、ライセンスは LICENSE.txt)。

| 対象 | 絵 |
|---|---|
| 主人公 | 緑のエイリアン p1。立ち / 歩き 11 コマ / ジャンプ / しゃがみ / はしご登り 2 コマ / やられ |
| Slime / Goblin / Ghost | slimeGreen / spider(Goblin の代役)/ ghost。歩き 2 コマ、被弾、死亡 |
| 足場 | grassMid を線分に沿って敷き詰める(斜面は回転)。地面の足場は下に grassCenter を詰める |
| はしご / ロープ | ladder_mid / ropeVertical を縦に敷き詰める |
| ポータル | door_open(2 タイル) |
| ドロップ | gem(赤 = Potion、青 = Sword、黄 = Shield、緑 = Herb)、coinGold(Ore)、star(その他) |
| 背景 / HUD | bg_grasslands(横方向に視差)、雲、ハート、顔 |

取り込み設定(Sprite、70 px = 1 unit、足元中央が原点、FullRect)は `Assets/Terrace/Editor/ArtImportProcessor.cs` が自動で付けるので、
画像を同じフォルダに足すだけで使える。素材が無い環境ではコード生成スプライト(四角と丸)で動く。
こま送りは Animator アセットを使わず `SpriteAnimator` が回す。

## 設計のポイント

- ゲームの中身(`Assets/Terrace/Runtime/Core`)は UnityEngine を参照しない純 C#。EditMode テストで秒速に検証でき、
  後でサーバー権威に移すときもそのまま使える
- Unity 層は「入力を InputFrame にして渡す」「状態を読んで描く」だけ
- 見た目は Kenney の CC0 素材を Resources から読む。無ければコード生成スプライトで動く
- シーンは `ProjectSetup` がコードで生成する(手作業のシーン編集なし)
- 共有コードは C# 9 の範囲(Unity の言語バージョン)

## やっていないこと(次の段階)

- サーバー接続(MagicOnion / YetAnotherHttpHandler)。`GameSimulation` の LocalWorld をサーバーからの通知に差し替える
- マップ間移動(ポータルの TargetMapId が別マップのものはメッセージだけ)
- 経験値・レベル・スキル・インベントリ UI
- IL2CPP ビルド(MessagePack / MasterMemory の生成済みリゾルバ登録が必要)
