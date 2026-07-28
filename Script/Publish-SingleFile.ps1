# Publishes the UI app as a self-contained single-file unpackaged exe and assembles the
# whole DISTRIBUTABLE UNIT into Distribution\ (fresh staging, whitelist-driven), plus a
# release ZIP. Recipe per the shared WinUI single-file playbook (OpenMIC/辭錄): MSBuild
# only, both runtime layers self-contained, no trimming, EnableMsixTooling on for
# resources.pri, product name fixed at build time via AssemblyName (never rename
# post-build). Requires PowerShell 7+ (pwsh).
param(
    [string]$ProductName = 'SiniticKinshipTermCalculator',
    # Optional signing hook, run at the ONLY correct point (staged exe, before hashing and
    # zipping): a command line with {0} replaced by the staged exe path, e.g.
    #   -SignCommand 'signtool sign /fd SHA256 /a "{0}"'
    # After it runs, the signature must verify or the publish fails. Re-running the script
    # without the hook rebuilds an UNSIGNED artifact — signing is part of the pipeline, not
    # a post-step, so the manifest always describes the bytes actually shipped.
    [string]$SignCommand = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'UI\UI.csproj'
$distribution = Join-Path $repoRoot 'Distribution'

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -prerelease -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild.exe not found via vswhere (-prerelease).' }
# Toolchain provenance: the SDK band is pinned by global.json; the VS/MSBuild pick is the
# machine's latest (incl. prerelease) by standing selection, so RECORD what was used.
$head = (git -C $repoRoot rev-parse HEAD).Trim()
Write-Output "Toolchain: MSBuild $((Get-Item $msbuild).VersionInfo.ProductVersion) | HEAD $head"

# Dirty-input rejection: the +sha stamp records HEAD, not the bytes compiled — the audit
# built an uncommitted window-title edit into an EXE that still claimed to be HEAD. A
# release artifact may only be produced from a fully clean tree (no exceptions here; the
# validation loop's derived-workbook allowance does not apply to shipping).
$dirty = @(git -C $repoRoot status --porcelain)
if ($dirty.Count -gt 0) {
    Write-Output 'DIRTY TREE (commit or remove before publishing):'
    $dirty | Select-Object -First 10 | ForEach-Object { "  $_" }
    throw 'Publish refused on a dirty tree.'
}

# Restore MUST be a separate MSBuild invocation: a single -t:Restore,Publish call on a
# clean clone evaluates the project before the restored WinUI/XAML source generators are
# wired in, failing with CS5001 (no Main) / CS0103 (no InitializeComponent) on first run
# and only succeeding on the retry that finds the warmed obj\.
$common = @(
    '-p:Configuration=Release',
    '-p:Platform=x64',
    '-p:RuntimeIdentifier=win-x64',
    '-p:SelfContained=true',
    '-p:WindowsAppSDKSelfContained=true',
    '-p:PublishSingleFile=true',
    '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-p:PublishTrimmed=false',
    '-p:PublishReadyToRun=false',   # keep the CLI publish byte-consistent with Distribution.pubxml
    '-p:DebugType=None',
    '-p:DebugSymbols=false',
    '-p:EnableMsixTooling=true',
    '-p:WindowsPackageType=None',
    "-p:PublishProductName=$ProductName",
    '-p:PublishProfile=',
    '-v:minimal', '-nologo'
)
& $msbuild $project -t:Restore @common
if ($LASTEXITCODE -ne 0) { throw "Restore failed with $LASTEXITCODE" }
& $msbuild $project -t:Publish @common
if ($LASTEXITCODE -ne 0) { throw "Publish failed with $LASTEXITCODE" }

$publishDir = Join-Path $repoRoot 'UI\bin\x64\Release\net10.0-windows10.0.26100.0\win-x64\publish'
$exe = Join-Path $publishDir "$ProductName.exe"
if (-not (Test-Path $exe)) { throw "Published exe not found: $exe" }

# Provenance seal (same law as the validation loop): the artifact must be stamped with the
# current HEAD. An incremental publish after a docs-only commit keeps the old stamp —
# rebuild once, then hard-fail rather than ship a binary that misstates its source.
function Get-ExeRevision([string]$path) {
    $pv = (Get-Item $path).VersionInfo.ProductVersion
    if ($pv -match '\+([0-9a-f]{7,40})$') { return $Matches[1] } else { return '' }
}
if ((Get-ExeRevision $exe) -ne $head) {
    & $msbuild $project -t:Rebuild @common
    if ($LASTEXITCODE -ne 0) { throw "Rebuild failed with $LASTEXITCODE" }
    & $msbuild $project -t:Publish @common
    if ($LASTEXITCODE -ne 0) { throw "Re-publish failed with $LASTEXITCODE" }
}
$rev = Get-ExeRevision $exe
if ($rev -ne $head) { throw "PROVENANCE FAILED: exe stamped '$rev', HEAD is '$head'" }
Write-Output "Artifact provenance-sealed to $head"

# ---- Assemble the distributable unit in FRESH whitelist staging on the SAME VOLUME as
# Distribution\ (a %TEMP% staging on another drive made the final Move a copy+delete with
# a visible window; a same-volume rename is instantaneous). The old flow copied into
# whatever Distribution\ already held; the audit pre-planted a stale file and it shipped.
$staging = Join-Path $repoRoot ("Distribution.staging-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
New-Item -ItemType Directory -Path (Join-Path $staging 'Lexicon') | Out-Null
New-Item -ItemType Directory -Path (Join-Path $staging 'ThirdPartyLicenses') | Out-Null

Copy-Item $exe (Join-Path $staging "$ProductName.exe")
# The lexicon layers ship as EDITABLE files next to the exe (the same layers are embedded
# in the assembly; loose files are the extension point and never break the app if deleted).
$lexiconSource = Join-Path $repoRoot 'Resource\Data\Lexicon'
Copy-Item (Join-Path $lexiconSource '*.yaml') (Join-Path $staging 'Lexicon')
Copy-Item (Join-Path $lexiconSource '*.yaml.txt') (Join-Path $staging 'Lexicon')
# Licensing set: the package must explain itself without the repository, and the WebView2
# license REQUIRES its text to accompany binary redistribution.
Copy-Item (Join-Path $repoRoot 'LICENSE') (Join-Path $staging 'LICENSE')
Copy-Item (Join-Path $repoRoot 'ATTRIBUTION.md') (Join-Path $staging 'ATTRIBUTION.md')
Copy-Item (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.md') (Join-Path $staging 'THIRD-PARTY-NOTICES.md')
Copy-Item (Join-Path $repoRoot 'ThirdPartyLicenses\*') (Join-Path $staging 'ThirdPartyLicenses')

# Signing happens HERE — on the staged exe, before any hash or zip exists, so the manifest
# always describes the shipped bytes. (Signing after manifest/zip invalidates both.)
if ($SignCommand) {
    $stagedExe = Join-Path $staging "$ProductName.exe"
    $cmd = $SignCommand -f $stagedExe
    Write-Output "Signing: $cmd"
    Invoke-Expression $cmd
    if ($LASTEXITCODE -ne 0) { throw "Sign command failed with $LASTEXITCODE" }
    $sig = Get-AuthenticodeSignature $stagedExe
    if ($sig.Status -ne 'Valid') { throw "Signature did not verify: $($sig.Status) $($sig.StatusMessage)" }
    Write-Output "Signature verified: $($sig.SignerCertificate.Subject)"
}

# Integrity manifest over every packaged file (the Lexicon layers override embedded data
# at runtime, so a tampered layer changes what users see).
$manifest = Join-Path $staging 'SHA256SUMS.txt'
Get-ChildItem $staging -Recurse -File | Sort-Object { $_.FullName.Substring($staging.Length + 1) } | ForEach-Object {
    $rel = $_.FullName.Substring($staging.Length + 1).Replace('\', '/')
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)  $rel"
} | Set-Content $manifest -Encoding ascii

# DETERMINISTIC release ZIP: entries sorted by path, every entry timestamp fixed to the
# COMMIT time (not the build time). Compress-Archive stored per-file build timestamps, so
# the same commit produced a different zip hash on every run — the audit measured three
# different hashes from identical inputs. Built outside staging so it cannot swallow
# itself; the zip's own hash goes into the loose manifest only (a file cannot contain its
# own digest).
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$commitTime = [DateTimeOffset]::Parse((git -C $repoRoot log -1 --format=%cI).Trim())
$zipName = "$ProductName-$($rev.Substring(0, 9)).zip"
$zipTemp = Join-Path $repoRoot ("Distribution.staging-zip-" + [Guid]::NewGuid().ToString('N'))
try {
    $fs = [System.IO.File]::Open($zipTemp, [System.IO.FileMode]::CreateNew)
    $zip = New-Object System.IO.Compression.ZipArchive($fs, [System.IO.Compression.ZipArchiveMode]::Create)
    Get-ChildItem $staging -Recurse -File | Sort-Object { $_.FullName.Substring($staging.Length + 1) } | ForEach-Object {
        $rel = $_.FullName.Substring($staging.Length + 1).Replace('\', '/')
        $entry = $zip.CreateEntry($rel, [System.IO.Compression.CompressionLevel]::Optimal)
        $entry.LastWriteTime = $commitTime
        $in = [System.IO.File]::OpenRead($_.FullName)
        $out = $entry.Open()
        $in.CopyTo($out)
        $out.Dispose(); $in.Dispose()
    }
    $zip.Dispose(); $fs.Dispose()
    Move-Item $zipTemp (Join-Path $staging $zipName)
}
finally {
    if (Test-Path $zipTemp) { Remove-Item $zipTemp -Force }
}
"$((Get-FileHash (Join-Path $staging $zipName) -Algorithm SHA256).Hash)  $zipName" | Add-Content $manifest -Encoding ascii

# Same-volume rename swap (no cross-volume copy window): old folder renamed aside, fresh
# one renamed in, old removed last.
$old = "$distribution.old-" + [Guid]::NewGuid().ToString('N')
if (Test-Path $distribution) { Move-Item $distribution $old }
Move-Item $staging $distribution
if (Test-Path $old) { Remove-Item $old -Recurse -Force }

$size = [Math]::Round((Get-Item (Join-Path $distribution "$ProductName.exe")).Length / 1MB, 1)
$layerCount = (Get-ChildItem (Join-Path $distribution 'Lexicon') -Filter '*.yaml').Count
$entryCount = (Get-Content (Join-Path $distribution 'SHA256SUMS.txt')).Count
$signState = if ($SignCommand) { 'signed' } else { 'UNSIGNED' }
Write-Output "Published ($signState): Distribution\$ProductName.exe (${size} MB) + Lexicon\ ($layerCount layers) + licensing set + $zipName | SHA256SUMS.txt ($entryCount entries)"
