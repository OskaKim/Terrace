# Terrace — AI 向けの決まり

メイプルストーリーを手本にした 2D 横スクロール MORPG。1 つの git リポジトリに、4 つのプロジェクト(フォルダ)と、それらを束ねる文書を置く。

## 最初に読むもの

- 文書の目次と「どの事実がどこにあるか」: [docs/README.md](docs/README.md)
- 用語: [docs/glossary.md](docs/glossary.md)
- 現在地と未決事項: [docs/roadmap.md](docs/roadmap.md)

作業に関係する文書だけを読めばよい。目的別の索引は docs/README.md にある。

## フォルダと git の境界

```
Terrace/                 ← git リポジトリは、この 1 つだけ
  CLAUDE.md, README.md, docs/, tools/
  Terrace.MasterData/    ← CSV → master.bytes の変換 CLI、テーブル定義
  Terrace.Map/           ← マップの型・検証・マップ JSON
  Terrace.Server/        ← ゲームサーバー、通信の定義、テストクライアント
  Terrace.Client/        ← Unity のクライアント
```

- 4 プロジェクトは必ずこのフォルダの直下に兄弟として並ぶ(Server の csproj と Client の同期スクリプトが `..\` で相対参照する)
- 1 つの変更は、複数のプロジェクトと文書にまたがっても 1 つのコミット(1 つの PR)にまとめてよい。通信の定義を変えたら、Server・Client への複製・文書を同じ変更で直す
- 各プロジェクトにも CLAUDE.md と `.gitignore` がある(そのプロジェクト固有の決まり)
- 以前は 4 つが独立したリポジトリだった。まとめた理由は [ADR 0009](docs/decisions/0009-single-repository.md)

## タスクの進め方

仕事は GitHub の Issue で受ける。手順の正は [docs/guides/task-workflow.md](docs/guides/task-workflow.md)。

- 「#N をタスクにして」: 候補の Issue を人間と話して詰め、タスクの形に書き直す。推測で仕様を埋めない
- 「#N を実装して」: `pwsh tools/task.ps1 start N` で作業場(`../Terrace-wt/N`、ブランチ `task/N`)を切り、その中だけで作業する。`verify` で検証し、`pr` で PR を作って止まる
- 「マージしたから片付けて」「main を最新にして」: 本体のフォルダで `pwsh tools/task.ps1 sync`(`start` も最初に同じことをする)
- **マージしない。main に直接 push しない。** マージは人間だけが行う
- Issue の範囲の外は直さない。気づいたことは PR の「見てほしい所」か、新しい候補 Issue に書く
- `docs/roadmap.md` はタスクが求めるときだけ直す(並列のタスクがぶつかるため)

## 破ってはいけない決まり

1. **共有コードは C# 9 の範囲で書く。** 対象は `Terrace.Map/src/Terrace.Map`、`Terrace.MasterData/src/Shared`、`Terrace.Server/src/Terrace.Shared`。
   ブロック形式の namespace、`get; set;` プロパティ。file-scoped namespace・global using・record・`init`・raw string・collection expression・primary constructor は使わない(Unity の言語バージョンと IsExternalInit 不在のため)
2. **ゲーム規則は UnityEngine / MagicOnion に依存しない純 C# に置く。** Client は `Runtime/Core`(`noEngineReferences`)、Server は `Rooms/`。Unity 層と Hub は橋渡しだけ
3. **`Terrace.Client/Assets/Terrace/Shared/` は複製物。** 手で編集しない。元のプロジェクトを直して `Terrace.Client/tools/sync-shared.ps1` を実行する
4. **同じ規則が Client(`LocalWorld` / `GameSimulation`)と Server(`Room`)の 2 か所にある。** 片方を変えたら、もう片方と [docs/architecture/authority.md](docs/architecture/authority.md)、該当する `docs/spec/` を確認する
5. **マスタの追加は「Shared にクラスを 1 つ + 同名の CSV」だけで済ませる。** 一覧ファイルや設定ファイルを作らない
6. **マップ JSON は `MapData.Validate()` が空になる状態を保つ。**
7. **通信 DTO の MessagePack `[Key(n)]` は付け替えない。** 追加は末尾の番号で
8. **Unity のシーンは手で編集しない。** `ProjectSetup`(メニュー `Terrace > Create Main Scene` / `tools/unity.ps1 -Setup`)が生成する

## 検証のコマンド

| 対象 | コマンド(各プロジェクトのフォルダで) |
|---|---|
| タスクの作業場でまとめて | `pwsh tools/task.ps1 verify`(変えたプロジェクトに応じて下を選んで回す) |
| Terrace.MasterData / Terrace.Map / Terrace.Server | `dotnet test` |
| Terrace.Client の Core(Unity なし) | `dotnet test Terrace.Client/tests/Terrace.Client.Core.Tests` |
| Terrace.Server の通信経路 | サーバー起動 + テストクライアント 2 つ([docs/guides/run-and-test.md](docs/guides/run-and-test.md)) |
| Terrace.Client | `pwsh tools/unity.ps1 -EditMode` / `-PlayMode`(エディタを開いているときは `-Mirror` を付ける) |
| 文書のリンクと `sources` | `pwsh tools/check-docs.ps1`(このフォルダで) |
| Client の複製が元と同じか | `pwsh tools/check-sync.ps1`(`-MasterData` で master.bytes も) |

PR では GitHub Actions(`.github/workflows/ci.yml`)が、Unity を使わない検証をすべて回す。

## 文書を保つ決まり

- 文書・コメントは日本語、識別子は英語
- **1 つの事実は 1 か所にだけ書く。** 他の文書からはリンクする
- **数値パラメータは書かない。** 設定クラスのプロパティ名を書き、値はコードを正とする(例: `PlayerConfig.InvulnerableSeconds`)
- **クラス一覧・メソッド一覧・テスト件数は書かない。** すぐ古くなる
- `docs/` の各文書は先頭に front matter を持つ。`sources` は正となるファイル(このフォルダからの相対パス)、`status` は実装の状態

コードを変えたら、次の文書を合わせて直す。

| 変えたもの | 直す文書 |
|---|---|
| ゲームの規則(移動・戦闘・敵・経済・マップ移動・経験値・レベル) | `docs/spec/*.md` |
| 誰が状態を決めるか(権威) | `docs/architecture/authority.md` |
| マップ JSON の形 | `docs/contracts/map-format.md` と `map.schema.json` |
| CSV / テーブル / manifest の形 | `docs/contracts/masterdata.md` |
| IGameHub / Receiver / DTO | `docs/contracts/protocol.md` |
| プロジェクト間の依存・配り方・同期 | `docs/architecture/system.md`、`docs/guides/sync.md` |
| 後から覆すと困る判断 | `docs/decisions/` に ADR を足す |
| 段階の完了・未決事項の解消 | `docs/roadmap.md` |
