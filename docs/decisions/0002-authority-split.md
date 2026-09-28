---
status: 採用
sources:
  - Terrace.Server/src/Terrace.Server/Rooms/IMoveValidator.cs
  - Terrace.Server/src/Terrace.Server/Rooms/Room.cs
---

# 0002 移動はクライアント権威、敵とドロップはサーバー権威

- 日付: 2026-09-04

## 背景

横スクロールアクションは移動の手触りが命で、通信の遅れを移動に感じさせたくない。一方で、敵の HP やドロップは複数人で共有し、食い違ってはいけない。

## 決定

- プレイヤーの移動は Client が計算し、Server は受け取った `MoveState` を他の Client へ中継する
- ただし後で権威を Server へ移せるよう、受け取る箇所に検証フック `IMoveValidator` を置く。初期実装は常に受け入れる
- 敵の HP・死亡・復活・ドロップ抽選は Server が決める。のちに巡回位置とドロップの拾得も Server 権威に加えた

## 理由

- 移動を Server で再現すると、足場の判定を含む物理シミュレーションを Server に持つ必要があり、遅延補償も要る。最初の段階では重すぎる
- 共有する状態(敵・ドロップ)だけを Server が持てば、食い違いは起きない

## 引き換えにしたもの

- 位置の改ざんには弱い。検証フックに速度や到達可能性の検査を足すまでは防げない
- 攻撃の当たり判定・プレイヤーの HP・メソと持ち物をどちらが持つかは、この判断では決めていない([architecture/authority.md](../architecture/authority.md) の未決事項)

## 関連

- [architecture/authority.md](../architecture/authority.md)
- [contracts/protocol.md](../contracts/protocol.md)
