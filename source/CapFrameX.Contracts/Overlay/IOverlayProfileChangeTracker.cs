namespace CapFrameX.Contracts.Overlay
{
    public interface IOverlayProfileChangeTracker
    {
        bool HasPendingChanges { get; }

        void MarkPendingChanges();

        void ResetPendingChanges();
    }
}
