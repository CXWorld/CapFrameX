<#
.SYNOPSIS
    Registers the CapFrameX Vulkan layers of this folder, which draw the in-game overlay in
    Vulkan games.
.DESCRIPTION
    Staged as vulkan\Register-CapFrameXVulkanLayer.ps1 beside the x64 layer; the x86 layer lives
    in vulkan\x86. The portable CapFrameX runs it when the user agrees at startup; the installer
    registers its layers itself and does not ship this script.

    The Vulkan loader reads implicit layers from HKLM\SOFTWARE\Khronos\Vulkan\ImplicitLayers, one
    registry view per bitness: the x64 manifest goes into the 64-bit view, the x86 manifest into
    WOW6432Node. It identifies a layer by the NAME inside its manifest, so a second CapFrameX
    registration competes with this one. Every other enabled CapFrameX registration in HKLM is
    therefore disabled (DWORD 1, not deleted: an installed CapFrameX keeps its value, and
    repairing that installation enables it again). CapFrameX registrations in HKCU are removed:
    the loader ignores HKCU for elevated games, and HKCU is not split by bitness, so an entry there
    disables the layer for the other bitness.

    Only CapFrameX registry values are changed; no file is written. Requires an elevated
    PowerShell and never starts UAC itself. Exit code 0 means the layers are registered.
