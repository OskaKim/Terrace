# Terrace.Client

Terrace の Unity クライアント(Unity 6000.3.6f1)。線分(フットホールド)の上を歩く 2D 横スクロール ARPG の、
「操作して動き、マップを移動し、敵を倒す」ところまでを動かす。ひとり(オフライン)でも、
Terrace.Server に繋いで複数人(オンライン)でも遊べる。

全体像は [ARCHITECTURE.md](ARCHITECTURE.md)(アスキーアートの設計図)。

## 遊び方

1. Unity Hub で `Terrace.Client` を開く(6000.3.6f1)。初回は NuGetForUnity が `Assets/packages.config` から NuGet パッケージを復元する
2. `Assets/Scenes/Main.unity` を開く。無ければメニュー `Terrace > Create Main Scene`
3. 再生すると名前と接続先を入れる窓が出る。「ひとりで遊ぶ」ならサーバー無しで始まる。「オンラインで遊ぶ」は次の節

| キー | 動作 |
|---|---|
| ← → | 移動(坂道は自動で追従。崖の端から歩き出すと落ちる) |
| ↑ | はしご・ロープをつかむ / ポータルに入る |
| ↓ | しゃがむ / はしごを降り始める |
| Space または Alt | ジャンプ(空中では方向を変えられない) |
| ↓ + ジャンプ | 下の足場へ飛び降りる |
| Ctrl または X | 攻撃(前方 1.6 ユニット、ダメージ 10) |
| Z | 足元のアイテムを拾う |
| マウス左クリック | NPC に話しかける。店の NPC(頭上にコインの看板)なら店の窓が開く |
| Esc | 店の窓を閉じる |

店の窓はメイプルストーリーの店を手本にしたもの。左が NPC の商品(行をクリックして選び「アイテムを買う」)、
右が自分の持ち物(装備 / 消費 / その他のタブ、行をクリックして選び「アイテムを売る」。売値は定価の半分)。
「ワンクリックで売却」を入れると持ち物の行をクリックしただけで売れる。店を開いている間は歩けない。
所持金(メソ)は左上に出る。始めは 3,000 メソで、敵を倒すとその敵の最大 HP と同じメソが手に入る。

メイプルストーリーの操作(Alt ジャンプ、Ctrl 攻撃、Z 拾う、↑ でポータル)を基本に、
Windows のエディタでは Alt 単押しがメニューにフォーカスを奪われることがあるので Space も割り当ててある。

敵に触れると HP が減って吹き飛ぶ(無敵 1 秒)。HP が 0 になると 2 秒後に `spawn` ポータルで復活する。
敵は倒すと `enemy.csv` の DropItemIds からドロップを落とし、湧き点の `respawnSeconds` 後に復活する。

## オンラインで遊ぶ(複数人)

1. サーバーを立てる(別のターミナルで。止めるときは Ctrl+C)

   ```bash
   cd ../Terrace.Server
   dotnet run --project src/Terrace.Server
   ```

2. 再生して、窓に名前とサーバー(既定 `http://localhost:5000`)を入れ「オンラインで遊ぶ」
3. もう 1 人を足す。どれか 1 つ
   - エディタの中で: メニュー `Window > Multiplayer > Multiplayer Play Mode` で仮想プレイヤーを有効にする(パッケージは導入済み)。
     仮想プレイヤーごとに同じ窓が出るので、それぞれ「オンラインで遊ぶ」
   - exe を並べて: `pwsh tools/unity.ps1 -Build` で `Build/Windows/Terrace.exe` を作り、複数起動する。
     引数 `-terraceName alice -terraceServer http://localhost:5000 -terraceOnline` を付けると窓を出さずに繋ぐ(`-terraceOffline` でひとり)
   - 別の PC から: サーバーの `appsettings.json` で `Terrace:ListenAnyIP` を `true` にし、Client の窓にサーバー PC の IP(`http://192.168.x.x:5000`)を入れる。
     Windows のファイアウォールで gRPC のポートを開ける

同じマップにいる人は、色違いのエイリアンと足元の名札で見える。右上に接続先と「このマップ N 人」が出る。

| 何を | 誰が決めるか |
|---|---|
| 自分の位置・動き、HP・被弾、メソ・持ち物・店 | 自分の Client(今は保存しない) |
| 敵の湧き・巡回・HP・撃破・復活、落とし物と拾う順番(早い者勝ち) | サーバー。全員に同じものが見える |
| 敵を倒した報酬(メソ) | 倒した人だけが得る |

