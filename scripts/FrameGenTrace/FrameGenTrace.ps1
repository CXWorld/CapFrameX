<#
.SYNOPSIS
    Records an ETW trace of a running game for the analysis of frame generation pacing, replays it
    with PresentMon and packs both into a zip on the desktop.

.DESCRIPTION
    Written for the report to AMD about display times with Driver-Assisted Frame Pacing (FSR Frame
    Generation, Adrenalin 26.10.1 and later): PresentMon shows the two frames of each pair almost at
    the same time, while a high-speed camera shows even pacing.

    1. Writes a Windows Performance Recorder profile (FrameGenTrace.wprp) with every provider
       PresentMon reads, so the trace replays in PresentMon (--etl_file), plus the DxgKrnl GPU
       scheduling, HAGS and flip events and kernel thread scheduling for a GPU timeline in WPA or
       GPUView.
    2. Waits for the start delay (switch to the game meanwhile), records for the given time, and
       beeps at the start and at the end.
    3. Replays the trace with PresentMon into a CSV and summarizes the busiest swapchain: frame
       types, present modes and how many displayed frames reached the screen within 1 ms of the
       previous one.
    4. Packs the trace, CSV, summary and profile into a zip on the desktop.

    Close overlays (CapFrameX, Intel PresentMon, RTSS) before recording: an overlay window above a
    fullscreen game can change how the game is presented.

    PresentMon is taken from the CapFrameX installation, or from a PresentMon-<version>-x64.exe
    next to this script. Recording needs administrator rights; the script asks for elevation.

.PARAMETER Seconds
    Length of the recording.

.PARAMETER StartDelaySeconds
    Time to switch to the game before the recording starts.

.PARAMETER Label
    Short tag for the file names, e.g. vsync-off or vsync-on.

.PARAMETER ExtraProviders
    Further ETW providers to record, by GUID or registered name, with all keywords at verbose level
    (for example a display driver provider a GPU vendor asks for).

.PARAMETER EtlFile
    Replays and summarizes an existing trace instead of recording one.

.PARAMETER PresentMonPath
    PresentMon console executable to use instead of searching for one.

.PARAMETER OutputDirectory
    Folder that receives the result folder and zip. Default: the desktop.

.PARAMETER NoPause
    Neither waits for Enter at the end nor opens Explorer.
#>
[CmdletBinding()]
param(
    [ValidateRange(5, 120)][int]$Seconds = 20,
    [ValidateRange(0, 120)][int]$StartDelaySeconds = 10,
    [ValidatePattern('^[\w.-]*$')][string]$Label = '',
    [string[]]$ExtraProviders = @(),
    [string]$EtlFile,
    [string]$PresentMonPath,
    [string]$OutputDirectory = [Environment]::GetFolderPath('Desktop'),
    [switch]$NoPause
)

$ErrorActionPreference = 'Stop'
$ScriptVersion = '1.0'
$Inv = [Globalization.CultureInfo]::InvariantCulture

function Exit-Trace([int]$code) {
    if (-not $NoPause) { Read-Host 'Press Enter to close' | Out-Null }
    exit $code
}

