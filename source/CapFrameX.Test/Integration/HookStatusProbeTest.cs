using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.ExceptionServices;
using System.Threading;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    [DoNotParallelize]
    public class HookStatusProbeTest
    {
        private const long StatusSize = 64;
        private const long StatusSizeV2 = 128;
        private const int Magic = 0x31534843; // 'C''H''S''1'
        private static int _nextTestPid = 1600000000;

        [TestMethod]
        public void TryRead_MissingMappingReportsFalseWithoutFirstChanceException()
        {
            // Every published status polls this probe, and a status is published on each
            // transition — not just once per second. Throwing here floods the debugger.
            int pid = NextTestPid();
            int callingThread = Environment.CurrentManagedThreadId;
            int fileNotFoundExceptions = 0;
            EventHandler<FirstChanceExceptionEventArgs> handler = (_, args) =>
            {
                if (Environment.CurrentManagedThreadId == callingThread &&
                    args.Exception is FileNotFoundException)
                    fileNotFoundExceptions++;
            };

            bool success;
            string error;
            AppDomain.CurrentDomain.FirstChanceException += handler;
            try
            {
                success = HookStatusProbe.TryRead(pid, out _, out error);
            }
            finally
            {
                AppDomain.CurrentDomain.FirstChanceException -= handler;
            }

            Assert.IsFalse(success);
            Assert.IsNull(error);
            Assert.AreEqual(0, fileNotFoundExceptions);
        }

        [TestMethod]
        public void TryRead_ReadsExistingHookStatus()
        {
            int pid = NextTestPid();

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookStatusProbe.GetMappingName(pid), StatusSize,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StatusSize, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, Magic);
                view.Write(4, 1);      // version
                view.Write(8, pid);
                view.Write(12, unchecked((int)(uint)(NativeHookStatusFlags.Loaded |
                                                     NativeHookStatusFlags.HooksArmed |
                                                     NativeHookStatusFlags.PresentSeen |
                                                     NativeHookStatusFlags.EarlyInjectionRequired)));
                view.Write(16, 4242L); // last heartbeat
                view.Write(24, 4200L); // last state change
                view.Write(32, 0);     // last error
                view.Write(36, 3);     // steady refcount
                view.Write(40, 2);     // legacy release threshold
                view.Write(44, 17);    // metrics entries
                view.Write(48, 2560);  // resolution X
                view.Write(52, 1440);  // resolution Y
                view.Write(56, 2);     // API = D3D12
                view.Flush();

                bool success = HookStatusProbe.TryRead(
                    pid, out NativeHookStatusSnapshot snapshot, out string error);

                Assert.IsTrue(success);
                Assert.IsNull(error);
                Assert.AreEqual(NativeHookStatusFlags.Loaded | NativeHookStatusFlags.HooksArmed |
                    NativeHookStatusFlags.PresentSeen |
                    NativeHookStatusFlags.EarlyInjectionRequired, snapshot.Flags);
                Assert.AreEqual(4242L, snapshot.LastHeartbeatTickMs);
                Assert.AreEqual(4200L, snapshot.LastStateChangeTickMs);
                Assert.AreEqual(3, snapshot.SteadyRefcount);
                Assert.AreEqual(2, snapshot.ReleaseThreshold);
                Assert.AreEqual(17, snapshot.MetricsEntryCount);
                // Offsets 48/52 of the native block — the swapchain extent the capture file's
                // ResolutionInfo is written from. Reading them at the wrong offset would yield a
                // plausible-looking number, so pin the layout here.
                Assert.AreEqual(2560, snapshot.ResolutionX);
                Assert.AreEqual(1440, snapshot.ResolutionY);
                Assert.AreEqual(NativeHookApi.D3D12, snapshot.Api);
            }
        }

        /// <summary>
        /// A hook newer than this reader could publish an API id we have no name for. It must
        /// degrade to unknown instead of becoming a nonsense enum value in the capture file.
        /// </summary>
        [TestMethod]
        public void TryRead_ReportsAnUnrecognizedApiAsUnknown()
        {
            int pid = NextTestPid();

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookStatusProbe.GetMappingName(pid), StatusSize,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StatusSize, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, Magic);
                view.Write(4, 1);
                view.Write(8, pid);
                view.Write(12, unchecked((int)(uint)NativeHookStatusFlags.Loaded));
                view.Write(56, 99);
                view.Flush();

                Assert.IsTrue(HookStatusProbe.TryRead(
                    pid, out NativeHookStatusSnapshot snapshot, out _));
                Assert.AreEqual(NativeHookApi.Unknown, snapshot.Api);
                Assert.IsNull(HookOverlayStatusEvaluator.FormatApi(snapshot.Api));
            }
        }

        /// <summary>
        /// A hook build that predates the resolution fields leaves them as the zeroed reserved
        /// words it published instead. That must read as "unknown", never as a 0x0 resolution.
        /// </summary>
        [TestMethod]
        public void TryRead_ReportsAnOlderHooksReservedWordsAsUnknownResolution()
        {
            int pid = NextTestPid();

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookStatusProbe.GetMappingName(pid), StatusSize,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StatusSize, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, Magic);
                view.Write(4, 1);
                view.Write(8, pid);
                view.Write(12, unchecked((int)(uint)NativeHookStatusFlags.Loaded));
                view.Flush();

                Assert.IsTrue(HookStatusProbe.TryRead(
                    pid, out NativeHookStatusSnapshot snapshot, out _));
                Assert.AreEqual(0, snapshot.ResolutionX);
                Assert.AreEqual(0, snapshot.ResolutionY);
                Assert.IsNull(HookOverlayStatusEvaluator.FormatResolution(
                    snapshot.ResolutionX, snapshot.ResolutionY));
            }
        }

        /// <summary>
        /// Version 2 appends the compatibility-probing fields after the untouched 64-byte V1
        /// block. Every offset is pinned here and in the native hook_status_test.
        /// </summary>
        [TestMethod]
        [DataRow(1)]
        [DataRow(5)]
        public void TryRead_ReadsTheVersion2ProbingFields(int queueState)
        {
            int pid = NextTestPid();

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookStatusProbe.GetMappingName(pid), StatusSizeV2,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StatusSizeV2, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, Magic);
                view.Write(4, 2);      // version
                view.Write(8, pid);
                view.Write(12, unchecked((int)(uint)(NativeHookStatusFlags.Loaded |
                                                     NativeHookStatusFlags.HooksArmed)));
                view.Write(16, 4242L);
                view.Write(64, 15);    // installPhase = FidelityFxExports
                view.Write(68, (1 << 8) | 2);
                view.Write(72, 4);     // appliedFlags = EnableGenericD3D12PresentRoute
                view.Write(76, 3);     // appliedSequence
                view.Write(80, 8);     // pendingRestartFlags = DisableFidelityFxSwapchainLifecycleHooks
                view.Write(84, 6);     // liveReloadCapabilities = bits 1 and 2
                view.Write(88, 120);   // coverageAttempts
                view.Write(92, 118);   // coverageSubmitted
                view.Write(96, 2);     // coverageMissed
                view.Write(100, 1 | (2 << 2) | (1 << 4) | (2 << 5)); // DLSS, active, authoritative, DLSS-G on
                view.Write(104, queueState); // Observed or replacement binding unavailable
                view.Write(108, 13);   // lastDeclineReason = D3D12NoQueue
                view.Write(112, 5);    // routeSource = StreamlineProxy + 1
                view.Write(116, 2);    // compatChannelVersion
                view.Write(120, 777L); // lastFlagsAppliedTickMs
                view.Flush();

                Assert.IsTrue(HookStatusProbe.TryRead(
                    pid, out NativeHookStatusSnapshot snapshot, out string error), error);

                Assert.AreEqual(2, snapshot.Version);
                Assert.AreEqual(4242L, snapshot.LastHeartbeatTickMs);
                Assert.AreEqual(NativeHookInstallPhase.FidelityFxExports, snapshot.InstallPhase);
                Assert.AreEqual((1 << 8) | 2, snapshot.InstallDetail);
                Assert.AreEqual(4u, snapshot.AppliedFlags);
                Assert.AreEqual(3u, snapshot.AppliedSequence);
                Assert.AreEqual(8u, snapshot.PendingRestartFlags);
                Assert.AreEqual(6u, snapshot.LiveReloadCapabilities);
                Assert.AreEqual(120, snapshot.CoverageAttempts);
                Assert.AreEqual(118, snapshot.CoverageSubmitted);
                Assert.AreEqual(2, snapshot.CoverageMissed);
                Assert.AreEqual(1, snapshot.FgTechnology);
                Assert.AreEqual(2, snapshot.FgActivity);
                Assert.IsTrue(snapshot.FgAuthoritative);
                Assert.AreEqual(2, snapshot.StreamlineDlssgMode);
                Assert.AreEqual((NativeHookQueueState)queueState, snapshot.QueueState);
                Assert.AreEqual(NativeHookDeclineReason.D3D12NoQueue, snapshot.LastDeclineReason);
                Assert.AreEqual(5, snapshot.RouteSource);
                Assert.AreEqual(2, snapshot.CompatChannelVersion);
                Assert.AreEqual(777L, snapshot.LastFlagsAppliedTickMs);
            }
        }

        [TestMethod]
        public void TryRead_ReadsAVersion1BlockWithZeroedVersion2Fields()
        {
            // The shipped hook may lag the reader (see the prebuilt tree): its 64-byte block
            // must keep parsing, with every V2 field at its "unknown" zero.
            int pid = NextTestPid();

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookStatusProbe.GetMappingName(pid), StatusSize,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StatusSize, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, Magic);
                view.Write(4, 1);
                view.Write(8, pid);
                view.Write(12, unchecked((int)(uint)NativeHookStatusFlags.Loaded));
                view.Flush();

                Assert.IsTrue(HookStatusProbe.TryRead(
                    pid, out NativeHookStatusSnapshot snapshot, out _));
                Assert.AreEqual(1, snapshot.Version);
                Assert.AreEqual(NativeHookInstallPhase.None, snapshot.InstallPhase);
                Assert.AreEqual(NativeHookQueueState.None, snapshot.QueueState);
                Assert.AreEqual(NativeHookDeclineReason.None, snapshot.LastDeclineReason);
                Assert.AreEqual(0u, snapshot.LiveReloadCapabilities);
                Assert.AreEqual(0, snapshot.CoverageAttempts);
            }
        }

        [TestMethod]
        public void TryRead_ReportsUnrecognizedVersion2ValuesAsUnknown()
        {
            int pid = NextTestPid();

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookStatusProbe.GetMappingName(pid), StatusSizeV2,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StatusSizeV2, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, Magic);
                view.Write(4, 2);
                view.Write(8, pid);
                view.Write(12, unchecked((int)(uint)NativeHookStatusFlags.Loaded));
                view.Write(64, 99);
                view.Write(104, 99);
                view.Write(108, 99);
                view.Flush();

                Assert.IsTrue(HookStatusProbe.TryRead(
                    pid, out NativeHookStatusSnapshot snapshot, out _));
                Assert.AreEqual(NativeHookInstallPhase.Unknown, snapshot.InstallPhase);
                Assert.AreEqual(NativeHookQueueState.Unknown, snapshot.QueueState);
                Assert.AreEqual(NativeHookDeclineReason.Unknown, snapshot.LastDeclineReason);
            }
        }

        [TestMethod]
        public void TryRead_RejectsAVersionThisReaderDoesNotKnow()
        {
            int pid = NextTestPid();

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookStatusProbe.GetMappingName(pid), StatusSizeV2,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StatusSizeV2, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, Magic);
                view.Write(4, 3);
                view.Write(8, pid);
                view.Flush();

                Assert.IsFalse(HookStatusProbe.TryRead(pid, out _, out string error));
                StringAssert.Contains(error, "version 3");
            }
        }

        [TestMethod]
        public void TryRead_RejectsAForeignHeader()
        {
            int pid = NextTestPid();

            using (MemoryMappedFile mapping = MemoryMappedFile.CreateNew(
                HookStatusProbe.GetMappingName(pid), StatusSize,
                MemoryMappedFileAccess.ReadWrite))
            using (MemoryMappedViewAccessor view = mapping.CreateViewAccessor(
                0, StatusSize, MemoryMappedFileAccess.ReadWrite))
            {
                view.Write(0, 0x11223344);
                view.Flush();

                Assert.IsFalse(HookStatusProbe.TryRead(pid, out _, out string error));
                Assert.IsNotNull(error);
            }
        }

        private static int NextTestPid()
            => Interlocked.Increment(ref _nextTestPid);
    }
}