サーバーが落ちたり回線が切れたりすると、その場でひとり遊びに切り替わって続けられる(敵は自分で湧かせ直す)。
名前は窓に入れた値が残り(PlayerPrefs)、同じ名前でも別人として参加できる。

## マップと町

`Assets/StreamingAssets/maps/` の JSON(形式は Terrace.Map と同じ)を起動時に全部読み、マップ ID で引く(`MapRegistry`)。
`GameBootstrap` の `Start Map Id`(既定 100 = 町)から始まり、門のポータルで ↑ を押すと隣のマップへ移る。
一度訪れたマップの敵の状態(倒した・復活待ち)はマップ ID ごとに覚えている。ファイル名が `sample` で始まる JSON は読まない。

```
   [草原 map 1] ←── west_gate / east_gate ──→ [テラスの町 map 100] ←── east_gate / west_gate ──→ [砂の崖 map 2]
```

| ファイル | 内容 |
|---|---|
| `town01.json` | テラスの町(map 100)。敵はいない。店の NPC が 2 人(メリー = 雑貨屋、ラク = 薬屋)と案内人。家 2 軒・柵・草木は `decorations` で置いた飾り。両端の門が狩場へ繋がる |
| `field01.json` | 草原(map 1)。坂道 2 本、上段の足場 3 枚と最上段 1 枚、ロープ 3 本とはしご 1 本、湧き点 5 か所(Slime ×2、Goblin ×2、Ghost ×1) |
| `field02.json` | 砂の崖(map 2)。テーマ `sand`(砂のタイルと砂漠の背景)。坂道から台地へ登り、ロープとはしごで 3 段。湧き点 6 か所 |
| `sample_map.json` | Terrace.Map のサンプル(同期で複製、一覧には含めない) |

マップの `theme`(grass / stone / sand / castle / snow)で地面のタイルと背景が変わる。
`npcs` に置いた NPC は `kind` が `shop` なら `shopId` の店(今は仮で `general` = 全品、`potion` = 消費のみ、`equip` = 装備のみ)を開く。
`decorations` は素材フォルダからの相対パス(`Buildings/houseBeige`、`Items/bush` など)で絵を置く。当たり判定は無い。

```
 草原 (map 1)
 y
 13                       ==== #20 ====
  9              ==== #11 ====  | rope4
  6   == #10 ==     | ladder2      == #12 ==
  4            /‾‾‾‾ #3 ‾‾‾‾\        | rope3
  0  [spawn]==#1==/  #2  #4  \==== #5 ======[east_gate→町]
       | rope1
      x: 0    10   20   28    44 52       76
```

## テスト

Unity を開かずに、バッチで回せる。

```bash
pwsh tools/unity.ps1 -EditMode     # 純 C# の移動・戦闘・敵・店・オンラインの反映(通信は偽物)、アセット読み込み
pwsh tools/unity.ps1 -PlayMode     # 実際に起動して歩き、敵を倒し、拾う。ログイン窓と接続失敗も。Logs/smoke.png に画面を保存
pwsh tools/e2e-online.ps1          # サーバーを立てて PlayMode を走らせる。2 人で繋いで互いに見え、攻撃が届くかまで確かめる(Logs/online.png)
pwsh tools/unity.ps1 -Setup        # Main シーンの生成(コンパイル確認も兼ねる)
pwsh tools/unity.ps1 -Build        # Windows 版を Build/Windows/Terrace.exe に書き出す
pwsh tools/unity.ps1 -Open         # エディタで開く
```

結果は `Logs/*-results.xml` と `Logs/*.log`。

## 他のリポジトリとの同期

共有コードと成果物は手で編集せず、同期スクリプトで複製する。

```bash
pwsh tools/sync-shared.ps1        # Map / MasterData / 通信定義の .cs、maps/*.json、master.bytes(masterdata-build を実行)
pwsh tools/nuget-restore.ps1      # NuGet パッケージの復元(NuGetForUnity CLI)+ アナライザ meta の修正
```

