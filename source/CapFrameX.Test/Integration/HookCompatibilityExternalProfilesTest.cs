using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookCompatibilityExternalProfilesTest
    {
        [TestMethod]
        public void MissingExternalFiles_CreateOnlyTheConfiguredUserTemplate()
        {
            using (var files = new ProfileFiles())
            {
                IReadOnlyDictionary<string, HookCompatibilityProfile> profiles =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        files.UserPath);

                AssertEmbeddedForzaProfile(profiles);
                Assert.IsFalse(File.Exists(files.ApplicationPath));
                AssertEmptyProfileFile(files.UserPath);
                Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(files.ApplicationPath)));
                Assert.IsTrue(Directory.Exists(Path.GetDirectoryName(files.UserPath)));
            }

            using (var files = new ProfileFiles())
            {
                IReadOnlyDictionary<string, HookCompatibilityProfile> profiles =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        userProfilesPath: null);

                AssertEmbeddedForzaProfile(profiles);
                Assert.IsFalse(File.Exists(files.ApplicationPath));
                Assert.IsFalse(File.Exists(files.UserPath));
                Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(files.ApplicationPath)));
                Assert.IsFalse(Directory.Exists(Path.GetDirectoryName(files.UserPath)));
            }
        }

        [TestMethod]
        public async Task ConcurrentLoads_CreateOneValidEmptyUserTemplate()
        {
            using (var files = new ProfileFiles())
            {
                var loads = new Task<IReadOnlyDictionary<string, HookCompatibilityProfile>>[4];
                for (int index = 0; index < loads.Length; index++)
                {
                    loads[index] = Task.Run(() => HookCompatibilityProfileCatalog.LoadProfiles(
                        files.ApplicationPath, files.UserPath));
                }

                IReadOnlyDictionary<string, HookCompatibilityProfile>[] results =
                    await Task.WhenAll(loads);

                foreach (IReadOnlyDictionary<string, HookCompatibilityProfile> profiles in results)
                    AssertEmbeddedForzaProfile(profiles);
                AssertEmptyProfileFile(files.UserPath);
                Assert.IsFalse(File.Exists(files.ApplicationPath));
                CollectionAssert.AreEquivalent(new[] { files.UserPath },
                    Directory.GetFiles(Path.GetDirectoryName(files.UserPath)));
            }
        }

        [TestMethod]
        public void FailedUserTemplateCreation_KeepsEmbeddedAndApplicationProfiles()
        {
            using (var files = new ProfileFiles())
            {
                files.WriteApplication(Document(
                    "<Profile executable=\"ApplicationGame.exe\" injectionDelayMilliseconds=\"31\" />"));
                string blockedDirectory = Path.GetDirectoryName(files.UserPath);
                const string blockingFileContent = "Existing file prevents configuration directory creation.";
                File.WriteAllText(blockedDirectory, blockingFileContent);

                IReadOnlyDictionary<string, HookCompatibilityProfile> profiles =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        files.UserPath);

                AssertEmbeddedForzaProfile(profiles);
                Assert.AreEqual(TimeSpan.FromMilliseconds(31),
                    GetProfile(profiles, "ApplicationGame.exe").InjectionDelay);
                Assert.AreEqual(blockingFileContent, File.ReadAllText(blockedDirectory));
                Assert.IsFalse(File.Exists(files.UserPath));
            }
        }

        [TestMethod]
        public void ExternalAdditions_RetainEmbeddedProfilesAndBothSources()
        {
            using (var files = new ProfileFiles())
            {
                string applicationXml = Document(
                    "<Profile executable=\"ApplicationGame.exe\" enableGenericD3D12PresentRoute=\"true\" />");
                string userXml = Document(
                    "<Profile executable=\"UserGame.exe\" injectionDelayMilliseconds=\"321\" />");
                files.WriteApplication(applicationXml);
                files.WriteUser(userXml);

                IReadOnlyDictionary<string, HookCompatibilityProfile> profiles =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        files.UserPath);

                AssertEmbeddedForzaProfile(profiles);
                Assert.IsTrue(GetProfile(profiles, "ApplicationGame").EnableGenericD3D12PresentRoute);
                Assert.AreEqual(TimeSpan.FromMilliseconds(321),
                    GetProfile(profiles, "UserGame.exe").InjectionDelay);
                Assert.AreEqual(applicationXml, File.ReadAllText(files.ApplicationPath));
                Assert.AreEqual(userXml, File.ReadAllText(files.UserPath));
            }
        }

        [TestMethod]
        public void ApplicationProfile_FullyReplacesSameEmbeddedExecutable()
        {
            using (var files = new ProfileFiles())
            {
                files.WriteApplication(Document(
                    "<Profile executable=\"FORZAHORIZON6.EXE\" injectionDelayMilliseconds=\"99\" source=\"application\" />"));

                IReadOnlyDictionary<string, HookCompatibilityProfile> profiles =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        files.UserPath);
                HookCompatibilityProfile profile = GetProfile(profiles,
                    @"C:\Games\ForzaHorizon6.exe");

                Assert.AreEqual(NativeHookCompatibilityFlags.None, profile.NativeFlags);
                Assert.IsFalse(profile.EnableGenericD3D12PresentRoute);
                Assert.IsFalse(profile.DisableFidelityFxSwapchainLifecycleHooks);
                Assert.AreEqual(TimeSpan.FromMilliseconds(99), profile.InjectionDelay);
                Assert.AreEqual("application", profile.Source);
            }
        }

        [TestMethod]
        public void UserProfile_HasHighestPriorityAndResetsOmittedSettings()
        {
            using (var files = new ProfileFiles())
            {
                files.WriteApplication(Document(
                    "<Profile executable=\"ForzaHorizon6.exe\" enableXeFgNativePresentQueueRoute=\"true\" " +
                    "enableGenericD3D12PresentRoute=\"true\" disableFidelityFxSwapchainLifecycleHooks=\"true\" " +
                    "injectionDelayMilliseconds=\"500\" earlyInjectionModule=\"d3d12.dll\" source=\"application\" />"));
                files.WriteUser(Document(
                    "<Profile executable=\"C:\\Games\\FORZAHORIZON6.EXE\" injectionDelayMilliseconds=\"7\" source=\"user\" />"));

                IReadOnlyDictionary<string, HookCompatibilityProfile> profiles =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        files.UserPath);
                HookCompatibilityProfile profile = GetProfile(profiles, "forzahorizon6");

                Assert.AreEqual(NativeHookCompatibilityFlags.None, profile.NativeFlags);
                Assert.IsFalse(profile.EnableXeFgNativePresentQueueRoute);
                Assert.IsFalse(profile.EnableGenericD3D12PresentRoute);
                Assert.IsFalse(profile.DisableFidelityFxSwapchainLifecycleHooks);
                Assert.IsFalse(profile.RequiresEarlyInjection);
                Assert.IsNull(profile.EarlyInjectionModule);
                Assert.AreEqual(TimeSpan.FromMilliseconds(7), profile.InjectionDelay);
                Assert.AreEqual("user", profile.Source);
            }
        }

        [DataTestMethod]
        [DataRow("<HookCompatibilityProfiles version=\"1\"><Profile executable=\"Discarded.exe\" injectionDelayMilliseconds=\"1\" /><Profile executable=\"Broken.exe\" enableGenericD3D12PresentRoute=\"invalid\" /></HookCompatibilityProfiles>")]
        [DataRow("<HookCompatibilityProfiles version=\"2\"><Profile executable=\"Discarded.exe\" injectionDelayMilliseconds=\"1\" /></HookCompatibilityProfiles>")]
        [DataRow("<HookCompatibilityProfiles version=\"1\"><Profile executable=\"Discarded.exe\" injectionDelayMilliseconds=\"1\" /><Profile executable=\"DISCARDED.EXE\" injectionDelayMilliseconds=\"2\" /></HookCompatibilityProfiles>")]
        public void InvalidApplicationSource_IsIgnoredAtomicallyWhileUserSourceLoads(string invalidXml)
        {
            using (var files = new ProfileFiles())
            {
                files.WriteApplication(invalidXml);
                files.WriteUser(Document(
                    "<Profile executable=\"RetainedUser.exe\" injectionDelayMilliseconds=\"9\" />"));

                IReadOnlyDictionary<string, HookCompatibilityProfile> profiles =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        files.UserPath);

                AssertEmbeddedForzaProfile(profiles);
                Assert.IsFalse(HookCompatibilityProfileCatalog.TryGet("Discarded.exe", out _, profiles));
                Assert.AreEqual(TimeSpan.FromMilliseconds(9),
                    GetProfile(profiles, "RetainedUser.exe").InjectionDelay);
                Assert.AreEqual(invalidXml, File.ReadAllText(files.ApplicationPath));
            }
        }

        [DataTestMethod]
        [DataRow("<HookCompatibilityProfiles version=\"1\"><Profile executable=\"Discarded.exe\" injectionDelayMilliseconds=\"1\" /><Profile executable=\"Broken.exe\" /> </HookCompatibilityProfiles>")]
        [DataRow("<HookCompatibilityProfiles version=\"1\"><Profile executable=\"Discarded.exe\" injectionDelayMilliseconds=\"1\" />")]
        [DataRow("<HookCompatibilityProfiles version=\"2\"><Profile executable=\"Discarded.exe\" injectionDelayMilliseconds=\"1\" /></HookCompatibilityProfiles>")]
        public void InvalidUserSource_KeepsValidApplicationOverridesWithoutPartialMerge(string invalidXml)
        {
            using (var files = new ProfileFiles())
            {
                files.WriteApplication(Document(
                    "<Profile executable=\"ForzaHorizon6.exe\" injectionDelayMilliseconds=\"27\" />"));
                files.WriteUser(invalidXml);

                IReadOnlyDictionary<string, HookCompatibilityProfile> profiles =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        files.UserPath);

                HookCompatibilityProfile profile = GetProfile(profiles, "ForzaHorizon6.exe");
                Assert.AreEqual(TimeSpan.FromMilliseconds(27), profile.InjectionDelay);
                Assert.AreEqual(NativeHookCompatibilityFlags.None, profile.NativeFlags);
                Assert.IsFalse(HookCompatibilityProfileCatalog.TryGet("Discarded.exe", out _, profiles));
                Assert.AreEqual(invalidXml, File.ReadAllText(files.UserPath));
            }
        }

        [TestMethod]
        public void UnreadableExternalFile_KeepsEmbeddedAndOtherSourceProfiles()
        {
            using (var files = new ProfileFiles())
            {
                files.WriteApplication(Document(
                    "<Profile executable=\"LockedGame.exe\" injectionDelayMilliseconds=\"1\" />"));
                files.WriteUser(Document(
                    "<Profile executable=\"ReadableGame.exe\" injectionDelayMilliseconds=\"2\" />"));

                using (File.Open(files.ApplicationPath, FileMode.Open, FileAccess.ReadWrite,
                    FileShare.None))
                {
                    IReadOnlyDictionary<string, HookCompatibilityProfile> profiles =
                        HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                            files.UserPath);

                    AssertEmbeddedForzaProfile(profiles);
                    Assert.IsFalse(HookCompatibilityProfileCatalog.TryGet("LockedGame.exe", out _, profiles));
                    Assert.AreEqual(TimeSpan.FromMilliseconds(2),
                        GetProfile(profiles, "ReadableGame.exe").InjectionDelay);
                }
            }
        }

        [TestMethod]
        public void EarlyInjectionProfiles_UseBothSourcesAndRespectFullReplacement()
        {
            using (var files = new ProfileFiles())
            {
                files.WriteApplication(Document(
                    "<Profile executable=\"ApplicationEarly.exe\" earlyInjectionModule=\"d3d12.dll\" />"));
                files.WriteUser(Document(
                    "<Profile executable=\"UserEarly.exe\" earlyInjectionModule=\"dxgi.dll\" />" +
                    "<Profile executable=\"tlou-ii.exe\" injectionDelayMilliseconds=\"3\" />"));

                IReadOnlyDictionary<string, HookCompatibilityProfile> profiles =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        files.UserPath);
                var earlyProfiles = new List<HookCompatibilityProfile>(
                    HookCompatibilityProfileCatalog.GetEarlyInjectionProfiles(profiles));

                CollectionAssert.Contains(earlyProfiles, GetProfile(profiles, "ApplicationEarly.exe"));
                CollectionAssert.Contains(earlyProfiles, GetProfile(profiles, "UserEarly.exe"));
                CollectionAssert.DoesNotContain(earlyProfiles, GetProfile(profiles, "tlou-ii.exe"));
                CollectionAssert.Contains(earlyProfiles, GetProfile(profiles, "JediSurvivor.exe"));
            }
        }

        [TestMethod]
        public void ProfileSnapshot_ChangesOnlyWhenLoadedAgain()
        {
            using (var files = new ProfileFiles())
            {
                files.WriteUser(Document(
                    "<Profile executable=\"SnapshotGame.exe\" injectionDelayMilliseconds=\"11\" />"));
                IReadOnlyDictionary<string, HookCompatibilityProfile> original =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        files.UserPath);

                files.WriteUser(Document(
                    "<Profile executable=\"SnapshotGame.exe\" enableGenericD3D12PresentRoute=\"true\" />"));
                IReadOnlyDictionary<string, HookCompatibilityProfile> reloaded =
                    HookCompatibilityProfileCatalog.LoadProfiles(files.ApplicationPath,
                        files.UserPath);

                Assert.AreEqual(TimeSpan.FromMilliseconds(11),
                    GetProfile(original, "SnapshotGame.exe").InjectionDelay);
                Assert.AreEqual(NativeHookCompatibilityFlags.None,
                    GetProfile(original, "SnapshotGame.exe").NativeFlags);
                Assert.AreEqual(TimeSpan.Zero, GetProfile(reloaded, "SnapshotGame.exe").InjectionDelay);
                Assert.IsTrue(GetProfile(reloaded, "SnapshotGame.exe").EnableGenericD3D12PresentRoute);
            }
        }

        private static string Document(string profiles)
        {
            return "<HookCompatibilityProfiles version=\"1\">" + profiles +
                "</HookCompatibilityProfiles>";
        }

        private static HookCompatibilityProfile GetProfile(
            IReadOnlyDictionary<string, HookCompatibilityProfile> profiles, string executable)
        {
            Assert.IsTrue(HookCompatibilityProfileCatalog.TryGet(executable,
                out HookCompatibilityProfile profile, profiles), executable);
            return profile;
        }

        private static void AssertEmbeddedForzaProfile(
            IReadOnlyDictionary<string, HookCompatibilityProfile> profiles)
        {
            HookCompatibilityProfile profile = GetProfile(profiles, "ForzaHorizon6.exe");
            Assert.IsTrue(profile.EnableGenericD3D12PresentRoute);
            Assert.IsTrue(profile.DisableFidelityFxSwapchainLifecycleHooks);
        }

        private static void AssertEmptyProfileFile(string path)
        {
            Assert.IsTrue(File.Exists(path));
            using (Stream stream = File.OpenRead(path))
                Assert.AreEqual(0, HookCompatibilityProfileCatalog.ParseProfiles(stream).Count);
        }

        private sealed class ProfileFiles : IDisposable
        {
            private readonly string _testRoot = Path.GetFullPath(Path.Combine(
                Path.GetTempPath(), "CapFrameX.HookCompatibilityExternalProfilesTests"));
            private readonly string _directory;

            internal ProfileFiles()
            {
                _directory = Path.Combine(_testRoot, Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(_directory);
                ApplicationPath = Path.Combine(_directory, "application", "HookCompatibilityProfiles.xml");
                UserPath = Path.Combine(_directory, "configuration", "HookCompatibilityProfiles.xml");
            }

            internal string ApplicationPath { get; }
            internal string UserPath { get; }

            internal void WriteApplication(string xml)
            {
                Write(ApplicationPath, xml);
            }

            internal void WriteUser(string xml)
            {
                Write(UserPath, xml);
            }

            public void Dispose()
            {
                string directory = Path.GetFullPath(_directory);
                if (!directory.StartsWith(_testRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Test cleanup path escaped its temporary directory.");
                if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            }

            private static void Write(string path, string xml)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, xml);
            }
        }
    }
}
