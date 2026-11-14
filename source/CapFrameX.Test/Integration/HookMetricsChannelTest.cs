using System;
using System.Collections.Generic;
using System.Linq;
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
    [DoNotParallelize] // one isolated named section backs this test class
    public class HookMetricsChannelTest
    {
        // Mirrors of the private layout constants (kept local so a change to either side has to
        // be a deliberate edit here as well).
        private const int OffMagic = 0, OffEntryCount = 12, OffFlags = 20;
        private const int HeaderSize = 32;
        private const int RecordSize = 380; // v5: v4's 376 bytes + the value display mode
        private const int RId = 0;
        private static readonly string TestMapName = @"Local\CfxOsdMetricsTest_" + Guid.NewGuid().ToString("N");

        [TestMethod]
        public void Publish_DisplayModes_UsesVersionFiveAndPreservesFollowingRecords()
        {
            var entries = new List<OsdEntry>
            {
                new OsdEntry { Identifier = "GpuLoad", IsNumeric = true, Value = 76,
                    Unit = "%", ValueDisplayMode = OsdValueDisplayMode.TextAndBar },
                new OsdEntry { Identifier = "CpuLoad", IsNumeric = true, Value = 35,
                    Unit = "%", ValueDisplayMode = OsdValueDisplayMode.Bar },
                new OsdEntry { Identifier = "Frametime", ShowGraph = true }
            };

            using (var channel = HookMetricsChannel.Create(targetPid: 4244, mapName: TestMapName))
            {
                channel.Publish(entries, flags: 0u, targetPid: 4244);
                IntPtr view = OpenReadOnlyView();
                Assert.AreNotEqual(IntPtr.Zero, view, "The isolated metrics section must be readable.");
                try
                {
                    Assert.AreEqual(5, Marshal.ReadInt32(view, 4));
                    Assert.AreEqual(2, Marshal.ReadInt32(view, HeaderSize + 376));
                    Assert.AreEqual(1, Marshal.ReadInt32(view, HeaderSize + RecordSize + 376));
                    Assert.AreEqual(0, Marshal.ReadInt32(view, HeaderSize + 2 * RecordSize + 376));
                    Assert.AreEqual("Frametime", ReadIdentifier(view, 2));
                }
                finally
                {
                    UnmapViewOfFile(view);
                }
            }
        }

        [TestMethod]
        public void Publish_AnEnthusiastProfileOnAHighCoreCountCpu_KeepsEveryEntry()
        {
            // 24 cores x (clock + load) + CPU Max + package power/temp + 7 GPU + CPU/RAM headers
            // + DIMMs + RAM Used + 2 online metrics + Framerate/Frametime/DisplayTime = 68 on the
            // reference machine; per-thread loads and larger core counts need room well beyond.
            const int count = 256;
            var entries = new List<OsdEntry>();
            for (int i = 0; i < count; i++)
                entries.Add(new OsdEntry { Identifier = $"e{i}", IsNumeric = true });

            using (var channel = HookMetricsChannel.Create(targetPid: 4245, mapName: TestMapName))
            {
                channel.Publish(entries, flags: 0u, targetPid: 4245);

                IntPtr view = OpenReadOnlyView();
                Assert.AreNotEqual(IntPtr.Zero, view, "The isolated metrics section must be readable.");
                try
                {
                    Assert.AreEqual(count, Marshal.ReadInt32(view, OffEntryCount),
                        "the capacity is too small for the Enthusiast template");
                    Assert.AreEqual($"e{count - 1}", ReadIdentifier(view, count - 1));
                }
                finally
                {
                    UnmapViewOfFile(view);
                }
            }
        }

        [TestMethod]
        public void Publish_MoreEntriesThanTheOldCap_KeepsTheTailThatCarriesMetricsAndGraph()
        {
            // What the reference 24-core machine actually produces: past the old 64-entry cap.
            const int count = 68;

            var entries = new List<OsdEntry>();
            for (int i = 0; i < count - 4; i++)
                entries.Add(new OsdEntry { Identifier = $"/intelcpu/0/clock/{i}", IsNumeric = true });
            // The rows the template sorter puts last — the ones truncation used to eat.
            entries.Add(new OsdEntry { Identifier = "OnlineAverage", IsNumeric = true });
            entries.Add(new OsdEntry { Identifier = "Online1PercentLow", IsNumeric = true });
            entries.Add(new OsdEntry { Identifier = "Framerate", IsNumeric = true });
            entries.Add(new OsdEntry { Identifier = "Frametime", IsNumeric = true, ShowGraph = true });

            using (var channel = HookMetricsChannel.Create(targetPid: 4242, mapName: TestMapName))
            {
                channel.Publish(entries, flags: 0u, targetPid: 4242);

                IntPtr view = OpenReadOnlyView();
                Assert.AreNotEqual(IntPtr.Zero, view, "The isolated metrics section must be readable.");
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

            using (var channel = HookMetricsChannel.Create(targetPid: 4243, mapName: TestMapName))
            {
                // A MapSize too small for MaxEntries would corrupt memory past the view here.
                channel.Publish(entries, flags: 0u, targetPid: 4243);

                IntPtr view = OpenReadOnlyView();
                Assert.AreNotEqual(IntPtr.Zero, view, "The isolated metrics section must be readable.");
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

        [TestMethod]
        public void Publish_FontFamily_TravelsBehindTheRecordsWithBitSix()
        {
            var entries = new List<OsdEntry> { new OsdEntry { Identifier = "Framerate", IsNumeric = true } };
            const uint zoom = HookMetricsChannel.FlagZoom | (150u << HookMetricsChannel.ZoomShift);

            using (var channel = HookMetricsChannel.Create(targetPid: 4246, mapName: TestMapName))
            {
                channel.Publish(entries, zoom, targetPid: 4246, fontFamily: " Consolas ");
                IntPtr view = OpenReadOnlyView();
                Assert.AreNotEqual(IntPtr.Zero, view, "The isolated metrics section must be readable.");
                try
                {
                    Assert.AreEqual(zoom | HookMetricsChannel.FlagFontFamily, (uint)Marshal.ReadInt32(view, OffFlags));
                    Assert.AreEqual("Consolas", ReadFontFamily(view));
                    Assert.AreEqual("Framerate", ReadIdentifier(view, 0), "The records stay where they were.");
                    Assert.AreEqual(5, Marshal.ReadInt32(view, 4), "The font needs no new version.");

                    channel.Publish(entries, zoom | HookMetricsChannel.FlagFontFamily, targetPid: 4246, fontFamily: null);
                    Assert.AreEqual(zoom, (uint)Marshal.ReadInt32(view, OffFlags),
                        "Without a name the bit must be clear, whatever the caller passed.");
                    Assert.AreEqual(string.Empty, ReadFontFamily(view), "The previous name must not linger.");
                }
                finally
                {
                    UnmapViewOfFile(view);
                }
            }
        }

        [TestMethod]
        public void Publish_AtCapacityWithAFont_KeepsTheLastRecordAndTheFontApart()
        {
            // The font field lies behind the largest record area: a full entry set and a font of
            // the maximum length must not overwrite each other, and both must fit the mapping.
            var entries = new List<OsdEntry>();
            for (int i = 0; i < HookMetricsChannel.MaxEntries; i++)
                entries.Add(new OsdEntry { Identifier = new string('i', 63), IsNumeric = true });
            string font = new string('F', HookMetricsChannel.SzFontFamily + 10);

            using (var channel = HookMetricsChannel.Create(targetPid: 4247, mapName: TestMapName))
            {
                channel.Publish(entries, 0u, targetPid: 4247, fontFamily: font);
                IntPtr view = OpenReadOnlyView();
                Assert.AreNotEqual(IntPtr.Zero, view, "The isolated metrics section must be readable.");
                try
                {
                    Assert.AreEqual(new string('i', 63), ReadIdentifier(view, HookMetricsChannel.MaxEntries - 1));
                    Assert.AreEqual(font.Substring(0, HookMetricsChannel.SzFontFamily - 1), ReadFontFamily(view),
                        "The name stops one byte short of the field, which keeps its terminator.");
                }
                finally
                {
                    UnmapViewOfFile(view);
                }
            }
        }

        [TestMethod]
        public void TruncateUtf8_NeverSplitsACharacter()
        {
            Assert.AreEqual("Consolas", HookMetricsChannel.TruncateUtf8("Consolas", 127));
            string kanji = new string('游', 50); // three UTF-8 bytes each
            string cut = HookMetricsChannel.TruncateUtf8(kanji, 127);
            Assert.AreEqual(42, cut.Length);
            Assert.IsTrue(System.Text.Encoding.UTF8.GetByteCount(cut) <= 127);
            string emoji = string.Concat(Enumerable.Repeat("\U0001F600", 40)); // surrogate pairs, four bytes each
            Assert.AreEqual(31 * 2, HookMetricsChannel.TruncateUtf8(emoji, 127).Length);
        }

        /// <summary>
        /// Publish encodes the records into a buffer it keeps and copies them in one block. Every
        /// byte must be what the per-field writes produced before: the UTF-8 text cut at size - 1
        /// bytes (also inside a character, as before), zeros behind it, the numbers little-endian.
        /// A second, shorter publish must leave no byte of the first in a reused record.
        /// </summary>
        [TestMethod]
        public void Publish_EncodesEveryFieldAsThePerFieldWritesDid()
        {
            var first = new List<OsdEntry>
            {
                new OsdEntry { Identifier = "GpuTemp", Group = "GPU", Label = "Temperatur °C", ValueText = "64",
                    Unit = "°C", Value = 64.25, UpperLimit = 90, LowerLimit = -1.5, IsNumeric = true, Color = 0xFF8800FFu,
                    ShowGraph = true, Digits = 1, HasUpper = true, UpperColor = 0xFF0000FFu, HasLower = true,
                    LowerColor = 0x00FF00FFu, Separators = 2, GroupColor = 0x123456FFu, GroupScalePercent = 120,
                    ValueScalePercent = 80, ValueDisplayMode = OsdValueDisplayMode.TextAndBar },
                new OsdEntry { Identifier = new string('x', 80), Group = new string('游', 30), Label = "\U0001F600 emoji",
                    ValueText = null, Unit = "", Value = double.NaN },
                new OsdEntry { Identifier = "Frametime" }
            };
            var second = new List<OsdEntry> { new OsdEntry { Identifier = "A" } };

            using (var channel = HookMetricsChannel.Create(targetPid: 4248, mapName: TestMapName))
            {
                channel.Publish(first, flags: 0u, targetPid: 4248);
                IntPtr view = OpenReadOnlyView();
                Assert.AreNotEqual(IntPtr.Zero, view);
                try
                {
                    for (int i = 0; i < first.Count; i++)
                        CollectionAssert.AreEqual(ReferenceRecord(first[i]), ReadRecord(view, i), $"record {i}");
                    channel.Publish(second, flags: 0u, targetPid: 4248);
                    CollectionAssert.AreEqual(ReferenceRecord(second[0]), ReadRecord(view, 0),
                        "a shorter entry leaves nothing of the longer one before it");
                    Assert.AreEqual(1, Marshal.ReadInt32(view, OffEntryCount));
                    Assert.AreEqual(0, Marshal.ReadByte(view, HookMetricsChannel.OffFontFamily), "no font, an empty field");
                }
                finally
                {
                    UnmapViewOfFile(view);
                }
            }
        }

        private static byte[] ReadRecord(IntPtr view, int index)
        {
            var bytes = new byte[RecordSize];
            Marshal.Copy(IntPtr.Add(view, HeaderSize + index * RecordSize), bytes, 0, RecordSize);
            return bytes;
        }

        // The record as the per-field writes before 2026-11 produced it (Marshal.WriteInt32 and
        // WriteStr), written out independently of the channel's code.
        private static byte[] ReferenceRecord(OsdEntry e)
        {
            var record = new byte[RecordSize];
            void Text(int offset, int size, string s)
            {
                if (string.IsNullOrEmpty(s)) return;
                byte[] bytes = System.Text.Encoding.UTF8.GetBytes(s);
                Array.Copy(bytes, 0, record, offset, Math.Min(bytes.Length, size - 1));
            }
            void Int(int offset, int value) => BitConverter.GetBytes(value).CopyTo(record, offset);
            void Double(int offset, double value) => BitConverter.GetBytes(value).CopyTo(record, offset);
            Text(0, 64, e.Identifier); Text(64, 64, e.Group); Text(128, 96, e.Label);
            Text(224, 64, e.ValueText); Text(288, 16, e.Unit);
            Double(304, e.Value); Double(312, e.UpperLimit); Double(320, e.LowerLimit);
            Int(328, e.IsNumeric ? 1 : 0); Int(332, unchecked((int)e.Color)); Int(336, e.ShowGraph ? 1 : 0);
            Int(340, e.Digits); Int(344, e.HasUpper ? 1 : 0); Int(348, unchecked((int)e.UpperColor));
            Int(352, e.HasLower ? 1 : 0); Int(356, unchecked((int)e.LowerColor)); Int(360, e.Separators);
            Int(364, unchecked((int)e.GroupColor)); Int(368, e.GroupScalePercent); Int(372, e.ValueScalePercent);
            Int(376, (int)e.ValueDisplayMode);
            return record;
        }

        private static string ReadFontFamily(IntPtr view)
            => Marshal.PtrToStringUTF8(IntPtr.Add(view, HookMetricsChannel.OffFontFamily));

        private static string ReadIdentifier(IntPtr view, int index)
        {
            IntPtr rec = IntPtr.Add(view, HeaderSize + index * RecordSize);
            return Marshal.PtrToStringUTF8(IntPtr.Add(rec, RId));
        }

        private static IntPtr OpenReadOnlyView()
        {
            IntPtr map = OpenFileMappingW(FileMapRead, false, TestMapName);
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
