# Fault-injection proof for the release transaction (release audit B3 / F1 / R1 / R2).
#
# Why each assertion exists — every one of them is a defect this test previously had:
#   * It only checked "did publish throw?". Committing a wrong toolchain pin made publish
#     fail in the gate BEFORE any injection point, and the test scored three successful
#     injections and PASSED. Each round now requires the exact "SIMULATED FAILURE: <point>"
#     token in the output, so a failure at the wrong stage is red.
#   * It compared only SHA256SUMS.txt's own hash and the file count, so a live package
#     whose exe had been replaced behind its manifest still passed. It now snapshots every
#     file and re-verifies all of them, plus the manifest in BOTH directions
#     (manifest -> files and files -> manifest).
#   * It rewrote history with `git reset --hard` in whatever repository it was run from.
#     It now refuses to run unless the caller confirms this is a disposable clone, and it
#     verifies the probe commit actually landed before relying on it.
#
# The injected points cover the whole recoverable boundary, including 'live-writer' — the
# audit's out-of-transaction writer, which touches the release directory during the build
# and then fails.
# Requires PowerShell 7+.
param(
    # The test creates and removes a probe commit; running it against a working repository
    # would rewrite that repository's HEAD. Pass this only in a throwaway clone.
    [switch]$IAmADisposableClone
)
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Split-Path -Parent $PSScriptRoot
$distribution = Join-Path $repoRoot 'Distribution'
$publish = Join-Path $PSScriptRoot 'Publish-SingleFile.ps1'

if (-not $IAmADisposableClone) {
    throw 'Refusing to run: this test creates and then discards a probe commit (git reset --hard). Run it in a throwaway clone and pass -IAmADisposableClone.'
}
if (-not (Test-Path (Join-Path $distribution 'SHA256SUMS.txt'))) {
    throw 'No existing Distribution to protect — run a successful publish first.'
}
if (@(git -C $repoRoot status --porcelain).Count -gt 0) {
    throw 'Working tree is dirty — commit or clean before running the fault injection.'
}

function Get-TreeSnapshot([string]$dir) {
    $snap = @{}
    Get-ChildItem $dir -Recurse -File | ForEach-Object {
        $snap[$_.FullName.Substring($dir.Length + 1)] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
    }
    return $snap
}

