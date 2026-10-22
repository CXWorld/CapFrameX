<#
.SYNOPSIS
Measures the CPU load of CapFrameX while it reads PresentMon, polls sensors and drives the
overlay, without capturing.
.DESCRIPTION
Starts a copy of the built application in an isolated portable sandbox, once per scenario and
repetition, and measures the CPU time (kernel + user) and CPU cycles of CapFrameX.exe and of the
PresentMon process it runs. Nothing is ever captured: the measured state is CapFrameX idling in
the background with a game running, as it does while somebody plays.

Scenarios (see -ListScenarios):
  Idle                  CapFrameX runs, no game, no overlay renderer.
  PresentMon            The target (vkcube by default) runs, no overlay renderer: PresentMon
                        reading only.
  <Renderer>-Hidden     Renderer selected, overlay toggled off (ALT+O). The in-game renderer
                        still injects and follows the frames while hidden.
  <Renderer>-NoSensors  Overlay on, classic rows without hardware sensors: overlay pipeline and
                        renderer, no sensor polling.
  <Renderer>-Sensors    Overlay on with the configured rows, hardware sensors included.
  <Renderer>-Design     As -Sensors with the tile design -OverlayDesign (InGame, HookFree only).
<Renderer> is InGame, HookFree or Rtss. The differences between the scenarios give the share of
PresentMon reading, overlay and sensor reading; report.md lists them.

Every run gets a fresh copy of a configuration primed by one start of the app (defaults, or
-ConfigurationSource). The priming start also writes the overlay profile without hardware sensor
rows, through CapFrameX's own MCP endpoint. CapFrameX starts first, then the target, so that RTSS
and the Vulkan layer are in place before the game creates its swapchain.

Results go to <OutputDirectory>\<timestamp>: report.md, report.json, samples.csv and the
CapFrameX log of every run. Documentation: scripts\CPU_LOAD_TEST.md.

