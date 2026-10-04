---
status: 採用
sources:
  - Terrace.Client/Assets/Terrace/Runtime/Presentation/Terrace.Client.Presentation.asmdef
  - Terrace.Client/Assets/Terrace/Runtime/Presentation/IShopView.cs
  - Terrace.Client/Assets/Terrace/Runtime/Presentation/ShopPresenter.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/ShopView.cs
  - Terrace.Client/Assets/Terrace/Runtime/Unity/UguiFactory.cs
  - Terrace.Client/CLAUDE.md
---

# 0013 窓の UI は MVP にし、Presenter は Unity に依存しないアセンブリに置く

- 日付: 2026-10-04

## 背景

店の窓(`ShopWindow`)は 1 つの MonoBehaviour で、uGUI の組み立て、表示用の整形(題・メソ・ヒントの文言)、窓だけの状態(選んだ行・タブ・ワンクリック売却)、操作(買う・売る・閉じる・Esc)を抱えていた。「売って 0 個になったら選択を外す」のような窓の決まりは、Unity の PlayMode でしか確かめられなかった。窓がメッセージ欄に直接書き込む所もあった。ログイン窓も、同じ組み立ての小道具を重複して持っていた。

これから持ち物・装備・スキルの窓が増える([roadmap.md](../roadmap.md))。窓の作り方の型を先に決めておきたかった。

## 決定

- 窓の UI は MVP(Model–View–Presenter)で作る
  - Model は規則の側(店なら `TradingSystem` と `ShopSession`)。窓の都合で名前を変えない
  - `~Presenter` は、窓に何を出すか・押されたら何をするかを決め、窓だけの状態を持つ。窓の操作への知らせ(何も選ばずに押したなど)もここが出す
  - `I~View` は Presenter から見た窓の口(「表示せよ」と「押された」の知らせ)
  - `~View` は uGUI の MonoBehaviour。`I~View` を実装し、言われた通りに描いて押されたことを知らせるだけで、規則の側を操作しない
- Presenter と View の口は、新しいアセンブリ `Terrace.Client.Presentation`(`Runtime/Presentation`、`noEngineReferences`)に置く。.NET のテスト(`tests/Terrace.Client.Core.Tests`)にも取り込む
- uGUI の組み立ての小道具は `UguiFactory` にまとめ、窓ごとに書かない
- 世界の絵(キャラ・敵・地形・カメラ)と HUD は MVP にしない。毎フレーム状態を読んで描く今の形のままにする
- クラスの役目は接尾辞で示し、`~Model`・`~Controller`・`~Manager`・`~Window` は使わない(一覧は `Terrace.Client/CLAUDE.md` の「名前の付け方」)

## 理由

- 窓の決まりを、Unity なしで秒単位のテストで確かめられる。偽の窓に押されたことを起こさせるだけでよい
- Presenter を Core に置かず別のアセンブリにすると、Core を「ゲームの規則」だけの場所に保てる
- 窓の View が薄くなり、見た目の作り替え(Prefab にする、uGUI 以外にする)が Presenter に響かない
- 世界の絵は毎フレーム多くの物を動かすので、Presenter を挟むと手間が増えるだけ。ゲームでは状態を読んで描くのが定石

## 引き換えにしたもの

- 窓 1 つにつき、Presenter・View の口・View の 3 つのクラスが要る
- アセンブリが 1 つ増えた(Unity 層とテストのアセンブリが参照する)
- 窓の決まりのうち、見た目に関わるもの(行の色・タブの絵)は View に残る。これは PlayMode とスクリーンショットで確かめる

## 関連

- [ADR 0003](0003-pure-csharp-rules.md)
- [Terrace.Client/ARCHITECTURE.md](../../Terrace.Client/ARCHITECTURE.md)
