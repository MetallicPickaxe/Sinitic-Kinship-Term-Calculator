# Rebuilds MumuyMainAccuracyCompact.xlsx from the freshly exported compact TSVs.
# Flow: raw compact TSV -> review-input JSON -> ReferenceAccuracyExporter (judgment pass)
#      -> merged 17-column judged TSV -> pack_tsv_pair_to_xlsx.py -> xlsx (+ .pre-refresh.bak).
# Prerequisites: `py` launcher available.
# ExporterPath is REQUIRED-by-default from the caller (Run-ValidationLoop passes the exact
# binary it just built): the old hard-coded bin\Debug path silently picked up a STALE
# exporter while the loop built bin\x64\Debug, and the 438 face reported an old engine's
# judgments as green for two whole repair rounds. When called standalone with no path, the
# newest exe under bin is used — and its age is printed so staleness is visible.
param(
    [string]$ExporterPath = '',
    [int]$MaxMismatch = -1
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$referenceDir = Join-Path $repoRoot 'Resource\Data\Reference'
$compactTsv = Join-Path $referenceDir 'MumuyMainAccuracyCompact.tsv'
$unsupportedTsv = Join-Path $referenceDir 'MumuyMainAccuracyCompact.Unsupported.tsv'
$workbook = Join-Path $referenceDir 'MumuyMainAccuracyCompact.xlsx'
$backup = "$workbook.pre-refresh.bak"
$packScript = Join-Path $repoRoot 'Utility\Scripts\pack_tsv_pair_to_xlsx.py'

if ($ExporterPath) {
    $exporter = $ExporterPath
} else {
    $newest = Get-ChildItem (Join-Path $repoRoot 'Utility\ReferenceAccuracyExporter\bin') -Recurse -Filter 'ReferenceAccuracyExporter.exe' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $newest) { throw 'Exporter not built (no ReferenceAccuracyExporter.exe under bin)' }
    $exporter = $newest.FullName
    Write-Output "Exporter (newest, standalone mode): $exporter ($($newest.LastWriteTime))"
}

if (-not (Test-Path $exporter)) { throw "Exporter not found: $exporter" }
if (-not (Test-Path $compactTsv)) { throw "Compact TSV missing (run the exporter default pass first): $compactTsv" }

