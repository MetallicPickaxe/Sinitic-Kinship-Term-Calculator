# Every REPOSITORY path named by a SHIPPING document must exist.
#
# These six files go out with the release (or are what the operator runs the release from), and
# each one points the reader at concrete paths: "see Resource/Data/Reference/…", "run
# Script\Publish-SingleFile.ps1", "the layers live in Resource/Data/Lexicon/…". A path that has
# been renamed or removed turns those instructions into a dead end for someone who has only the
# package and no way to ask. Nothing else in the build notices — documentation does not compile.
#
# Deliberately narrow: only paths under known top-level directories, only file extensions this
# repository actually uses. Prose that merely happens to look path-shaped is not a reference, and
# a check that fires on prose gets disabled within a week.
#
# ---------------------------------------------------------------------------------------------
# WHY THE CLASSIFIER EXISTS (audit AUDIT_2026-08-02_USER_FEATURE_ACCEPTANCE.md §七)
#
# The first version of this gate demanded that EVERY extracted path exist at the moment it ran.
# That is wrong, and it produced a FALSE GREEN: the author's working copy already had UI\obj and
# Distribution lying around from earlier builds, so the gate passed there while a fresh clone
# failed it. A gate that only passes on the machine that wrote it is worse than no gate.
#
# Three kinds of path appear in these documents, and only the first is a promise the checkout
# itself can keep:
#
#   1. REPOSITORY paths     — tracked files. Must exist the moment the tree is checked out.
#   2. GENERATED artifacts  — UI\obj\project.assets.json, Distribution\SHA256SUMS.txt. They come
#                             into being at restore / publish time. Absent in a clean tree BY
#                             DESIGN.
#   3. SUPPLIED inputs      — the three Mumuy oracle files. Deliberately not redistributed; README
#                             pins their hashes and the operator drops them in.
#
# The classifier is `git check-ignore`, not a hand-written prefix list, because the ignore rules
# are already the repository's own statement of what is and is not part of the tree — a second
# list would drift away from the first one silently. When git is unavailable (a source tarball
# with no .git), a static fallback covers the same ground.
#
# Categories 2 and 3 are NOT skipped. They are checked whenever they are present, so a renamed
# artifact is still caught in a built tree, and when absent they are REPORTED BY NAME rather than
# quietly dropped — silence is how the first version came to look green.
# ---------------------------------------------------------------------------------------------
[CmdletBinding()]
param(
    # Fail if a generated / supplied path is missing instead of deferring it. For a run that
    # happens AFTER restore and publish, where every category should be satisfiable.
    [switch]$RequireArtifacts,
    # Verify the classifier itself against a fixed sample and exit. Cheap regression guard for
    # the defect above: the bug was entirely in how paths were classified.
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

# Fallback classifier, used verbatim when git cannot answer. Kept in sync with .gitignore by the
# self-test below, which asserts the two agree ACROSS THE WHOLE TREE.
#
# The oracle inputs are listed BY NAME. An earlier version of this function matched the whole of
# Utility/MumuyAlgorithm/Data/, and the review of this gate found that .gitignore names exactly
# three files in there while eleven more are tracked: branch.json, filter.json, input.json,
# main.json, multiple.json, pair.json, prefix.json, replace.json, similar.json, sort.json and
# juan-grammar-probe.tsv. Those eleven are repository files that must be present, and the
# directory-wide rule quietly demoted them to "may legitimately be absent" — in a tree with no
# .git, which is the only place this function ever runs. That is the same false-green class this
# whole gate was repaired to remove, re-opened one level narrower inside the repair.
function Test-IsArtifactPath {
    param([string]$Path)
    $p = $Path -replace '\\', '/'
    return $p -match '(^|/)(obj|bin)/' -or
           $p -match '^Distribution/' -or
           $p -match '^TestResults/' -or
           $p -eq 'Utility/MumuyAlgorithm/Data/mode-map.json' -or
           $p -eq 'Utility/MumuyAlgorithm/Data/cache.json' -or
           $p -eq 'Utility/MumuyAlgorithm/Data/kinship_terms.yaml'
}

if ($SelfTest) {
    # EVERY TRACKED FILE, not a sample. The sampled version of this self-test passed while the
    # fallback misclassified eleven tracked files, because the ten cases were drawn from the
    # places the two classifiers already agreed. A sample cannot establish equivalence, and
    # equivalence is the entire claim being made.
    #
    # Note what git contributes that a path rule cannot: check-ignore is TRACKING-AWARE. A file
    # force-added under an ignored directory reports as NOT ignored, because ignore rules do not
    # apply to tracked files — so a shipped artefact stays a repository path. The fallback must
    # therefore be narrow enough that no tracked file falls into it.
    $tracked = @(& git -C $repoRoot ls-files 2>$null)
    if ($LASTEXITCODE -ne 0 -or $tracked.Count -eq 0) {
        Write-Output 'Doc reference classifier self-test SKIPPED: no git index to compare against'
        exit 0
    }

    $ignoredSet = [System.Collections.Generic.HashSet[string]]::new(
        [string[]]@($tracked | & git -C $repoRoot check-ignore --stdin 2>$null),
        [System.StringComparer]::Ordinal)

    $bad = 0
    foreach ($p in $tracked) {
        $gitSays = $ignoredSet.Contains($p)
        $fallbackSays = Test-IsArtifactPath $p
        if ($gitSays -ne $fallbackSays) {
            if ($bad -lt 10) { Write-Output "  fallback disagrees with git for ${p}: git=$gitSays fallback=$fallbackSays" }
            $bad++
        }
    }

    # The other direction: the three inputs that are deliberately NOT in the tree must still be
    # recognised, or a fresh checkout would demand files the repository refuses to redistribute.
    foreach ($supplied in @('Utility/MumuyAlgorithm/Data/mode-map.json',
                            'Utility/MumuyAlgorithm/Data/cache.json',
                            'Utility/MumuyAlgorithm/Data/kinship_terms.yaml',
                            'UI/obj/project.assets.json',
                            'Distribution/SHA256SUMS.txt')) {
        if (-not (Test-IsArtifactPath $supplied)) {
            Write-Output "  fallback fails to recognise a known non-repository path: $supplied"
            $bad++
        }
    }

    if ($bad -gt 0) { Write-Output "DOC REFERENCE CLASSIFIER SELF-TEST FAILED ($bad)"; exit 1 }
    Write-Output "Doc reference classifier self-test OK: $($tracked.Count) tracked files agree with .gitignore, 5 known non-repository paths recognised"
    exit 0
}

$docs = @(
    'README.md',
    'ATTRIBUTION.md',
    'THIRD-PARTY-NOTICES.md',
    'LICENSE-INVENTORY.md',
    'Governance\FEATURES.md',
    'Governance\RELEASE_READINESS.md'
)

# Data files that TELL THE READER WHERE THEY CAME FROM are shipping documents too. The lexicon
# layers each carry a "生成自 <path>" header naming their generator input, and those headers went
# stale when the adjudication table moved out of Governance — eight dead pointers that this gate
# could not see, because it only read the six .md files above. They were found by hand, which is
# not a control. Same extraction, same classifier, so a renamed input cannot rot here either.
$docs += @(Get-ChildItem -Path (Join-Path $repoRoot 'Resource\Data\Lexicon') -Filter '*.yaml' -ErrorAction SilentlyContinue |
    ForEach-Object { "Resource\Data\Lexicon\$($_.Name)" })

$roots = 'Resource|Governance|Utility|Script|Test-Unit|Test-Verification|Test|KinshipCalculator\.Core|UI|Distribution'
$extensions = 'cs|ps1|py|yaml|tsv|json|md|txt|xlsx'
$pattern = "(?<![\w/])((?:$roots)[\\/][\w\\/.\-]+?\.(?:$extensions))"

# The public source tree is published without Governance. Its two documents are then legitimately
# absent — but ONLY when the whole directory is gone. A Governance directory that exists while one
# of its shipping documents does not is still a broken reference.
$governancePresent = Test-Path (Join-Path $repoRoot 'Governance')

$problems  = New-Object System.Collections.Generic.List[string]
$deferred  = New-Object System.Collections.Generic.List[string]
$skippedDocs = New-Object System.Collections.Generic.List[string]
$checked = 0
$artifactsChecked = 0

foreach ($doc in $docs) {
    $full = Join-Path $repoRoot $doc
    if (-not (Test-Path $full)) {
        if ($doc -like 'Governance\*' -and -not $governancePresent) {
            $skippedDocs.Add($doc)
        }
        else {
            $problems.Add("shipping document itself is missing: $doc")
        }
        continue
    }

    $text = Get-Content $full -Raw -Encoding utf8
    $paths = @([regex]::Matches($text, $pattern) | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
    foreach ($p in $paths) {
        $exists = Test-Path (Join-Path $repoRoot ($p -replace '/', '\'))

        $gitIgnored = & git -C $repoRoot check-ignore -- ($p -replace '\\', '/') 2>$null
        $isArtifact = if ($LASTEXITCODE -le 1) { $null -ne $gitIgnored } else { Test-IsArtifactPath $p }

        if ($isArtifact) {
            if ($exists) { $artifactsChecked++ }
            elseif ($RequireArtifacts) { $problems.Add("$doc points at a build output that was not produced: $p") }
            else { $deferred.Add("$doc -> $p") }
            continue
        }

        $checked++
        if (-not $exists) {
            $problems.Add("$doc points at a path that does not exist: $p")
        }
    }
}

if ($problems.Count -gt 0) {
    Write-Output "DOC REFERENCES FAILED ($($problems.Count)):"
    $problems | ForEach-Object { "  $_" }
    exit 1
}

# Named, never silent — the whole defect was a category disappearing without being mentioned.
if ($deferred.Count -gt 0) {
    Write-Output "Deferred to build time ($($deferred.Count) generated/supplied paths, not yet produced):"
    $deferred | ForEach-Object { "  $_" }
}
if ($skippedDocs.Count -gt 0) {
    Write-Output "Not in this tree (Governance is excluded from the public source tree): $($skippedDocs -join ', ')"
}
Write-Output "Doc references OK: $checked repository paths + $artifactsChecked present artifacts across $($docs.Count - $skippedDocs.Count) shipping documents"
# Explicit, for the same reason Test-LexiconInvariants.ps1 is: falling off the end of a
# PowerShell script leaves $LASTEXITCODE at the caller's previous value.
exit 0
