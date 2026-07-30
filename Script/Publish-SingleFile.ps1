# Publishes the UI app as a self-contained single-file unpackaged exe and assembles the
# whole DISTRIBUTABLE UNIT into Distribution\ (fresh staging, whitelist-driven), plus a
# deterministic release ZIP. Requires PowerShell 7+ (pwsh).
#
# Release-audit guarantees implemented here:
#   B2  the resolved toolchain must match Script\toolchain.lock.json exactly, and every
#       package carries a BUILDINFO recording what produced it — a release artifact is
#       reproducible from the public commit alone, not "self-declared to come from it".
#   B1  the third-party licensing set is REGENERATED from the locked dependency graph
#       before the dirty check, so a stale inventory shows up as a dirty tree instead of
#       shipping an incomplete legal package.
#   B3  the Distribution swap is transactional: any failure restores the previous
#       Distribution and removes staging; nothing is left half-swapped.
param(
    [string]$ProductName = 'SiniticKinshipTermCalculator',
    # Optional signing hook, run at the ONLY correct point (staged exe, before hashing and
    # zipping): a command line with {0} replaced by the staged exe path, e.g.
    #   -SignCommand 'signtool sign /fd SHA256 /a "{0}"'
    # After it runs, the signature must verify or the publish fails. Re-running the script
    # without the hook rebuilds an UNSIGNED artifact — signing is part of the pipeline, not
    # a post-step, so the manifest always describes the bytes actually shipped.
    [string]$SignCommand = '',
    # Fault-injection points used by Script\Test-PublishFaultInjection.ps1 to prove the
    # transactional swap. Never set during a real release.
    [ValidateSet('', 'sign', 'before-swap', 'mid-swap')]
    [string]$SimulateFailure = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'UI\UI.csproj'
$distribution = Join-Path $repoRoot 'Distribution'
$lockPath = Join-Path $repoRoot 'Script\toolchain.lock.json'
$lock = Get-Content $lockPath -Raw | ConvertFrom-Json

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
$msbuild = & $vswhere -prerelease -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild.exe not found via vswhere (-prerelease).' }
$msbuildVersion = (Get-Item $msbuild).VersionInfo.ProductVersion
# dotnet resolves the SDK from global.json in the CURRENT DIRECTORY. Run these from the
# repository or they report whatever SDK is newest on the machine (an 11.0 preview here)
# and the recorded identity would describe a compiler that never touched this build.
Push-Location $repoRoot
try {
    $sdkVersion = (& dotnet --version).Trim()
    $sdkInfo = & dotnet --info
}
finally { Pop-Location }
$sdkBasePath = (($sdkInfo | Select-String -Pattern '^\s*Base Path:\s*(.+)$' | Select-Object -First 1).Matches.Groups[1].Value).Trim()
$sdkCommit = (($sdkInfo | Select-String -Pattern '^\s*Commit:\s*([0-9a-f]+)' | Select-Object -First 1).Matches.Groups[1].Value).Trim()
$roslyn = Join-Path $sdkBasePath 'Roslyn\bincore\csc.dll'
$head = (git -C $repoRoot rev-parse HEAD).Trim()

# ---- B2/F2: the toolchain must match the pin EXACTLY, by identity as well as by name.
# A prefix comparison was accepting 18.9.10, 18.9.100-foreign and even "18.9.1evil"; and a
# version string alone does not identify the bytes that actually compile the product, so
# the compiler binaries are pinned by SHA-256 too.
$msbuildSha = (Get-FileHash $msbuild -Algorithm SHA256).Hash
$roslynSha = if (Test-Path $roslyn) { (Get-FileHash $roslyn -Algorithm SHA256).Hash } else { '(csc.dll not found)' }

$toolchainErrors = @()
if ($sdkVersion -ne $lock.sdkVersion) { $toolchainErrors += "SDK version $sdkVersion != pinned $($lock.sdkVersion)" }
if ($lock.sdkCommit -and $sdkCommit -ne $lock.sdkCommit) { $toolchainErrors += "SDK commit $sdkCommit != pinned $($lock.sdkCommit)" }
if ($msbuildVersion -ne $lock.msbuildVersion) { $toolchainErrors += "MSBuild version $msbuildVersion != pinned $($lock.msbuildVersion)" }
if ($lock.msbuildSha256 -and $msbuildSha -ne $lock.msbuildSha256) { $toolchainErrors += "MSBuild.exe SHA-256 $msbuildSha != pinned $($lock.msbuildSha256)" }
if ($lock.roslynCscSha256 -and $roslynSha -ne $lock.roslynCscSha256) { $toolchainErrors += "Roslyn csc.dll SHA-256 $roslynSha != pinned $($lock.roslynCscSha256)" }
if ($toolchainErrors.Count -gt 0) {
    Write-Output 'TOOLCHAIN MISMATCH (edit Script\toolchain.lock.json to re-pin deliberately):'
    $toolchainErrors | ForEach-Object { "  $_" }
    throw 'Publish refused: toolchain does not match the release pin.'
}
Write-Output "Toolchain pinned+verified: SDK $sdkVersion | MSBuild $msbuildVersion | runtime pack $($lock.runtimeFrameworkVersion) | HEAD $head"

# ---- Live-directory guard (release audit F1). A project target used to copy the fresh exe
# into the live Distribution\ during Publish — outside this script's transaction — so a
# later failure left a new exe beside the old manifest and the fault test still passed.
# The target is gone; this snapshot makes any future writer impossible to miss: the live
# directory must be byte-identical when the swap begins.
function Get-TreeSnapshot([string]$dir) {
    $snap = @{}
    if (Test-Path $dir) {
        Get-ChildItem $dir -Recurse -File | ForEach-Object {
            $snap[$_.FullName.Substring($dir.Length + 1)] = (Get-FileHash $_.FullName -Algorithm SHA256).Hash
        }
    }
    return $snap
}
$liveBefore = Get-TreeSnapshot $distribution

$inventoryScript = Join-Path $repoRoot 'Utility\Scripts\Build-LicenseInventory.ps1'

# ---- Dirty-input rejection: the +sha stamp records HEAD, not the bytes compiled — the
# audit built an uncommitted window-title edit into an EXE that still claimed HEAD. A
# release artifact may only be produced from a fully clean tree.
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
    "-p:RuntimeIdentifier=$($lock.targetRuntimeIdentifier)",
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

$publishDir = Join-Path $repoRoot "UI\bin\x64\Release\net10.0-windows10.0.26100.0\$($lock.targetRuntimeIdentifier)\publish"
$exe = Join-Path $publishDir "$ProductName.exe"
if (-not (Test-Path $exe)) { throw "Published exe not found: $exe" }

# Provenance seal: the artifact must be stamped with the current HEAD. An incremental
# publish after a docs-only commit keeps the old stamp — rebuild once, then hard-fail
# rather than ship a binary that misstates its source.
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

# ---- B1/F3: the licensing inventory is derived from THIS build's artifact (the release
# deps.json names the runtime packs actually embedded), so it can only run after the
# publish. It hard-fails on an unresolved component, a nuspec-declared license file that
# is not shipped, or a toolchain pin that disagrees with the artifact.
& $inventoryScript -Configuration Release -Rid $lock.targetRuntimeIdentifier -ProductName $ProductName | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Publish refused: license inventory generation reported problems.' }
$dirtyAfter = @(git -C $repoRoot status --porcelain)
if ($dirtyAfter.Count -gt 0) {
    Write-Output 'LICENSING INVENTORY STALE (regenerate, review and commit before publishing):'
    $dirtyAfter | Select-Object -First 10 | ForEach-Object { "  $_" }
    throw 'Publish refused: the committed licensing inventory does not match the resolved dependency graph.'
}

# ---- Assemble the distributable unit in FRESH whitelist staging on the SAME VOLUME as
# Distribution\. The whole assembly + swap is transactional (B3).
$staging = Join-Path $repoRoot ("Distribution.staging-" + [Guid]::NewGuid().ToString('N'))
$old = $null
try {
    New-Item -ItemType Directory -Path $staging | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $staging 'Lexicon') | Out-Null

    Copy-Item $exe (Join-Path $staging "$ProductName.exe")
    # The lexicon layers ship as EDITABLE files next to the exe (the same layers are
    # embedded in the assembly; loose files are the extension point and never break the
    # app if deleted).
    $lexiconSource = Join-Path $repoRoot 'Resource\Data\Lexicon'
    Copy-Item (Join-Path $lexiconSource '*.yaml') (Join-Path $staging 'Lexicon')
    Copy-Item (Join-Path $lexiconSource '*.yaml.txt') (Join-Path $staging 'Lexicon')
    # Licensing set: the package must explain itself without the repository, and several
    # vendor licenses (WebView2's among them) require their text to accompany binary
    # redistribution. ThirdPartyLicenses\ is the generated per-component tree.
    Copy-Item (Join-Path $repoRoot 'LICENSE') (Join-Path $staging 'LICENSE')
    Copy-Item (Join-Path $repoRoot 'ATTRIBUTION.md') (Join-Path $staging 'ATTRIBUTION.md')
    Copy-Item (Join-Path $repoRoot 'THIRD-PARTY-NOTICES.md') (Join-Path $staging 'THIRD-PARTY-NOTICES.md')
    Copy-Item (Join-Path $repoRoot 'LICENSE-INVENTORY.md') (Join-Path $staging 'LICENSE-INVENTORY.md')
    Copy-Item (Join-Path $repoRoot 'ThirdPartyLicenses') (Join-Path $staging 'ThirdPartyLicenses') -Recurse

    # ---- BUILDINFO (B2). Deliberately free of anything that varies between two clones of
    # the same commit on the pinned toolchain: no timestamps, no machine name, no repo
    # path (project.assets.json is NOT hashed for exactly that reason — it embeds absolute
    # paths; packages.lock.json is the path-free lock).
    $buildinfo = @(
        "product: $ProductName",
        "commit: $head",
        "commitClean: true (publish refuses a dirty tree)",
        "sdkVersion: $sdkVersion",
        "sdkCommit: $sdkCommit",
        "msbuildVersion: $msbuildVersion",
        "msbuildSha256: $msbuildSha",
        "roslynCscSha256: $roslynSha",
        "runtimeFrameworkVersion: $($lock.runtimeFrameworkVersion)",
        "windowsAppSdkVersion: $($lock.windowsAppSdkVersion)",
        "runtimeIdentifier: $($lock.targetRuntimeIdentifier)",
        "selfContained: true; singleFile: true; trimmed: false; readyToRun: false",
        "toolchainLockSha256: $((Get-FileHash $lockPath -Algorithm SHA256).Hash)",
        "publishScriptSha256: $((Get-FileHash $PSCommandPath -Algorithm SHA256).Hash)",
        "licenseInventorySha256: $((Get-FileHash (Join-Path $repoRoot 'LICENSE-INVENTORY.md') -Algorithm SHA256).Hash)",
        "dependencyGraph: recorded in LICENSE-INVENTORY.md (hashed above); no packages.lock.json by design (see UI.csproj)",
        "signed: $(if ($SignCommand) { 'yes' } else { 'no' })"
    )
    Set-Content (Join-Path $staging 'BUILDINFO.txt') ($buildinfo -join "`n") -Encoding ascii

    # Signing happens HERE — on the staged exe, before any hash or zip exists, so the
    # manifest always describes the shipped bytes.
    if ($SimulateFailure -eq 'sign') { throw 'SIMULATED FAILURE: signing stage' }
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

    # Integrity manifest over every packaged file (the Lexicon layers override embedded
    # data at runtime, so a tampered layer changes what users see).
    $manifest = Join-Path $staging 'SHA256SUMS.txt'
    Get-ChildItem $staging -Recurse -File | Sort-Object { $_.FullName.Substring($staging.Length + 1) } | ForEach-Object {
        $rel = $_.FullName.Substring($staging.Length + 1).Replace('\', '/')
        "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)  $rel"
    } | Set-Content $manifest -Encoding ascii

    # DETERMINISTIC release ZIP: entries sorted by path, every entry timestamp fixed to the
    # COMMIT time (not the build time). Compress-Archive stored per-file build timestamps,
    # so the same commit produced a different zip hash on every run. Built outside staging
    # so it cannot swallow itself; the zip's own hash goes into the loose manifest only (a
    # file cannot contain its own digest).
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

    # ---- Transactional swap (B3). Everything above is verified BEFORE the live path is
    # touched. The window between the two renames is real, so the failure path restores
    # the previous Distribution instead of leaving the release path missing.
    # Nothing may have touched the live release directory while we were building and
    # staging (F1). If something did, the package on disk is already inconsistent —
    # refuse rather than swap on top of it.
    $liveNow = Get-TreeSnapshot $distribution
    $drift = @()
    foreach ($k in $liveBefore.Keys) {
        if (-not $liveNow.ContainsKey($k)) { $drift += "removed: $k" }
        elseif ($liveNow[$k] -ne $liveBefore[$k]) { $drift += "modified: $k" }
    }
    foreach ($k in $liveNow.Keys) { if (-not $liveBefore.ContainsKey($k)) { $drift += "added: $k" } }
    if ($drift.Count -gt 0) {
        Write-Output 'LIVE DISTRIBUTION WAS MODIFIED DURING THE BUILD (something writes outside the transaction):'
        $drift | Select-Object -First 10 | ForEach-Object { "  $_" }
        throw 'Publish refused: the live release directory changed before the swap.'
    }

    if ($SimulateFailure -eq 'before-swap') { throw 'SIMULATED FAILURE: before swap' }
    if (Test-Path $distribution) {
        $old = "$distribution.old-" + [Guid]::NewGuid().ToString('N')
        Move-Item $distribution $old
    }
    if ($SimulateFailure -eq 'mid-swap') { throw 'SIMULATED FAILURE: between the two renames' }
    Move-Item $staging $distribution
    $staging = $null   # ownership transferred; finally must not delete it
    if ($old -and (Test-Path $old)) { Remove-Item $old -Recurse -Force; $old = $null }
}
catch {
    # Restore the previous Distribution if the swap was interrupted after it was moved
    # aside, then drop the half-built staging. The release path is never left missing.
    if ($old -and (Test-Path $old) -and -not (Test-Path $distribution)) {
        Move-Item $old $distribution
        Write-Output "ROLLBACK: previous Distribution restored from $(Split-Path $old -Leaf)"
        $old = $null
    }
    if ($old -and (Test-Path $old)) { Remove-Item $old -Recurse -Force }
    if ($staging -and (Test-Path $staging)) {
        Remove-Item $staging -Recurse -Force
        Write-Output 'ROLLBACK: staging removed'
    }
    throw
}

$size = [Math]::Round((Get-Item (Join-Path $distribution "$ProductName.exe")).Length / 1MB, 1)
$layerCount = (Get-ChildItem (Join-Path $distribution 'Lexicon') -Filter '*.yaml').Count
$licenseCount = (Get-ChildItem (Join-Path $distribution 'ThirdPartyLicenses') -Recurse -File).Count
$entryCount = (Get-Content (Join-Path $distribution 'SHA256SUMS.txt')).Count
$signState = if ($SignCommand) { 'signed' } else { 'UNSIGNED' }
Write-Output "Published ($signState): Distribution\$ProductName.exe (${size} MB) + Lexicon\ ($layerCount layers) + $licenseCount vendor license files + BUILDINFO + ZIP | SHA256SUMS.txt ($entryCount entries)"
