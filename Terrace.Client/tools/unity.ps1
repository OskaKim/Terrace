#Requires -Version 7
# Unity をバッチで動かす道具。
#   tools/unity.ps1 -Setup      Main シーンを生成して Build Settings に登録する(コンパイル確認も兼ねる)
#   tools/unity.ps1 -EditMode   EditMode テストを実行する
#   tools/unity.ps1 -PlayMode   PlayMode テストを実行する(スクリーンショット Logs/smoke.png も出る)
#   tools/unity.ps1 -Open       Unity エディタでプロジェクトを開く
param(
    [switch]$Setup,
    [switch]$EditMode,
    [switch]$PlayMode,
    [switch]$Open,
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.6f1\Editor\Unity.exe'
)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot -Parent
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force $logs | Out-Null

function Invoke-Unity([string[]]$UnityArgs, [string]$LogName) {
    $log = Join-Path $logs $LogName
    $all = @('-batchmode', '-projectPath', $project, '-logFile', $log) + $UnityArgs
    $process = Start-Process -FilePath $UnityPath -ArgumentList $all -PassThru -Wait -NoNewWindow
    $errors = Select-String -Path $log -Pattern 'error CS\d+|Scripts have compiler errors|Exception:' -SimpleMatch:$false | Select-Object -First 15
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
if ($Open) {
    Start-Process -FilePath $UnityPath -ArgumentList @('-projectPath', $project)
    "opened: $project"
}
