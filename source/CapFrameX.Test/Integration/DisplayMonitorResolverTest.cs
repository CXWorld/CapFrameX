using System;
using System.Windows.Forms;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class DisplayMonitorResolverTest
    {
        [TestMethod]
        public void MonitorIndexCache_ResolvesAgainOnALayoutChangeAnotherDisplayOrAfterItsLifetime()
        {
            long now = 1000, signature = 7;
            int resolutions = 0, index = 1;
            var cache = new DisplayMonitorResolver.MonitorIndexCache(name => { resolutions++; return index; },
                () => signature, () => now);

            Assert.AreEqual(1, cache.Get(@"\\.\DISPLAY2"));
            now += 1000;
            Assert.AreEqual(1, cache.Get(@"\\.\display2"), "device names compare without case");
            Assert.AreEqual(1, resolutions, "an unchanged layout is not enumerated again");

            index = 0;
            signature = 8; // a display was added, removed or moved
            Assert.AreEqual(0, cache.Get(@"\\.\DISPLAY2"));
            Assert.AreEqual(2, resolutions);

            index = 2;
            Assert.AreEqual(2, cache.Get(@"\\.\DISPLAY3"), "another selected display");
            Assert.AreEqual(3, resolutions);

            index = 1;
            now += DisplayMonitorResolver.CacheLifetimeMs - 1;
            Assert.AreEqual(2, cache.Get(@"\\.\DISPLAY3"));
            now += 1;
            Assert.AreEqual(1, cache.Get(@"\\.\DISPLAY3"), "a new primary display shows within the lifetime");
            Assert.AreEqual(4, resolutions);
        }

        [TestMethod]
        public void GetMonitorIndexCached_EqualsTheUncachedIndex()
        {
            Assert.AreEqual(DisplayMonitorResolver.GetMonitorIndex(null), DisplayMonitorResolver.GetMonitorIndexCached(null));
            string first = Screen.AllScreens[0].DeviceName;
            Assert.AreEqual(DisplayMonitorResolver.GetMonitorIndex(first), DisplayMonitorResolver.GetMonitorIndexCached(first));
        }

        [TestMethod]
        public void FindMonitorIndex_SelectedDisplayIsNotPrimary_ReturnsSelectedIndex()
        {
            var monitors = CreateMonitors();

            int index = DisplayMonitorResolver.FindMonitorIndex(monitors, @"\\.\DISPLAY3");

            Assert.AreEqual(2, index);
        }

        [TestMethod]
        public void FindMonitorIndex_NoSelection_ReturnsPrimaryIndex()
        {
            var monitors = CreateMonitors();

            int index = DisplayMonitorResolver.FindMonitorIndex(monitors, string.Empty);

            Assert.AreEqual(1, index);
        }

        [TestMethod]
        public void FindMonitorIndex_SelectedDisplayIsMissing_ReturnsPrimaryIndex()
        {
            var monitors = CreateMonitors();

            int index = DisplayMonitorResolver.FindMonitorIndex(monitors, @"\\.\DISPLAY9");

            Assert.AreEqual(1, index);
        }

        [TestMethod]
        public void FindMonitorIndex_NoPrimaryFlag_FallsBackToFirstMonitor()
        {
            var monitors = new[]
            {
                new DisplayMonitorResolver.MonitorDescriptor(@"\\.\DISPLAY1", false),
                new DisplayMonitorResolver.MonitorDescriptor(@"\\.\DISPLAY2", false)
            };

            int index = DisplayMonitorResolver.FindMonitorIndex(monitors, string.Empty);

            Assert.AreEqual(0, index);
        }

        [TestMethod]
        public void GetMonitorIndex_MatchesEveryCurrentScreen()
        {
            Screen[] screens = Screen.AllScreens;

            for (int i = 0; i < screens.Length; i++)
            {
                Assert.AreEqual(i, DisplayMonitorResolver.GetMonitorIndex(screens[i].DeviceName));
            }
        }

        [TestMethod]
        public void GetMonitorIndex_NoSelectionMatchesCurrentPrimaryScreen()
        {
            int expected = Array.FindIndex(Screen.AllScreens, screen => screen.Primary);

            int actual = DisplayMonitorResolver.GetMonitorIndex(string.Empty);

            Assert.AreEqual(expected < 0 ? 0 : expected, actual);
        }

        private static DisplayMonitorResolver.MonitorDescriptor[] CreateMonitors()
        {
            return new[]
            {
                new DisplayMonitorResolver.MonitorDescriptor(@"\\.\DISPLAY1", false),
                new DisplayMonitorResolver.MonitorDescriptor(@"\\.\DISPLAY2", true),
                new DisplayMonitorResolver.MonitorDescriptor(@"\\.\DISPLAY3", false)
            };
        }
    }
}
