---
status: 文書の目次
sources:
  - CLAUDE.md
---

# 文書の目次

主な読み手は AI。人間向けには [OVERVIEW.md](OVERVIEW.md) だけを用意している。

## やりたいこと別の索引

| やりたいこと | 読む文書 |
|---|---|
| 全体をつかむ | [architecture/system.md](architecture/system.md)、[glossary.md](glossary.md) |
| いま何が済んでいて、次に何をするか | [roadmap.md](roadmap.md) |
| キャラクターの移動を変える | [spec/movement.md](spec/movement.md) |
| 攻撃・被弾・死亡を変える | [spec/combat.md](spec/combat.md) |
| 敵の湧き・巡回・ドロップを変える | [spec/enemy-drop.md](spec/enemy-drop.md)、[architecture/authority.md](architecture/authority.md) |
| メソ・持ち物・店を変える | [spec/economy-shop.md](spec/economy-shop.md) |
| ポータル・マップ移動・NPC を変える | [spec/map-travel-npc.md](spec/map-travel-npc.md) |
| マップ JSON を書く・形を変える | [contracts/map-format.md](contracts/map-format.md)、[guides/add-map.md](guides/add-map.md) |
| マスタ(CSV)を足す・形を変える | [contracts/masterdata.md](contracts/masterdata.md)、[guides/add-master-table.md](guides/add-master-table.md) |
| Client と Server の通信を変える | [contracts/protocol.md](contracts/protocol.md)、[architecture/authority.md](architecture/authority.md) |
| 共有コードや成果物を同期する | [guides/sync.md](guides/sync.md) |
| 環境を作る・動かす・テストする | [guides/setup.md](guides/setup.md)、[guides/run-and-test.md](guides/run-and-test.md) |
| なぜこうなっているのかを知る | [decisions/](decisions/README.md) |

## ファイル一覧

```
docs/
  OVERVIEW.md                 人間向けの概要
  README.md                   この目次
  glossary.md                 用語集
  roadmap.md                  段階・現在地・未決事項・既知の不整合
  architecture/
    system.md                 4 リポジトリの役目、依存、コードとデータの配り方
    authority.md              誰が状態を決めるか。Client と Server に同じ規則がある件
  spec/                       ゲームの規則(何が正しい動きか)
    movement.md               地上 / 空中 / はしご の移動
    combat.md                 攻撃・接触ダメージ・無敵・死亡・復活
    enemy-drop.md             敵の湧き・巡回・死亡・復活、ドロップの抽選・拾得・寿命
    economy-shop.md           メソ・持ち物・店の売り買い
    map-travel-npc.md         マップの読み込み、ポータル、マップ移動、NPC、飾り、テーマ
  contracts/                  リポジトリの境界にある約束(壊すと他のリポジトリが壊れる)
    map-format.md             マップ JSON の形と検証
    map.schema.json           マップ JSON の JSON Schema(エディタの補完と検査用)
    masterdata.md             CSV 仕様、テーブル、master.bytes と manifest.json
    protocol.md               IAccountService / IGameHub / Receiver / DTO と流れ
  decisions/                  ADR(後から覆すと困る判断の記録)
  guides/                     手順書
  prompts/                    最初に各リポジトリを作らせた指示書(歴史的な記録)
```

## どの事実がどこにあるか(文書に書かないもの)

文書には規則と構造だけを書き、次のものはコードやデータを正とする。

| 事実 | 正 |
|---|---|
| 移動の数値(歩く速さ、重力など) | `Terrace.Client/Assets/Terrace/Runtime/Core/MotorConfig.cs` |
| プレイヤーの数値(HP、攻撃、無敵時間、初期メソなど) | `Terrace.Client/Assets/Terrace/Runtime/Core/Player.cs` の `PlayerConfig` |
| 敵の既定値(ドロップ率、巡回速度など) | Client: `Runtime/Core/Enemies.cs` の `EnemyDefinition`、Server: `Rooms/EnemySpawnConfig.cs` と `Rooms/ISpawnConfigProvider.cs` |
| テーブルの列 | `Terrace.MasterData/src/Shared/Tables/*.cs` |
| マスタの中身(敵・アイテム) | `Terrace.MasterData/samples/csv/*.csv` |
| マップの中身 | `Terrace.Map/maps/*.json`([ADR 0006](decisions/0006-map-json-source-of-truth.md)) |
| 通信のメソッドと DTO | `Terrace.Server/src/Terrace.Shared/` |
| 操作キー | `Terrace.Client/README.md`、`Runtime/Unity/InputSources.cs` |
| 見た目の素材の割り当て | `Terrace.Client/README.md`、`Runtime/Unity/ArtLibrary.cs` |
| テスト件数・クラス一覧 | 書かない。コードを見る |

## リポジトリの中にある文書

リポジトリの内側で閉じる話は、そのリポジトリの文書にある。ここと重なる記述があれば `docs/` を正とする。

| 文書 | 内容 |
|---|---|
| `Terrace.Client/README.md` | 遊び方、操作、素材、テストの回し方 |
| `Terrace.Client/ARCHITECTURE.md` | Client の層、1 フレームの流れ、アセンブリ |
| `Terrace.Server/README.md` | 起動、テストクライアント、Server 内部の構成 |
| `Terrace.Map/README.md` | ライブラリの使い方、問い合わせ API |
| `Terrace.MasterData/README.md` | CLI の使い方、エラー出力の読み方 |

## 文書の書き方

先頭に front matter を置く。

```yaml
---
status: 実装済み(Client オフライン)/ Server 実装中     # 実装の状態を一言で
sources:                                             # 正となるファイル。このフォルダ(Terrace/)からの相対パス
  - Terrace.Client/Assets/Terrace/Runtime/Core/CharacterMotor.cs
---
```

- `status` に使う言葉: `実装済み` / `実装中(未コミット)` / `仮実装` / `未実装` / `提案中` / `採用`
- `sources` のパスが存在するかは `pwsh tools/check-docs.ps1` で検査できる
- 数値・クラス一覧・テスト件数は書かない(ルートの [CLAUDE.md](../CLAUDE.md) を参照)
