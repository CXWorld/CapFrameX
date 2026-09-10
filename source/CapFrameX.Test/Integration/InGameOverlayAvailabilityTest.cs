using System;
using System.Collections.Generic;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
#if CFX_INGAME_OVERLAY
    [TestClass]
    public class InGameOverlayAvailabilityTest
    {
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
        public void Configuration_CanReactivateInGameAfterReleaseMigration()
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

        private static ISettingsStorage CreateSettings()
        {
            var values = new Dictionary<string, object>();
            var settings = new Mock<ISettingsStorage>();
            settings.Setup(storage => storage.GetValue<object>(It.IsAny<string>()))
                .Returns((string key) => values[key]);
            settings.Setup(storage => storage.GetValue<bool>(It.IsAny<string>()))
                .Returns((string key) => (bool)values[key]);
            settings.Setup(storage => storage.SetValue(It.IsAny<string>(), It.IsAny<object>()))
                .Callback((string key, object value) => values[key] = value);
            return settings.Object;
        }
    }
#endif
}
