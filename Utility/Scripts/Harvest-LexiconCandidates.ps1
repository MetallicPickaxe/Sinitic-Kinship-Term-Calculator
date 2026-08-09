# Harvests candidate "other names" from the mumuy corpus we already ship as a comparison
# oracle, and applies the DETERMINISTIC filters, producing a review table for adjudication.
#
# Why this exists: the lexicon layers cover 157 terms, so the relations a user clicks first
# (父親/母親/兄弟姐妹/子女/配偶…) show an EMPTY "Other names" column even though the mumuy
# term set on disk lists 35 names for 父 alone. The corpus is the source; the work is
# separating real regional/colloquial variants from the noise it is mixed with.
#
# This script NEVER writes a lexicon file. It emits a review table; layer assignment is a
# judgement call made by a human/reviewer afterwards.
#
# Filters applied here are only the ones a machine can decide:
#   - already covered by our lexicon, or identical to our own standard form
#   - collective terms (父母/爹娘/兄弟姐妹…: they denote a GROUP, not this one person)
#   - birth-order labels (长子/次女/长孙…: rank, not an alternative name)
#   - cartesian synthesis (堂哥/堂老兄/堂哥哥/堂阿哥/同堂哥…: mumuy generates prefix x core
#     products; only a few are actually current)
#   - collisions with another relation's standard form (would make a variant ambiguous)
# Everything surviving is REPORTED, not accepted.
param(
    [string]$Face = 'Resource\Data\Reference\MumuyMainAccuracyCompact.tsv',
    [string]$OutFile = (Join-Path ([IO.Path]::GetTempPath()) 'LEXICON_PILOT_REVIEW.md'),
    [string[]]$Chains = @(
        'F','M','OB','YB','OS','YS','S','D','SP',
        'F.F','F.M','M.F','M.M',
        'F.OB','F.YB','F.OS','F.YS','M.OB','M.YB','M.OS','M.YS',
        'OB.S','OB.D','OS.S','OS.D',
        'S.S','S.D','D.S','D.D',
        'S.SP','D.SP','SP.F','SP.M',
        'F.OB.S','M.OB.S'
    )
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$facePath = Join-Path $repoRoot $Face

# ---- Hans/Hant normalisation, PARSED FROM THE ENGINE'S OWN TABLE so the two can never
# drift apart. Without this the harvester offers 父亲 as a "candidate" for 父親 — the same
# word in the other script — and every such pair pollutes the adjudication pile.
$converter = Join-Path $repoRoot 'KinshipCalculator.Core\Services\Formatting\KinshipScriptConverter.cs'
$hantToHans = @{}
foreach ($line in (Get-Content $converter -Encoding utf8)) {
    if ($line -match "^\s*\('(.)',\s*'(.)'\)") { $hantToHans[$Matches[1]] = $Matches[2] }
}
if ($hantToHans.Count -lt 10) { throw "Could not parse the script converter table ($($hantToHans.Count) pairs)" }
function ConvertTo-Hans([string]$s) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $s.ToCharArray()) {
        $k = [string]$ch
        [void]$sb.Append($(if ($hantToHans.ContainsKey($k)) { $hantToHans[$k] } else { $k }))
    }
    return $sb.ToString()
}

# ---- what we already have (keys AND values across every layer)
$have = New-Object System.Collections.Generic.HashSet[string]
foreach ($f in (Get-ChildItem (Join-Path $repoRoot 'Resource\Data\Lexicon') -Filter '*.yaml')) {
    foreach ($line in (Get-Content $f.FullName -Encoding utf8)) {
        if ($line -match '^\s{2}(\S+?):\s*\[(.*)\]\s*$') {
            [void]$have.Add((ConvertTo-Hans $Matches[1]))
            foreach ($v in ($Matches[2] -split ',')) { [void]$have.Add((ConvertTo-Hans $v.Trim())) }
        }
        elseif ($line -match '^\s+(male|female):\s*(\S+)\s*$') { [void]$have.Add((ConvertTo-Hans $Matches[2])) }
    }
}

# ---- collective terms: a name for a GROUP of people, never a variant of one person
$collectivePath = Join-Path $repoRoot 'Resource\Data\Reference\KinshipCollectiveTerms.tsv'
$collective = New-Object System.Collections.Generic.HashSet[string]
if (Test-Path $collectivePath) {
    foreach ($line in (Get-Content $collectivePath -Encoding utf8 | Select-Object -Skip 1)) {
        $c = $line -split "`t"
        if ($c.Count -ge 2 -and $c[1].Trim()) { [void]$collective.Add($c[1].Trim()) }
    }
}
# The shipped list is short; these are the collective SHAPES the corpus actually uses.
$collectivePatterns = @(
    '^(父母|爹娘|爹妈|爸妈|爸爸妈妈|父母亲|双亲|二亲|两亲|二老|高堂)$',
    '兄弟姐妹$', '姐妹兄弟$', '^同胞', '^手足$', '哥哥姐姐$', '弟弟妹妹$', '哥哥嫂嫂$', '^兄嫂$',
    '^(妻儿|妻小|妻女|子女|儿女|仔女|孩子们|儿辈|子辈)$',
    '^(侄甥|侄子女|侄子侄女|甥子女|外甥子女|孙子女|孙辈|孙息|孙枝)$',
    '^(夫妻|夫妇|伉俪|两口子)$'
)
# birth-order / seniority labels, not alternative names
$orderPatterns = @('^(长|次|幼|嫡|庶)(子|女|兄|弟|姐|妹|孙|孙女|媳)$', '^(元兄|长兄|长姐|长孙|长子|长女)$')

