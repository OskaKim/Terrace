# Fable 投入用プロンプト集

3本独立しています。順番に投げても、必要なものだけ投げてもOK。

プロジェクト名は **Terrace** で確定済み。投げる前に置換が要るのは `net9.0`（使いたい .NET バージョン）だけです。

---

## プロンプト1: マスタデータパイプライン

```
Unityゲーム向けのマスタデータ変換パイプラインを、Unity非依存の純.NETプロジェクトとして
新規に作ってください。リポジトリ名・ルート名前空間は Terrace.MasterData とします。

## 目的
CSVで書かれたマスタデータを、Cysharp/MasterMemory のバイナリに変換するCLIツールと、
その周辺のテストを作る。Unityへのローダはこのタスクのスコープ外（後で別途やる）。

## 技術前提
- .NET は net9.0
- MasterMemory v3系（NuGet: MasterMemory, MasterMemory.Generator）
- シリアライザは MessagePack
- テストは xUnit

## リポジトリ構成
    Terrace.MasterData/
      src/
        Shared/                          … テーブル定義(.cs)。唯一の正
        Terrace.MasterData.Builder/    … CLI本体
      tests/
        Terrace.MasterData.Tests/
      Terrace.MasterData.sln

Shared は独立したcsprojにせず、Builder.csproj から
<Compile Include="../Shared/**/*.cs" /> でグロブして取り込む形にしてください。
将来Unityとサーバーからも同じファイルを参照するためです。

## 設計判断（すでに確定済み。変更しないでください）
1. テーブルの発見は、[MemoryTable] 属性の付いた型をアセンブリからリフレクションで
   自動収集する。「追加するたびに編集する一覧ファイル」は絶対に作らないこと。
2. CSVファイル名（拡張子なし）== MemoryTable名 の規約で対応付ける。
   マッピング用の設定ファイルは作らないこと。
3. CSV→オブジェクトのマッピングはリフレクションで実装する。
   Source Generator は使わないこと。Builderはビルド時にしか走らないオフラインCLIなので
   実行速度は要件ではない。
4. データ検証は MasterMemory の Validator（IValidatable<T> / IValidator<T> /
   GetReferenceSet / Exists / Unique / CallOnce）をそのまま使う。
   独自の検証エンジンを実装しないこと。
5. エラーは fail-fast にせず、全件収集してからまとめて出力する。

## CSV仕様
- 1行目がヘッダ。ヘッダ名 == プロパティ名で対応付け（大文字小文字は区別しない）
- `#` で始まる行はコメントとして読み飛ばす
- bool は true/false/TRUE/FALSE/1/0 を受け付ける
- 配列は `|` 区切り（例: `1|2|3`）
- 空セルはその型のdefault
- enum は名前で指定（数値も受け付ける）
- 未知のヘッダ列があったら警告として報告し、処理は続行する
- 型定義にあるのにCSVに列がない場合はエラー

## 最重要機能: 行トレース
MasterMemoryのValidateが返す失敗結果は「Unique failed: ItemId, value = 2」のように
オブジェクト単位で、元のCSVのどこが悪いか分かりません。

CSV読み込み時に「どのオブジェクトがどのファイルの何行目の由来か」を保持しておき、
検証失敗を必ず以下の形式まで逆引きして出力できるようにしてください。

    item.csv:14  [ItemId] 主キーが重複しています (value = 2)
    quest.csv:7  [RewardItemId] item.csv に存在しないIDを参照しています (value = 999)

これがこのツールの一番の価値なので、ここは丁寧に作ってください。

## CLI仕様
    masterdata-build --input <CSVフォルダ> --output <出力フォルダ> [--verbose]
- 成功時は終了コード0、検証エラーが1件でもあれば非ゼロ
- 出力は master.bytes と manifest.json
- manifest.json には バイナリのSHA256ハッシュ、生成日時、テーブル名と件数の一覧を含める
  （後でクライアントとサーバーのマスタバージョン一致チェックに使う）

## サンプルテーブル
Shared に3つ定義してください。
- Item        : ItemId(PK), Name, Price, Category(enum)
- Quest       : QuestId(PK), Name, RewardItemId（Itemを参照）, RequiredLevel
- Enemy       : EnemyId(PK), Name, Hp, Attack, DropItemIds(配列)
Quest と Enemy には IValidatable<T> を実装し、参照チェックと範囲チェックを書いてください。

## テスト
xUnitで以下を必ずカバーしてください。
- 正常系: 3テーブルを変換 → 読み戻して件数と代表値が一致する
- 型変換: bool表記ゆれ、配列、enum、空セルのそれぞれ
- 異常系: 主キー重複CSV / 参照切れCSV / 型不正CSV を tests のフィクスチャとして同梱し、
  エラーメッセージが「期待するファイル名と行番号」を含むことをアサートする

