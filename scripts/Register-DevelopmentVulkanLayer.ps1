<#
.SYNOPSIS
    Register this checkout's matching x64/x86 CapFrameX Vulkan development layers.
.DESCRIPTION
    Requires an already elevated PowerShell for changes; never launches UAC. Validates both
    manifests and DLL architectures before touching the registry. Only CapFrameX implicit-layer
    values are changed: older HKLM registrations are disabled, development registrations are
    enabled in their own bitness views, and stale HKCU registrations are removed.
    A JSON backup is saved under this checkout's temp directory before any changes.
    Restore reverts only the recorded HKLM changes and never recreates unsafe HKCU registrations.
    Restart Vulkan games after changing registrations. No binaries are modified or installed.
.PARAMETER Configuration
    Application output configuration. Defaults to Debug.
.PARAMETER OutputPath
    Application output folder containing vulkan and vulkan\x86. Overrides Configuration.
.PARAMETER Restore
    JSON backup previously produced by this script. Restores its HKLM values only.
.PARAMETER ValidateOnly
    Validate payloads and display the scoped registry plan without writing files or registry.
.EXAMPLE
    .\scripts\Register-DevelopmentVulkanLayer.ps1 -ValidateOnly
.EXAMPLE
    .\scripts\Register-DevelopmentVulkanLayer.ps1
.EXAMPLE
    .\scripts\Register-DevelopmentVulkanLayer.ps1 -Restore .\temp\vulkan-layer-registration\backup-....json
#>
[CmdletBinding(SupportsShouldProcess, DefaultParameterSetName = 'Register')]
param(
    [Parameter(ParameterSetName = 'Register')]
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [Parameter(ParameterSetName = 'Register')][string]$OutputPath,
    [Parameter(Mandatory, ParameterSetName = 'Restore')][string]$Restore,
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$registryPath = 'SOFTWARE\Khronos\Vulkan\ImplicitLayers'
$layerName = 'VK_LAYER_CAPFRAMEX_overlay'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $principal = [Security.Principal.WindowsPrincipal]::new($identity)
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    finally { $identity.Dispose() }
}

function Get-LayerPayload([string]$Folder, [string]$View, [int]$ExpectedMachine) {
    $manifest = Join-Path $Folder 'cfx_osd_vklayer_v1.json'
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw "Manifest missing: $manifest" }
    $manifest = (Get-Item -LiteralPath $manifest).FullName
    $document = Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
    if ($document.layer.name -cne $layerName -or $document.layer.type -cne 'GLOBAL') { throw "Unexpected layer manifest: $manifest" }
    $library = [string]$document.layer.library_path
    if ([IO.Path]::IsPathRooted($library)) { throw "The layer DLL path must be relative: $manifest" }
    $dll = [IO.Path]::GetFullPath((Join-Path $Folder $library))
    $expectedDll = [IO.Path]::GetFullPath((Join-Path $Folder 'cfx_osd_vklayer.dll'))
    if (-not $dll.Equals($expectedDll, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $dll -PathType Leaf)) {
        throw "The manifest must reference its adjacent cfx_osd_vklayer.dll: $manifest"
    }
    $stream = [IO.File]::OpenRead($dll)
    $reader = [IO.BinaryReader]::new($stream)
    try {
        if ($stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5A4D) { throw "Invalid PE DLL: $dll" }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 64 -or $peOffset -gt $stream.Length - 6) { throw "Invalid PE header offset: $dll" }
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550 -or $reader.ReadUInt16() -ne $ExpectedMachine) { throw "Wrong DLL architecture for ${View}: $dll" }
    }
    finally { $reader.Dispose(); $stream.Dispose() }
    [pscustomobject]@{ View = $View; Manifest = $manifest; Dll = $dll; Sha256 = (Get-FileHash -LiteralPath $dll -Algorithm SHA256).Hash }
}

function Test-CapFrameXRegistration([string]$Name) {
    if (-not [IO.Path]::IsPathRooted($Name)) { return $false }
    # Exact dedicated manifest names also identify stale registrations whose files were removed.
    if ([IO.Path]::GetFileName($Name) -notmatch '^cfx_osd_vklayer(?:_v1)?\.json$') { return $false }
    if (Test-Path -LiteralPath $Name -PathType Leaf) {
        try { return (Get-Content -LiteralPath $Name -Raw | ConvertFrom-Json).layer.name -ceq $layerName }
        catch { return $true } # A damaged manifest with this exact CapFrameX filename is stale.
    }
    return $true
}

function Get-Registration([string]$Hive, [string]$View, [string]$Name) {
    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::$Hive, [Microsoft.Win32.RegistryView]::$View)
    try {
        $key = $base.OpenSubKey($registryPath, $false)
        try {
            $exists = $null -ne $key -and @($key.GetValueNames()) -contains $Name
            [pscustomobject]@{ Exists = $exists; Data = $(if ($exists) { $key.GetValue($Name) } else { $null });
                Kind = $(if ($exists) { $key.GetValueKind($Name).ToString() } else { $null }) }
        }
        finally { if ($null -ne $key) { $key.Dispose() } }
    }
    finally { $base.Dispose() }
}

