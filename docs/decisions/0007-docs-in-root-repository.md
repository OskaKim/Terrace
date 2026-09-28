---
status: 採用
sources:
  - CLAUDE.md
  - docs/README.md
  - .gitignore
---

# 0007 文書はルートフォルダの git で管理し、AI 向けに書く

- 日付: 2026-09-16

## 背景

- 4 リポジトリをまたぐ設計や仕様の文書を置く場所が無かった。ルートの `README.md` と `ARCHITECTURE.md` は git の管理外で、指示書はさらに外(`../docs/for_ai_prompts/`)にあった
- 開発は主に AI が進める。コードは日々変わり、README の記述はすでに実装より遅れていた

## 決定

- ルートの `Terrace/` を git で管理する。4 リポジトリは `.gitignore` で除外し、文書(`CLAUDE.md`、`README.md`、`docs/`、`tools/`)だけを持つ
- 主な読み手は AI。人間向けには `docs/OVERVIEW.md` だけを用意する
- 文書には「変わりにくいもの」だけを書く: 判断(ADR)、境界の約束(contracts)、ゲームの規則(spec)、構造(architecture)
- 数値パラメータ・クラス一覧・テスト件数は書かず、コードを正とする
- 1 つの事実は 1 か所にだけ書く。各文書の front matter の `sources` に正となるファイルを書き、`tools/check-docs.ps1` で存在を検査する

## 理由

- Server の csproj がすでに「4 リポジトリが `Terrace/` の直下に並ぶ」前提なので、ルートは事実上の作業場になっている。文書を 5 つ目のリポジトリに分けるより自然
- AI は必要な文書だけを読むので、目的別の索引と「正の所在」があれば迷わない
- 古くなりやすいものを書かなければ、保つ手間が減る

## 引き換えにしたもの

- 各リポジトリを単独で clone したときには、全体の文書が付いてこない
- 各リポジトリの README と `docs/` に重なる記述が残っている。Client と Server の作業がコミットされた後で、README 側を削ってリンクに替える([roadmap.md](../roadmap.md))

## 関連

- [docs/README.md](../README.md)(文書の目次と書き方)
- ルートの `CLAUDE.md`(文書を保つ決まり)
