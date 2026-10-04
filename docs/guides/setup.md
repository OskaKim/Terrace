---
status: 手順
sources:
  - Terrace.Client/tools/nuget-restore.ps1
  - Terrace.Client/tools/sync-shared.ps1
  - Terrace.Client/tools/unity.ps1
  - Terrace.Client/ProjectSettings/ProjectVersion.txt
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

## 取ってくる

```bash
git config --global core.longpaths true
git clone https://github.com/OskaKim/Terrace.git
```

4 プロジェクトは 1 つのリポジトリの中に並んでいる。フォルダの場所と名前は変えない(相対パスで互いを参照する)。

```
Terrace/
  Terrace.Client/
  Terrace.Map/
  Terrace.MasterData/
  Terrace.Server/
```

`core.longpaths` は Windows 用。Client に同梱した NuGet パッケージのパスが長く、置き場所によっては 260 文字を超える。

## 初回の手順

1. .NET の 3 プロジェクトでテストを通す(NuGet の復元も兼ねる)

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

## つまずきやすい所

- Unity のエディタで開いたまま `tools/unity.ps1` を走らせると、同じプロジェクトを 2 つ開けずに失敗する。`-Mirror` を付ける
- `CS0433`(型の二重定義)が出たら `tools/nuget-restore.ps1` をやり直す。Source Generator の DLL の meta を直している
- エディタを開くと NuGetForUnity がアナライザ DLL の meta を自分の形に書き直すことがある。コンパイルが通っていれば、その差分はそのままコミットしてよい
- Windows の Smart App Control が有効だと、未署名の DLL(サーバーの MagicOnion やテストの DLL)の読み込みが `0x800711C7` で拒否されることがある。新しく置かれたファイルほど止められやすく、一度通ったファイルも後で止められることがある。サーバーが起動できないときは、サーバー側の確かめを CI(server-e2e)に任せる。Smart App Control を切るかはその PC の持ち主が決める(切ると戻せない)
