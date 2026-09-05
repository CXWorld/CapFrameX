namespace CapFrameX.Contracts.Latency
{
    public enum AmdFlmState
    {
        Disabled, Starting, WarmingUp, WaitingForClick, WaitingForResponse,
        SceneMoving, NoResponse, Measured, NoFrames, Error
    }

    public sealed class AmdFlmStatus
    {
        public AmdFlmState State { get; }
        public string Message { get; }
        public ulong Clicks { get; }
        public ulong RejectedClicks { get; }
        public ulong Timeouts { get; }
        public ulong Frames { get; }

        public AmdFlmStatus(AmdFlmState state, string message, ulong clicks = 0,
            ulong rejectedClicks = 0, ulong timeouts = 0, ulong frames = 0)
        {
            State = state;
            Message = message;
            Clicks = clicks;
            RejectedClicks = rejectedClicks;
            Timeouts = timeouts;
            Frames = frames;
        }
    }
}
