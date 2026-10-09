using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayDesignStarterCatalogTest
    {
        private string _folder;
        private string LibraryPath => Path.Combine(_folder, "OverlayDesigns", "profiles.json");

        [TestInitialize]
        public void Initialize() => _folder = Path.Combine(Path.GetTempPath(), "CapFrameX-starter-design-tests", Guid.NewGuid().ToString("N"));

        [TestCleanup]
        public void Cleanup()
        {
            if (File.Exists(LibraryPath)) File.SetAttributes(LibraryPath, FileAttributes.Normal);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [TestMethod]
        public void FreshLibraryContainsTwelveEditableSavedDesignsWithoutOpeningTheDesigner()
        {
            var store = Open();
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(store));
            Assert.AreEqual(12, store.Profiles.Count);
            Assert.AreEqual("Benchmark", store.Get(store.ActiveProfileId).Name);
            Assert.IsNull(store.StarterInitializationWarning);
            CollectionAssert.AreEqual(OverlayDesignStarterCatalog.Profiles.Select(p => p.Id).ToArray(), store.Profiles.Select(p => p.Id).ToArray());
            foreach (var profile in store.Profiles)
            {
                var document = OsdDesignDocument.FromJson(profile.DesignJson);
                Assert.IsTrue(document.IsCanvas, profile.Name + " must support immediate tile placement.");
                Assert.AreEqual(profile.Name, document.Name);
                Assert.IsTrue(OverlayDesignStarterCatalog.IsUnmodifiedStarter(profile));
                var runtime = OverlayRuntimeDesign.Parse(profile.Id, profile.Name, profile.DesignJson);
                Assert.IsNotNull(runtime.TemplateJson);
            }
            Assert.AreEqual(12, Open().Profiles.Count);
            Assert.AreEqual(OverlayDesignStarterCatalog.Version, (int)JObject.Parse(File.ReadAllText(LibraryPath))["StarterCatalogVersion"]);
        }

        [TestMethod]
        public void MigratingExistingLibraryPreservesCustomSameNameContentsAndSelection()
        {
            var store = Open();
            var benchmark = store.Create("Benchmark", Design("compact", 900));
            var custom = store.Create("My capture", Design("minimal", 720));
            string original = benchmark.DesignJson;
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(store));
            Assert.AreEqual(custom.Id, store.ActiveProfileId);
            Assert.AreEqual(original, store.Get(benchmark.Id).DesignJson);
            Assert.AreEqual(1, store.Profiles.Count(p => p.Name == "Benchmark"));
            Assert.AreEqual(13, store.Profiles.Count);
            Assert.IsFalse(OverlayDesignStarterCatalog.IsUnmodifiedStarter(benchmark));
        }

        [TestMethod]
        public void DeletedRenamedAndEditedStartersStayThatWayAfterReopening()
        {
            var store = Open();
            OverlayDesignStarterCatalog.EnsureProfiles(store);
            var compact = store.Profiles.Single(p => p.Name == "Compact");
            store.Delete(compact.Id);
            var benchmark = store.Profiles.Single(p => p.Name == "Benchmark");
            store.Rename(benchmark.Id, "My benchmark");
            store.Save(benchmark.Id, Design("benchmark", 920));
            store.SetActive(benchmark.Id);
            string bytesBefore = File.ReadAllText(LibraryPath);

            var reopened = Open();
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(reopened));
            Assert.AreEqual(11, reopened.Profiles.Count);
            Assert.IsFalse(reopened.Profiles.Any(p => p.Id == compact.Id || p.Name == "Benchmark"));
            Assert.AreEqual(920, OsdDesignDocument.FromJson(reopened.Get(benchmark.Id).DesignJson).Width);
            Assert.AreEqual(benchmark.Id, reopened.ActiveProfileId);
            Assert.AreEqual(bytesBefore, File.ReadAllText(LibraryPath), "An initialized library must not be rewritten on startup.");
        }

        [TestMethod]
        public void ConcurrentServiceAndEditorBootstrapCreateOneCatalogAndBothSeeIt()
        {
            var serviceStore = Open();
            var editorStore = Open();
            Task.WaitAll(Task.Run(() => Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(serviceStore))),
                Task.Run(() => Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(editorStore))));
            Assert.AreEqual(12, serviceStore.Profiles.Count);
            Assert.AreEqual(12, editorStore.Profiles.Count);
            Assert.AreEqual(12, Open().Profiles.Count);
            CollectionAssert.AreEqual(serviceStore.Profiles.Select(p => p.Id).ToArray(), editorStore.Profiles.Select(p => p.Id).ToArray());
        }

        [TestMethod]
        public void ReadOnlyLibraryStillLoadsItsExistingProfilesAndCanRetryBootstrapLater()
        {
            var store = Open();
            var custom = store.Create("My rows and tiles", Design("hybrid-compact", 480));
            string original = File.ReadAllText(LibraryPath);
            File.SetAttributes(LibraryPath, FileAttributes.ReadOnly);
            try
            {
                Assert.IsFalse(OverlayDesignStarterCatalog.EnsureProfiles(store));
                Assert.IsNotNull(store.StarterInitializationWarning);
                Assert.AreEqual(custom.Id, store.ActiveProfileId);
                Assert.AreEqual(1, store.Profiles.Count);
                Assert.AreEqual(original, File.ReadAllText(LibraryPath));
            }
            finally { File.SetAttributes(LibraryPath, FileAttributes.Normal); }
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(store));
            Assert.AreEqual(13, store.Profiles.Count);
            Assert.IsNull(store.StarterInitializationWarning);
        }

        [TestMethod]
        public void CorruptLibraryAndRecoveryBackupAreNeverReplacedByAutomaticStarters()
        {
            var store = Open();
            var custom = store.Create("Custom", Design("minimal", 400));
            store.Save(custom.Id, Design("minimal", 440));
            File.WriteAllText(LibraryPath, "broken primary");
            var recovered = Open();
            Assert.IsFalse(OverlayDesignStarterCatalog.EnsureProfiles(recovered));
            Assert.IsNotNull(recovered.RecoveryWarning);
            Assert.IsNotNull(recovered.StarterInitializationWarning);
            Assert.AreEqual("broken primary", File.ReadAllText(LibraryPath));
            Assert.AreEqual(400, OsdDesignDocument.FromJson(recovered.Get(custom.Id).DesignJson).Width);
            recovered.Save(custom.Id, recovered.Get(custom.Id).DesignJson);
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(recovered));
            Assert.AreEqual(13, recovered.Profiles.Count);
            Assert.AreEqual(1, Directory.GetFiles(Path.GetDirectoryName(LibraryPath), "profiles.corrupt-*.json").Length);
        }

        [TestMethod]
        public void FailedCatalogValidationDoesNotPartiallyInstallProfilesOrAdvanceMarker()
        {
            bool rejectCompact = false;
            string Validate(string json)
            {
                var document = JObject.Parse(json);
                if (rejectCompact && (string)document["name"] == "Compact") throw new InvalidDataException("Fixture invalid starter.");
                return OverlayRuntimeDesign.Canonicalize(json);
            }
            var store = new OverlayDesignProfileStore(_folder, Validate);
            var original = store.Create("Custom", Design("minimal", 400));
            string originalBytes = File.ReadAllText(LibraryPath);
            rejectCompact = true;
            Assert.IsFalse(OverlayDesignStarterCatalog.EnsureProfiles(store));
            Assert.AreEqual(originalBytes, File.ReadAllText(LibraryPath));
            Assert.AreEqual(1, store.Profiles.Count);
            Assert.AreEqual(original.Id, store.ActiveProfileId);
            rejectCompact = false;
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(store));
            Assert.AreEqual(13, store.Profiles.Count);
        }

        [TestMethod]
        public void FullMigratedLibraryDoesNotExceedCapacityOrFillLaterUserDeletions()
        {
            var store = Open();
            store.Create("Custom 0", Design("minimal", 400));
            var library = JObject.Parse(File.ReadAllText(LibraryPath));
            var first = (JObject)library["Profiles"][0];
            var profiles = (JArray)library["Profiles"];
            for (int i = 1; i < OverlayDesignProfileStore.MaximumProfiles; i++)
            {
                var profile = (JObject)first.DeepClone();
                profile["Id"] = Guid.NewGuid().ToString("N"); profile["Name"] = "Custom " + i;
                profiles.Add(profile);
            }
            File.WriteAllText(LibraryPath, library.ToString(Formatting.None));
            store = Open();
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(store));
            Assert.AreEqual(128, store.Profiles.Count);
            store.Delete(store.Profiles.Last().Id);
            var reopened = Open();
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(reopened));
            Assert.AreEqual(127, reopened.Profiles.Count);
            Assert.IsTrue(reopened.Profiles.All(p => p.Name.StartsWith("Custom ")));
        }

        private OverlayDesignProfileStore Open() => new OverlayDesignProfileStore(_folder, OverlayRuntimeDesign.Canonicalize);
        private static string Design(string preset, double width)
        {
            var document = OsdDesignDocument.CreatePreset(preset); document.Width = width; return document.ToJson();
        }
    }
}
