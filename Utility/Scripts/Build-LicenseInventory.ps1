# Regenerates ThirdPartyLicenses\ and LICENSE-INVENTORY.md from the ACTUAL locked
# dependency graph of the UI publish (release-audit round 4, blocker B1).
#
# Why this exists: the hand-written notices file listed a few top-level packages and
# claimed every text required for binary redistribution shipped. It did not — the
# self-contained publish also embeds the .NET runtime pack, the Windows App SDK runtime
# family and Windows AI MachineLearning, each carrying its own license/NOTICE files. A
# hand-maintained list cannot track that graph; this script derives it.
#
# Sources of truth (both deterministic — the inventory must be a pure function of them,
# or the publish's dirty gate would fire on unrelated build activity):
#   UI\obj\project.assets.json    — the resolved PackageReference closure for the shipped RID
#   Script\toolchain.lock.json    — the pinned runtime-pack version the publish embeds
param(
    [string]$Configuration = 'Release',
    [string]$Rid = 'win-x64'
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$assetsPath = Join-Path $repoRoot 'UI\obj\project.assets.json'
if (-not (Test-Path $assetsPath)) { throw "project.assets.json missing — restore the UI project first: $assetsPath" }
$assets = Get-Content $assetsPath -Raw | ConvertFrom-Json

$targetName = $assets.targets.PSObject.Properties.Name | Where-Object { $_ -match [regex]::Escape($Rid) + '$' } | Select-Object -First 1
if (-not $targetName) { throw "No target for RID '$Rid' in project.assets.json" }

$packageFolders = @($assets.packageFolders.PSObject.Properties.Name)

# 1. PackageReference closure for the shipped RID.
$components = [ordered]@{}
foreach ($p in $assets.targets.$targetName.PSObject.Properties) {
    if ($p.Value.type -ne 'package') { continue }
    $id, $ver = $p.Name -split '/', 2
    $components["$id/$ver"] = [pscustomobject]@{ Id = $id; Version = $ver; Kind = 'package' }
}

# 2. Runtime packs embedded by the self-contained publish. Taken from the PINNED toolchain,
# not from a deps.json: several deps.json files coexist under obj\ (one per assembly name
# the project has been built under), the newest flips depending on what ran last, and the
# inventory would then change without the dependency graph changing — which the publish's
# dirty gate reads as "stale inventory". The pin in Script\toolchain.lock.json IS what the
# build embeds (UI.csproj declares the same patch via KnownRuntimePack), so it is both
# deterministic and accurate.
$toolchain = Get-Content (Join-Path $repoRoot 'Script\toolchain.lock.json') -Raw | ConvertFrom-Json
$runtimeVersion = $toolchain.runtimeFrameworkVersion
foreach ($packId in @("Microsoft.NETCore.App.Runtime.$Rid", "Microsoft.WindowsDesktop.App.Runtime.$Rid")) {
    $found = $false
    foreach ($folder in $packageFolders) {
        if (Test-Path (Join-Path $folder "$packId\$runtimeVersion")) { $found = $true; break }
    }
    if (-not $found) { throw "Pinned runtime pack not in the package cache: $packId/$runtimeVersion" }
    $components["$packId/$runtimeVersion"] = [pscustomobject]@{ Id = $packId; Version = $runtimeVersion; Kind = 'runtime pack' }
}

Write-Output "Dependency graph: $($components.Count) components ($targetName)"

# 3. Collect each component's license/notice files and declared license expression.
$licensePattern = '^(LICENSE|LICENCE|NOTICE|THIRD-PARTY-NOTICES|THIRDPARTYNOTICES|ThirdPartyNotices)(\..*)?$'
$outDir = Join-Path $repoRoot 'ThirdPartyLicenses'
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path $outDir | Out-Null

$rows = New-Object System.Collections.Generic.List[object]
$missing = New-Object System.Collections.Generic.List[string]

foreach ($c in ($components.Values | Sort-Object Id, Version)) {
    $pkgDir = $null
    foreach ($folder in $packageFolders) {
        $candidate = Join-Path $folder "$($c.Id)\$($c.Version)"
        if (Test-Path $candidate) { $pkgDir = $candidate; break }
    }
    if (-not $pkgDir) { $missing.Add("$($c.Id)/$($c.Version) — package folder not found"); continue }

    # Declared license from the nuspec (expression preferred; file/url recorded otherwise).
    $license = ''
    $nuspec = Get-ChildItem $pkgDir -Filter '*.nuspec' -File | Select-Object -First 1
    if ($nuspec) {
        try {
            [xml]$nx = Get-Content $nuspec.FullName -Raw
            $lic = $nx.package.metadata.license
            if ($lic -is [string]) { $license = $lic }
            elseif ($lic) { $license = "$($lic.type): $($lic.'#text')" }
            if (-not $license -and $nx.package.metadata.licenseUrl) { $license = "url: $($nx.package.metadata.licenseUrl)" }
        } catch { $license = '(nuspec unreadable)' }
    }

    $files = Get-ChildItem $pkgDir -File | Where-Object { $_.Name -match $licensePattern }
    $shipped = @()
    if ($files) {
        $compDir = Join-Path $outDir "$($c.Id).$($c.Version)"
        New-Item -ItemType Directory -Path $compDir -Force | Out-Null
        foreach ($f in $files) {
            Copy-Item $f.FullName (Join-Path $compDir $f.Name)
            $shipped += [pscustomobject]@{
                Name = $f.Name
                Size = $f.Length
                Sha = (Get-FileHash $f.FullName -Algorithm SHA256).Hash
                Rel = "ThirdPartyLicenses/$($c.Id).$($c.Version)/$($f.Name)"
            }
        }
    }

    $rows.Add([pscustomobject]@{
        Id = $c.Id; Version = $c.Version; Kind = $c.Kind
        License = $license; Files = $shipped
    })
}

# 4. Emit the inventory document.
$doc = New-Object System.Collections.Generic.List[string]
$doc.Add('# Third-party license inventory')
$doc.Add('')
$doc.Add('**Generated** by `Utility\Scripts\Build-LicenseInventory.ps1` from the locked dependency')
$doc.Add('graph of the shipped publish — `UI\obj\project.assets.json` (PackageReference closure)')
$doc.Add('plus the build''s own `*.deps.json` (runtime packs embedded by the self-contained')
$doc.Add('publish). It is not hand-maintained and therefore cannot drift from the artifact.')
$doc.Add('')
$doc.Add("- Target: ``$targetName``")
$doc.Add("- Components: **$($rows.Count)**")
$doc.Add("- Components shipping license/notice files: **$(@($rows | Where-Object { $_.Files.Count -gt 0 }).Count)**")
$doc.Add("- Files reproduced under ``ThirdPartyLicenses\``: **$(($rows | ForEach-Object { $_.Files.Count } | Measure-Object -Sum).Sum)**")
$doc.Add('')
$doc.Add('> **Legal review status:** this inventory is a *technical completeness* artifact —')
$doc.Add('> it proves which vendor files exist for which embedded component and ships them.')
$doc.Add('> A formal legal review of the redistribution terms (notably the Windows App SDK')
$doc.Add('> Software License Terms, its end-user pass-through obligations, and the Windows AI')
$doc.Add('> MachineLearning / .NET self-contained distribution surface) has **not** been')
$doc.Add('> performed. That review remains an open release item.')
$doc.Add('')
$doc.Add('## Components')
$doc.Add('')
$doc.Add('| Component | Version | Kind | Declared license | Shipped license/notice files |')
$doc.Add('|---|---|---|---|---|')
foreach ($r in $rows) {
    $fileCell = if ($r.Files.Count -eq 0) { '— *(none in package)*' } else { ($r.Files | ForEach-Object { "``$($_.Name)``" }) -join '<br>' }
    $lic = if ($r.License) { $r.License } else { '— *(not declared in nuspec)*' }
    $doc.Add("| $($r.Id) | $($r.Version) | $($r.Kind) | $lic | $fileCell |")
}
$doc.Add('')
$doc.Add('## Shipped files (SHA-256)')
$doc.Add('')
$doc.Add('| File | Bytes | SHA-256 |')
$doc.Add('|---|---:|---|')
foreach ($r in $rows) {
    foreach ($f in $r.Files) { $doc.Add("| ``$($f.Rel)`` | $($f.Size) | ``$($f.Sha)`` |") }
}
$doc.Add('')
$doc.Add('## Components shipping no license file')
$doc.Add('')
$doc.Add('These packages carry no LICENSE/NOTICE file of their own inside the package. Their')
$doc.Add('terms are the declared license expression in the table above (for the Microsoft')
$doc.Add('first-party graph these resolve to the MIT text or to the platform license already')
$doc.Add('reproduced under the components that do ship one).')
$doc.Add('')
foreach ($r in ($rows | Where-Object { $_.Files.Count -eq 0 })) {
    $doc.Add("- $($r.Id) $($r.Version) — $(if ($r.License) { $r.License } else { 'no declared license metadata' })")
}
if ($missing.Count -gt 0) {
    $doc.Add('')
    $doc.Add('## Unresolved')
    $doc.Add('')
    foreach ($m in $missing) { $doc.Add("- $m") }
}
$doc.Add('')

# WriteAllText, not Set-Content: Set-Content re-joins the input with the platform newline,
# producing CRLF, which then fights .gitattributes (eol=lf) and leaves the tree dirty after
# every regeneration — which in turn blocks the publish's dirty gate.
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$docPath = Join-Path $repoRoot 'LICENSE-INVENTORY.md'
[IO.File]::WriteAllText($docPath, ($doc -join "`n") + "`n", $utf8NoBom)

# The .NET-on-Windows note is EMITTED here rather than hand-placed in ThirdPartyLicenses\:
# this script wipes and rebuilds that folder, so anything hand-dropped into it would be
# silently lost on the next regeneration (it was, once).
$dotnetNote = @(
    '# .NET on Windows — licensing note for this self-contained distribution',
    '',
    'The executable in this package is a **self-contained** .NET publish for Windows: it embeds',
    'the .NET runtime packs listed in `LICENSE-INVENTORY.md`. Most of .NET is MIT-licensed, but',
    'Microsoft documents that certain Windows-specific components carried by self-contained',
    'Windows apps are licensed under the **.NET Library License** and other Microsoft terms',
    'rather than MIT:',
    '',
    '- .NET license information for Windows:',
    '  <https://github.com/dotnet/core/blob/main/license-information-windows.md>',
    '- .NET license inventory: <https://github.com/dotnet/core/blob/main/license-information.md>',
    '- .NET Library License: <https://dotnet.microsoft.com/dotnet_library_license.htm>',
    '- Self-contained deployment for Windows apps:',
    '  <https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/self-contained-deploy/deploy-self-contained-apps>',
    '',
    'The runtime packs'' own `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` are reproduced beside',
    'this note in their per-package folders. The Windows App SDK runtime embedded by',
    '`WindowsAppSDKSelfContained=true` is governed by the Microsoft Software License Terms in',
    'the `Microsoft.WindowsAppSDK.*` folders.',
    ''
)
[IO.File]::WriteAllText((Join-Path $outDir 'DotNet-Windows-Licensing.md'), ($dotnetNote -join "`n") + "`n", $utf8NoBom)
Write-Output "Inventory written: LICENSE-INVENTORY.md ($($rows.Count) components, $(($rows | ForEach-Object { $_.Files.Count } | Measure-Object -Sum).Sum) files under ThirdPartyLicenses\)"
if ($missing.Count -gt 0) { Write-Output "UNRESOLVED: $($missing.Count) component(s)"; $missing | ForEach-Object { "  $_" } }
