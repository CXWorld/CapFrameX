using System;
using System.Reactive.Subjects;
using System.Reflection;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using CapFrameX.OSD.Interop;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// "Scale with resolution" reaches the in-game renderers as a metrics header flag and the
    /// hook-free renderer as a reference height; the native core does the scaling itself.
    /// </summary>
    [TestClass]
    public class OverlayResolutionScalingTest
    {
        [TestMethod]
        public void HeaderFlags_CarryTheResolutionScalingBitOnlyWhileEnabled()
        {
            var config = new Mock<IAppConfiguration>();
            config.SetupAllProperties();
            config.Object.OsdZoom = 150;
            config.Object.OsdReplayBufferSize = 500;

            uint off = HookMetricsPublisher.BuildHeaderFlags(config.Object);
            config.Object.OsdScaleWithResolution = true;
            uint on = HookMetricsPublisher.BuildHeaderFlags(config.Object);

            Assert.AreEqual(0u, off & HookMetricsChannel.FlagResolutionScale);
            Assert.AreEqual(HookMetricsChannel.FlagResolutionScale, on & HookMetricsChannel.FlagResolutionScale);
            Assert.AreEqual(off, on & ~HookMetricsChannel.FlagResolutionScale,
                "The new bit must leave every other header field unchanged.");
            Assert.AreEqual(150u, (on >> HookMetricsChannel.ZoomShift) & 0xFFu,
                "The zoom still travels separately; the hook multiplies it by the resolution factor.");
        }

        [TestMethod]
        public void HeaderFlags_ResolutionScalingBitDoesNotOverlapAnotherField()
        {
            const uint lowFlagBits = 0xFFu;
            uint flags = HookMetricsChannel.FlagResolutionScale;
            Assert.AreEqual(flags, flags & lowFlagBits, "Bits 8..31 carry the opacity, zoom and replay buffer.");
            foreach (uint other in new[] { HookMetricsChannel.FlagPresentMonGraph,
                         HookMetricsChannel.FlagBackgroundAlpha, HookMetricsChannel.FlagDisableValueSmoothing,
                         HookMetricsChannel.FlagZoom, HookMetricsChannel.FlagReplayBuffer })
                Assert.AreEqual(0u, flags & other);
        }

        [TestMethod]
        public void HookFreeBridge_ForwardsTheReferenceHeightAtStartAndOnChange()
        {
            using var configChanges = new Subject<(string key, object value)>();
            using var active = new Subject<bool>();
            using var entries = new Subject<IOverlayEntry[]>();
            var config = new Mock<IAppConfiguration>();
            config.SetupGet(x => x.OnValueChanged).Returns(configChanges);
            config.SetupGet(x => x.OsdScaleWithResolution).Returns(true);
            var overlay = new Mock<IOverlayService>();
            overlay.SetupGet(x => x.IsOverlayActiveStream).Returns(active);
            overlay.SetupGet(x => x.OnDictionaryUpdated).Returns(entries);

            using var bridge = new OsdOverlayBridge(overlay.Object, config.Object);
            object host = ReadField<object>(bridge, "_osd");
            Assert.AreEqual(OsdHost.DesignReferenceHeight, ReadField<int>(host, "_referenceHeight"),
                "A stored setting must reach the renderer before it is first created.");

            configChanges.OnNext((nameof(IAppConfiguration.OsdScaleWithResolution), false));
            Assert.AreEqual(0, ReadField<int>(host, "_referenceHeight"));

            configChanges.OnNext((nameof(IAppConfiguration.OsdScaleWithResolution), true));
            Assert.AreEqual(OsdHost.DesignReferenceHeight, ReadField<int>(host, "_referenceHeight"));
        }

        private static T ReadField<T>(object target, string name) =>
            (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException($"Missing field: {name}")).GetValue(target);
    }
}
