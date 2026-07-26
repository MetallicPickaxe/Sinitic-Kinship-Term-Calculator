# Default path is derived from the script location (two levels above Utility\Scripts\ =
# repository root) rather than hard-coding anyone's local drive layout.
param(
    [string]$WorkbookPath = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'Resource\Data\Reference\MumuyReferenceWorkbook.xlsx'),
    [Parameter(Mandatory = $true)]
    [string]$SourceSheet,
    [Parameter(Mandatory = $true)]
    [string]$NewSheet,
    [Parameter(Mandatory = $true)]
    [string[]]$Columns
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $WorkbookPath)) {
    throw "Workbook not found: $WorkbookPath"
}

if ([string]::IsNullOrWhiteSpace($SourceSheet)) {
    throw "SourceSheet is required."
}

if ([string]::IsNullOrWhiteSpace($NewSheet)) {
    throw "NewSheet is required."
}

if ($NewSheet.Length -gt 31) {
    throw "Excel sheet name must be 31 characters or fewer: $NewSheet"
}

$excel = $null
$workbook = $null
$source = $null
$cloned = $null

try {
    $excel = New-Object -ComObject Excel.Application
    $excel.Visible = $false
    $excel.DisplayAlerts = $false

    $workbook = $excel.Workbooks.Open($WorkbookPath)

    try {
        $source = $workbook.Worksheets.Item($SourceSheet)
    } catch {
        throw "Source sheet not found: $SourceSheet"
    }

    foreach ($sheet in $workbook.Worksheets) {
        if ($sheet.Name -eq $NewSheet) {
            throw "Target sheet already exists: $NewSheet"
        }
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($sheet)
    }

    $source.Copy([System.Type]::Missing, $source)
    $cloned = $workbook.ActiveSheet
    $cloned.Name = $NewSheet

    $usedRange = $cloned.UsedRange
    $lastColumn = $usedRange.Column + $usedRange.Columns.Count - 1
    [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($usedRange)

    if ($lastColumn -lt 1) {
        $lastColumn = 1
    }

    $columnIndex = $lastColumn + 1
    foreach ($columnName in $Columns) {
        if ([string]::IsNullOrWhiteSpace($columnName)) {
            continue
        }

        $sourceColumn = $cloned.Columns.Item($lastColumn)
        $targetColumn = $cloned.Columns.Item($columnIndex)
        $sourceColumn.Copy() | Out-Null
        $targetColumn.PasteSpecial(-4122)
        $targetColumn.PasteSpecial(8)
        $excel.CutCopyMode = $false

        $cloned.Cells.Item(1, $columnIndex).Value2 = $columnName
        $rangeToClear = $cloned.Range($cloned.Cells.Item(2, $columnIndex), $cloned.Cells.Item(1048576, $columnIndex))
        $rangeToClear.ClearContents()

        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($rangeToClear)
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($targetColumn)
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($sourceColumn)

        $columnIndex += 1
    }

    $workbook.Save()
    Write-Output "Workbook=$WorkbookPath"
    Write-Output "SourceSheet=$SourceSheet"
    Write-Output "NewSheet=$NewSheet"
    Write-Output ("Columns=" + ($Columns -join ","))
}
finally {
    if ($cloned -ne $null) {
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($cloned)
    }
    if ($source -ne $null) {
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($source)
    }
    if ($workbook -ne $null) {
        $workbook.Close($true)
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($workbook)
    }
    if ($excel -ne $null) {
        $excel.Quit()
        [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($excel)
    }
    [gc]::Collect()
    [gc]::WaitForPendingFinalizers()
}
