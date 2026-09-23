using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Configuration
{
    [TestClass]
    public class HotkeyConfigurationTest
    {
        private static readonly (string Property, string DefaultValue)[] Hotkeys =
        {
            (nameof(IAppConfiguration.CaptureHotKey), "F11"),
            (nameof(IAppConfiguration.OverlayHotKey), "Alt+O"),
            (nameof(IAppConfiguration.OverlayConfigHotKey), "Alt+C"),
            (nameof(IAppConfiguration.OverlayPositionHotkey), "Alt+P"),
            (nameof(IAppConfiguration.ResetHistoryHotkey), "Alt+R"),
            (nameof(IAppConfiguration.ThreadAffinityHotkey), "Control+A"),
            (nameof(IAppConfiguration.ResetMetricsHotkey), "Alt+M")
        };

        private readonly List<JsonSettingsStorage> _storages = new List<JsonSettingsStorage>();
        private string _configurationFolder;

        [TestInitialize]
        public void Initialize()
        {
            _configurationFolder = Path.Combine(Path.GetTempPath(), "cfx-hotkey-test-" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public async Task Cleanup()
        {
            foreach (var storage in _storages)
                await WaitForSave(storage);

            File.Delete(Path.Combine(_configurationFolder, "AppSettings.json"));
            if (Directory.Exists(_configurationFolder))
                Directory.Delete(_configurationFolder);
        }

        [TestMethod]
        public async Task AllHotkeys_KeepDefaultsWhenMissingAndPreserveExplicitDisablingAcrossReloads()
        {
            var first = CreateConfiguration(out var firstStorage);
            foreach (var hotkey in Hotkeys)
            {
                var property = typeof(IAppConfiguration).GetProperty(hotkey.Property);
                Assert.AreEqual(hotkey.DefaultValue, property.GetValue(first), hotkey.Property);
                property.SetValue(first, string.Empty);
            }
            await WaitForSave(firstStorage);

            var json = JObject.Parse(File.ReadAllText(Path.Combine(_configurationFolder, "AppSettings.json")));
            foreach (var hotkey in Hotkeys)
            {
                Assert.IsNotNull(json[hotkey.Property], "Disabling must retain the setting: " + hotkey.Property);
                Assert.AreEqual(string.Empty, (string)json[hotkey.Property], hotkey.Property);
            }

            var restarted = CreateConfiguration(out var restartedStorage);
            foreach (var hotkey in Hotkeys)
            {
                var property = typeof(IAppConfiguration).GetProperty(hotkey.Property);
                Assert.AreEqual(string.Empty, property.GetValue(restarted), hotkey.Property);
                property.SetValue(restarted, hotkey.DefaultValue);
            }
            await WaitForSave(restartedStorage);

            var reassigned = CreateConfiguration(out _);
            foreach (var hotkey in Hotkeys)
                Assert.AreEqual(hotkey.DefaultValue, typeof(IAppConfiguration).GetProperty(hotkey.Property).GetValue(reassigned));
        }

        private IAppConfiguration CreateConfiguration(out JsonSettingsStorage storage)
        {
            var paths = new Mock<IPathService>();
            paths.SetupGet(value => value.ConfigFolder).Returns(_configurationFolder);
            storage = new JsonSettingsStorage(NullLogger<JsonSettingsStorage>.Instance, paths.Object);
            _storages.Add(storage);
            return new CapFrameXConfiguration(NullLogger<CapFrameXConfiguration>.Instance, storage);
        }

        private static Task WaitForSave(JsonSettingsStorage storage)
            => storage.WaitForPendingSaveAsync().WaitAsync(TimeSpan.FromSeconds(10));
    }
}
