#Requires -Version 7
# Unity をバッチで動かす道具。
#   tools/unity.ps1 -Setup      Main シーンを生成して Build Settings に登録する(コンパイル確認も兼ねる)
#   tools/unity.ps1 -EditMode   EditMode テストを実行する
#   tools/unity.ps1 -PlayMode   PlayMode テストを実行する(スクリーンショット Logs/smoke*.png も出る)
#                               環境変数 TERRACE_SERVER (例 http://localhost:5000) があれば、オンラインのテストも実際に接続して走る
#   tools/unity.ps1 -Build      Windows 版を Build/Windows/Terrace.exe に書き出す(複数起動して多人数で試す)
#   tools/unity.ps1 -Open       Unity エディタでプロジェクトを開く
#   -Mirror                     エディタでこのプロジェクトを開いたまま検証したいとき。プロジェクトを一時フォルダへ複製して
#                               そちらでバッチを走らせ、Logs(結果 XML・ログ・スクリーンショット)を Logs/mirror/ に写す
param(
    [switch]$Setup,
    [switch]$EditMode,
    [switch]$PlayMode,
    [switch]$Build,
    [switch]$Open,
    [switch]$Mirror,
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$source = Split-Path $PSScriptRoot -Parent
$project = $source
$logs = Join-Path $source 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null

if ($Mirror) {
    $project = Join-Path ([IO.Path]::GetTempPath()) 'Terrace.Client-mirror'
    "mirror: $source -> $project"
    # Library は写さない(複製側が自分の Library を持ち続ける)。エディタで開いた本体の Library を別の場所へ写すと、
    # スクリプトとアセットの対応がずれてシーンにスクリプトが埋め込まれることがあった。初回だけ取り込みに時間がかかる
    & robocopy $source $project /MIR /XD Temp Logs obj Build Library .git .vs .idea /XF *.csproj *.sln /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit $LASTEXITCODE)" }
    $logs = Join-Path $project 'Logs'
    New-Item -ItemType Directory -Force $logs | Out-Null
}

function Invoke-Unity([string[]]$UnityArgs, [string]$LogName) {
    $log = Join-Path $logs $LogName
    $all = @('-batchmode', '-projectPath', $project, '-logFile', $log) + $UnityArgs
    # -Wait は子孫プロセス(Unity が残すコンパイルサーバー等)の終了まで待ってしまうため、Unity 本体の終了だけを待つ
    $process = Start-Process -FilePath $UnityPath -ArgumentList $all -PassThru -NoNewWindow
    $process.WaitForExit()
    $errors = Select-String -Path $log -Pattern 'error CS\d+|Scripts have compiler errors|another Unity instance' | Select-Object -First 15
    if ($errors) { "--- ${LogName}: エラー ---"; $errors | ForEach-Object { $_.Line } }
    return $process.ExitCode
}

function Show-TestResults([string]$Path) {
    if (-not (Test-Path $Path)) { "結果ファイルなし: $Path"; return }
    [xml]$xml = Get-Content $Path
    $run = $xml.'test-run'
    "tests: total=$($run.total) passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped) ($($run.duration)s)"
    $xml.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object {
        "  FAILED: $($_.fullname)"
        $msg = $_.failure.message.'#cdata-section'
        if ($msg) { "    " + ($msg -split "`n")[0] }
    }
}

if ($Setup) {
    $code = Invoke-Unity @('-quit', '-nographics', '-executeMethod', 'Terrace.Client.Editor.ProjectSetup.Run') 'setup.log'
    $setupCode = $code
    "setup: exit=$code"
}
if ($EditMode) {
    $results = Join-Path $logs 'editmode-results.xml'
    if (Test-Path $results) { Remove-Item $results }
    $code = Invoke-Unity @('-nographics', '-runTests', '-testPlatform', 'EditMode', '-testResults', $results) 'editmode.log'
    "editmode: exit=$code"
    Show-TestResults $results
}
if ($PlayMode) {
    $results = Join-Path $logs 'playmode-results.xml'
    if (Test-Path $results) { Remove-Item $results }
    $code = Invoke-Unity @('-runTests', '-testPlatform', 'PlayMode', '-testResults', $results) 'playmode.log'
    "playmode: exit=$code"
    Show-TestResults $results
}
if ($Build) {
    $code = Invoke-Unity @('-quit', '-executeMethod', 'Terrace.Client.Editor.ProjectSetup.BuildWindows') 'build.log'
    "build: exit=$code"
    Select-String -Path (Join-Path $logs 'build.log') -Pattern '^\[build\]' | ForEach-Object { $_.Line }
    if ($Mirror) {
        $built = Join-Path $project 'Build'
        if (Test-Path $built) {
            & robocopy $built (Join-Path $source 'Build') /MIR /NFL /NDL /NJH /NJS /NP /R:1 /W:1 | Out-Null
            "build copied to $(Join-Path $source 'Build')"
        }
    }
}
if ($Open) {
    Start-Process -FilePath $UnityPath -ArgumentList @('-projectPath', $source)
    "opened: $source"
}

if ($Mirror -and ($Setup -or $EditMode -or $PlayMode -or $Build)) {
    $back = Join-Path $source 'Logs\mirror'
    New-Item -ItemType Directory -Force $back | Out-Null
    Copy-Item (Join-Path $logs '*') $back -Force
    if ($Setup -and $setupCode -eq 0 -and (Test-Path (Join-Path $project 'Assets\Scenes\Main.unity'))) {
        # 複製側で生成したシーンを本体へ写す(本体のエディタが開いていても、ファイルは次の再読み込みで取り込まれる)
        New-Item -ItemType Directory -Force (Join-Path $source 'Assets\Scenes') | Out-Null
        Copy-Item (Join-Path $project 'Assets\Scenes\Main.unity*') (Join-Path $source 'Assets\Scenes') -Force
    }
    "logs copied to $back"
}
