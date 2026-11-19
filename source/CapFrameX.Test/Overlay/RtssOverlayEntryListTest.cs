using CapFrameX.Contracts.Overlay;
using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics;
using System.Linq;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// The entry list the RTSS wrapper hands to its native renderer.
    /// </summary>
    [TestClass]
    public class RtssOverlayEntryListTest
    {
        [TestMethod]
        public void SetOverlayEntries_StoresEveryEntry()
        {
            var wrapper = CreateWrapper();

            wrapper.SetOverlayEntries(Entries("Framerate", "Frametime", "CpuLoad"));

            Assert.AreEqual(3, wrapper.OverlayEntryCount);
        }

        [TestMethod]
        public void SetOverlayEntries_ReplacesThePreviousEntries()
        {
            var wrapper = CreateWrapper();
            wrapper.SetOverlayEntries(Entries("Framerate", "Frametime", "CpuLoad"));

            wrapper.SetOverlayEntries(Entries("GpuLoad"));

            Assert.AreEqual(1, wrapper.OverlayEntryCount);
        }

        [TestMethod]
        public void SetOverlayEntries_EmptyList_ClearsThePreviousEntries()
        {
            // Regression: an empty list returned early, so the last rows stayed on screen after
            // every entry was switched off.
            var wrapper = CreateWrapper();
            wrapper.SetOverlayEntries(Entries("Framerate", "Frametime"));

            wrapper.SetOverlayEntries(Array.Empty<IOverlayEntry>());

            Assert.AreEqual(0, wrapper.OverlayEntryCount);
        }

        [TestMethod]
        public void SetOverlayEntries_Null_KeepsThePreviousEntries()
        {
            var wrapper = CreateWrapper();
            wrapper.SetOverlayEntries(Entries("Framerate", "Frametime"));

            wrapper.SetOverlayEntries(null);

            Assert.AreEqual(2, wrapper.OverlayEntryCount);
        }

        [TestMethod]
        public void GetFrameTimesInterval_ReturnsEmptyWithoutRtssFrameTimes()
        {
            // Crash reports of 1.8.6.2 (2026-11-17, Quake Live): RTSS not running, not hooking the
            // game, or its newest frame time a second or more: the native reader returns no frame
            // times, and pinning the first element of the empty managed array threw
            // IndexOutOfRangeException. The process ID is one RTSS cannot have an entry for.
            var wrapper = CreateWrapper();

            var frameTimes = wrapper.GetFrameTimesInterval(UnusedProcessId(), 1000);

            Assert.IsNotNull(frameTimes);
            Assert.AreEqual(0, frameTimes.Length);
        }

        // Never disposed: Dispose releases the RTSS OSD slot owned by "CapFrameX", which would
        // clear the overlay of a CapFrameX instance running on this machine.
        private static RTSSCSharpWrapper CreateWrapper()
            => new RTSSCSharpWrapper(exception => throw new AssertFailedException(exception.Message));

        private static int UnusedProcessId()
        {
            var running = Process.GetProcesses().Select(process => process.Id).ToHashSet();
            int pid = 0x3FFFFFFC;
            while (running.Contains(pid))
                pid -= 4;
            return pid;
        }

        private static IOverlayEntry[] Entries(params string[] identifiers)
            => identifiers
                .Select(identifier => (IOverlayEntry)new OverlayEntryWrapper(identifier)
                {
                    GroupName = identifier,
                    Value = 42,
                    ShowOnOverlay = true
                })
                .ToArray();
    }
}
