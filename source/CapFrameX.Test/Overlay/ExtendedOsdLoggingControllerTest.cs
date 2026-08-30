using System;
using System.Collections.Generic;
using System.IO;
using CapFrameX.ViewModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class ExtendedOsdLoggingControllerTest
    {
        private string _testDirectory;
        private string _debugConfigurationPath;
        private Dictionary<string, string> _environment;
        private List<(string name, string value)> _setCalls;

        [TestInitialize]
        public void Initialize()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(),
                $"CapFrameX.ExtendedOsdLoggingTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDirectory);
            _debugConfigurationPath = Path.Combine(_testDirectory, "OsdDebug.json");
            _environment = new Dictionary<string, string>(StringComparer.Ordinal);
            _setCalls = new List<(string name, string value)>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }

        [TestMethod]
        public void SetEnabled_EnablesAllLoggingAndPreservesOtherDebugOptions()
        {
            File.WriteAllText(_debugConfigurationPath,
                new JObject
                {
                    ["maxRenderFps"] = 60,
                    ["presentStats"] = false
                }.ToString(Formatting.Indented));
            ExtendedOsdLoggingController controller = CreateController();

            controller.SetEnabled(true);

            Assert.AreEqual("1", _environment[ExtendedOsdLoggingController.HookLogEnvironmentVariable]);
            Assert.AreEqual("1", _environment[ExtendedOsdLoggingController.VulkanLayerLogEnvironmentVariable]);
            Assert.AreEqual(2, _setCalls.Count);
            JObject result = JObject.Parse(File.ReadAllText(_debugConfigurationPath));
            Assert.AreEqual(60, result.Value<int>("maxRenderFps"));
            Assert.IsTrue(result.Value<bool>("presentStats"));
            Assert.IsTrue(controller.IsEnabled());
        }

        [TestMethod]
        public void SetEnabled_DisablesAllLoggingAndPreservesOtherDebugOptions()
        {
            _environment[ExtendedOsdLoggingController.HookLogEnvironmentVariable] = "1";
            _environment[ExtendedOsdLoggingController.VulkanLayerLogEnvironmentVariable] = "1";
            File.WriteAllText(_debugConfigurationPath,
                new JObject
                {
                    ["noComposite"] = true,
                    ["presentStats"] = true
                }.ToString(Formatting.Indented));
            ExtendedOsdLoggingController controller = CreateController();

            controller.SetEnabled(false);

            Assert.AreEqual("0", _environment[ExtendedOsdLoggingController.HookLogEnvironmentVariable]);
            Assert.AreEqual("0", _environment[ExtendedOsdLoggingController.VulkanLayerLogEnvironmentVariable]);
            JObject result = JObject.Parse(File.ReadAllText(_debugConfigurationPath));
            Assert.IsTrue(result.Value<bool>("noComposite"));
            Assert.IsFalse(result.Value<bool>("presentStats"));
            Assert.IsFalse(controller.IsEnabled());
        }

        [TestMethod]
        public void SetEnabled_CreatesMissingDebugConfiguration()
        {
            ExtendedOsdLoggingController controller = CreateController();

            controller.SetEnabled(true);

            Assert.IsTrue(File.Exists(_debugConfigurationPath));
            JObject result = JObject.Parse(File.ReadAllText(_debugConfigurationPath));
            Assert.IsTrue(result.Value<bool>("presentStats"));
        }

        [TestMethod]
        public void IsEnabled_ReturnsTrueWhenOnlyOneLegacyLoggingSwitchIsEnabled()
        {
            _environment[ExtendedOsdLoggingController.HookLogEnvironmentVariable] = "1";
            ExtendedOsdLoggingController controller = CreateController();

            Assert.IsTrue(controller.IsEnabled());
        }

        [TestMethod]
        public void SetEnabled_DoesNotChangeEnvironmentWhenDebugJsonIsMalformed()
        {
            const string malformedJson = "{ not valid json";
            File.WriteAllText(_debugConfigurationPath, malformedJson);
            ExtendedOsdLoggingController controller = CreateController();

            Assert.ThrowsException<JsonReaderException>(() => controller.SetEnabled(true));

            Assert.AreEqual(0, _setCalls.Count);
            Assert.AreEqual(malformedJson, File.ReadAllText(_debugConfigurationPath));
        }

        private ExtendedOsdLoggingController CreateController()
        {
            return new ExtendedOsdLoggingController(
                _debugConfigurationPath,
                name => _environment.TryGetValue(name, out string value) ? value : null,
                (name, value) =>
                {
                    _setCalls.Add((name, value));
                    _environment[name] = value;
                });
        }
    }
}
