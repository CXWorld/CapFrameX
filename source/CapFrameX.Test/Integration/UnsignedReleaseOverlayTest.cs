using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CapFrameX.Configuration;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
#if !CFX_INGAME_OVERLAY
    [TestClass]
    public class UnsignedReleaseOverlayTest
    {
        [TestMethod]
        public void Configuration_MigratesInGameSelectionToHookFree()
        {
            var settings = new MemorySettings();
            settings.SetValue(nameof(IAppConfiguration.EnableHookOverlay), true);
            settings.SetValue(nameof(IAppConfiguration.EnableHookFreeOverlay), false);

            var configuration = CreateConfiguration(settings);

            Assert.IsFalse(configuration.EnableHookOverlay);
            Assert.IsTrue(configuration.EnableHookFreeOverlay);
            Assert.IsFalse(settings.GetValue<bool>(nameof(IAppConfiguration.EnableHookOverlay)));
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Configuration_PreservesSupportedRenderer(bool hookFree)
        {
            var settings = new MemorySettings();
            settings.SetValue(nameof(IAppConfiguration.EnableHookOverlay), false);
            settings.SetValue(nameof(IAppConfiguration.EnableHookFreeOverlay), hookFree);

            var configuration = CreateConfiguration(settings);

            Assert.AreEqual(hookFree, configuration.EnableHookFreeOverlay);
            Assert.IsFalse(configuration.EnableHookOverlay);
        }

        [TestMethod]
        public void Configuration_RejectsInGameActivationAndPublishesDisabledValue()
        {
            var configuration = CreateConfiguration(new MemorySettings());
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
            }

            Assert.IsFalse(configuration.EnableHookOverlay);
            Assert.AreEqual(false, notifiedValue);
            Assert.IsTrue(configuration.EnableHookFreeOverlay);
        }

        [TestMethod]
        public void ReleaseAssembly_ContainsHookFreeRendererWithoutInGameInjection()
        {
            var assembly = typeof(OsdOverlayBridge).Assembly;
            Assert.IsNotNull(assembly.GetType("CapFrameX.OSD.Integration.OsdOverlayBridge"));
            Assert.IsNull(assembly.GetType("CapFrameX.OSD.Integration.HookInjector"));
            Assert.IsNull(assembly.GetType("CapFrameX.OSD.Integration.HookOverlayManager"));
            Assert.IsNull(assembly.GetType("CapFrameX.OSD.Integration.HookMetricsPublisher"));
            Assert.IsFalse(assembly.GetManifestResourceNames().Any(name =>
                name.EndsWith("HookCompatibilityProfiles.xml", StringComparison.Ordinal)));

            var imports = assembly.GetTypes()
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Select(method => method.GetCustomAttribute<DllImportAttribute>())
                .Where(attribute => attribute != null)
                .Select(attribute => attribute.EntryPoint)
                .ToArray();
            CollectionAssert.DoesNotContain(imports, "CreateRemoteThread");
            CollectionAssert.DoesNotContain(imports, "VirtualAllocEx");
            CollectionAssert.DoesNotContain(imports, "WriteProcessMemory");
        }

        [TestMethod]
        public void Status_ExplainsCertificateRequirement()
        {
            using (var status = new HookOverlayStatusService())
            {
                Assert.IsFalse(OverlayAvailability.IsInGameAvailable);
                Assert.AreEqual(EHookOverlayStatus.Disabled, status.Current.State);
                Assert.AreEqual(OverlayAvailability.InGameUnavailableMessage, status.Current.Detail);
            }
        }

        private static CapFrameXConfiguration CreateConfiguration(MemorySettings settings)
        {
            return new CapFrameXConfiguration(NullLogger<CapFrameXConfiguration>.Instance, settings);
        }

        private sealed class MemorySettings : ISettingsStorage
        {
            private readonly Dictionary<string, object> _values = new Dictionary<string, object>();

            public Task Load() => Task.CompletedTask;

            public T GetValue<T>(string key) => (T)_values[key];

            public void SetValue(string key, object value) => _values[key] = value;
        }
    }
#endif
}