## 実装の進め方
まず最小の縦切り（1テーブルでCSV読む→バイナリ吐く→読み戻す）を通してから、
型変換層 → 行トレースと検証層 → CLI仕上げ、の順に厚くしてください。

## やらないこと
- Unityへの依存（UnityEngine の参照は一切禁止）
- Googleスプレッドシート連携
- 暗号化、難読化、差分パッチ、CDN配信
- GUIエディタ

## 成果物
上記に加えて、README.md に「新しいマスタを1つ追加する手順」を書いてください。
その手順が「Sharedにクラスを1つ足して、同名のCSVを置くだけ」で済んでいない場合、
設計が間違っているので直してください。
```

---

## プロンプト2: フットホールド（足場）とマップデータ

```
フットホール（線分）ベースの2D横スクロールアクションRPGのマップ構造を扱う
純C#ライブラリを作ってください。ルート名前空間は Terrace.Map です。

## 前提
- .NET は net9.0、テストは xUnit
- UnityEngine への依存は一切禁止。物理エンジンも使わない
- 座標系は X が右、Y が上。単位は無次元（1.0 = 1ユニット）

## 中核となるデータモデル
このジャンルのマップはタイルマップではなく「フットホール（線分）のグラフ」です。

- Foothold: Id, X1, Y1, X2, Y2, PrevId, NextId, Layer
  地面や床は線分の連なりとして表現され、線分の端が前後のフットホールに繋がる。
  PrevId / NextId が 0 なら、そこがチェーンの端（＝崖）。
  斜めの線分も許容すること（坂道になる）。
- Ladder: Id, X, Y1, Y2, IsRope（はしごとロープを区別するフラグ）
- Portal: Id, Name, X, Y, TargetMapId, TargetPortalName
- SpawnPoint: Id, X, Y, EnemyId, RespawnSeconds
- Map: Id, Name, 上記のコレクション, ワールド境界(Left/Right/Top/Bottom)

## 必要なクエリAPI
コントローラ実装側が物理エンジンなしで移動を組めるだけの情報を返してください。
- FindFootholdBelow(x, y)        … 指定座標の真下にある一番近いフットホール
- GetYAt(foothold, x)            … 線分上の指定X座標におけるY（斜面対応）
- IsWithinX(foothold, x)         … Xが線分の範囲内か
- GetNext(foothold, direction)   … 端に到達したとき繋がる次のフットホール（無ければnull）
- IsEdge(foothold, direction)    … その方向が崖かどうか
- FindLadderNear(x, y, range)    … つかまれるはしご/ロープ
- FindPortalNear(x, y, range)
- ClampToWorld(position)

## シリアライズ
マップはマスタデータではなく構造データなので、JSONで読み書きします。
System.Text.Json を使い、Load(path) / Save(path) を用意してください。
サンプルとして、坂道・段差・崖・はしご1本・ポータル2つを含む
テスト用マップのJSONを1枚同梱してください。

## バリデーション
Map.Validate() を用意し、以下を検出して結果リストを返してください。
- PrevId/NextId が存在しないIdを指している
- 前後の繋がりが相互に一致していない（AのNextがBなのにBのPrevがAでない）
- 線分の端点が繋がっている相手の端点と離れすぎている（許容誤差つき）
- ポータルの接続先が空、または自分自身を指している
- ワールド境界の外にあるオブジェクト

## テスト
- 平坦な床の上でのFindFootholdBelow
- 斜面上のGetYAtが線形補間で正しい値を返す
- チェーンの端でGetNextがnullを返し、IsEdgeがtrueになる
- 重なった複数レイヤーがあるときに、正しい方を返す
- Validateが上記の各不整合を検出する（壊れたマップJSONをフィクスチャとして同梱）

## やらないこと
- 描画、Unity連携、キャラクターコントローラそのものの実装
- 経路探索（後で別途）
```

---

## プロンプト3: サーバーのルーム管理と共有インターフェース

```
2D横スクロールMORPGのゲームサーバーを、MagicOnionで作るための土台を用意してください。
ルート名前空間は Terrace.Server / Terrace.Shared です。

## 前提
- .NET は net9.0、MagicOnion 7.10系、テストは xUnit
- ルームのロジック本体は MagicOnion に依存しない純C#クラスとして書き、
  Hub からはそれを呼ぶだけにしてください（テスト可能にするため）
- UnityEngine への依存は禁止

## 構成
    src/
      Terrace.Shared/       … インターフェース定義とDTO（後でUnityからも参照する）
      Terrace.Server/       … ASP.NET Core + MagicOnion。ルームロジックもここ
      Terrace.TestClient/   … 動作確認用のコンソールクライアント
    tests/
      Terrace.Server.Tests/

