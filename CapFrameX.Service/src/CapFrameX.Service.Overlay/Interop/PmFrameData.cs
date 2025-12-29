using System.Runtime.InteropServices;

namespace CapFrameX.Service.Overlay.Interop;

/// <summary>
/// Matches PM_FRAME_DATA from PresentMonAPI.h
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public unsafe struct PmFrameData
{
    public const int MaxPmPath = 260;
    public const int MaxRuntimeLength = 7;
    public const int MaxPmFanCount = 5;
    public const int MaxPmPsuCount = 5;

    /// <summary>The name of the process that called Present()</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxPmPath)]
    public string Application;

    /// <summary>The process ID of the process that called Present()</summary>
    public uint ProcessId;

    /// <summary>The address of the swap chain that was presented into</summary>
    public ulong SwapChainAddress;

    /// <summary>The runtime used to present (e.g., D3D9 or DXGI)</summary>
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = MaxRuntimeLength)]
    public string Runtime;

    /// <summary>The sync interval provided by the application in the Present() call</summary>
    public int SyncInterval;

    /// <summary>Flags used in the Present() call</summary>
    public uint PresentFlags;

    /// <summary>Whether the frame was dropped (1) or displayed (0)</summary>
    public uint Dropped;

    /// <summary>The time of the Present() call, in seconds, relative to when PresentMon started recording</summary>
    public double TimeInSeconds;

    /// <summary>The time spent inside the Present() call, in milliseconds</summary>
    public double MsInPresentApi;

    /// <summary>The time between this Present() call and the previous one, in milliseconds</summary>
    public double MsBetweenPresents;

    /// <summary>Whether tearing is possible (1) or not (0)</summary>
    public uint AllowsTearing;

    /// <summary>The presentation mode used by the system for this Present()</summary>
    public PmPresentMode PresentMode;

    /// <summary>The time between the Present() call and when the GPU work completed, in milliseconds</summary>
    public double MsUntilRenderComplete;

    /// <summary>The time between the Present() call and when the frame was displayed, in milliseconds</summary>
    public double MsUntilDisplayed;

    /// <summary>How long the previous frame was displayed before this Present() was displayed, in milliseconds</summary>
    public double MsBetweenDisplayChange;

    /// <summary>The time between the Present() call and when the GPU work started, in milliseconds</summary>
    public double MsUntilRenderStart;

    /// <summary>The time of the Present() call, as a performance counter value</summary>
    public ulong QpcTime;

    /// <summary>The time between the Present() call and the earliest keyboard or mouse interaction</summary>
    public double MsSinceInput;

    /// <summary>The time that any GPU engine was active working on this frame, in milliseconds</summary>
    public double MsGpuActive;

    /// <summary>The time video encode/decode was active separate from the other engines in milliseconds</summary>
    public double MsGpuVideoActive;

    // Power telemetry
    public PmFrameDataOptDouble GpuPowerW;
    public PmFrameDataOptDouble GpuSustainedPowerLimitW;
    public PmFrameDataOptDouble GpuVoltageV;
    public PmFrameDataOptDouble GpuFrequencyMhz;
    public PmFrameDataOptDouble GpuTemperatureC;
    public PmFrameDataOptDouble GpuUtilization;
    public PmFrameDataOptDouble GpuRenderComputeUtilization;
    public PmFrameDataOptDouble GpuMediaUtilization;

    public PmFrameDataOptDouble VramPowerW;
    public PmFrameDataOptDouble VramVoltageV;
    public PmFrameDataOptDouble VramFrequencyMhz;
    public PmFrameDataOptDouble VramEffectiveFrequencyGbs;
    public PmFrameDataOptDouble VramTemperatureC;

    public fixed byte FanSpeedRpmData[MaxPmFanCount * 9]; // 5 * sizeof(PmFrameDataOptDouble) = 5 * 9 = 45 bytes

    public fixed byte PsuTypeData[MaxPmPsuCount * 5]; // 5 * sizeof(PmFrameDataOptPsuType) = 5 * 5 = 25 bytes
    public fixed byte PsuPowerData[MaxPmPsuCount * 9]; // 5 * sizeof(PmFrameDataOptDouble) = 5 * 9 = 45 bytes
    public fixed byte PsuVoltageData[MaxPmPsuCount * 9]; // 5 * sizeof(PmFrameDataOptDouble) = 5 * 9 = 45 bytes

    // GPU memory telemetry
    public PmFrameDataOptUInt64 GpuMemTotalSizeB;
    public PmFrameDataOptUInt64 GpuMemUsedB;
    public PmFrameDataOptUInt64 GpuMemMaxBandwidthBps;
    public PmFrameDataOptDouble GpuMemReadBandwidthBps;
    public PmFrameDataOptDouble GpuMemWriteBandwidthBps;

    // Throttling flags
    public PmFrameDataOptInt GpuPowerLimited;
    public PmFrameDataOptInt GpuTemperatureLimited;
    public PmFrameDataOptInt GpuCurrentLimited;
    public PmFrameDataOptInt GpuVoltageLimited;
    public PmFrameDataOptInt GpuUtilizationLimited;

    public PmFrameDataOptInt VramPowerLimited;
    public PmFrameDataOptInt VramTemperatureLimited;
    public PmFrameDataOptInt VramCurrentLimited;
    public PmFrameDataOptInt VramVoltageLimited;
    public PmFrameDataOptInt VramUtilizationLimited;

    // CPU Telemetry
    public PmFrameDataOptDouble CpuUtilization;
    public PmFrameDataOptDouble CpuPowerW;
    public PmFrameDataOptDouble CpuPowerLimitW;
    public PmFrameDataOptDouble CpuTemperatureC;
    public PmFrameDataOptDouble CpuFrequency;

    public void SetFanSpeedRpm(int index, double? value)
    {
        if (index < 0 || index >= MaxPmFanCount) return;
        var opt = PmFrameDataOptDouble.Create(value);
        fixed (byte* ptr = FanSpeedRpmData)
        {
            *(PmFrameDataOptDouble*)(ptr + index * sizeof(PmFrameDataOptDouble)) = opt;
        }
    }

    public void SetPsuType(int index, PmPsuType? value)
    {
        if (index < 0 || index >= MaxPmPsuCount) return;
        var opt = PmFrameDataOptPsuType.Create(value);
        fixed (byte* ptr = PsuTypeData)
        {
            *(PmFrameDataOptPsuType*)(ptr + index * sizeof(PmFrameDataOptPsuType)) = opt;
        }
    }

    public void SetPsuPower(int index, double? value)
    {
        if (index < 0 || index >= MaxPmPsuCount) return;
        var opt = PmFrameDataOptDouble.Create(value);
        fixed (byte* ptr = PsuPowerData)
        {
            *(PmFrameDataOptDouble*)(ptr + index * sizeof(PmFrameDataOptDouble)) = opt;
        }
    }

    public void SetPsuVoltage(int index, double? value)
    {
        if (index < 0 || index >= MaxPmPsuCount) return;
        var opt = PmFrameDataOptDouble.Create(value);
        fixed (byte* ptr = PsuVoltageData)
        {
            *(PmFrameDataOptDouble*)(ptr + index * sizeof(PmFrameDataOptDouble)) = opt;
        }
    }
}
