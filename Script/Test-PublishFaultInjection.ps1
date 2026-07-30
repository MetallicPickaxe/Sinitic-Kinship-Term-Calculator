# Fault-injection proof for the release transaction (release audit B3 / F1).
#
# History of why this test looks the way it does:
#   Round 3 injected failures and only compared SHA256SUMS.txt's own hash and the file
#   count. That was too weak: a project target (CopyToDistribution) was overwriting the
#   live exe during Publish — outside the transaction — and the test still reported
#   PASSED, leaving a live package whose exe did not match its own manifest.
# So this test now:
#   1. advances HEAD with an empty commit, guaranteeing the newly built exe DIFFERS in
#      bytes and stamp from the baseline package (otherwise an overwrite is invisible);
#   2. snapshots the baseline package file-by-file (SHA-256 of every file);
#   3. after each injected failure re-verifies EVERY file against that snapshot, AND
#      recomputes every manifest entry, AND checks for staging/old debris.
# Requires PowerShell 7+. Run from a clean tree; publishing refuses a dirty one.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Split-Path -Parent $PSScriptRoot
$distribution = Join-Path $repoRoot 'Distribution'
$publish = Join-Path $PSScriptRoot 'Publish-SingleFile.ps1'

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
    # Every manifest entry must exist and match — this is what catches an exe replaced
    # behind the manifest's back.
    $problems = @()
    $manifestPath = Join-Path $dir 'SHA256SUMS.txt'
    foreach ($line in (Get-Content $manifestPath)) {
        if ($line -notmatch '^([0-9A-Fa-f]{64})\s\s(.+)$') { $problems += "unparsable line: $line"; continue }
        $expected = $Matches[1]; $rel = $Matches[2]
        $path = Join-Path $dir ($rel -replace '/', '\')
        if (-not (Test-Path $path)) { $problems += "missing: $rel"; continue }
        if ((Get-FileHash $path -Algorithm SHA256).Hash -ne $expected) { $problems += "hash mismatch: $rel" }
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
Write-Output "Baseline package: $($baseline.Count) files, manifest fully verified"

$startHead = (git -C $repoRoot rev-parse HEAD).Trim()
$failures = 0
try {
    # A publish of the SAME commit would rebuild byte-identical output, hiding any
    # overwrite of the live package. Advance HEAD so the new exe is provably different.
    git -C $repoRoot commit -q --allow-empty -m 'fault-injection probe commit (temporary)'
    $probeHead = (git -C $repoRoot rev-parse --short HEAD).Trim()
    Write-Output "Probe commit $probeHead — the build under test produces a DIFFERENT exe than the baseline"

    foreach ($point in 'sign', 'before-swap', 'mid-swap') {
        Write-Output ''
        Write-Output "--- injecting failure at: $point"
        $threw = $false
        try {
            & $publish -SimulateFailure $point *>&1 | Select-String -Pattern 'ROLLBACK|SIMULATED|LIVE DISTRIBUTION' | ForEach-Object { "    $($_.Line)" }
        }
        catch { $threw = $true }
        if (-not $threw) { Write-Output "  FAIL: publish did not fail at $point"; $failures++ }

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

        # (2) manifest still describes the files that are actually there
        $mp = Test-ManifestConsistency $distribution
        if ($mp.Count -gt 0) {
            Write-Output "  FAIL: manifest inconsistent ($($mp.Count)):"
            $mp | Select-Object -First 5 | ForEach-Object { "      $_" }
            $failures++
        }
        else { Write-Output '  OK: every manifest entry recomputed and matching' }

        # (3) no staging / old debris. -Filter 'Distribution.*' would also match the
        # extension-less 'Distribution' itself under Windows wildcard rules.
        $debris = @(Get-ChildItem $repoRoot -Directory -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -like 'Distribution.*' -and $_.Name -ne 'Distribution' })
        if ($debris.Count -gt 0) { Write-Output "  FAIL: debris left: $($debris.Name -join ', ')"; $failures++ }
        else { Write-Output '  OK: no staging/old debris' }
    }
}
finally {
    # Drop the probe commit and any residue so the repository is exactly as it was.
    git -C $repoRoot reset -q --hard $startHead
    Get-ChildItem $repoRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'Distribution.*' -and $_.Name -ne 'Distribution' } |
        ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
}

Write-Output ''
if ($failures -gt 0) { Write-Output "FAULT INJECTION FAILED ($failures problem(s))"; exit 1 }
Write-Output "FAULT INJECTION PASSED: every file of the live package survived all three injected failures byte-unchanged, manifest fully consistent, no debris"
