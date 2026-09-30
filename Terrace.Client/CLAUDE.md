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
| `Assets/StreamingAssets` | マップ JSON、master.bytes |

## 決まり

- ゲームの規則は `Runtime/Core` に書く。UnityEngine を参照しない。Unity 層は入力を `InputFrame` にして渡し、状態を読んで描くだけ
- Core は通信の実装を知らない。送るのは `IOnlineChannel`、受けるのは `OnlineInbox`(通知を積むだけ。反映は `GameSimulation.Step` の頭)。サーバーの通知を映す規則は `GameSimulation.Online.cs` と `LocalWorld` の `Apply*`
- 通信の定義(`IGameHub` / DTO)を変えたら、Server 側を直してから `pwsh tools/sync-shared.ps1` で写し、`OnlineInbox` と `GameSimulation.Online.cs` を合わせる
- NuGet パッケージを足すときは依存も `Assets/packages.config` に並べる(NuGetForUnity の復元は依存を辿らない)。その後 `pwsh tools/nuget-restore.ps1`
- `Assets/Terrace/Shared/` は手で編集しない。元のプロジェクトを直して `pwsh tools/sync-shared.ps1`
- Unity のバージョンに合わせて C# 9 の範囲で書く
- シーンは手で編集しない。`ProjectSetup` が生成する(`pwsh tools/unity.ps1 -Setup`)
- 見た目は `Assets/Resources/Terrace/Art/Kenney/` の CC0 素材から読む。素材が無くても動くよう、コード生成スプライトへの逃げ道を残す
- `LocalWorld` は Server の `Room` と同じ規則。敵とドロップの規則を変えたら `../docs/spec/enemy-drop.md` を直し、Server 側の対応も確認する
- 店の品揃えは `PlaceholderShopCatalog` の仮。マスタ化するまで ShopId を増やすときはここを直す

## 検証

```bash
pwsh tools/unity.ps1 -EditMode      # エディタで開いているときは -Mirror を付ける
pwsh tools/unity.ps1 -PlayMode      # 2 人で繋ぐテストは省略される
pwsh tools/e2e-online.ps1           # サーバーを立てて PlayMode。2 人で繋ぐテストまで走る
```

結果は `Logs/*-results.xml`、`Logs/*.log`、`Logs/smoke.png`、`Logs/online.png`(`-Mirror` のときは `Logs/mirror/`)。サーバーのログは `Logs/e2e-server.log`。

## 変えたら

- 移動・戦闘・敵・経済・マップ移動の規則 → `../docs/spec/` の該当文書
- 層やアセンブリの構成 → `ARCHITECTURE.md`
- 操作・素材 → `README.md`
