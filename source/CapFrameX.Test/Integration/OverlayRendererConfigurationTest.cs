using System;
using System.Collections.Generic;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class OverlayRendererConfigurationTest
    {
        [TestMethod]
        public void Configuration_DefaultsToInGameForNewSettings()
        {
            var settings = CreateSettings();

            var configuration = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);

            Assert.IsTrue(configuration.EnableHookOverlay);
            Assert.IsFalse(configuration.EnableHookFreeOverlay);
            Assert.IsTrue(settings.GetValue<bool>(nameof(IAppConfiguration.EnableHookOverlay)));
        }

        [TestMethod]
        public void Configuration_PreservesExistingHookFreeSelection()
        {
            var settings = CreateSettings();
            settings.SetValue(nameof(IAppConfiguration.EnableHookOverlay), false);
            settings.SetValue(nameof(IAppConfiguration.EnableHookFreeOverlay), true);

            var configuration = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);

            Assert.IsFalse(configuration.EnableHookOverlay);
            Assert.IsTrue(configuration.EnableHookFreeOverlay);
        }

        [TestMethod]
        public void Configuration_PreservesExistingInGameSelection()
        {
            var settings = CreateSettings();
            settings.SetValue(nameof(IAppConfiguration.EnableHookOverlay), true);
            settings.SetValue(nameof(IAppConfiguration.EnableHookFreeOverlay), false);

            var configuration = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);

            Assert.IsTrue(configuration.EnableHookOverlay);
            Assert.IsFalse(configuration.EnableHookFreeOverlay);
            Assert.IsTrue(settings.GetValue<bool>(nameof(IAppConfiguration.EnableHookOverlay)));
        }

        [TestMethod]
        public void Configuration_CanSwitchFromHookFreeToInGame()
        {
            var settings = CreateSettings();
            settings.SetValue(nameof(IAppConfiguration.EnableHookOverlay), false);
            settings.SetValue(nameof(IAppConfiguration.EnableHookFreeOverlay), true);
            var configuration = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);
            object notifiedValue = null;
            using (configuration.OnValueChanged.Subscribe(change =>
            {
                if (change.key == nameof(IAppConfiguration.EnableHookOverlay))
                {
                    notifiedValue = change.value;
                }
            }))
            {
                configuration.EnableHookOverlay = true;
                configuration.EnableHookFreeOverlay = false;
            }

            var reloaded = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);
            Assert.AreEqual(true, notifiedValue);
            Assert.IsTrue(reloaded.EnableHookOverlay);
            Assert.IsFalse(reloaded.EnableHookFreeOverlay);
        }

        [TestMethod]
        public void Configuration_MigratesTheFlagsToTheRenderer()
        {
            var settings = CreateSettings();
            settings.SetValue(nameof(IAppConfiguration.EnableHookOverlay), false);
            settings.SetValue(nameof(IAppConfiguration.EnableHookFreeOverlay), false);

            var configuration = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);

            Assert.AreEqual(EOverlayRenderer.Rtss, configuration.OverlayRenderer);
            Assert.AreEqual("Rtss", settings.GetValue<string>(nameof(IAppConfiguration.OverlayRenderer)));
        }

        [TestMethod]
        public void Configuration_PersistsOffWithTheFlagsOfRtss()
        {
            var settings = CreateSettings();
            var configuration = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);

            configuration.OverlayRenderer = EOverlayRenderer.Off;

            Assert.IsFalse(configuration.EnableHookOverlay);
            Assert.IsFalse(configuration.EnableHookFreeOverlay);
            // A build that only knows the flags reads them as RTSS.
            Assert.IsFalse(settings.GetValue<bool>(nameof(IAppConfiguration.EnableHookOverlay)));
            Assert.IsFalse(settings.GetValue<bool>(nameof(IAppConfiguration.EnableHookFreeOverlay)));
            var reloaded = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);
            Assert.AreEqual(EOverlayRenderer.Off, reloaded.OverlayRenderer);
        }

        // After a rollback to a build that only knows the flags, its last choice wins.
        [TestMethod]
        public void Configuration_FollowsFlagsThatAnOlderBuildChanged()
        {
            var settings = CreateSettings();
            new CapFrameXConfiguration(NullLogger<CapFrameXConfiguration>.Instance, settings)
                .OverlayRenderer = EOverlayRenderer.Off;
            settings.SetValue(nameof(IAppConfiguration.EnableHookFreeOverlay), true);

            var reloaded = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);

            Assert.AreEqual(EOverlayRenderer.HookFree, reloaded.OverlayRenderer);
            Assert.AreEqual("HookFree", settings.GetValue<string>(nameof(IAppConfiguration.OverlayRenderer)));
        }

        [TestMethod]
        public void Configuration_IgnoresAnUnknownStoredRenderer()
        {
            var settings = CreateSettings();
            settings.SetValue(nameof(IAppConfiguration.OverlayRenderer), "Banana");
            settings.SetValue(nameof(IAppConfiguration.EnableHookOverlay), false);
            settings.SetValue(nameof(IAppConfiguration.EnableHookFreeOverlay), true);

            var configuration = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);

            Assert.AreEqual(EOverlayRenderer.HookFree, configuration.OverlayRenderer);
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => configuration.OverlayRenderer = (EOverlayRenderer)42);
            Assert.AreEqual(EOverlayRenderer.HookFree, configuration.OverlayRenderer);
        }

        // Subscribers watch different keys; each must already see the final selection.
        [TestMethod]
        public void Configuration_RaisesTheRendererFirstAndOnlyChangedFlagsAfterTheSwitch()
        {
            var settings = CreateSettings();
            var configuration = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);
            var changes = new List<(string Key, object Value, EOverlayRenderer Renderer)>();
            using (configuration.OnValueChanged.Subscribe(change =>
                changes.Add((change.key, change.value, configuration.OverlayRenderer))))
            {
                configuration.OverlayRenderer = EOverlayRenderer.HookFree;
                configuration.OverlayRenderer = EOverlayRenderer.HookFree;
                configuration.OverlayRenderer = EOverlayRenderer.Off;
            }

            CollectionAssert.AreEqual(new[]
            {
                (nameof(IAppConfiguration.OverlayRenderer), (object)EOverlayRenderer.HookFree, EOverlayRenderer.HookFree),
                (nameof(IAppConfiguration.EnableHookOverlay), (object)false, EOverlayRenderer.HookFree),
                (nameof(IAppConfiguration.EnableHookFreeOverlay), (object)true, EOverlayRenderer.HookFree),
                (nameof(IAppConfiguration.OverlayRenderer), (object)EOverlayRenderer.Off, EOverlayRenderer.Off),
                (nameof(IAppConfiguration.EnableHookFreeOverlay), (object)false, EOverlayRenderer.Off),
            }, changes);
        }

        [TestMethod]
        [DataRow(EOverlayRenderer.InGame, false, EOverlayRenderer.Rtss)]
        [DataRow(EOverlayRenderer.HookFree, false, EOverlayRenderer.HookFree)]
        [DataRow(EOverlayRenderer.Off, false, EOverlayRenderer.Off)]
        [DataRow(EOverlayRenderer.Off, true, EOverlayRenderer.InGame)]
        public void WritingTheHookFlag_SelectsARenderer(EOverlayRenderer current, bool value, EOverlayRenderer expected)
        {
            Assert.AreEqual(expected, OverlayRendererSelection.WithHookOverlay(current, value));
        }

        [TestMethod]
        [DataRow(EOverlayRenderer.HookFree, false, EOverlayRenderer.Rtss)]
        [DataRow(EOverlayRenderer.InGame, false, EOverlayRenderer.InGame)]
        [DataRow(EOverlayRenderer.Off, false, EOverlayRenderer.Off)]
        [DataRow(EOverlayRenderer.InGame, true, EOverlayRenderer.HookFree)]
        public void WritingTheHookFreeFlag_SelectsARenderer(EOverlayRenderer current, bool value, EOverlayRenderer expected)
        {
            Assert.AreEqual(expected, OverlayRendererSelection.WithHookFreeOverlay(current, value));
        }

        [TestMethod]
        public void Reconcile_KeepsTheStoredRendererWhileTheFlagsAgree()
        {
            Assert.AreEqual(EOverlayRenderer.Off, OverlayRendererSelection.Reconcile(EOverlayRenderer.Off, false, false));
            Assert.AreEqual(EOverlayRenderer.Off, OverlayRendererSelection.Reconcile(EOverlayRenderer.Off, null, null));
            Assert.AreEqual(EOverlayRenderer.InGame, OverlayRendererSelection.Reconcile(EOverlayRenderer.Off, true, false));
            Assert.AreEqual(EOverlayRenderer.InGame, OverlayRendererSelection.Reconcile(null, null, null));
            Assert.AreEqual(EOverlayRenderer.HookFree, OverlayRendererSelection.Reconcile(null, false, null));
            // Both flags set was never a valid selection; the in-game overlay always won.
            Assert.AreEqual(EOverlayRenderer.InGame, OverlayRendererSelection.Reconcile(null, true, true));
        }

        [TestMethod]
        public void ProfileSharing_RequiresExplicitConsentAndDoesNotInheritLearningSettings()
        {
            var settings = CreateSettings();
            settings.SetValue("HookOverlayAutoCompatibility", true);
            var configuration = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);
            Assert.IsFalse(configuration.ShareOverlayCompatibilityProfiles);
            configuration.ShareOverlayCompatibilityProfiles = true;
            var reloaded = new CapFrameXConfiguration(
                NullLogger<CapFrameXConfiguration>.Instance, settings);
            Assert.IsTrue(reloaded.ShareOverlayCompatibilityProfiles);
            reloaded.ShareOverlayCompatibilityProfiles = false;
            Assert.IsFalse(configuration.ShareOverlayCompatibilityProfiles);
        }

        private static ISettingsStorage CreateSettings()
        {
            var values = new Dictionary<string, object>();
            var settings = new Mock<ISettingsStorage>();
            settings.Setup(storage => storage.GetValue<object>(It.IsAny<string>()))
                .Returns((string key) => values[key]);
            settings.Setup(storage => storage.GetValue<bool>(It.IsAny<string>()))
                .Returns((string key) => (bool)values[key]);
            settings.Setup(storage => storage.GetValue<string>(It.IsAny<string>()))
                .Returns((string key) => (string)values[key]);
            settings.Setup(storage => storage.SetValue(It.IsAny<string>(), It.IsAny<object>()))
                .Callback((string key, object value) => values[key] = value);
            return settings.Object;
        }
    }
}
