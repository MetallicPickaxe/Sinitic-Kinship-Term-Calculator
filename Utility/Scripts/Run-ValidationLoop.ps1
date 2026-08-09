# Full validation loop, provenance-sealed (release-audit round 2).
# Requires PowerShell 7+ (pwsh); Windows PowerShell 5.1 cannot parse this file.
#
# History of why each seal exists:
#   - The 438 helper once read a stale bin\Debug exporter while the loop built
#     bin\x64\Debug: the main face reported an OLD engine as green for two rounds.
#   - Newest-timestamp selection was then shown to accept a back/forward-dated old
#     binary. Selection is now by DETERMINISTIC path, and the assembly's source
#     revision (+sha stamped by the SDK) must equal the current git HEAD — a doc-only
#     commit therefore forces a rebuild, which is the point: the binary provenance is
#     the commit, not a timestamp.
#   - The 90k TSV is deleted before the run and checked after (freshness, exact row
#     count, judgment values from the legal set), so a crashed exporter cannot leave
#     yesterday's numbers on disk to be re-counted as today's.
# Gates (any failure exits 1): build/restore/test exit codes; suite totals at least
# the recorded floor with zero failures; 438 rows == 438 with served-miss <= Max438;
# 90k rows == 90042 with served-miss <= Max90k. Metrics are reported SPLIT: a
# primary-answer mismatch whose reference term is still served among our candidates
# (候選命中) is disclosed separately from a genuine served-miss.
param(
    # ALL THREE counters ratchet on BOTH faces. Gating served-miss alone let every primary
    # answer rot while the reference survived only as a tagged candidate (the audit turned
    # all 438 primaries wrong and the loop stayed green); candidate-hit inflation is the
    # same rot in disguise, so it ratchets too.
    # 438 re-baselined 23/23/0 -> 29/29/0 on 2026-08-02, measured against the previous commit in
    # a worktree so the movement is attributed, not assumed. EXACTLY eight rows changed, all of
    # them the SP.{F,M}.{OB,YB,OS,YS}.SP family that the spouse-collateral analyzer now names:
    # two reached 一致 (伯岳母/叔岳母) and six reached 可接受簡寫 while serving mumuy's southern
    # word as a candidate (姑岳父/舅岳母/姨岳父 with 姑公/舅婆/姨公). They came OUT of 已收編
    # (descriptive-chain accepted), so candidate-hit rose because coverage grew, not because a
    # primary rotted — the distinction this three-counter split exists to make.
    # 29 -> 31 on the same day, second lexicon batch. Three rows moved, again attributed row by
    # row against the previous commit: SP.OB.S and SP.YB.S now serve mumuy's 舅侄 as a candidate,
    # and SP.YB.SP rose from 已收編 to 可接受簡寫. No primary answer changed.
    # 31 -> 33, long-tail batch. Eight rows moved: six only changed WHICH candidate the judgment
    # names (伯外公 -> 大姥爷 and friends, same class, no counter effect), and SP.OB.D / SP.YB.D
    # gained a candidate hit on 舅侄女. Again no primary answer changed.
    [int]$Max438Primary = 33, [int]$Max438CandidateHit = 33, [int]$Max438Mismatch = 0,
    # 90k TIGHTENS for the first time: 3601/34/3567 -> 3585/34/3551. The sibling-spouse-sibling
    # analyzer stopped discarding the bridge sibling's gender, and both affected formatters now
    # follow the project's own 姻/眷 connector rule. 24 rows moved, 16 of them straight to 一致.
    # Tightens again the same day: -> 3537/2/3535, after two composite formatters stopped
    # dropping the depth their analyzers had already counted (兄弟眷父 named a parent AND a
    # grandparent; 姑甥 covered four descending generations). 48 fewer primary mismatches, and
    # candidate-hit all but vanishes because those rows now agree outright.
    [int]$Max90kPrimary = 3537, [int]$Max90kCandidateHit = 2, [int]$Max90kMismatch = 3535,
    # Exact suite fingerprints (passed/failed/skipped). Any deviation is red: a skipped or
    # vanished test must be re-baselined CONSCIOUSLY, never absorbed by a floor.
    # 156 -> 173: LexiconWiringRepairTests (15), the view-model variant-chip guard, and the M4
    # generation-consistency gauge. 173 -> 207: the user-feature acceptance suites named in
    # the 2026-08-02 user-feature acceptance contract -- F1 other names (12), F2 query history
    # (11), F3 侄/姪 glyph policy (11, the last one added after driving the live window showed
    # the glyph chip sitting below the fold). Two of F1's answer the audit's unlabelled-alternate
    # finding: its named example, and a sweep because that defect lived in a fall-through a spot
    # check walks straight past. Four more replace a single VACUOUS pruning test the response
    # review caught: it asserted things that stayed true whether or not the policy ran, so the
    # policy is now a pure function asserted directly and mutation-checked in both directions.
    # F2's eleventh is the real Undo case its Clear test only claimed to cover.
    # 63 -> 64: the lexicon reachability sweep.
    # 207 -> 211, from the English other-names round. Two attempts were made and both REVERTED:
    # copying the Chinese names into the English column (an English interface that prints
    # 爸爸 · 老爸 · 爹 is not an English interface) and then having the notice point at Chinese
    # (scoped reasoning that does not stop at Chinese). The tests pin what survived: no Han in an
    # English session, the notice speaking only for the language on screen, the two Chinese
    # scripts holding the same set of names, and the Simplified layer labels -- which had been
    # rendering in Traditional all along, found while looking at this.
    # 211 -> 213, from the layout rework: the line under the term must be the relation's English
    # NAME rather than engine coordinates (swept over every two-token path), and the 的-chain
    # must appear exactly where it separates two same-word readings and nowhere else.
    # 213 -> 222: round-3 (ACCEPTANCE_2026-08-04_UI_ROUND3.md). Nine tests for V2's per-press
    # origin and V3's grouped other-names -- which keys offer variants and which honestly cannot,
    # that the menu shows real words rather than mode names, that each choice lands the sequence
    # its word promises, that NOTHING survives a key press (the whole reason the radio mode was
    # withdrawn), and that grouping moves the attribution without dropping a name. One of the nine
    # records an engine asymmetry rather than a requirement: upward the three forms come back as
    # three words, downward all six collapse to 兒子 / 女兒. Recorded, not endorsed -- the round
    # freezes the engine.
    # 222 -> 232: the engine round (ACCEPTANCE_2026-08-04_ENGINE_FIXPOINT.md), +10 new. The
    # round-3 asymmetry test above was REWRITTEN in place rather than added to, since E4 closed
    # the very thing it recorded -- all eight variant forms are named now, so it is no longer a
    # record of a defect and no longer carries the "recorded, not endorsed" caveat.
    #   +1  M5_IdentityDetoursDoNotChangeTheAnswer (E3) -- the audit's 6,038-detour sweep made
    #       permanent and grown to 6,114; walking out to a relative and straight back may not
    #       change the answer. This one gate replaces a defect family no other gate could see:
    #       neither mumuy face contains a single doubling-back chain.
    #   +5  EngineFixpointAcceptanceTests (E1) -- the reported chain answers like the short
    #       question, identity detours answer like the direct relation, reduction repeats until
    #       nothing more cancels, a round trip through the WRONG sex does not cancel, and
    #       exhausting the iteration cap degrades instead of throwing.
    #   +4  SamePersonGroupingTests (E2) -- the two double images the sweep named are gone, no
    #       path shows one person twice, genuinely different people (the two 姑母, 伯父/叔父) stay
    #       apart, and the backstop merges only what the reader cannot tell apart.
    [int]$UnitPassed = 232, [int]$UnitFailed = 0, [int]$UnitSkipped = 1,
    [int]$VerificationPassed = 64, [int]$VerificationFailed = 0, [int]$VerificationSkipped = 0,
    # Baselines live in this committed script. Overriding any of them from the CLI is a
    # RE-BASELINING act and must be declared — a release run must never absorb a quiet
    # override (the audit re-baselined around an ignored invariant via plain CLI args).
    [switch]$AllowBaselineOverride
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$baselineParams = @('Max438Primary','Max438CandidateHit','Max438Mismatch',
    'Max90kPrimary','Max90kCandidateHit','Max90kMismatch',
    'UnitPassed','UnitFailed','UnitSkipped',
    'VerificationPassed','VerificationFailed','VerificationSkipped')
$overridden = @($baselineParams | Where-Object { $PSBoundParameters.ContainsKey($_) })
if ($overridden.Count -gt 0 -and -not $AllowBaselineOverride) {
    Write-Output "BASELINE OVERRIDE REFUSED (pass -AllowBaselineOverride to re-baseline consciously): $($overridden -join ', ')"
    exit 1
}
if ($overridden.Count -gt 0) {
    Write-Output "BASELINE OVERRIDDEN (declared): $($overridden -join ', ')"
}

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
$vstest = & $vswhere -latest -prerelease -find '**\vstest.console.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild.exe not found via vswhere (-prerelease).' }
if (-not $vstest) { throw 'vstest.console.exe not found via vswhere (-prerelease).' }
$head = (git -C $root rev-parse HEAD).Trim()
Write-Output "Toolchain: MSBuild $((Get-Item $msbuild).VersionInfo.ProductVersion) | HEAD $head"

