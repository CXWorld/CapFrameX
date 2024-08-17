namespace CapFrameX.Contracts.Logging
{
    public interface ILogEntry
    {
        string MessageInfo { get; set; }
        ELogMessageType MessageType { get; set; }
        string Message { get; set; }
    }
}