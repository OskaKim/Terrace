---
status: 実装済み(Client)。店の品揃えは仮実装。Server には無い
sources:
  - Terrace.Client/Assets/Terrace/Runtime/Core/ShopSession.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Items.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/Player.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/TradingSystem.cs
  - Terrace.Client/Assets/Terrace/Runtime/Core/KillRewardSystem.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/MasterDataRepository.cs
  - Terrace.Client/Assets/Terrace/Runtime/Presentation/ShopPresenter.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/ShopView.cs
---

# メソ・持ち物・店

店の見た目と操作はメイプルストーリーの店を手本にする。

## メソ

- 始めの所持金: `PlayerConfig.StartingMeso`
- 敵を倒すと得る: `EnemyDefinition.MesoReward`。0 以下なら敵の最大 HP と同じ。マスタに列が無いので、今は常に最大 HP
- 死亡しても減らない
- 画面の左上に出す

## 持ち物

- アイテム ID の並び。同じアイテムも 1 個ずつ入る。上限は無い
- 表示ではアイテムごとにまとめて個数を出し、タブで分ける
- アイテムを使う・装備する機能は無い

### タブ(ItemKind)とマスタの分類(ItemCategory)

| ItemCategory(マスタ) | ItemKind(タブ) |
|---|---|
| `Weapon`、`Armor` | `Equip`(装備) |
| `Consumable` | `Use`(消費) |
| `Material`、`None` | `Etc`(その他) |

変換は Client の `MasterDataRepository.KindOf` だけにある。

## 店を開く・閉じる

- 開く: `kind` が `shop` の NPC をクリックする([map-travel-npc.md](map-travel-npc.md))。`ShopId` で品揃えを引く
  - 品揃えが無い ShopId なら、メッセージを出して開かない
  - 死亡中は開けない
- 開いている間: 移動の入力を受け付けない(その場に立ち止まる)
- 閉じる: 「店を出る」ボタン、Esc、マップ移動、死亡

## 品揃え(仮実装)

`PlaceholderShopCatalog` がアイテムの台帳から組み立てる。本来はマスタ(shop.csv)にする予定([roadmap.md](../roadmap.md))。

| ShopId | 名前 | 売る物 |
|---|---|---|
| `general` | 雑貨屋 | 全アイテム |
| `potion` | 薬屋 | タブが `Use` のもの |
| `equip` | 武具屋 | タブが `Equip` のもの |

## 買う

- 値段 = マスタの `Price`
- 合計 = 値段 × 個数。所持メソが足りなければ買えない
- 成功するとメソが減り、持ち物に個数分入る

## 売る

- 売値 = `max(1, Price / 2)`(整数の割り算)
- 持っている個数より多くは売れない
- 成功すると持ち物から個数分消え、売値 × 個数のメソが入る
- その店の品揃えに無いアイテムも売れる

## 結果(ShopResult)

| 結果 | 意味 |
|---|---|
| `Ok` | 成功 |
| `NotEnoughMeso` | メソが足りない |
| `UnknownItem` | 台帳に無いアイテム |
| `NotSoldHere` | この店では売っていない |
| `NotInInventory` | 持っていない、または足りない |

## 店の窓(Client の UI)

- 左: NPC の商品。行をクリックして選び「アイテムを買う」
- 右: 自分の持ち物。装備 / 消費 / その他のタブ。行をクリックして選び「アイテムを売る」
- 「ワンクリックで売却」を入れると、持ち物の行をクリックしただけで売れる

## オンライン

Server にメソ・持ち物・店は無い。どちらが権威を持つかは未決([architecture/authority.md](../architecture/authority.md))。今はオンラインでも Client が自分の分を持ち、撃破の報酬と拾った物は Server の通知を見て自分で足す。保存はしない(接続し直すと初めから)。
