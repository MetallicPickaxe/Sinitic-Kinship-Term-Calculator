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
    [int]$Max438Mismatch = 0,
    [int]$Max90kMismatch = 3567,
    [int]$MinUnitTotal = 157,
    [int]$MinVerificationTotal = 59
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$vswhere = 'C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
$vstest = & $vswhere -latest -prerelease -find '**\vstest.console.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild.exe not found via vswhere (-prerelease).' }
if (-not $vstest) { throw 'vstest.console.exe not found via vswhere (-prerelease).' }
$head = (git -C $root rev-parse HEAD).Trim()
Write-Output "Toolchain: MSBuild $((Get-Item $msbuild).VersionInfo.ProductVersion) | HEAD $head"

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

function Invoke-Suite([string]$label, [string]$dll, [int]$minTotal) {
    Write-Output "${label}:"
    $lines = & $vstest $dll --logger:'console;verbosity=minimal' 2>$null
    $vstestExit = $LASTEXITCODE
    $summary = $lines | Select-String -Pattern 'Passed!|Failed!|Total tests|Passed:|Failed:|Skipped:' | ForEach-Object { $_.Line }
    $summary
    $failedCount = -1; $totalCount = -1
    foreach ($line in $lines) {
        if ($line -match 'Failed:\s+(\d+)') { $failedCount = [int]$Matches[1] }
        if ($line -match 'Total:\s+(\d+)') { $totalCount = [int]$Matches[1] }
    }
    if ($vstestExit -ne 0 -or $failedCount -ne 0 -or $totalCount -lt $minTotal) {
        Write-Output "GATE FAILED: $label (vstest exit $vstestExit, failed $failedCount, total $totalCount < floor $minTotal)"
        exit 1
    }
}

Invoke-Suite 'UNIT' $projects[0].Bin $MinUnitTotal
Invoke-Suite 'VERIFICATION' $projects[1].Bin $MinVerificationTotal

Write-Output 'MAIN:'
& "$root\Utility\Scripts\build_judged_main_workbook.ps1" -ExporterPath $exporter -MaxMismatch $Max438Mismatch
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
Write-Output "90k primary-answer mismatches: $($servedMiss90k + $candidateHit90k) (of which $candidateHit90k candidate-served)"
Write-Output "90k served misses: $servedMiss90k (gate <= $Max90kMismatch)"
if ($servedMiss90k -gt $Max90kMismatch) { Write-Output 'GATE FAILED: 90k served-miss above gate'; exit 1 }

Write-Output 'VALIDATION LOOP: ALL GATES GREEN'
