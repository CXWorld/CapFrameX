$dll = 'C:\Users\Intel\source\repos\CapFrameX\source\bin\Release\x64\net472\LibreHardwareMonitorLib.dll'
$asm = [System.Reflection.Assembly]::LoadFrom($dll)
$asm.GetType('LibreHardwareMonitor.Hardware.OpCode').GetMethod('Open', [System.Reflection.BindingFlags]'Static,Public,NonPublic').Invoke($null, @()) | Out-Null
$tO = $asm.GetType('LibreHardwareMonitor.PawnIo.IntelOobmsm')
$o = [Activator]::CreateInstance($tO)
$mTryRead = $tO.GetMethod('TryReadConfigDword')

"--- Full ext-cap chain at 00:0A.0 ---"
$offset = [int]0x100
$iter = 0
while ($offset -ne 0 -and $iter -lt 32) {
    $args = [object[]]@([int]$offset, [uint32]0)
    $ok = $mTryRead.Invoke($o, $args)
    $hdr = [uint32]$args[1]
    if (-not $ok) { "  (read failed at 0x{0:X3})" -f $offset; break }
    if ($hdr -eq 0 -or $hdr -eq 0xFFFFFFFF) { "  (end at 0x{0:X3}: hdr=0x{1:X8})" -f $offset, $hdr; break }
    $capId  = [int]($hdr -band 0xFFFF)
    $capVer = [int](($hdr -shr 16) -band 0xF)
    $next   = [int](($hdr -shr 20) -band 0xFFC)
    $capName = switch ($capId) {
        0x000B { 'VSEC' }
        0x000E { 'ARI' }
        0x0023 { 'TPMI' }
        0x0024 { 'EA' }
        default { 'unknown' }
    }
    "  off=0x{0:X3}  cap_id=0x{1:X4} ({2})  ver={3}  next=0x{4:X3}" -f $offset, $capId, $capName, $capVer, $next
    if ($capId -eq 0x000B -or $capId -eq 0x0023) {
        $args2 = [object[]]@([int]($offset + 4), [uint32]0)
        $ok2 = $mTryRead.Invoke($o, $args2)
        $vsecHdr = [uint32]$args2[1]
        $vsecId  = [int]($vsecHdr -band 0xFFFF)
        $vsecRev = [int](($vsecHdr -shr 16) -band 0xF)
        $vsecLen = [int](($vsecHdr -shr 20) -band 0xFFF)
        "          VSEC ID=0x{0:X4}  rev={1}  len={2}" -f $vsecId, $vsecRev, $vsecLen
        $args3 = [object[]]@([int]($offset + 8), [uint32]0)
        $ok3 = $mTryRead.Invoke($o, $args3)
        $entryHdr = [uint32]$args3[1]
        $tbir = [int]($entryHdr -band 0x7)
        $disc = [int]($entryHdr -band 0xFFFFFFF8)
        "          TBIR={0}  Discovery offset=0x{1:X8}" -f $tbir, $disc
    }
    if ($next -eq 0 -or $next -eq $offset) { break }
    $offset = $next
    $iter++
}
$tO.GetMethod('Close').Invoke($o, @()) | Out-Null
