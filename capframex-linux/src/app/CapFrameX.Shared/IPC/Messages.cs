using System.Runtime.InteropServices;

namespace CapFrameX.Shared.IPC;

/// <summary>
/// IPC Message types (must match daemon/common.h)
/// </summary>
public enum MessageType : uint
{
    GameStarted = 1,
    GameStopped = 2,
    StartCapture = 3,
    StopCapture = 4,
    FrametimeData = 5,
    Ping = 6,
    Pong = 7,
    ConfigUpdate = 8,
    StatusRequest = 9,
    StatusResponse = 10,
    LayerHello = 11,
    SwapchainCreated = 12,
    SwapchainDestroyed = 13,
    IgnoreListAdd = 14,
    IgnoreListRemove = 15,
    IgnoreListGet = 16,
    IgnoreListResponse = 17,
    IgnoreListUpdated = 18,
}

/// <summary>
/// IPC Message header (must match daemon/common.h)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MessageHeader
{
    public uint Type;
    public uint PayloadSize;
    public ulong Timestamp;
}

/// <summary>
/// Game detected payload (must match daemon/common.h)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public unsafe struct GameDetectedPayload
{
    public int Pid;
    public fixed byte GameName[256];
    public fixed byte ExePath[4096];
    public fixed byte Launcher[256];
}

/// <summary>
/// Frame data point for IPC (must match daemon/common.h)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct FrameDataPointIpc
{
    public ulong FrameNumber;
    public ulong TimestampNs;
    public float FrametimeMs;
    public float Fps;
    public int Pid;  // Source process ID
}

/// <summary>
/// Ignore list entry for IPC (must match daemon/common.h)
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Ansi)]
public unsafe struct IgnoreListEntry
{
    public fixed byte ProcessName[256];
}
