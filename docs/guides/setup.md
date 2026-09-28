---
status: 手順
sources:
  - Terrace.Client/tools/nuget-restore.ps1
  - Terrace.Client/tools/sync-shared.ps1
  - Terrace.Client/tools/unity.ps1
  - Terrace.Client/ProjectSettings/ProjectVersion.txt
  - tools/clone-all.ps1
  - tools/github-publish.ps1
---

# 環境を作る

## 要るもの

| もの | 版 | 備考 |
|---|---|---|
| .NET SDK | 10.0 | すべての .NET プロジェクトが `net10.0`(Terrace.Shared は netstandard2.1) |
| Unity | 6000.3.6f1 | `tools/unity.ps1` は `C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe` を既定で使う。違う場所なら `-UnityPath` |
| PowerShell | 7 以上 | `tools/*.ps1` は `#Requires -Version 7` |
| NuGetForUnity CLI | 4.5.0 | `Assets/packages.config` を変えるときだけ要る。`dotnet tool install --global NuGetForUnity.Cli`。.NET 9 向けなので `nuget-restore.ps1` がロールフォワードを許可して動かす |
| Git | | |

## GitHub から取ってくる

GitHub には 5 つのリポジトリがある。文書のリポジトリ `Terrace` を取り、その中に残りの 4 つを並べる。

```bash
git clone https://github.com/<owner>/Terrace.git
cd Terrace
pwsh tools/clone-all.ps1
```

## フォルダを並べる

4 リポジトリを同じフォルダの直下に置く。名前も変えない(`tools/clone-all.ps1` がこの形に並べる)。

```
Terrace/
  Terrace.Client/
  Terrace.Map/
  Terrace.MasterData/
  Terrace.Server/
```

## 初回の手順

1. .NET の 3 リポジトリでテストを通す(NuGet の復元も兼ねる)

   ```bash
   cd Terrace.MasterData && dotnet test
   cd ../Terrace.Map && dotnet test
   cd ../Terrace.Server && dotnet test
   ```

2. Unity Hub で `Terrace.Client` を開き、`Assets/Scenes/Main.unity` を再生する。
   NuGet パッケージ(`Assets/Packages/`)、共有コードの複製、マップ、master.bytes、Main シーンはコミットしてあるので、取ってきたまま開ける。
   UPM の git パッケージ(NuGetForUnity、YetAnotherHttpHandler)は初回に Unity が取ってくる(Git が要る)

後から変えたときの手順:

| 変えたもの | 実行するもの(`Terrace.Client` で) |
|---|---|
| `Assets/packages.config` | `pwsh tools/nuget-restore.ps1` |
| 共有コード・マップ・CSV | `pwsh tools/sync-shared.ps1` |
| シーンの作り(`ProjectSetup`) | `pwsh tools/unity.ps1 -Setup` |

## GitHub へ上げる

`gh`(GitHub CLI)でログインしておき、ルートで実行する。5 つのリポジトリを作り(既定は private)、`main` を push する。未コミットの変更があれば何もせずに止まる。

```bash
pwsh tools/github-publish.ps1 -DryRun
pwsh tools/github-publish.ps1
```

## つまずきやすい所

- Unity のエディタで開いたまま `tools/unity.ps1` を走らせると、同じプロジェクトを 2 つ開けずに失敗する。`-Mirror` を付ける
- `CS0433`(型の二重定義)が出たら `tools/nuget-restore.ps1` をやり直す。Source Generator の DLL の meta を直している
- エディタを開くと NuGetForUnity がアナライザ DLL の meta を自分の形に書き直すことがある。コンパイルが通っていれば、その差分はそのままコミットしてよい
- Windows の Smart App Control が有効だと、Defender の照会がうまくいかない間、未署名の DLL(サーバーの MagicOnion やテストの DLL)の読み込みが `0x800711C7` で拒否されることがある。時間を置くと通る
