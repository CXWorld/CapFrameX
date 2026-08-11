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
                view.Write(40, 2);     // release threshold
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
