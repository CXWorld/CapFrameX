using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Overlay;
using CapFrameX.ViewModel;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Reactive.Subjects;

namespace CapFrameX.Test.Overlay
{
    [TestClass]
    public class OverlayActivationPolicyTest
    {
        [DataTestMethod]
        [DataRow(true, false, false, false, false)]
        [DataRow(true, true, false, false, true)]
        [DataRow(true, false, true, false, true)]
        [DataRow(true, false, false, true, true)]
        [DataRow(false, true, false, false, false)]
        public void GetInitialOverlayActiveState_RequiresAvailableRenderer(
            bool configuredActive, bool rtssInstalled, bool hookFreeEnabled,
            bool hookEnabled, bool expected)
        {
            bool actual = OverlayService.GetInitialOverlayActiveState(
                configuredActive, rtssInstalled, hookFreeEnabled, hookEnabled);

            Assert.AreEqual(expected, actual);
        }

        [DataTestMethod]
        [DataRow(false, false, false, true)]
        [DataRow(true, false, false, false)]
        [DataRow(false, true, false, false)]
        [DataRow(false, false, true, false)]
        [DataRow(false, true, true, false)]
        public void ShouldDefaultToHookFreeOverlay_OnlyReplacesUnavailableRtss(
            bool rtssInstalled, bool hookFreeEnabled, bool hookEnabled, bool expected)
        {
            bool actual = OverlayService.ShouldDefaultToHookFreeOverlay(
                rtssInstalled, hookFreeEnabled, hookEnabled);

            Assert.AreEqual(expected, actual);
        }

        [DataTestMethod]
        [DataRow(false, false, false, false, true)]
        [DataRow(true, false, false, false, false)]
        [DataRow(true, true, false, false, true)]
        [DataRow(true, false, true, false, true)]
        [DataRow(true, false, false, true, true)]
        public void CanSetOverlayActive_AllowsDeactivationWithoutRenderer(
            bool requestedActive, bool rtssInstalled, bool hookFreeEnabled,
            bool hookEnabled, bool expected)
        {
            bool actual = OverlayViewModel.CanSetOverlayActive(
                requestedActive, rtssInstalled, hookFreeEnabled, hookEnabled);

            Assert.AreEqual(expected, actual);
        }

        // Every overlay switch (Overlay items, OSD options, status bar, hotkey) goes through
        // OverlayActivation, so they refuse and publish alike.
        [TestMethod]
        public void TrySet_StoresAndPublishesTheRequestedState()
        {
            var (configuration, overlay, published) = CreateOverlay(isActive: false, hookFree: true);

            Assert.IsTrue(OverlayActivation.TrySet(configuration.Object, overlay.Object,
                isRTSSInstalled: false, active: true));
            Assert.IsTrue(configuration.Object.IsOverlayActive);
            CollectionAssert.AreEqual(new[] { true }, published);

            Assert.IsTrue(OverlayActivation.TrySet(configuration.Object, overlay.Object,
                isRTSSInstalled: false, active: false));
            Assert.IsFalse(configuration.Object.IsOverlayActive);
            CollectionAssert.AreEqual(new[] { true, false }, published);
        }

        [TestMethod]
        public void TrySet_RefusesActivationWithoutRendererAndPublishesNothing()
        {
            var (configuration, overlay, published) = CreateOverlay(isActive: false, hookFree: false);

            Assert.IsFalse(OverlayActivation.TrySet(configuration.Object, overlay.Object,
                isRTSSInstalled: false, active: true));
            Assert.IsFalse(configuration.Object.IsOverlayActive);
            Assert.AreEqual(0, published.Count);
        }

        [TestMethod]
        public void TrySet_AlwaysAllowsSwitchingOffAStaleActiveState()
        {
            var (configuration, overlay, published) = CreateOverlay(isActive: true, hookFree: false);

            Assert.IsTrue(OverlayActivation.TrySet(configuration.Object, overlay.Object,
                isRTSSInstalled: false, active: false));
            Assert.IsFalse(configuration.Object.IsOverlayActive);
            CollectionAssert.AreEqual(new[] { false }, published);
        }

        private static (Mock<IAppConfiguration> configuration, Mock<IOverlayService> overlay, List<bool> published)
            CreateOverlay(bool isActive, bool hookFree)
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupAllProperties();
            configuration.Object.IsOverlayActive = isActive;
            configuration.Object.EnableHookFreeOverlay = hookFree;
            configuration.Object.EnableHookOverlay = false;

            var published = new List<bool>();
            var stream = new Subject<bool>();
            stream.Subscribe(published.Add);
            var overlay = new Mock<IOverlayService>();
            overlay.SetupGet(service => service.IsOverlayActiveStream).Returns(stream);
            return (configuration, overlay, published);
        }
    }
}