# Dirty-input rejection: the +sha assembly stamp records HEAD, not the bytes actually
# compiled — the audit built an edited, uncommitted .cs and the seal still claimed HEAD.
# So the seal is only meaningful over a clean tree. The single allowed exception is the
# judged 438 workbook, which the loop itself refreshes as a DERIVED output (it is not a
# build input); everything else — modified or untracked — fails the run.
$dirtyAllowed = @(
    'Resource/Data/Reference/MumuyMainAccuracyCompact.xlsx',
    # The compact face is also a DERIVED output. It used to be refreshed by hand, and it drifted:
    # its our_* columns still showed the pre-K16 slot order (爺爺 | 祖父 instead of 祖父 | 爺爺)
    # months after the swap. The judgments were never affected — the workbook pass reads only the
    # REFERENCE columns from this file and recomputes our side live — but a tracked artifact that
    # silently describes an old engine is exactly what this loop exists to prevent, so the loop
    # now regenerates it below.
    'Resource/Data/Reference/MumuyMainAccuracyCompact.tsv',
    'Resource/Data/Reference/MumuyMainAccuracyCompact.Unsupported.tsv'
)
$dirty = @(git -C $root status --porcelain | Where-Object {
    $path = ($_ -replace '^..\s+', '') -replace '^"(.*)"$', '$1'
    $dirtyAllowed -notcontains $path
})
if ($dirty.Count -gt 0) {
    Write-Output 'DIRTY BUILD INPUTS (commit or remove before validating):'
    $dirty | Select-Object -First 10 | ForEach-Object { "  $_" }
    exit 1
}

