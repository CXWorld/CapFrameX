using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.View.OverlayDesignAuthoring;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Integration
{
    [STATestClass]
    [DoNotParallelize]
    public class OverlayDesignAuthoringServiceTest
    {
        private const string TwoMetrics = @"{ ""width"": 320, ""tiles"": [
            { ""kind"": ""metric"", ""key"": ""fps"", ""label"": ""FPS"", ""valueSize"": 36 },
            { ""kind"": ""metric"", ""key"": ""gpuTemp"", ""label"": ""GPU"" } ] }";
        private string _folder;
        private Mock<IAppConfiguration> _configuration;
        private Subject<(string key, object value)> _changes;
        private OverlayDesignService _designService;
        private FakeEditorHost _editor;
        private FakeTelemetry _telemetry;
        private OverlayDesignAuthoringService _service;

        public TestContext TestContext { get; set; }

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CapFrameX-authoring-tests", Guid.NewGuid().ToString("N"));
            _configuration = new Mock<IAppConfiguration>();
            _configuration.SetupAllProperties();
            _configuration.Object.EnableHookOverlay = true;
            _changes = new Subject<(string, object)>();
            _configuration.SetupGet(config => config.OnValueChanged).Returns(_changes);
            _designService = new OverlayDesignService(_folder, _configuration.Object, () => _configuration.Object.IsOverlayActive = true);
            _editor = new FakeEditorHost();
            _telemetry = new FakeTelemetry();
            _service = new OverlayDesignAuthoringService(_folder, _designService, _telemetry, null, _editor);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _designService.Dispose();
            _changes.Dispose();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        [TestMethod]
        public async Task CreatedProfilesOpenInTheEditorStoreWithBoundKeys()
        {
            var created = await _service.CreateProfileAsync("Claude test", TwoMetrics, null, null);

            Assert.AreEqual("Claude test", created.Profile.Name);
            Assert.AreEqual("canvas", created.Profile.Layout);
            Assert.AreEqual(2, created.Profile.TileCount);
            var editorStore = new OverlayDesignProfileStore(_folder, json => OsdDesignDocument.FromJson(json).ToJson());
            var saved = editorStore.Get(created.Profile.Id);
            var document = OsdDesignDocument.FromJson(saved.DesignJson);
            Assert.AreEqual("Claude test", document.Name);
            Assert.AreEqual("sensor:gpu", document.Tiles[1].Key, "The portable role was bound to this machine's reading.");
            Assert.AreEqual("sensor:fps", document.Tiles[0].Key);
            Assert.IsFalse(created.EditorReloaded);
            Assert.AreEqual(0, _editor.Reloads.Count, "A closed editor is not reloaded.");
        }

        [TestMethod]
        public async Task GetEditAndUpdateWorkOnTheSavedDocument()
        {
            var created = await _service.CreateProfileAsync("Edit me", TwoMetrics, null, null);

            var edited = await _service.EditProfileAsync("edit ME", @"[
                { ""op"": ""updateTile"", ""path"": ""1"", ""values"": { ""label"": ""GPU temp"", ""color"": ""#FF6A00FF"" } },
                { ""op"": ""addTile"", ""tile"": { ""kind"": ""chart"", ""columnSpan"": 2 } } ]");
            var fetched = await _service.GetProfileAsync(created.Profile.Id);

            Assert.AreEqual(3, edited.Profile.TileCount);
            var document = JObject.Parse(fetched.DocumentJson);
            Assert.AreEqual("GPU temp", (string)document["tiles"][1]["label"]);
            Assert.AreEqual("#FF6A00FF", (string)document["tiles"][1]["color"]);
            Assert.AreEqual("2", (string)document["tiles"][2]["path"]);
            Assert.AreEqual("frametimes", (string)document["tiles"][2]["key"]);

            document["accent"] = "#00C8FFFF";
            ((JArray)document["tiles"]).RemoveAt(2);
            var updated = await _service.UpdateProfileAsync("Edit me", document.ToString());
            Assert.AreEqual(2, updated.Profile.TileCount);
            Assert.AreEqual("#00C8FFFF", (string)JObject.Parse(updated.DocumentJson)["accent"]);
            Assert.AreEqual(created.Profile.Id, updated.Profile.Id);
        }

        [TestMethod]
        public async Task FailedEditsLeaveTheProfileUnchanged()
        {
            var created = await _service.CreateProfileAsync("Atomic", TwoMetrics, null, null);
            string before = new OverlayDesignProfileStore(_folder, OverlayRuntimeDesignCanonical).Get(created.Profile.Id).DesignJson;

            var error = await Assert.ThrowsExactlyAsync<OverlayDesignAuthoringException>(() => _service.EditProfileAsync("Atomic", @"[
                { ""op"": ""updateTile"", ""path"": ""0"", ""values"": { ""label"": ""Changed"" } },
                { ""op"": ""updateTile"", ""path"": ""1"", ""values"": { ""digits"": 9 } } ]"));

            StringAssert.Contains(error.Message, "Tile 1");
            Assert.AreEqual(before, new OverlayDesignProfileStore(_folder, OverlayRuntimeDesignCanonical).Get(created.Profile.Id).DesignJson);
        }

        [TestMethod]
        public async Task UnsavedEditorChangesBlockEveryLibraryChange()
        {
            var created = await _service.CreateProfileAsync("Guarded", TwoMetrics, null, null);
            _editor.State = new OverlayDesignEditorState(true, created.Profile.Id, true, false);

            await Assert.ThrowsExactlyAsync<OverlayDesignAuthoringException>(() => _service.CreateProfileAsync("Second", TwoMetrics, null, null));
            await Assert.ThrowsExactlyAsync<OverlayDesignAuthoringException>(() =>
                _service.EditProfileAsync("Guarded", @"[ { ""op"": ""removeTile"", ""path"": ""0"" } ]"));
            await Assert.ThrowsExactlyAsync<OverlayDesignAuthoringException>(() => _service.RenameProfileAsync("Guarded", "Renamed"));

            var library = await _service.GetLibraryAsync();
            Assert.IsTrue(library.EditorHasUnsavedChanges);
            Assert.IsFalse(library.Profiles.Any(profile => profile.Name == "Second" || profile.Name == "Renamed"));
            Assert.AreEqual(2, library.Profiles.Single(profile => profile.Id == created.Profile.Id).TileCount);
        }

        [TestMethod]
        public async Task AnOpenEditorFollowsEveryChange()
        {
            _editor.State = new OverlayDesignEditorState(true, null, false, false);

            var created = await _service.CreateProfileAsync("Followed", TwoMetrics, null, null);
            var renamed = await _service.RenameProfileAsync(created.Profile.Id, "Followed 2");

            Assert.IsTrue(created.EditorReloaded);
            CollectionAssert.AreEqual(new[] { created.Profile.Id, created.Profile.Id }, _editor.Reloads);
            Assert.AreEqual("Followed 2", renamed.Name);
        }

        [TestMethod]
        public async Task EditingTheActiveProfileUpdatesTheOverlayAtOnce()
        {
            var created = await _service.CreateProfileAsync("Live", TwoMetrics, null, null);
            var state = await _service.ActivateProfileAsync("Live");
            Assert.AreEqual(created.Profile.Id, state.ActiveProfileId);
            Assert.IsTrue(_configuration.Object.IsOverlayActive, "Activating a profile shows the overlay.");

            var edited = await _service.EditProfileAsync("Live", @"[ { ""op"": ""updateTile"", ""path"": ""0"", ""values"": { ""label"": ""Frames"" } } ]");

            Assert.IsTrue(edited.OverlayUpdated);
            StringAssert.Contains(_designService.CurrentDesign.TemplateJson, "\"label\":\"Frames\"");

            var rows = await _service.DeactivateProfileAsync();
            Assert.IsNull(rows.ActiveProfileId);
        }

        [TestMethod]
        public async Task TileProfilesCannotBeActivatedForRtss()
        {
            await _service.CreateProfileAsync("Rtss", TwoMetrics, null, null);
            _configuration.Object.EnableHookOverlay = false;
            _configuration.Object.EnableHookFreeOverlay = false;

            var error = await Assert.ThrowsExactlyAsync<OverlayDesignAuthoringException>(() => _service.ActivateProfileAsync("Rtss"));
            StringAssert.Contains(error.Message, "cfx_set_osd_options");
        }

        [TestMethod]
        public async Task ProfilesComeFromTemplatesAndCopiesWithUniqueNames()
        {
            var template = await _service.CreateProfileAsync("From template", null, "minimal", null);
            var copy = await _service.CreateProfileAsync("Copy", null, null, "From template");

            Assert.AreEqual("canvas", template.Profile.Layout, "Templates become canvas designs, as in the editor.");
            Assert.AreEqual(template.Profile.TileCount, copy.Profile.TileCount);
            var duplicate = await Assert.ThrowsExactlyAsync<OverlayDesignAuthoringException>(() =>
                _service.CreateProfileAsync("copy", TwoMetrics, null, null));
            StringAssert.Contains(duplicate.Message, "already exists");
            await Assert.ThrowsExactlyAsync<OverlayDesignAuthoringException>(() => _service.CreateProfileAsync("Two sources", TwoMetrics, "minimal", null));
            await Assert.ThrowsExactlyAsync<OverlayDesignAuthoringException>(() => _service.CreateProfileAsync("Missing", null, "no-such-template", null));
        }

        [TestMethod]
        public async Task DeletingAProfileKeepsTheLastOne()
        {
            var created = await _service.CreateProfileAsync("Doomed", TwoMetrics, null, null);
            int count = (await _service.GetLibraryAsync()).Profiles.Count;

            var library = await _service.DeleteProfileAsync("Doomed");

            Assert.AreEqual(count - 1, library.Profiles.Count);
            Assert.IsFalse(library.Profiles.Any(profile => profile.Id == created.Profile.Id));
            var unknown = await Assert.ThrowsExactlyAsync<OverlayDesignAuthoringException>(() => _service.GetProfileAsync("Doomed"));
            StringAssert.Contains(unknown.Message, "There is no tile profile 'Doomed'");
        }

        [TestMethod]
        public async Task PreviewsRenderSavedProfilesAndUnsavedDocuments()
        {
            await _service.CreateProfileAsync("Preview", TwoMetrics, null, null);

            var saved = await _service.RenderPreviewAsync("Preview", null, liveValues: false);
            var draft = await _service.RenderPreviewAsync(null, @"{ ""tiles"": [ { ""kind"": ""text"", ""text"": ""Draft"" } ] }", liveValues: true);

            await _service.CreateProfileAsync("Template preview", null, "benchmark", null);
            var template = await _service.RenderPreviewAsync("Template preview", null, liveValues: false);
            foreach (var (name, preview) in new[] { ("saved", saved), ("draft", draft), ("template", template) })
            {
                CollectionAssert.AreEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, preview.PngBytes.Take(4).ToArray(), "A PNG image.");
                Assert.IsTrue(preview.Width > 0 && preview.Height > 0);
                if (TestContext?.ResultsDirectory == null) continue;
                string path = Path.Combine(TestContext.ResultsDirectory, "tile-preview-" + name + ".png");
                File.WriteAllBytes(path, preview.PngBytes);
                TestContext.AddResultFile(path);
            }
            Assert.AreEqual("demo", saved.Values);
            Assert.AreEqual(2, saved.Tiles.Count);
            Assert.AreEqual("0", saved.Tiles[0].Path);
            Assert.IsTrue(saved.Tiles[1].X > saved.Tiles[0].X, "The two half-width tiles sit side by side.");
            Assert.AreEqual("live", draft.Values);
            Assert.AreEqual(1, draft.Tiles.Count);
        }

        [TestMethod]
        public async Task TheSourceCatalogListsReadingsAndFamilies()
        {
            var catalog = await _service.GetSourcesAsync();

            Assert.IsTrue(catalog.Sources.Any(source => source.Key == "sensor:gpu" && source.SemanticKey == "gpuTemp"));
            var family = catalog.Groups.Single();
            Assert.AreEqual(4, family.MemberCount);
            Assert.AreEqual(family.Key, catalog.Sources.First(source => source.Name == "Core #0").GroupKey);
            Assert.IsTrue(_service.GetTemplates().Any(template => template.Id == "minimal"));
            StringAssert.Contains(_service.GetDesignGuide(), "## Tile document");
        }

        private static string OverlayRuntimeDesignCanonical(string json) => JObject.Parse(json).ToString(Newtonsoft.Json.Formatting.None);

        private sealed class FakeEditorHost : IOverlayDesignEditorHost
        {
            public OverlayDesignEditorState State { get; set; }
            public List<string> Reloads { get; } = new List<string>();

            public Task<bool> ReloadAsync(string profileId)
            {
                Reloads.Add(profileId);
                return Task.FromResult(true);
            }

            public Task OpenAsync(string profileId) => Task.CompletedTask;
        }

        private sealed class FakeTelemetry : IOverlayTelemetryService
        {
            private readonly BehaviorSubject<OverlayTelemetrySnapshot> _snapshots;

            public FakeTelemetry()
            {
                _snapshots = new BehaviorSubject<OverlayTelemetrySnapshot>(new OverlayTelemetrySnapshot(DateTime.UtcNow,
                    new Dictionary<string, double?> { ["sensor:fps"] = 144, ["sensor:gpu"] = 61 }));
            }

            public IObservable<OverlayTelemetrySnapshot> Snapshots => _snapshots.AsObservable();

            public Task<IReadOnlyList<OverlayTelemetrySource>> GetSourcesAsync()
            {
                var sources = new List<OverlayTelemetrySource>
                {
                    new OverlayTelemetrySource { Id = "sensor:fps", Name = "Framerate", Unit = "FPS", Category = "Performance", SemanticKey = "fps" },
                    new OverlayTelemetrySource { Id = "sensor:gpu", Name = "GPU Core", Unit = "°C", Category = "Graphics", SemanticKey = "gpuTemp" }
                };
                for (int core = 0; core < 4; core++)
                    sources.Add(new OverlayTelemetrySource
                    {
                        Id = "sensor:core" + core, Name = "Core #" + core, Unit = "MHz", Category = "Processor",
                        HardwareName = "Test CPU", DeviceKey = "device:cpu", GroupKey = "core-clocks", GroupName = "Core clocks", GroupOrder = core
                    });
                return Task.FromResult<IReadOnlyList<OverlayTelemetrySource>>(sources);
            }

            public IDisposable AcquirePreview() => Disposable.Empty;

            public void Dispose() => _snapshots.Dispose();
        }
    }
}
