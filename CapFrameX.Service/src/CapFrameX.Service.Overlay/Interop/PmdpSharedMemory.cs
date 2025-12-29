using System.Runtime.InteropServices;

namespace CapFrameX.Service.Overlay.Interop;

/// <summary>
/// Status codes for the PresentMon data provider
/// </summary>
public static class PmdpStatus
{
    public const uint Ok = 0;
    public const uint InitFailed = 1;
    public const uint StartStreamFailed = 2;
    public const uint GetFrameDataFailed = 3;
}

/// <summary>
/// Signature values for shared memory
/// </summary>
public static class PmdpSignature
{
    /// <summary>
    /// 'PMDP' - Data provider's memory is initialized and contains valid data
    /// </summary>
    public const uint Valid = 0x50444D50; // 'PMDP' in little-endian

    /// <summary>
    /// Memory is marked for deallocation and no longer contains valid data
    /// </summary>
    public const uint Dead = 0xDEAD;
}

/// <summary>
/// Version constants for shared memory structure
/// </summary>
public static class PmdpVersion
{
    /// <summary>
    /// Version 1.0 - (major << 16) + minor
    /// </summary>
    public const uint V1_0 = 0x00010000;
}

/// <summary>
/// Shared memory constants
/// </summary>
public static class PmdpConstants
{
    /// <summary>
    /// Name of the shared memory section
    /// </summary>
    public const string SharedMemoryName = "PMDPSharedMemory";

    /// <summary>
    /// Size of the frame ring buffer array
    /// </summary>
    public const int FrameArraySize = 8192;

    /// <summary>
    /// Window name used for provider detection by OverlayEditor
    /// </summary>
    public const string ConnectWindowName = "CapFrameXDataProviderConnectWnd";
}

/// <summary>
/// Matches PMDP_SHARED_MEMORY header from PMDPSharedMemory.h
/// The actual frame array is managed separately due to its size
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct PmdpSharedMemoryHeader
{
    /// <summary>
    /// Signature allows applications to verify status of shared memory
    /// 'PMDP' (0x50444D50) - valid data
    /// 0xDEAD - marked for deallocation
    /// </summary>
    public uint Signature;

    /// <summary>
    /// Structure version ((major << 16) + minor)
    /// Must be set to 0x0001xxxx for v1.x structure
    /// </summary>
    public uint Version;

    /// <summary>
    /// Size of PM_FRAME_DATA for compatibility with future versions
    /// </summary>
    public uint FrameArrEntrySize;

    /// <summary>
    /// Offset of arrFrame array for compatibility with future versions
    /// </summary>
    public uint FrameArrOffset;

    /// <summary>
    /// Size of arrFrame array for compatibility with future versions
    /// </summary>
    public uint FrameArrSize;

    /// <summary>
    /// Total frame count in ring buffer array
    /// </summary>
    public uint FrameCount;

    /// <summary>
    /// Frame position in ring buffer array
    /// </summary>
    public uint FramePos;

    /// <summary>
    /// Current PMDP_STATUS_XXX status
    /// </summary>
    public uint Status;
}
