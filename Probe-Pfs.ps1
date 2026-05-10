$dll = 'C:\Users\Intel\source\repos\CapFrameX\source\bin\Release\x64\net472\LibreHardwareMonitorLib.dll'
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
$asm.GetType('LibreHardwareMonitor.Hardware.OpCode').GetMethod('Open',[System.Reflection.BindingFlags]'Static,Public,NonPublic').Invoke($null,@()) | Out-Null
$tO = $asm.GetType('LibreHardwareMonitor.PawnIo.IntelOobmsm')
$o = [Activator]::CreateInstance($tO)
$mRead = $tO.GetMethod('TryReadBarDword')

function DumpRegion([int]$start, [int]$count, [string]$label) {
    Write-Host ""
    Write-Host "--- $label : 0x$('{0:X4}' -f $start) ($count dwords) ---"
    for ($i = 0; $i -lt $count; $i++) {
        $off = $start + $i * 4
        $a = [object[]]@([int]$off, [uint32]0)
        $ok = $mRead.Invoke($o,$a)
        $val = [uint32]$a[1]
        if ($i % 4 -eq 0) {
            Write-Host -NoNewline ("  +0x{0:X4}: " -f $off)
        }
        Write-Host -NoNewline ("0x{0:X8} " -f $val)
        if ($i % 4 -eq 3) { Write-Host "" }
    }
    if ($count % 4 -ne 0) { Write-Host "" }
}

# GUID 0x3086000 "normal Punit telemetry" lives at BAR0+0x82E8
DumpRegion 0x82E8 8 "Aperture for GUID 0x3086000 (normal Punit) header"

# Container_2 (LNL D2D layout) should be at BAR0+0x82F8
# bits[34..41] = MAIN_NOC_FREQ (NCLK) x 50 MHz (= our 'NGU')
# bits[50..57] = D2D_FREQ x 50 MHz
DumpRegion 0x82F8 4 "Container_2 (LNL D2D layout)"
$a = [object[]]@([int]0x82F8, [uint32]0); $mRead.Invoke($o,$a) | Out-Null; $lo = [uint32]$a[1]
$a = [object[]]@([int]0x82FC, [uint32]0); $mRead.Invoke($o,$a) | Out-Null; $hi = [uint32]$a[1]
$qword = ([uint64]$hi -shl 32) -bor [uint64]$lo
"  qword = 0x{0:X16}" -f $qword
$ngu_ratio = ($qword -shr 34) -band 0xFF
$d2d_ratio = ($qword -shr 50) -band 0xFF
"  bits[34..41] (NCLK ratio)   = 0x{0:X2} = {0} -> {1} MHz" -f $ngu_ratio, ($ngu_ratio * 50)
"  bits[50..57] (D2D ratio)    = 0x{0:X2} = {0} -> {1} MHz" -f $d2d_ratio, ($d2d_ratio * 50)

# Also check the area around 0x8000 (fixed telemetry)
DumpRegion 0x8000 8 "Aperture for GUID 0x3086100 (fixed Punit) header"

# Try a wider scan around 0x82E8 to find natural alignment
DumpRegion 0x82E0 16 "Wider context around 0x82E8"

$tO.GetMethod('Close').Invoke($o,@()) | Out-Null