| 元 | 先 |
|---|---|
| `../Terrace.Map/src/Terrace.Map/*.cs`(MapSerializer.cs 以外) | `Assets/Terrace/Shared/Map/` |
| `../Terrace.MasterData/src/Shared/**/*.cs` | `Assets/Terrace/Shared/MasterData/Tables/` |
| `../Terrace.Server/src/Terrace.Shared/**/*.cs`(通信の定義) | `Assets/Terrace/Shared/Protocol/` |
| `masterdata-build` の出力 | `Assets/StreamingAssets/master.bytes`, `master.manifest.json` |
| `../Terrace.Map/maps/*.json`、`../Terrace.Map/samples/sample_map.json` | `Assets/StreamingAssets/maps/` |

## 依存

| 種別 | 名前 | 用途 |
|---|---|---|
| UPM | com.unity.inputsystem | キーボード入力 |
| UPM | com.unity.nuget.newtonsoft-json | マップ JSON の読み込み |
| UPM | com.unity.test-framework | テスト |
| UPM | NuGetForUnity(git, v4.5.0) | NuGet パッケージの管理 |
| UPM | YetAnotherHttpHandler(git, 1.11.5) | Unity から HTTP/2(h2c)で gRPC を話す(ネイティブ DLL 同梱) |
| UPM | com.unity.multiplayer.playmode | エディタの中で仮想プレイヤーを並べて試す |
| NuGet | MasterMemory 3.0.4 / MessagePack 3.1.8 | master.bytes の読み込み(Source Generator が MemoryDatabase を生成) |
| NuGet | MagicOnion.Client 7.10.2(と Grpc.Net.Client ほか依存一式) | サーバーとの通信。依存は `packages.config` に並べてある(NuGetForUnity の復元は依存を辿らない) |

NuGetForUnity の CLI は .NET 9 向けなので、.NET 10 で動かすときは `DOTNET_ROLL_FORWARD=Major` を付ける(`tools/nuget-restore.ps1` が付ける)。

## 見た目(素材)

[Kenney Platformer Art Deluxe](https://kenney.nl/assets/platformer-art-deluxe)(CC0)から使う絵だけを
`Assets/Resources/Terrace/Art/Kenney/` に置いてある(出典と一覧は同フォルダの README.md、ライセンスは LICENSE.txt)。

| 対象 | 絵 |
|---|---|
| 主人公 | 緑のエイリアン p1。立ち / 歩き 11 コマ / ジャンプ / しゃがみ / はしご登り 2 コマ / やられ |
| NPC | 色違いのエイリアン(alienPink / alienBlue / alienYellow / alienBeige)。店の NPC は頭上にコインの看板 |
| Slime / Goblin / Ghost | slimeGreen / spider(Goblin の代役)/ ghost。歩き 2 コマ、被弾、死亡 |
| 足場 | テーマのタイル(grassMid / sandMid など)を線分に沿って敷き詰める(斜面は回転)。地面の足場は下に詰め物を敷く |
| はしご / ロープ | ladder_mid / ropeVertical を縦に敷き詰める |
| ポータル | door_open(2 タイル)。出現地点は描かない |
| 町の飾り | Buildings 拡張の家(壁・屋根・扉・窓・看板)、柵、草木、丘 |
| ドロップ | gem(赤 = Potion、青 = Sword、黄 = Shield、緑 = Herb)、coinGold(Ore)、star(その他) |
| 背景 / HUD | テーマごとの背景(bg_grasslands / bg_desert / bg_castle、横方向に視差)、雲、ハート、顔、コイン |
| 店の窓 | Kenney UI Pack(CC0)のパネル・ボタン・チェックを 9 スライスで使う。文字は Unity 内蔵フォント |

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

## テストの回し方(エディタで開いたまま)

エディタで同じプロジェクトを開いていると、バッチの Unity は起動できない(同じプロジェクトは同時に 1 つ)。
そのときは `-Mirror` を付ける。プロジェクトを一時フォルダへ複製してそちらで走らせ、結果とスクリーンショットを `Logs/mirror/` に写す。

```bash
pwsh tools/unity.ps1 -EditMode -Mirror
pwsh tools/unity.ps1 -PlayMode -Mirror
```

## やっていないこと(次の段階)

- 店の品揃えをマスタ(shop.csv)にする。今は `PlaceholderShopCatalog` の仮(全品 / 消費のみ / 装備のみ)
- アイテムを使う・装備する、経験値・レベル・スキル
- IL2CPP ビルド(MessagePack / MasterMemory の生成済みリゾルバ登録と、MagicOnion のクライアント事前生成が必要。
  今のオンライン接続は MagicOnion の動的生成を使うので、エディタと Mono ビルドでだけ動く)
- 残りの一覧と未決事項は `../docs/roadmap.md`
