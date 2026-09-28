# Terrace — AI 向けの決まり

メイプルストーリーを手本にした 2D 横スクロール MORPG。4 つの独立した git リポジトリと、それらを束ねる文書(このフォルダ)からなる。

## 最初に読むもの

- 文書の目次と「どの事実がどこにあるか」: [docs/README.md](docs/README.md)
- 用語: [docs/glossary.md](docs/glossary.md)
- 現在地と未決事項: [docs/roadmap.md](docs/roadmap.md)

作業に関係する文書だけを読めばよい。目的別の索引は docs/README.md にある。

## フォルダと git の境界

```
Terrace/                 ← この git は文書だけを管理する(4 リポジトリは .gitignore で除外)
  CLAUDE.md, README.md, docs/, tools/
  Terrace.MasterData/    ← 独立した git
  Terrace.Map/           ← 独立した git
  Terrace.Server/        ← 独立した git
  Terrace.Client/        ← 独立した git
```

- 4 リポジトリは必ずこのフォルダの直下に兄弟として並ぶ(Server の csproj と Client の同期スクリプトが `..\` で相対参照する)
- コミットは変更したファイルが属するリポジトリで行う。`git` コマンドはそのリポジトリの中で実行する
- 各リポジトリにも CLAUDE.md がある(そのリポジトリ固有の決まり)
- GitHub でも同じ 5 つのリポジトリに分かれる。取ってくるのは `tools/clone-all.ps1`、上げるのは `tools/github-publish.ps1`

## 破ってはいけない決まり

1. **共有コードは C# 9 の範囲で書く。** 対象は `Terrace.Map/src/Terrace.Map`、`Terrace.MasterData/src/Shared`、`Terrace.Server/src/Terrace.Shared`。
   ブロック形式の namespace、`get; set;` プロパティ。file-scoped namespace・global using・record・`init`・raw string・collection expression・primary constructor は使わない(Unity の言語バージョンと IsExternalInit 不在のため)
2. **ゲーム規則は UnityEngine / MagicOnion に依存しない純 C# に置く。** Client は `Runtime/Core`(`noEngineReferences`)、Server は `Rooms/`。Unity 層と Hub は橋渡しだけ
3. **`Terrace.Client/Assets/Terrace/Shared/` は複製物。** 手で編集しない。元リポジトリを直して `Terrace.Client/tools/sync-shared.ps1` を実行する
4. **同じ規則が Client(`LocalWorld` / `GameSimulation`)と Server(`Room`)の 2 か所にある。** 片方を変えたら、もう片方と [docs/architecture/authority.md](docs/architecture/authority.md)、該当する `docs/spec/` を確認する
5. **マスタの追加は「Shared にクラスを 1 つ + 同名の CSV」だけで済ませる。** 一覧ファイルや設定ファイルを作らない
6. **マップ JSON は `MapData.Validate()` が空になる状態を保つ。**
7. **通信 DTO の MessagePack `[Key(n)]` は付け替えない。** 追加は末尾の番号で
8. **Unity のシーンは手で編集しない。** `ProjectSetup`(メニュー `Terrace > Create Main Scene` / `tools/unity.ps1 -Setup`)が生成する

## 検証のコマンド

| 対象 | コマンド(各リポジトリのフォルダで) |
|---|---|
| Terrace.MasterData / Terrace.Map / Terrace.Server | `dotnet test` |
| Terrace.Server の通信経路 | サーバー起動 + テストクライアント 2 つ([docs/guides/run-and-test.md](docs/guides/run-and-test.md)) |
| Terrace.Client | `pwsh tools/unity.ps1 -EditMode` / `-PlayMode`(エディタを開いているときは `-Mirror` を付ける) |
| 文書のリンクと `sources` | `pwsh tools/check-docs.ps1`(このフォルダで) |

## 文書を保つ決まり

- 文書・コメントは日本語、識別子は英語
- **1 つの事実は 1 か所にだけ書く。** 他の文書からはリンクする
- **数値パラメータは書かない。** 設定クラスのプロパティ名を書き、値はコードを正とする(例: `PlayerConfig.InvulnerableSeconds`)
- **クラス一覧・メソッド一覧・テスト件数は書かない。** すぐ古くなる
- `docs/` の各文書は先頭に front matter を持つ。`sources` は正となるファイル(このフォルダからの相対パス)、`status` は実装の状態

コードを変えたら、次の文書を合わせて直す。

| 変えたもの | 直す文書 |
|---|---|
| ゲームの規則(移動・戦闘・敵・経済・マップ移動) | `docs/spec/*.md` |
| 誰が状態を決めるか(権威) | `docs/architecture/authority.md` |
| マップ JSON の形 | `docs/contracts/map-format.md` と `map.schema.json` |
| CSV / テーブル / manifest の形 | `docs/contracts/masterdata.md` |
| IGameHub / Receiver / DTO | `docs/contracts/protocol.md` |
| リポジトリ間の依存・配り方・同期 | `docs/architecture/system.md`、`docs/guides/sync.md` |
| 後から覆すと困る判断 | `docs/decisions/` に ADR を足す |
| 段階の完了・未決事項の解消 | `docs/roadmap.md` |