$work = Join-Path ([IO.Path]::GetTempPath()) ("judged-workbook-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $work | Out-Null

try {
    # 1. Raw TSV -> review-input JSON (TableRowNumber / ChainSymbolPath / MumuyTermSet).
    $rows = Get-Content $compactTsv -Encoding UTF8
    $header = $rows[0] -split "`t"
    $idx = @{}
    for ($i = 0; $i -lt $header.Count; $i++) { $idx[$header[$i]] = $i }

    $inputRows = foreach ($line in ($rows | Select-Object -Skip 1)) {
        $c = $line -split "`t"
        [pscustomobject]@{
            TableRowNumber = [int]$c[$idx['table_row_number']]
            ChainSymbolPath = $c[$idx['chain_symbol_path']]
            MumuyTermSet = $c[$idx['mumuy_term_set']]
        }
    }

    $inputJson = Join-Path $work 'review-input.json'
    $outputJson = Join-Path $work 'review-output.json'
    $inputRows | ConvertTo-Json -Depth 3 | Set-Content $inputJson -Encoding UTF8

    # 2. Judgment pass.
    & $exporter --workbook-review-input $inputJson --workbook-review-output $outputJson | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Exporter review pass failed with $LASTEXITCODE" }
    $review = Get-Content $outputJson -Encoding UTF8 | ConvertFrom-Json
    $reviewByRow = @{}
    foreach ($r in $review) { $reviewByRow[[int]$r.TableRowNumber] = $r }

    # 3. Merge into the 17-column judged layout the workbook uses.
    $judgedTsv = Join-Path $work 'MumuyMainAccuracyCompact.judged.tsv'
    $out = New-Object System.Collections.Generic.List[string]
    $out.Add((@(
        '表内行号（索引，不更新）', '参考源行号集（参考，不更新）', 'Mumuy原始键集（参考，不更新）',
        'Mumuy链选择器集（参考，不更新）', '标准链路径（参考，不更新）', 'Mumuy称谓集合（参考，不更新）',
        '程序输出_男_正式或回退（程序刷新）', '程序输出_男_日常首选（程序刷新）', '程序输出_男_日常其他候选（程序刷新）', '程序输出_男_exact_match（程序刷新）',
        '程序输出_女_正式或回退（程序刷新）', '程序输出_女_日常首选（程序刷新）', '程序输出_女_日常其他候选（程序刷新）', '程序输出_女_exact_match（程序刷新）',
        'Mumuy主称谓候选（参考，不更新）', '我们的推断称谓（程序刷新）', '对照判定（程序刷新）'
    ) -join "`t"))

    foreach ($line in ($rows | Select-Object -Skip 1)) {
        $c = $line -split "`t"
        $rowNo = [int]$c[$idx['table_row_number']]
        $r = $reviewByRow[$rowNo]
        if ($null -eq $r) { throw "No review output for row $rowNo" }
        $out.Add((@(
            $c[$idx['table_row_number']], $c[$idx['source_sheet_row_numbers']], $c[$idx['raw_key_set']],
            $c[$idx['chain_selector_set']], $c[$idx['chain_symbol_path']], $c[$idx['mumuy_term_set']],
            $r.OurOfficialOrFallbackMale, $r.OurDailyFolk1stCandidateMale, $r.OurDailyFolkOthersMale, ([int][bool]$r.OurIsExactMatchMale),
            $r.OurOfficialOrFallbackFemale, $r.OurDailyFolk1stCandidateFemale, $r.OurDailyFolkOthersFemale, ([int][bool]$r.OurIsExactMatchFemale),
            $c[$idx['mumuy_primary_term_candidates']], $r.CandidateDisplay, $r.JudgmentDisplay
        ) -join "`t"))
    }

    [IO.File]::WriteAllLines($judgedTsv, $out, (New-Object System.Text.UTF8Encoding($false)))

    # 4. Backup + pack.
    if (Test-Path $workbook) { Copy-Item $workbook $backup -Force }
    & py $packScript --compact $judgedTsv --unsupported $unsupportedTsv --output $workbook | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "pack_tsv_pair_to_xlsx failed with $LASTEXITCODE" }

    # 5. Judgment distribution summary.
    $dist = @{}
    foreach ($r in $review) {
        $v = [string]$r.JudgmentDisplay
        $p = '其他'
        foreach ($k in @('可接受簡寫', '不一致', '一致', '已收編', '拒收')) { if ($v.StartsWith($k)) { $p = $k; break } }
        $dist[$p] = 1 + ($dist[$p] ?? 0)
    }
    'Judgment distribution:'
    $dist.GetEnumerator() | Sort-Object Value -Descending | ForEach-Object { '  {0} × {1}' -f $_.Key, $_.Value }

    # Row-count seal: the face is exactly the curated 438; anything else means the
    # exporter judged a different table than the one this workbook claims to be.
    if ($review.Count -ne 438) {
        throw "438 GATE FAILED: review row count $($review.Count) != 438"
    }

    # Split metrics (release-audit round 2): a primary-answer mismatch whose reference
    # term is still served among our candidates (候選命中) is disclosed, not hidden
    # inside the acceptable bucket; the hard gate is on genuine served misses.
    $servedMiss = [int]($dist['不一致'] ?? 0)
    $candidateHit = @($review | Where-Object { [string]$_.JudgmentDisplay -like '*候選命中*' }).Count
    Write-Output "438 primary-answer mismatches: $($servedMiss + $candidateHit) (of which $candidateHit candidate-served)"
    Write-Output "438 served misses: $servedMiss$(if ($MaxMismatch -ge 0) { " (gate <= $MaxMismatch)" })"
    if ($MaxMismatch -ge 0 -and $servedMiss -gt $MaxMismatch) {
        throw "438 GATE FAILED: $servedMiss served misses > $MaxMismatch"
    }
}
finally {
    Remove-Item $work -Recurse -Force -ErrorAction SilentlyContinue
}
