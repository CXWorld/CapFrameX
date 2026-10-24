using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        private static readonly string[] SlickPresetIds =
        {
            "Slick-Overlay-Vertical-Panel", "Slick-Overlay-Wide-3-Rows", "Slick-Overlay-Compact-2x2", "Slick-Overlay-Single-Line"
        };
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
        [DataRow("older-original")]
        [DataRow("edited")]
        [DataRow("renamed")]
        [DataRow("deleted")]
        [DataRow("same-name")]
        public void CatalogCorrectionUpdatesOnlyTheKnownOriginalAndPreservesSelection(string state)
        {
            // The correction knows two earlier revisions and replaces either of them unmodified.
            string olderJson = Design("minimal", 360);
            string originalJson = Design("minimal", 400);
            var original = new OverlayDesignStarterProfile("revision-fixture", state == "older-original" ? olderJson : originalJson, 4);
            var corrected = new OverlayDesignStarterProfile("revision-fixture", Design("minimal", 480), 4,
                OverlayDesignStarterProfile.ComputeDesignHash(olderJson), OverlayDesignStarterProfile.ComputeDesignHash(originalJson));
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
                bool replaced = (state == "original" || state == "older-original") && profile.Id == original.Id;
                Assert.AreEqual(replaced ? corrected.DesignJson : profile.DesignJson,
                    upgraded.Get(profile.Id).DesignJson);
            }
            Assert.AreEqual(5, (int)JObject.Parse(File.ReadAllText(LibraryPath))["StarterCatalogVersion"]);
            string updatedBytes = File.ReadAllText(LibraryPath);
            Assert.IsTrue(Open().EnsureStarterProfiles(new[] { corrected }, 5));
            Assert.AreEqual(updatedBytes, File.ReadAllText(LibraryPath));
        }

        [TestMethod]
        [DataRow("original", true)]
        [DataRow("renamed", false)]
        [DataRow("edited", false)]
        [DataRow("name-taken", false)]
        public void CatalogUpgradeRenamesOnlyAnUnmodifiedStarterWhoseNewNameIsFree(string state, bool replaced)
        {
            // Version eighteen renamed hardware starters: the corrected design keeps the profile ID of
            // the former preset and replaces it under its former name only.
            string originalJson = WithName(Design("minimal", 400), "OLD - Model 1234");
            var original = new OverlayDesignStarterProfile("old-preset", originalJson, 4);
            var corrected = new OverlayDesignStarterProfile("new-preset", "old-preset", WithName(Design("minimal", 480), "NEW - Series"), 4,
                new[] { OverlayDesignStarterProfile.ComputeDesignHash(originalJson) })
            {
                PreviousNames = new[] { "OLD - Model 1234" }
            };
            Assert.AreEqual(original.Id, corrected.Id);
            var store = Open();
            Assert.IsTrue(store.EnsureStarterProfiles(new[] { original }, 4));
            store.SetActive(original.Id);
            if (state == "renamed") store.Rename(original.Id, "My model");
            if (state == "edited") store.Save(original.Id, WithName(Design("minimal", 420), original.Name));
            if (state == "name-taken") store.Create("new - series", Design("compact", 480));
            var before = store.Get(original.Id);
            string selected = store.ActiveProfileId;

            var upgraded = Open();
            Assert.IsTrue(upgraded.EnsureStarterProfiles(new[] { corrected }, 5));
            var profile = upgraded.Get(original.Id);
            Assert.AreEqual(replaced ? corrected.Name : before.Name, profile.Name);
            Assert.AreEqual(replaced ? corrected.DesignJson : before.DesignJson, profile.DesignJson);
            Assert.AreEqual(selected, upgraded.ActiveProfileId);
            Assert.AreEqual(upgraded.Profiles.Count, Open().Profiles.Count, "The upgraded library must load again.");
        }

        [TestMethod]
        public void GenericHardwareStartersKeepTheProfilesOfTheModelSpecificDesignsTheyReplace()
        {
            foreach (var (presetId, formerPresetId, formerName) in new[]
            {
                ("RTX-50-Ryzen-3D-APEX", "RTX-5090-9800X3D-APEX", "APEX - RTX 5090 + 9800X3D"),
                ("AMD-Ryzen-Radeon-REDLINE", "AMD-Ryzen-Radeon-REDLINE", "REDLINE - Ryzen 7 9800X3D + Radeon"),
                ("RTX-50-Core-Ultra-FUSION-ARL", "RTX-5090-Core-Ultra-285K-FUSION", "FUSION - RTX 5090 + Core Ultra 9 285K")
            })
            {
                var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
                Assert.AreEqual(new OverlayDesignStarterProfile(formerPresetId, starter.DesignJson).Id, starter.Id, presetId);
                CollectionAssert.AreEqual(new[] { formerName }, starter.PreviousNames.ToArray(), presetId);
            }
        }

        [TestMethod]
        [DataRow(2, 3)]
        [DataRow(3, 4)]
        [DataRow(5, 6)]
        [DataRow(6, 7)]
        [DataRow(16, 17)]
        [DataRow(17, 18)]
        [DataRow(18, 19)]
        [DataRow(19, 20)]
        [DataRow(20, 21)]
        [DataRow(21, 22)]
        [DataRow(22, 23)]
        public void VersionUpgradeAddsOnlyNewProfilesAndPreservesTheExistingLibrary(int previousVersion, int targetVersion)
        {
            var catalog = OverlayDesignStarterCatalog.Profiles.Where(profile => profile.IntroducedInVersion <= targetVersion).ToArray();
            var newStarters = catalog.Where(profile => profile.IntroducedInVersion == targetVersion).ToArray();
            string[] expectedIds = targetVersion switch
            {
                3 => new[] { "core-types-overview", "core-types-compact", "core-types-thermal" },
                4 => new[] { "RTX-50-Ryzen-3D-APEX", "AMD-Ryzen-Radeon-REDLINE", "RTX-50-Core-Ultra-FUSION-ARL",
                    "AMD-Ryzen-5-9600X-Badge", "AMD-Ryzen-7-9800X3D-Badge" },
                6 => new[] { "Panther-Lake-Arc-Aurora", "OLED-Minimal" },
                17 => SlickPresetIds,
                18 => new[] { "RTX-50-Core-FUSION-RPL", "RTX-50-Ryzen-FUSION" },
                19 or 20 or 21 or 22 or 23 => Array.Empty<string>(),
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
        public void CatalogUpgradeListsShippedDesignsInCatalogOrderAndKeepsTheUsersOwnProfilesInPlace()
        {
            // A library that grew with the catalog: every version appended its designs, so FUSION RPL
            // and FUSION Ryzen ended up behind the Slick Overlay designs instead of beside FUSION ARL.
            var catalog = OverlayDesignStarterCatalog.Profiles;
            var store = Open();
            var grown = catalog.Where(profile => profile.IntroducedInVersion <= 18).OrderBy(profile => profile.IntroducedInVersion).ToArray();
            Assert.IsTrue(store.EnsureStarterProfiles(grown, 18));
            CollectionAssert.AreEqual(grown.Select(profile => profile.Id).ToArray(), store.Profiles.Select(profile => profile.Id).ToArray());
            var deleted = catalog.Single(profile => profile.PresetId == "compact");
            store.Delete(deleted.Id);
            var custom = store.Create("My capture", Design("minimal", 520));
            var renamed = catalog.Single(profile => profile.PresetId == "turquoise-wide");
            store.Rename(renamed.Id, "My turquoise");

            var upgraded = Open();
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(upgraded));

            var ids = upgraded.Profiles.Select(profile => profile.Id).ToList();
            Assert.AreEqual(ids.Count - 1, ids.IndexOf(custom.Id), "The user's own profile keeps its place.");
            CollectionAssert.AreEqual(catalog.Where(profile => profile.Id != deleted.Id).Select(profile => profile.Id).ToArray(),
                ids.Where(id => id != custom.Id).ToArray(), "The shipped designs follow the catalog order, renamed ones included.");
            Assert.AreEqual("My turquoise", upgraded.Get(renamed.Id).Name);
            int fusion = ids.IndexOf(catalog.Single(profile => profile.PresetId == "RTX-50-Core-Ultra-FUSION-ARL").Id);
            CollectionAssert.AreEqual(new[] { "RTX-50-Core-FUSION-RPL", "RTX-50-Ryzen-FUSION" },
                ids.Skip(fusion + 1).Take(2).Select(id => catalog.Single(profile => profile.Id == id).PresetId).ToArray());
        }

        [TestMethod]
        public void NewShippedDesignJoinsItsGroupBeforeTheUsersOwnProfiles()
        {
            // An upgrade adds a design beside the designs listed around it in the catalog, not at the end.
            var oldStarter = new OverlayDesignStarterProfile("first-fixture", Design("minimal", 400), 1);
            var newStarter = new OverlayDesignStarterProfile("second-fixture", Design("compact", 480), 2);
            var lastStarter = new OverlayDesignStarterProfile("third-fixture", Design("benchmark", 560), 1);
            var store = Open();
            Assert.IsTrue(store.EnsureStarterProfiles(new[] { oldStarter, lastStarter }, 1));
            var custom = store.Create("My design", Design("benchmark", 640));
            Assert.IsTrue(store.EnsureStarterProfiles(new[] { oldStarter, newStarter, lastStarter }, 2));
            CollectionAssert.AreEqual(new[] { oldStarter.Id, newStarter.Id, lastStarter.Id, custom.Id },
                store.Profiles.Select(profile => profile.Id).ToArray());
        }

        [TestMethod]
        [DataRow("RTX-50-Ryzen-3D-APEX", "APEX 3D - RTX 50 Series + Ryzen 3D", false, 4)]
        [DataRow("AMD-Ryzen-Radeon-REDLINE", "REDLINE - Ryzen + Radeon", true, 4)]
        [DataRow("RTX-50-Core-Ultra-FUSION-ARL", "FUSION ARL - RTX 50 Series + Intel Core Ultra", true, 4)]
        [DataRow("RTX-50-Core-FUSION-RPL", "FUSION RPL - RTX 50 Series + Intel Core", true, 18)]
        [DataRow("RTX-50-Ryzen-FUSION", "FUSION Ryzen - RTX 50 Series + Ryzen", true, 18)]
        [DataRow("AMD-Ryzen-5-9600X-Badge", "AMD Ryzen 5 9600X Badge", true, 4)]
        [DataRow("AMD-Ryzen-7-9800X3D-Badge", "AMD Ryzen 7 9800X3D Badge", true, 4)]
        [DataRow("Panther-Lake-Arc-Aurora", "Panther Lake - Arc Aurora", false, 6)]
        [DataRow("OLED-Minimal", "OLED Minimal", false, 6)]
        [DataRow("NVIDIA-GeForce-RTX-GREENLINE", "GREENLINE - GeForce RTX", true, 7)]
        [DataRow("ICELINE-Vertical", "ICELINE - Vertical", true, 7)]
        [DataRow("Slick-Overlay-Vertical-Panel", "Slick Overlay - Vertical Panel", true, 17)]
        [DataRow("Slick-Overlay-Wide-3-Rows", "Slick Overlay - Wide 3 Rows", true, 17)]
        [DataRow("Slick-Overlay-Compact-2x2", "Slick Overlay - Compact 2x2", true, 17)]
        [DataRow("Slick-Overlay-Single-Line", "Slick Overlay - Single Line", true, 17)]
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
        [DataRow("RTX-50-Ryzen-3D-APEX", 136.0)]
        [DataRow("AMD-Ryzen-Radeon-REDLINE", 144.0)]
        [DataRow("RTX-50-Core-Ultra-FUSION-ARL", 144.0)]
        [DataRow("RTX-50-Core-FUSION-RPL", 144.0)]
        [DataRow("RTX-50-Ryzen-FUSION", 144.0)]
        public void FramePacingChartIsTwiceAsHighAndReplacesTheUnmodifiedEarlierCopy(string presetId, double height)
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
            var document = OsdDesignDocument.FromJson(starter.DesignJson);
            var chart = document.Tiles.Single(tile => tile.Kind == "chart" && tile.Label == "FRAME PACING");
            Assert.AreEqual(height, chart.FrameHeight);

            if (presetId == "AMD-Ryzen-Radeon-REDLINE")
            {
                // Version nineteen: the GPU block below the CPU tiles, not above them as in version twenty.
                var gpu = document.Tiles.Single(tile => tile.Label == "AMD Radeon GPU");
                var cpuTiles = new[] { "AMD Ryzen CPU", "CORE / THREAD LOADS", "CPU Load Heatmap" }
                    .Select(label => document.Tiles.Single(tile => tile.Label == label)).ToArray();
                foreach (var tile in cpuTiles) tile.Y -= gpu.FrameHeight + document.Gap;
                gpu.Y = cpuTiles[^1].Y + cpuTiles[^1].FrameHeight + document.Gap;
                document.Tiles.Remove(gpu);
                document.Tiles.Insert(document.Tiles.IndexOf(cpuTiles[^1]) + 1, gpu);
                Assert.IsTrue(starter.CanUpdate(new OverlayDesignProfile(starter.Id, starter.Name, document.ToJson(), DateTime.UtcNow)),
                    "An unmodified version-nineteen REDLINE gets the GPU block above its CPU tiles.");
                // Version nineteen also gave REDLINE the core loads below its CPU block.
                var loads = document.Tiles.Where(tile => tile.Label is "CORE / THREAD LOADS" or "CPU Load Heatmap").ToArray();
                Assert.AreEqual(2, loads.Length);
                double top = loads.Min(tile => tile.Y.Value);
                double space = loads.Max(tile => tile.Y.Value + tile.FrameHeight.Value) + document.Gap - top;
                foreach (var tile in loads) document.Tiles.Remove(tile);
                foreach (var tile in document.Tiles.Where(tile => tile.Y >= top)) tile.Y -= space;
                document.CanvasHeight -= space;
                // Its badge image sat in a container of its own, which stored the image four times.
                var cpu = document.Tiles.Single(tile => tile.Label == "AMD Ryzen CPU");
                var image = cpu.Children.Single(tile => tile.Label == "Ryzen badge");
                var container = new OsdDesignTile
                {
                    Kind = "container", Label = "Ryzen badge", Key = null, Padding = 0, BorderWidth = 0, BackgroundColor = "#00000000",
                    X = image.X, Y = image.Y, FrameWidth = image.FrameWidth, FrameHeight = image.FrameHeight
                };
                cpu.Children[cpu.Children.IndexOf(image)] = container;
                image.Label = "Frame and branding";
                image.X = 0;
                image.Y = 0;
                container.Children.Add(image);
            }
            // Before version twenty-three no chart drew the stutter threshold.
            chart.ChartLines = null;
            // Version eighteen: the chart half as high, every tile below it higher up by the difference.
            double bottom = chart.Y.Value + chart.FrameHeight.Value, half = height / 2;
            foreach (var tile in document.Tiles.Where(tile => tile.Y >= bottom - .001)) tile.Y -= half;
            chart.FrameHeight = half;
            document.CanvasHeight -= half;
            var earlier = new OverlayDesignProfile(starter.Id, starter.Name, document.ToJson(), DateTime.UtcNow);
            Assert.IsTrue(starter.CanUpdate(earlier), "An unmodified version-eighteen copy gets the higher chart.");
            Assert.IsFalse(starter.CanUpdate(new OverlayDesignProfile(starter.Id, starter.Name, starter.DesignJson, DateTime.UtcNow)),
                "The current design is never replaced.");
        }

        [TestMethod]
        [DataRow("RTX-50-Core-Ultra-FUSION-ARL", "#6DDBFEFF")]
        [DataRow("RTX-50-Core-FUSION-RPL", "#6DDBFEFF")]
        [DataRow("RTX-50-Ryzen-FUSION", "#ED7618FF")]
        public void FusionFramePacingChartDrawsTheStutterThresholdAndReplacesTheUnmodifiedEarlierCopy(string presetId, string color)
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
            var document = OsdDesignDocument.FromJson(starter.DesignJson);
            document.Validate();
            Assert.AreEqual(starter.DesignJson, OverlayRuntimeDesign.Canonicalize(document.ToJson()), "The starter is stored as the editor saves it.");
            // The frametimes in the design's color under the stutter threshold of the analysis chart.
            var chart = document.Tiles.Single(tile => tile.Kind == "chart" && tile.Label == "FRAME PACING");
            Assert.AreEqual("frametimes", chart.Key);
            Assert.AreEqual(2, chart.ChartLines.Count);
            var values = chart.ChartLines[0];
            Assert.AreEqual(OsdChartLine.ValueKind, values.Kind);
            Assert.AreEqual("frametimes", values.Series);
            Assert.AreEqual(color, values.Color);
            var threshold = chart.ChartLines[1];
            Assert.AreEqual(OsdChartLine.StutterKind, threshold.Kind);
            Assert.AreEqual("frametimes", threshold.Series);
            Assert.AreEqual(OsdChartLine.DefaultStutterFactor, threshold.Factor);
            Assert.AreEqual(OsdChartLine.DefaultStutterColor, threshold.Color);
            Assert.AreEqual("Stutter Threshold", threshold.Label);

            // Version twenty-two: the same chart with the frametimes alone.
            chart.ChartLines = null;
            Assert.IsTrue(starter.CanUpdate(new OverlayDesignProfile(starter.Id, starter.Name, document.ToJson(), DateTime.UtcNow)),
                "An unmodified version-twenty-two copy gets the stutter threshold.");
        }

        [TestMethod]
        [DataRow("RTX-50-Core-Ultra-FUSION-ARL", true)]
        [DataRow("RTX-50-Core-FUSION-RPL", true)]
        [DataRow("RTX-50-Ryzen-FUSION", false)]
        [DataRow("AMD-Ryzen-Radeon-REDLINE", false)]
        public void CoreLoadStartersBreakDownTheCpuOfTheirFamilyByCoreType(string presetId, bool hybrid)
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
            var document = OsdDesignDocument.FromJson(starter.DesignJson);
            // Core loads bind to the CPU's load family on first use, whatever the core count; hybrid
            // CPUs show their P- and E-cores in tiles of their own.
            var groups = document.EnumerateTiles().Where(tile => tile.Kind == "group").ToArray();
            var heatmap = groups.Single(group => group.GroupDisplay == "heatmap");
            Assert.AreEqual("role:cpu-loads", heatmap.GroupKey);
            Assert.IsNull(heatmap.GroupWindowSeconds, "The heatmap keeps the default window.");
            var bars = groups.Where(group => group.GroupDisplay == "bar").ToArray();
            CollectionAssert.AreEqual(hybrid ? new[] { "role:cpu-loads|P", "role:cpu-loads|E" } : new[] { "role:cpu-loads" },
                bars.Select(group => group.GroupKey).ToArray());
            Assert.IsTrue(bars.All(group => group.Key == group.GroupKey && group.GroupBarHeight.HasValue && group.GroupBarWidth == null
                && group.GroupColumns == 32), "The bars spread over the tile: the layout derives their width from the readings.");

            // Hybrid CPUs name the fastest P- and E-core; one core type needs the CPU's max clock only.
            var keys = OverlayRuntimeDesign.Parse(starter.Id, starter.Name, starter.DesignJson).MetricKeys.ToArray();
            string[] clocks = hybrid ? new[] { "cpuPCoreClock", "cpuECoreClock" } : new[] { "cpuClock" };
            CollectionAssert.IsSubsetOf(clocks, keys);
            CollectionAssert.AreEquivalent(clocks, keys.Where(key => key.StartsWith("cpu", StringComparison.Ordinal) && key.EndsWith("Clock", StringComparison.Ordinal)).ToArray());
        }

        [TestMethod]
        [DataRow("NVIDIA-GeForce-RTX-GREENLINE", new[] { "gpuName" })]
        [DataRow("ICELINE-Vertical", new[] { "gpuName", "cpuName" })]
        [DataRow("Slick-Overlay-Vertical-Panel", new[] { "gpuNameShort", "cpuNameShort", "gpuDriver" })]
        [DataRow("Slick-Overlay-Wide-3-Rows", new[] { "gpuNameShort", "cpuNameShort", "gpuDriver" })]
        [DataRow("Slick-Overlay-Compact-2x2", new[] { "gpuNameShort", "cpuNameShort", "gpuDriver" })]
        [DataRow("Slick-Overlay-Single-Line", new[] { "gpuNameShort", "cpuNameShort", "gpuDriver" })]
        [DataRow("RTX-50-Ryzen-3D-APEX", new[] { "gpuNameShort", "cpuNameShort" })]
        [DataRow("AMD-Ryzen-Radeon-REDLINE", new[] { "gpuNameShort", "cpuNameShort" })]
        [DataRow("RTX-50-Core-Ultra-FUSION-ARL", new[] { "gpuNameShort", "cpuNameShort" })]
        [DataRow("RTX-50-Core-FUSION-RPL", new[] { "gpuNameShort", "cpuNameShort" })]
        [DataRow("RTX-50-Ryzen-FUSION", new[] { "gpuNameShort", "cpuNameShort" })]
        public void GenericStarterNamesTheInstalledHardwareAndStaysPortable(string presetId, string[] nameKeys)
        {
            var starter = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == presetId);
            var document = OsdDesignDocument.FromJson(starter.DesignJson);
            var tiles = document.EnumerateTiles().ToArray();
            foreach (string key in nameKeys)
                Assert.AreEqual(1, tiles.Count(tile => tile.Kind == "metric" && tile.Key == key), key + " must show the name the system reports.");
            // The native metric draws text readouts in the theme text colour whatever their value
            // colour, so each one declares that colour and the editor shows what is rendered.
            foreach (var readout in tiles.Where(tile => tile.Kind == "metric"
                && new[] { "gpuName", "cpuName", "gpuNameShort", "cpuNameShort", "graphicsApi", "gpuDriver" }.Contains(tile.Key)))
                Assert.AreEqual(document.TextColor, readout.ValueColor, readout.Key);
            // Also the bare model numbers the former hardware designs showed: "5090", "9800X3D", "285K", "Ultra 9".
            Assert.IsFalse(tiles.Any(tile => Regex.IsMatch(tile.Label + " " + tile.Text,
                    @"\b(?:[RG]TX|RX)\s*\d{3,4}\b|\bi[3579]-\d{4}|\bRyzen\s+\d|\b[2-5]0[5-9]0\b|\b\d{4}X3D\b|\b\d{3}K\b|\bUltra\s+[3579]\b", RegexOptions.IgnoreCase)),
                "No tile may name a specific CPU or GPU model.");
            var keys = OverlayRuntimeDesign.Parse(starter.Id, starter.Name, starter.DesignJson).MetricKeys;
            Assert.IsFalse(keys.Any(key => key.StartsWith("sensor", StringComparison.Ordinal)),
                "Semantic keys bind on any GPU and CPU; hardware sensor keys would tie the design to one system.");
        }

        [TestMethod]
        [DataRow("NVIDIA-GeForce-RTX-GREENLINE")]
        [DataRow("ICELINE-Vertical")]
        [DataRow("Slick-Overlay-Vertical-Panel")]
        [DataRow("Slick-Overlay-Wide-3-Rows")]
        [DataRow("Slick-Overlay-Compact-2x2")]
        [DataRow("Slick-Overlay-Single-Line")]
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
        public void AdaptedStartersDescribeThemselvesAndCreditTheirAuthorsThroughRenamesAndEdits()
        {
            foreach (var starter in OverlayDesignStarterCatalog.Profiles)
            {
                bool adapted = SlickPresetIds.Contains(starter.PresetId);
                Assert.AreEqual(adapted, starter.Description.Count > 0, starter.PresetId);
                Assert.AreEqual(adapted, starter.Links.Count > 0, starter.PresetId);
                Assert.IsTrue(starter.Links.All(link => link.Uri.IsAbsoluteUri && link.Uri.Scheme == Uri.UriSchemeHttps), starter.PresetId);
            }

            var store = Open();
            Assert.IsTrue(OverlayDesignStarterCatalog.EnsureProfiles(store));
            var panel = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == "Slick-Overlay-Vertical-Panel");
            store.Rename(panel.Id, "My corner panel");
            store.Save(panel.Id, Design("minimal", 400));

            var info = OverlayDesignStarterCatalog.Describe(store.Get(panel.Id));
            Assert.AreEqual("My corner panel", info.Name);
            Assert.IsTrue(info.HasDescription);
            Assert.AreEqual(3, info.Description.Count, "Layout, credit and notes.");
            Assert.IsTrue(info.Description.All(paragraph => !paragraph.StartsWith("OverlayDesignStarter_", StringComparison.Ordinal)),
                "Every paragraph resolves to catalog text.");
            StringAssert.Contains(info.Description[1], "HeyOkay");
            CollectionAssert.Contains(info.Links.Select(link => link.Uri.AbsoluteUri).ToArray(), "https://github.com/HeyOkay/Slick-Overlay-for-RTSS");

            var copy = store.Duplicate(panel.Id, "My copy");
            Assert.IsFalse(OverlayDesignStarterCatalog.Describe(copy).HasDescription, "A copy is the user's own design.");
            var benchmark = OverlayDesignStarterCatalog.Profiles.Single(profile => profile.PresetId == "benchmark");
            Assert.IsFalse(OverlayDesignStarterCatalog.Describe(store.Get(benchmark.Id)).HasDescription);
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
        private static string WithName(string json, string name)
        {
            var document = OsdDesignDocument.FromJson(json); document.Name = name; return document.ToJson();
        }
    }
}
