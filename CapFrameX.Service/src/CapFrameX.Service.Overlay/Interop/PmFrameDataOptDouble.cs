using System.Runtime.InteropServices;

namespace CapFrameX.Service.Overlay.Interop;

/// <summary>
/// Matches PM_FRAME_DATA_OPT_DOUBLE from PresentMonAPI.h
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PmFrameDataOptDouble
{
    public double Data;
    [MarshalAs(UnmanagedType.U1)]
    public bool Valid;

    public static PmFrameDataOptDouble Create(double? value)
    {
        return new PmFrameDataOptDouble
        {
            Data = value ?? 0,
            Valid = value.HasValue
        };
    }
}

/// <summary>
/// Matches PM_FRAME_DATA_OPT_UINT64 from PresentMonAPI.h
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PmFrameDataOptUInt64
{
    public ulong Data;
    [MarshalAs(UnmanagedType.U1)]
    public bool Valid;

    public static PmFrameDataOptUInt64 Create(ulong? value)
    {
        return new PmFrameDataOptUInt64
        {
            Data = value ?? 0,
            Valid = value.HasValue
        };
    }
}

/// <summary>
/// Matches PM_FRAME_DATA_OPT_INT from PresentMonAPI.h
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PmFrameDataOptInt
{
    public int Data;
    [MarshalAs(UnmanagedType.U1)]
    public bool Valid;

    public static PmFrameDataOptInt Create(int? value)
    {
        return new PmFrameDataOptInt
        {
            Data = value ?? 0,
            Valid = value.HasValue
        };
    }
}

/// <summary>
/// Matches PM_PRESENT_MODE from PresentMonAPI.h
/// </summary>
public enum PmPresentMode : int
{
    HardwareLegacyFlip = 0,
    HardwareLegacyCopyToFrontBuffer,
    HardwareIndependentFlip,
    ComposedFlip,
    HardwareComposedIndependentFlip,
    ComposedCopyWithGpuGdi,
    ComposedCopyWithCpuGdi,
    Unknown
}

/// <summary>
/// Matches PM_PSU_TYPE from PresentMonAPI.h
/// </summary>
public enum PmPsuType : int
{
    None = 0,
    Pcie,
    Pin6,
    Pin8
}

/// <summary>
/// Matches PM_FRAME_DATA_OPT_PSU_TYPE from PresentMonAPI.h
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PmFrameDataOptPsuType
{
    public PmPsuType Data;
    [MarshalAs(UnmanagedType.U1)]
    public bool Valid;

    public static PmFrameDataOptPsuType Create(PmPsuType? value)
    {
        return new PmFrameDataOptPsuType
        {
            Data = value ?? PmPsuType.None,
            Valid = value.HasValue
        };
    }
}
