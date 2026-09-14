#if CFX_INGAME_OVERLAY
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Overlay;
using CapFrameX.OSD.Integration;
using CapFrameX.OverlayReporting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace CapFrameX.Test.Integration
{
    [TestClass]
    public class HookProfileReportServiceTest
    {
        private string _folder;
        private Mock<IAppConfiguration> _configuration;
        private Subject<(string key, object value)> _changes;
        private int _contextReads;

        [TestInitialize]
        public void Initialize()
        {
            _folder = Path.Combine(Path.GetTempPath(), "cfx-report-test-" + Guid.NewGuid().ToString("N"));
            _changes = new Subject<(string key, object value)>();
            _configuration = new Mock<IAppConfiguration>();
            _configuration.SetupGet(c => c.OnValueChanged).Returns(_changes);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _changes.Dispose();
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        private HookProfileReportService Create(Handler handler, bool consent = true)
        {
            _configuration.SetupGet(c => c.ShareOverlayCompatibilityProfiles).Returns(consent);
            return new HookProfileReportService(_configuration.Object, _folder,
                "https://updates.capframex.com/api/v2/releases", "1.9.1.1", "Beta", handler,
                (pid, game, hook, token) =>
                {
                    Interlocked.Increment(ref _contextReads);
                    return new HookProfileReportContext
                    {
                        Game = new ReportBinary { Name = "game.exe", FileVersion = "1.2.3.4",
                            Architecture = "x64", Sha256 = new string('a', 64), ReadStatus = "complete" },
                        Hook = new ReportBinary { Name = "cfx_osd_hook.dll", Architecture = "x64",
                            Sha256 = new string('b', 64), ReadStatus = "complete" },
                        Gpus = new List<ReportGpu> { new ReportGpu { Name = "Test GPU", DriverVersion = "1.2.3", VendorId = "10DE" } },
                        Status = "complete"
                    };
                }, false);
        }

        private static void Begin(HookProfileReportService service, int pid = 42)
            => service.Begin(pid, "game", @"C:\Users\PrivateUser\Games\game.exe",
                @"C:\PrivateBuild\cfx_osd_hook.dll", "hook-hash", "D3D12", "Late",
                HookProfileReportService.Profile(HookCompatibilityStage.Create(HookCompatibilityStageId.Generic), "sl+sldlssg"));

        [TestMethod]
        public async Task NoConsent_DoesNotReadContextCreateReportsOrSend()
        {
            var handler = new Handler();
            using var service = Create(handler, false);
            Begin(service);
            service.Record(42, new ReportEvent { Kind = "test" });
            await service.FlushAsync();
            Assert.AreEqual(0, _contextReads);
            Assert.AreEqual(0, handler.Bodies.Count);
            Assert.IsFalse(Directory.Exists(_folder));
        }

        [TestMethod]
        public async Task Consent_SendsProfilesFailuresAndFgTransitionsWithoutPrivateFields()
        {
            var handler = new Handler();
            using var service = Create(handler);
            Begin(service);
            service.Record(42, new ReportEvent { Kind = "action", Verdict = "RendererStalled" });
            var learned = new HookLearnedProfileEntry
            {
                ExecutableName = "game", ExecutablePath = @"C:\Users\PrivateUser\game.exe",
                StageId = HookCompatibilityStageId.Generic, Verified = true, EvidenceSignature = "sl+sldlssg",
                LastVerdict = "Success", LastVerdictDetail = "PrivateUser", PendingReason = "PrivateBuild"
            };
            service.ProfileOutcome(42, learned);
            foreach (int technology in new[] { 1, 3, 1 })
                service.Observe(42, true, new NativeHookStatusSnapshot
                {
                    Version = 2, FgTechnology = technology, FgActivity = 2, FgAuthoritative = true,
                    Flags = NativeHookStatusFlags.RendererReady | NativeHookStatusFlags.Rendered,
                    CoverageSubmitted = 100, QueueState = NativeHookQueueState.Explicit,
                    MetricsEntryCount = 48, LastHeartbeatTickMs = (long)HookStatusProbe.CurrentTickCount
                }, EHookOverlayStatus.Active, HookStatusProbe.CurrentTickCount, true, false);
            service.End(42, "target-changed");
            await service.FlushAsync();
            Assert.AreEqual(1, handler.Bodies.Count);
            string body = handler.Bodies.Single();
            Assert.IsFalse(body.Contains("PrivateUser"));
            Assert.IsFalse(body.Contains("PrivateBuild"));
            Assert.IsFalse(body.Contains("executablePath"));
            var report = JsonSerializer.Deserialize<OverlayProfileReport>(body, HookProfileReportService.JsonOptions);
            Assert.IsTrue(report.Events.Any(e => e.Verdict == "RendererStalled"));
            Assert.IsTrue(report.Events.Any(e => e.Profile?.Verified == true));
            CollectionAssert.AreEqual(new[] { 1, 3, 1 }, report.Events.Where(e => e.Native != null).Select(e => e.Native.FgTechnology).ToArray());
            Assert.AreEqual("1.2.3.4", report.Game.FileVersion);
            Assert.AreEqual("10DE", report.Gpus.Single().VendorId);
            Assert.IsTrue(report.Events.All(e => e.ElapsedMs <= report.DurationMs));
            Assert.AreEqual("https://updates.capframex.com/api/v1/overlay-reports", handler.Uris.Single());
        }

        [TestMethod]
        public async Task Offline_RetriesTheSameReportAfterRestart()
        {
            var offline = new Handler { Status = HttpStatusCode.ServiceUnavailable };
            using (var service = Create(offline))
            {
                Begin(service);
                service.End(42, "target-changed");
                await service.FlushAsync();
            }
            Assert.AreEqual(1, Directory.GetFiles(Path.Combine(_folder, "OverlayProfileReports"), "*.json").Length);
            var online = new Handler();
            using (var service = Create(online)) await service.FlushAsync();
            Assert.AreEqual(offline.Bodies.Single(), online.Bodies.Single());
            Assert.AreEqual(0, Directory.GetFiles(Path.Combine(_folder, "OverlayProfileReports"), "*.json").Length);
        }

        [TestMethod]
        public async Task Revocation_CancelsInFlightUploadAndPurgesUnsentReports()
        {
            var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool canceled = false;
            var handler = new Handler
            {
                OnSend = async token =>
                {
                    started.SetResult();
                    try { await Task.Delay(Timeout.Infinite, token); }
                    catch (OperationCanceledException) { canceled = true; throw; }
                }
            };
            using var service = Create(handler);
            Begin(service);
            Task uploading = service.FlushAsync();
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            _changes.OnNext((nameof(IAppConfiguration.ShareOverlayCompatibilityProfiles), false));
            await uploading;
            await service.FlushAsync();
            Assert.IsTrue(canceled);
            Assert.AreEqual(0, Directory.GetFiles(Path.Combine(_folder, "OverlayProfileReports")).Length);
            Begin(service, 43);
            await service.FlushAsync();
            Assert.AreEqual(1, handler.Bodies.Count);
        }

        [TestMethod]
        public async Task ImmediateReconsent_DropsOldReportsAndRotatesParticipant()
        {
            var handler = new Handler { Status = HttpStatusCode.ServiceUnavailable };
            using var service = Create(handler);
            Begin(service);
            service.End(42, "target-changed");
            await service.FlushAsync();
            var first = JsonSerializer.Deserialize<OverlayProfileReport>(handler.Bodies.Single(), HookProfileReportService.JsonOptions);
            _changes.OnNext((nameof(IAppConfiguration.ShareOverlayCompatibilityProfiles), false));
            _changes.OnNext((nameof(IAppConfiguration.ShareOverlayCompatibilityProfiles), true));
            handler.Status = HttpStatusCode.Accepted;
            Begin(service, 43);
            service.End(43, "target-changed");
            await service.FlushAsync();
            Assert.AreEqual(2, handler.Bodies.Count);
            var second = JsonSerializer.Deserialize<OverlayProfileReport>(handler.Bodies.Last(), HookProfileReportService.JsonOptions);
            Assert.AreNotEqual(first.ParticipantId, second.ParticipantId);
            Assert.AreNotEqual(first.SessionId, second.SessionId);
        }

        [TestMethod]
        public async Task EventsAreBoundedAndTruncationIsReported()
        {
            var handler = new Handler();
            using var service = Create(handler);
            Begin(service);
            for (int i = 0; i < 1000; i++) service.Record(42, new ReportEvent { Kind = "action", State = i.ToString() });
            service.End(42, "target-changed");
            await service.FlushAsync();
            var report = JsonSerializer.Deserialize<OverlayProfileReport>(handler.Bodies.Single(), HookProfileReportService.JsonOptions);
            Assert.AreEqual(256, report.Events.Count);
            Assert.AreEqual("plan", report.Events.First().Kind);
            Assert.AreEqual("999", report.Events.Last().State);
            Assert.AreEqual(745, report.DroppedEvents);
        }

        [TestMethod]
        public async Task BackoffDoesNotBlockLocalObservationAndAcknowledgementIsRequired()
        {
            var handler = new Handler { Status = HttpStatusCode.TooManyRequests };
            using var service = Create(handler);
            Begin(service);
            await service.FlushAsync();
            service.Record(42, new ReportEvent { Kind = "action", Verdict = "QueueRebinding" });
            await service.FlushAsync();
            Assert.AreEqual(1, handler.Bodies.Count);
            Assert.AreEqual(2, Directory.GetFiles(Path.Combine(_folder, "OverlayProfileReports"), "*.json").Length);
        }

        [TestMethod]
        public void EndpointAndModuleCollectionAreRestricted()
        {
            Assert.IsNull(HookProfileReportService.ResolveEndpoint("http://updates.capframex.com/api/v2/releases"));
            Assert.IsNull(HookProfileReportService.ResolveEndpoint("https://user:pass@updates.capframex.com/api/v2/releases"));
            Assert.IsTrue(HookProfileReportContext.IsRelevantModule("sl.dlss_g.dll"));
            Assert.IsTrue(HookProfileReportContext.IsRelevantModule("amd_fidelityfx_dx12.dll"));
            Assert.IsFalse(HookProfileReportContext.IsRelevantModule("unrelated-private-plugin.dll"));
        }

        [TestMethod]
        public void Shutdown_PersistsFinalEvidenceWithoutNetworkOrHardwareQueries()
        {
            var handler = new Handler();
            var service = Create(handler);
            Begin(service);
            service.Record(42, new ReportEvent { Kind = "action", Verdict = "NoQueue" });
            service.Dispose();
            Assert.AreEqual(0, _contextReads);
            Assert.AreEqual(0, handler.Bodies.Count);
            string stored = File.ReadAllText(Directory.GetFiles(Path.Combine(_folder, "OverlayProfileReports"), "*.json").Single());
            var report = JsonSerializer.Deserialize<OverlayProfileReport>(stored, HookProfileReportService.JsonOptions);
            Assert.AreEqual("host-stopped", report.EndReason);
            Assert.AreEqual("host-stopped-before-context", report.ContextStatus);
            Assert.IsTrue(report.Events.Any(e => e.Verdict == "NoQueue"));
        }

        private sealed class Handler : HttpMessageHandler
        {
            internal readonly List<string> Bodies = new List<string>();
            internal readonly List<string> Uris = new List<string>();
            internal HttpStatusCode Status = HttpStatusCode.Accepted;
            internal Func<CancellationToken, Task> OnSend;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(token));
                Uris.Add(request.RequestUri.AbsoluteUri);
                if (OnSend != null) await OnSend(token);
                return new HttpResponseMessage(Status);
            }
        }
    }
}
#endif
