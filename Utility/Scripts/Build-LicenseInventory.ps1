# Regenerates ThirdPartyLicenses\ and LICENSE-INVENTORY.md from the ACTUAL artifact of the
# release publish (release audit B1 / F3).
#
# What this must get right, and what went wrong before:
#   - Vendor files were discovered by scanning each package's ROOT with a fixed name
#     pattern. That silently missed nested notices (Microsoft.WindowsAppSDK.WinUI's
#     tools\NOTICE.txt) and nuspec-declared license files under other names
#     (Microsoft.Windows.SDK.BuildTools.MSIX's sdk_license.txt). Discovery is now
#     RECURSIVE, keeps the relative path, and a nuspec <license type="file"> target that
#     does not end up shipped is a HARD FAILURE.
#   - The runtime packs were taken from the toolchain pin, so the inventory described what
#     we INTENDED to embed rather than what the build actually embedded (it listed a
#     WindowsDesktop pack the publish does not use, and a mis-edited pin would have been
#     copied into BUILDINFO unchallenged). They now come from the release publish's own
#     deps.json, and the pinned values are CROSS-CHECKED against it — a mismatch fails.
#   - Everything was labelled as if it were embedded payload. Components are now layered
#     (runtime payload / reference-only / build-only), so a build tool is not presented as
#     something shipped inside the exe.
#   - Package content hashes (the NuGet sha512 the restore recorded) are now part of the
#     inventory, so the recorded graph is tied to package bytes, not just to version
#     strings.
# Any unresolved component, missing declared license file or graph/artifact mismatch exits
# nonzero — the publish then refuses to ship.
param(
    [string]$Configuration = 'Release',
    [string]$Rid = 'win-x64',
    [string]$ProductName = 'SiniticKinshipTermCalculator'
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$assetsPath = Join-Path $repoRoot 'UI\obj\project.assets.json'
$tfm = 'net10.0-windows10.0.26100.0'
$depsPath = Join-Path $repoRoot "UI\obj\x64\$Configuration\$tfm\$Rid\$ProductName.deps.json"

# project.assets.json is RESTORE-CONTEXT DEPENDENT: a release publish restore pulls
# packages a plain debug restore does not (Microsoft.NET.ILLink.Tasks, from the
# single-file/trim analysis). Resolve the graph the way the RELEASE resolves it, every time.
$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -prerelease -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild.exe not found via vswhere (-prerelease).' }
& $msbuild (Join-Path $repoRoot 'UI\UI.csproj') -t:Restore `
    "-p:Configuration=$Configuration" -p:Platform=x64 "-p:RuntimeIdentifier=$Rid" `
    -p:SelfContained=true -p:WindowsAppSDKSelfContained=true -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false `
    -p:PublishReadyToRun=false -p:WindowsPackageType=None -p:PublishProfile= `
    -v:quiet -nologo | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Restore for the inventory failed with $LASTEXITCODE" }

if (-not (Test-Path $assetsPath)) { throw "project.assets.json missing after restore: $assetsPath" }
if (-not (Test-Path $depsPath)) {
    throw "Release deps.json not found: $depsPath`nThe inventory is derived from the actual publish artifact — build/publish the release first (Script\Publish-SingleFile.ps1 does this before calling here)."
}
$assets = Get-Content $assetsPath -Raw | ConvertFrom-Json
$deps = Get-Content $depsPath -Raw | ConvertFrom-Json
$toolchain = Get-Content (Join-Path $repoRoot 'Script\toolchain.lock.json') -Raw | ConvertFrom-Json

$targetName = $assets.targets.PSObject.Properties.Name | Where-Object { $_ -match [regex]::Escape($Rid) + '$' } | Select-Object -First 1
if (-not $targetName) { throw "No target for RID '$Rid' in project.assets.json" }
$packageFolders = @($assets.packageFolders.PSObject.Properties.Name)

$errors = New-Object System.Collections.Generic.List[string]
$components = [ordered]@{}

# ---- 1. PackageReference closure, LAYERED by what the entry actually contributes.
foreach ($p in $assets.targets.$targetName.PSObject.Properties) {
    if ($p.Value.type -ne 'package') { continue }
    $id, $ver = $p.Name -split '/', 2
    $keys = @($p.Value.PSObject.Properties.Name)
    $layer =
        if ($keys -contains 'runtime' -or $keys -contains 'runtimeTargets' -or $keys -contains 'native') { 'runtime payload' }
        elseif ($keys -contains 'compile') { 'reference only (compile-time)' }
        elseif ($keys -contains 'build' -or $keys -contains 'buildMultiTargeting') { 'build only (not shipped)' }
        else { 'metadata only' }
    $components["$id/$ver"] = [pscustomobject]@{ Id = $id; Version = $ver; Layer = $layer }
}

# ---- 2. Runtime packs — from the ARTIFACT (the release deps.json), not from the pin.
$depsTarget = $deps.targets.PSObject.Properties | Select-Object -Last 1
$runtimePacks = @{}
foreach ($e in $depsTarget.Value.PSObject.Properties) {
    if ($e.Name -notlike 'runtimepack.*') { continue }
    $bare = $e.Name -replace '^runtimepack\.', ''
    $id, $ver = $bare -split '/', 2
    $runtimePacks[$id] = $ver
    $components["$id/$ver"] = [pscustomobject]@{ Id = $id; Version = $ver; Layer = 'runtime pack (embedded by self-contained publish)' }
}
if ($runtimePacks.Count -eq 0) { $errors.Add('No runtime packs found in the release deps.json — the artifact does not look self-contained.') }

# ---- 3. Cross-check the toolchain pin against what the artifact really contains. The pin
# is an EXPECTATION; the artifact is the fact. A pin edited without a rebuild used to be
# copied into BUILDINFO unchallenged.
$netCorePack = "Microsoft.NETCore.App.Runtime.$Rid"
if ($runtimePacks.ContainsKey($netCorePack)) {
    if ($runtimePacks[$netCorePack] -ne $toolchain.runtimeFrameworkVersion) {
        $errors.Add("Runtime pack mismatch: artifact embeds $netCorePack/$($runtimePacks[$netCorePack]) but Script\toolchain.lock.json pins $($toolchain.runtimeFrameworkVersion)")
    }
} else {
    $errors.Add("Expected runtime pack $netCorePack not present in the release deps.json")
}
$sdkEntry = $components.Keys | Where-Object { $_ -like 'Microsoft.WindowsAppSDK/*' } | Select-Object -First 1
if ($sdkEntry) {
    $sdkVer = ($sdkEntry -split '/', 2)[1]
    if ($sdkVer -ne $toolchain.windowsAppSdkVersion) {
        $errors.Add("Windows App SDK mismatch: resolved graph has $sdkVer but Script\toolchain.lock.json pins $($toolchain.windowsAppSdkVersion)")
    }
} else {
    $errors.Add('Microsoft.WindowsAppSDK not present in the resolved graph')
}

Write-Output "Dependency graph: $($components.Count) components ($targetName); runtime packs from $([IO.Path]::GetFileName($depsPath))"

# ---- 4. Collect vendor files: RECURSIVE, path-preserving, plus the nuspec-declared file.
$licensePattern = '(?i)^(LICENSE|LICENCE|NOTICE|THIRD-?PARTY-?NOTICES|ThirdPartyNotices|.*_license)(\..*)?$'
$outDir = Join-Path $repoRoot 'ThirdPartyLicenses'
if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Path $outDir | Out-Null

$rows = New-Object System.Collections.Generic.List[object]

foreach ($c in ($components.Values | Sort-Object Id, Version)) {
    $pkgDir = $null
    foreach ($folder in $packageFolders) {
        $candidate = Join-Path $folder "$($c.Id)\$($c.Version)"
        if (Test-Path $candidate) { $pkgDir = $candidate; break }
    }
    if (-not $pkgDir) { $errors.Add("$($c.Id)/$($c.Version) — package folder not found in any package source"); continue }

    # Declared license + the file it names, straight from the nuspec.
    $license = ''
    $declaredFile = $null
    $nuspec = Get-ChildItem $pkgDir -Filter '*.nuspec' -File | Select-Object -First 1
    if ($nuspec) {
        try {
            [xml]$nx = Get-Content $nuspec.FullName -Raw
            $lic = $nx.package.metadata.license
            if ($lic -is [string]) { $license = $lic }
            elseif ($lic) {
                $license = "$($lic.type): $($lic.'#text')"
                if ($lic.type -eq 'file') { $declaredFile = $lic.'#text' }
            }
            if (-not $license -and $nx.package.metadata.licenseUrl) { $license = "url: $($nx.package.metadata.licenseUrl)" }
        } catch { $license = '(nuspec unreadable)' }
    }

    # NuGet content hash recorded by the restore — ties the row to package BYTES.
    $contentHash = ''
    $libEntry = $assets.libraries.PSObject.Properties | Where-Object { $_.Name -eq "$($c.Id)/$($c.Version)" } | Select-Object -First 1
    if ($libEntry -and $libEntry.Value.sha512) { $contentHash = $libEntry.Value.sha512 }
    if (-not $contentHash) {
        $nupkgSha = Get-ChildItem $pkgDir -Filter '*.nupkg.sha512' -File | Select-Object -First 1
        if ($nupkgSha) { $contentHash = (Get-Content $nupkgSha.FullName -Raw).Trim() }
    }

    # RECURSIVE discovery, relative path preserved.
    $found = Get-ChildItem $pkgDir -Recurse -File | Where-Object { $_.Name -match $licensePattern }
    if ($declaredFile) {
        $declaredPath = Join-Path $pkgDir ($declaredFile -replace '/', '\')
        if (-not (Test-Path $declaredPath)) {
            $errors.Add("$($c.Id)/$($c.Version) — nuspec declares license file '$declaredFile' but it is not in the package")
        } elseif (-not ($found | Where-Object { $_.FullName -eq (Get-Item $declaredPath).FullName })) {
            $found = @($found) + @(Get-Item $declaredPath)
        }
    }

    $shipped = @()
    foreach ($f in ($found | Sort-Object FullName -Unique)) {
        $relInPkg = $f.FullName.Substring($pkgDir.Length + 1).Replace('\', '/')
        $destRel = "$($c.Id).$($c.Version)/$relInPkg"
        $dest = Join-Path $outDir ($destRel -replace '/', '\')
        New-Item -ItemType Directory -Path (Split-Path $dest -Parent) -Force | Out-Null
        Copy-Item $f.FullName $dest
        $shipped += [pscustomobject]@{
            Name = $relInPkg
            Size = $f.Length
            Sha = (Get-FileHash $f.FullName -Algorithm SHA256).Hash
            Rel = "ThirdPartyLicenses/$destRel"
        }
    }

    # The declared file must actually be among the shipped set.
    if ($declaredFile) {
        $declaredRel = ($declaredFile -replace '\\', '/')
        if (-not ($shipped | Where-Object { $_.Name -eq $declaredRel })) {
            $errors.Add("$($c.Id)/$($c.Version) — nuspec-declared license file '$declaredFile' was not shipped")
        }
    }

    $rows.Add([pscustomobject]@{
        Id = $c.Id; Version = $c.Version; Layer = $c.Layer
        License = $license; ContentHash = $contentHash; Files = $shipped
    })
}

# ---- 5. Emit the inventory.
$totalFiles = ($rows | ForEach-Object { $_.Files.Count } | Measure-Object -Sum).Sum
$doc = New-Object System.Collections.Generic.List[string]
$doc.Add('# Third-party license inventory')
$doc.Add('')
$doc.Add('**Generated** by `Utility\Scripts\Build-LicenseInventory.ps1` from the ARTIFACT of the')
$doc.Add('release publish: the resolved PackageReference closure in `UI\obj\project.assets.json`')
$doc.Add('(restored with the release properties) plus the runtime packs named in the release')
$doc.Add('build''s own `deps.json`. Vendor files are discovered RECURSIVELY inside each package,')
$doc.Add('and a package whose nuspec declares `<license type="file">` must ship that exact file or')
$doc.Add('generation fails. It is not hand-maintained and cannot drift from the artifact.')
$doc.Add('')
$doc.Add("- Target: ``$targetName``")
$doc.Add("- Runtime packs (from the artifact): " + (($runtimePacks.GetEnumerator() | Sort-Object Key | ForEach-Object { "``$($_.Key)/$($_.Value)``" }) -join ', '))
$doc.Add("- Components: **$($rows.Count)**")
$doc.Add("- Files reproduced under ``ThirdPartyLicenses\``: **$totalFiles**")
$doc.Add('')
$doc.Add('> **Layering matters.** Only rows marked *runtime payload* or *runtime pack* are')
$doc.Add('> carried inside the shipped executable. *Reference only* and *build only* rows take')
$doc.Add('> part in producing it without being embedded; they are listed for completeness of')
$doc.Add('> the build graph, not as redistributed binaries.')
$doc.Add('')
$doc.Add('> **Legal review status:** this inventory is a *technical completeness* artifact —')
$doc.Add('> it proves which vendor files exist for which component of the build and ships them.')
$doc.Add('> A formal legal review of the redistribution terms (notably the Windows App SDK')
$doc.Add('> Software License Terms and their end-user pass-through obligations, the Windows AI')
$doc.Add('> MachineLearning terms, and the .NET self-contained distribution surface) has **not**')
$doc.Add('> been performed. That review remains an open release item.')
$doc.Add('')
$doc.Add('## Components')
$doc.Add('')
$doc.Add('| Component | Version | Layer | Declared license | Shipped license/notice files |')
$doc.Add('|---|---|---|---|---|')
foreach ($r in ($rows | Sort-Object Layer, Id)) {
    $fileCell = if ($r.Files.Count -eq 0) { '— *(none in package)*' } else { ($r.Files | ForEach-Object { "``$($_.Name)``" }) -join '<br>' }
    $lic = if ($r.License) { $r.License } else { '— *(not declared in nuspec)*' }
    $doc.Add("| $($r.Id) | $($r.Version) | $($r.Layer) | $lic | $fileCell |")
}
$doc.Add('')
$doc.Add('## Package content hashes (as recorded by the restore)')
$doc.Add('')
$doc.Add('| Component | Version | NuGet content hash |')
$doc.Add('|---|---|---|')
foreach ($r in ($rows | Sort-Object Id)) {
    $ch = if ($r.ContentHash) { "``$($r.ContentHash)``" } else { '— *(runtime pack / no recorded hash)*' }
    $doc.Add("| $($r.Id) | $($r.Version) | $ch |")
}
$doc.Add('')
$doc.Add('## Shipped files (SHA-256)')
$doc.Add('')
$doc.Add('| File | Bytes | SHA-256 |')
$doc.Add('|---|---:|---|')
foreach ($r in ($rows | Sort-Object Id)) {
    foreach ($f in $r.Files) { $doc.Add("| ``$($f.Rel)`` | $($f.Size) | ``$($f.Sha)`` |") }
}
$doc.Add('')
$doc.Add('## Components shipping no license file')
$doc.Add('')
$doc.Add('These packages carry no LICENSE/NOTICE file of their own. Their terms are the declared')
$doc.Add('license expression in the table above.')
$doc.Add('')
foreach ($r in ($rows | Where-Object { $_.Files.Count -eq 0 } | Sort-Object Id)) {
    $doc.Add("- $($r.Id) $($r.Version) — $(if ($r.License) { $r.License } else { 'no declared license metadata' })")
}
$doc.Add('')

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $repoRoot 'LICENSE-INVENTORY.md'), ($doc -join "`n") + "`n", $utf8NoBom)

# The .NET-on-Windows note is EMITTED here rather than hand-placed in ThirdPartyLicenses\:
# this script wipes and rebuilds that folder, so anything hand-dropped would be lost.
$dotnetNote = @(
    '# .NET on Windows — licensing note for this self-contained distribution',
    '',
    'The executable in this package is a **self-contained** .NET publish for Windows: it embeds',
    'the .NET runtime pack listed in `LICENSE-INVENTORY.md`. Most of .NET is MIT-licensed, but',
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
    'The runtime pack''s own `LICENSE.TXT` and `THIRD-PARTY-NOTICES.TXT` are reproduced beside',
    'this note in its per-package folder. The Windows App SDK runtime embedded by',
    '`WindowsAppSDKSelfContained=true` is governed by the Microsoft Software License Terms in',
    'the `Microsoft.WindowsAppSDK.*` folders.',
    ''
)
[IO.File]::WriteAllText((Join-Path $outDir 'DotNet-Windows-Licensing.md'), ($dotnetNote -join "`n") + "`n", $utf8NoBom)

if ($errors.Count -gt 0) {
    Write-Output "LICENSE INVENTORY FAILED ($($errors.Count) problem(s)):"
    $errors | ForEach-Object { "  $_" }
    exit 1
}
Write-Output "Inventory written: LICENSE-INVENTORY.md ($($rows.Count) components, $totalFiles files under ThirdPartyLicenses\)"
