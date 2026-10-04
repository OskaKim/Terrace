#Requires -Version 7
# タスク(GitHub の Issue)ごとに作業場を切って、実装から PR までを揃える道具。
# 1 タスク = 1 作業場(git worktree)= 1 ブランチ = 1 PR。作業場が分かれているので、複数の AI セッションが並列に作業できる。
#
#   pwsh tools/task.ps1 start 12       Issue #12 の作業場を作る(../Terrace-wt/12、ブランチ task/12)。Issue に着手の印を付ける
#   pwsh tools/task.ps1 list           作業場の一覧(Issue・ブランチ・試験用ポート・変更の有無)
#   pwsh tools/task.ps1 verify         今いる作業場で、変えたプロジェクトの検証をまとめて回す(結果は .task-verify.md)
#   pwsh tools/task.ps1 pr             今いる作業場のブランチを main に載せ直して push し、PR を作る
#   pwsh tools/task.ps1 finish 12      作業場とブランチを片付け、本体の main を最新にする
#   pwsh tools/task.ps1 sync           マージ済みのタスクをすべて片付け、本体の main を最新にする
#
# start も最初に sync と同じことをする。人間は PR をマージするだけでよく、片付けと本体の更新は次の start でまとめて行われる。
#
# 手順の全体は docs/guides/task-workflow.md。
param(
    [Parameter(Mandatory, Position = 0)]
    [ValidateSet('start', 'list', 'verify', 'pr', 'finish', 'sync')]
    [string]$Command,

    [Parameter(Position = 1)]
    [int]$Issue,

    # start: status:ready でない Issue や、着手済みの Issue でも作る / finish: マージ前でも片付ける
    [switch]$Force,
    # verify: Unity の試験を省く(Unity が要らない変更、または急ぎのとき。PR にその旨が残る)
    [switch]$SkipUnity,
    # pr: PR の本文を自分で書いたファイルにする(既定は自動で組み立てる)
    [string]$BodyFile,
    # pr: 下書きの PR にする
    [switch]$Draft
)
$ErrorActionPreference = 'Stop'
# gh と git は UTF-8 で出力する。Windows の既定(cp932)で読むと日本語の題やコミットの題が化ける
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$here = Split-Path $PSScriptRoot -Parent           # この道具がある作業場(本体か、task の作業場)
$taskFile = Join-Path $here '.task.json'
$verifyFile = Join-Path $here '.task-verify.md'

function Invoke-Git {
    $output = & git -C $here @args 2>&1
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') が失敗しました: $output" }
    return $output
}

function Get-MainWorktree {
    # git worktree list の先頭が本体
    $first = (& git -C $here worktree list --porcelain | Select-Object -First 1)
    return ($first -replace '^worktree ', '').Replace('/', [IO.Path]::DirectorySeparatorChar)
}

function Get-TaskRoot { return Join-Path (Split-Path (Get-MainWorktree) -Parent) 'Terrace-wt' }

function Get-Ports([int]$Number) {
    # 作業場ごとに試験用のポートを分ける(5000/5001 は手で立てるサーバー用に空けておく)
    $grpc = 5100 + 2 * ($Number % 400)
    return @{ Grpc = $grpc; Http = $grpc + 1 }
}

function Assert-Gh {
    if (-not (Get-Command gh -ErrorAction SilentlyContinue)) { throw 'GitHub CLI (gh) が要ります' }
}

# 本体(最初の worktree)の main を origin/main まで早送りする。本体が別のブランチを開いているか、
# 未コミットの変更があるときは触らない(fetch だけする)
function Update-MainCheckout {
    $main = Get-MainWorktree
    & git -C $main fetch -q origin 2>$null
    $branch = (& git -C $main branch --show-current).Trim()
    if ($branch -ne 'main') { "本体は $branch を開いているので、main の更新はしません(fetch だけしました)"; return }
    if (@(& git -C $main status --porcelain --untracked-files=no).Count -gt 0) { '本体に未コミットの変更があるので、main の更新はしません(fetch だけしました)'; return }
    $before = (& git -C $main rev-parse --short HEAD).Trim()
    $out = & git -C $main merge --ff-only -q origin/main 2>&1
    if ($LASTEXITCODE -ne 0) { "本体の main を早送りできませんでした: $out"; return }
    $after = (& git -C $main rev-parse --short HEAD).Trim()
    if ($before -eq $after) { '本体の main は最新です' } else { "本体の main を最新にしました($before → $after)" }
}

