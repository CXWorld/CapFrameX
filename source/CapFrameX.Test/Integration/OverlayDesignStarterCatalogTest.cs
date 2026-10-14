using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CapFrameX.Contracts.Sensor;
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
        [DataRow("original")]
        [DataRow("edited")]
        [DataRow("renamed")]
        [DataRow("deleted")]
        [DataRow("same-name")]
        public void CatalogCorrectionUpdatesOnlyTheKnownOriginalAndPreservesSelection(string state)
        {
            string originalJson = Design("minimal", 400);
            var original = new OverlayDesignStarterProfile("revision-fixture", originalJson, 4);
            var corrected = new OverlayDesignStarterProfile("revision-fixture", Design("minimal", 480), 4,
                OverlayDesignStarterProfile.ComputeDesignHash(originalJson));
            var store = Open();
            var custom = store.Create("My custom design", Design("benchmark", 640));
            Assert.IsTrue(store.EnsureStarterProfiles(new[] { original }, 4));
            store.SetActive(original.Id);
            if (state == "edited") store.Save(original.Id, Design("minimal", 420));
            if (state == "renamed") store.Rename(original.Id, "My renamed design");
            if (state == "deleted" || state == "same-name")
            {
                store.Delete(original.Id);
                store.SetActive(custom.Id);
            }
            if (state == "same-name") store.Create(original.Name, originalJson);
            var before = store.Profiles.ToArray();
            string selected = store.ActiveProfileId;

            var upgraded = Open();
            Assert.IsTrue(upgraded.EnsureStarterProfiles(new[] { corrected }, 5));
            Assert.AreEqual(before.Length, upgraded.Profiles.Count);
            Assert.AreEqual(selected, upgraded.ActiveProfileId);
            foreach (var profile in before)
            {
                Assert.AreEqual(profile.Name, upgraded.Get(profile.Id).Name);
                Assert.AreEqual(state == "original" && profile.Id == original.Id ? corrected.DesignJson : profile.DesignJson,
                    upgraded.Get(profile.Id).DesignJson);
            }
            Assert.AreEqual(5, (int)JObject.Parse(File.ReadAllText(LibraryPath))["StarterCatalogVersion"]);
            string updatedBytes = File.ReadAllText(LibraryPath);
            Assert.IsTrue(Open().EnsureStarterProfiles(new[] { corrected }, 5));
            Assert.AreEqual(updatedBytes, File.ReadAllText(LibraryPath));
        }

        [TestMethod]
        [DataRow(2, 3)]
        [DataRow(3, 4)]
        [DataRow(5, 6)]
        [DataRow(6, 7)]
        public void VersionUpgradeAddsOnlyNewProfilesAndPreservesTheExistingLibrary(int previousVersion, int targetVersion)
        {
            var catalog = OverlayDesignStarterCatalog.Profiles.Where(profile => profile.IntroducedInVersion <= targetVersion).ToArray();
            var newStarters = catalog.Where(profile => profile.IntroducedInVersion == targetVersion).ToArray();
            string[] expectedIds = targetVersion switch
            {
                3 => new[] { "core-types-overview", "core-types-compact", "core-types-thermal" },
                4 => new[] { "RTX-5090-9800X3D-APEX", "AMD-Ryzen-Radeon-REDLINE", "RTX-5090-Core-Ultra-285K-FUSION",
                    "AMD-Ryzen-5-9600X-Badge", "AMD-Ryzen-7-9800X3D-Badge" },
                6 => new[] { "Panther-Lake-Arc-Aurora", "OLED-Minimal" },
                _ => new[] { "NVIDIA-GeForce-RTX-GREENLINE", "ICELINE-Vertical" }
            };
            CollectionAssert.AreEquivalent(expectedIds,
                newStarters.Select(profile => profile.PresetId).ToArray());

            bool EnsureProfiles(OverlayDesignProfileStore library) => targetVersion == OverlayDesignStarterCatalog.Version
                ? OverlayDesignStarterCatalog.EnsureProfiles(library)
                : library.EnsureStarterProfiles(catalog, targetVersion);

            var store = Open();
            Assert.IsTrue(store.EnsureStarterProfiles(catalog.Where(profile => profile.IntroducedInVersion <= previousVersion).ToArray(), previousVersion));
            var deleted = catalog.Where(profile => profile.PresetId == "compact" || profile.PresetId == "turquoise-compact").ToArray();
            foreach (var starter in deleted) store.Delete(starter.Id);
            var edited = catalog.Single(profile => profile.PresetId == "benchmark");
            store.Rename(edited.Id, "My edited benchmark");
            store.Save(edited.Id, Design("benchmark", 920));
            store.Create("My custom capture", Design("minimal", 520));
            store.SetActive(edited.Id);
            var beforeUpgrade = store.Profiles.ToArray();
            Assert.AreEqual(previousVersion, (int)JObject.Parse(File.ReadAllText(LibraryPath))["StarterCatalogVersion"]);

            var upgraded = Open();
            Assert.IsTrue(EnsureProfiles(upgraded));
            Assert.AreEqual(beforeUpgrade.Length + expectedIds.Length, upgraded.Profiles.Count);
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
            Assert.AreEqual(targetVersion, (int)JObject.Parse(File.ReadAllText(LibraryPath))["StarterCatalogVersion"]);

            string upgradedBytes = File.ReadAllText(LibraryPath);
            Assert.IsTrue(EnsureProfiles(upgraded));
            Assert.IsTrue(EnsureProfiles(Open()));
            Assert.AreEqual(upgradedBytes, File.ReadAllText(LibraryPath), "Repeated upgrades must not rewrite or duplicate profiles.");

            foreach (var starter in newStarters) upgraded.Delete(starter.Id);
            string deletedBytes = File.ReadAllText(LibraryPath);
            var reopened = Open();
            Assert.IsTrue(EnsureProfiles(reopened));
            Assert.AreEqual(beforeUpgrade.Length, reopened.Profiles.Count);
            Assert.IsFalse(reopened.Profiles.Any(profile => newStarters.Concat(deleted).Any(starter => starter.Id == profile.Id)));
            Assert.AreEqual(edited.Id, reopened.ActiveProfileId);
            Assert.AreEqual(deletedBytes, File.ReadAllText(LibraryPath), "Deleted starters must not be installed again.");
        }

        [TestMethod]
        [DataRow("RTX-5090-9800X3D-APEX", "APEX - RTX 5090 + 9800X3D", false, 4)]
        [DataRow("AMD-Ryzen-Radeon-REDLINE", "REDLINE - Ryzen 7 9800X3D + Radeon", true, 4)]
        [DataRow("RTX-5090-Core-Ultra-285K-FUSION", "FUSION - RTX 5090 + Core Ultra 9 285K", true, 4)]
        [DataRow("AMD-Ryzen-5-9600X-Badge", "AMD Ryzen 5 9600X Badge", true, 4)]
        [DataRow("AMD-Ryzen-7-9800X3D-Badge", "AMD Ryzen 7 9800X3D Badge", true, 4)]
        [DataRow("Panther-Lake-Arc-Aurora", "Panther Lake - Arc Aurora", false, 6)]
        [DataRow("OLED-Minimal", "OLED Minimal", false, 6)]
        [DataRow("NVIDIA-GeForce-RTX-GREENLINE", "GREENLINE - GeForce RTX", true, 7)]
        [DataRow("ICELINE-Vertical", "ICELINE - Vertical", true, 7)]
        public void HardwareStarterCanBeEditedAndRenderedWithItsEmbeddedGraphics(string presetId, string name, bool hasImages, int introducedInVersion)
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
            Assert.AreEqual(introducedInVersion, starter.IntroducedInVersion);
            Assert.AreEqual(name, starter.Name);
            var document = OsdDesignDocument.FromJson(starter.DesignJson);
            Assert.IsTrue(document.IsCanvas);
            Assert.AreEqual(name, document.Name);
            var original = OverlayRuntimeDesign.Parse(starter.Id, starter.Name, starter.DesignJson);
            var runtime = OverlayRuntimeDesign.Parse(starter.Id, starter.Name, document.ToJson());
            CollectionAssert.AreEqual(original.MetricKeys.ToArray(), runtime.MetricKeys.ToArray());
            Assert.AreEqual(original.PixelShift, runtime.PixelShift, "Editing must retain the design's pixel orbit request.");
            string[] Images(string json) => JObject.Parse(json).Descendants().OfType<JProperty>()
                .Where(property => property.Name == "dataUri").Select(property => (string)property.Value).ToArray();
            // Compare renderer scenes: editor metadata contains additional copies of each image.
            // APEX, Panther Lake and OLED Minimal use text and vector elements only, while the
            // other hardware profiles embed PNGs.
            var images = Images(original.TemplateJson);
            Assert.AreEqual(hasImages, images.Length > 0);
            Assert.IsTrue(images.All(uri => uri.StartsWith("data:image/png;base64,", StringComparison.Ordinal)));
            CollectionAssert.AreEqual(images, Images(runtime.TemplateJson), "Editing and rendering must retain every embedded image.");
        }

        [TestMethod]
        public void FusionStarterBreaksDownThe285KByCoreType()
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == "RTX-5090-Core-Ultra-285K-FUSION");
            var document = OsdDesignDocument.FromJson(starter.DesignJson);
            string Key(string type, string name) => "sensor:" + Uri.EscapeDataString(
                SensorIdentifierHelper.BuildStableIdentifier("Intel Core Ultra 9 285K", type, name));
            // Arrow Lake enumerates its P-cores between the E-core clusters.
            var coreTypes = new[]
            {
                (Label: "P-CORES", Type: "P", Cores: new[] { 1, 2, 11, 12, 13, 14, 23, 24 }),
                (Label: "E-CORES", Type: "E", Cores: Enumerable.Range(3, 8).Concat(Enumerable.Range(15, 8)).ToArray())
            };
            foreach (var (label, type, cores) in coreTypes)
            {
                var group = document.Tiles.Single(tile => tile.Kind == "group" && tile.Label == label);
                CollectionAssert.AreEqual(cores.Select(core => Key("Load", $"Core #{core} {type}")).ToArray(),
                    group.GroupMembers.Select(member => member.Key).ToArray());
                Assert.IsTrue(group.GroupMembers.All(member => member.IsEnabled && member.CoreType == type));
                Assert.AreEqual(1, document.GetGroupBarLayout(group).Rows, label + " must remain a single row of bars.");
            }

            var keys = OverlayRuntimeDesign.Parse(starter.Id, starter.Name, starter.DesignJson).MetricKeys.ToArray();
            CollectionAssert.IsSubsetOf(new[] { Key("Clock", "CPU P Max"), Key("Clock", "CPU E Max") }, keys);
            CollectionAssert.DoesNotContain(keys, "cpuClock", "The core-type max clocks replace the combined CPU max clock.");
        }

        [TestMethod]
        [DataRow("NVIDIA-GeForce-RTX-GREENLINE", new[] { "gpuName" })]
        [DataRow("ICELINE-Vertical", new[] { "gpuName", "cpuName" })]
        public void GenericStarterNamesTheInstalledHardwareAndStaysPortable(string presetId, string[] nameKeys)
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
            var document = OsdDesignDocument.FromJson(starter.DesignJson);
            var tiles = document.EnumerateTiles().ToArray();
            foreach (string key in nameKeys)
                Assert.AreEqual(1, tiles.Count(tile => tile.Kind == "metric" && tile.Key == key), key + " must show the name the system reports.");
            // The native metric draws text readouts in the theme text colour whatever their value
            // colour, so each one declares that colour and the editor shows what is rendered.
            foreach (var readout in tiles.Where(tile => tile.Kind == "metric" && new[] { "gpuName", "cpuName", "graphicsApi" }.Contains(tile.Key)))
                Assert.AreEqual(document.TextColor, readout.ValueColor, readout.Key);
            Assert.IsFalse(tiles.Any(tile => Regex.IsMatch(tile.Label + " " + tile.Text, @"\b(?:[RG]TX|RX)\s*\d{3,4}\b|\bi[3579]-\d{4}|\bRyzen\s+\d")),
                "No tile may name a specific CPU or GPU model.");
            var keys = OverlayRuntimeDesign.Parse(starter.Id, starter.Name, starter.DesignJson).MetricKeys;
            Assert.IsFalse(keys.Any(key => key.StartsWith("sensor", StringComparison.Ordinal)),
                "Semantic keys bind on any GPU and CPU; hardware sensor keys would tie the design to one system.");
        }

        [TestMethod]
        [DataRow("NVIDIA-GeForce-RTX-GREENLINE")]
        [DataRow("ICELINE-Vertical")]
        public void GenericStarterKeepsItsFramerateReadoutsUnitlessWhenBound(string presetId)
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
            var catalog = new[] { ("fpsCurrent", "Framerate"), ("fps1pctLow", "Online1PercentLow"), ("fps", "OnlineAverage") }
                .Select(source => new OverlayTelemetrySource { Id = "metric/" + source.Item2, SemanticKey = source.Item1, Unit = "FPS" })
                .ToArray();
            var bound = OverlayRuntimeSourceBinder.Bind(OverlayRuntimeDesign.Parse(starter.Id, starter.Name, starter.DesignJson), catalog);
            var readouts = JObject.Parse(bound.TemplateJson).Descendants().OfType<JObject>()
                .Where(node => ((string)node["key"])?.StartsWith("metric/", StringComparison.Ordinal) == true).ToArray();
            Assert.AreEqual(3, readouts.Length, "Every framerate readout must bind to its source.");
            // Binding adopts the source unit unless a scale is set. The captions below the values
            // already name them, and a bound "FPS" unit would crowd the large readout.
            Assert.IsTrue(readouts.All(node => (string)node["unit"] == ""), string.Join(", ", readouts.Select(node => (string)node["unit"])));
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
