using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using CapFrameX.OSD.Integration;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// Capacity guards for the in-game hook's metrics shared memory. The channel silently
    /// truncated at 64 entries, which the Enthusiast template exceeds on any CPU with more than
    /// ~20 cores (one entry per core clock AND per core load). Because
    /// <c>OverlayViewModel.GetTemplateSortOrder</c> deliberately sorts the online metrics (70)
    /// and Framerate/Frametime (80) LAST, truncation dropped exactly the rows a user notices
    /// first — including the frametime graph, whose entry carries the ShowGraph flag.
    /// </summary>
    [TestClass]
    [DoNotParallelize] // one process-global named section backs every instance
    public class HookMetricsChannelTest
    {
        // Mirrors of the private layout constants (kept local so a change to either side has to
        // be a deliberate edit here as well).
        private const int OffMagic = 0, OffEntryCount = 12;
        private const int HeaderSize = 32;
        private const int RecordSize = 368;
        private const int RId = 0;

        [TestMethod]
        public void MaxEntries_HoldsAnEnthusiastProfileOnAHighCoreCountCpu()
        {
            // 24 cores x (clock + load) + CPU Max + package power/temp + 7 GPU + CPU/RAM headers
            // + DIMMs + RAM Used + 2 online metrics + Framerate/Frametime/DisplayTime = 68 on the
            // reference machine; leave room for per-thread loads and larger core counts.
            Assert.IsTrue(HookMetricsChannel.MaxEntries >= 256,
                $"capacity {HookMetricsChannel.MaxEntries} is too small for the Enthusiast template");
        }

        [TestMethod]
        public void Publish_MoreEntriesThanTheOldCap_KeepsTheTailThatCarriesMetricsAndGraph()
        {
            const int count = 68; // what the reference 24-core machine actually produces
            Assert.IsTrue(count > 64, "the regression case must exceed the old 64-entry cap");

            var entries = new List<OsdEntry>();
            for (int i = 0; i < count - 4; i++)
                entries.Add(new OsdEntry { Identifier = $"/intelcpu/0/clock/{i}", IsNumeric = true });
            // The rows the template sorter puts last — the ones truncation used to eat.
            entries.Add(new OsdEntry { Identifier = "OnlineAverage", IsNumeric = true });
            entries.Add(new OsdEntry { Identifier = "Online1PercentLow", IsNumeric = true });
            entries.Add(new OsdEntry { Identifier = "Framerate", IsNumeric = true });
            entries.Add(new OsdEntry { Identifier = "Frametime", IsNumeric = true, ShowGraph = true });

            using (var channel = HookMetricsChannel.Create(targetPid: 4242))
            {
                channel.Publish(entries, flags: 0u, targetPid: 4242);

                IntPtr view = OpenReadOnlyView();
                if (view == IntPtr.Zero)
                    Assert.Inconclusive("Global\\CfxOsdMetricsV1 could not be created or opened — " +
                        "creating a section in the Global namespace needs SeCreateGlobalPrivilege. " +
                        "Re-run this test from an ELEVATED shell; a skip here verifies nothing.");
                try
                {
                    Assert.AreEqual(unchecked((int)0x31584643u), Marshal.ReadInt32(view, OffMagic),
                        "magic mismatch — wrong mapping");
                    Assert.AreEqual(count, Marshal.ReadInt32(view, OffEntryCount),
                        "the entry set was truncated");

                    // The last record must be the graph-carrying Frametime entry, not a core clock.
                    Assert.AreEqual("Frametime", ReadIdentifier(view, count - 1));
                    Assert.AreEqual("Framerate", ReadIdentifier(view, count - 2));
                    Assert.AreEqual("Online1PercentLow", ReadIdentifier(view, count - 3));
                }
                finally
                {
                    UnmapViewOfFile(view);
                }
            }
        }

        [TestMethod]
        public void Publish_AtCapacity_StaysInsideTheMappedRegion()
        {
            var entries = new List<OsdEntry>();
            for (int i = 0; i < HookMetricsChannel.MaxEntries; i++)
                entries.Add(new OsdEntry { Identifier = $"e{i}", IsNumeric = true });

            using (var channel = HookMetricsChannel.Create(targetPid: 4243))
            {
                // A MapSize too small for MaxEntries would corrupt memory past the view here.
                channel.Publish(entries, flags: 0u, targetPid: 4243);

                IntPtr view = OpenReadOnlyView();
                if (view == IntPtr.Zero)
                    Assert.Inconclusive("Global\\CfxOsdMetricsV1 could not be created or opened — " +
                        "creating a section in the Global namespace needs SeCreateGlobalPrivilege. " +
                        "Re-run this test from an ELEVATED shell; a skip here verifies nothing.");
                try
                {
                    Assert.AreEqual(HookMetricsChannel.MaxEntries,
                        Marshal.ReadInt32(view, OffEntryCount));
                    Assert.AreEqual($"e{HookMetricsChannel.MaxEntries - 1}",
                        ReadIdentifier(view, HookMetricsChannel.MaxEntries - 1));
                }
                finally
                {
                    UnmapViewOfFile(view);
                }
            }
        }

        private static string ReadIdentifier(IntPtr view, int index)
        {
            IntPtr rec = IntPtr.Add(view, HeaderSize + index * RecordSize);
            return Marshal.PtrToStringUTF8(IntPtr.Add(rec, RId));
        }

        private static IntPtr OpenReadOnlyView()
        {
            IntPtr map = OpenFileMappingW(FileMapRead, false, @"Global\CfxOsdMetricsV1");
            if (map == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                return MapViewOfFile(map, FileMapRead, 0, 0, UIntPtr.Zero); // 0 => whole section
            }
            finally
            {
                CloseHandle(map);
            }
        }

        private const uint FileMapRead = 0x0004;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr OpenFileMappingW(uint access, bool inherit, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr map, uint access, uint offHigh,
            uint offLow, UIntPtr bytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr view);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