.PARAMETER ValidateOnly
    Check the layer files and show the planned changes without writing to the registry.
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\vulkan\Register-CapFrameXVulkanLayer.ps1
#>
[CmdletBinding()]
param(
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$registryPath = 'SOFTWARE\Khronos\Vulkan\ImplicitLayers'
$layerName = 'VK_LAYER_CAPFRAMEX_overlay'
$manifestFileName = 'cfx_osd_vklayer_v1.json'

# Console streams, so that CapFrameX, which redirects them, logs every line. CapFrameX reads
# them as UTF-8; the console code page would garble paths with non-ASCII characters.
if ([Console]::IsOutputRedirected) {
    try { [Console]::OutputEncoding = New-Object Text.UTF8Encoding($false) } catch { }
}
function Write-Line([string]$Text) { [Console]::Out.WriteLine($Text) }

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try {
        $principal = New-Object Security.Principal.WindowsPrincipal($identity)
        return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    finally { $identity.Dispose() }
}

function Get-JsonProperty($Object, [string]$Name) {
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

# $true when the manifest declares the CapFrameX layer, $false when it declares another one,
# $null when it cannot be read.
function Test-ManifestDeclaresLayer([string]$Path) {
    try {
        if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
        $document = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
        $layers = @(Get-JsonProperty $document 'layer') + @(Get-JsonProperty $document 'layers')
        foreach ($layer in $layers) {
            if ((Get-JsonProperty $layer 'name') -ceq $layerName) { return $true }
        }
        return $false
    }
    catch { return $null }
}

# A registration whose manifest is gone or damaged still counts as CapFrameX's by its file name,
# as for the installer's purge and the Info tab.
function Test-CapFrameXRegistration([string]$Name) {
    $declares = Test-ManifestDeclaresLayer $Name
    if ($null -ne $declares) { return $declares }
    try { return [IO.Path]::GetFileName($Name) -like 'cfx_osd_vklayer*.json' }
    catch { return $false }
}

function Get-LayerPayload([string]$Folder, [string]$View, [int]$ExpectedMachine) {
    $manifest = Join-Path $Folder $manifestFileName
    if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw "Layer manifest missing: $manifest" }
    $manifest = (Get-Item -LiteralPath $manifest).FullName
    $layer = Get-JsonProperty (Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json) 'layer'
    if ((Get-JsonProperty $layer 'name') -cne $layerName) { throw "Not a CapFrameX layer manifest: $manifest" }
    $library = [string](Get-JsonProperty $layer 'library_path')
    if ([string]::IsNullOrWhiteSpace($library) -or [IO.Path]::IsPathRooted($library)) {
        throw "The layer DLL path must be relative: $manifest"
    }
    $dll = [IO.Path]::GetFullPath((Join-Path $Folder $library))
    $expectedDll = [IO.Path]::GetFullPath((Join-Path $Folder 'cfx_osd_vklayer.dll'))
    if (-not $dll.Equals($expectedDll, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $dll -PathType Leaf)) {
        throw "The manifest must reference its adjacent cfx_osd_vklayer.dll: $manifest"
    }
    $stream = [IO.File]::OpenRead($dll)
    $reader = New-Object IO.BinaryReader($stream)
    try {
        if ($stream.Length -lt 64 -or $reader.ReadUInt16() -ne 0x5A4D) { throw "Invalid PE DLL: $dll" }
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        if ($peOffset -lt 64 -or $peOffset -gt $stream.Length - 6) { throw "Invalid PE header offset: $dll" }
        $stream.Position = $peOffset
        if ($reader.ReadUInt32() -ne 0x00004550 -or $reader.ReadUInt16() -ne $ExpectedMachine) {
            throw "Wrong DLL architecture for ${View}: $dll"
        }
    }
    finally { $reader.Dispose(); $stream.Dispose() }
    [pscustomobject]@{ View = $View; Manifest = $manifest; Dll = $dll }
}

function Open-BaseKey([string]$Hive, [string]$View) {
    [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::$Hive, [Microsoft.Win32.RegistryView]::$View)
}

# Data is the DWORD the loader reads (0 = enabled), $null for a value of another type.
function Get-Registration([string]$Hive, [string]$View, [string]$Name) {
    $base = Open-BaseKey $Hive $View
    try {
        $key = $base.OpenSubKey($registryPath, $false)
        try {
            $exists = $null -ne $key -and @($key.GetValueNames()) -contains $Name
            $data = $null
            if ($exists -and $key.GetValueKind($Name) -eq [Microsoft.Win32.RegistryValueKind]::DWord) { $data = [int]$key.GetValue($Name) }
            [pscustomobject]@{ Exists = $exists; Data = $data }
        }
        finally { if ($null -ne $key) { $key.Dispose() } }
    }
    finally { $base.Dispose() }
}

function Get-CapFrameXRegistrations([string]$Hive, [string]$View) {
    $base = Open-BaseKey $Hive $View
    try {
        $key = $base.OpenSubKey($registryPath, $false)
        try {
            if ($null -eq $key) { return }
            foreach ($name in $key.GetValueNames()) {
                if (-not (Test-CapFrameXRegistration $name)) { continue }
                $data = $null
                if ($key.GetValueKind($name) -eq [Microsoft.Win32.RegistryValueKind]::DWord) { $data = [int]$key.GetValue($name) }
                [pscustomobject]@{ Hive = $Hive; View = $View; Name = $name; Data = $data }
            }
        }
        finally { if ($null -ne $key) { $key.Dispose() } }
    }
    finally { $base.Dispose() }
}

function Set-Registration([string]$Hive, [string]$View, [string]$Name, [bool]$Exists, $Data) {
    $base = Open-BaseKey $Hive $View
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

function New-Change([string]$Hive, [string]$View, [string]$Name, $Before, [bool]$AfterExists, $AfterData) {
    [pscustomobject]@{ Hive = $Hive; View = $View; Name = $Name;
        BeforeExists = $Before.Exists; BeforeData = $Before.Data; AfterExists = $AfterExists; AfterData = $AfterData }
}

try {
    $payloads = @(
        Get-LayerPayload $PSScriptRoot 'Registry64' 0x8664
        Get-LayerPayload (Join-Path $PSScriptRoot 'x86') 'Registry32' 0x014C
    )

    $changes = New-Object 'System.Collections.Generic.List[object]'
    foreach ($payload in $payloads) {
        Write-Line "$($payload.View): $($payload.Manifest)"
        foreach ($current in @(Get-CapFrameXRegistrations 'LocalMachine' $payload.View)) {
            if ($current.Name -ine $payload.Manifest -and $current.Data -eq 0) {
                $changes.Add((New-Change 'LocalMachine' $payload.View $current.Name ([pscustomobject]@{ Exists = $true; Data = 0 }) $true 1))
            }
        }
        $existing = Get-Registration 'LocalMachine' $payload.View $payload.Manifest
        if (-not $existing.Exists -or $existing.Data -ne 0) {
            $changes.Add((New-Change 'LocalMachine' $payload.View $payload.Manifest $existing $true 0))
        }
    }
    # HKCU\Software is shared by both views, so one of them covers it.
    foreach ($current in @(Get-CapFrameXRegistrations 'CurrentUser' 'Registry64')) {
        $changes.Add((New-Change 'CurrentUser' 'Registry64' $current.Name ([pscustomobject]@{ Exists = $true; Data = $current.Data }) $false $null))
    }

    foreach ($change in $changes) {
        $action = if (-not $change.AfterExists) { 'remove' } elseif ($change.AfterData -eq 0) { 'enable' } else { 'disable' }
        Write-Line "  $action $($change.Hive) $($change.View): $($change.Name)"
    }
    if ($ValidateOnly) { Write-Line 'Validation only: the registry was not changed.'; exit 0 }
    if ($changes.Count -eq 0) { Write-Line 'The layers of this folder are already registered; nothing changed.'; exit 0 }
    if (-not (Test-Administrator)) {
        throw 'Run this script from an elevated PowerShell (Run as administrator). Nothing was changed.'
    }

    $applied = New-Object 'System.Collections.Generic.List[object]'
    try {
        foreach ($change in $changes) {
            $applied.Add($change)
            Set-Registration $change.Hive $change.View $change.Name $change.AfterExists $change.AfterData
        }
        foreach ($change in $changes) {
            $actual = Get-Registration $change.Hive $change.View $change.Name
            if ($actual.Exists -ne $change.AfterExists -or ($actual.Exists -and $actual.Data -ne $change.AfterData)) {
                throw "Registration verification failed: $($change.Name)"
            }
        }
    }
    catch {
        $failure = $_
        # The removed HKCU values are not restored: they are what breaks the layer.
        for ($index = $applied.Count - 1; $index -ge 0; $index--) {
            $change = $applied[$index]
            if ($change.Hive -ne 'LocalMachine') { continue }
            try { Set-Registration $change.Hive $change.View $change.Name $change.BeforeExists $change.BeforeData }
            catch { [Console]::Error.WriteLine("Rollback failed for $($change.Name): $($_.Exception.Message)") }
        }
        throw $failure
    }

    Write-Line 'Registration verified. Restart running Vulkan games to load the layer.'
    exit 0
}
catch {
    [Console]::Error.WriteLine($_.Exception.Message)
    exit 1
}