# Oracle-input seal: every figure this loop reports was measured against EXACTLY these
# bytes (also pinned in README). A different oracle silently changes the 90k face.
$oraclePins = @(
    @{ Path = "$root\Utility\MumuyAlgorithm\Data\mode-map.json";      Sha = 'FE4B66691BC3BD437E2C88D4D4C738F6DEAAF60844A610E235B7D0644F0B35D1' },
    @{ Path = "$root\Utility\MumuyAlgorithm\Data\cache.json";         Sha = '1E105A7DBF6DF3E8B0E3C7087D5F34F91273325F5B99E5C150E62C740590A9E4' },
    @{ Path = "$root\Utility\MumuyAlgorithm\Data\kinship_terms.yaml"; Sha = '67B2AECE10AB3E79AC33EA65F1CA64AFA474DF800D9B500A5C1386701337EFCF' }
)
foreach ($pin in $oraclePins) {
    if (-not (Test-Path $pin.Path)) { Write-Output "ORACLE MISSING: $($pin.Path)"; exit 1 }
    $actual = (Get-FileHash $pin.Path -Algorithm SHA256).Hash
    if ($actual -ne $pin.Sha) {
        Write-Output "ORACLE HASH MISMATCH: $($pin.Path)`n  expected $($pin.Sha)`n  actual   $actual"
        exit 1
    }
}
Write-Output 'Oracle inputs hash-verified (3 files)'

