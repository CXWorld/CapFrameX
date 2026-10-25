using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CapFrameX.Test.Integration
{
    /// <summary>
    /// Ricochet, Call of Duty's kernel anti-cheat, denies the module scan to an elevated
    /// CapFrameX. On 2026-10-25 (Xbox app) the policy blocked cod.exe and sp26-cod.exe with
    /// "module scan failed (5)" and nothing drew. It is recognized from outside now, and the
    /// hook-free overlay stands in for the in-game one.
    /// </summary>
    [TestClass]
    public class RicochetDetectionTest
    {
        private const string Content = @"E:\XboxGames\Call of Duty\Content";
        private const string Driver = Content + @"\Randgrid.sys";

        [TestMethod]
        public void FindRicochetDriver_BesideTheLauncherAndAboveTheTitle()
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Driver };

            Assert.AreEqual(Driver, HookTargetPolicy.FindRicochetDriver(Content + @"\cod.exe",
                files.Contains));
            Assert.AreEqual(Driver, HookTargetPolicy.FindRicochetDriver(
                Content + @"\sp26\sp26-cod.exe", files.Contains));
            Assert.IsNull(HookTargetPolicy.FindRicochetDriver(
                @"E:\Program Files\Epic Games\Metro2033Redux\metro.exe", files.Contains));
            Assert.IsNull(HookTargetPolicy.FindRicochetDriver(null, files.Contains));
        }

        [TestMethod]
        public void FindRicochetDriver_LooksAtMostTwoFoldersUp()
        {
            var files = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Driver };

            Assert.AreEqual(Driver, HookTargetPolicy.FindRicochetDriver(
                Content + @"\a\b\game.exe", files.Contains));
            Assert.IsNull(HookTargetPolicy.FindRicochetDriver(
                Content + @"\a\b\c\game.exe", files.Contains));
        }

        [TestMethod]
        public void GetRicochetReason_ADeniedScanNeedsTheRunningDriver()
        {
            string beside = HookTargetPolicy.GetRicochetReason(Driver, 0, null);
            StringAssert.StartsWith(beside, HookTargetPolicy.RicochetReasonPrefix);
            StringAssert.Contains(beside, "Randgrid.sys");

            string service = HookTargetPolicy.GetRicochetReason(null, 5, "atvi-randgrid_msstore");
            StringAssert.StartsWith(service, HookTargetPolicy.RicochetReasonPrefix);
            StringAssert.Contains(service, "atvi-randgrid_msstore");

            // Other protected processes deny the scan as well.
            Assert.IsNull(HookTargetPolicy.GetRicochetReason(null, 5, null));
            Assert.IsNull(HookTargetPolicy.GetRicochetReason(null, 87, "atvi-randgrid_msstore"));
        }

        [TestMethod]
        public void IsRicochetDriverService_MatchesTheStoreVariants()
        {
            Assert.IsTrue(HookTargetPolicy.IsRicochetDriverService("atvi-randgrid_msstore"));
            Assert.IsTrue(HookTargetPolicy.IsRicochetDriverService("ATVI-RANDGRID_SR"));
            Assert.IsFalse(HookTargetPolicy.IsRicochetDriverService("atvi-other"));
            Assert.IsFalse(HookTargetPolicy.IsRicochetDriverService(null));
        }

        [TestMethod]
        public void TryReadImagePath_NeedsNoModuleAccess()
        {
            using var current = Process.GetCurrentProcess();

            Assert.IsTrue(HookTargetPolicy.TryReadImagePath(current.Id, out string path));
            Assert.AreEqual(current.MainModule.FileName, path, ignoreCase: true);
            Assert.IsFalse(HookTargetPolicy.TryReadImagePath(0, out _));
        }

        [TestMethod]
        public void HookFreeFallback_OnlyForRicochetWhileTheInGameOverlayIsUnavailable()
        {
            string ricochet = HookTargetPolicy.GetRicochetReason(Driver, 0, null);
            Assert.IsTrue(HookTargetPolicy.RequestsHookFreeFallback(ricochet));
            Assert.IsTrue(HookOverlayManager.IsHookFreeFallback(Unavailable(ricochet), ricochet));

            // Another renderer selected, the game gone or its runtime still unknown.
            Assert.IsFalse(HookOverlayManager.IsHookFreeFallback(HookOverlayManager
                .GetInGameUnavailableReason(false, 24592, "DXGI", ricochet), ricochet));
            Assert.IsFalse(HookOverlayManager.IsHookFreeFallback(HookOverlayManager
                .GetInGameUnavailableReason(true, 24592, "DXGI", ricochet,
                    targetProcessAlive: false), ricochet));
            Assert.IsFalse(HookOverlayManager.IsHookFreeFallback(HookOverlayManager
                .GetInGameUnavailableReason(true, 24592, null, ricochet), ricochet));

            // Every other block leaves the game without an overlay, as before.
            foreach (string other in new[]
            {
                "module scan failed (5)",
                "anti-cheat module 'EasyAntiCheat_EOS.dll' matched 'easyanticheat'",
                "process 'cs2' is on the in-game injection blacklist"
            })
            {
                Assert.IsFalse(HookTargetPolicy.RequestsHookFreeFallback(other), other);
                Assert.IsFalse(HookOverlayManager.IsHookFreeFallback(Unavailable(other), other), other);
            }
        }

        [TestMethod]
        public void CreateHookFreeFallbackStatus_IsBlockedWhileTheOverlayIsOn()
        {
            string reason = Unavailable(HookTargetPolicy.GetRicochetReason(Driver, 0, null));

            HookOverlayStatus shown = HookOverlayManager.CreateHookFreeFallbackStatus(
                24592, "DXGI", visible: true, reason);
            Assert.AreEqual(EHookOverlayStatus.Blocked, shown.State);
            StringAssert.Contains(shown.Detail, HookTargetPolicy.RicochetReasonPrefix);
            StringAssert.Contains(shown.Detail, "the hook-free overlay is shown instead");

            HookOverlayStatus hidden = HookOverlayManager.CreateHookFreeFallbackStatus(
                24592, "DXGI", visible: false, reason);
            Assert.AreEqual(EHookOverlayStatus.Hidden, hidden.State);
        }

        [TestMethod]
        public void Bridge_ExistsAndShowsWhileItStandsIn()
        {
            Assert.IsTrue(OsdOverlayBridge.Exists(enabled: false, fallback: true));
            Assert.IsTrue(OsdOverlayBridge.IsShown(enabled: false, fallback: true,
                active: true, hasProcesses: true));
            // The usual soft-hides apply to the stand-in too.
            Assert.IsFalse(OsdOverlayBridge.IsShown(false, true, active: false, hasProcesses: true));
            Assert.IsFalse(OsdOverlayBridge.IsShown(false, true, active: true, hasProcesses: false));
            Assert.IsFalse(OsdOverlayBridge.Exists(enabled: false, fallback: false));
        }

        private static string Unavailable(string targetBlockReason)
            => HookOverlayManager.GetInGameUnavailableReason(true, 24592, "DXGI", targetBlockReason);
    }
}
