using System;
using System.Diagnostics;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class VulkanLayerModuleProbeTest
    {
        [TestMethod]
        public void IsLayerModule_MatchesTheLayerByNameOrPath()
        {
            Assert.IsTrue(VulkanLayerModuleProbe.IsLayerModule(
                "cfx_osd_vklayer.dll", @"C:\app\vulkan\cfx_osd_vklayer.dll"));
            Assert.IsTrue(VulkanLayerModuleProbe.IsLayerModule(
                "CFX_OSD_VKLAYER.DLL", null));
            Assert.IsTrue(VulkanLayerModuleProbe.IsLayerModule(
                null, @"C:\app\vulkan\cfx_osd_vklayer.dll"));
        }

        [TestMethod]
        public void IsLayerModule_DoesNotMatchTheDxgiHookOrOtherLayers()
        {
            Assert.IsFalse(VulkanLayerModuleProbe.IsLayerModule(
                "cfx_osd_hook.dll", @"C:\app\hook\cfx_osd_hook.dll"));
            Assert.IsFalse(VulkanLayerModuleProbe.IsLayerModule(
                "RTSSVkLayer64.dll", @"C:\RTSS\Vulkan\RTSSVkLayer64.dll"));
            Assert.IsFalse(VulkanLayerModuleProbe.IsLayerModule(null, null));
            Assert.IsFalse(VulkanLayerModuleProbe.IsLayerModule(string.Empty, string.Empty));
        }

        [TestMethod]
        public void GetPresence_ReportsAbsentForThisProcessWithoutAnError()
        {
            // The test host never loads the Vulkan layer, so this is the "plain DXGI target"
            // path: conclusively absent, no error, and therefore no suppression of injection.
            VulkanLayerPresence presence = VulkanLayerModuleProbe.GetPresence(
                Process.GetCurrentProcess().Id, out string error);

            Assert.AreEqual(VulkanLayerPresence.Absent, presence);
            Assert.IsNull(error);
        }

        [TestMethod]
        public void GetPresence_ReportsUnknownForAPidThatCannotBeScanned()
        {
            // A PID that no longer exists cannot be enumerated. That must surface as Unknown —
            // reporting Absent here is what let the DXGI hook into a Vulkan title.
            VulkanLayerPresence presence = VulkanLayerModuleProbe.GetPresence(
                int.MaxValue - 1, out string error);

            Assert.AreEqual(VulkanLayerPresence.Unknown, presence);
            Assert.IsNotNull(error);
        }

        [TestMethod]
        public void GetPresence_DoesNotCacheAnInconclusiveScan()
        {
            int pid = int.MaxValue - 2;

            Assert.AreEqual(VulkanLayerPresence.Unknown,
                VulkanLayerModuleProbe.GetPresence(pid, out _));
            // A retry must scan again instead of being served a cached "no layer".
            Assert.AreEqual(VulkanLayerPresence.Unknown,
                VulkanLayerModuleProbe.GetPresence(pid, out _));
        }

        [TestMethod]
        public void GetPresence_RejectsInvalidPidsWithoutScanning()
        {
            Assert.AreEqual(VulkanLayerPresence.Absent,
                VulkanLayerModuleProbe.GetPresence(0, out string error));
            Assert.IsNull(error);
            Assert.AreEqual(VulkanLayerPresence.Absent,
                VulkanLayerModuleProbe.GetPresence(-1, out error));
            Assert.IsNull(error);
        }

        [TestMethod]
        public void Prune_DropsCacheEntriesForProcessesThatExited()
        {
            int pid = Process.GetCurrentProcess().Id;
            VulkanLayerModuleProbe.GetPresence(pid, out _);

            VulkanLayerModuleProbe.Prune(_ => false);

            // Nothing observable but the absence of a stale entry; re-querying must still work.
            Assert.AreEqual(VulkanLayerPresence.Absent,
                VulkanLayerModuleProbe.GetPresence(pid, out string error));
            Assert.IsNull(error);
        }

        [TestMethod]
        public void Prune_RejectsMissingLivenessProbe()
        {
            Assert.ThrowsException<ArgumentNullException>(
                () => VulkanLayerModuleProbe.Prune(null));
        }
    }
}