## 割り切り（確定事項。変更しないでください）
移動はクライアント権威とします。クライアントが計算した座標をそのまま受け取って
他のクライアントに中継してください。ただし後で権威をサーバーに移せるように、
座標を受け取る箇所に「検証フック」のインターフェースだけ切っておいてください
（初期実装は常にtrueを返すNull実装）。

モンスターのHP、死亡判定、リスポーン、ドロップ抽選はサーバー権威とします。

## Shared に定義するもの
Unaryサービス:
- IAccountService: LoginAsync(string name) → プレイヤーIDとキャラ情報

StreamingHub:
- IGameHub / IGameHubReceiver
  - JoinAsync(int mapId, PlayerInfo self)
  - LeaveAsync()
  - MoveAsync(MoveState state)       … 座標、速度、向き、状態(立ち/歩き/ジャンプ/はしご)
  - AttackAsync(int enemyInstanceId, int damage)
  - Receiver: OnJoin / OnLeave / OnMove / OnEnemySpawn / OnEnemyDamaged /
              OnEnemyDead / OnSnapshot

DTOはすべて MessagePackObject。フィールドは最小限で構いません。

## ルームロジック（純C#）
- 1マップ = 1ルーム = 1グループ
- Room: プレイヤー辞書、Join/Leave、位置更新の保持、参加時に現在の全状態を返すSnapshot
- 敵の管理: スポーン地点から一定数を湧かせ、HPを持ち、0になったら死亡してN秒後に復活
- Tick(deltaTime) メソッドを持ち、リスポーンのタイマーを進める
  （実際のティック駆動は BackgroundService から呼ぶ）
- RoomManager: mapId でルームを取得・生成し、空になったら破棄

## サーバー起動
Program.cs は Minimal API 形式で、AddMagicOnion と MapMagicOnionService を使ってください。
ローカル起動できることが目的なので、認証・DB・Redisは入れないこと（すべてインメモリ）。
起動時にダミーのスポーン設定でルームを1つ作れる状態にしてください。

## テスト
ルームロジックを直接叩く形で書いてください（Hub経由でなくてよい）。
- プレイヤー2人がJoinして、片方のMoveがもう片方に届く形で状態が更新される
- Leaveすると他方から見えなくなる
- 敵にダメージを与えてHPが0になると死亡状態になり、Tickを進めるとリスポーンする
- 後からJoinしたプレイヤーのSnapshotに、既存プレイヤーと敵の現在状態が含まれる

## テストクライアント（重要）
Unityなしで通信経路まで検証したいので、MagicOnion.Client を使った
.NET コンソールアプリ Terrace.TestClient を作ってください。

- 起動引数: --name <表示名> --map <mapId> --server <URL>
- 起動すると IAccountService.LoginAsync でログインし、続けて StreamingHub に接続、
  指定マップに JoinAsync する
- IGameHubReceiver を実装し、受信したイベントを1行ずつ標準出力にログする
  例: [recv] OnMove player=alice x=120.5 y=64.0 state=Walk
- 送信側は2モードを用意
  - patrol（デフォルト）: 一定間隔でX座標を左右に往復させて MoveAsync を送り続ける
  - manual: 矢印キーで座標を動かし、スペースキーで最寄りの敵に AttackAsync
- Ctrl+C で LeaveAsync してから正常終了すること

## ローカル通信の前提
Unityクライアントも後で YetAnotherHttpHandler で平文HTTP/2に繋ぐので、
Kestrel は TLSなしのHTTP/2（h2c）エンドポイントを開くように設定してください。
テストクライアント側も http:// のURLでそのまま GrpcChannel.ForAddress できること。
開発用証明書まわりで詰まらせないことを優先します。

## やらないこと
- インベントリ、スキル、レベルアップ、パーティ、チャット
- データベース、認証、暗号化
- Docker / コンテナ化
- Unityクライアント側の実装
- 移動のサーバー側物理シミュレーション

## 成果物
README.md に以下を書いてください。
1. サーバーをローカル起動する手順と、接続先URLの確認方法
2. テストクライアントを2つ立ち上げて、片方の移動がもう片方の標準出力に
   流れてくるのを確認する手順（コピペで実行できるコマンド列で）
```

---

## 投げた後の確認ポイント

細かく見られなくても、この3つだけは目視しておくと後が楽です。

1. **プロンプト1**: `README.md` の「マスタ追加手順」が1〜2ステップになっているか
2. **プロンプト2**: `Foothold` に `PrevId` / `NextId` が残っているか（タイルマップに作り替えられていないか）
3. **プロンプト3**: ルームロジックが MagicOnion のクラスを継承していないか（純C#になっているか）

`dotnet test` が通るかどうかで大半は判断できます。プロンプト3だけは、
サーバーを起動してテストクライアントを2つ繋ぎ、片方の移動がもう片方に
流れてくるのを見るところまでやると、通信経路まで確認できます。
