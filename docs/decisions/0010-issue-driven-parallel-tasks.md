---
status: 採用
sources:
  - tools/task.ps1
  - .github/ISSUE_TEMPLATE/task.yml
  - .github/workflows/ci.yml
  - docs/guides/task-workflow.md
---

# 0010 仕事の単位は GitHub の Issue。AI は作業場を切って PR まで進め、マージは人間だけが行う

- 日付: 2026-10-04

## 背景

複数の AI セッションに並列で実装させ、人間は仕様を決めることと PR をマージすることに集中したい。そのためには、どのセッションでも同じ動きになる手順と、セッション同士がぶつからない仕組みが要る。リポジトリは 1 つにまとめた([ADR 0009](0009-single-repository.md))。

## 決定

- **仕事の単位は GitHub の Issue。** 思いつきは「候補」(`status:idea`)、仕様が決まったものは「タスク」(`status:ready`)。タスクは Issue のフォームで目的・仕様・受け入れ条件・触る範囲・やらないこと・先に済ますタスクを書く
- **状態はラベルで持つ。** `status:idea` → `status:ready` → `status:in-progress` → PR のマージで Issue が閉じる。別の表(Projects など)は今は使わない
- **1 タスク = 1 作業場 = 1 ブランチ = 1 PR。** 作業場は `git worktree` で本体の隣(`Terrace-wt/N`)に切る。試験用のポートも作業場ごとに分ける。道具は `tools/task.ps1`
- **マージは人間だけ。** AI は main に直接 push せず、PR を作ったところで止まる
- **検証は 2 段。** Unity が要らないものは GitHub Actions で PR ごとに回す。Unity の試験は AI が作業場で回し、結果を PR に貼る
- **Client の Core を Unity なしでも試せるようにする。** Core のソースと Unity に依存しない EditMode テストを、そのまま .NET のテストプロジェクトに取り込む

## 理由

- Issue なら人間はブラウザでもスマホでも候補を足せ、AI は `gh` で読み書きできる。PR の `Closes #N` で、マージと同時に閉じる
- ラベルだけなら、ほかの道具や権限が要らない
- 作業場を分ければ、作業中のファイル・ビルド出力・Unity の Library・試験用のポートがセッション同士でぶつからない
- Unity のライセンスを CI に置かずに済む。ゲーム規則の大半は Core にあるので、CI で回る範囲が広い

## 引き換えにしたもの

- Unity の試験は AI の報告を信じることになる。報告の形(`.task-verify.md`)を決めて、PR に貼らせる
- 作業場ごとに Unity の Library ができ、ディスクと初回の時間を食う。Unity を同時に回せるのは 2〜3 作業場まで
- 同じ所を触るタスクは並列にできない。タスクを書く段階で、触る範囲と順番を決める必要がある
- main を保護した(2026-10-04)。人間も AI も main へ直接 push できず、文書の小さな直しも PR を通す。中身は [guides/task-workflow.md](../guides/task-workflow.md) の「main の保護」

## 関連

- [guides/task-workflow.md](../guides/task-workflow.md)(手順)
- [OVERVIEW.md](../OVERVIEW.md) の「開発の進め方」(人間向け)
