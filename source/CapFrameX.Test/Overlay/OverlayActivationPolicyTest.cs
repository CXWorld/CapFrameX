using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Overlay;
using CapFrameX.Test.Mocks;
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
        [TestMethod]
        [DataRow(true, false, EOverlayRenderer.Rtss, false)]
        [DataRow(true, true, EOverlayRenderer.Rtss, true)]
        [DataRow(true, false, EOverlayRenderer.HookFree, true)]
        [DataRow(true, false, EOverlayRenderer.InGame, true)]
        [DataRow(true, true, EOverlayRenderer.Off, false)]
        [DataRow(false, true, EOverlayRenderer.Rtss, false)]
        public void GetInitialOverlayActiveState_RequiresAvailableRenderer(
            bool configuredActive, bool rtssInstalled, EOverlayRenderer renderer, bool expected)
        {
            bool actual = OverlayService.GetInitialOverlayActiveState(
                configuredActive, rtssInstalled, renderer);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        [DataRow(false, EOverlayRenderer.Rtss, true)]
        [DataRow(true, EOverlayRenderer.Rtss, false)]
        [DataRow(false, EOverlayRenderer.HookFree, false)]
        [DataRow(false, EOverlayRenderer.InGame, false)]
        [DataRow(false, EOverlayRenderer.Off, false)]
        public void ShouldReplaceUnavailableRtss_OnlyReplacesUnavailableRtss(
            bool rtssInstalled, EOverlayRenderer renderer, bool expected)
        {
            bool actual = OverlayService.ShouldReplaceUnavailableRtss(rtssInstalled, renderer);

            Assert.AreEqual(expected, actual);
        }

        [TestMethod]
        [DataRow(false, false, EOverlayRenderer.Rtss, true)]
        [DataRow(true, false, EOverlayRenderer.Rtss, false)]
        [DataRow(true, true, EOverlayRenderer.Rtss, true)]
        [DataRow(true, false, EOverlayRenderer.HookFree, true)]
        [DataRow(true, false, EOverlayRenderer.InGame, true)]
        [DataRow(true, true, EOverlayRenderer.Off, false)]
        [DataRow(false, true, EOverlayRenderer.Off, true)]
        public void CanSetOverlayActive_AllowsDeactivationWithoutRenderer(
            bool requestedActive, bool rtssInstalled, EOverlayRenderer renderer, bool expected)
        {
            bool actual = OverlayViewModel.CanSetOverlayActive(requestedActive, rtssInstalled, renderer);

            Assert.AreEqual(expected, actual);
        }

        // Every overlay switch (Overlay items, OSD options, status bar, hotkey) goes through
        // OverlayActivation, so they refuse and publish alike.
        [TestMethod]
        public void TrySet_StoresAndPublishesTheRequestedState()
        {
            var (configuration, overlay, published) = CreateOverlay(isActive: false, EOverlayRenderer.HookFree);

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
            var (configuration, overlay, published) = CreateOverlay(isActive: false, EOverlayRenderer.Rtss);

            Assert.IsFalse(OverlayActivation.TrySet(configuration.Object, overlay.Object,
                isRTSSInstalled: false, active: true));
            Assert.IsFalse(configuration.Object.IsOverlayActive);
            Assert.AreEqual(0, published.Count);
        }

        // Off means no renderer at all: even an installed RTSS must not be activated.
        [TestMethod]
        public void TrySet_RefusesActivationWhileTheRendererIsOff()
        {
            var (configuration, overlay, published) = CreateOverlay(isActive: false, EOverlayRenderer.Off);

            Assert.IsFalse(OverlayActivation.TrySet(configuration.Object, overlay.Object,
                isRTSSInstalled: true, active: true));
            Assert.IsFalse(configuration.Object.IsOverlayActive);
            Assert.AreEqual(0, published.Count);
        }

        [TestMethod]
        public void TrySet_AlwaysAllowsSwitchingOffAStaleActiveState()
        {
            var (configuration, overlay, published) = CreateOverlay(isActive: true, EOverlayRenderer.Rtss);

            Assert.IsTrue(OverlayActivation.TrySet(configuration.Object, overlay.Object,
                isRTSSInstalled: false, active: false));
            Assert.IsFalse(configuration.Object.IsOverlayActive);
            CollectionAssert.AreEqual(new[] { false }, published);
        }

        private static (Mock<IAppConfiguration> configuration, Mock<IOverlayService> overlay, List<bool> published)
            CreateOverlay(bool isActive, EOverlayRenderer renderer)
        {
            var configuration = new Mock<IAppConfiguration>();
            configuration.SetupAllProperties();
            configuration.WireOverlayRenderer(renderer);
            configuration.Object.IsOverlayActive = isActive;

            var published = new List<bool>();
            var stream = new Subject<bool>();
            stream.Subscribe(published.Add);
            var overlay = new Mock<IOverlayService>();
            overlay.SetupGet(service => service.IsOverlayActiveStream).Returns(stream);
            return (configuration, overlay, published);
        }
    }
}
