param(
    [switch]$InternalReadWorker,
    [switch]$InternalWriteWorker,
    [string]$WorkbookPath,
    [string]$WorksheetName = 'MainCompact',
    [string]$InputJsonPath,
    [string]$OutputJsonPath,
    [string]$MainDataPath,
    [string]$WriteSavedMarkerPath
)

$ErrorActionPreference = 'Stop'

function Release-ComObject {
    param($ComObject)

    if ($null -ne $ComObject -and [System.Runtime.InteropServices.Marshal]::IsComObject($ComObject)) {
        try {
            [void][System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($ComObject)
        }
        catch {
        }
    }
}

function Invoke-ComCleanup {
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

function Get-CellText {
    param(
        $Worksheet,
        [int]$Row,
        [int]$Column
    )

    $cell = $null
    try {
        $cell = $Worksheet.Cells.Item($Row, $Column)
        return [string]$cell.Text
    }
    finally {
        Release-ComObject $cell
    }
}

function Set-CellValue {
    param(
        $Worksheet,
        [int]$Row,
        [int]$Column,
        $Value
    )

    $cell = $null
    try {
        $cell = $Worksheet.Cells.Item($Row, $Column)
        $cell.Value2 = $Value
    }
    finally {
        Release-ComObject $cell
    }
}

function Get-HeaderMap {
    param($Worksheet, [int]$ColumnCount)

    $map = @{}
    for ($column = 1; $column -le $ColumnCount; $column++) {
        $header = Get-CellText -Worksheet $Worksheet -Row 1 -Column $column
        if (-not [string]::IsNullOrWhiteSpace($header)) {
            $map[$header.Trim()] = $column
        }
    }

    return $map
}

function Resolve-Column {
    param(
        $Worksheet,
        [hashtable]$HeaderMap,
        [string[]]$Aliases,
        [string]$PreferredHeader,
        [int]$CurrentColumnCount,
        [bool]$CreateIfMissing = $false
    )

    foreach ($alias in $Aliases) {
        if ($HeaderMap.ContainsKey($alias)) {
            $column = [int]$HeaderMap[$alias]
            if (-not [string]::Equals($alias, $PreferredHeader, [System.StringComparison]::Ordinal)) {
                Set-CellValue -Worksheet $Worksheet -Row 1 -Column $column -Value $PreferredHeader
                [void]$HeaderMap.Remove($alias)
                $HeaderMap[$PreferredHeader] = $column
            }

            return [pscustomobject]@{
                Column = $column
                ColumnCount = [Math]::Max($CurrentColumnCount, $column)
            }
        }
    }

    if (-not $CreateIfMissing) {
        throw "Missing required workbook column. Preferred='$PreferredHeader'; aliases='$($Aliases -join ', ')'"
    }

    $column = $CurrentColumnCount + 1
    Set-CellValue -Worksheet $Worksheet -Row 1 -Column $column -Value $PreferredHeader
    $HeaderMap[$PreferredHeader] = $column
    return [pscustomobject]@{
        Column = $column
        ColumnCount = $column
    }
}

function Split-RawKeySet {
    param([string]$RawKeySet)

    if ([string]::IsNullOrWhiteSpace($RawKeySet)) {
        return @()
    }

    $parts = [System.Text.RegularExpressions.Regex]::Split($RawKeySet.Trim(), '\s+\|\s+')
    return $parts |
        ForEach-Object { $_.Trim() } |
        Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
}

function Get-PrimaryTermsForRawKeySet {
    param(
        [string]$RawKeySet,
        [hashtable]$MainDictionary
    )

    $primaryTerms = New-Object 'System.Collections.Generic.List[string]'
    foreach ($rawKey in Split-RawKeySet -RawKeySet $RawKeySet) {
        if (-not $MainDictionary.ContainsKey($rawKey)) {
            continue
        }

        foreach ($term in $MainDictionary[$rawKey]) {
            if (-not [string]::IsNullOrWhiteSpace([string]$term)) {
                $primaryTerm = [string]$term
                if (-not $primaryTerms.Contains($primaryTerm)) {
                    [void]$primaryTerms.Add($primaryTerm)
                }
                break
            }
        }
    }

    return ($primaryTerms -join ' | ')
}

function Read-MainDictionary {
    param([string]$Path)

    $dictionary = @{}
    $json = Get-Content -Raw $Path
    $jsonDocumentType = [type]::GetType('System.Text.Json.JsonDocument, System.Text.Json', $false)

    if ($null -ne $jsonDocumentType) {
        $document = [System.Text.Json.JsonDocument]::Parse($json)
        try {
            foreach ($property in $document.RootElement.EnumerateObject()) {
                $terms = New-Object 'System.Collections.Generic.List[string]'
                if ($property.Value.ValueKind -eq [System.Text.Json.JsonValueKind]::Array) {
                    foreach ($element in $property.Value.EnumerateArray()) {
                        if ($element.ValueKind -eq [System.Text.Json.JsonValueKind]::String) {
                            $text = $element.GetString()
                            if (-not [string]::IsNullOrWhiteSpace($text)) {
                                [void]$terms.Add($text)
                            }
                        }
                    }
                }
                elseif ($property.Value.ValueKind -eq [System.Text.Json.JsonValueKind]::String) {
                    $text = $property.Value.GetString()
                    if (-not [string]::IsNullOrWhiteSpace($text)) {
                        [void]$terms.Add($text)
                    }
                }

                $dictionary[$property.Name] = $terms
            }
        }
        finally {
            if ($null -ne $document) {
                $document.Dispose()
            }
        }

        return $dictionary
    }

    Add-Type -AssemblyName System.Web.Extensions
    $serializer = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $serializer.MaxJsonLength = 67108864
    $object = $serializer.DeserializeObject($json)
    foreach ($entry in $object.GetEnumerator()) {
        $terms = New-Object 'System.Collections.Generic.List[string]'
        if ($entry.Value -is [System.Collections.IEnumerable] -and $entry.Value -isnot [string]) {
            foreach ($element in $entry.Value) {
                if (-not [string]::IsNullOrWhiteSpace([string]$element)) {
                    [void]$terms.Add([string]$element)
                }
            }
        }
        elseif (-not [string]::IsNullOrWhiteSpace([string]$entry.Value)) {
            [void]$terms.Add([string]$entry.Value)
        }

        $dictionary[[string]$entry.Key] = $terms
    }

    return $dictionary
}

function Get-CurrentRoundId {
    param([string]$ManifestPath)

    if (-not (Test-Path $ManifestPath)) {
        return 'ADHOC'
    }

    $line = Get-Content $ManifestPath | Where-Object { $_ -match '^\s*- `CURRENT_ROUND`: ' } | Select-Object -First 1
    if ($null -eq $line) {
        return 'ADHOC'
    }

    if ($line -match '`CURRENT_ROUND`: `([^`]+)`') {
        return $Matches[1]
    }

    return 'ADHOC'
}

function Get-ExcelProcessIds {
    return @(Get-Process Excel -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id)
}

function Stop-NewExcelProcesses {
    param([int[]]$BeforeIds)

    $afterIds = Get-ExcelProcessIds
    $newIds = @($afterIds | Where-Object { $_ -notin $BeforeIds })
    foreach ($id in $newIds) {
        try {
            Stop-Process -Id $id -Force -ErrorAction Stop
        }
        catch {
        }
    }

    return $newIds
}

function Test-WorkbookExclusiveAccess {
    param([string]$Path)

    try {
        $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
        $stream.Close()
        return $true
    }
    catch {
        return $false
    }
}

function Invoke-ExcelReadCore {
    param(
        [string]$WorkbookPath,
        [string]$WorksheetName,
        [string]$OutputJsonPath
    )

    $excel = $null
    $workbook = $null
    $sheet = $null
    $usedRange = $null

    try {
        $excel = New-Object -ComObject Excel.Application
        $excel.Visible = $false
        $excel.DisplayAlerts = $false
        $excel.ScreenUpdating = $false

        $workbook = $excel.Workbooks.Open($WorkbookPath, $null, $true)
        $sheet = $workbook.Worksheets.Item($WorksheetName)
        $usedRange = $sheet.UsedRange
        $rowCount = [int]$usedRange.Rows.Count
        $columnCount = [int]$usedRange.Columns.Count
        $headerMap = Get-HeaderMap -Worksheet $sheet -ColumnCount $columnCount

        $tableRowColumn = [int](Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('表内行号（索引，不更新）', 'table_row_number') -PreferredHeader '表内行号（索引，不更新）' -CurrentColumnCount $columnCount).Column
        $chainColumn = [int](Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('标准链路径（参考，不更新）', 'chain_symbol_path') -PreferredHeader '标准链路径（参考，不更新）' -CurrentColumnCount $columnCount).Column
        $termSetColumn = [int](Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('Mumuy称谓集合（参考，不更新）', 'mumuy_term_set') -PreferredHeader 'Mumuy称谓集合（参考，不更新）' -CurrentColumnCount $columnCount).Column

        $rows = New-Object 'System.Collections.Generic.List[object]'
        for ($row = 2; $row -le $rowCount; $row++) {
            $tableRowText = Get-CellText -Worksheet $sheet -Row $row -Column $tableRowColumn
            if ([string]::IsNullOrWhiteSpace($tableRowText)) {
                continue
            }

            $rows.Add([pscustomobject]@{
                TableRowNumber = [int]$tableRowText
                ChainSymbolPath = Get-CellText -Worksheet $sheet -Row $row -Column $chainColumn
                MumuyTermSet    = Get-CellText -Worksheet $sheet -Row $row -Column $termSetColumn
            })
        }

        $rows | ConvertTo-Json -Depth 4 | Set-Content -Path $OutputJsonPath -Encoding UTF8
    }
    finally {
        Release-ComObject $usedRange
        Release-ComObject $sheet
        if ($null -ne $workbook) {
            try { $workbook.Close($false) } catch {}
        }
        Release-ComObject $workbook
        if ($null -ne $excel) {
            try { $excel.Quit() } catch {}
        }
        Release-ComObject $excel
        Invoke-ComCleanup
    }
}

function Invoke-ExcelWriteCore {
    param(
        [string]$WorkbookPath,
        [string]$WorksheetName,
        [string]$UpdatesJsonPath,
        [hashtable]$MainDictionary,
        [string]$WriteSavedMarkerPath
    )

    $updates = Get-Content -Raw $UpdatesJsonPath | ConvertFrom-Json
    $updateMap = @{}
    foreach ($update in $updates) {
        $updateMap[[int]$update.TableRowNumber] = $update
    }

    $excel = $null
    $workbook = $null
    $sheet = $null
    $usedRange = $null

    try {
        $excel = New-Object -ComObject Excel.Application
        $excel.Visible = $false
        $excel.DisplayAlerts = $false
        $excel.ScreenUpdating = $false

        $workbook = $excel.Workbooks.Open($WorkbookPath)
        $sheet = $workbook.Worksheets.Item($WorksheetName)
        $usedRange = $sheet.UsedRange
        $rowCount = [int]$usedRange.Rows.Count
        $columnCount = [int]$usedRange.Columns.Count
        $headerMap = Get-HeaderMap -Worksheet $sheet -ColumnCount $columnCount

        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('表内行号（索引，不更新）', 'table_row_number') -PreferredHeader '表内行号（索引，不更新）' -CurrentColumnCount $columnCount
        $tableRowColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('Mumuy原始键集（参考，不更新）', 'raw_key_set') -PreferredHeader 'Mumuy原始键集（参考，不更新）' -CurrentColumnCount $columnCount
        $rawKeyColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('Mumuy主称谓候选（参考，不更新）', 'mumuy_primary_term_candidates') -PreferredHeader 'Mumuy主称谓候选（参考，不更新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $primaryReferenceColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('程序输出_男_正式或回退（程序刷新）', 'our_official_or_fallback_male') -PreferredHeader '程序输出_男_正式或回退（程序刷新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $maleOfficialColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('程序输出_男_日常首选（程序刷新）', 'our_daily_folk_1st_candidate_male') -PreferredHeader '程序输出_男_日常首选（程序刷新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $maleFirstColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('程序输出_男_日常其他候选（程序刷新）', 'our_daily_folk_others_male') -PreferredHeader '程序输出_男_日常其他候选（程序刷新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $maleOthersColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('程序输出_男_exact_match（程序刷新）', 'our_is_exact_match_male') -PreferredHeader '程序输出_男_exact_match（程序刷新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $maleExactColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('程序输出_女_正式或回退（程序刷新）', 'our_official_or_fallback_female') -PreferredHeader '程序输出_女_正式或回退（程序刷新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $femaleOfficialColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('程序输出_女_日常首选（程序刷新）', 'our_daily_folk_1st_candidate_female') -PreferredHeader '程序输出_女_日常首选（程序刷新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $femaleFirstColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('程序输出_女_日常其他候选（程序刷新）', 'our_daily_folk_others_female') -PreferredHeader '程序输出_女_日常其他候选（程序刷新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $femaleOthersColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('程序输出_女_exact_match（程序刷新）', 'our_is_exact_match_female') -PreferredHeader '程序输出_女_exact_match（程序刷新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $femaleExactColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('我们的推断称谓（程序刷新）', '我們的推斷稱謂') -PreferredHeader '我们的推断称谓（程序刷新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $candidateColumn = [int]$resolved.Column; $columnCount = [int]$resolved.ColumnCount
        $resolved = Resolve-Column -Worksheet $sheet -HeaderMap $headerMap -Aliases @('对照判定（程序刷新）', '判斷') -PreferredHeader '对照判定（程序刷新）' -CurrentColumnCount $columnCount -CreateIfMissing $true
        $judgmentColumn = [int]$resolved.Column

        for ($row = 2; $row -le $rowCount; $row++) {
            $tableRowText = Get-CellText -Worksheet $sheet -Row $row -Column $tableRowColumn
            if ([string]::IsNullOrWhiteSpace($tableRowText)) {
                continue
            }

            $key = [int]$tableRowText
            if (-not $updateMap.ContainsKey($key)) {
                continue
            }

            $update = $updateMap[$key]
            $rawKeySet = Get-CellText -Worksheet $sheet -Row $row -Column $rawKeyColumn
            Set-CellValue -Worksheet $sheet -Row $row -Column $primaryReferenceColumn -Value (Get-PrimaryTermsForRawKeySet -RawKeySet $rawKeySet -MainDictionary $MainDictionary)
            Set-CellValue -Worksheet $sheet -Row $row -Column $maleOfficialColumn -Value ([string]$update.OurOfficialOrFallbackMale)
            Set-CellValue -Worksheet $sheet -Row $row -Column $maleFirstColumn -Value ([string]$update.OurDailyFolk1stCandidateMale)
            Set-CellValue -Worksheet $sheet -Row $row -Column $maleOthersColumn -Value ([string]$update.OurDailyFolkOthersMale)
            Set-CellValue -Worksheet $sheet -Row $row -Column $maleExactColumn -Value ($(if ($update.OurIsExactMatchMale) { 'TRUE' } else { 'FALSE' }))
            Set-CellValue -Worksheet $sheet -Row $row -Column $femaleOfficialColumn -Value ([string]$update.OurOfficialOrFallbackFemale)
            Set-CellValue -Worksheet $sheet -Row $row -Column $femaleFirstColumn -Value ([string]$update.OurDailyFolk1stCandidateFemale)
            Set-CellValue -Worksheet $sheet -Row $row -Column $femaleOthersColumn -Value ([string]$update.OurDailyFolkOthersFemale)
            Set-CellValue -Worksheet $sheet -Row $row -Column $femaleExactColumn -Value ($(if ($update.OurIsExactMatchFemale) { 'TRUE' } else { 'FALSE' }))
            Set-CellValue -Worksheet $sheet -Row $row -Column $candidateColumn -Value ([string]$update.CandidateDisplay)
            Set-CellValue -Worksheet $sheet -Row $row -Column $judgmentColumn -Value ([string]$update.JudgmentDisplay)
        }

        $workbook.Save()
        Write-Output 'WRITE_SAVED'
    }
    finally {
        Release-ComObject $usedRange
        Release-ComObject $sheet
        if ($null -ne $workbook) {
            try { $workbook.Close($false) } catch {}
        }
        Release-ComObject $workbook
        if ($null -ne $excel) {
            try { $excel.Quit() } catch {}
        }
        Release-ComObject $excel
        Invoke-ComCleanup
    }
}

function Quote-PowerShellArgument {
    param([string]$Value)

    return "'" + $Value.Replace("'", "''") + "'"
}

function Invoke-WorkerPhase {
    param(
        [string]$ModeSwitch,
        [string[]]$AdditionalArguments,
        [int]$TimeoutSeconds,
        [string]$WorkbookPath,
        [string]$SuccessToken,
        [string]$StdOutPath,
        [string]$StdErrPath
    )

    $beforeExcel = Get-ExcelProcessIds
    if (Test-Path $StdOutPath) { Remove-Item $StdOutPath -Force -ErrorAction SilentlyContinue }
    if (Test-Path $StdErrPath) { Remove-Item $StdErrPath -Force -ErrorAction SilentlyContinue }

    $commandParts = New-Object 'System.Collections.Generic.List[string]'
    [void]$commandParts.Add('&')
    [void]$commandParts.Add((Quote-PowerShellArgument -Value $PSCommandPath))
    [void]$commandParts.Add($ModeSwitch)
    foreach ($argument in $AdditionalArguments) {
        if ($argument -match '^-') {
            [void]$commandParts.Add($argument)
        }
        else {
            [void]$commandParts.Add((Quote-PowerShellArgument -Value $argument))
        }
    }

    $commandLine = $commandParts -join ' '
    $workerShellPath = try { (Get-Process -Id $PID -ErrorAction Stop).Path } catch { $null }
    if ([string]::IsNullOrWhiteSpace($workerShellPath)) {
        $workerShellPath = 'powershell'
    }
    $proc = Start-Process $workerShellPath -ArgumentList @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-Command', $commandLine) -PassThru -WindowStyle Hidden -RedirectStandardOutput $StdOutPath -RedirectStandardError $StdErrPath
    $finished = $proc.WaitForExit($TimeoutSeconds * 1000)
    $timedOut = -not $finished
    $exitCode = $null
    if ($finished) {
        try { $proc.Refresh() } catch {}
        try { $exitCode = $proc.ExitCode } catch { $exitCode = 0 }
        if ($null -eq $exitCode) { $exitCode = 0 }
    }

    if ($timedOut) {
        try { Stop-Process -Id $proc.Id -Force -ErrorAction Stop } catch {}
    }

    $newExcel = Stop-NewExcelProcesses -BeforeIds $beforeExcel
    Start-Sleep -Milliseconds 500
    $workbookUnlocked = Test-WorkbookExclusiveAccess -Path $WorkbookPath
    $stdOutText = if (Test-Path $StdOutPath) { Get-Content -Raw $StdOutPath } else { '' }
    $successExists = if ([string]::IsNullOrWhiteSpace($SuccessToken)) { $true } else { $stdOutText -match [regex]::Escape($SuccessToken) }

    if ($timedOut) {
        if (-not $successExists) {
            throw "Worker phase '$ModeSwitch' timed out before producing required success token '$SuccessToken'. stdout='$StdOutPath' stderr='$StdErrPath'"
        }
        if (-not $workbookUnlocked) {
            throw "Worker phase '$ModeSwitch' timed out and workbook still remained locked after cleanup. stdout='$StdOutPath' stderr='$StdErrPath'"
        }
        return
    }

    if ($exitCode -ne 0) {
        $stderr = if (Test-Path $StdErrPath) { Get-Content -Raw $StdErrPath } else { '' }
        throw "Worker phase '$ModeSwitch' failed with exit code $exitCode. stderr=$stderr"
    }

    if (-not $successExists) {
        throw "Worker phase '$ModeSwitch' exited without producing required success token '$SuccessToken'. stdout='$StdOutPath' stderr='$StdErrPath'"
    }

    if (-not $workbookUnlocked) {
        throw "Worker phase '$ModeSwitch' exited but workbook remained locked."
    }
}

if ($InternalReadWorker) {
    Invoke-ExcelReadCore -WorkbookPath $WorkbookPath -WorksheetName $WorksheetName -OutputJsonPath $InputJsonPath
    Write-Output 'READ_COMPLETE'
    exit 0
}

if ($InternalWriteWorker) {
    $mainDictionary = Read-MainDictionary -Path $MainDataPath
    Invoke-ExcelWriteCore -WorkbookPath $WorkbookPath -WorksheetName $WorksheetName -UpdatesJsonPath $OutputJsonPath -MainDictionary $mainDictionary
    exit 0
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$manifestPath = Join-Path $repoRoot 'Governance\GOVERNANCE_MANIFEST.md'
$currentRound = Get-CurrentRoundId -ManifestPath $manifestPath
$WorkbookPath = (Resolve-Path (Join-Path $repoRoot 'Resource\Data\Reference\MumuyMainAccuracyCompact.xlsx')).Path
$MainDataPath = Join-Path $repoRoot 'Utility\MumuyAlgorithm\Data\main.json'
$artifactsDir = Join-Path $repoRoot ("Governance\Artifacts\{0}" -f $currentRound)
$InputJsonPath = Join-Path $artifactsDir 'mumuy-main-review-input.json'
$OutputJsonPath = Join-Path $artifactsDir 'mumuy-main-review-output.json'
$readStdOutPath = Join-Path $artifactsDir 'read-worker.stdout.log'
$readStdErrPath = Join-Path $artifactsDir 'read-worker.stderr.log'
$writeStdOutPath = Join-Path $artifactsDir 'write-worker.stdout.log'
$writeStdErrPath = Join-Path $artifactsDir 'write-worker.stderr.log'
New-Item -ItemType Directory -Force -Path $artifactsDir | Out-Null

Invoke-WorkerPhase -ModeSwitch '-InternalReadWorker' -AdditionalArguments @('-WorkbookPath', $WorkbookPath, '-WorksheetName', $WorksheetName, '-InputJsonPath', $InputJsonPath) -TimeoutSeconds 60 -WorkbookPath $WorkbookPath -SuccessToken 'READ_COMPLETE' -StdOutPath $readStdOutPath -StdErrPath $readStdErrPath

dotnet run --project (Join-Path $repoRoot 'Utility\ReferenceAccuracyExporter\ReferenceAccuracyExporter.csproj') -- --workbook-review-input $InputJsonPath --workbook-review-output $OutputJsonPath
if ($LASTEXITCODE -ne 0) {
    throw "ReferenceAccuracyExporter failed with exit code $LASTEXITCODE"
}

Invoke-WorkerPhase -ModeSwitch '-InternalWriteWorker' -AdditionalArguments @('-WorkbookPath', $WorkbookPath, '-WorksheetName', $WorksheetName, '-OutputJsonPath', $OutputJsonPath, '-MainDataPath', $MainDataPath) -TimeoutSeconds 180 -WorkbookPath $WorkbookPath -SuccessToken 'WRITE_SAVED' -StdOutPath $writeStdOutPath -StdErrPath $writeStdErrPath

