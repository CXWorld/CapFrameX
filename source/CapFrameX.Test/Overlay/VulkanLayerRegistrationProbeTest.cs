using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// The loader rules the Info tab applies to the CapFrameX implicit layer: one registration per
    /// bitness in its own HKLM view; anything a process of one bitness can reach but not load
    /// shadows the layer for that bitness.
    /// </summary>
    [TestClass]
    public class VulkanLayerRegistrationProbeTest
    {
        private const string Manifest64 = @"C:\Program Files\CapFrameX\vulkan\cfx_osd_vklayer_v1.json";
        private const string Manifest32 = @"C:\Program Files\CapFrameX\vulkan\x86\cfx_osd_vklayer_v1.json";

        private static VulkanLayerRegistryEntry Entry(VulkanLayerRegistryLocation location, string manifest,
            VulkanLayerLibraryBitness bitness, bool enabled = true, bool readable = true)
            => new VulkanLayerRegistryEntry(location, manifest, enabled, readable, bitness, "1");

        private static readonly VulkanLayerRegistryEntry Correct64 =
            Entry(VulkanLayerRegistryLocation.Machine64, Manifest64, VulkanLayerLibraryBitness.X64);

        private static readonly VulkanLayerRegistryEntry Correct32 =
            Entry(VulkanLayerRegistryLocation.Machine32, Manifest32, VulkanLayerLibraryBitness.X86);

        [TestMethod]
        public void Classify_OneMatchingRegistrationPerView_IsRegistered()
        {
            var status = VulkanLayerRegistrationProbe.Classify(new[] { Correct64, Correct32 });

            Assert.AreEqual(VulkanLayerRegistrationState.Registered, status.State);
            Assert.AreEqual("1", status.Version);
            StringAssert.Contains(status.Detail, "x64: " + Manifest64);
            StringAssert.Contains(status.Detail, "x86: " + Manifest32);
        }

        [TestMethod]
        public void Classify_No64Or32BitRegistration_IsNotRegistered()
        {
            var status = VulkanLayerRegistrationProbe.Classify(Array.Empty<VulkanLayerRegistryEntry>());

            Assert.AreEqual(VulkanLayerRegistrationState.NotRegistered, status.State);
            Assert.IsNull(status.Version);
        }

        [TestMethod]
        public void Classify_Only64BitRegistration_IsIncomplete()
        {
            var status = VulkanLayerRegistrationProbe.Classify(new[] { Correct64 });

            Assert.AreEqual(VulkanLayerRegistrationState.Incomplete, status.State);
            StringAssert.Contains(status.Detail, "x86: not registered");
        }

        [TestMethod]
        public void Classify_CurrentUserEntry_ShadowsTheOtherBitness()
        {
            // HKCU is read by both bitnesses; a 64-bit manifest there breaks 32-bit games even
            // though both HKLM views are correct.
            var hkcu = Entry(VulkanLayerRegistryLocation.CurrentUser, Manifest64, VulkanLayerLibraryBitness.X64);

            var status = VulkanLayerRegistrationProbe.Classify(new[] { Correct64, Correct32, hkcu });

            Assert.AreEqual(VulkanLayerRegistrationState.Conflicting, status.State);
            StringAssert.Contains(status.Detail, "x86: " + Manifest64 + " (registered in HKCU, points to a 64-bit DLL)");
        }

        [TestMethod]
        public void Classify_WrongBitnessInMachineView_IsConflicting()
        {
            var x86InNativeView = Entry(VulkanLayerRegistryLocation.Machine64, Manifest32, VulkanLayerLibraryBitness.X86);

            var status = VulkanLayerRegistrationProbe.Classify(new[] { Correct64, x86InNativeView, Correct32 });

            Assert.AreEqual(VulkanLayerRegistrationState.Conflicting, status.State);
            StringAssert.Contains(status.Detail, "points to a 32-bit DLL");
        }

        [TestMethod]
        public void Classify_MissingLayerDll_IsConflicting()
        {
            var missingDll = Entry(VulkanLayerRegistryLocation.Machine32, Manifest32, VulkanLayerLibraryBitness.Missing);

            var status = VulkanLayerRegistrationProbe.Classify(new[] { Correct64, missingDll });

            Assert.AreEqual(VulkanLayerRegistrationState.Conflicting, status.State);
            StringAssert.Contains(status.Detail, "layer DLL is missing");
        }

        [TestMethod]
        public void Classify_DisabledEntry_IsIgnored()
        {
            // The loader only reads manifests whose DWORD value is 0.
            var disabled = Entry(VulkanLayerRegistryLocation.Machine64, Manifest32, VulkanLayerLibraryBitness.X86, enabled: false);

            var status = VulkanLayerRegistrationProbe.Classify(new[] { Correct64, Correct32, disabled });

            Assert.AreEqual(VulkanLayerRegistrationState.Registered, status.State);
        }

        [TestMethod]
        public void Classify_LeftoverWithoutManifest_DoesNotChangeTheStateButIsReported()
        {
            const string leftover = @"E:\Build\vk_layer\cfx_osd_vklayer.json";
            var stale = Entry(VulkanLayerRegistryLocation.Machine64, leftover, VulkanLayerLibraryBitness.Missing, readable: false);

            var status = VulkanLayerRegistrationProbe.Classify(new[] { Correct64, Correct32, stale });

            Assert.AreEqual(VulkanLayerRegistrationState.Registered, status.State);
            StringAssert.Contains(status.Detail, "Leftover registration without manifest: " + leftover);
        }

        [TestMethod]
        public void ReadLibraryBitness_SystemDlls_MatchTheirFolder()
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

            Assert.AreEqual(VulkanLayerLibraryBitness.X64,
                VulkanLayerRegistrationProbe.ReadLibraryBitness(Path.Combine(windows, "System32", "kernel32.dll")));
            Assert.AreEqual(VulkanLayerLibraryBitness.X86,
                VulkanLayerRegistrationProbe.ReadLibraryBitness(Path.Combine(windows, "SysWOW64", "kernel32.dll")));
        }

        [TestMethod]
        public void ReadLibraryBitness_MissingFile_IsMissing()
        {
            Assert.AreEqual(VulkanLayerLibraryBitness.Missing,
                VulkanLayerRegistrationProbe.ReadLibraryBitness(@"C:\does\not\exist\cfx_osd_vklayer.dll"));
        }
    }
}
