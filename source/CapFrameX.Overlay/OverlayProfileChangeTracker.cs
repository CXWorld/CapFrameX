using CapFrameX.Contracts.Overlay;
using System.Threading;

namespace CapFrameX.Overlay
{
    public sealed class OverlayProfileChangeTracker : IOverlayProfileChangeTracker
    {
        private int _hasPendingChanges;

        public bool HasPendingChanges => Volatile.Read(ref _hasPendingChanges) != 0;

        public void MarkPendingChanges()
            => Interlocked.Exchange(ref _hasPendingChanges, 1);

        public void ResetPendingChanges()
            => Interlocked.Exchange(ref _hasPendingChanges, 0);
    }
}
