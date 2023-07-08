param(
    [string]$resultsPath
)

if (Test-Path $resultsPath) {
    $lineCount = @(Get-Content $resultsPath).Count
    Add-Content -Path (Join-Path $PSScriptRoot "../logs/countOf_$((Split-Path $resultsPath -Leaf).Replace('.csv','.txt'))") -Value $lineCount -Force
    "current results count is $lineCount"
}