# タスクの作業場・ローカルとリモートのブランチ・着手の印を消す
function Remove-Task([int]$Number, [string]$State, [bool]$AllowDirty) {
    $branch = "task/$Number"
    $path = Join-Path (Get-TaskRoot) "$Number"
    if (Test-Path -LiteralPath $path) {
        if ([IO.Path]::GetFullPath($path).TrimEnd('', '/') -eq [IO.Path]::GetFullPath($here).TrimEnd('', '/')) { "#${Number}: 今いる作業場なので消しません(本体から実行してください)"; return }
        if (-not $AllowDirty -and @(& git -C $path status --porcelain --untracked-files=no).Count -gt 0) { "#${Number}: 作業場に未コミットの変更があるので残しました: $path"; return }
        # Unity の Library など無視しているファイルごと消す
        Invoke-Git worktree remove --force $path | Out-Null
    }
    & git -C $here branch -D $branch 2>$null | Out-Null
    if ($State -eq 'MERGED') { & git -C $here push -q origin --delete $branch 2>$null | Out-Null }
    & gh issue edit $Number --remove-label 'status:in-progress' 2>$null | Out-Null
    "#${Number}: 片付けました(作業場とブランチ $branch)"
}

# PR がマージされたタスクの作業場をすべて片付ける
function Clear-MergedTasks {
    $taskRoot = Get-TaskRoot
    if (-not (Test-Path -LiteralPath $taskRoot)) { return }
    foreach ($dir in Get-ChildItem -LiteralPath $taskRoot -Directory) {
        $number = 0
        if (-not [int]::TryParse($dir.Name, [ref]$number)) { continue }
        $state = (& gh pr view "task/$number" --json state --jq .state 2>$null)
        if ($state -eq 'MERGED') { Remove-Task $number $state $false }
    }
}

function Read-Task {
    if (-not (Test-Path -LiteralPath $taskFile)) { throw "ここはタスクの作業場ではありません($here)。pwsh tools/task.ps1 start <番号> で作ってから、その中で実行してください" }
    return Get-Content -LiteralPath $taskFile -Raw | ConvertFrom-Json
}

