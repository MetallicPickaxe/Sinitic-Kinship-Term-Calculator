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
    # release transaction. Each one prints "SIMULATED FAILURE: <point>" immediately before
    # failing, and the test asserts that token — otherwise a failure at an EARLIER stage
    # (a bad toolchain pin, say) would be scored as a successful injection, which the audit
    # demonstrated. 'live-writer' reproduces the audit's out-of-transaction writer: it
    # writes the release directory during the build and then fails.
    # Never set during a real release.
    [ValidateSet('', 'live-writer', 'sign', 'before-swap', 'mid-swap')]
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

$vsVersion = (& $vswhere -prerelease -latest -property catalog_productDisplayVersion | Select-Object -First 1)
if ($vsVersion) { $vsVersion = $vsVersion.Trim() }

# Every pinned field is REQUIRED. Guarding each check with "if the lock has the field"
# meant a lock missing sdkCommit / msbuildSha256 / roslynCscSha256 silently disabled that
# check — a pin that can be switched off by deleting a line is not a pin.
$required = 'sdkVersion', 'sdkCommit', 'msbuildVersion', 'msbuildSha256', 'roslynCscSha256',
            'visualStudioProductDisplayVersion', 'runtimeFrameworkVersion', 'windowsAppSdkVersion',
            'targetRuntimeIdentifier'
$missing = @($required | Where-Object { -not $lock.PSObject.Properties.Name.Contains($_) -or -not $lock.$_ })
if ($missing.Count -gt 0) {
    throw "Publish refused: Script\toolchain.lock.json is missing required pin(s): $($missing -join ', ')"
}

$toolchainErrors = @()
if ($sdkVersion -ne $lock.sdkVersion) { $toolchainErrors += "SDK version $sdkVersion != pinned $($lock.sdkVersion)" }
if ($sdkCommit -ne $lock.sdkCommit) { $toolchainErrors += "SDK commit $sdkCommit != pinned $($lock.sdkCommit)" }
if ($msbuildVersion -ne $lock.msbuildVersion) { $toolchainErrors += "MSBuild version $msbuildVersion != pinned $($lock.msbuildVersion)" }
if ($msbuildSha -ne $lock.msbuildSha256) { $toolchainErrors += "MSBuild.exe SHA-256 $msbuildSha != pinned $($lock.msbuildSha256)" }
if ($roslynSha -ne $lock.roslynCscSha256) { $toolchainErrors += "Roslyn csc.dll SHA-256 $roslynSha != pinned $($lock.roslynCscSha256)" }
if ($vsVersion -ne $lock.visualStudioProductDisplayVersion) { $toolchainErrors += "Visual Studio $vsVersion != pinned $($lock.visualStudioProductDisplayVersion)" }
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

