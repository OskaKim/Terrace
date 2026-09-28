# Terrace.Server — AI 向けの決まり

全体の決まりは親フォルダの `../CLAUDE.md`。通信の約束は `../docs/contracts/protocol.md`、権威の分担は `../docs/architecture/authority.md`。

## このリポジトリ

MagicOnion のゲームサーバー(`src/Terrace.Server`)、Client と共有する通信の定義(`src/Terrace.Shared`)、動作確認用のコンソールクライアント(`src/Terrace.TestClient`)。

## 決まり

- ルームの規則は `Rooms/` の純 C#(`Room`、`RoomManager`)に書く。MagicOnion の型を継承・参照しない
- Hub(`Hubs/`)は `RoomManager` を呼ぶだけ。規則を書かない
- 状態変化の通知は `IRoomEventSink` を通す。Receiver を足したら `GroupRoomEventSink` とテストの `RecordingEventSink` も直す
- `src/Terrace.Shared` は netstandard2.1 / C# 9。Unity からも同じソースを使う
- DTO の MessagePack `[Key(n)]` は付け替えない。足すときは末尾の次の番号
- 移動はクライアント権威。受け取る箇所は `IMoveValidator` を通す
- 敵の HP・死亡・復活・巡回、ドロップの抽選・拾得はサーバー権威。同じ規則が Client の `LocalWorld` にもあるので、変えたら `../docs/spec/enemy-drop.md` を直し、Client 側の対応も確認する
- 兄弟リポジトリ(`../Terrace.Map`、`../Terrace.MasterData`)を `ProjectReference` で参照している。置き場所を変えない
- DB・認証・暗号化・Docker は入れない(今の段階ではインメモリ)

## 検証

```bash
dotnet test
dotnet run --project src/Terrace.Server
dotnet run --project src/Terrace.TestClient -- --name alice --map 1
```

## 変えたら

- `IGameHub` / Receiver / DTO → `../docs/contracts/protocol.md`
- 敵とドロップの規則 → `../docs/spec/enemy-drop.md`
- マップやマスタの読み方、配り方 → `../docs/architecture/system.md`
