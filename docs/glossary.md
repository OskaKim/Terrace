---
status: 採用
sources:
  - Terrace.Map/src/Terrace.Map
  - Terrace.Client/Assets/Terrace/Runtime/Core
  - Terrace.Server/src/Terrace.Server/Rooms
  - Terrace.Server/src/Terrace.Shared
---

# 用語集

コード上の名前を括弧に書く。

## マップ

| 用語 | 意味 |
|---|---|
| ユニット | 長さの単位。1.0 = 1 ユニット。X は右、Y は上が正。Client の絵は 70 px = 1 ユニット |
| マップ(`MapData`) | 1 枚の場。ID(`Id`)で引く。タイルマップではなく線分のグラフ |
| フットホールド / 足場(`Foothold`) | 立てる線分。`X1,Y1`–`X2,Y2`。傾いていれば坂道 |
| Prev / Next(`PrevId` / `NextId`) | 線分の端に繋がる隣の足場の ID。`PrevId` は (X1,Y1) 側、`NextId` は (X2,Y2) 側。0 なら端 |
| 崖 | 繋がる足場が無い端。歩いて越えると落ちる |
| 壁 | `X1 == X2` の垂直な足場。歩いて当たると止まる。真下検索の対象外 |
| レイヤー(`Layer`) | 重なった足場を区別する番号 |
| はしご / ロープ(`Ladder`、`IsRope`) | X 固定で上下できる区間。はしごとロープは見た目の違いだけ |
| ポータル(`Portal`、`PortalKind.Portal`) | ↑ で入ると `TargetMapId` のマップの `TargetPortalName` へ移る |
| 出現地点(`PortalKind.Spawn`) | マップに入ったとき・復活したときに立つ場所。入っても何も起きない。慣習で名前は `spawn` |
| 湧き点(`SpawnPoint`) | 敵が湧く場所。`EnemyId` と `RespawnSeconds` を持つ |
| NPC(`Npc`) | マップに立つ人物。`Kind` が `talk`(話すだけ)か `shop`(店を開く、`ShopId`) |
| 飾り(`Decoration`) | 見た目だけの絵。当たり判定なし |
| テーマ(`Theme`) | 地面のタイルと背景の種類。`grass` / `stone` / `sand` / `castle` / `snow` |
| ワールド境界(`WorldBounds`) | マップの外枠。Y が上なので `Top > Bottom` |

## ゲーム

| 用語 | 意味 |
|---|---|
| 移動モード(`MotorMode`) | `Ground`(足場の上)/ `Air`(空中)/ `Ladder`(はしご) |
| 入力フレーム(`InputFrame`) | 1 フレーム分の押下状態。Unity の入力系から切り離すための形 |
| 攻撃硬直(`AttackLockSeconds`) | 攻撃を始めてから歩けない時間 |
| 無敵(`InvulnerableTimer`) | 被弾・復活・マップ移動の直後に接触ダメージを受けない時間 |
| ノックバック | 被弾で敵と反対側へ吹き飛ぶこと。空中モードになる |
| 敵 ID(`EnemyId`) | マスタ上の敵の種類 |
| インスタンス ID(`InstanceId`) | マップ(ルーム)内の敵 1 体の番号。攻撃対象の指定に使う |
| ドロップ(`ItemDrop` / `RoomDrop`) | 地面に落ちたアイテム。`DropId` で区別し、寿命がある |
| メソ(`Meso`) | 所持金 |
| 経験値(`Exp`) | 敵を倒すと溜まる値。今のレベルの `ExpToNext` に届くとレベルが上がる |
| レベル(`Level`)・レベル表(`LevelTable`、マスタの `player_level`) | プレイヤーの成長の段階と、レベルごとの必要経験値・最大 HP・攻撃力の表 |
| 持ち物(`Inventory`) | 持っているアイテム ID の並び |
| アイテム分類(`ItemCategory`) | マスタ上の分類。`Weapon` / `Armor` / `Consumable` / `Material` |
| 持ち物タブ(`ItemKind`) | Client の表示上の分類。`Equip` / `Use` / `Etc`。`ItemCategory` から変換する |
| 店(`ShopDefinition`、`ShopSession`) | `ShopId` ごとの品揃えと、開いている間の売り買い |

