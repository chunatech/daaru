#!/usr/bin/pwsh
param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("linux-x64","win-x64")]
    [string]$BuildTarget,
    [switch]$CleanBuild
)

$OUTDIR = Join-Path $PSScriptRoot "build/release/$BuildTarget/daaru"

Write-Output "Removing old build file at: $OUTDIR"

Remove-Item (Join-Path $OUTDIR 'cw*') -Force -ErrorAction SilentlyContinue
Remove-Item (Join-Path $OUTDIR 'fsi_standalone') -Recurse -Force -ErrorAction SilentlyContinue

if ($CleanBuild) {
    Remove-Item (Join-Path $PSScriptRoot 'src/*/bin') -Recurse -Force
    Remove-Item (Join-Path $PSScriptRoot 'src/*/obj') -Recurse -Force
}

try {
    & dotnet publish (Join-Path $PSScriptRoot 'src/daaru/daaru.fsproj') -c Release -r $BuildTarget -o $OUTDIR --self-contained
    & dotnet publish (Join-Path $PSScriptRoot 'src/fsi_standalone/fsi_standalone.fsproj') -c Release -r $BuildTarget -o (Join-Path $OUTDIR 'fsi_standalone') --self-contained
}
catch {
    Write-Error "Build failed."
    Write-Error $_.message
    return
}

[string[]]$folders1 = @('drivers', 'scripts', 'libs', 'config', 'templates')
[string[]]$folders2 = @('staging', 'screenshots', 'logs', 'results')

foreach ($folder in $folders1+$folders2) {
    New-Item -ItemType Directory -Path (Join-Path $OUTDIR $folder) -Force
}

foreach ($folder in $folders1) {
    $extResFolder = "external_resources/default_$($folder)"
    if ($folder -eq "drivers") { $extResFolder = Join-Path $extResFolder $BuildTarget }
    $children = @(Get-ChildItem (Join-Path $PSScriptRoot $extResFolder))
    $outPathBase = Join-Path $OUTDIR $folder

    foreach ($child in $children) {
        $outPath = Join-Path $outPathBase (Split-Path $child.Fullname -Leaf)

        if (!(Test-Path $outPath) -or $CleanBuild) {
            Copy-Item -Path $child.Fullname -Destination $outPathBase -Recurse -Force
        }
    }
}

Copy-Item -Recurse -Path (Join-Path $PSScriptRoot 'external_resources/default_results_processing/*') -Destination (Join-Path $OUTDIR 'results/') -Force