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
