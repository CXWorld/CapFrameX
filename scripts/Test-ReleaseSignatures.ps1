[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Root,
    [string]$InventoryPath
)

$ErrorActionPreference = 'Stop'
$rootPath = (Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
$files = @(Get-ChildItem -LiteralPath $rootPath -Recurse -File |
    Where-Object { $_.Extension.ToLowerInvariant() -in @('.dll', '.exe', '.sys', '.msi', '.ps1') })
if ($files.Count -eq 0) { throw "No release binaries found in $rootPath" }

$inventory = foreach ($file in $files)
{
    $signature = Get-AuthenticodeSignature -LiteralPath $file.FullName
    [pscustomobject]@{
        Path = $file.FullName.Substring($rootPath.Length + 1)
        Status = [string]$signature.Status
        Thumbprint = $signature.SignerCertificate.Thumbprint
        Timestamp = $signature.TimeStamperCertificate.Subject
        SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash
    }
}
if ($InventoryPath) { $inventory | Export-Csv -LiteralPath $InventoryPath -NoTypeInformation -Encoding UTF8 }
$invalid = @($inventory | Where-Object Status -ne 'Valid')
if ($invalid.Count -gt 0)
{
    $details = ($invalid | ForEach-Object { "$($_.Path): $($_.Status)" }) -join [Environment]::NewLine
    throw "Release signature check failed ($($invalid.Count)/$($files.Count)):`n$details"
}
Write-Output "Verified $($files.Count) release signatures in $rootPath"
