#Requires -Version 7
# 文書の鮮度を機械的に確かめる。
#   - docs/**/*.md の front matter に status と sources があるか
#   - sources に書いたパス(Terrace/ からの相対)が実在するか
#   - ルートの *.md と docs/**/*.md の相対リンクが切れていないか(コードブロックの中は見ない)
# 問題があれば一覧を出して終了コード 1。
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$problems = [System.Collections.Generic.List[string]]::new()

# front matter を持たなくてよい文書(他所から写した原文など)
$noFrontMatter = @('docs/prompts/fable-prompts.md')

function Get-RelativePath([string]$Path) {
    return [IO.Path]::GetRelativePath($root, $Path).Replace('\', '/')
}

function Read-FrontMatter([string[]]$Lines) {
    if ($Lines.Count -eq 0 -or $Lines[0].Trim() -ne '---') { return $null }
    $result = @{ status = $null; sources = [System.Collections.Generic.List[string]]::new() }
    $inSources = $false
    for ($i = 1; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]
        if ($line.Trim() -eq '---') { return $result }
        if ($line -match '^status:\s*(.+)$') { $result.status = $Matches[1].Trim(); $inSources = $false; continue }
        if ($line -match '^sources:\s*$') { $inSources = $true; continue }
        if ($inSources -and $line -match '^\s+-\s+(.+)$') { $result.sources.Add($Matches[1].Trim()); continue }
        if ($line -match '^\S') { $inSources = $false }
    }
    return $null   # 閉じの --- が無い
}

$docs = @(Get-ChildItem (Join-Path $root 'docs') -Recurse -Filter *.md)
$rootDocs = @(Get-ChildItem $root -Filter *.md -File)

foreach ($file in $docs) {
    $relative = Get-RelativePath $file.FullName
    if ($noFrontMatter -contains $relative) { continue }
    $lines = @(Get-Content -LiteralPath $file.FullName -Encoding utf8)
    $front = Read-FrontMatter $lines
    if ($null -eq $front) { $problems.Add("${relative}: front matter がありません"); continue }
    if ([string]::IsNullOrWhiteSpace($front.status)) { $problems.Add("${relative}: front matter に status がありません") }
    if ($front.sources.Count -eq 0) { $problems.Add("${relative}: front matter に sources がありません") }
    foreach ($source in $front.sources) {
        if (-not (Test-Path (Join-Path $root $source))) { $problems.Add("${relative}: sources のパスがありません: $source") }
    }
}

foreach ($file in $docs + $rootDocs) {
    $relative = Get-RelativePath $file.FullName
    $inFence = $false
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding utf8) {
        $lineNumber++
        if ($line.TrimStart().StartsWith('```')) { $inFence = -not $inFence; continue }
        if ($inFence) { continue }
        foreach ($match in [regex]::Matches($line, '\]\(([^)\s]+)\)')) {
            $target = $match.Groups[1].Value
            if ($target -match '^(https?:|mailto:|#)') { continue }
            $target = ($target -split '#')[0] -replace ':\d+$', ''
            if ([string]::IsNullOrEmpty($target)) { continue }
            $resolved = Join-Path $file.DirectoryName ([Uri]::UnescapeDataString($target))
            if (-not (Test-Path -LiteralPath $resolved)) { $problems.Add("${relative}:${lineNumber}: リンク先がありません: $target") }
        }
    }
}

if ($problems.Count -gt 0) {
    "=== 問題 ($($problems.Count) 件) ==="
    $problems | ForEach-Object { $_ }
    exit 1
}
"ok: $($docs.Count + $rootDocs.Count) 文書を確認しました"
