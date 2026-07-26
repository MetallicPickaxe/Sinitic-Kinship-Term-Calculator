param(
    [ValidateSet("main", "mode-map", "multiple")]
    [string]$Source = "main"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$project = Join-Path $repoRoot 'Utility\ReferenceAccuracyExporter\ReferenceAccuracyExporter.csproj'

switch ($Source) {
    'main' { $prefix = 'MumuyMainAccuracyCompact' }
    'mode-map' { $prefix = 'MumuyModeMapAccuracyCompact' }
    'multiple' { $prefix = 'MumuyMultipleAccuracyCompact' }
    default { throw "Unsupported source: $Source" }
}

$compactTsv = Join-Path $repoRoot ("Resource\Data\Reference\{0}.tsv" -f $prefix)
$unsupportedTsv = Join-Path $repoRoot ("Resource\Data\Reference\{0}.Unsupported.tsv" -f $prefix)
$outputXlsx = Join-Path $repoRoot ("Resource\Data\Reference\{0}.xlsx" -f $prefix)
$packer = Join-Path $repoRoot 'Utility\Scripts\pack_tsv_pair_to_xlsx.py'

Write-Output ("EXPORT_SOURCE=" + $Source)
dotnet run --project $project -- --source $Source
python $packer --compact $compactTsv --unsupported $unsupportedTsv --output $outputXlsx
Remove-Item $compactTsv -Force -ErrorAction SilentlyContinue
Remove-Item $unsupportedTsv -Force -ErrorAction SilentlyContinue
Write-Output ("XLSX=" + $outputXlsx)