function Test-IsAdministrator {
    $principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# The kernel logger of a recording needs administrator rights; replaying a trace does not.
if (-not $EtlFile -and -not (Test-IsAdministrator)) {
    Write-Host 'Recording a trace needs administrator rights. Windows will ask for permission now.'
    $argList = @('-NoProfile', '-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"",
                 '-Seconds', $Seconds, '-StartDelaySeconds', $StartDelaySeconds,
                 '-OutputDirectory', "`"$OutputDirectory`"")
    if ($Label) { $argList += @('-Label', $Label) }
    if ($ExtraProviders.Count -gt 0) { $argList += @('-ExtraProviders', ($ExtraProviders -join ',')) }
    if ($PresentMonPath) { $argList += @('-PresentMonPath', "`"$PresentMonPath`"") }
    if ($NoPause) { $argList += '-NoPause' }
    try {
        Start-Process powershell.exe -Verb RunAs -ArgumentList $argList | Out-Null
    } catch {
        Write-Host "The recording was not started with administrator rights: $($_.Exception.Message)" -ForegroundColor Red
        Exit-Trace 1
    }
    exit 0
}

function Find-PresentMon {
    if ($PresentMonPath) {
        if (Test-Path -LiteralPath $PresentMonPath) { return (Resolve-Path -LiteralPath $PresentMonPath).Path }
        throw "PresentMon was not found at '$PresentMonPath'."
    }
    $folders = New-Object System.Collections.Generic.List[string]
    $folders.Add($PSScriptRoot)
    # The installer (1.9.1 and later) records its folder here, also for a custom location.
    foreach ($key in 'HKLM:\SOFTWARE\CapFrameX', 'HKCU:\Software\CapFrameX') {
        $installDir = (Get-ItemProperty $key -Name InstallDir -ErrorAction SilentlyContinue).InstallDir
        if ($installDir) { $folders.Add((Join-Path $installDir 'PresentMon')) }
    }
    foreach ($key in 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall',
                     'HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall') {
        foreach ($entry in (Get-ChildItem $key -ErrorAction SilentlyContinue)) {
            $p = Get-ItemProperty $entry.PSPath -ErrorAction SilentlyContinue
            if ($p -and $p.DisplayName -like 'CapFrameX*' -and $p.InstallLocation) {
                $folders.Add((Join-Path $p.InstallLocation 'PresentMon'))
            }
        }
    }
    foreach ($programFiles in @($env:ProgramFiles, ${env:ProgramFiles(x86)})) {
        if ($programFiles) { $folders.Add((Join-Path $programFiles 'CapFrameX\PresentMon')) }
    }
    foreach ($folder in $folders) {
        if (-not $folder -or -not (Test-Path -LiteralPath $folder)) { continue }
        # The console capture tool only: not Intel's GUI app (PresentMon.exe) or its service.
        $exe = Get-ChildItem -LiteralPath $folder -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Name -match '^(CX-)?PresentMon-\d[\d.]*-x64\.exe$' } |
            Sort-Object @{ Expression = { $_.Name -like 'CX-*' }; Descending = $true }, @{ Expression = 'LastWriteTime'; Descending = $true } |
            Select-Object -First 1
        if ($exe) { return $exe.FullName }
    }
    return $null
}

# ---------------------------------------------------------------------------------------------
# WPR profile. The event providers and keywords cover what PresentMon enables
# (PresentData/PresentMonTraceSession.cpp, EnableProvidersListing), so PresentMon can replay the
# trace. DxgKrnl adds GPU scheduling, the HAGS scheduling log and rundown for WPA/GPUView.
# ---------------------------------------------------------------------------------------------
$EventProviders = @(
    # Base | Profiler | Resource | GPUScheduler | HardwareSchedulingLog | Present
    @{ Id = 'DxgKrnl';        Name = '802EC45A-1E99-4B83-9920-87C98277BA9D'; Keywords = '0xC008043'; Level = 5; CaptureState = '0xC008043' },
    # Windows 7 era DxgKrnl events, enabled by PresentMon without a filter.
    @{ Id = 'DxgKrnlWin7';    Name = '65CD4C8A-0848-4583-92A0-31C0FBAF00C0'; Keywords = '0xFFFFFFFF'; Level = 4 },
    @{ Id = 'DXGI';           Name = 'CA11C036-0102-4A2D-A6AD-F03CFED5D3C9'; Keywords = '0xF'; Level = 5 },
    @{ Id = 'D3D9';           Name = '783ACA0A-790E-4D7F-8451-AA850511C6B9'; Keywords = '0x2'; Level = 5 },
    @{ Id = 'DwmCore';        Name = '9E9BBA3C-2E38-40CB-99F4-9E8281425164'; Keywords = '0x81'; Level = 5 },
    @{ Id = 'DwmCoreWin7';    Name = '8C9DD1AD-E6E5-4B07-B455-684A9D879900'; Keywords = '0xFFFFFFFF'; Level = 5 },
    @{ Id = 'Win32k';         Name = '8C416C79-D49B-4F01-A467-E56D3AA8234C'; Keywords = '0x1000'; Level = 4 },
    @{ Id = 'KernelProcess';  Name = '22FB2CD6-0E7B-422B-A0C7-2FAD1FD0E716'; Keywords = '0x10'; Level = 4; CaptureState = '0x10' },
    # Frame types (XeSS-FG, FSR Frame Generation 4.0.0 on DX12) and app timing.
    @{ Id = 'IntelPresentMon'; Name = 'ECAA4712-4644-442F-B94C-A32F6CF8A499'; Keywords = '0xFFFF'; Level = 5 },
    # NVIDIA flip metering and PC latency markers (games send the latter on every GPU).
    @{ Id = 'NvidiaDisplay';  Name = 'AE4F8626-8265-40D1-A70B-11B64240E8E9'; Keywords = '0xFFFFFFFFFFFFFFFF'; Level = 5 },
    @{ Id = 'NvidiaPcl';      Name = '0D216F06-82A6-4D49-BC4F-8F38AE56EFAB'; Keywords = '0xFFFFFFFFFFFFFFFF'; Level = 5 }
)

function New-TraceProfile([string]$path, [string[]]$extraProviders) {
    $providers = New-Object System.Collections.Generic.List[object]
    foreach ($p in $EventProviders) { $providers.Add($p) }
    $i = 0
    # A relaunch for elevation hands the list over as one comma-separated string.
    foreach ($name in @($extraProviders | ForEach-Object { $_ -split ',' })) {
        $name = $name.Trim().Trim('{', '}')
        if (-not $name) { continue }
        $i++
        $providers.Add(@{ Id = "Extra$i"; Name = $name; Keywords = '0xFFFFFFFFFFFFFFFF'; Level = 5 })
    }

    $sb = New-Object Text.StringBuilder
    [void]$sb.AppendLine('<?xml version="1.0" encoding="utf-8"?>')
    [void]$sb.AppendLine("<WindowsPerformanceRecorder Version=`"1.0`" Author=`"CapFrameX`" Comments=`"Frame generation pacing trace $ScriptVersion`">")
    [void]$sb.AppendLine('  <Profiles>')
    [void]$sb.AppendLine('    <SystemCollector Id="SC_CfxFrameGen" Name="NT Kernel Logger"><BufferSize Value="1024"/><Buffers Value="256"/></SystemCollector>')
    [void]$sb.AppendLine('    <EventCollector Id="EC_CfxFrameGen" Name="CapFrameX Frame Generation Trace"><BufferSize Value="1024"/><Buffers Value="512"/></EventCollector>')
    [void]$sb.AppendLine('    <SystemProvider Id="SP_CfxFrameGen">')
    [void]$sb.AppendLine('      <Keywords>')
    foreach ($k in 'ProcessThread', 'Loader', 'CSwitch', 'ReadyThread', 'DPC', 'Interrupt') {
        [void]$sb.AppendLine("        <Keyword Value=`"$k`"/>")
    }
    [void]$sb.AppendLine('      </Keywords>')
    [void]$sb.AppendLine('    </SystemProvider>')
    foreach ($p in $providers) {
        [void]$sb.AppendLine("    <EventProvider Id=`"EP_$($p.Id)`" Name=`"$([Security.SecurityElement]::Escape($p.Name))`" Level=`"$($p.Level)`">")
        [void]$sb.AppendLine("      <Keywords><Keyword Value=`"$($p.Keywords)`"/></Keywords>")
        if ($p.CaptureState) {
            [void]$sb.AppendLine("      <CaptureStateOnStart><Keyword Value=`"$($p.CaptureState)`"/></CaptureStateOnStart>")
        }
        [void]$sb.AppendLine('    </EventProvider>')
    }
    [void]$sb.AppendLine('    <Profile Id="CfxFrameGen.Verbose.File" Name="CfxFrameGen" Description="CapFrameX frame generation pacing" LoggingMode="File" DetailLevel="Verbose">')
    [void]$sb.AppendLine('      <Collectors>')
    [void]$sb.AppendLine('        <SystemCollectorId Value="SC_CfxFrameGen"><SystemProviderId Value="SP_CfxFrameGen"/></SystemCollectorId>')
    [void]$sb.AppendLine('        <EventCollectorId Value="EC_CfxFrameGen">')
    [void]$sb.AppendLine('          <EventProviders>')
    foreach ($p in $providers) { [void]$sb.AppendLine("            <EventProviderId Value=`"EP_$($p.Id)`"/>") }
    [void]$sb.AppendLine('          </EventProviders>')
    [void]$sb.AppendLine('        </EventCollectorId>')
    [void]$sb.AppendLine('      </Collectors>')
    [void]$sb.AppendLine('    </Profile>')
    [void]$sb.AppendLine('  </Profiles>')
    [void]$sb.AppendLine('</WindowsPerformanceRecorder>')
    [IO.File]::WriteAllText($path, $sb.ToString(), (New-Object Text.UTF8Encoding($false)))
}

function Invoke-Wpr([string[]]$arguments, [switch]$IgnoreFailure) {
    # Windows PowerShell turns redirected stderr of a native tool into errors, which 'Stop' would throw.
    $ErrorActionPreference = 'Continue'
    $output = & wpr.exe @arguments 2>&1 | Out-String
    if ($LASTEXITCODE -ne 0 -and -not $IgnoreFailure) {
        throw "wpr $($arguments[0]) failed (exit code $LASTEXITCODE): $($output.Trim())"
    }
    return $output
}

# ---------------------------------------------------------------------------------------------
# Summary of a PresentMon CSV
# ---------------------------------------------------------------------------------------------
function Get-Number([string]$text) {
    $value = 0.0
    if ([double]::TryParse($text, [Globalization.NumberStyles]::Float, $Inv, [ref]$value)) { return $value }
    return $null
}

function Get-Percentile($sorted, [double]$p) {
    if ($sorted.Count -eq 0) { return $null }
    $index = [Math]::Min($sorted.Count - 1, [Math]::Max(0, [int][Math]::Ceiling($p * $sorted.Count) - 1))
    return $sorted[$index]
}

function Format-Number($value, [string]$format = 'F2') {
    if ($null -eq $value) { return 'n/a' }
    return ([double]$value).ToString($format, $Inv)
}

function Format-Counts($rows, [string]$column) {
    $parts = foreach ($g in ($rows | Group-Object $column | Sort-Object Count -Descending)) {
        $name = $g.Name
        if (-not $name) { $name = '(empty)' }
        '{0} {1}' -f $name, $g.Count
    }
    return ($parts -join ', ')
}

function Get-TraceSummary([string]$csvPath) {
    $lines = New-Object System.Collections.Generic.List[string]
    $rows = @(Import-Csv -LiteralPath $csvPath)
    if ($rows.Count -eq 0) {
        $lines.Add('PresentMon found no presents in the trace.')
        return $lines
    }
    $columns = $rows[0].PSObject.Properties.Name
    foreach ($required in 'Application', 'ProcessID', 'SwapChainAddress', 'MsBetweenPresents', 'MsBetweenDisplayChange', 'MsUntilDisplayed') {
        if ($columns -notcontains $required) { throw "The PresentMon CSV has no column '$required'." }
    }

    # The busiest swapchain is the game.
    $chain = $rows | Group-Object Application, ProcessID, SwapChainAddress | Sort-Object Count -Descending | Select-Object -First 1
    $frames = @($chain.Group)
    $first = $frames[0]
    $lines.Add(('Target:          {0} (PID {1}), swapchain {2}, {3} presents' -f $first.Application, $first.ProcessID, $first.SwapChainAddress, $frames.Count))
    if ($columns -contains 'PresentRuntime') { $lines.Add("Runtime:         $(Format-Counts $frames 'PresentRuntime')") }
    if ($columns -contains 'PresentMode') { $lines.Add("Present modes:   $(Format-Counts $frames 'PresentMode')") }
    if ($columns -contains 'AllowsTearing') { $lines.Add("Allows tearing:  $(Format-Counts $frames 'AllowsTearing')") }
    if ($columns -contains 'SyncInterval') { $lines.Add("Sync interval:   $(Format-Counts $frames 'SyncInterval')") }
    if ($columns -contains 'FrameType') { $lines.Add("Frame types:     $(Format-Counts $frames 'FrameType')") }

    # PresentMon writes NA for MsUntilDisplayed when a frame was not displayed, and NA for
    # MsBetweenDisplayChange when the previous displayed frame has the same screen time.
    $displayed = 0
    $sameTime = 0
    $nearSameTime = 0
    $changes = New-Object System.Collections.Generic.List[double]
    $presents = New-Object System.Collections.Generic.List[double]
    $untilDisplayed = New-Object System.Collections.Generic.List[double]
    $flipDelays = 0
    $seenDisplayed = $false
    foreach ($row in $frames) {
        $between = Get-Number $row.MsBetweenPresents
        if ($null -ne $between) { $presents.Add($between) }
        $until = Get-Number $row.MsUntilDisplayed
        if ($null -eq $until) { continue }
        $displayed++
        $untilDisplayed.Add($until)
        if ($columns -contains 'MsFlipDelay' -and $null -ne (Get-Number $row.MsFlipDelay)) { $flipDelays++ }
        $change = Get-Number $row.MsBetweenDisplayChange
        if ($seenDisplayed) {
            if ($null -eq $change) {
                $sameTime++
                $changes.Add(0.0)
            } else {
                if ($change -lt 1.0) { $nearSameTime++ }
                $changes.Add($change)
            }
        }
        $seenDisplayed = $true
    }
    $lines.Add(('Displayed:       {0} of {1} presents ({2} not displayed)' -f $displayed, $frames.Count, ($frames.Count - $displayed)))

    $sortedChanges = @($changes | Sort-Object)
    $sortedPresents = @($presents | Sort-Object)
    $sortedUntil = @($untilDisplayed | Sort-Object)
    if ($sortedChanges.Count -gt 0) {
        $mean = ($sortedChanges | Measure-Object -Average).Average
        $lines.Add(('Display rate:    {0} fps (mean MsBetweenDisplayChange {1} ms)' -f (Format-Number (1000.0 / $mean) 'F1'), (Format-Number $mean)))
        $lines.Add(('MsBetweenDisplayChange: P5 {0}, median {1}, P95 {2} ms' -f
            (Format-Number (Get-Percentile $sortedChanges 0.05)), (Format-Number (Get-Percentile $sortedChanges 0.5)), (Format-Number (Get-Percentile $sortedChanges 0.95))))
        $closeTotal = $sameTime + $nearSameTime
        $lines.Add(('  displayed within 1 ms of the previous displayed frame: {0} of {1} ({2} %), {3} of them at the same screen time' -f
            $closeTotal, $sortedChanges.Count, (Format-Number (100.0 * $closeTotal / $sortedChanges.Count) 'F1'), $sameTime))
    }
    if ($sortedPresents.Count -gt 0) {
        $lines.Add(('MsBetweenPresents:      P5 {0}, median {1}, P95 {2} ms' -f
            (Format-Number (Get-Percentile $sortedPresents 0.05)), (Format-Number (Get-Percentile $sortedPresents 0.5)), (Format-Number (Get-Percentile $sortedPresents 0.95))))
    }
    if ($sortedUntil.Count -gt 0) {
        $lines.Add(('MsUntilDisplayed:       P5 {0}, median {1}, P95 {2} ms' -f
            (Format-Number (Get-Percentile $sortedUntil 0.05)), (Format-Number (Get-Percentile $sortedUntil 0.5)), (Format-Number (Get-Percentile $sortedUntil 0.95))))
    }
    if ($columns -contains 'MsFlipDelay') {
        $lines.Add("MsFlipDelay:     values on $flipDelays displayed frames (only a driver that reports its flip timing provides them)")
    }

    $others = @($rows | Group-Object Application, ProcessID, SwapChainAddress | Sort-Object Count -Descending | Select-Object -Skip 1 -First 5)
    if ($others.Count -gt 0) {
        $lines.Add('Other swapchains in the trace:')
        foreach ($o in $others) {
            $r = $o.Group[0]
            $lines.Add(('  {0} (PID {1}), swapchain {2}: {3} presents' -f $r.Application, $r.ProcessID, $r.SwapChainAddress, $o.Count))
        }
    }
    return $lines
}

function Add-SystemInfo($lines) {
    $os = Get-CimInstance Win32_OperatingSystem
    $cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion' -ErrorAction SilentlyContinue
    $build = $os.Version
    if ($cv -and $null -ne $cv.UBR) { $build = "$build.$($cv.UBR)" }
    $lines.Add("Windows:         $($os.Caption) $build")
    foreach ($gpu in @(Get-CimInstance Win32_VideoController | Where-Object { $_.Name })) {
        $mode = ''
        if ($gpu.CurrentHorizontalResolution) {
            $mode = ", {0}x{1} @ {2} Hz" -f $gpu.CurrentHorizontalResolution, $gpu.CurrentVerticalResolution, $gpu.CurrentRefreshRate
        }
        $lines.Add("GPU:             $($gpu.Name), driver $($gpu.DriverVersion)$mode")
    }
    # AMD writes the Adrenalin version into the display adapter's class key.
    $classKey = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}'
    foreach ($key in (Get-ChildItem $classKey -ErrorAction SilentlyContinue)) {
        $p = Get-ItemProperty $key.PSPath -ErrorAction SilentlyContinue
        if ($p -and $p.RadeonSoftwareVersion) { $lines.Add("Adrenalin:       $($p.RadeonSoftwareVersion) ($($p.DriverDesc))") }
    }
    $hwSchMode = (Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers' -Name HwSchMode -ErrorAction SilentlyContinue).HwSchMode
    $schedText = 'default'
    if ($hwSchMode -eq 2) { $schedText = 'on' } elseif ($hwSchMode -eq 1) { $schedText = 'off' }
    $lines.Add("HAGS:            $schedText")
    try {
        $monitors = @(Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorID -ErrorAction Stop | ForEach-Object {
            $decode = { param($codes) -join ($codes | Where-Object { $_ -ne 0 } | ForEach-Object { [char]$_ }) }
            ('{0} {1}' -f (& $decode $_.ManufacturerName), (& $decode $_.UserFriendlyName)).Trim()
        })
        if ($monitors.Count -gt 0) { $lines.Add("Monitors:        $($monitors -join ', ')") }
    } catch { }
}

# ---------------------------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------------------------
$summary = New-Object System.Collections.Generic.List[string]
$workDir = $null
try {
    $stamp = Get-Date -Format 'yyyy-MM-dd_HHmmss'
    $name = "CapFrameX-FrameGenTrace_$stamp"
    if ($Label) { $name = "${name}_$Label" }
    $workDir = Join-Path $OutputDirectory $name
    New-Item -ItemType Directory -Force -Path $workDir | Out-Null

    $summary.Add("CapFrameX frame generation trace $ScriptVersion - $(Get-Date -Format 'yyyy-MM-dd HH:mm')")
    if ($Label) { $summary.Add("Label:           $Label") }
    Add-SystemInfo $summary

    $presentMon = Find-PresentMon
    if ($presentMon) { $summary.Add("PresentMon:      $presentMon") }

    if ($EtlFile) {
        $etlPath = (Resolve-Path -LiteralPath $EtlFile).Path
        $summary.Add("Trace:           $etlPath (recorded earlier)")
    } else {
        $running = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
            $_.ProcessName -match '^(CapFrameX|PresentMon.*|CX-PresentMon.*|RTSS|MSIAfterburner)$' })
        if ($running.Count -gt 0) {
            $names = ($running | Select-Object -ExpandProperty ProcessName -Unique) -join ', '
            Write-Host "Running now: $names. Overlays can change how the game is presented; close them for a clean trace." -ForegroundColor Yellow
            $summary.Add("Running:         $names")
        }
        $profilePath = Join-Path $workDir 'FrameGenTrace.wprp'
        New-TraceProfile $profilePath $ExtraProviders
        $etlPath = Join-Path $workDir 'FrameGenTrace.etl'

        Write-Host ''
        Write-Host 'Start the game with frame generation, in fullscreen, and find a steady scene.'
        Write-Host "After you press Enter you have $StartDelaySeconds s to switch to the game. A beep marks the start"
        Write-Host "of the $Seconds s recording, two beeps its end. Do not switch windows while it records."
        Read-Host 'Press Enter to start' | Out-Null

        # A recording that an earlier run left behind would block the start.
        Invoke-Wpr @('-cancel') -IgnoreFailure | Out-Null
        for ($s = $StartDelaySeconds; $s -gt 0; $s--) {
            Write-Host -NoNewline "`rRecording starts in $s s "
            Start-Sleep -Seconds 1
        }
        Write-Host ''
        Invoke-Wpr @('-start', "$profilePath!CfxFrameGen", '-filemode') | Out-Null
        $startedAt = Get-Date
        [Console]::Beep(880, 200)
        Write-Host "Recording for $Seconds s ..."
        Start-Sleep -Seconds $Seconds
        [Console]::Beep(660, 150); [Console]::Beep(660, 150)
        Write-Host 'Saving the trace, this takes a while ...'
        Invoke-Wpr @('-stop', $etlPath, 'CapFrameX frame generation trace') | Out-Null
        $summary.Add(('Recorded:        {0} s from {1}' -f $Seconds, $startedAt.ToString('HH:mm:ss', $Inv)))
        $summary.Add(('Trace size:      {0} MB' -f (Format-Number ((Get-Item -LiteralPath $etlPath).Length / 1MB) 'F0')))
    }

    $summary.Add('')
    if ($presentMon) {
        Write-Host 'Replaying the trace with PresentMon ...'
        $csvPath = Join-Path $workDir 'PresentMon.csv'
        $pmArgs = @('--etl_file', "`"$etlPath`"", '--output_file', "`"$csvPath`"", '--no_track_input', '--qpc_time_ms',
                    '--track_frame_type', '--track_app_timing', '--write_display_metadata', '--track_pc_latency')
        $pmLog = Join-Path $workDir 'PresentMon.log'
        $proc = Start-Process -FilePath $presentMon -ArgumentList $pmArgs -NoNewWindow -Wait -PassThru `
            -RedirectStandardOutput $pmLog -RedirectStandardError "$pmLog.err"
        if (Test-Path -LiteralPath "$pmLog.err") {
            Get-Content -LiteralPath "$pmLog.err" | Add-Content -LiteralPath $pmLog
            Remove-Item -LiteralPath "$pmLog.err"
        }
        if ($proc.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $csvPath)) {
            $summary.Add("PresentMon could not replay the trace (exit code $($proc.ExitCode)), see PresentMon.log.")
        } else {
            $summary.Add('PresentMon replay of the trace (busiest swapchain):')
            foreach ($line in (Get-TraceSummary $csvPath)) { $summary.Add($line) }
        }
    } else {
        $summary.Add('No PresentMon console executable was found, so the trace was not replayed.')
    }

    $summaryPath = Join-Path $workDir 'FrameGenTrace-summary.txt'
    [IO.File]::WriteAllLines($summaryPath, $summary, (New-Object Text.UTF8Encoding($false)))
    Write-Host ''
    $summary | ForEach-Object { Write-Host $_ }

    if ($EtlFile) {
        Write-Host ''
        Write-Host "Results: $workDir" -ForegroundColor Cyan
    } else {
        Write-Host ''
        Write-Host 'Packing the zip ...'
        $zipPath = "$workDir.zip"
        Compress-Archive -Path (Join-Path $workDir '*') -DestinationPath $zipPath -Force
        Write-Host "Trace and summary: $zipPath" -ForegroundColor Cyan
        if (-not $NoPause) { Start-Process explorer.exe "/select,`"$zipPath`"" }
    }
    Exit-Trace 0
} catch {
    Write-Host ''
    Write-Host "The trace failed: $($_.Exception.Message)" -ForegroundColor Red
    if (-not $EtlFile) { Invoke-Wpr @('-cancel') -IgnoreFailure | Out-Null }
    if ($workDir -and (Test-Path -LiteralPath $workDir)) {
        $summary.Add('')
        $summary.Add("The trace failed: $($_.Exception.Message)")
        $summary.Add($_.ScriptStackTrace)
        [IO.File]::WriteAllLines((Join-Path $workDir 'FrameGenTrace-summary.txt'), $summary, (New-Object Text.UTF8Encoding($false)))
    }
    Exit-Trace 1
}