Requires Windows, an elevated shell (PresentMon uses ETW) and no running CapFrameX.
.EXAMPLE
.\scripts\Measure-CpuLoad.ps1
Measures all scenarios once with vkcube, 30 s warm-up and 60 s measurement each.
.EXAMPLE
.\scripts\Measure-CpuLoad.ps1 -Scenario Idle,PresentMon,HookFree-* -Repetitions 3
.EXAMPLE
.\scripts\Measure-CpuLoad.ps1 -Renderer HookFree -OverlayDesign 'FUSION*' -Settings @{ OSDRefreshPeriod = 250 }
.EXAMPLE
.\scripts\Measure-CpuLoad.ps1 -BaselineReport temp\cpu-load\20261022-101500\report.json
Compares the new results with an earlier report.
#>
[CmdletBinding()]
param(
    [string]$AppDirectory,
    [string]$OutputDirectory,
    [string[]]$Scenario = @('*'),
    [ValidateSet('InGame', 'HookFree', 'Rtss')]
    [string[]]$Renderer = @('InGame', 'HookFree', 'Rtss'),
    [string]$OverlayDesign,
    [ValidateRange(1, 50)]
    [int]$Repetitions = 1,
    [ValidateRange(0, 3600)]
    [int]$WarmupSeconds = 30,
    [ValidateRange(5, 7200)]
    [int]$DurationSeconds = 60,
    [ValidateRange(100, 60000)]
    [int]$SampleIntervalMilliseconds = 1000,
    [ValidateRange(0, 600)]
    [int]$CooldownSeconds = 5,
    [string]$TargetPath,
    [string]$TargetArguments = '--present_mode 2 --width 640 --height 480',
    [string]$TargetProcessName,
    [string]$ConfigurationSource,
    [hashtable]$Settings = @{},
    [switch]$ShowWindow,
    [ValidateRange(1, 65535)]
    [int]$Port = 1337,
    [string]$BaselineReport,
    [switch]$KeepRunData,
    [switch]$ListScenarios
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

# Windows PowerShell 5.1 serializes arrays that carry this type data as {"value": [...], "Count": n}.
if ($PSVersionTable.PSVersion.Major -lt 6)
{
    Remove-TypeData -TypeName System.Array -ErrorAction SilentlyContinue
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$invariantCulture = [Globalization.CultureInfo]::InvariantCulture
$utf8NoBom = New-Object System.Text.UTF8Encoding $false

# Keys that define a scenario. -Settings cannot change them.
$scenarioSettingKeys = @('IsOverlayActive', 'OverlayRenderer', 'EnableHookOverlay', 'EnableHookFreeOverlay',
    'ActiveOverlayDesignProfileId', 'WebservicePort', 'McpEnabled')
$presentMonProcessPattern = 'CX-PresentMon*'
$rtssProcessPatterns = @('RTSS', 'RTSSHooksLoader', 'RTSSHooksLoader64', 'EncoderServer', 'EncoderServer64')

#region Native helpers

$nativeSource = @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CapFrameXCpuLoad
{
    public sealed class ProcessSample
    {
        public int ProcessId;
        public bool Alive;
        public long CreationTime;
        public long KernelTime;
        public long UserTime;
        public ulong CycleTime;
        public long WorkingSet;
        public long PrivateBytes;
        public int HandleCount;
    }

    public sealed class ThreadSample
    {
        public int ThreadId;
        public long CreationTime;
        public long KernelTime;
        public long UserTime;
        public ulong CycleTime;
        public string Name;
        public string StartModule;
    }

    // C# 5 on purpose: Windows PowerShell 5.1 compiles this with the .NET Framework compiler.
    public static class NativeMethods
    {
        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessQueryLimitedInformation = 0x1000;
        private const uint ThreadQueryInformation = 0x0040;
        private const uint ThreadQueryLimitedInformation = 0x0800;
        private const uint WmClose = 0x0010;
        private const int ThreadQuerySetWin32StartAddress = 9;

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCountersEx
        {
            public uint Size;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize;
            public UIntPtr WorkingSetSize;
            public UIntPtr QuotaPeakPagedPoolUsage;
            public UIntPtr QuotaPagedPoolUsage;
            public UIntPtr QuotaPeakNonPagedPoolUsage;
            public UIntPtr QuotaNonPagedPoolUsage;
            public UIntPtr PagefileUsage;
            public UIntPtr PeakPagefileUsage;
            public UIntPtr PrivateUsage;
        }

        private sealed class ModuleRange
        {
            public ulong Start;
            public ulong End;
            public string Name;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenThread(uint access, bool inheritHandle, int threadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessTimes(IntPtr process, out long creation, out long exit, out long kernel, out long user);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetThreadTimes(IntPtr thread, out long creation, out long exit, out long kernel, out long user);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryProcessCycleTime(IntPtr process, out ulong cycleTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool QueryThreadCycleTime(IntPtr thread, out ulong cycleTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetProcessHandleCount(IntPtr process, out uint handleCount);

        [DllImport("kernel32.dll", EntryPoint = "K32GetProcessMemoryInfo", SetLastError = true)]
        private static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCountersEx counters, uint size);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        [DllImport("kernel32.dll")]
        private static extern int GetThreadDescription(IntPtr thread, out IntPtr description);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationThread(IntPtr thread, int informationClass, out IntPtr information, int length, IntPtr returnLength);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, StringBuilder text, int maxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        // Times are FILETIME values: 100 ns units, the creation time in UTC.
        public static ProcessSample SampleProcess(int processId)
        {
            ProcessSample sample = new ProcessSample();
            sample.ProcessId = processId;

            IntPtr process = OpenProcess(ProcessQueryLimitedInformation | ProcessVmRead, false, processId);
            if (process == IntPtr.Zero)
                process = OpenProcess(ProcessQueryLimitedInformation, false, processId);
            if (process == IntPtr.Zero)
                return sample;

            try
            {
                long creation, exit, kernel, user;
                if (!GetProcessTimes(process, out creation, out exit, out kernel, out user) || exit != 0)
                    return sample;

                sample.Alive = true;
                sample.CreationTime = creation;
                sample.KernelTime = kernel;
                sample.UserTime = user;

                ulong cycles;
                if (QueryProcessCycleTime(process, out cycles))
                    sample.CycleTime = cycles;

                ProcessMemoryCountersEx counters = new ProcessMemoryCountersEx();
                counters.Size = (uint)Marshal.SizeOf(typeof(ProcessMemoryCountersEx));
                if (GetProcessMemoryInfo(process, ref counters, counters.Size))
                {
                    sample.WorkingSet = (long)counters.WorkingSetSize.ToUInt64();
                    sample.PrivateBytes = (long)counters.PrivateUsage.ToUInt64();
                }

                uint handles;
                if (GetProcessHandleCount(process, out handles))
                    sample.HandleCount = (int)handles;

                return sample;
            }
            finally
            {
                CloseHandle(process);
            }
        }

        // Names come from the thread description (SetThreadDescription, also set by .NET for named
        // threads), else the module that contains the thread's start address is reported.
        public static ThreadSample[] SampleThreads(int processId)
        {
            List<ThreadSample> samples = new List<ThreadSample>();
            Process process;
            try
            {
                process = Process.GetProcessById(processId);
            }
            catch (ArgumentException)
            {
                return samples.ToArray();
            }

            using (process)
            {
                List<ModuleRange> modules = new List<ModuleRange>();
                try
                {
                    foreach (ProcessModule module in process.Modules)
                    {
                        ModuleRange range = new ModuleRange();
                        range.Start = (ulong)module.BaseAddress.ToInt64();
                        range.End = range.Start + (ulong)module.ModuleMemorySize;
                        range.Name = module.ModuleName;
                        modules.Add(range);
                    }
                }
                catch (Exception)
                {
                    // The breakdown then names threads by description only.
                }

                ProcessThreadCollection threads;
                try
                {
                    threads = process.Threads;
                }
                catch (Exception)
                {
                    return samples.ToArray();
                }

                foreach (ProcessThread thread in threads)
                {
                    int threadId = thread.Id;
                    IntPtr handle = OpenThread(ThreadQueryInformation | ThreadQueryLimitedInformation, false, threadId);
                    if (handle == IntPtr.Zero)
                        handle = OpenThread(ThreadQueryLimitedInformation, false, threadId);
                    if (handle == IntPtr.Zero)
                        continue;

                    try
                    {
                        long creation, exit, kernel, user;
                        if (!GetThreadTimes(handle, out creation, out exit, out kernel, out user))
                            continue;

                        ThreadSample sample = new ThreadSample();
                        sample.ThreadId = threadId;
                        sample.CreationTime = creation;
                        sample.KernelTime = kernel;
                        sample.UserTime = user;

                        ulong cycles;
                        if (QueryThreadCycleTime(handle, out cycles))
                            sample.CycleTime = cycles;

                        sample.Name = ReadThreadDescription(handle);

                        IntPtr startAddress;
                        if (NtQueryInformationThread(handle, ThreadQuerySetWin32StartAddress, out startAddress, IntPtr.Size, IntPtr.Zero) == 0)
                            sample.StartModule = FindModule(modules, (ulong)startAddress.ToInt64());

                        samples.Add(sample);
                    }
                    finally
                    {
                        CloseHandle(handle);
                    }
                }
            }

            return samples.ToArray();
        }

        public static long[] GetSystemTimesSnapshot()
        {
            long idle, kernel, user;
            if (!GetSystemTimes(out idle, out kernel, out user))
                return null;

            // The kernel time includes the idle time.
            return new long[] { idle, kernel, user };
        }

        // Posts WM_CLOSE to the matching top-level windows, hidden ones included: a tray-minimized
        // CapFrameX has no MainWindowHandle, so Process.CloseMainWindow cannot reach it.
        public static int PostCloseToWindows(int processId, string classPrefix, string titlePrefix)
        {
            List<IntPtr> windows = new List<IntPtr>();
            EnumWindows(delegate (IntPtr window, IntPtr parameter)
            {
                uint owner;
                GetWindowThreadProcessId(window, out owner);
                if (owner != (uint)processId)
                    return true;

                StringBuilder className = new StringBuilder(256);
                GetClassName(window, className, className.Capacity);
                StringBuilder title = new StringBuilder(256);
                GetWindowText(window, title, title.Capacity);

                if (className.ToString().StartsWith(classPrefix, StringComparison.Ordinal) &&
                    title.ToString().StartsWith(titlePrefix, StringComparison.Ordinal))
                {
                    windows.Add(window);
                }

                return true;
            }, IntPtr.Zero);

            foreach (IntPtr window in windows)
                PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);

            return windows.Count;
        }

        private static string ReadThreadDescription(IntPtr thread)
        {
            IntPtr description;
            try
            {
                if (GetThreadDescription(thread, out description) < 0 || description == IntPtr.Zero)
                    return null;
            }
            catch (EntryPointNotFoundException)
            {
                return null;
            }

            try
            {
                string text = Marshal.PtrToStringUni(description);
                return string.IsNullOrEmpty(text) ? null : text;
            }
            finally
            {
                LocalFree(description);
            }
        }

        private static string FindModule(List<ModuleRange> modules, ulong address)
        {
            foreach (ModuleRange module in modules)
            {
                if (address >= module.Start && address < module.End)
                    return module.Name;
            }

            return null;
        }
    }
}
'@

function Initialize-NativeMethods
{
    if (-not ('CapFrameXCpuLoad.NativeMethods' -as [type]))
    {
        Add-Type -TypeDefinition $nativeSource -Language CSharp
    }
}

#endregion

#region Formatting and statistics

function Format-Number
{
    param($Value, [int]$Digits = 2)

    if ($null -eq $Value) { return '' }
    $number = [double]$Value
    if ([double]::IsNaN($number) -or [double]::IsInfinity($number)) { return '' }
    return $number.ToString('F' + $Digits, $invariantCulture)
}

function Get-Percentile
{
    param([double[]]$Values, [double]$Percentile)

    if ($null -eq $Values -or $Values.Length -eq 0) { return $null }

    $sorted = [double[]]$Values.Clone()
    [Array]::Sort($sorted)
    $rank = ($Percentile / 100.0) * ($sorted.Length - 1)
    $lower = [int][Math]::Floor($rank)
    $upper = [int][Math]::Ceiling($rank)
    return $sorted[$lower] + ($sorted[$upper] - $sorted[$lower]) * ($rank - $lower)
}

# Mean, minimum and maximum of the per-run values of a scenario.
function Get-Spread
{
    param([double[]]$Values)

    if ($null -eq $Values -or $Values.Length -eq 0) { return $null }

    $sum = 0.0
    $minimum = [double]::MaxValue
    $maximum = [double]::MinValue
    foreach ($value in $Values)
    {
        $sum += $value
        if ($value -lt $minimum) { $minimum = $value }
        if ($value -gt $maximum) { $maximum = $value }
    }

    return [pscustomobject][ordered]@{
        Mean = $sum / $Values.Length
        Min = $minimum
        Max = $maximum
        Count = $Values.Length
    }
}

function Format-Spread
{
    param($Spread, [int]$Digits = 2)

    if ($null -eq $Spread) { return '' }
    $mean = Format-Number $Spread.Mean $Digits
    if ($Spread.Count -le 1) { return $mean }
    return '{0} ({1}-{2})' -f $mean, (Format-Number $Spread.Min $Digits), (Format-Number $Spread.Max $Digits)
}

function ConvertTo-MarkdownTable
{
    param([object[]]$Rows, [string[]]$Columns)

    $lines = New-Object 'System.Collections.Generic.List[string]'
    $lines.Add('| ' + ($Columns -join ' | ') + ' |')
    $lines.Add('|' + (($Columns | ForEach-Object { ' --- ' }) -join '|') + '|')
    foreach ($row in $Rows)
    {
        $cells = foreach ($column in $Columns)
        {
            $value = $row.$column
            if ($null -eq $value) { '' } else { ([string]$value).Replace('|', '\|') }
        }
        $lines.Add('| ' + ($cells -join ' | ') + ' |')
    }

    return ,$lines.ToArray()
}

function Get-PropertyValue
{
    param($Object, [string]$Name)

    if ($null -eq $Object) { return $null }
    if ($Object -is [Collections.IDictionary]) { return $Object[$Name] }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

# Get-Content attaches provider properties to the string; the files are read plainly instead.
function Read-TextFile
{
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    return [IO.File]::ReadAllText($Path)
}

#endregion

#region Scenarios and settings

function Get-ScenarioCatalog
{
    param([string[]]$Renderers, [string]$Design)

    # The baselines use the renderer Off: a selected renderer works even while the overlay is
    # toggled off (the in-game one injects and reads every frame), which the -Hidden scenarios show.
    $catalog = New-Object 'System.Collections.Generic.List[object]'
    $catalog.Add([pscustomobject][ordered]@{
        Name = 'Idle'; Target = $false; Overlay = $false; Renderer = 'Off'; Sensors = $true; Design = $null
        Description = 'CapFrameX running, no game, overlay renderer Off'
    })
    $catalog.Add([pscustomobject][ordered]@{
        Name = 'PresentMon'; Target = $true; Overlay = $false; Renderer = 'Off'; Sensors = $true; Design = $null
        Description = 'Game running, overlay renderer Off: PresentMon reading only'
    })

    foreach ($rendererName in $Renderers)
    {
        $catalog.Add([pscustomobject][ordered]@{
            Name = "$rendererName-Hidden"; Target = $true; Overlay = $false; Renderer = $rendererName; Sensors = $true; Design = $null
            Description = "$rendererName renderer selected, overlay toggled off (ALT+O)"
        })
        $catalog.Add([pscustomobject][ordered]@{
            Name = "$rendererName-NoSensors"; Target = $true; Overlay = $true; Renderer = $rendererName; Sensors = $false; Design = $null
            Description = "$rendererName overlay, classic rows without hardware sensors"
        })
        $catalog.Add([pscustomobject][ordered]@{
            Name = "$rendererName-Sensors"; Target = $true; Overlay = $true; Renderer = $rendererName; Sensors = $true; Design = $null
            Description = "$rendererName overlay, classic rows with hardware sensors"
        })
        if ($Design -and $rendererName -ne 'Rtss')
        {
            $catalog.Add([pscustomobject][ordered]@{
                Name = "$rendererName-Design"; Target = $true; Overlay = $true; Renderer = $rendererName; Sensors = $true; Design = $Design
                Description = "$rendererName overlay, tile design '$Design'"
            })
        }
    }

    return ,$catalog.ToArray()
}

function Select-Scenarios
{
    param([object[]]$Catalog, [string[]]$Patterns)

    foreach ($pattern in $Patterns)
    {
        if (-not ($Catalog | Where-Object { $_.Name -like $pattern }))
        {
            throw "Scenario '$pattern' matches nothing. Available: $(($Catalog | ForEach-Object Name) -join ', ')"
        }
    }

    # Catalog order, so that each scenario follows the one it is compared with.
    return ,@($Catalog | Where-Object {
        $name = $_.Name
        @($Patterns | Where-Object { $name -like $_ }).Count -gt 0
    })
}

# Settings every run shares: no update check or dialogs, nothing outside the run folder.
function Get-BaseSettings
{
    param([bool]$ShowMainWindow)

    return [ordered]@{
        StartMinimized = -not $ShowMainWindow
        MinimizeToTray = $true
        AutoUpdateCheckActive = $false
        SuppressFrameViewServiceWarning = $true
        ObservedDirectory = 'MyDocuments\CapFrameX\Captures'
        CaptureRootDirectory = 'MyDocuments\CapFrameX\Captures'
        ScreenshotDirectory = 'MyDocuments\CapFrameX\Screenshots'
        CloudDownloadDirectory = 'MyDocuments\CapFrameX\Captures\Cloud'
    }
}

# The renderer is stored with its two mirror flags; a flag that contradicts the stored renderer
# would win at load (it marks a rollback), so all three are written together.
function Get-ScenarioSettings
{
    param($ScenarioDefinition, [string]$DesignProfileId, [int]$WebservicePort, [bool]$EnableMcp)

    # A [string] parameter turns $null into ''; the classic layout is stored as null.
    $designId = $null
    if ($DesignProfileId) { $designId = $DesignProfileId }

    return [ordered]@{
        IsOverlayActive = [bool]$ScenarioDefinition.Overlay
        OverlayRenderer = [string]$ScenarioDefinition.Renderer
        EnableHookOverlay = $ScenarioDefinition.Renderer -eq 'InGame'
        EnableHookFreeOverlay = $ScenarioDefinition.Renderer -eq 'HookFree'
        ActiveOverlayDesignProfileId = $designId
        WebservicePort = [string]$WebservicePort
        McpEnabled = $EnableMcp
    }
}

function Merge-AppSettings
{
    param([string]$TemplateJson, [Collections.IDictionary[]]$Layers)

    $settings = $null
    if (-not [string]::IsNullOrWhiteSpace($TemplateJson))
    {
        $settings = $TemplateJson | ConvertFrom-Json
    }
    if ($null -eq $settings)
    {
        $settings = New-Object psobject
    }

    foreach ($layer in $Layers)
    {
        if ($null -eq $layer) { continue }
        foreach ($key in $layer.Keys)
        {
            $settings | Add-Member -MemberType NoteProperty -Name $key -Value $layer[$key] -Force
        }
    }

    return $settings | ConvertTo-Json -Depth 32
}

function Get-UserSettings
{
    param([hashtable]$UserSettings)

    $result = [ordered]@{}
    foreach ($key in $UserSettings.Keys)
    {
        if ($scenarioSettingKeys -contains $key)
        {
            Write-Warning "-Settings cannot change '$key': it defines the scenarios and is ignored."
            continue
        }
        $result[$key] = $UserSettings[$key]
    }
    return $result
}

#endregion

#region Measurement

function New-GroupState
{
    param([string]$Name)

    return [pscustomobject]@{
        Name = $Name
        Previous = @{}
        ProcessIds = New-Object 'System.Collections.Generic.HashSet[int]'
        CpuSeconds = 0.0
        KernelSeconds = 0.0
        Cycles = 0.0
        WindowSeconds = 0.0
        IntervalPercents = New-Object 'System.Collections.Generic.List[double]'
        WorkingSets = New-Object 'System.Collections.Generic.List[double]'
        PrivateBytes = New-Object 'System.Collections.Generic.List[double]'
        Handles = New-Object 'System.Collections.Generic.List[double]'
        LastPercent = $null
        LastKernelPercent = $null
        LastMegaCyclesPerSecond = $null
        LastWorkingSet = $null
        LastPrivateBytes = $null
    }
}

# Adds one sampling interval. A process that is new since the previous sample counts in full if it
# started within the interval; one that was running already but is seen for the first time (it
# matched only now) counts from the next interval on.
function Update-GroupState
{
    param($State, [int[]]$ProcessIds, [long]$IntervalStartFileTime, [double]$IntervalSeconds, [bool]$Record)

    $current = @{}
    $deltaTime = 0L
    $deltaKernel = 0L
    $deltaCycles = 0.0
    $workingSet = 0L
    $privateBytes = 0L
    $handles = 0L

    foreach ($processId in $ProcessIds)
    {
        $sample = [CapFrameXCpuLoad.NativeMethods]::SampleProcess($processId)
        if (-not $sample.Alive) { continue }

        $current[$processId] = $sample
        $workingSet += $sample.WorkingSet
        $privateBytes += $sample.PrivateBytes
        $handles += $sample.HandleCount

        $previous = $State.Previous[$processId]
        if ($null -ne $previous -and $previous.CreationTime -eq $sample.CreationTime)
        {
            $deltaTime += ($sample.KernelTime + $sample.UserTime) - ($previous.KernelTime + $previous.UserTime)
            $deltaKernel += $sample.KernelTime - $previous.KernelTime
            $deltaCycles += [double]$sample.CycleTime - [double]$previous.CycleTime
        }
        elseif ($sample.CreationTime -ge $IntervalStartFileTime)
        {
            $deltaTime += $sample.KernelTime + $sample.UserTime
            $deltaKernel += $sample.KernelTime
            $deltaCycles += [double]$sample.CycleTime
        }
    }

    $State.Previous = $current
    if (-not $Record) { return }

    foreach ($processId in $current.Keys) { $null = $State.ProcessIds.Add($processId) }

    $State.CpuSeconds += $deltaTime / 1e7
    $State.KernelSeconds += $deltaKernel / 1e7
    $State.Cycles += $deltaCycles
    $State.WindowSeconds += $IntervalSeconds

    $State.LastPercent = 100.0 * ($deltaTime / 1e7) / $IntervalSeconds
    $State.LastKernelPercent = 100.0 * ($deltaKernel / 1e7) / $IntervalSeconds
    $State.LastMegaCyclesPerSecond = $deltaCycles / 1e6 / $IntervalSeconds
    $State.IntervalPercents.Add($State.LastPercent)

    if ($current.Count -gt 0)
    {
        $State.LastWorkingSet = $workingSet / 1MB
        $State.LastPrivateBytes = $privateBytes / 1MB
        $State.WorkingSets.Add($State.LastWorkingSet)
        $State.PrivateBytes.Add($State.LastPrivateBytes)
        $State.Handles.Add([double]$handles)
    }
    else
    {
        $State.LastWorkingSet = $null
        $State.LastPrivateBytes = $null
    }
}

# CPU % is relative to one logical processor (100 % = one busy core), like Process Explorer.
function Get-GroupMetrics
{
    param($State)

    if ($State.WindowSeconds -le 0 -or $State.ProcessIds.Count -eq 0) { return $null }

    $intervals = $State.IntervalPercents.ToArray()
    $metrics = [ordered]@{
        CpuPercent = 100.0 * $State.CpuSeconds / $State.WindowSeconds
        KernelPercent = 100.0 * $State.KernelSeconds / $State.WindowSeconds
        P50Percent = Get-Percentile -Values $intervals -Percentile 50
        P95Percent = Get-Percentile -Values $intervals -Percentile 95
        MaxPercent = Get-Percentile -Values $intervals -Percentile 100
        MegaCyclesPerSecond = $State.Cycles / 1e6 / $State.WindowSeconds
        Processes = $State.ProcessIds.Count
    }

    if ($State.WorkingSets.Count -gt 0)
    {
        $metrics.WorkingSetMB = (Get-Spread -Values $State.WorkingSets.ToArray()).Mean
        $metrics.WorkingSetMaxMB = (Get-Spread -Values $State.WorkingSets.ToArray()).Max
        $metrics.PrivateMB = (Get-Spread -Values $State.PrivateBytes.ToArray()).Mean
        $metrics.Handles = (Get-Spread -Values $State.Handles.ToArray()).Mean
    }

    return [pscustomobject]$metrics
}

# Per-thread CPU of CapFrameX between two thread snapshots, grouped by thread name. Threads that
# ended within the window are missing; the process totals stay authoritative.
function Get-ThreadBreakdown
{
    param([object[]]$StartThreads, [object[]]$EndThreads, [long]$WindowStartFileTime, [double]$WindowSeconds)

    if ($WindowSeconds -le 0) { return ,@() }

    $start = @{}
    foreach ($thread in $StartThreads) { $start[[int]$thread.ThreadId] = $thread }

    $groups = @{}
    foreach ($thread in $EndThreads)
    {
        $before = $start[[int]$thread.ThreadId]
        if ($null -ne $before -and $before.CreationTime -eq $thread.CreationTime)
        {
            $time = ($thread.KernelTime + $thread.UserTime) - ($before.KernelTime + $before.UserTime)
            $cycles = [double]$thread.CycleTime - [double]$before.CycleTime
        }
        elseif ($thread.CreationTime -ge $WindowStartFileTime)
        {
            $time = $thread.KernelTime + $thread.UserTime
            $cycles = [double]$thread.CycleTime
        }
        else
        {
            continue
        }

        if ($thread.Name) { $label = $thread.Name }
        elseif ($thread.StartModule) { $label = "[$($thread.StartModule)]" }
        else { $label = '[unknown]' }

        if (-not $groups.ContainsKey($label))
        {
            $groups[$label] = [pscustomobject]@{ Name = $label; Threads = 0; Time = 0L; Cycles = 0.0 }
        }
        $group = $groups[$label]
        $group.Threads++
        $group.Time += $time
        $group.Cycles += $cycles
    }

    $result = foreach ($group in $groups.Values)
    {
        [pscustomobject][ordered]@{
            Name = $group.Name
            Threads = $group.Threads
            CpuPercent = 100.0 * ($group.Time / 1e7) / $WindowSeconds
            MegaCyclesPerSecond = $group.Cycles / 1e6 / $WindowSeconds
        }
    }

    return ,@($result | Sort-Object -Property @{ Expression = 'MegaCyclesPerSecond'; Descending = $true }, Name)
}

function Get-SystemBusyPercent
{
    param([long[]]$Previous, [long[]]$Current)

    if ($null -eq $Previous -or $null -eq $Current) { return $null }
    $idle = $Current[0] - $Previous[0]
    $total = ($Current[1] - $Previous[1]) + ($Current[2] - $Previous[2])
    if ($total -le 0) { return $null }
    return 100.0 * ($total - $idle) / $total
}

#endregion

#region Processes and HTTP

function Test-IsElevated
{
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    try
    {
        return (New-Object Security.Principal.WindowsPrincipal $identity).IsInRole(
            [Security.Principal.WindowsBuiltInRole]::Administrator)
    }
    finally
    {
        $identity.Dispose()
    }
}

# Process ids per group from one process enumeration. Patterns are wildcards on the process name.
function Get-ProcessIdsByPattern
{
    param([hashtable]$PatternsByGroup)

    $result = @{}
    foreach ($key in $PatternsByGroup.Keys) { $result[$key] = New-Object 'System.Collections.Generic.List[int]' }

    foreach ($process in [Diagnostics.Process]::GetProcesses())
    {
        try
        {
            foreach ($key in $PatternsByGroup.Keys)
            {
                foreach ($pattern in $PatternsByGroup[$key])
                {
                    if ($process.ProcessName -like $pattern)
                    {
                        $result[$key].Add($process.Id)
                        break
                    }
                }
            }
        }
        finally
        {
            $process.Dispose()
        }
    }

    return $result
}

function Test-ProcessRunning
{
    param([string[]]$Patterns)

    $ids = (Get-ProcessIdsByPattern -PatternsByGroup @{ Match = $Patterns })['Match']
    return $ids.Count -gt 0
}

function Stop-ProcessesByPattern
{
    param([string[]]$Patterns, [int[]]$Keep = @())

    $ids = (Get-ProcessIdsByPattern -PatternsByGroup @{ Match = $Patterns })['Match']
    foreach ($id in $ids)
    {
        if ($Keep -contains $id) { continue }
        try { Stop-Process -Id $id -Force -ErrorAction Stop } catch { }
    }
}

# The local API must never go through a configured proxy.
function Invoke-LocalHttp
{
    param([string]$Method, [string]$Url, [string]$Body, [int]$TimeoutMilliseconds = 5000)

    $request = [System.Net.HttpWebRequest]::Create($Url)
    $request.Method = $Method
    $request.Proxy = $null
    $request.Timeout = $TimeoutMilliseconds
    $request.ReadWriteTimeout = $TimeoutMilliseconds
    $request.Accept = 'application/json'

    # A [string] parameter turns $null into '', so an absent body is an empty one.
    if (-not [string]::IsNullOrEmpty($Body))
    {
        $bytes = $utf8NoBom.GetBytes($Body)
        $request.ContentType = 'application/json'
        $request.ContentLength = $bytes.Length
        $stream = $request.GetRequestStream()
        try { $stream.Write($bytes, 0, $bytes.Length) } finally { $stream.Dispose() }
    }

    $response = $request.GetResponse()
    try
    {
        $reader = New-Object System.IO.StreamReader($response.GetResponseStream(), [System.Text.Encoding]::UTF8)
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally
    {
        $response.Dispose()
    }
}

function Invoke-CapFrameXApi
{
    param([int]$ApiPort, [string]$Path)

    # Assigned first: Windows PowerShell 5.1 emits a JSON array as one object, the return unrolls it.
    $result = Invoke-LocalHttp -Method GET -Url "http://localhost:$ApiPort$Path" | ConvertFrom-Json
    return $result
}

# CapFrameX answers JSON-RPC tools/call without a session; every tool returns its result as JSON text.
function Invoke-McpTool
{
    param([int]$ApiPort, [string]$Name, [hashtable]$Arguments = @{})

    $request = [ordered]@{
        jsonrpc = '2.0'
        id = 1
        method = 'tools/call'
        params = [ordered]@{ name = $Name; arguments = $Arguments }
    }
    $response = Invoke-LocalHttp -Method POST -Url "http://localhost:$ApiPort/mcp" `
        -Body ($request | ConvertTo-Json -Depth 10 -Compress) -TimeoutMilliseconds 30000 | ConvertFrom-Json

    $errorObject = Get-PropertyValue $response 'error'
    if ($null -ne $errorObject) { throw "MCP $Name failed: $(Get-PropertyValue $errorObject 'message')" }

    $result = Get-PropertyValue $response 'result'
    $text = Get-PropertyValue (@(Get-PropertyValue $result 'content') | Select-Object -First 1) 'text'
    if (Get-PropertyValue $result 'isError') { throw "MCP $Name failed: $text" }
    if ([string]::IsNullOrEmpty($text)) { throw "MCP $Name returned no content." }
    return $text | ConvertFrom-Json
}

function Write-PortableConfiguration
{
    param([string]$AppPath, [string]$RunDirectory)

    $configuration = [ordered]@{
        portable = $true
        paths = [ordered]@{
            config = [IO.Path]::Combine($RunDirectory, 'Config')
            captures = [IO.Path]::Combine($RunDirectory, 'Captures')
            screenshots = [IO.Path]::Combine($RunDirectory, 'Screenshots')
            logs = [IO.Path]::Combine($RunDirectory, 'Logs')
            cloud = [IO.Path]::Combine($RunDirectory, 'Captures\Cloud')
        }
    }
    [IO.File]::WriteAllText([IO.Path]::Combine($AppPath, 'portable.json'),
        ($configuration | ConvertTo-Json -Depth 5), $utf8NoBom)
}

function Start-CapFrameX
{
    param([string]$AppPath)

    # PresentMon is started by its relative path, so the working directory must be the app folder.
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = [IO.Path]::Combine($AppPath, 'CapFrameX.exe')
    $startInfo.WorkingDirectory = $AppPath
    $startInfo.UseShellExecute = $false
    return [Diagnostics.Process]::Start($startInfo)
}

# Returns the port the API answers on. A busy port makes CapFrameX fall back to a random one,
# which it stores in AppSettings.json.
function Wait-CapFrameXApi
{
    param([Diagnostics.Process]$Process, [int]$ApiPort, [string]$SettingsPath, [int]$TimeoutSeconds = 120)

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $candidate = $ApiPort
    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds)
    {
        if ($Process.HasExited) { throw "CapFrameX exited during startup (exit code $($Process.ExitCode))." }

        try
        {
            $null = Invoke-CapFrameXApi -ApiPort $candidate -Path '/api/version'
            return $candidate
        }
        catch
        {
            if ($stopwatch.Elapsed.TotalSeconds -gt 30 -and (Test-Path -LiteralPath $SettingsPath))
            {
                try
                {
                    $storedPort = Get-PropertyValue (Read-TextFile $SettingsPath | ConvertFrom-Json) 'WebservicePort'
                    if ($storedPort) { $candidate = [int]$storedPort }
                }
                catch { }
            }
        }

        Start-Sleep -Milliseconds 1000
    }

    throw "The CapFrameX API did not answer within $TimeoutSeconds s (port $candidate)."
}

function Stop-CapFrameX
{
    param([Diagnostics.Process]$Process, [int]$TimeoutSeconds = 30)

    if ($null -eq $Process) { return $null }
    $result = 'exited'
    if (-not $Process.HasExited)
    {
        $closed = [CapFrameXCpuLoad.NativeMethods]::PostCloseToWindows($Process.Id, 'HwndWrapper[', 'CapFrameX')
        if ($closed -gt 0 -and $Process.WaitForExit($TimeoutSeconds * 1000))
        {
            $result = 'closed'
        }
        else
        {
            try { $Process.Kill() } catch { }
            $null = $Process.WaitForExit(10000)
            $result = 'killed'
        }
    }

    # PresentMon runs in a job that ends with CapFrameX; make sure no instance survives into the next run.
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    while ((Test-ProcessRunning -Patterns @($presentMonProcessPattern)) -and $stopwatch.Elapsed.TotalSeconds -lt 10)
    {
        Start-Sleep -Milliseconds 250
    }
    Stop-ProcessesByPattern -Patterns @($presentMonProcessPattern)
    $Process.Dispose()
    return $result
}

function Start-Target
{
    param([string]$Path, [string]$Arguments)

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $Path
    $startInfo.Arguments = $Arguments
    $startInfo.WorkingDirectory = [IO.Path]::GetDirectoryName($Path)
    $startInfo.UseShellExecute = $false
    $process = [Diagnostics.Process]::Start($startInfo)
    Start-Sleep -Milliseconds 1000
    if ($process.HasExited)
    {
        throw "The target exited immediately (exit code $($process.ExitCode)). vkcube needs a Vulkan-capable GPU."
    }
    return $process
}

function Stop-Target
{
    param([Diagnostics.Process]$Process)

    if ($null -eq $Process) { return }
    try
    {
        if (-not $Process.HasExited)
        {
            $null = $Process.CloseMainWindow()
            if (-not $Process.WaitForExit(3000))
            {
                $Process.Kill()
                $null = $Process.WaitForExit(5000)
            }
        }
    }
    catch { }
    $Process.Dispose()
}

# Waits until CapFrameX's process list (the overlay's candidates) contains the target.
function Wait-TargetDetected
{
    param([int]$ApiPort, [string]$Name, [Diagnostics.Process]$CapFrameX, [int]$TimeoutSeconds = 30)

    $pattern = [regex]::Escape($Name)
    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    $names = @()
    while ($stopwatch.Elapsed.TotalSeconds -lt $TimeoutSeconds)
    {
        if ($CapFrameX.HasExited) { throw 'CapFrameX exited while waiting for the target.' }
        try
        {
            $names = @(Invoke-CapFrameXApi -ApiPort $ApiPort -Path '/api/processes')
            if (@($names | Where-Object { $_ -match $pattern }).Count -gt 0)
            {
                return [pscustomobject]@{ Detected = $true; Processes = $names }
            }
        }
        catch { }
        Start-Sleep -Milliseconds 1000
    }

    return [pscustomobject]@{ Detected = $false; Processes = $names }
}

function Wait-WithCapFrameXAlive
{
    param([Diagnostics.Process]$CapFrameX, [int]$Seconds)

    $stopwatch = [Diagnostics.Stopwatch]::StartNew()
    while ($stopwatch.Elapsed.TotalSeconds -lt $Seconds)
    {
        if ($CapFrameX.HasExited) { throw "CapFrameX exited (exit code $($CapFrameX.ExitCode))." }
        Start-Sleep -Milliseconds ([Math]::Min(1000, [Math]::Max(1, [int](($Seconds - $stopwatch.Elapsed.TotalSeconds) * 1000))))
    }
}

#endregion

#region Environment

function Get-RtssInstallPath
{
    foreach ($key in @('HKLM:\SOFTWARE\WOW6432Node\Unwinder\RTSS', 'HKLM:\SOFTWARE\Unwinder\RTSS'))
    {
        try
        {
            $path = (Get-ItemProperty -LiteralPath $key -Name InstallPath -ErrorAction Stop).InstallPath
            if ($path -and (Test-Path -LiteralPath $path)) { return $path }
        }
        catch { }
    }
    return $null
}

function Get-VulkanLayerRegistrations
{
    $layers = @()
    try
    {
        $key = Get-Item -LiteralPath 'HKLM:\SOFTWARE\Khronos\Vulkan\ImplicitLayers' -ErrorAction Stop
        $layers = @($key.GetValueNames() | Where-Object { $_ -like '*cfx_osd_vklayer*' })
    }
    catch { }
    return ,$layers
}

function Get-EnvironmentInfo
{
    param([string]$AppPath, [string]$SourceAppPath)

    $info = [ordered]@{
        OperatingSystem = [Environment]::OSVersion.VersionString
        MemoryGB = $null
        Cpu = $null
        LogicalProcessors = [Environment]::ProcessorCount
        Gpus = @()
        PowerPlan = $null
        CapFrameXVersion = $null
        CapFrameXFileVersion = $null
        AppSource = $SourceAppPath
        PresentMon = @()
        GitCommit = $null
        RtssPath = $null
        RtssVersion = $null
        VulkanLayers = @()
        PowerShell = $PSVersionTable.PSVersion.ToString()
    }
    try
    {
        $os = Get-CimInstance Win32_OperatingSystem
        $info.OperatingSystem = "$($os.Caption) $($os.Version)"
        $info.MemoryGB = [Math]::Round($os.TotalVisibleMemorySize / 1MB, 1)
    }
    catch { }

    try { $info.Cpu = (@(Get-CimInstance Win32_Processor) | ForEach-Object { $_.Name.Trim() }) -join '; ' } catch { }
    try { $info.LogicalProcessors = [int](Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors } catch { }
    try { $info.Gpus = @(Get-CimInstance Win32_VideoController | ForEach-Object { $_.Name }) } catch { }
    try { $info.PowerPlan = ((powercfg /getactivescheme) -join ' ').Trim() } catch { }

    $executable = [IO.Path]::Combine($AppPath, 'CapFrameX.exe')
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo($executable)
    $info.CapFrameXVersion = $version.ProductVersion
    $info.CapFrameXFileVersion = $version.FileVersion
    $info.PresentMon = @(Get-ChildItem -LiteralPath ([IO.Path]::Combine($AppPath, 'PresentMon')) -Filter 'CX-PresentMon*.exe' -ErrorAction SilentlyContinue |
        ForEach-Object { $_.BaseName })

    try
    {
        $commit = (& git -C $repositoryRoot rev-parse --short HEAD 2>$null)
        $dirty = (& git -C $repositoryRoot status --porcelain --untracked-files=no 2>$null)
        if ($commit) { $info.GitCommit = if ($dirty) { "$commit (modified)" } else { [string]$commit } }
    }
    catch { }

    $rtssPath = Get-RtssInstallPath
    $info.RtssPath = $rtssPath
    if ($rtssPath -and (Test-Path -LiteralPath $rtssPath -PathType Leaf))
    {
        try { $info.RtssVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo($rtssPath).ProductVersion } catch { }
    }
    $info.VulkanLayers = Get-VulkanLayerRegistrations
    return [pscustomobject]$info
}

#endregion

#region Runs

function Copy-DirectoryContent
{
    param([string]$Source, [string]$Destination)

    $null = New-Item -ItemType Directory -Path $Destination -Force
    if (Test-Path -LiteralPath $Source)
    {
        Get-ChildItem -LiteralPath $Source -Force | Copy-Item -Destination $Destination -Recurse -Force
    }
}

function Sync-Sandbox
{
    param([string]$Source, [string]$Destination)

    $null = New-Item -ItemType Directory -Path $Destination -Force
    # /MIR keeps later runs fast; the portable data a development build may carry is left out.
    $excludedFolders = @('Portable', 'Logs', 'Updates') | ForEach-Object { [IO.Path]::Combine($Source, $_) }
    & robocopy $Source $Destination /MIR /XF portable.json /XD $excludedFolders /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit code $LASTEXITCODE while copying $Source." }
    $global:LASTEXITCODE = 0
}

function Get-OverlaySlot
{
    param([string]$SettingsJson)

    $slot = 0
    try
    {
        $value = Get-PropertyValue ($SettingsJson | ConvertFrom-Json) 'OverlayEntryConfigurationFile'
        if ($null -ne $value) { $slot = [int]$value }
    }
    catch { }
    return $slot
}

# One start of the app creates the configuration every run copies. It also stores the overlay
# profile without hardware sensor rows: the MCP endpoint hides them and saves the profile with
# CapFrameX's own serializer.
function Invoke-Priming
{
    param($Context)

    $primeDirectory = [IO.Path]::Combine($Context.ReportDirectory, 'prime')
    $configDirectory = [IO.Path]::Combine($primeDirectory, 'Config')
    $null = New-Item -ItemType Directory -Path $configDirectory -Force
    if ($Context.ConfigurationSource)
    {
        Copy-DirectoryContent -Source $Context.ConfigurationSource -Destination $configDirectory
    }

    $settingsPath = [IO.Path]::Combine($configDirectory, 'AppSettings.json')
    $templateJson = Read-TextFile $settingsPath
    # Any renderer but Rtss (which would launch RTSS) and Off (no overlay rows) will do.
    $primeScenario = [pscustomobject]@{ Overlay = $true; Renderer = 'InGame' }
    $json = Merge-AppSettings -TemplateJson $templateJson -Layers @(
        (Get-BaseSettings -ShowMainWindow $false),
        $Context.UserSettings,
        (Get-ScenarioSettings -ScenarioDefinition $primeScenario -DesignProfileId $null -WebservicePort $Context.Port -EnableMcp $true))
    [IO.File]::WriteAllText($settingsPath, $json, $utf8NoBom)

    $slot = Get-OverlaySlot -SettingsJson $json
    $overlayFileName = "OverlayEntryConfiguration_$slot.json"
    $overlayPath = [IO.Path]::Combine($configDirectory, $overlayFileName)
    $sensorsOverlay = $null
    if (Test-Path -LiteralPath $overlayPath)
    {
        $sensorsOverlay = [IO.Path]::Combine($primeDirectory, 'overlay-sensors.json')
        Copy-Item -LiteralPath $overlayPath -Destination $sensorsOverlay
    }

    Write-PortableConfiguration -AppPath $Context.AppPath -RunDirectory $primeDirectory
    Write-Host 'Priming: starting CapFrameX to create the configuration ...'
    $app = Start-CapFrameX -AppPath $Context.AppPath
    $warnings = New-Object 'System.Collections.Generic.List[string]'
    $hardwareRows = @()
    $noSensorsOverlay = $null
    try
    {
        $apiPort = Wait-CapFrameXApi -Process $app -ApiPort $Context.Port -SettingsPath $settingsPath

        # The overlay rows include the hardware sensors once the sensor service is up.
        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        $firstEntries = $null
        $entries = @()
        while ($stopwatch.Elapsed.TotalSeconds -lt 180)
        {
            if ($app.HasExited) { throw 'CapFrameX exited during priming.' }
            try { $entries = @((Invoke-McpTool -ApiPort $apiPort -Name 'cfx_get_overlay_entries').entries) } catch { $entries = @() }
            if ($entries.Count -gt 0 -and $null -eq $firstEntries) { $firstEntries = $stopwatch.Elapsed.TotalSeconds }
            if (@($entries | Where-Object { $_.identifier -match '^(/|pmcreader/)' }).Count -gt 0) { break }
            if ($null -ne $firstEntries -and $stopwatch.Elapsed.TotalSeconds - $firstEntries -gt 30) { break }
            Start-Sleep -Seconds 2
        }
        if ($entries.Count -eq 0) { throw 'CapFrameX reported no overlay rows within 180 s.' }

        $hardwareRows = @($entries | Where-Object { $_.showOnOverlay -and $_.identifier -match '^(/|pmcreader/)' })
        if ($hardwareRows.Count -eq 0)
        {
            $warnings.Add('The overlay profile shows no hardware sensors: the NoSensors and Sensors scenarios are the same.')
        }
        else
        {
            for ($index = 0; $index -lt $hardwareRows.Count; $index++)
            {
                $null = Invoke-McpTool -ApiPort $apiPort -Name 'cfx_set_overlay_entry' -Arguments @{
                    identifier = $hardwareRows[$index].identifier
                    showOnOverlay = $false
                    persist = ($index -eq $hardwareRows.Count - 1)
                }
            }

            $stopwatch.Restart()
            while (-not (Test-Path -LiteralPath $overlayPath) -and $stopwatch.Elapsed.TotalSeconds -lt 15)
            {
                Start-Sleep -Milliseconds 250
            }
            if (-not (Test-Path -LiteralPath $overlayPath)) { throw "CapFrameX did not write $overlayFileName." }
            Start-Sleep -Milliseconds 500
            $noSensorsOverlay = [IO.Path]::Combine($primeDirectory, 'overlay-nosensors.json')
            Copy-Item -LiteralPath $overlayPath -Destination $noSensorsOverlay
        }
        Write-Host ("Priming: {0} overlay rows, {1} of them hardware sensors." -f $entries.Count, $hardwareRows.Count)
    }
    finally
    {
        $null = Stop-CapFrameX -Process $app
    }

    return [pscustomobject]@{
        Directory = $primeDirectory
        ConfigDirectory = $configDirectory
        OverlayFileName = $overlayFileName
        SensorsOverlay = $sensorsOverlay
        NoSensorsOverlay = $noSensorsOverlay
        HardwareRows = @($hardwareRows | ForEach-Object {
            [pscustomobject]@{ Identifier = $_.identifier; Description = Get-PropertyValue $_ 'description' }
        })
        Warnings = $warnings.ToArray()
    }
}

function Resolve-OverlayDesign
{
    param([string]$ConfigDirectory, [string]$Design)

    $libraryPath = [IO.Path]::Combine($ConfigDirectory, 'OverlayDesigns\profiles.json')
    if (-not (Test-Path -LiteralPath $libraryPath)) { throw "No tile design library at $libraryPath." }

    $profiles = @((Read-TextFile $libraryPath | ConvertFrom-Json).Profiles)
    $found = @($profiles | Where-Object { $_.Id -eq $Design })
    if ($found.Count -eq 0) { $found = @($profiles | Where-Object { $_.Name -like $Design }) }
    if ($found.Count -ne 1)
    {
        throw "-OverlayDesign '$Design' matches $($found.Count) tile designs. Available: $(($profiles | ForEach-Object Name) -join ', ')"
    }
    return [pscustomobject]@{ Id = $found[0].Id; Name = $found[0].Name }
}

function Invoke-ScenarioRun
{
    param($Context, $ScenarioDefinition, [int]$Repetition, [int]$RunNumber)

    $runName = '{0:00}-{1}-r{2}' -f $RunNumber, $ScenarioDefinition.Name, $Repetition
    $runDirectory = [IO.Path]::Combine($Context.ReportDirectory, 'runs', $runName)
    $configDirectory = [IO.Path]::Combine($runDirectory, 'Config')
    Copy-DirectoryContent -Source $Context.Prime.ConfigDirectory -Destination $configDirectory

    # Overlay rows: the primed profile with or without the hardware sensor rows.
    $overlayPath = [IO.Path]::Combine($configDirectory, $Context.Prime.OverlayFileName)
    Remove-Item -LiteralPath $overlayPath -Force -ErrorAction SilentlyContinue
    $overlaySource = if ($ScenarioDefinition.Sensors) { $Context.Prime.SensorsOverlay } else { $Context.Prime.NoSensorsOverlay }
    if (-not $ScenarioDefinition.Sensors -and -not $overlaySource) { $overlaySource = $Context.Prime.SensorsOverlay }
    if ($overlaySource) { Copy-Item -LiteralPath $overlaySource -Destination $overlayPath }

    $designId = $null
    if ($ScenarioDefinition.Design) { $designId = $Context.Design.Id }
    $settingsPath = [IO.Path]::Combine($configDirectory, 'AppSettings.json')
    $json = Merge-AppSettings -TemplateJson (Read-TextFile $settingsPath) -Layers @(
        (Get-BaseSettings -ShowMainWindow $Context.ShowWindow),
        $Context.UserSettings,
        (Get-ScenarioSettings -ScenarioDefinition $ScenarioDefinition -DesignProfileId $designId -WebservicePort $Context.Port -EnableMcp $false))
    [IO.File]::WriteAllText($settingsPath, $json, $utf8NoBom)
    Write-PortableConfiguration -AppPath $Context.AppPath -RunDirectory $runDirectory

    $warnings = New-Object 'System.Collections.Generic.List[string]'
    $run = [ordered]@{
        Run = $runName
        Scenario = $ScenarioDefinition.Name
        Repetition = $Repetition
        Status = 'Ok'
        Error = $null
        StartedUtc = [DateTime]::UtcNow.ToString('o')
        Shutdown = $null
        TargetDetected = $null
        ListedProcesses = @()
        TargetFps = $null
        MeasuredSeconds = $null
        CapFrameX = $null
        PresentMon = $null
        Target = $null
        Rtss = $null
        SystemBusyPercent = $null
        Threads = @()
        ThreadCount = $null
        Warnings = $null
    }
    $samples = New-Object 'System.Collections.Generic.List[object]'

    $app = $null
    $target = $null
    try
    {
        $app = Start-CapFrameX -AppPath $Context.AppPath
        $apiPort = Wait-CapFrameXApi -Process $app -ApiPort $Context.Port -SettingsPath $settingsPath

        if ($ScenarioDefinition.Overlay -and $ScenarioDefinition.Renderer -eq 'Rtss')
        {
            # CapFrameX launches RTSS itself, but only while no game runs.
            $stopwatch = [Diagnostics.Stopwatch]::StartNew()
            while (-not (Test-ProcessRunning -Patterns @('RTSS')) -and $stopwatch.Elapsed.TotalSeconds -lt 30)
            {
                Start-Sleep -Milliseconds 500
            }
            if (-not (Test-ProcessRunning -Patterns @('RTSS'))) { $warnings.Add('RTSS did not start; the RTSS overlay is not drawn.') }
        }

        $targetPatterns = @()
        if ($ScenarioDefinition.Target)
        {
            if ($Context.AttachToTarget)
            {
                $targetPatterns = @($Context.TargetName)
                if (-not (Test-ProcessRunning -Patterns $targetPatterns)) { throw "The target process '$($Context.TargetName)' is not running." }
            }
            else
            {
                $target = Start-Target -Path $Context.TargetPath -Arguments $Context.TargetArguments
            }

            $detection = Wait-TargetDetected -ApiPort $apiPort -Name $Context.TargetName -CapFrameX $app
            $run.TargetDetected = $detection.Detected
            $run.ListedProcesses = @($detection.Processes)
            if (-not $detection.Detected) { $warnings.Add("CapFrameX did not list '$($Context.TargetName)': PresentMon sees no presents from it.") }
            $others = @($detection.Processes | Where-Object { $_ -notmatch [regex]::Escape($Context.TargetName) })
            if ($others.Count -gt 0) { $warnings.Add("CapFrameX also lists $($others -join ', '): the overlay may follow one of them.") }
        }
        else
        {
            try { $run.ListedProcesses = @(Invoke-CapFrameXApi -ApiPort $apiPort -Path '/api/processes') } catch { }
        }

        Write-Host ("  warm-up {0} s ..." -f $Context.WarmupSeconds)
        Wait-WithCapFrameXAlive -CapFrameX $app -Seconds $Context.WarmupSeconds

        $groups = [ordered]@{
            CapFrameX = New-GroupState 'CapFrameX'
            PresentMon = New-GroupState 'PresentMon'
            Target = New-GroupState 'Target'
            Rtss = New-GroupState 'Rtss'
        }
        $patterns = @{ PresentMon = @($presentMonProcessPattern); Rtss = $rtssProcessPatterns }
        if ($targetPatterns.Count -gt 0) { $patterns.Target = $targetPatterns }

        $resolveIds = {
            $byPattern = Get-ProcessIdsByPattern -PatternsByGroup $patterns
            $ids = @{
                CapFrameX = @($app.Id)
                PresentMon = $byPattern.PresentMon.ToArray()
                Rtss = $byPattern.Rtss.ToArray()
                Target = @()
            }
            if ($null -ne $target) { $ids.Target = @($target.Id) }
            elseif ($patterns.ContainsKey('Target')) { $ids.Target = $byPattern.Target.ToArray() }
            $ids
        }

        Write-Host ("  measuring {0} s ..." -f $Context.DurationSeconds)
        $intervalCount = [Math]::Max(1, [int][Math]::Round($Context.DurationSeconds * 1000.0 / $Context.SampleIntervalMilliseconds))
        $ids = & $resolveIds
        $windowStartFileTime = [DateTime]::UtcNow.ToFileTimeUtc()
        foreach ($key in $groups.Keys) { Update-GroupState -State $groups[$key] -ProcessIds $ids[$key] -IntervalStartFileTime $windowStartFileTime -IntervalSeconds 0 -Record $false }
        $systemPrevious = [CapFrameXCpuLoad.NativeMethods]::GetSystemTimesSnapshot()
        $systemFirst = $systemPrevious
        $threadStopwatch = [Diagnostics.Stopwatch]::StartNew()
        $threadsStart = [CapFrameXCpuLoad.NativeMethods]::SampleThreads($app.Id)
        $threadStartFileTime = [DateTime]::UtcNow.ToFileTimeUtc()
        $threadStopwatch.Restart()

        $stopwatch = [Diagnostics.Stopwatch]::StartNew()
        $lastElapsed = 0.0
        $intervalStartFileTime = $windowStartFileTime
        for ($index = 1; $index -le $intervalCount; $index++)
        {
            $wait = $index * $Context.SampleIntervalMilliseconds - $stopwatch.ElapsedMilliseconds
            if ($wait -gt 0) { Start-Sleep -Milliseconds $wait }
            if ($app.HasExited) { throw "CapFrameX exited during the measurement (exit code $($app.ExitCode))." }

            $elapsed = $stopwatch.Elapsed.TotalSeconds
            $intervalSeconds = $elapsed - $lastElapsed
            $lastElapsed = $elapsed
            $nowFileTime = [DateTime]::UtcNow.ToFileTimeUtc()
            $ids = & $resolveIds
            foreach ($key in $groups.Keys)
            {
                Update-GroupState -State $groups[$key] -ProcessIds $ids[$key] -IntervalStartFileTime $intervalStartFileTime -IntervalSeconds $intervalSeconds -Record $true
            }
            $intervalStartFileTime = $nowFileTime
            $systemCurrent = [CapFrameXCpuLoad.NativeMethods]::GetSystemTimesSnapshot()
            $systemBusy = Get-SystemBusyPercent -Previous $systemPrevious -Current $systemCurrent
            $systemPrevious = $systemCurrent

            $samples.Add([pscustomobject][ordered]@{
                Run = $runName
                Scenario = $ScenarioDefinition.Name
                Repetition = $Repetition
                Sample = $index
                ElapsedSeconds = $elapsed
                CapFrameXCpuPercent = $groups.CapFrameX.LastPercent
                CapFrameXKernelPercent = $groups.CapFrameX.LastKernelPercent
                CapFrameXMegaCyclesPerSecond = $groups.CapFrameX.LastMegaCyclesPerSecond
                CapFrameXWorkingSetMB = $groups.CapFrameX.LastWorkingSet
                CapFrameXPrivateMB = $groups.CapFrameX.LastPrivateBytes
                PresentMonCpuPercent = $groups.PresentMon.LastPercent
                PresentMonMegaCyclesPerSecond = $groups.PresentMon.LastMegaCyclesPerSecond
                TargetCpuPercent = $groups.Target.LastPercent
                RtssCpuPercent = $groups.Rtss.LastPercent
                SystemBusyPercent = $systemBusy
            })
        }

        $threadsEnd = [CapFrameXCpuLoad.NativeMethods]::SampleThreads($app.Id)
        $run.Threads = Get-ThreadBreakdown -StartThreads $threadsStart -EndThreads $threadsEnd `
            -WindowStartFileTime $threadStartFileTime -WindowSeconds $threadStopwatch.Elapsed.TotalSeconds
        $run.ThreadCount = @($threadsEnd).Count
        $run.MeasuredSeconds = $stopwatch.Elapsed.TotalSeconds
        $run.SystemBusyPercent = Get-SystemBusyPercent -Previous $systemFirst -Current $systemPrevious
        $run.CapFrameX = Get-GroupMetrics $groups.CapFrameX
        $run.PresentMon = Get-GroupMetrics $groups.PresentMon
        $run.Target = Get-GroupMetrics $groups.Target
        $run.Rtss = Get-GroupMetrics $groups.Rtss
        if ($null -eq $run.PresentMon) { $warnings.Add('No PresentMon process was found.') }

        if ($ScenarioDefinition.Target)
        {
            # The online metrics of the overlay target; NaN while CapFrameX has no frames for it.
            try
            {
                $fps = 0.0
                $value = @(Invoke-CapFrameXApi -ApiPort $apiPort -Path '/api/metrics?metricNames=Average')[0]
                if ([double]::TryParse([string]$value, [Globalization.NumberStyles]::Float, $invariantCulture, [ref]$fps) -and
                    -not [double]::IsNaN($fps) -and -not [double]::IsInfinity($fps))
                {
                    $run.TargetFps = $fps
                }
            }
            catch { }
        }
    }
    catch
    {
        $run.Status = 'Failed'
        $run.Error = $_.Exception.Message
        Write-Warning "$runName failed: $($_.Exception.Message)"
    }
    finally
    {
        if ($null -ne $target) { Stop-Target -Process $target }
        if ($null -ne $app) { $run.Shutdown = Stop-CapFrameX -Process $app }
        # An RTSS that CapFrameX launched would hook the targets of the following scenarios.
        Stop-ProcessesByPattern -Patterns $rtssProcessPatterns -Keep $Context.PreexistingRtssIds

        # Keep the logs and the settings the run used; the rest is a copy of the primed configuration.
        if (Test-Path -LiteralPath $settingsPath)
        {
            Copy-Item -LiteralPath $settingsPath -Destination ([IO.Path]::Combine($runDirectory, 'AppSettings.json')) -Force
        }
        if (-not $Context.KeepRunData)
        {
            foreach ($folder in @('Config', 'Captures', 'Screenshots'))
            {
                Remove-Item -LiteralPath ([IO.Path]::Combine($runDirectory, $folder)) -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }

    $run.Warnings = $warnings.ToArray()
    foreach ($warning in $warnings) { Write-Warning "${runName}: $warning" }
    return [pscustomobject]@{ Run = [pscustomobject]$run; Samples = $samples.ToArray() }
}

#endregion

#region Report

function Get-ScenarioSummaries
{
    param([object[]]$Catalog, [object[]]$Runs, [int]$LogicalProcessors)

    $summaries = foreach ($definition in $Catalog)
    {
        $scenarioRuns = @($Runs | Where-Object { $_.Scenario -eq $definition.Name -and $_.Status -eq 'Ok' -and $null -ne $_.CapFrameX })
        if ($scenarioRuns.Count -eq 0) { continue }

        $capframex = [double[]]@($scenarioRuns | ForEach-Object { $_.CapFrameX.CpuPercent })
        $presentMon = [double[]]@($scenarioRuns | ForEach-Object { if ($_.PresentMon) { $_.PresentMon.CpuPercent } else { 0.0 } })
        $total = New-Object 'System.Collections.Generic.List[double]'
        $cycles = New-Object 'System.Collections.Generic.List[double]'
        foreach ($run in $scenarioRuns)
        {
            $presentMonPercent = 0.0
            $presentMonCycles = 0.0
            if ($run.PresentMon) { $presentMonPercent = $run.PresentMon.CpuPercent; $presentMonCycles = $run.PresentMon.MegaCyclesPerSecond }
            $total.Add($run.CapFrameX.CpuPercent + $presentMonPercent)
            $cycles.Add($run.CapFrameX.MegaCyclesPerSecond + $presentMonCycles)
        }

        $optional = {
            param([scriptblock]$Selector)
            $values = @($scenarioRuns | ForEach-Object $Selector | Where-Object { $null -ne $_ })
            if ($values.Count -eq 0) { return $null }
            return Get-Spread -Values ([double[]]$values)
        }

        $totalSpread = Get-Spread -Values $total.ToArray()
        $threads = @{}
        foreach ($run in $scenarioRuns)
        {
            foreach ($thread in @($run.Threads))
            {
                if (-not $threads.ContainsKey($thread.Name)) { $threads[$thread.Name] = [pscustomobject]@{ Name = $thread.Name; Threads = 0.0; CpuPercent = 0.0; MegaCyclesPerSecond = 0.0 } }
                $threads[$thread.Name].Threads += $thread.Threads / $scenarioRuns.Count
                $threads[$thread.Name].CpuPercent += $thread.CpuPercent / $scenarioRuns.Count
                $threads[$thread.Name].MegaCyclesPerSecond += $thread.MegaCyclesPerSecond / $scenarioRuns.Count
            }
        }

        [pscustomobject][ordered]@{
            Scenario = $definition.Name
            Description = $definition.Description
            Runs = $scenarioRuns.Count
            CapFrameXCpuPercent = Get-Spread -Values $capframex
            CapFrameXP95Percent = & $optional { $_.CapFrameX.P95Percent }
            CapFrameXKernelPercent = & $optional { $_.CapFrameX.KernelPercent }
            PresentMonCpuPercent = Get-Spread -Values $presentMon
            TotalCpuPercent = $totalSpread
            TotalSystemPercent = $totalSpread.Mean / [Math]::Max(1, $LogicalProcessors)
            TotalMegaCyclesPerSecond = Get-Spread -Values $cycles.ToArray()
            CapFrameXWorkingSetMB = & $optional { $_.CapFrameX.WorkingSetMB }
            CapFrameXPrivateMB = & $optional { $_.CapFrameX.PrivateMB }
            CapFrameXThreads = & $optional { $_.ThreadCount }
            TargetFps = & $optional { $_.TargetFps }
            TargetCpuPercent = & $optional { if ($_.Target) { $_.Target.CpuPercent } }
            RtssCpuPercent = & $optional { if ($_.Rtss) { $_.Rtss.CpuPercent } }
            SystemBusyPercent = & $optional { $_.SystemBusyPercent }
            Threads = @($threads.Values | Sort-Object -Property @{ Expression = 'MegaCyclesPerSecond'; Descending = $true }, Name)
        }
    }

    return ,@($summaries)
}

function Get-Contributions
{
    param([object[]]$Summaries, [string[]]$Renderers, [string]$DesignName)

    $definitions = New-Object 'System.Collections.Generic.List[object]'
    $definitions.Add(@('PresentMon reading (game running)', 'Idle', 'PresentMon'))
    foreach ($rendererName in $Renderers)
    {
        $definitions.Add(@("$rendererName renderer, overlay hidden", 'PresentMon', "$rendererName-Hidden"))
        $definitions.Add(@("$rendererName overlay without sensors", 'PresentMon', "$rendererName-NoSensors"))
        $definitions.Add(@("Sensor reading ($rendererName overlay)", "$rendererName-NoSensors", "$rendererName-Sensors"))
        $definitions.Add(@("Tile design '$DesignName' ($rendererName)", "$rendererName-Sensors", "$rendererName-Design"))
    }

    $byName = @{}
    foreach ($summary in $Summaries) { $byName[$summary.Scenario] = $summary }

    $result = foreach ($definition in $definitions)
    {
        $from = $byName[$definition[1]]
        $to = $byName[$definition[2]]
        if ($null -eq $from -or $null -eq $to) { continue }

        [pscustomobject][ordered]@{
            Contribution = $definition[0]
            From = $definition[1]
            To = $definition[2]
            CapFrameXCpuPercent = $to.CapFrameXCpuPercent.Mean - $from.CapFrameXCpuPercent.Mean
            PresentMonCpuPercent = $to.PresentMonCpuPercent.Mean - $from.PresentMonCpuPercent.Mean
            TotalCpuPercent = $to.TotalCpuPercent.Mean - $from.TotalCpuPercent.Mean
            TotalMegaCyclesPerSecond = $to.TotalMegaCyclesPerSecond.Mean - $from.TotalMegaCyclesPerSecond.Mean
        }
    }

    return ,@($result)
}

function Compare-WithBaseline
{
    param([object[]]$Summaries, $Baseline)

    $baselineScenarios = @{}
    foreach ($scenarioSummary in @(Get-PropertyValue $Baseline 'Scenarios')) { $baselineScenarios[$scenarioSummary.Scenario] = $scenarioSummary }

    $result = foreach ($summary in $Summaries)
    {
        $before = $baselineScenarios[$summary.Scenario]
        if ($null -eq $before) { continue }
        $beforeTotal = [double]$before.TotalCpuPercent.Mean
        $beforeCycles = [double]$before.TotalMegaCyclesPerSecond.Mean
        [pscustomobject][ordered]@{
            Scenario = $summary.Scenario
            BaselineTotalCpuPercent = $beforeTotal
            TotalCpuPercent = $summary.TotalCpuPercent.Mean
            DeltaCpuPercent = $summary.TotalCpuPercent.Mean - $beforeTotal
            DeltaRelativePercent = if ($beforeTotal -gt 0) { 100.0 * ($summary.TotalCpuPercent.Mean - $beforeTotal) / $beforeTotal } else { $null }
            BaselineMegaCyclesPerSecond = $beforeCycles
            DeltaMegaCyclesPerSecond = $summary.TotalMegaCyclesPerSecond.Mean - $beforeCycles
        }
    }

    return ,@($result)
}

function Get-SummaryRows
{
    param([object[]]$Summaries)

    return ,@($Summaries | ForEach-Object {
        [pscustomobject][ordered]@{
            'Scenario' = $_.Scenario
            'Runs' = $_.Runs
            'CapFrameX %' = Format-Spread $_.CapFrameXCpuPercent
            'CapFrameX p95 %' = Format-Number (Get-PropertyValue $_.CapFrameXP95Percent 'Mean')
            'PresentMon %' = Format-Spread $_.PresentMonCpuPercent
            'Total %' = Format-Spread $_.TotalCpuPercent
            'Total % of system' = Format-Number $_.TotalSystemPercent 3
            'Total Mcycles/s' = Format-Spread $_.TotalMegaCyclesPerSecond 0
            'Working set MB' = Format-Number (Get-PropertyValue $_.CapFrameXWorkingSetMB 'Mean') 0
            'Target FPS' = Format-Number (Get-PropertyValue $_.TargetFps 'Mean') 0
            'Target %' = Format-Number (Get-PropertyValue $_.TargetCpuPercent 'Mean')
            'RTSS %' = Format-Number (Get-PropertyValue $_.RtssCpuPercent 'Mean')
            'System busy %' = Format-Number (Get-PropertyValue $_.SystemBusyPercent 'Mean') 1
        }
    })
}

function Get-ContributionRows
{
    param([object[]]$Contributions)

    return ,@($Contributions | ForEach-Object {
        [pscustomobject][ordered]@{
            'Contribution' = $_.Contribution
            'Compared' = "$($_.To) - $($_.From)"
            'CapFrameX %' = Format-Number $_.CapFrameXCpuPercent
            'PresentMon %' = Format-Number $_.PresentMonCpuPercent
            'Total %' = Format-Number $_.TotalCpuPercent
            'Total Mcycles/s' = Format-Number $_.TotalMegaCyclesPerSecond 0
        }
    })
}

function Get-BaselineRows
{
    param([object[]]$Comparison)

    return ,@($Comparison | ForEach-Object {
        [pscustomobject][ordered]@{
            'Scenario' = $_.Scenario
            'Baseline total %' = Format-Number $_.BaselineTotalCpuPercent
            'Total %' = Format-Number $_.TotalCpuPercent
            'Delta %' = Format-Number $_.DeltaCpuPercent
            'Delta relative %' = Format-Number $_.DeltaRelativePercent 1
            'Delta Mcycles/s' = Format-Number $_.DeltaMegaCyclesPerSecond 0
        }
    })
}

function New-MarkdownReport
{
    param($Report, [int]$TopThreads = 12)

    $lines = New-Object 'System.Collections.Generic.List[string]'
    $environment = $Report.Environment
    $parameters = $Report.Parameters
    $lines.Add('# CapFrameX CPU load')
    $lines.Add('')
    $lines.Add("Measured $($Report.CreatedUtc) UTC, CapFrameX $($environment.CapFrameXVersion)" +
        $(if (Get-PropertyValue $environment 'GitCommit') { " (git $($environment.GitCommit))" } else { '' }) + '.')
    $lines.Add('')
    $lines.Add("- CPU: $($environment.Cpu), $($environment.LogicalProcessors) logical processors")
    $lines.Add("- GPU: $(@($environment.Gpus) -join '; ')")
    $lines.Add("- OS: $($environment.OperatingSystem); power plan: $($environment.PowerPlan)")
    $lines.Add("- PresentMon: $(@($environment.PresentMon) -join ', '); RTSS: $(if ($environment.RtssPath) { "$($environment.RtssVersion) at $($environment.RtssPath)" } else { 'not installed' })")
    $lines.Add("- Target: $($parameters.Target) $($parameters.TargetArguments)")
    $lines.Add("- Per run: $($parameters.WarmupSeconds) s warm-up, $($parameters.DurationSeconds) s measurement, sampled every $($parameters.SampleIntervalMilliseconds) ms; $($parameters.Repetitions) repetition(s); main window $(if ($parameters.ShowWindow) { 'shown' } else { 'in the tray' })")
    if (@($parameters.Settings.Keys).Count -gt 0)
    {
        $lines.Add("- Settings: $((@($parameters.Settings.Keys) | ForEach-Object { "$_=$($parameters.Settings[$_])" }) -join ', ')")
    }
    $lines.Add("- Hardware sensor rows hidden in the NoSensors scenarios: $(@($Report.Prime.HardwareRows).Count)")
    $lines.Add('')
    $lines.Add('CPU % is relative to one logical processor (100 % = one busy core); "Total" is CapFrameX.exe plus')
    $lines.Add('its PresentMon process, "% of system" divides it by the logical processors (Task Manager).')
    $lines.Add('Cycles do not depend on clock speed and are the more precise measure for small differences.')
    $lines.Add('With several repetitions a cell reads "mean (min-max)" over the runs.')
    $lines.Add('')
    $lines.Add('## Scenarios')
    $lines.Add('')
    $summaryRows = Get-SummaryRows $Report.Scenarios
    foreach ($line in (ConvertTo-MarkdownTable -Rows $summaryRows -Columns @($summaryRows[0].PSObject.Properties.Name))) { $lines.Add($line) }
    $lines.Add('')
    foreach ($summary in $Report.Scenarios) { $lines.Add("- **$($summary.Scenario)**: $($summary.Description)") }

    if (@($Report.Contributions).Count -gt 0)
    {
        $lines.Add('')
        $lines.Add('## Contributions')
        $lines.Add('')
        $lines.Add('Differences between the scenario means. Small values are within the run-to-run noise;')
        $lines.Add('repeat the measurement (-Repetitions) before reading much into them.')
        $lines.Add('')
        $contributionRows = Get-ContributionRows $Report.Contributions
        foreach ($line in (ConvertTo-MarkdownTable -Rows $contributionRows -Columns @($contributionRows[0].PSObject.Properties.Name))) { $lines.Add($line) }
    }

    if (@($Report.Baseline).Count -gt 0)
    {
        $lines.Add('')
        $lines.Add("## Compared with $($Report.BaselinePath)")
        $lines.Add('')
        $baselineRows = Get-BaselineRows $Report.Baseline
        foreach ($line in (ConvertTo-MarkdownTable -Rows $baselineRows -Columns @($baselineRows[0].PSObject.Properties.Name))) { $lines.Add($line) }
    }

    $lines.Add('')
    $lines.Add('## CapFrameX threads')
    $lines.Add('')
    $lines.Add('Threads grouped by name (thread description), else by the module of their start address.')
    $lines.Add('Threads that ended within the measurement are missing; the totals above are authoritative.')
    foreach ($summary in $Report.Scenarios)
    {
        $lines.Add('')
        $lines.Add("### $($summary.Scenario)")
        $lines.Add('')
        $threadRows = @($summary.Threads | Select-Object -First $TopThreads | ForEach-Object {
            [pscustomobject][ordered]@{
                'Thread' = $_.Name
                'Count' = Format-Number $_.Threads 0
                'CPU %' = Format-Number $_.CpuPercent
                'Mcycles/s' = Format-Number $_.MegaCyclesPerSecond 1
            }
        })
        if ($threadRows.Count -eq 0) { $lines.Add('No thread data.'); continue }
        foreach ($line in (ConvertTo-MarkdownTable -Rows $threadRows -Columns @('Thread', 'Count', 'CPU %', 'Mcycles/s'))) { $lines.Add($line) }
    }

    $runWarnings = @($Report.Runs | Where-Object { $_.Status -ne 'Ok' -or @($_.Warnings).Count -gt 0 })
    if ($runWarnings.Count -gt 0 -or @($Report.Warnings).Count -gt 0)
    {
        $lines.Add('')
        $lines.Add('## Warnings')
        $lines.Add('')
        foreach ($warning in @($Report.Warnings)) { $lines.Add("- $warning") }
        foreach ($run in $runWarnings)
        {
            if ($run.Status -ne 'Ok') { $lines.Add("- $($run.Run): $($run.Status) - $($run.Error)") }
            foreach ($warning in @($run.Warnings)) { $lines.Add("- $($run.Run): $warning") }
        }
    }

    return ,$lines.ToArray()
}

function Export-Samples
{
    param([object[]]$Samples, [string]$Path)

    $columns = @('Run', 'Scenario', 'Repetition', 'Sample', 'ElapsedSeconds', 'CapFrameXCpuPercent', 'CapFrameXKernelPercent',
        'CapFrameXMegaCyclesPerSecond', 'CapFrameXWorkingSetMB', 'CapFrameXPrivateMB', 'PresentMonCpuPercent',
        'PresentMonMegaCyclesPerSecond', 'TargetCpuPercent', 'RtssCpuPercent', 'SystemBusyPercent')
    $writer = New-Object System.IO.StreamWriter($Path, $false, $utf8NoBom)
    try
    {
        $writer.WriteLine($columns -join ',')
        foreach ($sample in $Samples)
        {
            $cells = foreach ($column in $columns)
            {
                $value = $sample.$column
                if ($null -eq $value) { '' }
                elseif ($value -is [double] -or $value -is [single]) { ([double]$value).ToString('0.####', $invariantCulture) }
                else { [string]$value }
            }
            $writer.WriteLine($cells -join ',')
        }
    }
    finally
    {
        $writer.Dispose()
    }
}

#endregion

function Invoke-CpuLoadMeasurement
{
    $catalog = Get-ScenarioCatalog -Renderers $Renderer -Design $OverlayDesign
    if ($ListScenarios)
    {
        $catalog | Format-Table Name, Description -AutoSize | Out-String -Width 200 | Write-Host
        return
    }
    $selected = Select-Scenarios -Catalog $catalog -Patterns $Scenario

    if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'The CPU load measurement runs on Windows only.' }
    if (-not (Test-IsElevated)) { throw 'Run the measurement from an elevated shell: PresentMon needs administrator rights for ETW.' }
    if (-not [Environment]::Is64BitProcess) { throw 'Run the measurement from a 64-bit PowerShell.' }
    Initialize-NativeMethods

    if (-not $AppDirectory) { $AppDirectory = [IO.Path]::Combine($repositoryRoot, 'source\CapFrameX\bin\x64\Release\net10.0-windows') }
    $sourceApp = (Resolve-Path -LiteralPath $AppDirectory -ErrorAction SilentlyContinue)
    if (-not $sourceApp -or -not (Test-Path -LiteralPath ([IO.Path]::Combine($sourceApp.Path, 'CapFrameX.exe'))))
    {
        throw "CapFrameX.exe not found in '$AppDirectory'. Build the application (Release|x64) or pass -AppDirectory."
    }
    $sourceApp = $sourceApp.Path
    if (-not (Get-ChildItem -LiteralPath ([IO.Path]::Combine($sourceApp, 'PresentMon')) -Filter 'CX-PresentMon*.exe' -ErrorAction SilentlyContinue))
    {
        throw "No PresentMon in '$sourceApp\PresentMon'."
    }

    if (Test-ProcessRunning -Patterns @('CapFrameX')) { throw 'CapFrameX is running. Close it first: only one instance can run.' }
    if (Test-ProcessRunning -Patterns @($presentMonProcessPattern)) { throw 'A CapFrameX PresentMon process is still running. End it first.' }
    if ($ConfigurationSource -and -not (Test-Path -LiteralPath $ConfigurationSource -PathType Container))
    {
        throw "-ConfigurationSource '$ConfigurationSource' is not a folder."
    }
    $listener = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
        Where-Object { $_.Port -eq $Port }
    if ($listener) { Write-Warning "Port $Port is in use; CapFrameX will fall back to a random port, which this script reads from its settings." }

    if (-not $OutputDirectory) { $OutputDirectory = [IO.Path]::Combine($repositoryRoot, 'temp\cpu-load') }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    $reportDirectory = [IO.Path]::Combine($OutputDirectory, (Get-Date -Format 'yyyyMMdd-HHmmss'))
    $null = New-Item -ItemType Directory -Path $reportDirectory -Force
    $appPath = [IO.Path]::Combine($OutputDirectory, 'sandbox\app')
    Write-Host "Copying $sourceApp to the sandbox $appPath ..."
    Sync-Sandbox -Source $sourceApp -Destination $appPath

    # Target: vkcube from the app output unless another one is given. A name alone attaches to a
    # process that is already running and stays running.
    $attach = $false
    if (-not $TargetPath -and $TargetProcessName)
    {
        $attach = $true
        $resolvedTarget = $null
        $targetName = $TargetProcessName
    }
    else
    {
        if (-not $TargetPath) { $TargetPath = [IO.Path]::Combine($appPath, '3d-test-app\vkcube.exe') }
        if (-not (Test-Path -LiteralPath $TargetPath)) { throw "Target '$TargetPath' not found." }
        $resolvedTarget = (Resolve-Path -LiteralPath $TargetPath).Path
        $targetName = if ($TargetProcessName) { $TargetProcessName } else { [IO.Path]::GetFileNameWithoutExtension($resolvedTarget) }
    }

    $warnings = New-Object 'System.Collections.Generic.List[string]'
    $environment = Get-EnvironmentInfo -AppPath $appPath -SourceAppPath $sourceApp
    $preexistingRtssIds = (Get-ProcessIdsByPattern -PatternsByGroup @{ Rtss = $rtssProcessPatterns })['Rtss'].ToArray()
    if (Test-ProcessRunning -Patterns @('RTSS'))
    {
        $warnings.Add('RTSS was running before the measurement and stays running; its hooks are present in every scenario.')
    }
    if (-not $environment.RtssPath -and @($selected | Where-Object { $_.Renderer -eq 'Rtss' -and $_.Overlay }).Count -gt 0)
    {
        $warnings.Add('RTSS is not installed: the Rtss scenarios are skipped.')
        $selected = @($selected | Where-Object { -not ($_.Renderer -eq 'Rtss' -and $_.Overlay) })
    }
    if (@($environment.VulkanLayers).Count -eq 0 -and @($selected | Where-Object { $_.Renderer -eq 'InGame' -and $_.Overlay }).Count -gt 0)
    {
        $warnings.Add('No CapFrameX Vulkan layer is registered (HKLM): the InGame overlay cannot draw into Vulkan targets such as vkcube.')
    }
    if ($selected.Count -eq 0) { throw 'No scenario left to measure.' }

    # Background load distorts the system-wide figures and, through clock and cache effects, the rest.
    $systemBefore = [CapFrameXCpuLoad.NativeMethods]::GetSystemTimesSnapshot()
    Start-Sleep -Seconds 3
    $idleBusy = Get-SystemBusyPercent -Previous $systemBefore -Current ([CapFrameXCpuLoad.NativeMethods]::GetSystemTimesSnapshot())
    if ($null -ne $idleBusy -and $idleBusy -gt 10)
    {
        $warnings.Add(('The system was {0} % busy before the measurement; close other applications for stable results.' -f (Format-Number $idleBusy 1)))
    }
    foreach ($warning in $warnings) { Write-Warning $warning }

    $userSettings = Get-UserSettings -UserSettings $Settings
    $context = [pscustomobject]@{
        ReportDirectory = $reportDirectory
        AppPath = $appPath
        Port = $Port
        ShowWindow = [bool]$ShowWindow
        UserSettings = $userSettings
        ConfigurationSource = $ConfigurationSource
        TargetPath = $resolvedTarget
        TargetArguments = $TargetArguments
        TargetName = $targetName
        AttachToTarget = $attach
        WarmupSeconds = $WarmupSeconds
        DurationSeconds = $DurationSeconds
        SampleIntervalMilliseconds = $SampleIntervalMilliseconds
        PreexistingRtssIds = $preexistingRtssIds
        KeepRunData = [bool]$KeepRunData
        Prime = $null
        Design = $null
    }

    $runSeconds = $WarmupSeconds + $DurationSeconds + $CooldownSeconds + 25
    Write-Host ("{0} scenario(s) x {1} repetition(s), about {2:N0} min. Report: {3}" -f
        $selected.Count, $Repetitions, ($selected.Count * $Repetitions * $runSeconds / 60.0 + 1), $reportDirectory)

    $context.Prime = Invoke-Priming -Context $context
    foreach ($warning in $context.Prime.Warnings) { $warnings.Add($warning); Write-Warning $warning }
    if ($OverlayDesign)
    {
        $context.Design = Resolve-OverlayDesign -ConfigDirectory $context.Prime.ConfigDirectory -Design $OverlayDesign
        Write-Host "Tile design: $($context.Design.Name) ($($context.Design.Id))"
    }

    $runs = New-Object 'System.Collections.Generic.List[object]'
    $allSamples = New-Object 'System.Collections.Generic.List[object]'
    $runNumber = 0
    # Repetitions outermost: drift (temperature, background activity) spreads over all scenarios.
    for ($repetition = 1; $repetition -le $Repetitions; $repetition++)
    {
        foreach ($definition in $selected)
        {
            $runNumber++
            Write-Host ("[{0}/{1}] {2} (repetition {3})" -f $runNumber, ($selected.Count * $Repetitions), $definition.Name, $repetition)
            $result = Invoke-ScenarioRun -Context $context -ScenarioDefinition $definition -Repetition $repetition -RunNumber $runNumber
            $runs.Add($result.Run)
            foreach ($sample in $result.Samples) { $allSamples.Add($sample) }
            if ($result.Run.Status -eq 'Ok' -and $null -ne $result.Run.CapFrameX)
            {
                $presentMonPercent = if ($result.Run.PresentMon) { $result.Run.PresentMon.CpuPercent } else { 0.0 }
                Write-Host ("  CapFrameX {0} %, PresentMon {1} % of one core" -f
                    (Format-Number $result.Run.CapFrameX.CpuPercent), (Format-Number $presentMonPercent))
            }
            if ($CooldownSeconds -gt 0) { Start-Sleep -Seconds $CooldownSeconds }
        }
    }

    $summaries = Get-ScenarioSummaries -Catalog $selected -Runs $runs.ToArray() -LogicalProcessors $environment.LogicalProcessors
    $designName = if ($context.Design) { $context.Design.Name } else { $OverlayDesign }
    $contributions = Get-Contributions -Summaries $summaries -Renderers $Renderer -DesignName $designName
    $baseline = @()
    if ($BaselineReport)
    {
        $baseline = Compare-WithBaseline -Summaries $summaries -Baseline (Read-TextFile $BaselineReport | ConvertFrom-Json)
    }

    $report = [pscustomobject][ordered]@{
        Schema = 1
        CreatedUtc = [DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss')
        Parameters = [pscustomobject][ordered]@{
            Scenarios = @($selected | ForEach-Object Name)
            Renderers = $Renderer
            OverlayDesign = $OverlayDesign
            Repetitions = $Repetitions
            WarmupSeconds = $WarmupSeconds
            DurationSeconds = $DurationSeconds
            SampleIntervalMilliseconds = $SampleIntervalMilliseconds
            Target = if ($attach) { "running process '$targetName'" } else { $resolvedTarget }
            TargetArguments = if ($attach) { '' } else { $TargetArguments }
            ConfigurationSource = $ConfigurationSource
            Settings = $userSettings
            ShowWindow = [bool]$ShowWindow
        }
        Environment = $environment
        Prime = [pscustomobject]@{ HardwareRows = $context.Prime.HardwareRows; Design = $context.Design }
        Warnings = $warnings.ToArray()
        Scenarios = $summaries
        Contributions = $contributions
        BaselinePath = $BaselineReport
        Baseline = $baseline
        Runs = $runs.ToArray()
    }

    [IO.File]::WriteAllText([IO.Path]::Combine($reportDirectory, 'report.json'), ($report | ConvertTo-Json -Depth 12), $utf8NoBom)
    Export-Samples -Samples $allSamples.ToArray() -Path ([IO.Path]::Combine($reportDirectory, 'samples.csv'))
    if (@($summaries).Count -eq 0) { throw "No run succeeded. See the warnings above and the logs in $reportDirectory\runs." }
    [IO.File]::WriteAllLines([IO.Path]::Combine($reportDirectory, 'report.md'), (New-MarkdownReport -Report $report), $utf8NoBom)

    Write-Host ''
    $tables = @((Get-SummaryRows $summaries), (Get-ContributionRows $contributions), (Get-BaselineRows $baseline))
    foreach ($rows in $tables)
    {
        if (@($rows).Count -gt 0) { $rows | Format-Table -AutoSize | Out-String -Width 400 | Write-Host }
    }
    Write-Host "Report: $([IO.Path]::Combine($reportDirectory, 'report.md'))"
}

Invoke-CpuLoadMeasurement
