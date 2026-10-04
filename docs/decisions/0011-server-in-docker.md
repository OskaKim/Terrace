---
status: 採用
sources:
  - Terrace.Server/Dockerfile
  - .dockerignore
  - Terrace.Server/tools/TerraceServer.psm1
  - Terrace.Server/tools/server-docker.ps1
  - .github/workflows/ci.yml
---

# 0011 開発する PC では、サーバーを Docker のコンテナでも動かせるようにする

- 日付: 2026-10-04

## 背景

Windows の Smart App Control は、署名の無い DLL を読み込むたびに Microsoft の評判照会で許すかを決める。サーバーが使う `MagicOnion.Server.dll` は署名が無い。
同じファイルが 9/4〜9/30 は通り、10/4 から `0x800711C7` で拒否されるようになった(Code Integrity のログのイベント 3033)。判定は日によって変わり、個別に許す仕組みも無い。

その結果、開発に使う PC でサーバーが起動せず、人間が手でオンラインを遊ぶことも、`task.ps1 verify` のサーバーの要る試験もできなくなった。
Smart App Control を切れば直るが、守りが弱まるうえ、従来の仕様では一度切ると Windows を入れ直さないと戻せない。

## 決定

- サーバーを Linux のコンテナで動かす像を用意する(`Terrace.Server/Dockerfile`)。Linux の中は Smart App Control の対象にならない
- 人間が遊ぶときは `Terrace.Server/tools/server-docker.ps1` で立てる。Unity とテストクライアントは今まで通り Windows で動かし、`localhost` のポートでコンテナに繋ぐ
- 試験の道具(`e2e-testclients.ps1`、`e2e-online.ps1`)は、まず手元の dotnet でサーバーを立て、Smart App Control に止められたときだけ Docker で立て直す。Docker も使えなければ今まで通り `BLOCKED`
- 立て方の共通処理は `Terrace.Server/tools/TerraceServer.psm1` に置く
- CI に、Docker の像でサーバーを立てて同じ e2e を回すジョブを足す
- これは開発のための手段で、配備の仕組みではない。レジストリへの push や本番の構成は決めていない

## 理由

- 保安の設定に触れずに済む。Smart App Control をどうするかは PC の持ち主の判断のまま残せる
- この PC では Docker Desktop が既に動いていた
- 手元の dotnet を先に試すので、Smart App Control の無い PC や CI では今までと同じ動きのまま
- サーバーのコードは変えずに済んだ(全インターフェースで待ち受ける `ListenAnyIP` は既にあった)

## 引き換えにしたもの

- Docker で立てるときは像を作り直すので、手元の dotnet より起動が遅い(ソースを変えた後は数十秒、初回は .NET の像を取ってくるので数分)
- `verify` の結果が `ok` でも、サーバーは手元の Windows ではなくコンテナで動いていたことがある(出力に「Docker のコンテナ」と出る)
- 像とビルドのキャッシュがディスクを使う

## 関連

- [guides/run-and-test.md](../guides/run-and-test.md)(動かし方)
- [guides/setup.md](../guides/setup.md)(つまずきやすい所)
- [ADR 0008](0008-online-client-inbox.md)(同じ理由で MagicOnion のクライアント生成器に頼らない)
