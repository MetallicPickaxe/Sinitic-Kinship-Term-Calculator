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
    [int]$Max438Primary = 23, [int]$Max438CandidateHit = 23, [int]$Max438Mismatch = 0,
    [int]$Max90kPrimary = 3601, [int]$Max90kCandidateHit = 34, [int]$Max90kMismatch = 3567,
    # Exact suite fingerprints (passed/failed/skipped). Any deviation is red: a skipped or
    # vanished test must be re-baselined CONSCIOUSLY, never absorbed by a floor.
    [int]$UnitPassed = 156, [int]$UnitFailed = 0, [int]$UnitSkipped = 1,
    [int]$VerificationPassed = 63, [int]$VerificationFailed = 0, [int]$VerificationSkipped = 0,
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
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
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
$dirtyAllowed = @('Resource/Data/Reference/MumuyMainAccuracyCompact.xlsx')
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

$net = 'net10.0-windows10.0.26100.0'
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
