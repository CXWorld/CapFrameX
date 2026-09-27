using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Hotkey;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Hotkey
{
    [TestClass]
    [DoNotParallelize]
    public class HotkeyDictionaryBuilderTest
    {
        private static readonly FieldInfo ActionsField = typeof(HotkeyDictionaryBuilder)
            .GetField("_registeredActions", BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly FieldInfo SnapshotField = typeof(HotkeyDictionaryBuilder).Assembly
            .GetType("CapFrameX.Hotkey.GlobalHotkeyHook")
            .GetField("_registrations", BindingFlags.NonPublic | BindingFlags.Static);

        private DictionaryEntry[] _originalActions;
        private object _originalSnapshot;

        [TestInitialize]
        public void Initialize()
        {
            var actions = (IDictionary)ActionsField.GetValue(null);
            _originalActions = actions.Keys.Cast<object>().Select(key => new DictionaryEntry(key, actions[key])).ToArray();
            _originalSnapshot = SnapshotField.GetValue(null);
            actions.Clear();
        }

        [TestCleanup]
        public void Cleanup()
        {
            var actions = (IDictionary)ActionsField.GetValue(null);
            actions.Clear();
            foreach (var entry in _originalActions)
                actions.Add(entry.Key, entry.Value);
            SnapshotField.SetValue(null, _originalSnapshot);
        }

        [DataTestMethod]
        [DataRow(nameof(IAppConfiguration.CaptureHotKey), HotkeyAction.Capture, "F11")]
        [DataRow(nameof(IAppConfiguration.OverlayHotKey), HotkeyAction.Overlay, "Alt+O")]
        [DataRow(nameof(IAppConfiguration.OverlayConfigHotKey), HotkeyAction.OverlayConfig, "Alt+C")]
        [DataRow(nameof(IAppConfiguration.OverlayPositionHotkey), HotkeyAction.OverlayPosition, "Alt+P")]
        [DataRow(nameof(IAppConfiguration.ResetHistoryHotkey), HotkeyAction.ResetHistory, "Alt+R")]
        [DataRow(nameof(IAppConfiguration.ThreadAffinityHotkey), HotkeyAction.ThreadAffinity, "Control+A")]
        [DataRow(nameof(IAppConfiguration.ResetMetricsHotkey), HotkeyAction.ResetMetrics, "Alt+M")]
        public void Refresh_DisablingImmediatelyRemovesOldKeyAndReassigningRestoresAction(
            string propertyName, HotkeyAction hotkeyAction, string originalKey)
        {
            var config = new Mock<IAppConfiguration>();
            config.SetupAllProperties();
            var property = typeof(IAppConfiguration).GetProperty(propertyName);
            property.SetValue(config.Object, originalKey);
            int triggered = 0;
            HotkeyDictionaryBuilder.SetHotkey(config.Object, hotkeyAction, () => triggered++);

            Trigger(originalKey);
            Assert.AreEqual(1, triggered);

            property.SetValue(config.Object, string.Empty);
            HotkeyDictionaryBuilder.Refresh(config.Object);
            Trigger(originalKey);
            Assert.AreEqual(1, triggered, "The old hotkey must stop working immediately.");
            Assert.AreEqual(0, ((Array)SnapshotField.GetValue(null)).Length);

            property.SetValue(config.Object, "Control+F9");
            HotkeyDictionaryBuilder.Refresh(config.Object);
            Trigger(originalKey);
            Assert.AreEqual(1, triggered);
            Trigger("Control+F9");
            Assert.AreEqual(2, triggered, "Refresh must retain the action while its key is disabled.");
        }

        [TestMethod]
        public void DisablingOneHotkey_PreservesOtherAssignedHotkeys()
        {
            var config = new Mock<IAppConfiguration>();
            config.SetupAllProperties();
            config.Object.CaptureHotKey = "F11";
            config.Object.OverlayHotKey = "Alt+O";
            int captureTriggered = 0;
            int overlayTriggered = 0;
            HotkeyDictionaryBuilder.SetHotkey(config.Object, HotkeyAction.Capture, () => captureTriggered++);
            HotkeyDictionaryBuilder.SetHotkey(config.Object, HotkeyAction.Overlay, () => overlayTriggered++);

            config.Object.CaptureHotKey = string.Empty;
            HotkeyDictionaryBuilder.Refresh(config.Object);
            Trigger("F11");
            Trigger("Alt+O");

            Assert.AreEqual(0, captureTriggered);
            Assert.AreEqual(1, overlayTriggered);
        }

        [TestMethod]
        public void AllHotkeysDisabledAtStartup_CanBeEnabledWithoutRegisteringActionsAgain()
        {
            var config = new Mock<IAppConfiguration>();
            config.SetupAllProperties();
            foreach (var property in typeof(IAppConfiguration).GetProperties()
                .Where(property => property.Name.EndsWith("Hotkey", StringComparison.OrdinalIgnoreCase)))
                property.SetValue(config.Object, string.Empty);

            int triggered = 0;
            foreach (var action in Enum.GetValues<HotkeyAction>())
                HotkeyDictionaryBuilder.SetHotkey(config.Object, action, () => triggered++);
            Assert.AreEqual(0, ((Array)SnapshotField.GetValue(null)).Length);

            config.Object.CaptureHotKey = "F11";
            config.Object.OverlayHotKey = "Alt+O";
            config.Object.OverlayConfigHotKey = "Alt+C";
            config.Object.OverlayPositionHotkey = "Alt+P";
            config.Object.ResetHistoryHotkey = "Alt+R";
            config.Object.ThreadAffinityHotkey = "Control+A";
            config.Object.ResetMetricsHotkey = "Alt+M";
            HotkeyDictionaryBuilder.Refresh(config.Object);

            foreach (var combination in new[] { "F11", "Alt+O", "Alt+C", "Alt+P", "Alt+R", "Control+A", "Alt+M" })
                Trigger(combination);
            Assert.AreEqual(7, triggered);
        }

        private static void Trigger(string combination)
        {
            // Exercise the live snapshot without injecting keyboard input into the desktop.
            foreach (var registration in (Array)SnapshotField.GetValue(null))
            {
                var combinations = (IReadOnlyDictionary<string, Action>)registration.GetType()
                    .GetProperty("Combinations", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(registration);
                if (combinations.TryGetValue(combination, out var action))
                    action();
            }
        }
    }
}
