# Terrace

メイプルストーリーを手本にした 2D 横スクロール MORPG。4 つの独立した git リポジトリと、それらを束ねる文書からなる。

- **人間向けの概要:** [docs/OVERVIEW.md](docs/OVERVIEW.md)
- **AI 向けの決まり:** [CLAUDE.md](CLAUDE.md)(文書の目次は [docs/README.md](docs/README.md))

| リポジトリ | 内容 |
|---|---|
| [Terrace.MasterData](Terrace.MasterData/) | CSV を MasterMemory のバイナリ(master.bytes)に変える CLI。検証エラーを CSV の行まで逆引きする |
| [Terrace.Map](Terrace.Map/) | 線分(フットホールド)を繋いだマップ構造のライブラリ。問い合わせ API、JSON 入出力、検証 |
| [Terrace.Server](Terrace.Server/) | MagicOnion のゲームサーバー。ルームのロジックは純 C# |
| [Terrace.Client](Terrace.Client/) | Unity 6000.3 のクライアント。ひとりでも、Server に繋いで複数人でも遊べる |

## フォルダの前提

4 リポジトリは必ずこのフォルダの直下に兄弟として並べる。Server の csproj と Client の同期スクリプトが相対パスで互いを参照するため。

このフォルダ自体の git は文書(`CLAUDE.md`、`README.md`、`docs/`、`tools/`)だけを管理し、4 リポジトリは `.gitignore` で除外する。

## GitHub から取ってくる

GitHub では 5 つのリポジトリ(このフォルダの `Terrace` と 4 つの `Terrace.*`)に分かれている。

```bash
git clone https://github.com/<owner>/Terrace.git
cd Terrace
pwsh tools/clone-all.ps1
```

上げる側の手順は [docs/guides/setup.md](docs/guides/setup.md) の「GitHub へ上げる」(`tools/github-publish.ps1`)。

## 必要なもの

.NET SDK 10.0、Unity 6000.3.6f1、PowerShell 7。手順は [docs/guides/setup.md](docs/guides/setup.md)。