# ---- the key the lexicon is actually consulted with.
# NOT the face's `our_daily_folk_1st_candidate_male`: that column is "Label | AlternateLabel",
# whose first field is the DISPLAY primary. Keying a layer on it produces entries the engine
# never queries (伯伯 instead of 伯父) — silent dead data, which is what happened on the first
# pilot batch. The authority is the engine itself: Test-Verification's reachability sweep writes
# the standard form emitted per chain, and that is what a layer must be keyed by.
$probe = Get-ChildItem (Join-Path $repoRoot 'Test-Verification\bin') -Recurse -Filter 'lexicon-reachable-standards.tsv' -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $probe) {
    throw "Engine probe table missing under Test-Verification\bin.`nRun Test-Verification's EveryShippedVariantKey_ActuallySurfacesInBothScripts first."
}
Write-Output "Engine probe table: $($probe.FullName) ($($probe.LastWriteTime))"
$probe = $probe.FullName
$symbolToToken = @{
    F = 'father'; M = 'mother'; SP = 'spouse'
    OB = 'older-brother'; YB = 'younger-brother'; OS = 'older-sister'; YS = 'younger-sister'
    S = 'son'; D = 'daughter'
}
$engineStandard = @{}
foreach ($line in (Get-Content $probe -Encoding utf8 | Select-Object -Skip 1)) {
    $c = $line -split "`t"
    if ($c.Count -lt 3 -or $c[1] -ne 'Male') { continue }
    if (-not $engineStandard.ContainsKey($c[0])) { $engineStandard[$c[0]] = ($c[2] -split '\|')[0].Trim() }
}
function Get-EngineStandard([string]$symbolPath) {
    $key = ($symbolPath -split '\.' | ForEach-Object { $symbolToToken[$_] }) -join '.'
    return $engineStandard[$key]
}

# ---- our own standard form per chain, from the audited face
$rows = @{}
$header = (Get-Content $facePath -First 1 -Encoding utf8) -split "`t"
$idx = @{}
for ($i = 0; $i -lt $header.Count; $i++) { $idx[$header[$i]] = $i }
foreach ($line in (Get-Content $facePath -Encoding utf8 | Select-Object -Skip 1)) {
    $c = $line -split "`t"
    $chain = $c[$idx['chain_symbol_path']]
    if (-not $rows.ContainsKey($chain)) { $rows[$chain] = $c }
}

# every standard form in the whole face, to detect collisions
$allStandards = New-Object System.Collections.Generic.HashSet[string]
foreach ($c in $rows.Values) {
    $t = ($c[$idx['our_daily_folk_1st_candidate_male']] -split '\|')[0].Trim()
    if ($t) { [void]$allStandards.Add((ConvertTo-Hans $t)) }
}

$doc = New-Object System.Collections.Generic.List[string]
$doc.Add('# Lexicon pilot — candidate review table')
$doc.Add('')
$doc.Add('Generated by `Utility\Scripts\Harvest-LexiconCandidates.ps1` from the mumuy term sets')
$doc.Add('already on disk (the comparison oracle). **Nothing here is in the lexicon yet** — this')
$doc.Add('is the adjudication pile. Machine filters removed what a machine can decide; every')
$doc.Add('remaining term still needs a layer decision (通用口語 / 北系 / 南系 / 書面·古語 / 排除).')
$doc.Add('')
$doc.Add('Legend: **CAND** = survives all machine filters. `dup` already in lexicon · `self` equals')
$doc.Add('our standard · `coll` collective term · `rank` birth-order · `synth` cartesian synthesis ·')
$doc.Add('`clash` collides with another relation''s standard form.')
$doc.Add('')

$uncovered = New-Object System.Collections.Generic.List[string]
$totalCand = 0; $totalSeen = 0
$stats = @{ dup = 0; self = 0; coll = 0; rank = 0; synth = 0; clash = 0; cand = 0 }

