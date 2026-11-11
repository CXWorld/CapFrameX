using System;
using System.Collections.Generic;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Probe sessions of injected processes that are not the overlay target right now. The
    /// manager observes only the selected process, and a resident hook gets no second plan, so a
    /// session dropped on a target switch was gone for the rest of that process's life: no
    /// verdict, no learning, no live escalation, and a pending restart forgotten. Parked here
    /// with the process's re-plan count, it returns when its process is selected again. Not
    /// thread-safe; the manager guards it with its state lock.
    /// </summary>
    internal sealed class HookProbeSessionShelf
    {
        private readonly Dictionary<int, (HookCompatibilityProbeSession Session, int RearmCount)> _parked =
            new Dictionary<int, (HookCompatibilityProbeSession Session, int RearmCount)>();

        internal int Count => _parked.Count;

        /// <summary>Parks <paramref name="session"/>, replacing an older one of its process.</summary>
        internal void Park(HookCompatibilityProbeSession session, int rearmCount)
        {
            if (session == null) return;
            _parked[session.ProcessId] = (session, rearmCount);
        }

        /// <summary>Removes and returns the session parked for <paramref name="processId"/>.</summary>
        internal bool TryTake(int processId, out HookCompatibilityProbeSession session,
            out int rearmCount)
        {
            if (_parked.TryGetValue(processId, out var parked))
            {
                _parked.Remove(processId);
                session = parked.Session;
                rearmCount = parked.RearmCount;
                return true;
            }
            session = null;
            rearmCount = 0;
            return false;
        }

        internal HookCompatibilityProbeSession Find(int processId)
            => _parked.TryGetValue(processId, out var parked) ? parked.Session : null;

        internal void Remove(int processId) => _parked.Remove(processId);

        /// <summary>Drops the sessions of processes that exited.</summary>
        internal void Prune(Func<int, bool> isAlive)
        {
            if (_parked.Count == 0) return;
            var exited = new List<int>();
            foreach (int processId in _parked.Keys)
                if (!isAlive(processId)) exited.Add(processId);
            foreach (int processId in exited) _parked.Remove(processId);
        }
    }
}
