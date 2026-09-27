<#
.SYNOPSIS
    Automates Recipe A from docs/migrating-from-readarr.md on Windows:
    copies the existing Readarr AppData folder to the Librarr default
    location, leaving the Readarr folder untouched so the user can roll
    back by just starting the old binary.

.PARAMETER Src
    Source folder. Defaults to $env:ProgramData\Readarr.

.PARAMETER Dst
    Destination folder. Defaults to $env:ProgramData\Librarr.

.PARAMETER DryRun
    Show what would be copied without touching the filesystem.

.PARAMETER Force
    Skip the running-process check and the interactive confirmation.

.EXAMPLE
    .\scripts\migrate-readarr-folder.ps1

.EXAMPLE
    .\scripts\migrate-readarr-folder.ps1 -DryRun

.EXAMPLE
    .\scripts\migrate-readarr-folder.ps1 -Src 'D:\Apps\Readarr' -Dst 'D:\Apps\Librarr'

.NOTES
    Requires PowerShell 5.1+ (built into Windows 10/11). Run from an
    elevated prompt if your data folder lives under %ProgramData% — the
    Readarr installer's default — otherwise non-admin shells can't read
    or write there.
#>

[CmdletBinding()]
param(
    [string]$Src    = (Join-Path $env:ProgramData 'Readarr'),
    [string]$Dst    = (Join-Path $env:ProgramData 'Librarr'),
    [switch]$DryRun,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

Write-Host "Source:      $Src"
Write-Host "Destination: $Dst"
Write-Host

# ── Pre-flight ──────────────────────────────────────────────────────────
if (-not (Test-Path -LiteralPath $Src -PathType Container)) {
    Write-Error "Source folder '$Src' does not exist. Pass -Src if your data folder is elsewhere."
    exit 1
}

if (-not (Test-Path -LiteralPath (Join-Path $Src 'config.xml'))) {
    Write-Warning "'$Src\config.xml' not found. The source may not be a Readarr/Librarr data folder. Continuing because you pointed here."
}

if (Test-Path -LiteralPath $Dst) {
    Write-Error "Destination '$Dst' already exists. Refusing to merge into it. Move/rename it first, or pass -Dst to use a different target."
    exit 1
}

if (-not $Force) {
    # Both Readarr and Librarr binaries are named Readarr.exe / Readarr.Console.exe
    # (binary names intentionally kept at Phase 0), so probing Readarr* covers both.
    $running = @(Get-Process -Name 'Readarr','Readarr.Console' -ErrorAction SilentlyContinue)
    if ($running.Count -gt 0) {
        Write-Error "A Readarr/Librarr process is running (PIDs: $($running.Id -join ', ')). Stop it first, or rerun with -Force."
        exit 1
    }
}

# ── Confirmation ────────────────────────────────────────────────────────
if (-not $Force -and -not $DryRun) {
    Write-Host "About to copy '$Src' to '$Dst' (Readarr folder untouched)."
    $reply = Read-Host 'Continue? [y/N]'
    if ($reply -notmatch '^(y|Y|yes|YES)$') {
        Write-Host 'Aborted.'
        exit 0
    }
}

# ── Copy ────────────────────────────────────────────────────────────────
if ($DryRun) {
    Write-Host "[dry-run] would: Copy-Item -Recurse '$Src\*' -Destination '$Dst'"
    Write-Host '[dry-run] no files copied.'
    exit 0
}

New-Item -ItemType Directory -Path $Dst -Force | Out-Null
Copy-Item -LiteralPath $Src -Destination $Dst -Recurse -Force

# Copy-Item with -Recurse on a folder source creates $Dst\<basename>.
# We want the contents directly inside $Dst, so unwrap the inner folder.
$leaf = Split-Path -Leaf $Src
$inner = Join-Path $Dst $leaf
if (Test-Path -LiteralPath $inner -PathType Container) {
    Get-ChildItem -LiteralPath $inner -Force | Move-Item -Destination $Dst -Force
    Remove-Item -LiteralPath $inner -Force
}

# ── Sanity-check ────────────────────────────────────────────────────────
$srcSize = (Get-ChildItem -LiteralPath $Src -Recurse -Force -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum).Sum
$dstSize = (Get-ChildItem -LiteralPath $Dst -Recurse -Force -ErrorAction SilentlyContinue |
            Measure-Object -Property Length -Sum).Sum

if ($srcSize -and $dstSize) {
    $pct = [math]::Round(($dstSize / $srcSize) * 100, 0)
    Write-Host
    Write-Host "Copy complete. Source: $([math]::Round($srcSize/1KB)) KB, destination: $([math]::Round($dstSize/1KB)) KB (~$pct% match)."
    if ($pct -lt 95) {
        Write-Warning "Destination is noticeably smaller than source. Inspect '$Dst' manually."
    }
}

Write-Host @"

Done. Next steps:
  1. Start Librarr — it picks up the copy at '$Dst' on first launch.
  2. Verify the library, indexers, and download clients look right.
  3. Run Settings → Metadata → Switch Metadata Source.
  4. Once you're satisfied, you can delete '$Src' — but the Readarr
     folder is your rollback parachute, so consider keeping it for a
     few days.

See docs/migrating-from-readarr.md for the full migration guide.
"@
