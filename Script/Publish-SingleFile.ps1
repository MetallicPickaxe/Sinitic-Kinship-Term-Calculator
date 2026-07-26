# Publishes the UI app as a self-contained single-file unpackaged exe into Distribution\.
# Recipe per the shared WinUI single-file playbook (OpenMIC/辭錄): MSBuild only, both
# runtime layers self-contained, no trimming, EnableMsixTooling on for resources.pri,
# product name fixed at build time via AssemblyName (never rename post-build).
param(
    [string]$ProductName = 'SiniticKinshipTermCalculator'
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
Write-Output "Toolchain: MSBuild $((Get-Item $msbuild).VersionInfo.ProductVersion) | HEAD $((git -C $repoRoot rev-parse HEAD).Trim())"

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
$head = (git -C $repoRoot rev-parse HEAD).Trim()
if ((Get-ExeRevision $exe) -ne $head) {
    & $msbuild $project -t:Rebuild @common
    if ($LASTEXITCODE -ne 0) { throw "Rebuild failed with $LASTEXITCODE" }
    & $msbuild $project -t:Publish @common
    if ($LASTEXITCODE -ne 0) { throw "Re-publish failed with $LASTEXITCODE" }
}
$rev = Get-ExeRevision $exe
if ($rev -ne $head) { throw "PROVENANCE FAILED: exe stamped '$rev', HEAD is '$head'" }
Write-Output "Artifact provenance-sealed to $head"

New-Item -ItemType Directory -Force -Path $distribution | Out-Null
Copy-Item $exe (Join-Path $distribution "$ProductName.exe") -Force

# Ship the lexicon layers as EDITABLE files next to the exe. The same four layers are also
# embedded in the assembly (single-file publish extracts to %TEMP%, so a loose-file lookup
# alone would be unreliable), but shipping them loose is what makes the layer system an
# actual extension point: a user can edit these, or drop additional *.yaml alongside them,
# and the engine stacks whatever it finds. Loose files never break the app if deleted.
$lexiconSource = Join-Path $repoRoot 'Resource\Data\Lexicon'
$lexiconTarget = Join-Path $distribution 'Lexicon'
New-Item -ItemType Directory -Force -Path $lexiconTarget | Out-Null
# The four *.yaml are the live layers; the *.yaml.txt sample ships as a rename-to-use template.
Copy-Item (Join-Path $lexiconSource '*.yaml') $lexiconTarget -Force
Copy-Item (Join-Path $lexiconSource '*.yaml.txt') $lexiconTarget -Force

# Integrity manifest: the exe AND every behavior-affecting loose file (the Lexicon layers
# override embedded data at runtime, so a tampered layer changes what users see).
$manifest = Join-Path $distribution 'SHA256SUMS.txt'
$entries = @(Get-Item (Join-Path $distribution "$ProductName.exe"))
$entries += Get-ChildItem $lexiconTarget -File | Sort-Object Name
$entries | ForEach-Object {
    $rel = $_.FullName.Substring($distribution.Length + 1).Replace('\', '/')
    "$((Get-FileHash $_.FullName -Algorithm SHA256).Hash)  $rel"
} | Set-Content $manifest -Encoding ascii

$size = [Math]::Round((Get-Item (Join-Path $distribution "$ProductName.exe")).Length / 1MB, 1)
$layerCount = (Get-ChildItem $lexiconTarget -Filter '*.yaml').Count
Write-Output "Published: Distribution\$ProductName.exe (${size} MB) + Lexicon\ ($layerCount layers) + SHA256SUMS.txt ($($entries.Count) entries)"
