# Materialises the adjudicated candidates (Resource\Data\Reference\LEXICON_PILOT_DECISIONS.tsv) into the
# lexicon layer files. Adjudication is a human/reviewed step; this script only mechanises
# what the decisions already say, so the YAML can always be regenerated from the table.
#
# Rules encoded here, each one a decision recorded in the pilot:
#   - Only `high` and `med` confidence ship. `low` is held for the blind-agent protocol;
#     shipping a guess as a regional variant is exactly the kind of unverified claim this
#     project keeps getting burned by.
#   - `exclude` never ships (mumuy structure labels, rank templates, genealogy terms,
#     generic nouns, honorifics for someone else's child, suspected surname noise).
#   - A bucket may name several regions (yue-min): the term goes into EACH of those layers,
#     because it is genuinely used in each. The reverse lookup reports the first layer in
#     stack order, which is fine.
#   - A `-m` / `-f` bucket suffix is the EGO's gender, not the term's: 老婆 is `-m` because a
#     male ego says it. Those rows go into the layer's `variants_male:` / `variants_female:`
#     blocks, which the loader keeps separate so 配偶 can carry both 老公 and 老婆 without
#     offering either to the wrong person.
#   - The `standard-m` / `standard-f` bucket is NOT a layer at all: 丈夫 / 妻子 are standard
#     forms, and a standard form belongs in lexicon-standard.yaml's `entries:` (keyed by chain
#     with male/female columns), not in a variant layer. Those rows are reported and skipped.
param(
    [string]$Decisions = 'Resource\Data\Reference\LEXICON_PILOT_DECISIONS.tsv',
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$lexDir = Join-Path $repoRoot 'Resource\Data\Lexicon'

# bucket -> (file, layer id, display name, layer kind, provenance note)
$layerSpec = [ordered]@{
    colloquial = @{ File = 'register-colloquial.yaml'; Id = 'register-colloquial'; Name = '通用口語'; Layer = 'register'; Note = '全國通行的口語形' }
    literary   = @{ File = 'register-literary.yaml';   Id = 'register-literary';   Name = '書面·古語'; Layer = 'register'; Note = '書面語與古語稱謂(《爾雅》系、典故稱謂等)' }
    north      = @{ File = 'dialect-north.yaml';       Id = 'dialect-north';       Name = '北系';     Layer = 'dialect'; Note = '北方官話' }
    southwest  = @{ File = 'dialect-southwest.yaml';   Id = 'dialect-southwest';   Name = '西南官話'; Layer = 'dialect'; Note = '川渝雲貴' }
    northwest  = @{ File = 'dialect-northwest.yaml';   Id = 'dialect-northwest';   Name = '西北';     Layer = 'dialect'; Note = '陝甘寧青(「尕」系)' }
    xiang      = @{ File = 'dialect-xiang.yaml';       Id = 'dialect-xiang';       Name = '湘語';     Layer = 'dialect'; Note = '湖南(「毑」「娭毑」系)' }
    wu         = @{ File = 'dialect-wu.yaml';          Id = 'dialect-wu';          Name = '吳語';     Layer = 'dialect'; Note = '蘇滬浙(「囡」系)' }
    min        = @{ File = 'dialect-min.yaml';         Id = 'dialect-min';         Name = '閩語';     Layer = 'dialect'; Note = '閩台(「囝」「依」系)' }
    yue        = @{ File = 'dialect-yue.yaml';         Id = 'dialect-yue';         Name = '粵語';     Layer = 'dialect'; Note = '廣府/港澳' }
    hakka      = @{ File = 'dialect-hakka.yaml';       Id = 'dialect-hakka';       Name = '客家';     Layer = 'dialect'; Note = '客語' }
    south      = @{ File = 'dialect-south.yaml';       Id = 'dialect-south';       Name = '南系';     Layer = 'dialect'; Note = '南方(閩/粵/吳,未細分)' }
}

$rows = @(Import-Csv (Join-Path $repoRoot $Decisions) -Delimiter "`t")
$byLayer = @{}        # bucket -> block -> standard -> terms   (block: variants / variants_male / variants_female)
$skippedStandard = New-Object System.Collections.Generic.List[string]
$skippedLow = 0; $skippedExclude = 0; $unknownBucket = New-Object System.Collections.Generic.List[string]
$genderCount = 0

foreach ($r in $rows) {
    $bucket = $r.bucket.Trim()
    if ($bucket -eq 'exclude') { $skippedExclude++; continue }
    if ($r.confidence.Trim() -eq 'low') { $skippedLow++; continue }

    $block = 'variants'
    if ($bucket -match '^(.*)-m$') { $bucket = $Matches[1]; $block = 'variants_male'; $genderCount++ }
    elseif ($bucket -match '^(.*)-f$') { $bucket = $Matches[1]; $block = 'variants_female'; $genderCount++ }

    if ($bucket -eq 'standard') { $skippedStandard.Add("$($r.standard)/$($r.term)"); continue }
    if ($bucket -eq 'minority-dialect') { $unknownBucket.Add("$($r.term) [$bucket]"); continue }

    foreach ($part in ($bucket -split '-')) {
        if (-not $layerSpec.Contains($part)) { $unknownBucket.Add("$($r.term) [$part]"); continue }
        if (-not $byLayer.ContainsKey($part)) { $byLayer[$part] = @{} }
        if (-not $byLayer[$part].ContainsKey($block)) { $byLayer[$part][$block] = @{} }
        $std = $r.standard.Trim()
        if (-not $byLayer[$part][$block].ContainsKey($std)) { $byLayer[$part][$block][$std] = New-Object System.Collections.Generic.List[string] }
        if (-not $byLayer[$part][$block][$std].Contains($r.term.Trim())) { $byLayer[$part][$block][$std].Add($r.term.Trim()) }
    }
}

# An existing layer file carries curated content: hand-written provenance notes, the K14/K16
# attribution block, meta fields like default_enabled, and inline comments explaining
# individual entries. Regenerating such a file from the decision table DESTROYS all of that
# (the first version of this script did exactly that and had to be reverted). So existing
# files are merged SURGICALLY, line by line: known keys get their missing values appended
# inside the brackets, new keys are inserted at the end of the variants block under a
# machine-added marker, and every other line is passed through untouched.
function Merge-IntoExisting([System.Collections.Generic.List[string]]$lines, [string]$blockName, [hashtable]$additions) {
    # Locate the block this pass owns. Keys are only matched INSIDE it, so a key present in both
    # `variants:` and `variants_male:` cannot bleed across.
    $start = -1; $end = $lines.Count
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^$blockName\s*:\s*$") { $start = $i; continue }
        if ($start -ge 0 -and $lines[$i] -match '^\S') { $end = $i; break }
    }
    if ($start -lt 0) {
        # New block for this file: append it whole at the end.
        $doc = New-Object System.Collections.Generic.List[string]
        $doc.Add('')
        $doc.Add("${blockName}:")
        $added = 0
        foreach ($k in ($additions.Keys | Sort-Object)) {
            $doc.Add("  ${k}: [$(@($additions[$k]) -join ', ')]")
            $added += @($additions[$k]).Count
        }
        $lines.AddRange($doc)
        return @{ Lines = $lines; Added = $added }
    }

    $handled = New-Object System.Collections.Generic.HashSet[string]
    $added = 0
    for ($i = $start + 1; $i -lt $end; $i++) {
        if ($lines[$i] -notmatch '^(\s{2})(\S+?):\s*\[(.*)\]\s*$') { continue }
        $indent = $Matches[1]; $key = $Matches[2]; $body = $Matches[3]
        if (-not $additions.ContainsKey($key)) { continue }
        [void]$handled.Add($key)
        $have = @($body -split ',' | ForEach-Object { $_.Trim() } | Where-Object { $_ })
        $new = @($additions[$key] | Where-Object { $have -notcontains $_ })
        if ($new.Count -eq 0) { continue }
        $lines[$i] = "$indent${key}: [$((@($have) + $new) -join ', ')]"
        $added += $new.Count
    }

    $pending = @($additions.Keys | Where-Object { -not $handled.Contains($_) } | Sort-Object)
    if ($pending.Count -gt 0) {
        # Insert at the end of THIS block, keeping the file's own trailing structure.
        $lastEntry = $start
        for ($i = $start + 1; $i -lt $end; $i++) { if ($lines[$i] -match '^\s{2}\S+?:\s*\[') { $lastEntry = $i } }
        $block = New-Object System.Collections.Generic.List[string]
        $block.Add('')
        $block.Add('  # ↓ 由 Utility\Scripts\Build-LexiconLayers.ps1 自 LEXICON_PILOT_DECISIONS.tsv 生成')
        foreach ($k in $pending) {
            $block.Add("  ${k}: [$(@($additions[$k]) -join ', ')]")
            $added += @($additions[$k]).Count
        }
        $lines.InsertRange($lastEntry + 1, $block)
    }
    return @{ Lines = $lines; Added = $added }
}

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$blockOrder = @('variants', 'variants_male', 'variants_female')
$written = 0; $termCount = 0
foreach ($bucket in $layerSpec.Keys) {
    if (-not $byLayer.ContainsKey($bucket)) { continue }
    $spec = $layerSpec[$bucket]
    $path = Join-Path $lexDir $spec.File
    $additions = @{}
    foreach ($std in ($byLayer[$bucket]['variants'] ?? @{}).Keys) { $additions[$std] = @($byLayer[$bucket]['variants'][$std]) }

    if (Test-Path $path) {
        $lines = [System.Collections.Generic.List[string]]@(Get-Content $path -Encoding utf8)
        $addedHere = 0
        foreach ($blockName in $blockOrder) {
            if (-not $byLayer[$bucket].ContainsKey($blockName)) { continue }
            $blockAdditions = @{}
            foreach ($std in $byLayer[$bucket][$blockName].Keys) { $blockAdditions[$std] = @($byLayer[$bucket][$blockName][$std]) }
            $merged = Merge-IntoExisting $lines $blockName $blockAdditions
            $lines = $merged.Lines
            $addedHere += $merged.Added
        }
        $termCount += $addedHere
        if ($WhatIf) { Write-Output "WHATIF merge $($spec.File): +$addedHere terms into an existing curated file" }
        else { [IO.File]::WriteAllText($path, (($lines) -join "`n") + "`n", $utf8NoBom); $written++ }
    }
    else {
        $doc = New-Object System.Collections.Generic.List[string]
        $doc.Add("# $($spec.Name) · $($spec.Note)")
        $doc.Add('#')
        $doc.Add('# 鍵 = 標準形(引擎算出的主位詞);值 = 該層的地方/語域形。本層可停用、可替換;')
        $doc.Add('# 停用後主位仍為標準漢語,不影響引擎正確性。')
        $doc.Add('#')
        $doc.Add('# 生成自 Resource\Data\Reference\LEXICON_PILOT_DECISIONS.tsv(Utility\Scripts\Build-LexiconLayers.ps1)。')
        $doc.Add('# 詞源:mumuy/relationship(MIT)之稱謂集合;')
        $doc.Add('# 【分層歸屬為本專案裁決,mumuy 資料本身不帶任何地域/語域欄位】。')
        $doc.Add('meta:')
        $doc.Add("  id: $($spec.Id)")
        $doc.Add("  name: $($spec.Name)")
        $doc.Add("  layer: $($spec.Layer)")
        if ($spec.Layer -eq 'dialect') { $doc.Add("  region: $($spec.Note)") }
        $doc.Add('  provenance: 詞形採自 mumuy(MIT)稱謂集合;分層由本專案裁決(LEXICON_PILOT_DECISIONS.tsv)')
        $doc.Add('  default_enabled: true')
        foreach ($blockName in $blockOrder) {
            if (-not $byLayer[$bucket].ContainsKey($blockName)) { continue }
            $doc.Add("${blockName}:")
            foreach ($std in ($byLayer[$bucket][$blockName].Keys | Sort-Object)) {
                $doc.Add("  ${std}: [$(@($byLayer[$bucket][$blockName][$std]) -join ', ')]")
                $termCount += @($byLayer[$bucket][$blockName][$std]).Count
            }
        }
        $doc.Add('')
        if ($WhatIf) { Write-Output "WHATIF create $($spec.File): $($additions.Keys.Count) keys" }
        else { [IO.File]::WriteAllText($path, ($doc -join "`n"), $utf8NoBom); $written++ }
    }
}

Write-Output "Layers written: $written  (total variant entries across layers: $termCount)"
Write-Output "Held back: low-confidence $skippedLow · excluded $skippedExclude · standard-form rows $($skippedStandard.Count)"
Write-Output "Ego-scoped entries emitted (variants_male / variants_female): $genderCount"
if ($skippedStandard.Count -gt 0) {
    Write-Output "  standard forms — belong in lexicon-standard.yaml entries:, not a variant layer:"
    Write-Output "    $(($skippedStandard) -join ', ')"
}
if ($unknownBucket.Count -gt 0) {
    Write-Output "  unmapped buckets: $(($unknownBucket | Select-Object -Unique | Select-Object -First 8) -join ', ')"
}
