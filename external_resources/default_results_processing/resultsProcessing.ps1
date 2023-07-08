param(
    [string]$resultsPath
)

$lineCount = @(Get-Content $resultsPath).Count
Add-Content -Path (Join-Path $PSScriptRoot "../logs/countOf_$((SplitPath $resultsPath -Leaf).Replace('.csv','.txt'))") -Force