function Get-CapFrameXRegistrations([string]$Hive, [string]$View) {
    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::$Hive, [Microsoft.Win32.RegistryView]::$View)
    try {
        $key = $base.OpenSubKey($registryPath, $false)
        try {
            if ($null -eq $key) { return }
            foreach ($name in $key.GetValueNames()) {
                if (Test-CapFrameXRegistration $name) {
                    if ($key.GetValueKind($name) -ne [Microsoft.Win32.RegistryValueKind]::DWord) { throw "Unexpected registry type for CapFrameX value: $Hive $View $name" }
                    [pscustomobject]@{ Hive = $Hive; View = $View; Name = $name; Data = [int]$key.GetValue($name) }
                }
            }
        }
        finally { if ($null -ne $key) { $key.Dispose() } }
    }
    finally { $base.Dispose() }
}

function Set-Registration([string]$Hive, [string]$View, [string]$Name, [bool]$Exists, $Data) {
    $base = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::$Hive, [Microsoft.Win32.RegistryView]::$View)
    try {
        $key = if ($Exists) { $base.CreateSubKey($registryPath) } else { $base.OpenSubKey($registryPath, $true) }
        try {
            if ($null -eq $key) { return }
            if ($Exists) { $key.SetValue($Name, [int]$Data, [Microsoft.Win32.RegistryValueKind]::DWord) }
            else { $key.DeleteValue($Name, $false) }
            $key.Flush()
        }
        finally { if ($null -ne $key) { $key.Dispose() } }
    }
    finally { $base.Dispose() }
}

$changes = [Collections.Generic.List[object]]::new()
$payloads = @()
if ($PSCmdlet.ParameterSetName -eq 'Restore') {
    $backup = Get-Content -LiteralPath $Restore -Raw | ConvertFrom-Json
    if ($backup.Schema -ne 1 -or $backup.RegistryPath -cne $registryPath -or $backup.LayerName -cne $layerName -or
        $backup.MachineName -ine [Environment]::MachineName) { throw 'This is not a matching CapFrameX Vulkan registration backup for this computer.' }
    foreach ($change in @($backup.Changes)) {
        if ($change.Hive -notin @('LocalMachine', 'CurrentUser') -or $change.View -notin @('Registry64', 'Registry32') -or
            -not (Test-CapFrameXRegistration $change.Name) -or $change.BeforeExists -isnot [bool] -or $change.AfterExists -isnot [bool]) {
            throw 'The backup contains an invalid or unrelated registration.'
        }
        if ($change.Hive -eq 'CurrentUser') { continue }
        if (($change.BeforeExists -and ($null -eq $change.BeforeData -or [long]$change.BeforeData -lt [int]::MinValue -or [long]$change.BeforeData -gt [int]::MaxValue)) -or
            ($change.AfterExists -and $change.AfterData -notin @(0, 1))) { throw 'The backup contains invalid DWORD data.' }
        $current = Get-Registration $change.Hive $change.View $change.Name
        $alreadyRestored = $current.Exists -eq $change.BeforeExists -and (-not $current.Exists -or ($current.Kind -eq 'DWord' -and $current.Data -eq $change.BeforeData))
        if ($alreadyRestored) { continue }
        if ($current.Exists -ne $change.AfterExists -or ($current.Exists -and ($current.Kind -ne 'DWord' -or $current.Data -ne $change.AfterData))) {
            throw "Registration changed after the backup; restore stopped before writing: $($change.Name)"
        }
        $changes.Add([pscustomobject]@{ Hive = $change.Hive; View = $change.View; Name = $change.Name;
            BeforeExists = $current.Exists; BeforeData = $current.Data; AfterExists = $change.BeforeExists; AfterData = $change.BeforeData })
    }
    Write-Host 'Restore affects recorded HKLM registrations only. Purged unsafe HKCU entries will not be recreated.'
}
else {
    if (-not $OutputPath) { $OutputPath = Join-Path $repositoryRoot "source\CapFrameX\bin\x64\$Configuration\net10.0-windows" }
    $OutputPath = [IO.Path]::GetFullPath($OutputPath)
    $payloads = @(
        Get-LayerPayload (Join-Path $OutputPath 'vulkan') 'Registry64' 0x8664
        Get-LayerPayload (Join-Path $OutputPath 'vulkan\x86') 'Registry32' 0x014C
    )
    foreach ($payload in $payloads) {
        Write-Host "$($payload.View): $($payload.Manifest)"
        Write-Host "  DLL SHA256: $($payload.Sha256)"
        foreach ($current in @(Get-CapFrameXRegistrations 'LocalMachine' $payload.View)) {
            if ($current.Name -ine $payload.Manifest -and $current.Data -ne 1) {
                $changes.Add([pscustomobject]@{ Hive = 'LocalMachine'; View = $payload.View; Name = $current.Name;
                    BeforeExists = $true; BeforeData = $current.Data; AfterExists = $true; AfterData = 1 })
            }
        }
        $existing = Get-Registration 'LocalMachine' $payload.View $payload.Manifest
        if ($existing.Exists -and $existing.Kind -ne 'DWord') { throw "Unexpected registry type for $($payload.Manifest)" }
        if (-not $existing.Exists -or $existing.Data -ne 0) {
            $changes.Add([pscustomobject]@{ Hive = 'LocalMachine'; View = $payload.View; Name = $payload.Manifest;
                BeforeExists = $existing.Exists; BeforeData = $existing.Data; AfterExists = $true; AfterData = 0 })
        }
    }
    foreach ($view in @('Registry64', 'Registry32')) {
        foreach ($current in @(Get-CapFrameXRegistrations 'CurrentUser' $view)) {
            $changes.Add([pscustomobject]@{ Hive = 'CurrentUser'; View = $view; Name = $current.Name;
                BeforeExists = $true; BeforeData = $current.Data; AfterExists = $false; AfterData = $null })
        }
    }
}

