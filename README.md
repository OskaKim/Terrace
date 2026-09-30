# Terrace

メイプルストーリーを手本にした 2D 横スクロール MORPG。この 1 つのリポジトリに、4 つのプロジェクトと、それらを束ねる文書を置いている。

- **人間向けの概要:** [docs/OVERVIEW.md](docs/OVERVIEW.md)
- **AI 向けの決まり:** [CLAUDE.md](CLAUDE.md)(文書の目次は [docs/README.md](docs/README.md))

| プロジェクト | 内容 |
|---|---|
| [Terrace.MasterData](Terrace.MasterData/) | CSV を MasterMemory のバイナリ(master.bytes)に変える CLI。検証エラーを CSV の行まで逆引きする |
| [Terrace.Map](Terrace.Map/) | 線分(フットホールド)を繋いだマップ構造のライブラリ。問い合わせ API、JSON 入出力、検証 |
| [Terrace.Server](Terrace.Server/) | MagicOnion のゲームサーバー。ルームのロジックは純 C# |
| [Terrace.Client](Terrace.Client/) | Unity 6000.3 のクライアント。ひとりでも、Server に繋いで複数人でも遊べる |

## フォルダの前提

4 プロジェクトはこのフォルダの直下に兄弟として並ぶ。Server の csproj と Client の同期スクリプトが相対パスで互いを参照するので、場所と名前を変えない。

## 取ってくる

```bash
git clone https://github.com/OskaKim/Terrace.git
```

Windows ではパスが長くなるので、取る前に `git config --global core.longpaths true` を設定しておく。

## 必要なもの

.NET SDK 10.0、Unity 6000.3.6f1、PowerShell 7。手順は [docs/guides/setup.md](docs/guides/setup.md)。
