<#
.SYNOPSIS
    Leaves exactly one CapFrameX Vulkan overlay layer registered: the installed one.

.DESCRIPTION
    The Vulkan loader identifies an implicit layer by the NAME inside its manifest, not by path.
    Every CapFrameX manifest declares VK_LAYER_CAPFRAMEX_overlay, so a second registration - a
    development tree registered through the OSD repo's register_layer.cmd, or a leftover pointing
    at a previous install location - competes with the installed one, and which of them the loader
    picks is not determined by anything you control. The symptom is silent: a game loads a layer
    build you did not mean to test.

    This script keeps the registrations that point into the current CapFrameX installation and
    removes every other one, in both registry views. HKCU is always cleared: the loader ignores
    user-controlled layer sources for elevated targets, and HKCU is not split by bitness, so a
    single entry there disables the layer for every bitness.

    Only registry values are touched. No file on disk is added, changed or deleted.

.PARAMETER DryRun
    Report what would be removed and change nothing.

.PARAMETER InstallLocation
    The CapFrameX installation to treat as authoritative. Detected automatically when omitted.

.EXAMPLE
    .\Cleanup-VulkanLayerRegistrations.ps1 -DryRun
    .\Cleanup-VulkanLayerRegistrations.ps1
#>
[CmdletBinding()]
param(
    [switch]$DryRun,
    [string]$InstallLocation
)

$ErrorActionPreference = 'Stop'
$LAYER_KEY = 'SOFTWARE\Khronos\Vulkan\ImplicitLayers'
$MATCH     = '*cfx_osd_vklayer*'

# --- elevation ------------------------------------------------------------------------------
# Deleting under HKLM needs it. Relaunch through UAC rather than failing, so the script can be
# started from the same non-elevated shell everything else runs in.
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host 'Not elevated - relaunching through UAC...' -ForegroundColor Yellow
    $argv = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-NoExit', '-File', "`"$PSCommandPath`"")
    if ($DryRun) { $argv += '-DryRun' }
    if ($InstallLocation) { $argv += @('-InstallLocation', "`"$InstallLocation`"") }
    try { Start-Process -FilePath (Get-Process -Id $PID).Path -Verb RunAs -ArgumentList $argv }
    catch { Write-Error 'Elevation was declined.'; exit 1 }
    exit 0
}

# --- locate the installation ----------------------------------------------------------------
function Get-CapFrameXInstallLocation {
    # The uninstall entry is authoritative and survives a change of install folder, which is
    # exactly the case this script has to get right.
    $roots = @(
        'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall'
        'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'
    )
    foreach ($r in $roots) {
        if (-not (Test-Path $r)) { continue }
        foreach ($k in Get-ChildItem $r -ErrorAction SilentlyContinue) {
            $p = $k.GetValue('DisplayName')
            if ($p -notlike 'CapFrameX*') { continue }
            $loc = $k.GetValue('InstallLocation')
            if ($loc -and (Test-Path $loc)) { return $loc.TrimEnd('\') }
        }
    }
    # Fallback for a portable or hand-placed install.
    foreach ($c in @("$env:ProgramFiles\CapFrameX", "${env:ProgramFiles(x86)}\CapFrameX")) {
        if (Test-Path (Join-Path $c 'CapFrameX.exe')) { return $c.TrimEnd('\') }
    }
    return $null
}

if (-not $InstallLocation) { $InstallLocation = Get-CapFrameXInstallLocation }

if ($InstallLocation) {
    Write-Host "Installed CapFrameX : $InstallLocation"
} else {
    Write-Host 'Installed CapFrameX : not found' -ForegroundColor Yellow
    Write-Host '  Without an installation there is nothing to keep. Re-run with -InstallLocation'
    Write-Host '  to name one explicitly, or every registration below will be removed.'
}
Write-Host ''

# --- collect ---------------------------------------------------------------------------------
$views = @(
    @{ Label = 'HKLM 64-bit'; Hive = 'LocalMachine'; View = 'Registry64'; AlwaysRemove = $false }
    @{ Label = 'HKLM 32-bit'; Hive = 'LocalMachine'; View = 'Registry32'; AlwaysRemove = $false }
    # Never a valid location for this layer - see the header of the OSD repo's register_layer.cmd.
    @{ Label = 'HKCU 64-bit'; Hive = 'CurrentUser';  View = 'Registry64'; AlwaysRemove = $true }
    @{ Label = 'HKCU 32-bit'; Hive = 'CurrentUser';  View = 'Registry32'; AlwaysRemove = $true }
)

$found = @()
foreach ($v in $views) {
    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey($v.Hive, $v.View)
    try {
        $key = $base.OpenSubKey($LAYER_KEY, $false)
        if (-not $key) { continue }
        foreach ($name in $key.GetValueNames()) {
            if ($name -notlike $MATCH) { continue }
            $keep = -not $v.AlwaysRemove -and $InstallLocation -and
                    $name.StartsWith($InstallLocation, [StringComparison]::OrdinalIgnoreCase)
            $found += [pscustomobject]@{ View = $v; Label = $v.Label; Name = $name; Keep = $keep }
        }
        $key.Close()
    } finally { $base.Close() }
}

if (-not $found) { Write-Host 'No CapFrameX layer registrations found - nothing to do.'; exit 0 }

Write-Host 'Registered CapFrameX layers:'
foreach ($f in $found) {
    $tag = if ($f.Keep) { 'keep  ' } else { 'REMOVE' }
    $col = if ($f.Keep) { 'Green' } else { 'Yellow' }
    Write-Host ('  [{0}] {1,-12} {2}' -f $tag, $f.Label, $f.Name) -ForegroundColor $col
}
Write-Host ''

$toRemove = @($found | Where-Object { -not $_.Keep })
if (-not $toRemove) { Write-Host 'Only the installed layer is registered - nothing to do.' -ForegroundColor Green; exit 0 }

if ($DryRun) { Write-Host ("Dry run - {0} registration(s) would be removed." -f $toRemove.Count); exit 0 }

# --- remove ------------------------------------------------------------------------------------
$removed = 0
foreach ($f in $toRemove) {
    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey($f.View.Hive, $f.View.View)
    try {
        $key = $base.OpenSubKey($LAYER_KEY, $true)
        if (-not $key) { continue }
        $key.DeleteValue($f.Name, $false)
        $key.Close()
        Write-Host ('  removed  {0,-12} {1}' -f $f.Label, $f.Name)
        $removed++
    } finally { $base.Close() }
}

# --- verify --------------------------------------------------------------------------------------
Write-Host ''
Write-Host 'Registered CapFrameX layers now:'
$remaining = 0
foreach ($v in $views) {
    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey($v.Hive, $v.View)
    try {
        $key = $base.OpenSubKey($LAYER_KEY, $false)
        if (-not $key) { continue }
        foreach ($name in $key.GetValueNames() | Where-Object { $_ -like $MATCH }) {
            Write-Host ('  {0,-12} {1}' -f $v.Label, $name)
            $remaining++
        }
        $key.Close()
    } finally { $base.Close() }
}

if ($remaining -eq 0) {
    Write-Host ''
    Write-Host 'WARNING: no CapFrameX layer is registered any more - Vulkan titles get no overlay.' -ForegroundColor Red
    Write-Host 'Re-register the installed one with the OSD repo''s register_layer.cmd <installdir>\vulkan' -ForegroundColor Red
}

Write-Host ''
Write-Host ("Done - {0} registration(s) removed." -f $removed) -ForegroundColor Green
