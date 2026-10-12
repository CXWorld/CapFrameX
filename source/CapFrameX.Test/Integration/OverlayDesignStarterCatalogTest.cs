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
        public void CatalogUpgradeAddsOnlyNewDesignsAndNeverResurrectsDeletedStarters()
        {
            var oldStarter = new OverlayDesignStarterProfile("old-fixture", Design("minimal", 400), 1);
            var newStarter = new OverlayDesignStarterProfile("new-fixture", Design("compact", 480), 2);
            var store = Open();
            var custom = store.Create("My design", Design("benchmark", 640));
            Assert.IsTrue(store.EnsureStarterProfiles(new[] { oldStarter }, 1));
            store.Delete(oldStarter.Id);
            store.SetActive(custom.Id);
            Assert.IsTrue(store.EnsureStarterProfiles(new[] { oldStarter, newStarter }, 2));
            Assert.AreEqual(2, store.Profiles.Count);
            Assert.IsFalse(store.Profiles.Any(profile => profile.Id == oldStarter.Id));
            Assert.IsTrue(store.Profiles.Any(profile => profile.Id == newStarter.Id));
            Assert.AreEqual(custom.Id, store.ActiveProfileId);
            store.Delete(newStarter.Id);
            var reopened = Open();
            Assert.IsTrue(reopened.EnsureStarterProfiles(new[] { oldStarter, newStarter }, 2));
            Assert.AreEqual(1, reopened.Profiles.Count);
        }

        [TestMethod]
        public void VersionTwoUpgradeAddsExactlyThreeCoreTypeProfilesAndPreservesTheExistingLibrary()
        {
            var catalog = OverlayDesignStarterCatalog.Profiles;
            var newStarters = catalog.Where(profile => profile.IntroducedInVersion == 3).ToArray();
            CollectionAssert.AreEquivalent(new[] { "core-types-overview", "core-types-compact", "core-types-thermal" },
                newStarters.Select(profile => profile.PresetId).ToArray());
            Assert.AreEqual(3, OverlayDesignStarterCatalog.Version);

            var store = Open();
            Assert.IsTrue(store.EnsureStarterProfiles(catalog.Where(profile => profile.IntroducedInVersion <= 2).ToArray(), 2));
            var deleted = catalog.Where(profile => profile.PresetId == "compact" || profile.PresetId == "turquoise-compact").ToArray();
            foreach (var starter in deleted) store.Delete(starter.Id);
            var edited = catalog.Single(profile => profile.PresetId == "benchmark");
            store.Rename(edited.Id, "My edited benchmark");
            store.Save(edited.Id, Design("benchmark", 920));
            store.Create("My custom capture", Design("minimal", 520));
            store.SetActive(edited.Id);
            var beforeUpgrade = store.Profiles.ToArray();
            Assert.AreEqual(2, (int)JObject.Parse(File.ReadAllText(LibraryPath))["StarterCatalogVersion"]);

            var upgraded = Open();
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(upgraded));
            Assert.AreEqual(beforeUpgrade.Length + 3, upgraded.Profiles.Count);
            CollectionAssert.AreEquivalent(newStarters.Select(profile => profile.Id).ToArray(),
                upgraded.Profiles.Where(profile => beforeUpgrade.All(previous => previous.Id != profile.Id))
                    .Select(profile => profile.Id).ToArray());
            foreach (var previous in beforeUpgrade)
            {
                Assert.AreEqual(previous.Name, upgraded.Get(previous.Id).Name);
                Assert.AreEqual(previous.DesignJson, upgraded.Get(previous.Id).DesignJson);
            }
            Assert.IsFalse(upgraded.Profiles.Any(profile => deleted.Any(starter => starter.Id == profile.Id)),
                "Deleted designs from catalog versions one and two must remain deleted.");
            Assert.AreEqual(edited.Id, upgraded.ActiveProfileId);
            Assert.AreEqual(3, (int)JObject.Parse(File.ReadAllText(LibraryPath))["StarterCatalogVersion"]);

            string upgradedBytes = File.ReadAllText(LibraryPath);
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(upgraded));
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(Open()));
            Assert.AreEqual(upgradedBytes, File.ReadAllText(LibraryPath), "Repeated upgrades must not rewrite or duplicate profiles.");

            foreach (var starter in newStarters) upgraded.Delete(starter.Id);
            string deletedBytes = File.ReadAllText(LibraryPath);
            var reopened = Open();
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(reopened));
            Assert.AreEqual(beforeUpgrade.Length, reopened.Profiles.Count);
            Assert.IsFalse(reopened.Profiles.Any(profile => newStarters.Concat(deleted).Any(starter => starter.Id == profile.Id)));
            Assert.AreEqual(edited.Id, reopened.ActiveProfileId);
            Assert.AreEqual(deletedBytes, File.ReadAllText(LibraryPath), "Deleted version-three starters must not be installed again.");
        }

        [TestMethod]
        [DataRow("core-types-overview")]
        [DataRow("core-types-compact")]
        [DataRow("core-types-thermal")]
        public void CoreTypeStarterMatchesTheCanonicalGalleryExport(string presetId)
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
            var gallery = OsdDesignPresets.Find(presetId).CreateDocument();
            Assert.AreEqual(3, starter.IntroducedInVersion);
            Assert.IsTrue(gallery.IsCanvas, "Core-type starters must be immediately editable in the tile placement grid.");
            Assert.AreEqual(gallery.Name, starter.Name);
            Assert.IsTrue(JToken.DeepEquals(JToken.Parse(gallery.ToJson()), JToken.Parse(starter.DesignJson)),
                "The embedded starter must be exported from the same gallery document without binding example hardware.");
            var reopened = OsdDesignDocument.FromJson(starter.DesignJson);
            Assert.IsTrue(JToken.DeepEquals(JToken.Parse(gallery.ToJson()), JToken.Parse(reopened.ToJson())),
                "The saved starter must retain the gallery document when reopened.");
        }

        [TestMethod]
        public void FreshLibraryContainsEditableSavedDesignsWithoutOpeningTheDesigner()
        {
            var store = Open();
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(store));
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count, store.Profiles.Count);
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
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count, Open().Profiles.Count);
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
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count + 1, store.Profiles.Count);
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
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count - 1, reopened.Profiles.Count);
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
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count, serviceStore.Profiles.Count);
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count, editorStore.Profiles.Count);
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count, Open().Profiles.Count);
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
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count + 1, store.Profiles.Count);
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
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count + 1, recovered.Profiles.Count);
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
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count + 1, store.Profiles.Count);
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
