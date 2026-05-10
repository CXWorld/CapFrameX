# Test-IntelOobmsm.ps1
#
# Standalone Triage fuer die OOBMSM-Pipeline. Laedt LibreHardwareMonitorLib,
# instanziert IntelOobmsm + IntelOobmsmClocks, dumpt jeden Schritt der Chain.
#
# Usage:
#   pwsh -File .\Test-IntelOobmsm.ps1
#   pwsh -File .\Test-IntelOobmsm.ps1 -DllPath '...\bin\Release\x64\net472\LibreHardwareMonitorLib.dll'
#
# Voraussetzung: PawnIO-Treiber installiert + laeuft, elevated PowerShell.

[CmdletBinding()]
param(
    [string]$DllPath = "$PSScriptRoot\source\bin\Release\x64\net472\LibreHardwareMonitorLib.dll"
)

if (-not (Test-Path $DllPath)) {
    Write-Error "DLL not found: $DllPath"
    exit 1
}

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host " IntelOOBMSM Pipeline Diagnostic" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan
Write-Host "DLL: $DllPath"
Write-Host ""

Write-Host "[0] PawnIO driver pre-check..."
$svc = Get-Service -Name PawnIO -ErrorAction SilentlyContinue
if ($null -eq $svc) {
    Write-Host "    Service PawnIO   : NOT INSTALLED" -ForegroundColor Red
} else {
    Write-Host "    Service PawnIO   : $($svc.Status)"
}
$dev = Test-Path '\\?\GLOBALROOT\Device\PawnIO'
Write-Host "    \Device\PawnIO   : $(if ($dev) { 'present' } else { 'maybe (Test-Path lies)' })"
$ts = (bcdedit /enum '{current}' | Select-String 'testsigning').Line
Write-Host "    bcdedit          : $($ts.Trim())"
Write-Host ""

$asm = [System.Reflection.Assembly]::LoadFrom($DllPath)
# IntelOobmsm uses OpCode.CpuId (internal static) for platform resolution.
# In production it gets initialised by Computer.Open(); standalone we
# call OpCode.Open() via reflection ourselves.
$tOpCode = $asm.GetType('LibreHardwareMonitor.Hardware.OpCode', $true, $false)
$mOpCodeOpen = $tOpCode.GetMethod('Open', [System.Reflection.BindingFlags]'Static,Public,NonPublic')
if ($null -ne $mOpCodeOpen) {
    [void]$mOpCodeOpen.Invoke($null, @())
    Write-Host "    OpCode.Open()    : invoked"
}

$tOobmsm = $asm.GetType('LibreHardwareMonitor.PawnIo.IntelOobmsm', $true)
$tClocks = $asm.GetType('LibreHardwareMonitor.PawnIo.IntelOobmsmClocks', $true)

# ----- Stage 1: kernel module + CPU allowlist -----
Write-Host "[1] Constructing IntelOobmsm (kernel module load)..."
$oobmsm = [System.Activator]::CreateInstance($tOobmsm)
$isLoaded = $tOobmsm.GetProperty('IsLoaded').GetValue($oobmsm)
$platform = $tOobmsm.GetProperty('DetectedPlatform').GetValue($oobmsm)
$validated = $tOobmsm.GetProperty('IsValidated').GetValue($oobmsm)
$fPawn = $tOobmsm.GetField('_pawnIO', [System.Reflection.BindingFlags]'Instance,NonPublic')
$pawnInstance = $fPawn.GetValue($oobmsm)
$pPawnLoaded = $pawnInstance.GetType().GetProperty('IsLoaded', [System.Reflection.BindingFlags]'Instance,NonPublic')
$pawnLoaded = $pPawnLoaded.GetValue($pawnInstance)
Write-Host "    IsLoaded         : $isLoaded"
Write-Host "    DetectedPlatform : $platform"
Write-Host "    IsValidated      : $validated"
Write-Host "    PawnIO module    : $(if ($pawnLoaded) { 'loaded' } else { 'NOT loaded (main() returned error)' })"

if (-not $isLoaded) {
    Write-Host ""
    Write-Host "STOP: kernel module did not load." -ForegroundColor Red
    exit 2
}

# ----- Stage 2: Identity -----
Write-Host ""
Write-Host "[2] Identity..."
$mGetId = $tOobmsm.GetMethod('TryGetIdentity')
$args2 = [object[]]@([uint16]0, [uint16]0, [byte]0, [byte]0, [byte]0, [uint64]0, [int]0)
$ok = $mGetId.Invoke($oobmsm, $args2)
if ($ok) {
    Write-Host ("    VID/DID          : 0x{0:X4} / 0x{1:X4}" -f $args2[0], $args2[1])
    Write-Host ("    BDF              : {0:X2}:{1:X2}.{2}" -f $args2[2], $args2[3], $args2[4])
    Write-Host ("    BAR0 phys / size : 0x{0:X16} / 0x{1:X}" -f $args2[5], $args2[6])
} else {
    Write-Host "STOP: TryGetIdentity returned False." -ForegroundColor Red
    exit 3
}