# Lexicon structure gate. The layer files are data, so no compiler checks them: a duplicate key,
# a word hung off two different standard forms, or a variant that shadows another relation's
# standard form all load silently and corrupt the reverse lookup. (Reachability — whether a key
# is a form the engine can actually emit — is enforced from inside the verification suite, which
# can drive the calculator.)
& "$root\Utility\Scripts\Test-LexiconInvariants.ps1"
if ($LASTEXITCODE -ne 0) { Write-Output 'GATE FAILED: lexicon invariants'; exit 1 }

# Shipping documents point the reader at concrete repository paths. Documentation does not
# compile, so a renamed file turns those instructions into a dead end for someone holding only
# the package — with nothing in the build to notice.
#
# The self-test runs FIRST and is not ceremony. The audit of 2026-08-02 caught this gate passing
# here while failing in a fresh clone, because it demanded that build outputs (UI\obj,
# Distribution\) already exist — which they did, on the machine that wrote it. The repair was a
# classifier separating repository paths from generated and operator-supplied ones, so the
# classifier is the part that must not silently rot.
& "$root\Utility\Scripts\Test-DocReferences.ps1" -SelfTest
if ($LASTEXITCODE -ne 0) { Write-Output 'GATE FAILED: doc-reference classifier self-test'; exit 1 }

& "$root\Utility\Scripts\Test-DocReferences.ps1"
if ($LASTEXITCODE -ne 0) { Write-Output 'GATE FAILED: shipping-document references'; exit 1 }

$net = 'net10.0-windows10.0.26100.0'
# NOT in this list: Test\Test-UI.csproj. Its cases are [UITestMethod] and call window.Activate(),
# so running them opens a real window and takes focus — unacceptable inside an unattended gate.
# That is a deliberate exclusion, stated here rather than left invisible.
#
# But "run them by hand" needs to say HOW, because from a command line you cannot. Measured
# 2026-08-03, before publishing the release package:
#
#   vstest.console Test-UI.dll  -> all three FAIL: "UITestMethodAttribute.DispatcherQueue should
#                                  not be null". The wiring exists (Test\UnitTestApp.xaml.cs:54
#                                  sets it), but only once the WinUI app is running, and loading
#                                  the DLL in-process never starts it.
#   vstest.console Test-UI.exe  -> "No test is available."
#
# The project is OutputType=WinExe and its OnLaunched calls UnitTestClient.Run(CommandLine), so
# the app must be launched BY the test platform as an app-container host — which in practice
# means Visual Studio's Test Explorer. Anyone reading "must be run by hand" and reaching for the
# CLI will get three red tests and think the product broke; it did not.
#
# Everything about the view model that can be asserted without a window lives in Test-Unit
# instead (the variant-chip guard, for one), which is where UI-surface regressions should be
# pinned. Real-window verification is done by publishing and driving the app with `winapp ui`,
# which is what the 2026-08-02 walkthroughs and the pre-release check both used.
$projects = @(
    @{ Proj = "$root\Test-Unit\Test-Unit.csproj";                                     Bin = "$root\Test-Unit\bin\x64\Debug\$net\win-x64\Test-Unit.dll" },
    @{ Proj = "$root\Test-Verification\Test-Verification.csproj";                     Bin = "$root\Test-Verification\bin\x64\Debug\$net\Test-Verification.dll" },
    @{ Proj = "$root\Utility\ReferenceAccuracyExporter\ReferenceAccuracyExporter.csproj"; Bin = "$root\Utility\ReferenceAccuracyExporter\bin\x64\Debug\$net\ReferenceAccuracyExporter.exe" }
)

function Get-SourceRevision([string]$binary) {
    $pv = (Get-Item $binary).VersionInfo.ProductVersion
    if ($pv -match '\+([0-9a-f]{7,40})$') { return $Matches[1] }
    return ''
}

