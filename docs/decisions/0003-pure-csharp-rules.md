---
status: 採用
sources:
  - Terrace.Client/Assets/Terrace/Runtime/Core/Terrace.Client.Core.asmdef
  - Terrace.Client/Assets/Terrace/Runtime/Core/GameSimulation.cs
  - Terrace.Server/src/Terrace.Server/Rooms/Room.cs
  - Terrace.Server/src/Terrace.Server/Hubs/GameHub.cs
---

# 0003 ゲーム規則は純 C# に置き、オフラインで先に作る

- 日付: 2026-09-04(Server)、2026-09-14(Client)

## 背景

規則を Unity の MonoBehaviour や MagicOnion の Hub に書くと、エディタや通信を立ち上げないと確かめられない。AI に書かせたコードを速く検証したい。

## 決定

- Server: ルームの規則は MagicOnion に依存しない `Room` / `RoomManager` に置く。Hub は呼ぶだけ。状態変化は `IRoomEventSink` で知らせる
- Client: ゲームの中身は UnityEngine を参照しない `Terrace.Client.Core`(asmdef の `noEngineReferences`)に置く。Unity 層は「入力を `InputFrame` にして渡す」「状態を読んで描く」だけ
- Client は Server に繋ぐ前に、オフラインで遊べる形を先に作る。Server の `Room` の役を `LocalWorld` が同じ規則で肩代わりし、オンライン化で差し替える

## 理由

- `dotnet test` や Unity の EditMode テストで、規則を秒単位で検証できる
- Server と通信が無くても、遊んで手触りを確かめられる
- Core は後で Server 権威に移すときにも使い回せる

## 引き換えにしたもの

- 敵とドロップの規則が Client と Server の 2 か所にある。変えるときは両方を直す必要がある([architecture/authority.md](../architecture/authority.md))
- Unity 層と Core の間に、イベントと同期のための橋渡しコードが要る

## 関連

- `Terrace.Client/ARCHITECTURE.md`
- [spec/enemy-drop.md](../spec/enemy-drop.md)
