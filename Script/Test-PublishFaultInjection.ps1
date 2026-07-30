# Fault-injection proof for the transactional Distribution swap (release audit B3).
#
# The audit forced a failure between the two renames and found: Distribution missing,
# Distribution.old-* orphaned, staging orphaned, no rollback. This script pins the fixed
# behaviour by injecting each failure point and asserting, after every one, that:
#   - Distribution\ still exists and still holds the ORIGINAL bytes (manifest unchanged),
#   - no Distribution.old-* or Distribution.staging-* debris is left behind.
# Requires PowerShell 7+. Run from a clean tree; publishing itself refuses a dirty one.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Split-Path -Parent $PSScriptRoot
$distribution = Join-Path $repoRoot 'Distribution'
$publish = Join-Path $PSScriptRoot 'Publish-SingleFile.ps1'

if (-not (Test-Path (Join-Path $distribution 'SHA256SUMS.txt'))) {
    throw 'No existing Distribution to protect — run a successful publish first.'
}
$before = (Get-FileHash (Join-Path $distribution 'SHA256SUMS.txt') -Algorithm SHA256).Hash
$beforeCount = (Get-ChildItem $distribution -Recurse -File).Count
Write-Output "Baseline Distribution: manifest $($before.Substring(0,16))…, $beforeCount files"

$failures = 0
foreach ($point in 'sign', 'before-swap', 'mid-swap') {
    Write-Output ''
    Write-Output "--- injecting failure at: $point"
    $threw = $false
    try {
        & $publish -SimulateFailure $point *>&1 | Select-String -Pattern 'ROLLBACK|SIMULATED' | ForEach-Object { "    $($_.Line)" }
    }
    catch { $threw = $true }
    if (-not $threw) { Write-Output "  FAIL: publish did not fail at $point"; $failures++ }

    $exists = Test-Path (Join-Path $distribution 'SHA256SUMS.txt')
    $after = if ($exists) { (Get-FileHash (Join-Path $distribution 'SHA256SUMS.txt') -Algorithm SHA256).Hash } else { '(missing)' }
    $afterCount = if ($exists) { (Get-ChildItem $distribution -Recurse -File).Count } else { 0 }
    # -Filter 'Distribution.*' uses Windows wildcard semantics and also matches the
    # extension-less 'Distribution' itself; compare names explicitly instead.
    $debris = @(Get-ChildItem $repoRoot -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like 'Distribution.*' -and $_.Name -ne 'Distribution' })

    if (-not $exists) { Write-Output '  FAIL: Distribution is missing'; $failures++ }
    elseif ($after -ne $before) { Write-Output "  FAIL: Distribution changed ($after)"; $failures++ }
    elseif ($afterCount -ne $beforeCount) { Write-Output "  FAIL: file count changed ($afterCount)"; $failures++ }
    else { Write-Output "  OK: Distribution intact ($afterCount files, manifest unchanged)" }

    if ($debris.Count -gt 0) {
        Write-Output "  FAIL: debris left: $($debris.Name -join ', ')"
        $failures++
    }
    else { Write-Output '  OK: no staging/old debris' }
}

Write-Output ''
if ($failures -gt 0) { Write-Output "FAULT INJECTION FAILED ($failures problem(s))"; exit 1 }
Write-Output 'FAULT INJECTION PASSED: Distribution survives every injected failure, no debris'
