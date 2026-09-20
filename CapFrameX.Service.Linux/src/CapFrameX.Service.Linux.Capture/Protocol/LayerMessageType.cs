namespace CapFrameX.Service.Linux.Capture.Protocol;

/// <summary>
/// Message types of the Unix socket protocol between the Vulkan layer and the service.
/// </summary>
/// <remarks>
/// The numbering is the one the existing Linux layer and daemon already speak, so the service can
/// stand in for the daemon before the layer is rebuilt. The values are fixed by
/// <c>common.h</c> in the native tree and must not be renumbered.
/// </remarks>
public enum LayerMessageType : uint
{
    /// <summary>Daemon or service to client: a game process was detected.</summary>
    GameStarted = 1,

    /// <summary>Daemon or service to client: a game process exited.</summary>
    GameStopped = 2,

    /// <summary>Client to service: start delivering frames of a process.</summary>
    StartCapture = 3,

    /// <summary>Client to service: stop delivering frames of a process.</summary>
    StopCapture = 4,

    /// <summary>Layer to service: one or more frame data points.</summary>
    FrametimeData = 5,

    /// <summary>Keepalive.</summary>
    Ping = 6,

    /// <summary>Keepalive response.</summary>
    Pong = 7,

    /// <summary>Configuration change for layer or service.</summary>
    ConfigUpdate = 8,

    /// <summary>Client to service: status request.</summary>
    StatusRequest = 9,

    /// <summary>Service to client: status response.</summary>
    StatusResponse = 10,

    /// <summary>Layer to service: the layer announces itself with process information.</summary>
    LayerHello = 11,

    /// <summary>Layer to service: a swap chain was created.</summary>
    SwapchainCreated = 12,

    /// <summary>Layer to service: a swap chain was destroyed.</summary>
    SwapchainDestroyed = 13,

    /// <summary>Client to service: add a process to the ignore list.</summary>
    IgnoreListAdd = 14,

    /// <summary>Client to service: remove a process from the ignore list.</summary>
    IgnoreListRemove = 15,

    /// <summary>Client to service: request the ignore list.</summary>
    IgnoreListGet = 16,

    /// <summary>Service to client: the ignore list.</summary>
    IgnoreListResponse = 17,

    /// <summary>Service to client: the ignore list changed.</summary>
    IgnoreListUpdated = 18,

    /// <summary>Service to client: game information changed.</summary>
    GameUpdated = 19,
}
