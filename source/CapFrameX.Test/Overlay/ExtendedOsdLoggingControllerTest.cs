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
        private Dictionary<string, string> _processEnvironment;
        private List<(string name, string value)> _setCalls;
        private int _environmentNotifications;

        [TestInitialize]
        public void Initialize()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(),
                $"CapFrameX.ExtendedOsdLoggingTests.{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDirectory);
            _debugConfigurationPath = Path.Combine(_testDirectory, "OsdDebug.json");
            _environment = new Dictionary<string, string>(StringComparer.Ordinal);
            _processEnvironment = new Dictionary<string, string>(StringComparer.Ordinal);
            _setCalls = new List<(string name, string value)>();
            _environmentNotifications = 0;
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
            Assert.AreEqual("1", _environment[ExtendedOsdLoggingController.PresentStatsEnvironmentVariable]);
            Assert.AreEqual("1", _environment[ExtendedOsdLoggingController.VerboseLogEnvironmentVariable]);
            Assert.AreEqual(4, _setCalls.Count);
            JObject result = JObject.Parse(File.ReadAllText(_debugConfigurationPath));
            Assert.AreEqual(60, result.Value<int>("maxRenderFps"));
            Assert.IsTrue(result.Value<bool>("presentStats"));
            Assert.IsTrue(result.Value<bool>("verboseLog"));
            Assert.IsTrue(controller.IsEnabled());
            Assert.AreEqual(1, _environmentNotifications);
        }

        [TestMethod]
        public void SetEnabled_DisablesAllLoggingAndPreservesOtherDebugOptions()
        {
            _environment[ExtendedOsdLoggingController.HookLogEnvironmentVariable] = "1";
            _environment[ExtendedOsdLoggingController.VulkanLayerLogEnvironmentVariable] = "1";
            _processEnvironment[ExtendedOsdLoggingController.PresentStatsEnvironmentVariable] = "1";
            _processEnvironment[ExtendedOsdLoggingController.VerboseLogEnvironmentVariable] = "1";
            File.WriteAllText(_debugConfigurationPath,
                new JObject
                {
                    ["noComposite"] = true,
                    ["presentStats"] = true,
                    ["verboseLog"] = true
                }.ToString(Formatting.Indented));
            ExtendedOsdLoggingController controller = CreateController();

            controller.SetEnabled(false);

            Assert.AreEqual("0", _environment[ExtendedOsdLoggingController.HookLogEnvironmentVariable]);
            Assert.AreEqual("0", _environment[ExtendedOsdLoggingController.VulkanLayerLogEnvironmentVariable]);
            foreach (var pair in _environment)
            {
                Assert.AreEqual("0", pair.Value, pair.Key);
                Assert.AreEqual("0", _processEnvironment[pair.Key], pair.Key);
            }
            JObject result = JObject.Parse(File.ReadAllText(_debugConfigurationPath));
            Assert.IsTrue(result.Value<bool>("noComposite"));
            Assert.IsFalse(result.Value<bool>("presentStats"));
            Assert.IsFalse(result.Value<bool>("verboseLog"));
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

        [DataTestMethod]
        [DataRow("CFX_HOOK_LOG", false)]
        [DataRow("CFX_VKLAYER_LOG", false)]
        [DataRow("CFX_OSD_PRESENT_STATS", false)]
        [DataRow("CFX_OSD_VERBOSE_LOG", false)]
        [DataRow("CFX_HOOK_LOG", true)]
        [DataRow("CFX_VKLAYER_LOG", true)]
        [DataRow("CFX_OSD_PRESENT_STATS", true)]
        [DataRow("CFX_OSD_VERBOSE_LOG", true)]
        public void IsEnabled_LegacyEnvironmentCannotEnableDefaultLogging(string name, bool inherited)
        {
            (inherited ? _processEnvironment : _environment)[name] = "1";
            ExtendedOsdLoggingController controller = CreateController();

            Assert.IsFalse(controller.IsEnabled());
            Assert.AreEqual(0, _environmentNotifications);
            controller.SetEnabled(false);
            Assert.IsFalse(controller.IsEnabled());
            Assert.AreEqual(1, _environmentNotifications);
        }

        [DataTestMethod]
        [DataRow("presentStats")]
        [DataRow("verboseLog")]
        public void IsEnabled_DetectsEachJsonSwitch(string property)
        {
            File.WriteAllText(_debugConfigurationPath, new JObject { [property] = true }.ToString());
            ExtendedOsdLoggingController controller = CreateController();

            Assert.IsTrue(controller.IsEnabled());
            controller.SetEnabled(false);
            Assert.IsFalse(controller.IsEnabled());
        }

        [TestMethod]
        public void IsEnabled_NoDiagnosticSettings_DefaultsToOff()
        {
            Assert.IsFalse(CreateController().IsEnabled());
            Assert.IsFalse(File.Exists(_debugConfigurationPath));
            Assert.AreEqual(0, _setCalls.Count);
            Assert.AreEqual(0, _environmentNotifications);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void ApplyProcessSettings_RestartUsesSavedSelectionDespiteStaleLauncherEnvironment(bool enabled)
        {
            CreateController().SetEnabled(enabled);
            string savedJson = File.ReadAllText(_debugConfigurationPath);
            _setCalls.Clear();
            _environmentNotifications = 0;
            foreach (string name in _environment.Keys)
                _processEnvironment[name] = enabled ? "0" : "1";

            ExtendedOsdLoggingController restartedController = CreateController();
            Assert.AreEqual(enabled, restartedController.IsEnabled());
            restartedController.ApplyProcessSettings();

            foreach (string name in _environment.Keys)
                Assert.AreEqual(enabled ? "1" : "0", _processEnvironment[name], name);
            Assert.AreEqual(savedJson, File.ReadAllText(_debugConfigurationPath));
            Assert.AreEqual(0, _setCalls.Count, "Startup must not rewrite user environment variables.");
            Assert.AreEqual(0, _environmentNotifications, "Startup must not wait on a Windows broadcast.");
        }

        [TestMethod]
        public void ApplyProcessSettings_MissingConfiguration_ClearsInheritedDiagnostics()
        {
            _processEnvironment[ExtendedOsdLoggingController.HookLogEnvironmentVariable] = "1";
            _processEnvironment[ExtendedOsdLoggingController.VulkanLayerLogEnvironmentVariable] = "1";
            _processEnvironment[ExtendedOsdLoggingController.PresentStatsEnvironmentVariable] = "1";
            _processEnvironment[ExtendedOsdLoggingController.VerboseLogEnvironmentVariable] = "1";

            CreateController().ApplyProcessSettings();

            foreach (var pair in _processEnvironment)
                Assert.AreEqual("0", pair.Value, pair.Key);
            Assert.IsFalse(File.Exists(_debugConfigurationPath));
            Assert.AreEqual(0, _setCalls.Count);
            Assert.AreEqual(0, _environmentNotifications);
        }

        [TestMethod]
        public void ApplyProcessSettings_MalformedConfiguration_DisablesInheritedDiagnosticsAndReportsError()
        {
            _processEnvironment[ExtendedOsdLoggingController.HookLogEnvironmentVariable] = "1";
            File.WriteAllText(_debugConfigurationPath, "{ not valid json");

            Assert.ThrowsException<JsonReaderException>(() => CreateController().ApplyProcessSettings());

            Assert.AreEqual(4, _processEnvironment.Count);
            foreach (var pair in _processEnvironment)
                Assert.AreEqual("0", pair.Value, pair.Key);
            Assert.AreEqual(0, _environmentNotifications);
        }

        [TestMethod]
        public void SetEnabled_JsonWriteFailureRestoresUserAndProcessVariables()
        {
            // A directory at the destination forces the atomic JSON replacement to fail.
            Directory.CreateDirectory(_debugConfigurationPath);
            string[] names = { "CFX_HOOK_LOG", "CFX_VKLAYER_LOG", "CFX_OSD_PRESENT_STATS", "CFX_OSD_VERBOSE_LOG" };
            foreach (string name in names)
            {
                _environment[name] = "1";
                _processEnvironment[name] = "1";
            }

            Assert.ThrowsException<UnauthorizedAccessException>(() => CreateController().SetEnabled(false));

            foreach (string name in names)
            {
                Assert.AreEqual("1", _environment[name]);
                Assert.AreEqual("1", _processEnvironment[name]);
            }
            Assert.AreEqual(0, _environmentNotifications);
        }

        [TestMethod]
        public void SetEnabled_FailedFirstSave_RemovesNewUserVariables()
        {
            Directory.CreateDirectory(_debugConfigurationPath);

            Assert.ThrowsException<UnauthorizedAccessException>(() => CreateController().SetEnabled(true));

            Assert.AreEqual(0, _environment.Count,
                "Rollback must restore missing variables without leaving new diagnostic settings behind.");
            Assert.AreEqual(0, _environmentNotifications);
            Assert.IsFalse(CreateController().IsEnabled());
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
                    if (value == null)
                        _environment.Remove(name);
                    else
                        _environment[name] = value;
                },
                name => _processEnvironment.TryGetValue(name, out string value) ? value : null,
                (name, value) => _processEnvironment[name] = value,
                () => _environmentNotifications++);
        }
    }
}
