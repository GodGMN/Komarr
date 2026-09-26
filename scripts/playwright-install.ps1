<#
.SYNOPSIS
    One-shot bootstrap for the Readarr.Playwright.Test browser bundle.

.DESCRIPTION
    Restores NuGet packages so the bundled Playwright CLI is on disk,
    then invokes it to download Chromium. Idempotent — safe to re-run.
    Cached at %LOCALAPPDATA%\ms-playwright on Windows.
#>

$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project  = Join-Path $repoRoot 'src\NzbDrone.Playwright.Test\Readarr.Playwright.Test.csproj'

if (-not (Test-Path -LiteralPath $project)) {
    Write-Error "Playwright test project not found at $project"
    exit 1
}

# Two things this used to get wrong, both fixed on the bash side first:
#
# 1. It searched only the project's own bin\, which Directory.Build.props
#    redirects every test project away from — so on a clean checkout it
#    never found the CLI and always exited with "still not found".
# 2. Which driver runs `install` decides which Chromium revision lands in
#    the cache, and it must be the same driver the tests later launch
#    through. _tests\ is shared and never cleaned, so it accumulates
#    drivers keyed by target framework and RID; taking the first match
#    can pick up a years-old leftover, install its browser, and leave the
#    run failing with "Executable doesn't exist".
#
# So: search _tests\ (keeping bin\ as a fallback for anyone overriding
# those props) and select by version against the pin.
$pinned = (Select-String -LiteralPath (Join-Path $repoRoot 'src\Directory.Packages.props') `
                         -Pattern 'PackageVersion Include="Microsoft\.Playwright" Version="([^"]*)"' |
           Select-Object -First 1).Matches.Groups[1].Value

if (-not $pinned) {
    Write-Error 'Could not read the Microsoft.Playwright pin from src\Directory.Packages.props.'
    exit 1
}

# The driver carries a prerelease suffix (1.55.0-beta-...) against an
# assembly version of 1.55.0, so compare on major.minor — the same rule as
# the runtime check in _AssemblyGate.AssertDriverMatchesClient.
$pinnedSeries = ($pinned -split '\.')[0..1] -join '.'

function Get-DriverSeries($wrapperPath) {
    $manifest = Join-Path (Split-Path -Parent $wrapperPath) 'package.json'
    if (-not (Test-Path -LiteralPath $manifest)) { return $null }
    $version = (Get-Content -Raw -LiteralPath $manifest | ConvertFrom-Json).version
    if (-not $version) { return $null }
    return ($version -split '\.')[0..1] -join '.'
}

function Find-Wrappers {
    $roots = @(
        (Join-Path $repoRoot '_tests'),
        (Join-Path $repoRoot 'src\NzbDrone.Playwright.Test\bin')
    ) | Where-Object { Test-Path -LiteralPath $_ }

    if (-not $roots) { return @() }

    Get-ChildItem -LiteralPath $roots -Filter 'playwright.ps1' -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\.playwright' }
}

function Find-Cli {
    Find-Wrappers | Where-Object { (Get-DriverSeries $_.FullName) -eq $pinnedSeries } | Select-Object -First 1
}

Write-Host "Restoring Playwright test project so the CLI is on disk..."
dotnet restore $project

Write-Host "Locating the Playwright $pinned driver..."
$playwright = Find-Cli

if (-not $playwright) {
    # The wrapper isn't laid down until at least one build runs.
    Write-Host 'Triggering a build so Playwright drops its CLI wrapper...'
    dotnet build $project -nologo --verbosity quiet
    $playwright = Find-Cli
}

if (-not $playwright) {
    Write-Host "No .playwright driver matching the pinned $pinned found under _tests\ or bin\." -ForegroundColor Red
    Write-Host '       Drivers present:'
    foreach ($wrapper in Find-Wrappers) {
        Write-Host ("         {0}  {1}" -f (Get-DriverSeries $wrapper.FullName), $wrapper.FullName)
    }
    Write-Error 'If they are all stale, delete _tests\ and rebuild.'
    exit 1
}

Write-Host "Using $($playwright.FullName)"
Write-Host 'Installing Chromium...'
& $playwright.FullName install chromium

Write-Host ''
Write-Host 'Done. You can now run:'
Write-Host '  $env:READARR_RUN_PLAYWRIGHT = "1"; dotnet test src\NzbDrone.Playwright.Test\'
