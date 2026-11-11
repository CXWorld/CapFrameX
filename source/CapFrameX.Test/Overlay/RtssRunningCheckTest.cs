using CapFrameX.RTSSIntegration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Overlay
{
    /// <summary>
    /// When the RTSS service lists the processes to learn whether RTSS runs. Its shared memory
    /// answers on every overlay tick; the process list only confirms it now and then.
    /// </summary>
    [TestClass]
    public class RtssRunningCheckTest
    {
        [TestMethod]
        public void MissingSharedMemory_AlwaysConsultsTheProcessList()
        {
            // RTSS may be starting and not have created its section yet.
            Assert.IsTrue(RTSSService.ShouldConsultProcessList(false, 0));
            Assert.IsTrue(RTSSService.ShouldConsultProcessList(false, long.MaxValue));
        }

        [TestMethod]
        public void ValidSharedMemory_IsTrustedBetweenProcessListChecks()
        {
            // Regression: every overlay tick took a system-wide process snapshot although the
            // shared memory already told that RTSS was running.
            Assert.IsTrue(RTSSService.ShouldConsultProcessList(true, long.MaxValue), "The first tick confirms.");
            Assert.IsFalse(RTSSService.ShouldConsultProcessList(true, 0));
            Assert.IsFalse(RTSSService.ShouldConsultProcessList(true, RTSSService.ProcessListIntervalMs - 1));
            Assert.IsTrue(RTSSService.ShouldConsultProcessList(true, RTSSService.ProcessListIntervalMs),
                "A game RTSS hooked keeps the section alive after an RTSS crash; the process list catches that.");
        }

        [TestMethod]
        public void LaunchDecision_DefersOnlyForARunningVulkanGame()
        {
            Assert.AreEqual(RTSSService.RTSSLaunchDecision.NotInstalled,
                RTSSService.DecideLaunch(isInstalled: false, isRTSSRunning: false, gameIsRunning: false, gamePresentsWithVulkan: false));
            Assert.AreEqual(RTSSService.RTSSLaunchDecision.AlreadyRunning,
                RTSSService.DecideLaunch(isInstalled: true, isRTSSRunning: true, gameIsRunning: true, gamePresentsWithVulkan: true));
            Assert.AreEqual(RTSSService.RTSSLaunchDecision.DeferredVulkanGameRunning,
                RTSSService.DecideLaunch(isInstalled: true, isRTSSRunning: false, gameIsRunning: true, gamePresentsWithVulkan: true));
            // Injecting into a live DXGI game is RTSS' normal operating mode.
            Assert.AreEqual(RTSSService.RTSSLaunchDecision.Launch,
                RTSSService.DecideLaunch(isInstalled: true, isRTSSRunning: false, gameIsRunning: true, gamePresentsWithVulkan: false));
            Assert.AreEqual(RTSSService.RTSSLaunchDecision.Launch,
                RTSSService.DecideLaunch(isInstalled: true, isRTSSRunning: false, gameIsRunning: false, gamePresentsWithVulkan: false));
        }
    }
}
