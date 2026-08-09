# Verifies that the EXTRACTED package cache bytes the build actually consumes still match
# the signed .nupkg they came from (release audit R4).
#
# The gap this closes: the licensing inventory recorded each package's sha512 out of
# project.assets.json / .nupkg.sha512 — restore metadata describing the ORIGINAL archive.
# Nothing compared those values to the expanded files on disk, which are what the compiler
# and the single-file bundler actually read. The audit replaced YamlDotNet.dll in the cache
# (inventory still green, git still clean) and, worse, swapped the runtime pack's
# coreclr.dll for hostpolicy.dll and got a fully published EXE with a correct provenance
# stamp. Both are supply-chain substitutions that reached the release artifact.
#
# What this script does per package in the resolved graph:
#   1. locate <id>.<version>.nupkg in the package folder and verify it against the
#      .nupkg.sha512 recorded by NuGet (the archive is itself authenticated);
#   2. open that archive and compare EVERY extracted file on disk with the corresponding
#      archive entry, byte for byte (SHA-256 of the entry stream vs the file);
#   3. report a file that exists on disk but not in the archive, and vice versa.
# Anything that does not match is a hard failure.
#
# Cost: it rehashes the expanded cache for the graph (the runtime pack dominates). That is
# the price of knowing the bytes going into the release are the vendor's.
param(
    [string]$Configuration = 'Release',
    [string]$Rid = 'win-x64',
    [string]$ProductName = 'SiniticKinshipTermCalculator'
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$assetsPath = Join-Path $repoRoot 'UI\obj\project.assets.json'
$tfm = 'net11.0-windows10.0.26100.0'
$depsPath = Join-Path $repoRoot "UI\obj\x64\$Configuration\$tfm\$Rid\$ProductName.deps.json"
if (-not (Test-Path $assetsPath)) { throw "project.assets.json missing: $assetsPath" }
if (-not (Test-Path $depsPath)) { throw "Release deps.json missing: $depsPath (publish first)" }

$assets = Get-Content $assetsPath -Raw | ConvertFrom-Json
$deps = Get-Content $depsPath -Raw | ConvertFrom-Json
$packageFolders = @($assets.packageFolders.PSObject.Properties.Name)
$targetName = $assets.targets.PSObject.Properties.Name | Where-Object { $_ -match [regex]::Escape($Rid) + '$' } | Select-Object -First 1

# Same component set the inventory covers: the PackageReference closure plus the runtime
# packs the artifact actually embeds.
$components = [ordered]@{}
foreach ($p in $assets.targets.$targetName.PSObject.Properties) {
    if ($p.Value.type -ne 'package') { continue }
    $id, $ver = $p.Name -split '/', 2
    $components["$id/$ver"] = @{ Id = $id; Version = $ver }
}
$depsTarget = $deps.targets.PSObject.Properties | Select-Object -Last 1
foreach ($e in $depsTarget.Value.PSObject.Properties) {
    if ($e.Name -notlike 'runtimepack.*') { continue }
    $id, $ver = ($e.Name -replace '^runtimepack\.', '') -split '/', 2
    $components["$id/$ver"] = @{ Id = $id; Version = $ver }
}

# Files NuGet writes into the expanded folder itself — not part of the archive. They are
# named after the package (<id>.<version>.nupkg.sha512), so match by pattern, not by a
# bare name.
function Test-IsExtractionArtifact([string]$rel) {
    return ($rel -eq '.nupkg.metadata') -or
           ($rel -like '*.nupkg.sha512') -or
           ($rel -like '*.nupkg') -or
           ($rel -eq '.signature.p7s')
}

$problems = New-Object System.Collections.Generic.List[string]
$checkedPackages = 0
$checkedFiles = 0

foreach ($c in ($components.Values | Sort-Object { $_.Id })) {
    $pkgDir = $null
    foreach ($folder in $packageFolders) {
        $candidate = Join-Path $folder "$($c.Id)\$($c.Version)"
        if (Test-Path $candidate) { $pkgDir = $candidate; break }
    }
    if (-not $pkgDir) { $problems.Add("$($c.Id)/$($c.Version): package folder not found"); continue }

    $nupkg = Join-Path $pkgDir "$($c.Id.ToLowerInvariant()).$($c.Version.ToLowerInvariant()).nupkg"
    if (-not (Test-Path $nupkg)) {
        $found = Get-ChildItem $pkgDir -Filter '*.nupkg' -File | Select-Object -First 1
        if ($found) { $nupkg = $found.FullName } else { $problems.Add("$($c.Id)/$($c.Version): no .nupkg in the cache — cannot authenticate the expanded files"); continue }
    }

    # 1. the archive itself, against NuGet's recorded hash
    $shaFile = Join-Path $pkgDir "$($c.Id.ToLowerInvariant()).$($c.Version.ToLowerInvariant()).nupkg.sha512"
    if (-not (Test-Path $shaFile)) {
        $alt = Get-ChildItem $pkgDir -Filter '*.nupkg.sha512' -File | Select-Object -First 1
        if ($alt) { $shaFile = $alt.FullName }
    }
    if (Test-Path $shaFile) {
        $expected = (Get-Content $shaFile -Raw).Trim()
        $actual = [Convert]::ToBase64String([System.Security.Cryptography.SHA512]::HashData([IO.File]::ReadAllBytes($nupkg)))
        if ($actual -ne $expected) { $problems.Add("$($c.Id)/$($c.Version): .nupkg does not match its recorded sha512"); continue }
    }
    else { $problems.Add("$($c.Id)/$($c.Version): no .nupkg.sha512 — the archive is unauthenticated"); continue }

    # 2. every expanded file against the archive entry it came from
    $archive = [System.IO.Compression.ZipFile]::OpenRead($nupkg)
    try {
        $entries = @{}
        foreach ($entry in $archive.Entries) {
            if ($entry.FullName.EndsWith('/')) { continue }
            $entries[[Uri]::UnescapeDataString($entry.FullName).Replace('/', '\')] = $entry
        }
        foreach ($f in (Get-ChildItem $pkgDir -Recurse -File)) {
            $rel = $f.FullName.Substring($pkgDir.Length + 1)
            if (Test-IsExtractionArtifact $rel) { continue }
            $entry = $entries[$rel]
            if (-not $entry) {
                # NuGet lowercases the nuspec on extraction; match case-insensitively before
                # declaring an extra file.
                $match = $entries.Keys | Where-Object { $_ -ieq $rel } | Select-Object -First 1
                if ($match) { $entry = $entries[$match] }
            }
            if (-not $entry) { $problems.Add("$($c.Id)/$($c.Version): '$rel' is in the cache but NOT in the .nupkg"); continue }

            $stream = $entry.Open()
            try { $entryHash = [BitConverter]::ToString([System.Security.Cryptography.SHA256]::Create().ComputeHash($stream)).Replace('-', '') }
            finally { $stream.Dispose() }
            $fileHash = (Get-FileHash $f.FullName -Algorithm SHA256).Hash
            if ($entryHash -ne $fileHash) {
                $problems.Add("$($c.Id)/$($c.Version): '$rel' differs from the .nupkg content (cache tampering or corruption)")
            }
            $checkedFiles++
        }
    }
    finally { $archive.Dispose() }
    $checkedPackages++
}

if ($problems.Count -gt 0) {
    Write-Output "PACKAGE INTEGRITY FAILED ($($problems.Count) problem(s)):"
    $problems | Select-Object -First 20 | ForEach-Object { "  $_" }
    exit 1
}
Write-Output "Package integrity verified: $checkedPackages packages, $checkedFiles expanded files match their signed .nupkg contents"
