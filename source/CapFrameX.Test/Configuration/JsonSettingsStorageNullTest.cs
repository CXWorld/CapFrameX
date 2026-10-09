using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.OSD.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Configuration
{
    [TestClass]
    public class JsonSettingsStorageNullTest
    {
        private readonly List<JsonSettingsStorage> _storages = new List<JsonSettingsStorage>();
        private string _folder;
        private string SettingsPath => Path.Combine(_folder, "AppSettings.json");

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "cfx-null-settings-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            foreach (var storage in _storages)
                await WaitForSave(storage);
            if (Directory.Exists(_folder))
                Directory.Delete(_folder, true);
        }

        [TestMethod]
        public async Task JsonNullIsPreservedForReferenceAndNullableTypes()
        {
            File.WriteAllText(SettingsPath, "{\"Text\":null,\"Items\":null,\"Count\":null,\"Enabled\":null,\"Timestamp\":null}");
            var storage = CreateStorage();
            await storage.Load();

            Assert.IsNull(storage.GetValue<string>("Text"));
            Assert.IsNull(storage.GetValue<object>("Text"));
            Assert.IsNull(storage.GetValue<string[]>("Items"));
            Assert.IsNull(storage.GetValue<List<string>>("Items"));
            Assert.IsNull(storage.GetValue<int?>("Count"));
            Assert.IsNull(storage.GetValue<bool?>("Enabled"));
            Assert.IsNull(storage.GetValue<DateTime?>("Timestamp"));
            Assert.IsTrue(storage.WaitForPendingSaveAsync().IsCompleted, "Reading explicit null must not schedule a repair write.");
        }

        [TestMethod]
        public async Task JsonNullForNonNullableTypesHasAnActionableTypeError()
        {
            File.WriteAllText(SettingsPath, "{\"Count\":null,\"Enabled\":null,\"Timestamp\":null}");
            var storage = CreateStorage();
            await storage.Load();

            var error = Assert.ThrowsExactly<InvalidOperationException>(() => storage.GetValue<int>("Count"));
            StringAssert.Contains(error.Message, "Count");
            StringAssert.Contains(error.Message, nameof(Int32));
            StringAssert.Contains(error.Message.ToLowerInvariant(), "null");
            Assert.ThrowsExactly<InvalidOperationException>(() => storage.GetValue<bool>("Enabled"));
            Assert.ThrowsExactly<InvalidOperationException>(() => storage.GetValue<DateTime>("Timestamp"));
            Assert.ThrowsExactly<KeyNotFoundException>(() => storage.GetValue<string>("Missing"));
        }

        [TestMethod]
        public async Task NullAndStringTransitionsPersistAndRepeatedAssignmentsDoNotScheduleWrites()
        {
            const string key = "SelectedProfile";
            File.WriteAllText(SettingsPath, "{\"SelectedProfile\":null}");
            var storage = CreateStorage();
            await storage.Load();
            storage.SetValue(key, null);
            storage.SetValue(key, null);
            Assert.IsTrue(storage.WaitForPendingSaveAsync().IsCompleted, "An existing null assigned again is a no-op.");

            storage.SetValue(key, "profile-id");
            await WaitForSave(storage);
            Assert.AreEqual("profile-id", (string)JObject.Parse(File.ReadAllText(SettingsPath))[key]);
            Task saved = storage.WaitForPendingSaveAsync();
            storage.SetValue(key, "profile-id");
            Assert.AreSame(saved, storage.WaitForPendingSaveAsync(), "An equal string must not start another save.");
            var loadedId = CreateStorage();
            await loadedId.Load();
            Assert.AreEqual("profile-id", loadedId.GetValue<string>(key));

            storage.SetValue(key, null);
            await WaitForSave(storage);
            Assert.AreEqual(JTokenType.Null, JObject.Parse(File.ReadAllText(SettingsPath))[key].Type);
            saved = storage.WaitForPendingSaveAsync();
            storage.SetValue(key, null);
            Assert.AreSame(saved, storage.WaitForPendingSaveAsync(), "An equal null must not start another save.");
            var loadedNull = CreateStorage();
            await loadedNull.Load();
            Assert.IsNull(loadedNull.GetValue<string>(key));
        }

        [TestMethod]
        public async Task ConfigurationKeepsMissingDefaultsSeparateFromExplicitNullAndEmptyValues()
        {
            File.WriteAllText(SettingsPath, "{\"ActiveOverlayDesignProfileId\":null,\"OverlayHotKey\":\"\"}");
            var configuration = CreateConfiguration(out var storage);

            Assert.IsNull(configuration.ActiveOverlayDesignProfileId);
            Assert.IsNull(configuration.ActiveOverlayDesignProfileId);
            Assert.AreEqual(string.Empty, configuration.OverlayHotKey, "An explicitly disabled hotkey must not regain its default.");
            Assert.IsTrue(storage.WaitForPendingSaveAsync().IsCompleted);
            Assert.AreEqual("F11", configuration.CaptureHotKey);
            Assert.AreEqual(100, configuration.MovingAverageWindowSize);
            await WaitForSave(storage);

            var saved = JObject.Parse(File.ReadAllText(SettingsPath));
            Assert.AreEqual(JTokenType.Null, saved[nameof(IAppConfiguration.ActiveOverlayDesignProfileId)].Type);
            Assert.AreEqual(string.Empty, (string)saved[nameof(IAppConfiguration.OverlayHotKey)]);
            Assert.AreEqual("F11", (string)saved[nameof(IAppConfiguration.CaptureHotKey)]);
            Assert.AreEqual(100, (int)saved[nameof(IAppConfiguration.MovingAverageWindowSize)]);
        }

        [TestMethod]
        public async Task RuntimeDesignActivationAndClassicFallbackSurviveRealConfigurationReloads()
        {
            var configuration = CreateConfiguration(out var storage);
            // The first read persists the missing property's null default; the second read
            // previously dereferenced that null even without restarting the application.
            Assert.IsNull(configuration.ActiveOverlayDesignProfileId);
            Assert.IsNull(configuration.ActiveOverlayDesignProfileId);
            var profiles = new OverlayDesignProfileStore(_folder, json => JObject.Parse(json).ToString(Formatting.None));
            var profile = profiles.Create("Test design", "{\"version\":1,\"root\":{\"type\":\"panel\",\"children\":[{\"type\":\"metric\",\"key\":\"fps\"}]}}");
            var designs = new OverlayDesignService(_folder, configuration);
            Assert.IsFalse(designs.IsEnabled);
            designs.Activate(profile.Id);
            Assert.AreEqual(profile.Id, configuration.ActiveOverlayDesignProfileId);
            Assert.IsTrue(designs.IsEnabled);
            await WaitForSave(storage);

            var restartedConfiguration = CreateConfiguration(out var restartedStorage);
            var restartedDesigns = new OverlayDesignService(_folder, restartedConfiguration);
            Assert.AreEqual(profile.Id, restartedDesigns.ActiveProfileId);
            restartedDesigns.Deactivate();
            Assert.IsNull(restartedConfiguration.ActiveOverlayDesignProfileId);
            Assert.IsFalse(restartedDesigns.IsEnabled);
            await WaitForSave(restartedStorage);
            Task saved = restartedStorage.WaitForPendingSaveAsync();
            restartedDesigns.Deactivate();
            Assert.AreSame(saved, restartedStorage.WaitForPendingSaveAsync());
            Assert.AreEqual(JTokenType.Null, JObject.Parse(File.ReadAllText(SettingsPath))[nameof(IAppConfiguration.ActiveOverlayDesignProfileId)].Type);

            var classicConfiguration = CreateConfiguration(out _);
            Assert.IsNull(classicConfiguration.ActiveOverlayDesignProfileId);
            var classicDesigns = new OverlayDesignService(_folder, classicConfiguration);
            Assert.IsFalse(classicDesigns.IsEnabled);
            Assert.AreEqual(OverlayDesignStarterCatalog.Profiles.Count + 1, classicDesigns.Profiles.Count, "Returning to Row overlay must retain the saved design and starter library.");
        }

        private JsonSettingsStorage CreateStorage()
        {
            var paths = new Mock<IPathService>();
            paths.SetupGet(path => path.ConfigFolder).Returns(_folder);
            var storage = new JsonSettingsStorage(NullLogger<JsonSettingsStorage>.Instance, paths.Object);
            _storages.Add(storage);
            return storage;
        }

        private CapFrameXConfiguration CreateConfiguration(out JsonSettingsStorage storage)
        {
            storage = CreateStorage();
            return new CapFrameXConfiguration(NullLogger<CapFrameXConfiguration>.Instance, storage);
        }

        private static Task WaitForSave(JsonSettingsStorage storage)
            => storage.WaitForPendingSaveAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }
}
