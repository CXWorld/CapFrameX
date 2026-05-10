$dll = 'C:\Users\Intel\source\repos\CapFrameX\source\bin\Release\x64\net472\LibreHardwareMonitorLib.dll'
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
$asm.GetType('LibreHardwareMonitor.Hardware.OpCode').GetMethod('Open',[System.Reflection.BindingFlags]'Static,Public,NonPublic').Invoke($null,@()) | Out-Null
$tO = $asm.GetType('LibreHardwareMonitor.PawnIo.IntelOobmsm')
$o = [Activator]::CreateInstance($tO)
$mPhys = $tO.GetMethod('TryReadPhysicalDword')
$mGetBar = $tO.GetMethod('TryGetBarPhysicalAddress')
$mRead = $tO.GetMethod('TryReadConfigDword')

# Use the existing TryGetBarPhysicalAddress
$a = [object[]]@([int]0,[uint64]0)
$mGetBar.Invoke($o,$a) | Out-Null
$bar0 = [uint64]$a[1]
"BAR0 base = 0x{0:X16}" -f $bar0

# Probe TPMI[0] aperture at BAR0 + 0xF800
foreach ($off in 0,4,8,12,16,20,24,28,32,64,128,256) {
    $addr = $bar0 + [uint64]0xF800 + [uint64]$off
    $a = [object[]]@($addr, [uint32]0)
    $ok = $mPhys.Invoke($o,$a)
    $val = [uint32]$a[1]
    "  +0x{0:X5}  ok={1}  val=0x{2:X8}" -f ($off+0xF800), $ok, $val
}
"---"
"Try LNL D2D-style offsets:"
foreach ($container_off in 0x82F8, 0x6348, 0x82E8, 0x8300, 0) {
    $addr = $bar0 + [uint64]0xF800 + [uint64]$container_off
    $a = [object[]]@($addr, [uint32]0)
    $ok = $mPhys.Invoke($o,$a)
    $val = [uint32]$a[1]
    "  rel 0x{0:X4}  ok={1}  val=0x{2:X8}" -f $container_off, $ok, $val
}

$tO.GetMethod('Close').Invoke($o,@()) | Out-Null