# ----- Stage 3: Ext-cap walk (C#-side) -----
Write-Host ""
Write-Host "[3] Extended-capability walk..."
$caps = $tOobmsm.GetMethod('EnumerateExtCaps').Invoke($oobmsm, @())
Write-Host ("    enumerated       : {0}" -f $caps.Count)
$tEntry = $tOobmsm.GetNestedType('ExtCapEntry')
foreach ($c in $caps) {
    $cfg  = $tEntry.GetProperty('ConfigOffset').GetValue($c)
    $cid  = $tEntry.GetProperty('CapId').GetValue($c)
    $cver = $tEntry.GetProperty('CapVersion').GetValue($c)
    $vid2 = $tEntry.GetProperty('VsecId').GetValue($c)
    $vlen = $tEntry.GetProperty('VsecLength').GetValue($c)
    $tbir = $tEntry.GetProperty('Tbir').GetValue($c)
    $disc = $tEntry.GetProperty('DiscoveryOffset').GetValue($c)
    $kind = switch ($cid) { 0x000B { 'VSEC' }; 0x0023 { 'TPMI' }; default { 'cap' } }
    Write-Host ("    cfg=0x{0:X3}  id=0x{1:X4} ({2})  ver={3}  vsec_id=0x{4:X4}  len={5}  TBIR={6}  disc=0x{7:X8}" -f $cfg, $cid, $kind, $cver, $vid2, $vlen, $tbir, $disc)
}

# ----- Stage 4: smoke-test BAR0 read -----
Write-Host ""
Write-Host "[4] BAR0 smoke-read (first 32 bytes)..."
$mReadBar = $tOobmsm.GetMethod('TryReadBarDword')
foreach ($off in 0,4,8,12,16,20,24,28) {
    $a = [object[]]@([int]$off, [uint32]0)
    $okR = $mReadBar.Invoke($oobmsm, $a)
    Write-Host ("    BAR0 +0x{0:X4}      : ok={1}  val=0x{2:X8}" -f $off, $okR, $a[1])
}

# ----- Stage 5: IntelOobmsmClocks -----
Write-Host ""
Write-Host "[5] IntelOobmsmClocks..."
$clocks = [Activator]::CreateInstance($tClocks, @($oobmsm))
$isReady   = $tClocks.GetProperty('IsReady').GetValue($clocks)
$isValid   = $tClocks.GetProperty('IsValidated').GetValue($clocks)
$hasNgu    = $tClocks.GetProperty('HasNgu').GetValue($clocks)
$hasD2d    = $tClocks.GetProperty('HasD2d').GetValue($clocks)
$subOff    = $tClocks.GetProperty('SubApertureBarOffset').GetValue($clocks)
$guid      = $tClocks.GetProperty('MatchedGuid').GetValue($clocks)
Write-Host "    IsReady          : $isReady"
Write-Host "    IsValidated      : $isValid"
Write-Host "    HasNgu / HasD2d  : $hasNgu / $hasD2d"
Write-Host ("    SubAperture@BAR0 : 0x{0:X8}" -f $subOff)
Write-Host ("    Matched GUID     : 0x{0:X8}" -f $guid)

if ($isReady) {
    $tSample = $tClocks.GetNestedType('Sample')
    $sampleArg = [Activator]::CreateInstance($tSample)
    $a = [object[]]@($sampleArg)
    $okRead = $tClocks.GetMethod('TryRead').Invoke($clocks, $a)
    $sample = $a[0]
    Write-Host ""
    Write-Host "[6] Live read..."
    if ($okRead) {
        $hasN = $tSample.GetProperty('HasNgu').GetValue($sample)
        $hasD = $tSample.GetProperty('HasD2d').GetValue($sample)
        $nMhz = $tSample.GetProperty('NguMhz').GetValue($sample)
        $dMhz = $tSample.GetProperty('D2dMhz').GetValue($sample)
        $rawN = $tSample.GetProperty('RawNgu').GetValue($sample)
        $rawD = $tSample.GetProperty('RawD2d').GetValue($sample)
        Write-Host ("    NGU              : {0} MHz  (raw=0x{1:X16}, hasNgu={2})" -f $nMhz, $rawN, $hasN)
        Write-Host ("    D2D              : {0} MHz  (raw=0x{1:X16}, hasD2d={2})" -f $dMhz, $rawD, $hasD)
    } else {
        Write-Host "    TryRead returned False (decode mismatch or read failure)" -ForegroundColor Yellow
    }
}

$tOobmsm.GetMethod('Close').Invoke($oobmsm, @()) | Out-Null
Write-Host ""
Write-Host "Done." -ForegroundColor Green