foreach ($entry in $projects) {
    & $msbuild $entry.Proj -t:Restore -p:Configuration=Debug -p:Platform=x64 -v:q -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Output "RESTORE FAILED: $($entry.Proj)"; exit 1 }
    & $msbuild $entry.Proj -t:Build -p:Configuration=Debug -p:Platform=x64 -v:q -nologo | Out-Null
    if ($LASTEXITCODE -ne 0) { Write-Output "BUILD FAILED: $($entry.Proj)"; exit 1 }

    if (-not (Test-Path $entry.Bin)) { Write-Output "BINARY MISSING at deterministic path: $($entry.Bin)"; exit 1 }
    # Provenance seal: the assembly must be stamped with the current HEAD. An incremental
    # build after a new commit keeps the old stamp — rebuild once, then hard-fail.
    if ((Get-SourceRevision $entry.Bin) -ne $head) {
        & $msbuild $entry.Proj -t:Rebuild -p:Configuration=Debug -p:Platform=x64 -v:q -nologo | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Output "REBUILD FAILED: $($entry.Proj)"; exit 1 }
    }
    $rev = Get-SourceRevision $entry.Bin
    if ($rev -ne $head) {
        Write-Output "PROVENANCE FAILED: $($entry.Bin) stamped '$rev', HEAD is '$head'"
        exit 1
    }
}
$exporter = $projects[2].Bin
Write-Output "Binaries provenance-sealed to $head"

function Invoke-Suite([string]$label, [string]$dll, [int]$expPassed, [int]$expFailed, [int]$expSkipped) {
    Write-Output "${label}:"
    $lines = & $vstest $dll --logger:'console;verbosity=minimal' 2>$null
    $vstestExit = $LASTEXITCODE
    $summary = $lines | Select-String -Pattern 'Passed!|Failed!|Total tests|Passed:|Failed:|Skipped:' | ForEach-Object { $_.Line }
    $summary
    $passed = -1; $failed = -1; $skipped = -1
    foreach ($line in $lines) {
        if ($line -match 'Passed:\s+(\d+)') { $passed = [int]$Matches[1] }
        if ($line -match 'Failed:\s+(\d+)') { $failed = [int]$Matches[1] }
        if ($line -match 'Skipped:\s+(\d+)') { $skipped = [int]$Matches[1] }
    }
    if ($vstestExit -ne 0 -or $passed -ne $expPassed -or $failed -ne $expFailed -or $skipped -ne $expSkipped) {
        Write-Output "GATE FAILED: $label fingerprint $passed/$failed/$skipped != expected $expPassed/$expFailed/$expSkipped (vstest exit $vstestExit)"
        exit 1
    }
}

Invoke-Suite 'UNIT' $projects[0].Bin $UnitPassed $UnitFailed $UnitSkipped
Invoke-Suite 'VERIFICATION' $projects[1].Bin $VerificationPassed $VerificationFailed $VerificationSkipped

# The metamorphic terminal-gender invariant is enforced by FULLY-QUALIFIED NAME with an
# exact 1/0/0 fingerprint: a substring /Tests: filter let a same-named always-pass decoy
# in another class satisfy "Passed==1" while the real invariant sat [Ignore]d.
Write-Output 'M3 (named enforcement):'
$m3Fqn = 'Test_Unit.MetamorphicInvariantTests.M3_TerminalGenderConsistencyGauge'
$m3 = & $vstest $projects[0].Bin "/TestCaseFilter:FullyQualifiedName=$m3Fqn" --logger:'console;verbosity=minimal' 2>$null
$m3Exit = $LASTEXITCODE
$m3Passed = -1; $m3Failed = -1; $m3Skipped = 0; $m3Total = -1
foreach ($line in $m3) {
    if ($line -match 'Passed:\s+(\d+)') { $m3Passed = [int]$Matches[1] }
    if ($line -match 'Failed:\s+(\d+)') { $m3Failed = [int]$Matches[1] }
    if ($line -match 'Skipped:\s+(\d+)') { $m3Skipped = [int]$Matches[1] }
    if ($line -match 'Total:\s+(\d+)') { $m3Total = [int]$Matches[1] }
}
$m3 | Select-String -Pattern 'Passed!|Failed!|Skipped:' | ForEach-Object { $_.Line }
if ($m3Exit -ne 0 -or $m3Passed -ne 1 -or $m3Failed -ne 0 -or $m3Skipped -ne 0 -or $m3Total -ne 1) {
    Write-Output "GATE FAILED: M3 invariant fingerprint $m3Passed/$m3Failed/$m3Skipped total $m3Total != 1/0/0 total 1 (exit $m3Exit)"
    exit 1
}

