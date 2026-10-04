[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Root,
    [Parameter(Mandatory = $true)][string]$Thumbprint,
    [Parameter(Mandatory = $true)][string]$SignTool,
    [string]$TimestampUrl = 'http://time.certum.pl',
    [string]$InventoryPath
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path -LiteralPath $Root).Path
$signToolPath = (Resolve-Path -LiteralPath $SignTool).Path
$files = @(Get-ChildItem -LiteralPath $rootPath -Recurse -File |
    Where-Object { $_.Extension.ToLowerInvariant() -in @('.dll', '.exe', '.sys', '.msi', '.ps1') })
if ($files.Count -eq 0) { throw "No release binaries found in $rootPath" }

foreach ($file in $files)
{
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    # Keep existing vendor signatures and fail on corruption or untrusted signatures.
    if ($signature.Status -eq 'Valid') { continue }
    if ($signature.Status -ne 'NotSigned')
    {
        throw "Refusing to replace $($signature.Status) signature: $($file.FullName)"
    }
    if ($file.Extension -eq '.sys') { throw "Driver needs upstream kernel signing: $($file.FullName)" }

    & $signToolPath sign /sha1 $Thumbprint /tr $TimestampUrl /td sha256 /fd sha256 /d CapFrameX $file.FullName
    if ($LASTEXITCODE -ne 0) { throw "Signing failed: $($file.FullName)" }
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    if ($signature.Status -ne 'Valid' -or
        $signature.SignerCertificate.Thumbprint -ne $Thumbprint -or
        $null -eq $signature.TimeStamperCertificate)
    {
        throw "Signature or timestamp verification failed: $($file.FullName)"
    }
}
& "$PSScriptRoot\Test-ReleaseSignatures.ps1" -Root $rootPath -InventoryPath $InventoryPath
