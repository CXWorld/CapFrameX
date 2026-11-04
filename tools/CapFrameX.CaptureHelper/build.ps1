<#
.SYNOPSIS
Builds the x86 and the x64 variant of the CapFrameX Capture Helper and zips them.

.DESCRIPTION
Writes publish\x86, publish\x64, a zip per variant (CapFrameX.CaptureHelper-<version>-<platform>.zip)
and one with both (CapFrameX.CaptureHelper-<version>.zip, folders x86 and x64) next to this script.
-Platform builds one variant only, e.g. while the other one runs from publish; the zip with both then
takes the other from its last build. Needs the .NET SDK and the .NET Framework 4.8 targeting pack
(part of Visual Studio).
#>
param(
    [string] $Configuration = 'Release',
    [ValidateSet('x86', 'x64')] [string[]] $Platform = @('x86', 'x64'))

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'CapFrameX.CaptureHelper.csproj'
$publish = Join-Path $PSScriptRoot 'publish'
$version = ([xml] (Get-Content $project)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1

foreach ($variant in $Platform) {
    $out = Join-Path $publish $variant
    if (Test-Path $out) { Remove-Item $out -Recurse -Force }
    # No trailing backslash: before a closing quote it would escape the quote. MSBuild adds it.
    dotnet build $project -c $Configuration "-p:Platform=$variant" "-p:OutDir=$out" --nologo -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "The $variant build failed." }

    $zip = Join-Path $publish "CapFrameX.CaptureHelper-$version-$variant.zip"
    if (Test-Path $zip) { Remove-Item $zip }
    Get-ChildItem $out -Exclude *.pdb | Compress-Archive -DestinationPath $zip
    "$variant package: $zip"
}

$missing = 'x86', 'x64' | Where-Object { -not (Test-Path (Join-Path $publish "$_\CapFrameX.CaptureHelper.exe")) }
if ($missing) {
    "No package with both variants: $($missing -join ' and ') not built yet."
    return
}
$staging = Join-Path $publish 'both'
if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
foreach ($variant in 'x86', 'x64') {
    $target = New-Item -ItemType Directory (Join-Path $staging $variant)
    Get-ChildItem (Join-Path $publish $variant) -Exclude *.pdb | Copy-Item -Destination $target -Recurse
}
$both = Join-Path $publish "CapFrameX.CaptureHelper-$version.zip"
if (Test-Path $both) { Remove-Item $both }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $both
Remove-Item $staging -Recurse -Force
"package with both variants: $both"
