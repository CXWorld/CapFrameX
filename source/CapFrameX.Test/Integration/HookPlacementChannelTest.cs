using System;
using System.Runtime.InteropServices;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookPlacementChannelTest
    {
        // Mirrors the native reader layout in overlay_placement_shm.cpp.
        private const int OffMagic = 0, OffSeq = 8, OffAnchor = 12,
                          OffMarginX = 16, OffMarginY = 20;

        [TestMethod]
        public void Publish_ForcedWithUnchangedPlacement_AdvancesSequence()
        {
            string mapName = @"Local\CfxOsdPlacementTest_" + Guid.NewGuid().ToString("N");
            using (var channel = HookPlacementChannel.Create(mapName))
            {
                channel.Publish(anchor: 1, marginX: 30, marginY: 40);

                IntPtr view = OpenReadOnlyView(mapName);
                Assert.AreNotEqual(IntPtr.Zero, view, "the placement mapping could not be opened");

                try
                {
                    Assert.AreEqual(unchecked((int)HookPlacementChannel.Magic),
                        Marshal.ReadInt32(view, OffMagic));
                    Assert.AreEqual(1, Marshal.ReadInt32(view, OffAnchor));
                    Assert.AreEqual(30, Marshal.ReadInt32(view, OffMarginX));
                    Assert.AreEqual(40, Marshal.ReadInt32(view, OffMarginY));

                    int firstSequence = Marshal.ReadInt32(view, OffSeq);
                    channel.Publish(anchor: 1, marginX: 30, marginY: 40);
                    Assert.AreEqual(firstSequence, Marshal.ReadInt32(view, OffSeq),
                        "an unchanged steady-state placement should not be republished");

                    channel.Publish(anchor: 1, marginX: 30, marginY: 40, force: true);
                    Assert.AreEqual(firstSequence + 2, Marshal.ReadInt32(view, OffSeq),
                        "reactivation must make an unchanged placement visible to a new renderer");
                }
                finally
                {
                    UnmapViewOfFile(view);
                }
            }
        }

        private static IntPtr OpenReadOnlyView(string mapName)
        {
            IntPtr map = OpenFileMappingW(FileMapRead, false, mapName);
            if (map == IntPtr.Zero) return IntPtr.Zero;
            try
            {
                return MapViewOfFile(map, FileMapRead, 0, 0, UIntPtr.Zero);
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
