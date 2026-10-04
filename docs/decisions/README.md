---
status: 目次
sources:
  - docs/prompts/fable-prompts.md
---

# 判断の記録(ADR)

後から覆すと困る判断を 1 つにつき 1 ファイルで残す。覆すときは古い ADR を消さず、状態を「置き換え済み(ADR NNNN)」にして新しい ADR を足す。

| ADR | 判断 | 状態 |
|---|---|---|
| [0001](0001-foothold-graph.md) | マップはタイルではなく足場(線分)のグラフで表す | 採用 |
| [0002](0002-authority-split.md) | 移動はクライアント権威、敵とドロップはサーバー権威 | 採用 |
| [0003](0003-pure-csharp-rules.md) | ゲーム規則は純 C# に置き、オフラインで先に作る | 採用 |
| [0004](0004-share-code-by-copy-csharp9.md) | 共有コードは C# 9 に留め、Client へは複製、Server へはプロジェクト参照で渡す | 採用 |
| [0005](0005-masterdata-csv-reflection.md) | マスタは CSV から MasterMemory へ。テーブルはリフレクションで自動発見 | 採用 |
| [0006](0006-map-json-source-of-truth.md) | マップ JSON の正は Terrace.Map/maps に置き、Client へ同期する | 採用 |
| [0007](0007-docs-in-root-repository.md) | 文書はルートフォルダの git で管理し、AI 向けに書く | 一部を置き換え済み(ADR 0009) |
| [0008](0008-online-client-inbox.md) | オンラインの Client は通知を受け箱に積んで Step で反映し、切れたらオフラインに戻る | 一部を置き換え済み(ADR 0012) |
| [0009](0009-single-repository.md) | 4 つのプロジェクトと文書を 1 つのリポジトリにまとめる | 採用 |
| [0010](0010-issue-driven-parallel-tasks.md) | 仕事の単位は Issue。AI は作業場を切って PR まで進め、マージは人間だけ | 採用 |
| [0011](0011-server-in-docker.md) | 開発する PC では、サーバーを Docker のコンテナでも動かせるようにする | 採用 |
| [0012](0012-client-world-authority.md) | Client の敵と落とし物の世界は、入れ物と権威に分け、権威を差し替える | 採用 |
| [0013](0013-ui-windows-mvp.md) | 窓の UI は MVP にし、Presenter は Unity に依存しないアセンブリに置く | 採用 |

## 書き方

```markdown
---
status: 提案中 | 採用 | 置き換え済み(ADR NNNN)
sources:
  - 関係するファイル
---

# NNNN 判断を一文で

- 日付: YYYY-MM-DD

## 背景
## 決定
## 理由
## 引き換えにしたもの
## 関連
```
