using CapFrameX.Capture.Contracts;
using CapFrameX.Data.CaptureTarget;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace CapFrameX.Test.Data
{
    [TestClass]
    public class CaptureTargetResolverTest
    {
        private const string Composed = "Composed: Flip";
        private const string IndependentFlip = "Hardware: Independent Flip";

        [TestMethod]
        public void NoCandidate_SelectsNothing()
        {
            var resolution = CaptureTargetResolver.Resolve(new List<CaptureTargetCandidate>(), 0, captureRequest: false);

            Assert.IsNull(resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.NoCandidate, resolution.Reason);
        }

        [TestMethod]
        public void SingleCandidate_IsSelectedWithoutEvidence()
        {
            var launcher = Candidate("launcher", 100, idleSeconds: 60);

            var resolution = Resolve(0, launcher);

            Assert.AreSame(launcher, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.OnlyCandidate, resolution.Reason);
        }

        [TestMethod]
        public void GpuDominantProcess_TakesTheTargetFromThePreviousOne()
        {
            var game = Candidate("game", 100, gpuBusyShare: 0.62);
            var browser = Candidate("msedge", 200, gpuBusyShare: 0.04);

            var resolution = Resolve(previousTarget: 200, game, browser);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.GpuDominant, resolution.Reason);
            StringAssert.Contains(resolution.Detail, "62 %");
            StringAssert.Contains(resolution.Detail, "'msedge' 4 %");
        }

        [TestMethod]
        public void SimilarGpuLoad_KeepsThePreviousTarget()
        {
            // Below the ratio in both directions: the target does not flap between the two.
            var game = Candidate("game", 100, gpuBusyShare: 0.30);
            var other = Candidate("other", 200, gpuBusyShare: 0.15);

            Assert.AreSame(other, Resolve(previousTarget: 200, game, other).Target);
            Assert.AreSame(game, Resolve(previousTarget: 100, game, other).Target);
            Assert.AreEqual(ECaptureTargetReason.PreviousTarget, Resolve(previousTarget: 200, game, other).Reason);
        }

        [TestMethod]
        public void GpuShareBelowTheMinimum_IsNoDominance()
        {
            var game = Candidate("game", 100, gpuBusyShare: CaptureTargetResolver.MinimumDominantGpuShare - 0.01);
            var tool = Candidate("tool", 200, gpuBusyShare: 0);

            var resolution = Resolve(0, game, tool);

            Assert.IsNull(resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.Ambiguous, resolution.Reason);
        }

        [TestMethod]
        public void UnmeasuredCandidate_BlocksTheGpuDominance()
        {
            // The game has just started and has no complete second yet; counted as idle, the
            // browser would take the target from it.
            var game = Candidate("game", 100, gpuBusyShare: null);
            var browser = Candidate("msedge", 200, gpuBusyShare: 0.5);

            var resolution = Resolve(previousTarget: 100, game, browser);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.PreviousTarget, resolution.Reason);
        }

        [TestMethod]
        public void PreviousTarget_StaysWhileItIsMinimized()
        {
            var game = Candidate("game", 100, gpuBusyShare: 0, presentsPerSecond: 0, idleSeconds: 40);
            var browser = Candidate("msedge", 200, gpuBusyShare: 0.05, presentMode: IndependentFlip);

            var resolution = Resolve(previousTarget: 100, game, browser);

            Assert.AreSame(game, resolution.Target);
        }

        [TestMethod]
        public void PreviousTarget_ThatLeftTheList_IsIgnored()
        {
            var game = Candidate("game", 100, presentMode: IndependentFlip);
            var browser = Candidate("msedge", 200);

            var resolution = Resolve(previousTarget: 300, game, browser);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.PresentsLikeAGame, resolution.Reason);
        }

        [TestMethod]
        public void CaptureRequest_TakesTheProcessInTheForeground()
        {
            var game = Candidate("game", 100, gpuBusyShare: 0.05);
            var benchmark = Candidate("benchmark", 200, gpuBusyShare: 0.8);
            game.IsForegroundNow = true;

            var request = CaptureTargetResolver.Resolve(new[] { game, benchmark }, 200, captureRequest: true);
            var heartbeat = CaptureTargetResolver.Resolve(new[] { game, benchmark }, 200, captureRequest: false);

            Assert.AreSame(game, request.Target);
            Assert.AreEqual(ECaptureTargetReason.ForegroundAtCapture, request.Reason);
            Assert.AreSame(benchmark, heartbeat.Target, "Outside a capture request the foreground does not decide.");
        }

        [TestMethod]
        public void CaptureRequest_IgnoresAnIdleProcessInTheForeground()
        {
            var launcher = Candidate("launcher", 100, gpuBusyShare: 0, idleSeconds: 30);
            var game = Candidate("game", 200, gpuBusyShare: 0.4);
            launcher.IsForegroundNow = true;

            var resolution = CaptureTargetResolver.Resolve(new[] { launcher, game }, 0, captureRequest: true);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.GpuDominant, resolution.Reason);
        }

        [TestMethod]
        public void LastForeground_DecidesWithoutAPreviousTarget()
        {
            var game = Candidate("game", 100, gpuBusyShare: 0.2);
            var browser = Candidate("msedge", 200, gpuBusyShare: 0.1);
            game.SecondsSinceForeground = 4;
            browser.SecondsSinceForeground = 90;

            var resolution = Resolve(0, game, browser);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.LastForeground, resolution.Reason);
            StringAssert.Contains(resolution.Detail, "4 s ago");
        }

        [TestMethod]
        public void LastForeground_SkipsAProcessThatStoppedPresenting()
        {
            var launcher = Candidate("launcher", 100, idleSeconds: 20);
            var game = Candidate("game", 200);
            launcher.SecondsSinceForeground = 1;

            var resolution = Resolve(0, launcher, game);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.OnlyActive, resolution.Reason);
        }

        [TestMethod]
        public void OnlyActiveProcess_WinsOverAnIdleOne()
        {
            var launcher = Candidate("launcher", 100, idleSeconds: CaptureTargetResolver.ActiveIdleSeconds + 1);
            var game = Candidate("game", 200, idleSeconds: CaptureTargetResolver.ActiveIdleSeconds);

            var resolution = Resolve(0, launcher, game);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.OnlyActive, resolution.Reason);
        }

        [TestMethod]
        public void KnownGame_WinsAmongActiveProcesses()
        {
            var game = Candidate("game", 100);
            var browser = Candidate("msedge", 200, presentMode: IndependentFlip);
            var idleKnownGame = Candidate("oldgame", 300, idleSeconds: 30);
            game.IsKnownGame = true;
            idleKnownGame.IsKnownGame = true;

            var resolution = Resolve(0, game, browser, idleKnownGame);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.KnownGame, resolution.Reason);
        }

        [TestMethod]
        public void GameLibraryFolder_CountsAsAKnownGame()
        {
            var game = Candidate("game", 100);
            var browser = Candidate("msedge", 200);
            game.IsInGameLibrary = true;

            var resolution = Resolve(0, game, browser);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.KnownGame, resolution.Reason);
            StringAssert.Contains(resolution.Detail, "game library");
        }

        [TestMethod]
        public void IndependentFlip_PresentsLikeAGame()
        {
            var game = Candidate("game", 100, presentMode: "Hardware Composed: Independent Flip");
            var browser = Candidate("msedge", 200, presentMode: Composed);

            var resolution = Resolve(0, game, browser);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.PresentsLikeAGame, resolution.Reason);
        }

        [TestMethod]
        public void GeneratedFrames_PresentLikeAGame()
        {
            var game = Candidate("game", 100, presentMode: Composed, generatedFrames: true);
            var browser = Candidate("msedge", 200, presentMode: Composed);

            Assert.AreSame(game, Resolve(0, game, browser).Target);
        }

        [TestMethod]
        public void TwoGames_StayAmbiguous()
        {
            var first = Candidate("first", 100, gpuBusyShare: 0.4, presentMode: IndependentFlip);
            var second = Candidate("second", 200, gpuBusyShare: 0.3, presentMode: IndependentFlip);
            first.IsKnownGame = true;
            second.IsKnownGame = true;

            var resolution = Resolve(0, first, second);

            Assert.IsNull(resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.Ambiguous, resolution.Reason);
        }

        [TestMethod]
        public void ProcessesOfOneExecutable_SelectTheFirstAsBefore()
        {
            var first = Candidate("game", 100);
            var second = Candidate("game", 200);

            var resolution = Resolve(0, first, second);

            Assert.AreSame(first, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.SameExecutable, resolution.Reason);
        }

        [TestMethod]
        public void WithoutStatistics_TheProcessListStillDecides()
        {
            var game = new CaptureTargetCandidate("game", 100) { IsKnownGame = true };
            var tool = new CaptureTargetCandidate("tool", 200);

            var resolution = Resolve(0, game, tool);

            Assert.AreSame(game, resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.KnownGame, resolution.Reason);
        }

        [TestMethod]
        public void WithoutStatistics_NothingElseDecides()
        {
            var resolution = Resolve(0, new CaptureTargetCandidate("game", 100), new CaptureTargetCandidate("tool", 200));

            Assert.IsNull(resolution.Target);
            Assert.AreEqual(ECaptureTargetReason.Ambiguous, resolution.Reason);
        }

        [TestMethod]
        [DataRow(@"D:\SteamLibrary\steamapps\common\Cyberpunk 2077\bin\x64\Cyberpunk2077.exe", true)]
        [DataRow(@"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe", true)]
        [DataRow(@"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe", false)]
        [DataRow(@"C:\Program Files (x86)\GOG Galaxy\Games\The Witcher 3\bin\x64\witcher3.exe", true)]
        [DataRow(@"E:\XboxGames\Forza Horizon 5\Content\ForzaHorizon5.exe", true)]
        [DataRow(@"C:\Program Files (x86)\Ubisoft\Ubisoft Game Launcher\games\Anno 1800\Bin\Win64\Anno1800.exe", true)]
        [DataRow(@"C:\Program Files\EA Games\Battlefield 2042\BF2042.exe", true)]
        [DataRow(@"C:/Program Files/EA Games/Game/game.exe", true)]
        [DataRow(@"C:\Program Files (x86)\Steam\steamwebhelper.exe", false)]
        [DataRow(@"C:\Program Files\WindowsApps\SpotifyAB.SpotifyMusic\Spotify.exe", false)]
        [DataRow(@"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", false)]
        [DataRow(null, false)]
        [DataRow("", false)]
        public void IsGameLibraryPath(string path, bool expected)
        {
            Assert.AreEqual(expected, CaptureTargetResolver.IsGameLibraryPath(path));
        }

        [TestMethod]
        [DataRow("Hardware: Independent Flip", true)]
        [DataRow("Hardware Composed: Independent Flip", true)]
        [DataRow("Hardware: Legacy Flip", true)]
        [DataRow("Hardware: Legacy Copy to front buffer", true)]
        [DataRow("Composed: Flip", false)]
        [DataRow("Composed: Copy with GPU GDI", false)]
        [DataRow("Unknown", false)]
        [DataRow(null, false)]
        public void IsHardwarePresentMode(string presentMode, bool expected)
        {
            Assert.AreEqual(expected, CaptureTargetResolver.IsHardwarePresentMode(presentMode));
        }

        [TestMethod]
        public void Describe_ListsWhatTheRulesSaw()
        {
            var game = Candidate("game", 100, gpuBusyShare: 0.625, presentsPerSecond: 143.7,
                presentMode: IndependentFlip, generatedFrames: true);
            game.SecondsSinceForeground = 3.2;
            game.IsKnownGame = true;

            string text = CaptureTargetResolver.Describe(game);

            Assert.AreEqual("'game' (PID 100): 143.7 presents/s, GPU 62.5 %, idle 0 s, " +
                "Hardware: Independent Flip, generated frames, foreground 3 s ago, known game", text);
            StringAssert.Contains(CaptureTargetResolver.Describe(new CaptureTargetCandidate("tool", 200)), "no statistics");
        }

        private static CaptureTargetResolution Resolve(int previousTarget, params CaptureTargetCandidate[] candidates)
            => CaptureTargetResolver.Resolve(candidates, previousTarget, captureRequest: false);

        private static CaptureTargetCandidate Candidate(string name, int processId, double? gpuBusyShare = 0.01,
            double presentsPerSecond = 60, int idleSeconds = 0, string presentMode = Composed, bool generatedFrames = false)
            => new CaptureTargetCandidate(name, processId)
            {
                Activity = new CaptureProcessActivity(name, processId, idleSeconds, presentsPerSecond,
                    gpuBusyShare, presentMode, generatedFrames)
            };
    }
}