foreach ($change in $changes) {
    $action = if (-not $change.AfterExists) { 'remove' } elseif ($change.AfterData -eq 0) { 'enable' } else { 'disable / restore' }
    Write-Host "  $action $($change.Hive) $($change.View): $($change.Name)"
}
if ($ValidateOnly) { Write-Host 'Validation only: no registry or file changes.'; return }
if ($changes.Count -eq 0) { Write-Host 'The requested registrations are already in place; nothing changed.'; return }
if (-not $WhatIfPreference -and -not (Test-Administrator)) { throw 'Run this script from an already elevated PowerShell (Run as administrator). No changes were made; this script does not launch UAC.' }
if (-not $PSCmdlet.ShouldProcess('CapFrameX Vulkan implicit-layer registrations in the displayed registry views', 'Apply the scoped registration plan')) { return }

$registrySnapshot = @(
    foreach ($hive in @('LocalMachine', 'CurrentUser')) {
        foreach ($view in @('Registry64', 'Registry32')) { Get-CapFrameXRegistrations $hive $view }
    }
)
foreach ($change in $changes) {
    $current = Get-Registration $change.Hive $change.View $change.Name
    if ($current.Exists -ne $change.BeforeExists -or ($current.Exists -and ($current.Kind -ne 'DWord' -or $current.Data -ne $change.BeforeData))) {
        throw "Registration changed during validation; no changes were made. Run the script again: $($change.Name)"
    }
}
$backupFolder = Join-Path $repositoryRoot 'temp\vulkan-layer-registration'
[IO.Directory]::CreateDirectory($backupFolder) | Out-Null
$backupPath = Join-Path $backupFolder ('backup-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N') + '.json')
$snapshot = [ordered]@{ Schema = 1; MachineName = [Environment]::MachineName; CreatedUtc = [DateTime]::UtcNow.ToString('o');
    RegistryPath = $registryPath; LayerName = $layerName; Payloads = $payloads;
    ExistingRegistrations = $registrySnapshot; Changes = $changes.ToArray() }
[IO.File]::WriteAllText($backupPath, ($snapshot | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Write-Host "Backup: $backupPath"
$applied = [Collections.Generic.List[object]]::new()
try {
    foreach ($change in $changes) {
        $applied.Add($change)
        Set-Registration $change.Hive $change.View $change.Name $change.AfterExists $change.AfterData
    }
    foreach ($change in $changes) {
        $actual = Get-Registration $change.Hive $change.View $change.Name
        if ($actual.Exists -ne $change.AfterExists -or ($actual.Exists -and ($actual.Kind -ne 'DWord' -or $actual.Data -ne $change.AfterData))) {
            throw "Registration verification failed: $($change.Name)"
        }
    }
}
catch {
    $failure = $_
    for ($index = $applied.Count - 1; $index -ge 0; $index--) {
        $change = $applied[$index]
        if ($change.Hive -ne 'LocalMachine') { continue }
        try { Set-Registration $change.Hive $change.View $change.Name $change.BeforeExists $change.BeforeData }
        catch { Write-Warning "Automatic HKLM rollback failed for $($change.Name). Use the backup: $backupPath" }
    }
    throw $failure
}
Write-Host 'Registration verified. Restart Vulkan games to load the selected layer.'
Write-Host "Restore HKLM registrations with: .\scripts\Register-DevelopmentVulkanLayer.ps1 -Restore '$backupPath'"
Write-Host 'Unsafe HKCU registrations remain removed after restore.'
