# Terrace.Client — AI 向けの決まり

全体の決まりは親フォルダの `../CLAUDE.md`。Client の層と 1 フレームの流れは `ARCHITECTURE.md`、ゲームの規則は `../docs/spec/`。

## このプロジェクト

Unity 6000.3.6f1 のクライアント。ひとり(オフライン)でも、Terrace.Server に繋いで複数人(オンライン)でも遊べる。

| フォルダ | 中身 |
|---|---|
| `Assets/Terrace/Runtime/Core` | ゲームの規則(純 C#、`noEngineReferences`)。`Online/` に通信の受け口(送る口と受け箱) |
| `Assets/Terrace/Runtime/Online` | MagicOnion + YetAnotherHttpHandler で Server に繋ぐ(`MagicOnionConnection`) |
| `Assets/Terrace/Runtime/Unity` | MonoBehaviour、読み込み、描画、入力、UI(ログイン窓・店の窓) |
| `Assets/Terrace/Editor` | シーン生成(`ProjectSetup`)、素材の取り込み設定 |
| `Assets/Terrace/Shared` | 他のプロジェクトからの複製(Map / MasterData / Protocol。`MasterData/AssemblyInfo.cs` と各 asmdef・csc.rsp だけは Client のもの) |
| `Assets/Tests/EditMode`、`PlayMode` | テスト |
| `tests/Terrace.Client.Core.Tests` | Core と Unity に依存しない EditMode テストを .NET で回すプロジェクト(ソースは Assets から取り込むだけ) |
| `Assets/StreamingAssets` | マップ JSON、master.bytes |

## 決まり

- ゲームの規則は `Runtime/Core` に書く。UnityEngine を参照しない。Unity 層は入力を `InputFrame` にして渡し、状態を読んで描くだけ
- EditMode テストは、Unity の API を使わずに書けるものは使わずに書く(.NET の `tests/Terrace.Client.Core.Tests` と CI でも回るように)。Unity の API が要るテストは `tests/Terrace.Client.Core.Tests/Terrace.Client.Core.Tests.csproj` の `Exclude` に足す
- Core は通信の実装を知らない。送るのは `IOnlineChannel`、受けるのは `OnlineInbox`(通知を積むだけ。反映は `GameSimulation.Step` の頭)。オンラインの接続(参加・移動の送信・通知の受け取り・スナップショット待ち・他のプレイヤー)は `OnlineSession` にまとめ、`GameSimulation` は「オンラインなら `OnlineSession` がある」ことだけを知る。敵と落とし物の通知を映す規則は `RoomMirror` の `Apply*`
- 通信の定義(`IGameHub` / DTO)を変えたら、Server 側を直してから `pwsh tools/sync-shared.ps1` で写し、`OnlineInbox` と `OnlineSession`(敵と落とし物は `RoomMirror`)を合わせる
- NuGet パッケージを足すときは依存も `Assets/packages.config` に並べる(NuGetForUnity の復元は依存を辿らない)。その後 `pwsh tools/nuget-restore.ps1`
- `Assets/Terrace/Shared/` は手で編集しない。元のプロジェクトを直して `pwsh tools/sync-shared.ps1`
- Unity のバージョンに合わせて C# 9 の範囲で書く
- シーンは手で編集しない。`ProjectSetup` が生成する(`pwsh tools/unity.ps1 -Setup`)
- 見た目は `Assets/Resources/Terrace/Art/Kenney/` の CC0 素材から読む。素材が無くても動くよう、コード生成スプライトへの逃げ道を残す
- 音は Core に持ち込まない。Core はイベントで知らせるだけにし、Unity 層の `AudioDirector` が鳴らす。ファイルは `Assets/Resources/Terrace/Audio/` に置く。効果音の対応は `AudioLibrary` にだけ書き、BGM はマップ JSON の `bgm` が決める。音が無くても黙って動くようにする
- 敵と落とし物の世界は、入れ物(`WorldState`)と誰が決めるか(`IWorldAuthority`: オフラインは `OfflineRoom`、オンラインは `RoomMirror`)に分かれる。攻撃と拾うは権威に頼むだけで、報酬や持ち物は権威の結果のイベントで動かす(オンラインかどうかで分けない)
- `OfflineRoom` は Server の `Room` と同じ規則。敵とドロップの規則を変えたら `../docs/spec/enemy-drop.md` を直し、Server 側の対応も確認する
- 店の品揃えは `PlaceholderShopCatalog` の仮。マスタ化するまで ShopId を増やすときはここを直す

## 名前の付け方

クラス名の接尾辞で役目を示す。下の一覧にある役は、その接尾辞を必ず使う。一覧に無い役(`CharacterMotor`・`CameraRig`・`GameBootstrap` など)には、無理に接尾辞を付けない。

| 接尾辞 | 役目 | 置き場所 | 例 |
|---|---|---|---|
| `~View` | 見せるもの(MonoBehaviour)。MVP の窓では `I~View` を実装し、言われた通りに描くだけ | Unity | `PlayerView`、`HudView` |
| `I~View` | Presenter から見た View の口 | Presentation(`Runtime/Presentation`。窓の MVP 化で作る) | `IShopView` |
| `~Presenter` | 窓に何を出すかを決め、押されたら規則の側を操作する | Presentation | `ShopPresenter` |
| `~System` | 規則の務め 1 つ(`GameSimulation` から割る係) | Core | `CombatSystem`、`TravelSystem` |
| `~State` | 変わる状態 | Core | `PlayerState` |
| `~Config` | 数値の設定 | Core / Unity | `MotorConfig`、`AudioConfig` |
| `~Definition` | マスタから作る定義 | Core | `EnemyDefinition`、`ShopDefinition` |
| `~Catalog` | 規則が引く台帳 | Core | `IItemCatalog`、`IShopCatalog` |
| `~Library` | 素材の台帳 | Unity | `ArtLibrary`、`AudioLibrary` |
| `~Repository` / `~Registry` / `~Loader` | データを読む | Unity | `MasterDataRepository`、`MapRegistry`、`MapLoader` |
| `~Source` | 入力の供給元 | Unity | `KeyboardInputSource` |
| `~Factory` | 作る | Core / Unity | `SpriteFactory` |

使わない接尾辞:

- `~Manager`: 何でも入る袋になる
- `~Controller`: MVP では Controller の仕事を Presenter と `~Source` に分ける
- `~Model`: MVP の Model は役であって、クラスの種類ではない。規則の側のクラスを窓の都合で名付けない
- `~Window`: 窓は `~View`

付け替えの決まり:

- 名前の付け替えは、タスクで触ったクラスから行う。付け替えだけの PR は作らない(並列のタスクとすべてぶつかるため)
- 新しく作るクラスは最初から一覧に従う

## 検証

```bash
dotnet test tests/Terrace.Client.Core.Tests   # Unity なしで Core を試す(CI でも回る)
pwsh tools/unity.ps1 -EditMode      # エディタで開いているときは -Mirror を付ける
pwsh tools/unity.ps1 -PlayMode      # 2 人で繋ぐテストは省略される
pwsh tools/e2e-online.ps1           # サーバーを立てて PlayMode。2 人で繋ぐテストまで走る
```

結果は `Logs/*-results.xml`、`Logs/*.log`、`Logs/smoke.png`、`Logs/online.png`(`-Mirror` のときは `Logs/mirror/`)。サーバーのログは `Logs/e2e-server.log`。

## 変えたら

- 移動・戦闘・敵・経済・マップ移動の規則 → `../docs/spec/` の該当文書
- 層やアセンブリの構成 → `ARCHITECTURE.md`
- 操作・素材 → `README.md`
