# Structural gate for the lexicon layers (step 5 of the lexicon pipeline).
#
# Adding hundreds of regional variants is only an improvement if the additions stay
# coherent. Without a gate, "補得多" quietly becomes "補得爛": a word can end up meaning two
# different relations, or shadow another relation's standard form, and the reverse lookup
# that tags each UI chip with its layer silently starts lying.
#
# Enforced:
#   1. every layer file parses and declares id / name / layer
#   2. layer ids AND display names are unique — the name is the text printed on every candidate
#      chip (爸爸 · 通用口語), so two layers sharing one makes the chip ambiguous to the reader
#      while every id-based check stays green
#   3. no duplicate key inside one file
#   4. a variant is never equal to the standard form it hangs off
#   5. a variant attached to MORE THAN ONE standard form must be registered in
#      Resource\Data\Reference\KinshipAmbiguousVariants.tsv with a reason. Genuine dialect
#      ambiguity (阿婆 = 奶奶 or 外婆) is a fact of the language and is allowed once
#      documented; an UNdocumented one is a mistake and fails.
#   6. a variant must not equal some OTHER relation's standard form unless documented the
#      same way (it would shadow that relation in the reverse lookup)
#   7. every character used by a layer is either mapped by KinshipScriptConverter or listed in
#      Resource\Data\Reference\KinshipScriptNeutralChars.txt. A regional word carries characters
#      the engine never composes, and an unmapped simplified one survives untouched into the
#      Traditional rendering (老汉 shown to a zh-Hant reader).
# Exit code is nonzero on any violation so the validation loop can gate on it.
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$lexDir = Join-Path $repoRoot 'Resource\Data\Lexicon'

# Hans normal form, parsed from the engine's own converter table so the two cannot drift.
# Needed because a variant may repeat its key in the OTHER script (外孫子: [外孙子]), which is
# the same word and therefore not a variant at all.
$converter = Join-Path $repoRoot 'KinshipCalculator.Core\Services\Formatting\KinshipScriptConverter.cs'
$hantToHans = @{}
foreach ($line in (Get-Content $converter -Encoding utf8)) {
    if ($line -match "^\s*\('(.)',\s*'(.)'\)") { $hantToHans[$Matches[1]] = $Matches[2] }
}
if ($hantToHans.Count -lt 10) { throw "Could not parse the script converter table ($($hantToHans.Count) pairs)" }
function ConvertTo-Hans([string]$s) {
    $sb = New-Object System.Text.StringBuilder
    foreach ($ch in $s.ToCharArray()) {
        $k = [string]$ch
        [void]$sb.Append($(if ($hantToHans.ContainsKey($k)) { $hantToHans[$k] } else { $k }))
    }
    return $sb.ToString()
}

$neutralPath = Join-Path $repoRoot 'Resource\Data\Reference\KinshipScriptNeutralChars.txt'
$scriptKnown = @{}
foreach ($k in $hantToHans.Keys) { $scriptKnown[$k] = $true; $scriptKnown[$hantToHans[$k]] = $true }
if (Test-Path $neutralPath) {
    foreach ($line in (Get-Content $neutralPath -Encoding utf8)) {
        $t = $line.Trim()
        if ($t -and -not $t.StartsWith('#')) { $scriptKnown[$t] = $true }
    }
}

$problems = New-Object System.Collections.Generic.List[string]
$unmappedChars = New-Object System.Collections.Generic.SortedSet[string]
$ids = @{}
$names = @{}
$variantOwners = @{}   # variant -> set of standard forms
$standards = New-Object System.Collections.Generic.HashSet[string]
$entryCount = 0