foreach ($chain in $Chains) {
    if (-not $rows.ContainsKey($chain)) { $doc.Add("## $chain — (not in the face)"); $doc.Add(''); continue }
    $c = $rows[$chain]
    $standard = Get-EngineStandard $chain
    if (-not $standard) {
        # The probe sweeps a DECLARED corpus (see LexiconKeyReachabilityTests), so a chain deeper
        # than it reaches has no recorded key. Skipping is correct — inventing a key is what the
        # first pilot batch did — but it is REPORTED, never silent, so the gap stays visible.
        $uncovered.Add($chain)
        continue
    }
    $standardF = ($c[$idx['our_daily_folk_1st_candidate_female']] -split '\|')[0].Trim()
    $terms = @($c[$idx['mumuy_term_set']] -split '\|' | ForEach-Object { $_.Trim() } | Where-Object { $_ } | Select-Object -Unique)
    $totalSeen += $terms.Count

    # cartesian-synthesis detection: split each term into (prefix, core) at every position and
    # count how many distinct prefixes share a core and vice versa. A term whose prefix AND
    # core are both high-frequency inside this chain's own set is almost certainly generated.
    $prefixCount = @{}; $coreCount = @{}
    foreach ($t in $terms) {
        for ($k = 1; $k -lt $t.Length; $k++) {
            $p = $t.Substring(0, $k); $s = $t.Substring($k)
            $prefixCount[$p] = 1 + ($prefixCount[$p] ?? 0)
            $coreCount[$s] = 1 + ($coreCount[$s] ?? 0)
        }
    }

    $kept = New-Object System.Collections.Generic.List[string]
    $dropped = New-Object System.Collections.Generic.List[string]
    foreach ($t in $terms) {
        $why = $null
        $tn = ConvertTo-Hans $t
        if ($tn -eq (ConvertTo-Hans $standard) -or $tn -eq (ConvertTo-Hans $standardF)) { $why = 'self' }
        elseif ($have.Contains($tn)) { $why = 'dup' }
        elseif ($collective.Contains($tn)) { $why = 'coll' }
        if (-not $why) { foreach ($p in $collectivePatterns) { if ($t -match $p) { $why = 'coll'; break } } }
        if (-not $why) { foreach ($p in $orderPatterns) { if ($t -match $p) { $why = 'rank'; break } } }
        if (-not $why -and $t.Length -ge 3) {
            for ($k = 1; $k -lt $t.Length; $k++) {
                $p = $t.Substring(0, $k); $s = $t.Substring($k)
                if (($prefixCount[$p] ?? 0) -ge 4 -and ($coreCount[$s] ?? 0) -ge 4) { $why = 'synth'; break }
            }
        }
        if (-not $why -and $allStandards.Contains($tn)) { $why = 'clash' }

        if ($why) { $dropped.Add("$t`:$why"); $stats[$why]++ }
        else { $kept.Add($t); $stats['cand']++ }
    }
    $totalCand += $kept.Count

    $doc.Add("## $chain — standard: **$standard**$(if ($standardF -and $standardF -ne $standard) { " / 女方 **$standardF**" })")
    $doc.Add('')
    $doc.Add("mumuy 詞數 $($terms.Count) → 機器過濾後 **$($kept.Count)** 待裁決")
    $doc.Add('')
    if ($kept.Count -gt 0) {
        $doc.Add('| 候選 | 建議層 | 理由 |')
        $doc.Add('|---|---|---|')
        foreach ($t in $kept) { $doc.Add("| $t |  |  |") }
    }
    else { $doc.Add('*(無)*') }
    $doc.Add('')
    if ($dropped.Count -gt 0) {
        $doc.Add("<details><summary>機器已濾除 $($dropped.Count) 條</summary>`n`n``$($dropped -join '  ·  ')```n`n</details>")
        $doc.Add('')
    }
}

$doc.Insert(11, "**Pilot 統計:$($Chains.Count) 條關係,mumuy 原始 $totalSeen 詞 → 待裁決 $totalCand 條。** " +
    "機器濾除:已收 $($stats['dup']) · 同標準形 $($stats['self']) · 群稱 $($stats['coll']) · 排行 $($stats['rank']) · 合成 $($stats['synth']) · 撞名 $($stats['clash'])。")
$doc.Insert(12, '')

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $repoRoot $OutFile), ($doc -join "`n") + "`n", $utf8NoBom)
Write-Output "Review table: $OutFile  ($($Chains.Count) chains, $totalSeen raw terms -> $totalCand to adjudicate)"
Write-Output "  filtered: dup=$($stats['dup']) self=$($stats['self']) collective=$($stats['coll']) rank=$($stats['rank']) synthetic=$($stats['synth']) clash=$($stats['clash'])"
if ($uncovered.Count -gt 0) {
    Write-Output "  NOT HARVESTED — outside the engine probe corpus ($($uncovered.Count) chains): $(($uncovered | Select-Object -First 12) -join ', ')$(if ($uncovered.Count -gt 12) { ' …' })"
}
