using System;
using System.Collections.Generic;

namespace CapFrameX.Contracts.Overlay
{
    /// <summary>
    /// What the UI shows about a learned in-game compatibility profile. The full entry lives
    /// in the OSD integration assembly; this is the renderer-independent summary.
    /// </summary>
    public sealed class HookLearnedProfileSummary
    {
        public HookLearnedProfileSummary(string executableName, string evidenceSignature,
            string stageName, bool verified, bool exhausted, string pendingStageName,
            DateTime updatedUtc)
        {
            ExecutableName = executableName;
            EvidenceSignature = evidenceSignature;
            StageName = stageName;
            Verified = verified;
            Exhausted = exhausted;
            PendingStageName = pendingStageName;
            UpdatedUtc = updatedUtc;
        }

        public string ExecutableName { get; }
        public string EvidenceSignature { get; }
        public string StageName { get; }
        public bool Verified { get; }
        public bool Exhausted { get; }
        /// <summary>The stage the next launch of this title starts with, if one is scheduled.</summary>
        public string PendingStageName { get; }
        public DateTime UpdatedUtc { get; }
    }

    public interface IHookLearnedProfileService
    {
        /// <summary>Entries for one executable (name without extension), newest first.</summary>
        IReadOnlyList<HookLearnedProfileSummary> GetForProcess(string executableName);

        IReadOnlyList<HookLearnedProfileSummary> GetAll();

        /// <summary>Fires after every change to the store.</summary>
        IObservable<int> Changes { get; }

        /// <summary>Forgets every learned profile; the next launch of each title probes again.</summary>
        void Reset();
    }

    /// <summary>Stand-in for builds without the in-game overlay: nothing learned, nothing to reset.</summary>
    public sealed class NullHookLearnedProfileService : IHookLearnedProfileService
    {
        public static readonly NullHookLearnedProfileService Instance =
            new NullHookLearnedProfileService();

        private NullHookLearnedProfileService()
        {
        }

        public IReadOnlyList<HookLearnedProfileSummary> GetForProcess(string executableName)
            => Array.Empty<HookLearnedProfileSummary>();

        public IReadOnlyList<HookLearnedProfileSummary> GetAll()
            => Array.Empty<HookLearnedProfileSummary>();

        public IObservable<int> Changes { get; } = new NeverObservable();

        public void Reset()
        {
        }

        private sealed class NeverObservable : IObservable<int>
        {
            public IDisposable Subscribe(IObserver<int> observer) => new NoopDisposable();

            private sealed class NoopDisposable : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}
