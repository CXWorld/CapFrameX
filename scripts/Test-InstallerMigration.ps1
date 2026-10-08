<#
.SYNOPSIS
Runs isolated MSI downgrade and install-folder migration regression tests.
.DESCRIPTION
Requires Windows, WiX 6 (restored by the installer build), and .NET Framework's C#
compiler. Builds tiny per-user MSI products with unique product/upgrade/component
identities under temp/installer-migration-tests. It never installs or uninstalls
CapFrameX. Test products are uninstalled in finally; payloads, MSIs and logs remain.
The harvesting, upgrade, REINSTALLMODE and legacy cleanup rules come from the real
installer sources. Only fixture paths and registry/component identities change.
.EXAMPLE
pwsh -File scripts/Test-InstallerMigration.ps1 -IncludeNegativeControl
#>
[CmdletBinding()]
param(
    [string]$WixPath,
    [switch]$BuildOnly,
    [switch]$IncludeNegativeControl
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$runId = [Guid]::NewGuid().ToString('N')
$runRoot = Join-Path $repoRoot "temp\installer-migration-tests\$runId"
$null = New-Item -ItemType Directory -Path $runRoot -Force

if (-not $WixPath)
{
    $nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE '.nuget\packages' }
    $WixPath = Join-Path $nugetRoot 'wixtoolset.sdk\6.0.2\tools\net472\x64\wix.exe'
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
foreach ($tool in @($WixPath, $compiler))
{
    if (-not (Test-Path -LiteralPath $tool -PathType Leaf)) { throw "Missing tool: $tool. Restore/build the installer first." }
}

[xml]$product = Get-Content -LiteralPath (Join-Path $repoRoot 'source\CapFrameXInstaller\Product.wxs') -Raw
[xml]$cleanup = Get-Content -LiteralPath (Join-Path $repoRoot 'source\CapFrameXInstaller\LegacyVulkanCleanup.wxs') -Raw
$namespace = 'http://wixtoolset.org/schemas/v4/wxs'
$ns = [System.Xml.XmlNamespaceManager]::new($product.NameTable)
$ns.AddNamespace('w', $namespace)
$cleanupNs = [System.Xml.XmlNamespaceManager]::new($cleanup.NameTable)
$cleanupNs.AddNamespace('w', $namespace)
$upgradeRule = $product.SelectSingleNode('//w:MajorUpgrade', $ns)
$reinstallRule = $product.SelectSingleNode('//w:SetProperty[@Id="REINSTALLMODE"]', $ns)
$installDirectory = $product.SelectSingleNode('//w:Directory[@Id="INSTALLFOLDER"]', $ns)
$binaryGroup = $product.SelectSingleNode('//w:ComponentGroup[@Id="CapFrameXBinaries"]', $ns)
$cleanupComponent = $cleanup.SelectSingleNode('//w:Component[@Id="LegacyVulkanCleanup"]', $cleanupNs)
$legacyDirectory = $cleanup.SelectSingleNode('//w:Directory[@Id="LEGACYINSTALLFOLDER"]', $cleanupNs)
if (-not $reinstallRule -or -not $installDirectory.GetAttribute('ComponentGuidGenerationSeed') -or -not $cleanupComponent)
{
    throw 'The production installer is missing the migration seed, REINSTALLMODE rule or legacy Vulkan cleanup component.'
}

function Assert-UnderRunRoot([string]$Path)
{
    $fullPath = [IO.Path]::GetFullPath($Path)
    if (-not $fullPath.StartsWith($runRoot + '\', [StringComparison]::OrdinalIgnoreCase))
    {
        throw "Fixture path escapes the test run: $fullPath"
    }
    return $fullPath
}

function New-VersionedPayload([string]$Directory, [string]$Version)
{
    $null = New-Item -ItemType Directory -Path $Directory -Force
    $source = Join-Path $Directory 'Fixture.cs'
    @"
using System.Reflection;
[assembly: AssemblyVersion("$Version")]
[assembly: AssemblyFileVersion("$Version")]
public static class Fixture { public static void Main() {} }
"@ | Set-Content -LiteralPath $source -Encoding UTF8
    foreach ($file in @('CapFrameX.exe', 'CapFrameX.dll', 'CapFrameX.ADLX.dll'))
    {
        $target = if ($file.EndsWith('.exe')) { 'exe' } else { 'library' }
        & $compiler /nologo "/target:$target" "/out:$(Join-Path $Directory $file)" $source
        if ($LASTEXITCODE -ne 0) { throw "Could not compile fixture $file" }
    }
    # These two explicit files accompany the production Files harvest.
    '<configuration />' | Set-Content -LiteralPath (Join-Path $Directory 'CapFrameX.dll.config')
    '<profiles />' | Set-Content -LiteralPath (Join-Path $Directory 'HookCompatibilityProfiles.xml')
}

function New-FixtureMsi($Scenario, [bool]$Old)
{
    $label = if ($Old) { 'old' } else { 'new' }
    $payload = Join-Path $Scenario.Root "$label-payload"
    New-VersionedPayload $payload $(if ($Old) { '2.0.0.0' } else { '1.0.0.0' })

    $group = $binaryGroup.CloneNode($true)
    foreach ($attribute in $group.SelectNodes('.//@*'))
    {
        $attribute.Value = $attribute.Value.Replace('$(var.CapFrameXInstaller.BinaryDir)', $payload)
    }
    # Fixture.cs is a compiler input, not part of the installed payload.
    $files = $group.SelectSingleNode('w:Files', $ns)
    $exclude = $product.CreateElement('Exclude', $namespace)
    $exclude.SetAttribute('Files', (Join-Path $payload 'Fixture.cs'))
    $null = $files.AppendChild($exclude)

    $directory = $installDirectory.CloneNode($true)
    # A real production seed would collide with installed CapFrameX components.
    # Keep the old/new identity relationship but namespace it to this test product.
    if ($Old -and $Scenario.Relocated)
    {
        $directory.RemoveAttribute('ComponentGuidGenerationSeed')
    }
    else
    {
        $directory.SetAttribute('ComponentGuidGenerationSeed', $Scenario.ComponentSeed)
    }
    $legacy = $legacyDirectory.CloneNode($true)
    $component = $cleanupComponent.CloneNode($true)
    $component.SetAttribute('Guid', $Scenario.CleanupGuid)
    foreach ($registry in $component.SelectNodes('.//w:RegistryValue', $cleanupNs))
    {
        $registry.SetAttribute('Root', 'HKCU')
        $registry.SetAttribute('Key', "Software\CapFrameXInstallerMigrationTests\$runId\$($Scenario.Name)")
    }

    $productCode = if ($Old) { $Scenario.OldCode } else { $Scenario.NewCode }
    $version = if ($Old) { '2.0.0' } else { '1.0.0' }
    $reinstallXml = if (-not $Old -and -not $Scenario.NegativeControl) { $reinstallRule.OuterXml } else { '' }
    $cleanupXml = if (-not $Old) { $component.OuterXml } else { '' }
    $cleanupRef = if (-not $Old) { '<ComponentRef Id="LegacyVulkanCleanup" />' } else { '' }
    $wxs = Join-Path $Scenario.Root "$label.wxs"
    @"
<Wix xmlns="$namespace">
  <Package Name="CapFrameX installer regression $runId $($Scenario.Name) $label"
           Manufacturer="CapFrameX regression tests" Version="$version" Language="1033"
           ProductCode="$productCode" UpgradeCode="$($Scenario.UpgradeCode)" Scope="perUser">
    $($upgradeRule.OuterXml)
    $reinstallXml
    <MediaTemplate EmbedCab="yes" />
    <Feature Id="ProductFeature"><ComponentGroupRef Id="CapFrameXBinaries" />$cleanupRef</Feature>
  </Package>
  <Fragment>
    <StandardDirectory Id="LocalAppDataFolder">
      <Directory Id="FIXTUREROOT" Name="CapFrameXInstallerMigrationTests-$runId-$($Scenario.Name)">
        $($directory.OuterXml)
        $($legacy.OuterXml)
      </Directory>
    </StandardDirectory>
    $($group.OuterXml)
    $cleanupXml
  </Fragment>
</Wix>
"@ | Set-Content -LiteralPath $wxs -Encoding UTF8
    $msi = Join-Path $Scenario.Root "$label.msi"
    & $WixPath build $wxs -arch x64 -out $msi -intermediatefolder (Join-Path $Scenario.Root "$label-obj")
    if ($LASTEXITCODE -ne 0) { throw "WiX build failed for $wxs" }
    return $msi
}

function Invoke-FixtureMsi([string[]]$Arguments, [string]$Log, [int[]]$AllowedExitCodes = @(0))
{
    $Log = Assert-UnderRunRoot $Log
    # msiexec does not accept quoted switches. Quote only paths/property values.
    $allArguments = @($Arguments) + @('/qn', '/norestart', '/l*v', $Log)
    $commandLine = ($allArguments | ForEach-Object {
        if ($_.StartsWith('/')) { $_ }
        elseif ($_ -match '^([A-Z][A-Z0-9_]*)=(.*)$') { $Matches[1] + '="' + $Matches[2] + '"' }
        else { '"' + $_ + '"' }
    }) -join ' '
    $process = Start-Process -FilePath (Join-Path $env:WINDIR 'System32\msiexec.exe') -ArgumentList $commandLine -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(120000))
    {
        $process.Kill()
        throw "Fixture msiexec timed out. See $Log"
    }
    $process.WaitForExit()
    if ($process.ExitCode -notin $AllowedExitCodes)
    {
        throw "msiexec returned $($process.ExitCode). See $Log"
    }
}

$scenarios = @(
    @{ Name = 'relocated-downgrade'; Relocated = $true; NegativeControl = $false; KeepUserFile = $true },
    @{ Name = 'same-folder-downgrade'; Relocated = $false; NegativeControl = $false; KeepUserFile = $false }
)
if ($IncludeNegativeControl)
{
    $scenarios += @{ Name = 'without-reinstallmode'; Relocated = $false; NegativeControl = $true; KeepUserFile = $false }
}

foreach ($scenario in $scenarios)
{
    $scenario.Root = Assert-UnderRunRoot (Join-Path $runRoot $scenario.Name)
    $null = New-Item -ItemType Directory -Path $scenario.Root
    foreach ($key in @('OldCode', 'NewCode', 'UpgradeCode', 'CleanupGuid')) { $scenario[$key] = [Guid]::NewGuid().ToString('B') }
    # Incorporating the authored seed binds this identity transition to Product.wxs.
    $seedBytes = [Text.Encoding]::UTF8.GetBytes($installDirectory.GetAttribute('ComponentGuidGenerationSeed') + $runId + $scenario.Name)
    $hasher = [Security.Cryptography.MD5]::Create()
    try { $scenario.ComponentSeed = [Guid]::new($hasher.ComputeHash($seedBytes)).ToString('B') }
    finally { $hasher.Dispose() }
    $scenario.Legacy = Assert-UnderRunRoot (Join-Path $scenario.Root 'legacy')
    $scenario.Current = Assert-UnderRunRoot (Join-Path $scenario.Root 'current')
    $oldMsi = New-FixtureMsi $scenario $true
    $newMsi = New-FixtureMsi $scenario $false
    if ($BuildOnly) { Write-Output "Built $($scenario.Name): $($scenario.Root)"; continue }

    $commonProperties = @("FIXTUREROOT=$($scenario.Root)", "LEGACYINSTALLFOLDER=$($scenario.Legacy)", 'REINSTALLMODE=muso')
    $oldLocation = if ($scenario.Relocated) { $scenario.Legacy } else { $scenario.Current }
    $scenarioError = $null
    try
    {
        Invoke-FixtureMsi (@('/i', $oldMsi, "INSTALLFOLDER=$oldLocation") + $commonProperties) (Join-Path $scenario.Root 'old-install.log')
        foreach ($subfolder in @('vulkan', 'vulkan\x86'))
        {
            $folder = Join-Path $scenario.Legacy $subfolder
            $null = New-Item -ItemType Directory -Path $folder -Force
            'old layer backup' | Set-Content -LiteralPath (Join-Path $folder 'cfx_osd_vklayer.dll.before-percent-bars-20261006-211724.bak')
        }
        $userFile = Join-Path $scenario.Legacy 'keep-user-data.txt'
        $vulkanUserFile = Join-Path $scenario.Legacy 'vulkan\x86\keep-layer-notes.txt'
        if ($scenario.KeepUserFile)
        {
            'user data' | Set-Content -LiteralPath $userFile
            'layer notes' | Set-Content -LiteralPath $vulkanUserFile
        }

        Invoke-FixtureMsi (@('/i', $newMsi, "INSTALLFOLDER=$($scenario.Current)") + $commonProperties) (Join-Path $scenario.Root 'new-install.log')
        $missing = @()
        foreach ($file in @('CapFrameX.exe', 'CapFrameX.dll', 'CapFrameX.ADLX.dll'))
        {
            $installed = Join-Path $scenario.Current $file
            if (-not (Test-Path -LiteralPath $installed)) { $missing += $file; continue }
            $expected = Get-FileHash -LiteralPath (Join-Path $scenario.Root "new-payload\$file")
            if ((Get-FileHash -LiteralPath $installed).Hash -ne $expected.Hash)
            {
                throw "$($scenario.Name): installed $file does not match the selected lower-version package."
            }
        }
        if ($scenario.NegativeControl)
        {
            if ($missing.Count -ne 3) { throw "Negative control did not reproduce the three missing binaries: $missing" }
        }
        elseif ($missing.Count -gt 0) { throw "$($scenario.Name): missing $($missing -join ', ')" }

        foreach ($subfolder in @('vulkan', 'vulkan\x86'))
        {
            $backups = @(Get-ChildItem -LiteralPath (Join-Path $scenario.Legacy $subfolder) -Filter 'cfx_osd_vklayer.dll*.bak' -ErrorAction SilentlyContinue)
            if ($backups.Count -gt 0) { throw "$($scenario.Name): legacy Vulkan backups remain." }
        }
        if ($scenario.KeepUserFile)
        {
            if (-not (Test-Path -LiteralPath $userFile) -or -not (Test-Path -LiteralPath $vulkanUserFile))
            {
                throw 'Cleanup removed unrelated user data.'
            }
            $remaining = @(Get-ChildItem -LiteralPath $scenario.Legacy -Force)
            if ($remaining.Count -ne 2) { throw "Legacy application files remain after folder migration: $($remaining.Name -join ', ')" }
        }
        elseif (Test-Path -LiteralPath $scenario.Legacy) { throw 'The empty legacy application folder was not removed.' }

        if (-not $scenario.NegativeControl)
        {
            $installedExe = Assert-UnderRunRoot (Join-Path $scenario.Current 'CapFrameX.exe')
            Remove-Item -LiteralPath $installedExe
            $repairProperties = @($commonProperties | Where-Object { -not $_.StartsWith('REINSTALLMODE=') })
            Invoke-FixtureMsi (@('/fomus', $newMsi, "INSTALLFOLDER=$($scenario.Current)") + $repairProperties) (Join-Path $scenario.Root 'repair.log')
            $expectedExe = Get-FileHash -LiteralPath (Join-Path $scenario.Root 'new-payload\CapFrameX.exe')
            if (-not (Test-Path -LiteralPath $installedExe) -or (Get-FileHash -LiteralPath $installedExe).Hash -ne $expectedExe.Hash)
            {
                throw "$($scenario.Name): repair did not restore the missing executable."
            }
            if ($scenario.KeepUserFile -and -not (Test-Path -LiteralPath $vulkanUserFile))
            {
                throw 'Repair removed unrelated Vulkan user data.'
            }
        }
        Write-Output "PASS $($scenario.Name)"
    }
    catch
    {
        $scenarioError = $_
        throw
    }
    finally
    {
        # Only randomly generated fixture ProductCodes may be passed to /x.
        # 1605 means the product was already removed by the major upgrade.
        $cleanupError = $null
        foreach ($entry in @(@('new', $scenario.NewCode), @('old', $scenario.OldCode)))
        {
            try
            {
                Invoke-FixtureMsi @('/x', $entry[1]) (Join-Path $scenario.Root "$($entry[0])-uninstall.log") @(0, 1605)
            }
            catch
            {
                if (-not $cleanupError) { $cleanupError = $_ }
                Write-Warning "Fixture cleanup failed: $_"
            }
        }
        if ($cleanupError -and -not $scenarioError) { throw $cleanupError }
    }
}
Write-Output "Installer migration test artifacts: $runRoot"
