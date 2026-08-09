# Builds the blind-classification probe for the held-back lexicon candidates, and scores the
# answers that come back.
#
# WHY BLIND. 70 harvested terms were adjudicated `low` confidence — I could not tell which
# region or register they belong to, only that they looked plausible. Shipping a guess as a
# regional attribution is exactly the failure this project keeps auditing out of itself: the
# layer file would then assert something no one verified, and the reverse lookup would repeat it
# to the user with a confident-looking tag.
#
# The protocol is the operator's proposal: put the question to independent classifiers who
# cannot see my answer, and accept a term ONLY where an independent verdict agrees with mine.
# Agreement is then evidence; my own confidence never is.
#
#   -Emit   writes LEXICON_BLIND_PROBE.tsv (to the temp directory by default) — term, the standard form it would attach
#           to, and NOTHING else. No bucket, no confidence, no note: a classifier who can see my
#           guess is not independent, and this file is what gets handed out.
#   -Score  reads back a filled copy (an `answer` column added) and reports agreement against
#           the held decisions. It NEVER writes the decision table; promoting a term stays a
#           deliberate edit, because a script that promotes on agreement would just automate the
#           judgement the protocol exists to externalise.
param(
    [switch]$Emit,
    [string]$Score = '',
    [string]$Decisions = 'Resource\Data\Reference\LEXICON_PILOT_DECISIONS.tsv',
    [string]$ProbeFile = (Join-Path ([IO.Path]::GetTempPath()) 'LEXICON_BLIND_PROBE.tsv')
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# The filled answer sheet comes back from wherever the classifier put it, which is usually not
# inside the repo. Join-Path on an already-absolute path silently produces a nonsense path.
function Resolve-Input([string]$path) {
    return [IO.Path]::IsPathRooted($path) ? $path : (Join-Path $repoRoot $path)
}

$rows = @(Import-Csv (Resolve-Input $Decisions) -Delimiter "`t")
$held = @($rows | Where-Object { $_.confidence.Trim() -eq 'low' -and $_.bucket.Trim() -ne 'exclude' })

if ($Emit) {
    $out = New-Object System.Collections.Generic.List[string]
    $out.Add("term`tmeans`tanswer")
    foreach ($r in ($held | Sort-Object standard, term)) {
        # `means` is the relation, not my classification — the classifier needs to know WHICH
        # relative the word names, or the question is unanswerable.
        $out.Add("$($r.term.Trim())`t$($r.standard.Trim())`t")
    }
    [IO.File]::WriteAllText((Resolve-Input $ProbeFile), ($out -join "`n") + "`n", $utf8NoBom)

    Write-Output "Blind probe: $ProbeFile ($($held.Count) terms, no classification included)"
    Write-Output ''
    Write-Output 'Instructions to hand out with the file (verbatim):'
    Write-Output '  For each row, fill `answer` with ONE of:'
    Write-Output '    colloquial | literary | north | northwest | xiang | wu | min | yue | hakka'
    Write-Output '    | southwest | south | unknown'
    Write-Output '  Use `unknown` freely — it is a valid answer and costs nothing. A guess is'
    Write-Output '  worse than an abstention here, because agreement is what promotes a term.'
    Write-Output '  A term may be current in several regions; name the one you are most sure of.'
    exit 0
}

if (-not $Score) {
    Write-Output "Nothing to do. Pass -Emit to write the probe, or -Score <filled.tsv> to score it."
    exit 1
}

$answers = @(Import-Csv (Resolve-Input $Score) -Delimiter "`t")
$mine = @{}
foreach ($r in $held) { $mine["$($r.term.Trim())`t$($r.standard.Trim())"] = $r.bucket.Trim() }

$agree = New-Object System.Collections.Generic.List[string]
$differ = New-Object System.Collections.Generic.List[string]
$abstain = 0
$unmatched = 0
foreach ($a in $answers) {
    $key = "$($a.term.Trim())`t$($a.means.Trim())"
    if (-not $mine.ContainsKey($key)) { $unmatched++; continue }
    $answer = $a.answer.Trim()
    if (-not $answer -or $answer -eq 'unknown') { $abstain++; continue }

    # My bucket may name several regions (min-yue); an independent verdict naming ANY of them is
    # agreement — the claim being tested is "this term belongs to that layer", not "you picked
    # the same primary region I did".
    $buckets = @($mine[$key] -split '-')
    if ($buckets -contains $answer) { $agree.Add("$($a.term) [$answer]") }
    else { $differ.Add("$($a.term): mine $($mine[$key]) vs theirs $answer") }
}

Write-Output "Blind scoring over $($held.Count) held terms:"
Write-Output "  agreed    $($agree.Count)  -> promotable to med confidence, BY HAND"
Write-Output "  differed  $($differ.Count)  -> keep held; the disagreement is the finding"
Write-Output "  abstained $abstain"
if ($unmatched -gt 0) { Write-Output "  unmatched $unmatched  (rows not in the held set — check the file)" }
Write-Output ''
if ($differ.Count -gt 0) {
    Write-Output 'Disagreements:'
    $differ | ForEach-Object { "  $_" }
}
exit 0