switch ($Command) {
    'start' {
        if ($Issue -le 0) { throw 'Issue の番号を指定してください(例: pwsh tools/task.ps1 start 12)' }
        Assert-Gh
        $info = & gh issue view $Issue --json number,title,state,labels,url | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or -not $info) { throw "Issue #$Issue を読めませんでした" }
        $labels = @($info.labels | ForEach-Object { $_.name })
        if ($info.state -ne 'OPEN') { throw "Issue #$Issue は閉じています" }
        if (-not $Force -and $labels -notcontains 'status:ready') { throw "Issue #$Issue に status:ready が付いていません(仕様が決まっていない)。決めてから付けるか、-Force" }
        if (-not $Force -and $labels -contains 'status:in-progress') { throw "Issue #$Issue は着手済みです(status:in-progress)。別のセッションが作業していないか確かめてください。続けるなら -Force" }

        # 先にマージ済みのタスクを片付けて、本体を最新にしておく
        Clear-MergedTasks
        Update-MainCheckout

        $branch = "task/$Issue"
        $taskRoot = Get-TaskRoot
        $path = Join-Path $taskRoot "$Issue"
        if (Test-Path -LiteralPath $path) { throw "作業場がもうあります: $path" }
        New-Item -ItemType Directory -Force $taskRoot | Out-Null

        Invoke-Git fetch -q origin | Out-Null
        $exists = & git -C $here branch --list $branch
        if ($exists) { Invoke-Git worktree add $path $branch | Out-Null }
        else { Invoke-Git worktree add -b $branch $path origin/main | Out-Null }

        $ports = Get-Ports $Issue
        [ordered]@{
            issue    = $Issue
            title    = $info.title
            url      = $info.url
            branch   = $branch
            grpcPort = $ports.Grpc
            httpPort = $ports.Http
            created  = (Get-Date).ToString('s')
        } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $path '.task.json') -Encoding utf8

        & gh issue edit $Issue --add-label 'status:in-progress' --remove-label 'status:ready' | Out-Null
        & gh issue comment $Issue --body "着手しました。作業場 ``Terrace-wt/$Issue``、ブランチ ``$branch``。" | Out-Null

        "作業場: $path"
        "ブランチ: $branch(origin/main から)"
        "試験用ポート: gRPC $($ports.Grpc) / HTTP $($ports.Http)"
        ""
        "次: その作業場で実装し、pwsh tools/task.ps1 verify → コミット → pwsh tools/task.ps1 pr"
        "Unity を使う検証の初回は、作業場ごとの Library を作るので数分かかります"
    }

    'list' {
        $entries = @()
        $current = $null
        foreach ($line in (& git -C $here worktree list --porcelain)) {
            if ($line -like 'worktree *') { $current = @{ Path = $line.Substring(9) }; $entries += $current }
            elseif ($line -like 'branch *') { $current.Branch = $line.Substring(7) -replace '^refs/heads/', '' }
        }
        $rows = foreach ($entry in $entries) {
            $file = Join-Path $entry.Path '.task.json'
            $task = if (Test-Path -LiteralPath $file) { Get-Content -LiteralPath $file -Raw | ConvertFrom-Json } else { $null }
            $dirty = @(& git -C $entry.Path status --porcelain).Count
            [pscustomobject]@{
                Issue  = if ($task) { "#$($task.issue)" } else { '(本体)' }
                Branch = $entry.Branch
                Ports  = if ($task) { "$($task.grpcPort)/$($task.httpPort)" } else { '' }
                Dirty  = $dirty
                Title  = if ($task) { $task.title } else { '' }
                Path   = $entry.Path
            }
        }
        $rows | Format-Table -AutoSize | Out-String -Width 200
    }

    'verify' {
        $task = if (Test-Path -LiteralPath $taskFile) { Read-Task } else { $null }
        Invoke-Git fetch -q origin | Out-Null
        $base = (Invoke-Git merge-base origin/main HEAD).Trim()
        $changed = @(& git -C $here diff --name-only $base) + @(& git -C $here ls-files --others --exclude-standard) | Sort-Object -Unique
        $top = @($changed | ForEach-Object { ($_ -split '/')[0] } | Sort-Object -Unique)
        $touches = { param($name) $top -contains $name }

        # 依存: Map と MasterData を変えたら Server も、共有コードを変えたら Client の Core も試す
        $runMasterData = & $touches 'Terrace.MasterData'
        $runMap = & $touches 'Terrace.Map'
        $runServer = (& $touches 'Terrace.Server') -or $runMap -or $runMasterData
        $runClient = (& $touches 'Terrace.Client') -or $runMap -or $runMasterData -or (& $touches 'Terrace.Server')
        $runUnity = (& $touches 'Terrace.Client') -and -not $SkipUnity

        $results = [System.Collections.Generic.List[object]]::new()
        # 終了コード 2 は「この PC では確かめられなかった」(Smart App Control がサーバーの DLL を止め、Docker も使えなかったなど)。失敗とは分けて記す
        function Step([string]$Name, [scriptblock]$Body) {
            "=== $Name"
            $global:LASTEXITCODE = 0
            $result = 'ok'
            try {
                & $Body
                if ($LASTEXITCODE -eq 2) { $result = 'BLOCKED(Smart App Control がサーバーを止め、Docker も使えなかった。サーバーの要る試験だけ省いた)' }
                elseif ($LASTEXITCODE -ne 0) { $result = 'FAILED' }
            }
            catch { $_.Exception.Message; $result = 'FAILED' }
            $results.Add([pscustomobject]@{ Step = $Name; Result = $result })
        }

        Step '文書の検査(check-docs)' { & pwsh -NoProfile -File (Join-Path $here 'tools/check-docs.ps1') }
        Step '複製の検査(check-sync)' { & pwsh -NoProfile -File (Join-Path $here 'tools/check-sync.ps1') -MasterData:($runMasterData) }
        if ($runMasterData) { Step 'Terrace.MasterData: dotnet test' { & dotnet test (Join-Path $here 'Terrace.MasterData') --nologo -v q } }
        if ($runMap) { Step 'Terrace.Map: dotnet test' { & dotnet test (Join-Path $here 'Terrace.Map') --nologo -v q } }
        if ($runServer) { Step 'Terrace.Server: dotnet test' { & dotnet test (Join-Path $here 'Terrace.Server') --nologo -v q } }
        if ($runClient) { Step 'Terrace.Client Core: dotnet test' { & dotnet test (Join-Path $here 'Terrace.Client/tests/Terrace.Client.Core.Tests') --nologo -v q } }
        $serverPorts = if ($task) { @('-GrpcPort', $task.grpcPort, '-HttpPort', $task.httpPort) } else { @('-GrpcPort', 5100, '-HttpPort', 5101) }
        if ($runServer) { Step 'Terrace.Server: テストクライアント 2 つで通信の経路' { & pwsh -NoProfile -File (Join-Path $here 'Terrace.Server/tools/e2e-testclients.ps1') @serverPorts } }
        if ($runUnity) {
            $clientTools = Join-Path $here 'Terrace.Client/tools'
            Step 'Terrace.Client: Unity EditMode' {
                $out = & pwsh -NoProfile -File (Join-Path $clientTools 'unity.ps1') -EditMode
                $out
                if (-not ($out -match 'failed=0')) { $global:LASTEXITCODE = 1 }
            }
            Step 'Terrace.Client: Unity PlayMode(サーバーを立てて 2 人接続まで)' {
                $out = & pwsh -NoProfile -File (Join-Path $clientTools 'e2e-online.ps1') @serverPorts
                $code = $LASTEXITCODE
                $out
                $global:LASTEXITCODE = if ($code -eq 2) { 2 } elseif ($out -match 'failed=0') { 0 } else { 1 }
            }
        }

        $failed = @($results | Where-Object Result -eq 'FAILED')
        $lines = @('| 検証 | 結果 |', '|---|---|') + @($results | ForEach-Object { "| $($_.Step) | $($_.Result) |" })
        if ((& $touches 'Terrace.Client') -and $SkipUnity) { $lines += ''; $lines += 'Unity の試験は省いた(-SkipUnity)。' }
        $lines += ''
        $lines += "変えたプロジェクト: $(if ($top.Count) { $top -join '、' } else { 'なし' })"
        $lines | Set-Content -LiteralPath $verifyFile -Encoding utf8
        ""
        $lines
        if ($failed.Count -gt 0) { throw "検証に失敗したものがあります: $($failed.Step -join '、')" }
    }

    'pr' {
        Assert-Gh
        $task = Read-Task
        if (@(& git -C $here status --porcelain).Count -gt 0) { throw '未コミットの変更があります。コミットしてから実行してください' }
        Invoke-Git fetch -q origin | Out-Null
        $behind = [int]((Invoke-Git rev-list --count HEAD..origin/main).Trim())
        if ($behind -gt 0) {
            "main が $behind コミット進んでいるので、載せ直します(rebase)"
            & git -C $here rebase origin/main
            if ($LASTEXITCODE -ne 0) {
                & git -C $here rebase --abort
                throw '載せ直しで衝突しました。git rebase origin/main を手で行って解消し、検証し直してから、もう一度 pr を実行してください'
            }
            "載せ直したので、検証をやり直してください(pwsh tools/task.ps1 verify)。済んだらもう一度 pr"
            return
        }
        if (@(& git -C $here rev-list origin/main..HEAD).Count -eq 0) { throw 'main から進んだコミットがありません' }

        $bodyPath = $BodyFile
        if (-not $bodyPath) {
            $commits = @(& git -C $here log --reverse --format='- %s' origin/main..HEAD)
            $verify = if (Test-Path -LiteralPath $verifyFile) { Get-Content -LiteralPath $verifyFile -Raw } else { '(pwsh tools/task.ps1 verify を実行していません)' }
            $bodyPath = Join-Path ([IO.Path]::GetTempPath()) "terrace-pr-$($task.issue).md"
            @(
                "Closes #$($task.issue)",
                '',
                '## 変更',
                '',
                $commits,
                '',
                '## 検証',
                '',
                $verify,
                '',
                '## 見てほしい所',
                '',
                '(実装した AI が書く。仕様から外れた判断、迷った所、手で遊んで確かめてほしい所)'
            ) | Set-Content -LiteralPath $bodyPath -Encoding utf8
        }

        Invoke-Git push -u origin $task.branch | Out-Null
        $title = $task.title -replace '^\[(task|idea)\]\s*', ''
        $prArgs = @('pr', 'create', '--base', 'main', '--head', $task.branch, '--title', "#$($task.issue) $title", '--body-file', $bodyPath)
        if ($Draft) { $prArgs += '--draft' }
        & gh @prArgs
        if ($LASTEXITCODE -ne 0) { throw 'PR を作れませんでした(同じブランチの PR が既にあるなら gh pr view で確かめる)' }
    }

    'finish' {
        if ($Issue -le 0) { throw 'Issue の番号を指定してください(例: pwsh tools/task.ps1 finish 12)' }
        Assert-Gh
        $state = (& gh pr view "task/$Issue" --json state --jq .state 2>$null)
        if (-not $Force -and $state -ne 'MERGED') { throw "ブランチ task/$Issue の PR がまだマージされていません($state)。片付けるなら -Force" }
        Remove-Task $Issue $state ([bool]$Force)
        Update-MainCheckout
    }

    'sync' {
        Assert-Gh
        Clear-MergedTasks
        Update-MainCheckout
    }
}
