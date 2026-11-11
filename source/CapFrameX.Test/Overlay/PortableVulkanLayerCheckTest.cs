using CapFrameX.Overlay;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// When the portable version offers to register its Vulkan layers, and the script that does it.
    /// </summary>
    [TestClass]
    public class PortableVulkanLayerCheckTest
    {
        private const string Own64 = @"D:\CapFrameX Portable\vulkan\cfx_osd_vklayer_v1.json";
        private const string Own32 = @"D:\CapFrameX Portable\vulkan\x86\cfx_osd_vklayer_v1.json";
        private const string Installed64 = @"C:\Program Files\CapFrameX\vulkan\cfx_osd_vklayer_v1.json";
        private const string Installed32 = @"C:\Program Files\CapFrameX\vulkan\x86\cfx_osd_vklayer_v1.json";

        private static readonly VulkanLayerPayload Payload64 = new VulkanLayerPayload(Own64, "OWN64");
        private static readonly VulkanLayerPayload Payload32 = new VulkanLayerPayload(Own32, "OWN32");

        private static string Library(string manifest) => Path.Combine(Path.GetDirectoryName(manifest), "cfx_osd_vklayer.dll");

        private static VulkanLayerRegistryEntry Entry(VulkanLayerRegistryLocation location, string manifest,
            VulkanLayerLibraryBitness bitness, bool enabled = true)
            => new VulkanLayerRegistryEntry(location, manifest, enabled, true, bitness, "1", Library(manifest));

        private static VulkanLayerRegistryEntry Machine64(string manifest)
            => Entry(VulkanLayerRegistryLocation.Machine64, manifest, VulkanLayerLibraryBitness.X64);

        private static VulkanLayerRegistryEntry Machine32(string manifest)
            => Entry(VulkanLayerRegistryLocation.Machine32, manifest, VulkanLayerLibraryBitness.X86);

        private static PortableVulkanLayerAssessment Assess(IReadOnlyCollection<VulkanLayerRegistryEntry> entries,
            string installedHash64 = "OTHER64", string installedHash32 = "OTHER32",
            VulkanLayerPayload x64 = null, VulkanLayerPayload x86 = null)
        {
            var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [Library(Own64)] = "OWN64",
                [Library(Own32)] = "OWN32",
                [Library(Installed64)] = installedHash64,
                [Library(Installed32)] = installedHash32
            };

            return PortableVulkanLayerCheck.Assess(entries, x64 ?? Payload64, x86 ?? Payload32,
                path => hashes.TryGetValue(path, out string hash) ? hash : null);
        }

        [TestMethod]
        public void NothingRegistered_OffersTheInstallation()
        {
            Assert.AreEqual(PortableVulkanLayerAction.Install, Assess(Array.Empty<VulkanLayerRegistryEntry>()).Action);
        }

        [TestMethod]
        public void LayersOfThisFolderInBothViews_NeedNothing()
        {
            Assert.AreEqual(PortableVulkanLayerAction.None, Assess(new[] { Machine64(Own64), Machine32(Own32) }).Action);
        }

        [TestMethod]
        public void InstalledLayersWithIdenticalFiles_NeedNothing()
        {
            // An installed CapFrameX of the same build: the loader loads the same files.
            var assessment = Assess(new[] { Machine64(Installed64), Machine32(Installed32) }, "OWN64", "OWN32");

            Assert.AreEqual(PortableVulkanLayerAction.None, assessment.Action);
        }

        [TestMethod]
        public void InstalledLayersOfAnotherBuild_OfferTheUpdate()
        {
            var assessment = Assess(new[] { Machine64(Installed64), Machine32(Installed32) });

            Assert.AreEqual(PortableVulkanLayerAction.Update, assessment.Action);
            StringAssert.Contains(assessment.Detail, "x64: " + Installed64);
            StringAssert.Contains(assessment.Detail, "x86: " + Installed32);
        }

        [TestMethod]
        public void OneBitnessWithoutRegistration_OffersTheUpdate()
        {
            var assessment = Assess(new[] { Machine64(Own64) });

            Assert.AreEqual(PortableVulkanLayerAction.Update, assessment.Action);
            StringAssert.Contains(assessment.Detail, "x86: not registered");
        }

        [TestMethod]
        public void ForeignRegistrationBesideTheOwnOne_OffersTheUpdate()
        {
            // The loader uses one of two registrations of the same layer name.
            var assessment = Assess(new[] { Machine64(Own64), Machine64(Installed64), Machine32(Own32) });

            Assert.AreEqual(PortableVulkanLayerAction.Update, assessment.Action);
        }

        [TestMethod]
        public void CurrentUserRegistration_OffersTheUpdate()
        {
            // Elevated games ignore HKCU, and 32-bit games cannot load the 64-bit DLL it names.
            var hkcu = Entry(VulkanLayerRegistryLocation.CurrentUser, Own64, VulkanLayerLibraryBitness.X64);

            var assessment = Assess(new[] { Machine64(Own64), Machine32(Own32), hkcu });

            Assert.AreEqual(PortableVulkanLayerAction.Update, assessment.Action);
        }

        [TestMethod]
        public void OwnManifestInTheWrongView_OffersTheUpdate()
        {
            var x86InNativeView = Entry(VulkanLayerRegistryLocation.Machine64, Own32, VulkanLayerLibraryBitness.X86);

            Assert.AreEqual(PortableVulkanLayerAction.Update, Assess(new[] { x86InNativeView, Machine32(Own32) }).Action);
        }

        [TestMethod]
        public void DisabledForeignRegistration_IsIgnored()
        {
            // The script disables the other registrations; the loader skips them.
            var disabled = Entry(VulkanLayerRegistryLocation.Machine64, Installed64, VulkanLayerLibraryBitness.X64, enabled: false);

            Assert.AreEqual(PortableVulkanLayerAction.None, Assess(new[] { Machine64(Own64), disabled, Machine32(Own32) }).Action);
        }

        [TestMethod]
        public void Signature_ChangesOnlyWithTheLayersOrTheirRegistration()
        {
            var installed = new[] { Machine64(Installed64), Machine32(Installed32) };
            string signature = Assess(installed).Signature;

            Assert.AreEqual(signature, Assess(installed).Signature, "Same layers and registrations.");
            Assert.AreNotEqual(signature, Assess(installed, installedHash64: "NEWER64").Signature, "The installation was updated.");
            Assert.AreNotEqual(signature, Assess(installed, x64: new VulkanLayerPayload(Own64, "NEXT64")).Signature,
                "A newer portable version was extracted over this one.");
            Assert.AreNotEqual(signature, Assess(new[] { Machine64(Installed64) }).Signature, "A registration was removed.");
            Assert.AreNotEqual(signature, Assess(installed, x64: new VulkanLayerPayload(@"E:\Moved\vulkan\cfx_osd_vklayer_v1.json", "OWN64")).Signature,
                "The portable folder moved.");
        }

        [TestMethod]
        public void FolderWithoutLayers_IsNotAssessed()
        {
            string folder = CreateTempFolder();
            try
            {
                Assert.IsNull(PortableVulkanLayerCheck.Assess(folder, out string problem));
                StringAssert.Contains(problem, "no CapFrameX layer manifest");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void FolderWithLayers_IsComparedWithTheRegistry()
        {
            string folder = CreateTempFolder();
            try
            {
                StageLayers(folder);

                var assessment = PortableVulkanLayerCheck.Assess(folder, out string problem);

                Assert.IsNotNull(assessment, problem);
                // Nothing on this machine registers a fresh temporary folder or a copy of kernel32.
                Assert.AreNotEqual(PortableVulkanLayerAction.None, assessment.Action);
                Assert.IsFalse(string.IsNullOrEmpty(assessment.Signature));
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void Script_ValidateOnly_PlansTheRegistrationOfItsOwnFolder()
        {
            // The folder name is not ASCII: the output has to arrive as UTF-8.
            string folder = CreateTempFolder();
            try
            {
                StageLayers(folder);
                string script = StageScript(folder);
                string manifest64 = Path.Combine(folder, "vulkan", "cfx_osd_vklayer_v1.json");
                string manifest32 = Path.Combine(folder, "vulkan", "x86", "cfx_osd_vklayer_v1.json");

                var result = VulkanLayerRegistrationScript.Run(script, TimeSpan.FromSeconds(60), "-ValidateOnly");

                Assert.AreEqual(0, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Output, "Registry64: " + manifest64);
                StringAssert.Contains(result.Output, "Registry32: " + manifest32);
                StringAssert.Contains(result.Output, "enable LocalMachine Registry64: " + manifest64);
                StringAssert.Contains(result.Output, "enable LocalMachine Registry32: " + manifest32);
                StringAssert.Contains(result.Output, "Validation only");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        [TestMethod]
        public void Script_DllOfTheWrongBitness_IsRejected()
        {
            string folder = CreateTempFolder();
            try
            {
                StageLayers(folder, swapBitness: true);
                string script = StageScript(folder);

                var result = VulkanLayerRegistrationScript.Run(script, TimeSpan.FromSeconds(60), "-ValidateOnly");

                Assert.AreEqual(1, result.ExitCode, result.Output + result.Error);
                StringAssert.Contains(result.Error, "Wrong DLL architecture for Registry64");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        private static string CreateTempFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "cfx-vk-portable-" + Guid.NewGuid().ToString("N") + " Prüfung");
            Directory.CreateDirectory(folder);
            return folder;
        }

        /// <summary>
        /// A layer pair as the build stages it, with kernel32 of either bitness standing in for the
        /// layer DLLs: the check and the script read only the manifest and the PE header.
        /// </summary>
        private static void StageLayers(string appFolder, bool swapBitness = false)
        {
            string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            string dll64 = Path.Combine(windows, "System32", "kernel32.dll");
            string dll32 = Path.Combine(windows, "SysWOW64", "kernel32.dll");
            const string manifest = "{\n  \"file_format_version\": \"1.2.0\",\n  \"layer\": {\n" +
                "    \"name\": \"VK_LAYER_CAPFRAMEX_overlay\",\n    \"type\": \"GLOBAL\",\n" +
                "    \"library_path\": \".\\\\cfx_osd_vklayer.dll\",\n    \"api_version\": \"1.3.0\",\n" +
                "    \"implementation_version\": \"1\"\n  }\n}\n";

            foreach (var (folder, dll) in new[]
            {
                (Path.Combine(appFolder, "vulkan"), swapBitness ? dll32 : dll64),
                (Path.Combine(appFolder, "vulkan", "x86"), swapBitness ? dll64 : dll32)
            })
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "cfx_osd_vklayer_v1.json"), manifest);
                File.Copy(dll, Path.Combine(folder, "cfx_osd_vklayer.dll"));
            }
        }

        private static string StageScript(string appFolder)
        {
            string source = Path.Combine(FindRepositoryRoot(), "source", "CapFrameX.OSD.Integration", PortableVulkanLayerCheck.ScriptFileName);
            string target = PortableVulkanLayerCheck.GetScriptPath(appFolder);
            File.Copy(source, target);
            return target;
        }

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CapFrameX.sln")))
                dir = dir.Parent;

            Assert.IsNotNull(dir, "Could not find CapFrameX.sln above the test output.");
            return dir.FullName;
        }
    }
}