function Test-ManifestConsistency([string]$dir) {
    # BOTH directions: every manifest entry must exist and match, and every file present
    # must be listed (except the manifest itself, which cannot contain its own digest).
    $problems = @()
    $manifestPath = Join-Path $dir 'SHA256SUMS.txt'
    $listed = New-Object System.Collections.Generic.HashSet[string]
    foreach ($line in (Get-Content $manifestPath)) {
        if ($line -notmatch '^([0-9A-Fa-f]{64})\s\s(.+)$') { $problems += "unparsable line: $line"; continue }
        $expected = $Matches[1]; $rel = $Matches[2]
        [void]$listed.Add($rel)
        $path = Join-Path $dir ($rel -replace '/', '\')
        if (-not (Test-Path $path)) { $problems += "missing: $rel"; continue }
        if ((Get-FileHash $path -Algorithm SHA256).Hash -ne $expected) { $problems += "hash mismatch: $rel" }
    }
    foreach ($f in (Get-ChildItem $dir -Recurse -File)) {
        $rel = $f.FullName.Substring($dir.Length + 1).Replace('\', '/')
        if ($rel -eq 'SHA256SUMS.txt') { continue }
        if (-not $listed.Contains($rel)) { $problems += "unlisted payload: $rel" }
    }
    return $problems
}

$baseline = Get-TreeSnapshot $distribution
$baselineManifestProblems = Test-ManifestConsistency $distribution
if ($baselineManifestProblems.Count -gt 0) {
    Write-Output 'BASELINE PACKAGE IS ALREADY INCONSISTENT:'
    $baselineManifestProblems | ForEach-Object { "  $_" }
    exit 1
}
Write-Output "Baseline package: $($baseline.Count) files, manifest verified in both directions"

$startHead = (git -C $repoRoot rev-parse HEAD).Trim()
$failures = 0
try {
    # A publish of the SAME commit would rebuild byte-identical output, hiding any
    # overwrite of the live package. Advance HEAD so the new exe is provably different —
    # and verify that actually happened before trusting the rounds below.
    git -C $repoRoot commit -q --allow-empty -m 'fault-injection probe commit (temporary)'
    if ($LASTEXITCODE -ne 0) { throw "Probe commit failed with $LASTEXITCODE" }
    $probeHead = (git -C $repoRoot rev-parse HEAD).Trim()
    if ($probeHead -eq $startHead) { throw 'Probe commit did not change HEAD — the rounds would be meaningless.' }
    Write-Output "Probe commit $($probeHead.Substring(0,7)) — the build under test produces a DIFFERENT exe than the baseline"

    foreach ($point in 'live-writer', 'sign', 'before-swap', 'mid-swap') {
        Write-Output ''
        Write-Output "--- injecting failure at: $point"
        $threw = $false
        $output = @()
        try {
            $output = & $publish -SimulateFailure $point *>&1 | ForEach-Object { "$_" }
        }
        catch { $threw = $true }
        $output | Where-Object { $_ -match 'ROLLBACK|SIMULATED FAILURE|LIVE DISTRIBUTION' } | ForEach-Object { "    $_" }

        # (0) the run must have failed AT THE INJECTED STAGE, not somewhere earlier
        if (-not $threw) { Write-Output "  FAIL: publish did not fail at $point"; $failures++ }
        $token = "SIMULATED FAILURE: $point"
        if (-not ($output | Where-Object { $_ -like "*$token*" })) {
            Write-Output "  FAIL: injection point '$point' was never reached (token '$token' absent) — the run failed at an earlier stage"
            $failures++
        }
        else { Write-Output "  OK: reached the injected stage ($token)" }

        if (-not (Test-Path $distribution)) { Write-Output '  FAIL: Distribution is missing'; $failures++; continue }

        # (1) every file byte-unchanged against the baseline snapshot
        $now = Get-TreeSnapshot $distribution
        $diffs = @()
        foreach ($k in $baseline.Keys) {
            if (-not $now.ContainsKey($k)) { $diffs += "removed: $k" }
            elseif ($now[$k] -ne $baseline[$k]) { $diffs += "MODIFIED: $k" }
        }
        foreach ($k in $now.Keys) { if (-not $baseline.ContainsKey($k)) { $diffs += "added: $k" } }
        if ($diffs.Count -gt 0) {
            Write-Output "  FAIL: package changed ($($diffs.Count) difference(s)):"
            $diffs | Select-Object -First 5 | ForEach-Object { "      $_" }
            $failures++
        }
        else { Write-Output "  OK: all $($now.Count) files byte-identical to baseline" }

        # (2) manifest still describes exactly the files that are there, both directions
        $mp = Test-ManifestConsistency $distribution
        if ($mp.Count -gt 0) {
            Write-Output "  FAIL: manifest inconsistent ($($mp.Count)):"
            $mp | Select-Object -First 5 | ForEach-Object { "      $_" }
            $failures++
        }
        else { Write-Output '  OK: manifest consistent in both directions' }

        # (3) no staging / parked / old debris. -Filter 'Distribution.*' would also match
        # the extension-less 'Distribution' itself under Windows wildcard rules.
        $debris = @(Get-ChildItem $repoRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'Distribution.*' -and $_.Name -ne 'Distribution' })
        if ($debris.Count -gt 0) { Write-Output "  FAIL: debris left: $($debris.Name -join ', ')"; $failures++ }
        else { Write-Output '  OK: no staging/parked/old debris' }
    }
}
finally {
    # Drop the probe commit and any residue so the clone is exactly as it was.
    git -C $repoRoot reset -q --hard $startHead
    Get-ChildItem $repoRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'Distribution.*' -and $_.Name -ne 'Distribution' } |
        ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Output ''
if ($failures -gt 0) { Write-Output "FAULT INJECTION FAILED ($failures problem(s))"; exit 1 }
Write-Output "FAULT INJECTION PASSED: all four injection points reached; the live package survived each one byte-unchanged with a consistent manifest and no debris"