# ---- SINGLE RECOVERABLE BOUNDARY (release audit R1). The previous version only rechecked
# the live directory inside the staging transaction, so a writer that touched
# Distribution\ during Restore/Publish/inventory — then failed — exited before any check
# and left the live package altered (the audit's probe target proved it: 49 -> 50 files).
# Now the live directory is PARKED before the build even starts: the release path does not
# exist while anything else runs, so an out-of-transaction writer can only create a NEW
# directory, which is detected; and every exit path restores the parked package.
$parked = $null
if (Test-Path $distribution) {
    $parked = "$distribution.parked-" + [Guid]::NewGuid().ToString('N')
    Move-Item $distribution $parked
    Write-Output "Live package parked for the duration of the build ($(Split-Path $parked -Leaf))"
}
$staging = $null
try {

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

# Stands in for a build-stage writer that touches the release directory and then fails —
# the exact shape the audit used to bypass the old in-transaction-only check.
if ($SimulateFailure -eq 'live-writer') {
    New-Item -ItemType Directory -Path $distribution -Force | Out-Null
    Set-Content (Join-Path $distribution 'AUDIT-LIVE-WRITER.txt') 'out-of-transaction write before publish failure' -Encoding ascii
    throw 'SIMULATED FAILURE: live-writer'
}

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

# ---- R4: the expanded package cache the compiler and bundler actually read must still
# match the signed .nupkg it came from. Recording restore metadata was not enough — the
# audit swapped a DLL in the cache and produced a fully published, correctly stamped EXE.
& (Join-Path $repoRoot 'Utility\Scripts\Test-PackageIntegrity.ps1') `
    -Configuration Release -Rid $lock.targetRuntimeIdentifier -ProductName $ProductName | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Publish refused: package integrity verification failed (see above).' }
$dirtyAfter = @(git -C $repoRoot status --porcelain)
if ($dirtyAfter.Count -gt 0) {
    Write-Output 'LICENSING INVENTORY STALE (regenerate, review and commit before publishing):'
    $dirtyAfter | Select-Object -First 10 | ForEach-Object { "  $_" }
    throw 'Publish refused: the committed licensing inventory does not match the resolved dependency graph.'
}

# ---- Assemble the distributable unit in FRESH whitelist staging on the SAME VOLUME as
# Distribution\. The live path is parked (see above), so nothing here touches it until the
# final rename.
$staging = Join-Path $repoRoot ("Distribution.staging-" + [Guid]::NewGuid().ToString('N'))
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
        "visualStudio: $vsVersion",
        "pathMap: repository root mapped to /_/ (Directory.Build.props) — build is location-independent",
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
    if ($SimulateFailure -eq 'sign') { throw 'SIMULATED FAILURE: sign' }
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

    # ---- Final swap. The live path was parked before the build, so it must NOT exist now:
    # if it does, something wrote the release directory outside this transaction (the
    # audit's probe target did exactly that) and the run is refused.
    if (Test-Path $distribution) {
        $intruders = @(Get-ChildItem $distribution -Recurse -File | ForEach-Object { $_.FullName.Substring($distribution.Length + 1) })
        Write-Output 'LIVE DISTRIBUTION WAS RECREATED DURING THE BUILD (something writes outside the transaction):'
        $intruders | Select-Object -First 10 | ForEach-Object { "  $_" }
        throw 'Publish refused: the live release directory was written outside the transaction.'
    }

    if ($SimulateFailure -eq 'before-swap') { throw 'SIMULATED FAILURE: before-swap' }
    if ($SimulateFailure -eq 'mid-swap') {
        # Simulate a crash in the middle of the swap: the staged package is renamed in and
        # then the run fails, so the recovery path must remove it and restore the parked one.
        Move-Item $staging $distribution
        $staging = $null
        throw 'SIMULATED FAILURE: mid-swap'
    }
    Move-Item $staging $distribution
    $staging = $null   # ownership transferred; the recovery path must not delete it
    if ($parked -and (Test-Path $parked)) { Remove-Item $parked -Recurse -Force; $parked = $null }
}
catch {
    # ---- Recovery for EVERY failure path, from before the restore through the swap.
    # A half-swapped or intruder-created live directory is discarded and the parked
    # package is renamed back, so the release path is never left missing, partial or
    # polluted.
    if ($parked -and (Test-Path $parked)) {
        if (Test-Path $distribution) {
            Remove-Item $distribution -Recurse -Force
            Write-Output 'ROLLBACK: discarded the live directory written during this run'
        }
        Move-Item $parked $distribution
        Write-Output "ROLLBACK: parked package restored to Distribution\"
        $parked = $null
    }
    if ($staging -and (Test-Path $staging)) {
        Remove-Item $staging -Recurse -Force
        Write-Output 'ROLLBACK: staging removed'
    }
    throw
}

$size = [Math]::Round((Get-Item (Join-Path $distribution "$ProductName.exe")).Length / 1MB, 1)
$layerCount = (Get-ChildItem (Join-Path $distribution 'Lexicon') -Filter '*.yaml').Count
# The generated .NET note is ours, not a vendor file — count them separately so the
# summary line stops overstating the vendor set (audit R5).
$licenseFiles = @(Get-ChildItem (Join-Path $distribution 'ThirdPartyLicenses') -Recurse -File)
$vendorCount = @($licenseFiles | Where-Object { $_.Name -ne 'DotNet-Windows-Licensing.md' }).Count
$entryCount = (Get-Content (Join-Path $distribution 'SHA256SUMS.txt')).Count
$signState = if ($SignCommand) { 'signed' } else { 'UNSIGNED' }
Write-Output "Published ($signState): Distribution\$ProductName.exe (${size} MB) + Lexicon\ ($layerCount layers) + $vendorCount vendor license files + 1 generated note + BUILDINFO + ZIP | SHA256SUMS.txt ($entryCount entries)"
