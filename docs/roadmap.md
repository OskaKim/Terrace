---
status: 2026-09-28 時点
sources:
  - Terrace.Client/README.md
  - Terrace.Server/README.md
---

# 段階と現在地

「やっていないこと」「未決事項」はこの文書だけに書く。各リポジトリの README にある同種の記述は古い可能性がある。

## 段階

| 段階 | 内容 | 状態 |
|---|---|---|
| 0. 土台 | MasterData(CSV 変換と行トレース)、Map(フットホールドと検証)、Server(ルームとテストクライアント)を別々に作る | 完了(2026-09-04) |
| 1. オフラインの Client | Unity で歩く・跳ぶ・はしご・ポータル・攻撃・敵・ドロップ。Map と MasterData を同期して使う | 完了(2026-09-14) |
| 1.5 見た目と町 | Kenney の素材で見た目を作る。マップにポータルの種類・NPC・飾り・テーマを足す。町(map 100)と砂の崖(map 2)、NPC の店、メソ | 完了(2026-09-28) |
| 2. マップとマスタで動く Server | Server がマップとマスタを読んで敵を湧かせ、巡回させ、ドロップを決め、拾わせる(`PickupAsync`、`OnEnemyMove`、`OnDropSpawn`、`OnDropRemoved`) | 完了(2026-09-16) |
| 3. Client と Server の接続 | Client の LocalWorld をサーバーからの通知で動かす。YetAnotherHttpHandler で h2c に繋ぐ。ログイン窓、他のプレイヤーの表示、切断時のオフライン切り替え | 完了(2026-09-28)。考え方は [ADR 0008](decisions/0008-online-client-inbox.md) |
| 4 以降 | 下の「やっていないこと」から選ぶ | 未着手 |

## やっていないこと

### 次の候補

- オンラインで Client に残っている権威(プレイヤーの HP、メソと持ち物、攻撃の当たり判定)をどうするか決める。下の未決事項
- Multiplayer Play Mode(エディタの仮想プレイヤー)と Windows ビルド(`tools/unity.ps1 -Build`)で複数人を実際に並べて確かめる。自動テストは「Unity の Client 1 つ + 画面の無い Client 1 つ」で通している
- 店の品揃えをマスタ(shop.csv)にする。今は `PlaceholderShopCatalog` の仮(`general` = 全品、`potion` = 消費、`equip` = 装備)
- 各リポジトリの README から、`docs/` と重なる仕様の記述を落としてリンクに替える(Client と Server の作業がコミットされてから)

### その後

- アイテムを使う・装備する
- 経験値・レベル・スキル
- 経路探索(Map)
- 認証・永続化(今はすべてインメモリ)
- IL2CPP ビルド(MessagePack / MasterMemory の生成済みリゾルバ登録と、MagicOnion のクライアント事前生成が要る。今の Client は MagicOnion の動的生成に頼っている)
- チャット(当面やらないことから外すか決める)

### 当面やらないこと

- パーティ、チャット
- データベース、暗号化、Docker
- 移動のサーバー側物理シミュレーション
- マスタの Google スプレッドシート連携、暗号化、差分配信、GUI エディタ

## 未決事項

| 事項 | 選択肢・論点 | 関係する文書 |
|---|---|---|
| オンライン時のプレイヤー HP・被弾の権威 | Client のまま / Server へ移す | [architecture/authority.md](architecture/authority.md) |
| オンライン時のメソ・持ち物・店の権威 | Client のまま / Server へ移す(Server には今まったく無い) | [spec/economy-shop.md](spec/economy-shop.md) |
| 攻撃の当たり判定とダメージ量 | 今の `AttackAsync` は Client が選んだ敵とダメージ量をそのまま受け取る | [spec/combat.md](spec/combat.md)、[contracts/protocol.md](contracts/protocol.md) |
| マスタに無い敵の値 | ドロップ率・巡回速度・大きさ・メソ報酬を enemy テーブルに足すか | [spec/enemy-drop.md](spec/enemy-drop.md) |

## 既知の不整合

| 不整合 | 詳細 |
|---|---|
| 規則の数値が Client と Server の両方に直書き | ドロップ寿命、ドロップ率、巡回速度 |
| 湧き点の `RespawnSeconds` が 0 以下のとき | Server は 10 秒として扱い、Client はそのまま(すぐ復活) |
| 最初のマップ | Server のログインは map 100(`AccountRegistry.StartMapId`)を返すが、起動時に作るルームは map 1(`TerraceServerOptions.InitialMapId`)。Unity の Client はログイン結果を見ず、自分の `GameBootstrap.StartMapId` から始める |
| 名乗った PlayerId を信じる | `JoinAsync` の `self.PlayerId` をそのまま使う。ログインで発行した番号との照合は無い(認証が無いため) |
| マスタの版を突き合わせない | Client と Server の `sha256` を接続時に比べない。CSV を片方だけ変えると、敵の名前や最大 HP の見え方がずれる |
| 各リポジトリの README の古い記述 | Server README の「スポーン設定はダミー」や Receiver の一覧、テストクライアントの出力例など。仕様は `docs/` を正とする |