## 通信とサーバー

| 用語 | 意味 |
|---|---|
| ルーム(`Room`) | Server の 1 マップ分の世界。1 マップ = 1 ルーム = 1 配信グループ |
| スナップショット(`RoomSnapshot`) | 参加した瞬間に受け取る、ルームの全状態(自分以外のプレイヤー・敵・ドロップ) |
| 権威 | その状態を最終的に決める側。移動は Client、敵とドロップは Server([architecture/authority.md](architecture/authority.md)) |
| 検証フック(`IMoveValidator`) | Client から届いた移動を受け入れるか決める口。今は常に受け入れる |
| 通知先(`IRoomEventSink`) | Room が状態変化を知らせる口。Server ではグループ配信に、テストでは記録に繋ぐ |
| Tick | Server が一定間隔で全ルームの時間を進めること(`RoomTickService`) |
| StreamingHub / Receiver | MagicOnion の双方向通信。Client → Server が `IGameHub`、Server → Client が `IGameHubReceiver` |
| h2c | TLS なしの HTTP/2。開発中の gRPC はこれで繋ぐ |

## データと開発

| 用語 | 意味 |
|---|---|
| マスタ | CSV で書く表データ(敵・アイテム・クエスト) |
| master.bytes | マスタを MasterMemory のバイナリにしたもの |
| manifest.json | master.bytes の SHA256・生成日時・テーブル件数。Client では `master.manifest.json` という名前で置く |
| 行トレース | マスタの検証エラーを `item.csv:14 [列名]` の形で CSV の行まで逆引きすること |
| 共有コード | Unity と Server からも使う C# 9 のコード(Map、MasterData の Shared、Server の Shared) |
| 同期(sync) | 共有コードと成果物を Client へ複製すること(`tools/sync-shared.ps1`) |
| Core 層 / Unity 層 | Client の純 C# のゲーム本体 / MonoBehaviour と読み込み・描画 |
| 世界の入れ物(`WorldState`) | Client の 1 マップ分の敵とドロップと、その問い合わせ(攻撃の相手探し・接触・拾える物)。誰が決めるかは知らない |
| 世界の権威(`IWorldAuthority`) | Client で敵とドロップを決める側。オフラインは `OfflineRoom`(Server の Room の代わりを同じ規則で務める)、オンラインは `RoomMirror`(Server の通知を映すだけ)。攻撃と拾うは頼むだけで、結果は同じイベントで届く |
| 受け箱(`OnlineInbox`) | Server からの通知をいったん積んでおく Client の箱。反映は `GameSimulation.Step` の頭で行う |
| 送る口(`IOnlineChannel`) | Client の Core が Server へ送るときに使う口。実装は `MagicOnionConnection`、テストでは記録するだけの偽物 |
| スナップショット待ち | `JoinAsync` を送ってから、そのマップの `OnSnapshot` が届くまで。この間の通知は前のマップの残りとして捨てる |
| 他のプレイヤー(`RemotePlayers`) | 同じマップにいる自分以外。表示位置は届いた位置へ滑らかに寄せる |
| GameSimulation | Client の 1 セッション分のまとめ役。係(`~System`)を組み立て、入力を受けて 1 フレームの中で決まった順に係を呼ぶ。規則は係にある |
| Presenter / View(窓) | Client の窓の UI の分け方(MVP)。`~Presenter`(`Runtime/Presentation`、純 C#)が何を出し押されたら何をするかを決め、`~View`(uGUI)は言われた通りに描いて押されたことを知らせる |
| GameNarrator | Client のメッセージ欄の文言を作る所。係のイベントを見て `MessageLog` に一行ずつ流す。文言はここにだけある |
| 係(`~System`) | Client の規則の務め 1 つを受け持つクラス(`PlayerLifeSystem`・`CombatSystem`・`KillRewardSystem`・`LootingSystem`・`TravelSystem`・`TradingSystem`)。共有する状態は `GameContext` から読む |