Write-Output 'MAIN:'
# Refresh the compact face FIRST so the tracked artifact describes the engine that is about to be
# judged. The workbook pass reads this file's reference columns and recomputes ours, so the
# refresh cannot bias the gate — it only stops the file itself from going stale.
& $exporter --source main --judge | Select-Object -Last 1
if ($LASTEXITCODE -ne 0) { Write-Output 'GATE FAILED: 438 compact export'; exit 1 }
& "$root\Utility\Scripts\build_judged_main_workbook.ps1" -ExporterPath $exporter `
    -MaxMismatch $Max438Mismatch -MaxPrimary $Max438Primary -MaxCandidateHit $Max438CandidateHit
if ($LASTEXITCODE -ne 0) { Write-Output 'GATE FAILED: 438 workbook pass'; exit 1 }

Write-Output '90K:'
$tsv = "$root\Resource\Data\Reference\MumuyModeMapAccuracyCompact.tsv"
if (Test-Path $tsv) { Remove-Item $tsv -Force }   # a crash must not leave stale numbers behind
$runStart = Get-Date
& $exporter --source mode-map --judge | Select-Object -Last 4
if ($LASTEXITCODE -ne 0) { Write-Output 'GATE FAILED: 90k export/judge pass'; exit 1 }
if (-not (Test-Path $tsv)) { Write-Output 'GATE FAILED: 90k TSV not produced'; exit 1 }
if ((Get-Item $tsv).LastWriteTime -lt $runStart) { Write-Output 'GATE FAILED: 90k TSV not fresh'; exit 1 }

$rows = Import-Csv $tsv -Delimiter "`t"
if ($rows.Count -ne 90042) { Write-Output "GATE FAILED: 90k row count $($rows.Count) != 90042"; exit 1 }
$legalPrefixes = @('一致', '可接受簡寫', '不一致', '已收編', '拒收', '界外', '群稱')
$illegal = $rows | Where-Object { $j = ($_.our_judgment -split '：')[0]; $legalPrefixes -notcontains $j } | Select-Object -First 3
if ($illegal) {
    Write-Output "GATE FAILED: illegal judgment value(s): $($illegal | ForEach-Object { $_.our_judgment } | Select-Object -First 3)"
    exit 1
}

$rows | Group-Object { ($_.our_judgment -split '：')[0] } | Sort-Object Count -Descending | ForEach-Object {
    '  {0} × {1} ({2}%)' -f $_.Name, $_.Count, [Math]::Round(100.0 * $_.Count / $rows.Count, 1)
}
$servedMiss90k = ($rows | Where-Object { $_.our_judgment -like '不一致*' }).Count
$candidateHit90k = ($rows | Where-Object { $_.our_judgment -like '*候選命中*' }).Count
$primary90k = $servedMiss90k + $candidateHit90k
Write-Output "90k primary-answer mismatches: $primary90k (gate <= $Max90kPrimary; of which $candidateHit90k candidate-served, gate <= $Max90kCandidateHit)"
Write-Output "90k served misses: $servedMiss90k (gate <= $Max90kMismatch)"
if ($primary90k -gt $Max90kPrimary) { Write-Output 'GATE FAILED: 90k primary-answer mismatch above ratchet'; exit 1 }
if ($candidateHit90k -gt $Max90kCandidateHit) { Write-Output 'GATE FAILED: 90k candidate-hit above ratchet'; exit 1 }
if ($servedMiss90k -gt $Max90kMismatch) { Write-Output 'GATE FAILED: 90k served-miss above gate'; exit 1 }

Write-Output 'VALIDATION LOOP: ALL GATES GREEN'