foreach ($file in (Get-ChildItem $lexDir -Filter '*.yaml' | Sort-Object Name)) {
    $lines = Get-Content $file.FullName -Encoding utf8
    $meta = @{}
    $inMeta = $false
    $block = ''
    # Keys are unique WITHIN a block: 配偶 legitimately appears in variants_male and
    # variants_female, and those two are different audiences, not a duplicate.
    $keysHere = New-Object System.Collections.Generic.HashSet[string]
    foreach ($line in $lines) {
        if ($line -match '^meta:\s*$') { $inMeta = $true; continue }
        if ($line -match '^\S') { $inMeta = $false }
        if ($line -match '^(variants(_male|_female)?)\s*:\s*$') { $block = $Matches[1]; continue }
        if ($inMeta -and $line -match '^\s+(\w+):\s*(.+?)\s*$') { $meta[$Matches[1]] = $Matches[2] }
        if ($line -match '^\s{2}(\S+?):\s*\[(.*)\]\s*$') {
            $key = $Matches[1]
            if (-not $keysHere.Add("$block/$key")) { $problems.Add("$($file.Name): duplicate key '$key' in $block") }
            [void]$standards.Add($key)
            foreach ($ch in $key.ToCharArray()) {
                if (-not $scriptKnown.ContainsKey([string]$ch)) { [void]$unmappedChars.Add([string]$ch) }
            }
            foreach ($v in ($Matches[2] -split ',')) {
                $t = $v.Trim()
                if (-not $t) { continue }
                $entryCount++
                foreach ($ch in $t.ToCharArray()) {
                    if (-not $scriptKnown.ContainsKey([string]$ch)) { [void]$unmappedChars.Add([string]$ch) }
                }
                if ((ConvertTo-Hans $t) -eq (ConvertTo-Hans $key)) { $problems.Add("$($file.Name): '$key' lists itself as a variant ('$t')") }
                if (-not $variantOwners.ContainsKey($t)) { $variantOwners[$t] = New-Object System.Collections.Generic.HashSet[string] }
                [void]$variantOwners[$t].Add($key)
            }
        }
    }
    foreach ($required in 'id', 'name', 'layer') {
        if (-not $meta.ContainsKey($required)) { $problems.Add("$($file.Name): meta is missing '$required'") }
    }
    if ($meta.ContainsKey('id')) {
        if ($ids.ContainsKey($meta['id'])) { $problems.Add("duplicate layer id '$($meta['id'])' in $($file.Name) and $($ids[$meta['id']])") }
        else { $ids[$meta['id']] = $file.Name }
    }
    # `name` is not decoration: it is the string printed on every candidate chip (爸爸 · 通用口語).
    # Two layers sharing one name make the chip ambiguous for the reader while every id-based
    # check stays green, so the display name is required to be unique too.
    if ($meta.ContainsKey('name')) {
        if ($names.ContainsKey($meta['name'])) { $problems.Add("duplicate layer NAME '$($meta['name'])' in $($file.Name) and $($names[$meta['name']]) — this is the text on the UI chip") }
        else { $names[$meta['name']] = $file.Name }
    }
}

# documented ambiguity
$allowPath = Join-Path $repoRoot 'Resource\Data\Reference\KinshipAmbiguousVariants.tsv'
$allowed = @{}
if (Test-Path $allowPath) {
    foreach ($row in (Import-Csv $allowPath -Delimiter "`t")) {
        $allowed[$row.variant] = @($row.standards -split '\|' | ForEach-Object { $_.Trim() })
    }
}

foreach ($v in $variantOwners.Keys) {
    $owners = @($variantOwners[$v])
    if ($owners.Count -le 1) { continue }
    if (-not $allowed.ContainsKey($v)) {
        $problems.Add("undocumented ambiguity: '$v' is a variant of $($owners -join ' / ') — register it in KinshipAmbiguousVariants.tsv with a reason, or remove one")
        continue
    }
    $missing = @($owners | Where-Object { $allowed[$v] -notcontains $_ })
    if ($missing.Count -gt 0) { $problems.Add("ambiguity ledger for '$v' does not cover: $($missing -join ', ')") }
}

foreach ($v in $variantOwners.Keys) {
    if (-not $standards.Contains($v)) { continue }
    if (@($variantOwners[$v]) -contains $v) { continue }   # already reported as self-reference
    if (-not $allowed.ContainsKey($v)) {
        $problems.Add("shadowing: '$v' is a variant of $((@($variantOwners[$v])) -join '/') but is ALSO a standard form in its own right — document it or rename")
    }
}

if ($unmappedChars.Count -gt 0) {
    $problems.Add("characters neither mapped by KinshipScriptConverter nor listed as script-neutral: $($unmappedChars -join '') — add the Hant/Hans pair to the converter, or the character to KinshipScriptNeutralChars.txt")
}

if ($problems.Count -gt 0) {
    Write-Output "LEXICON INVARIANTS FAILED ($($problems.Count)):"
    $problems | Select-Object -First 20 | ForEach-Object { "  $_" }
    exit 1
}
Write-Output "Lexicon invariants OK: $($ids.Count) layers, $($standards.Count) standard forms, $entryCount variant entries, $($allowed.Count) documented ambiguities, $($scriptKnown.Count) script-classified characters"
# Explicit: falling off the end leaves $LASTEXITCODE at whatever the caller's previous native
# command set, so the loop's `if ($LASTEXITCODE -ne 0)` would fail a passing gate.
exit 0
