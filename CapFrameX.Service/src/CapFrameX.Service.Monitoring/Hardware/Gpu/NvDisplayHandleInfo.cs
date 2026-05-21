using CapFrameX.Service.Monitoring.Interop;

namespace CapFrameX.Service.Monitoring.Hardware.Gpu;

internal readonly struct NvDisplayHandleInfo
{
    public NvDisplayHandleInfo(NvApi.NvDisplayHandle handle, string displayName)
    {
        Handle = handle;
        DisplayName = displayName;
    }

    public NvApi.NvDisplayHandle Handle { get; }
    public string DisplayName { get; }
}
