using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CapFrameX.Contracts.Configuration;
using CapFrameX.OverlayReporting;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// Opt-in diagnostics, independent of local learning. No file/network/context collection
    /// before consent. Bounded checkpoints survive network outages; revocation cancels uploads
    /// and removes unsent data. Native callbacks only append small managed snapshots.
    /// </summary>
    public sealed partial class HookProfileReportService : IDisposable
    {
        internal const int MaxEvents = 256, MaxPending = 32, MaxOutboxFiles = 128;
        internal const int MaxReportBytes = 256 * 1024;
        internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 16
        };
        private readonly object _gate = new object();
        private readonly SemaphoreSlim _ioGate = new SemaphoreSlim(1);
        private readonly IAppConfiguration _configuration;
        private readonly IDisposable _consentSubscription;
        private readonly string _folder, _appVersion, _appChannel;
        private readonly Uri _endpoint;
        private readonly HttpClient _http;
        private readonly Timer _timer;
        private readonly Dictionary<int, Session> _sessions = new Dictionary<int, Session>();
        private readonly Queue<Pending> _pending = new Queue<Pending>();
        private readonly Func<int, string, string, CancellationToken, HookProfileReportContext> _capture;
        private CancellationTokenSource _consentCancellation = new CancellationTokenSource();
        private bool _consented, _disposed;
        private int _epoch, _pumpActive;
        private bool _purgeRequired;
        private Guid _participant;
        private DateTime _retryAfterUtc;

        private sealed class Session
        {
            internal int Pid, Sequence, Dropped;
            internal Guid Id = Guid.NewGuid();
            internal long Started = Environment.TickCount64;
            internal string GameName, GamePath, HookPath, HookBuild, Api, AttachMode;
            internal string LastStatusKey;
            internal long LastSampleMs;
            internal ReportProfile LastProfile;
            internal ReportEvent LastSample;
            internal HookProfileReportContext Context;
            internal DateTime ContextReadUtc;
            internal List<ReportEvent> Events = new List<ReportEvent>();
        }

        private sealed class Pending
        {
            internal Session Session;
            internal OverlayProfileReport Report;
            internal int Epoch;
        }

        public HookProfileReportService(IAppConfiguration configuration, string configurationFolder,
            string updateCatalogUri, string appVersion, string appChannel)
            : this(configuration, configurationFolder, updateCatalogUri, appVersion, appChannel,
                  null, new HookProfileReportContext().Capture, true) { }

        internal HookProfileReportService(IAppConfiguration configuration, string configurationFolder,
            string updateCatalogUri, string appVersion, string appChannel, HttpMessageHandler handler,
            Func<int, string, string, CancellationToken, HookProfileReportContext> capture, bool startTimer)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _folder = Path.Combine(configurationFolder, "OverlayProfileReports");
            _appVersion = appVersion;
            _appChannel = appChannel;
            _capture = capture;
            _endpoint = ResolveEndpoint(updateCatalogUri);
            _http = new HttpClient(handler ?? new HttpClientHandler
            {
                AllowAutoRedirect = false, UseCookies = false
            }) { Timeout = TimeSpan.FromSeconds(15) };
            _consented = configuration.ShareOverlayCompatibilityProfiles;
            _consentSubscription = configuration.OnValueChanged
                .Where(c => c.key == nameof(IAppConfiguration.ShareOverlayCompatibilityProfiles))
                .Subscribe(c => ChangeConsent((bool)c.value));
            if (startTimer)
                _timer = new Timer(_ => _ = PumpSafelyAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            // Also clear leftovers from a previously opted-out/crashed application on startup.
            if (!_consented) _ = PurgeSafelyAsync();
        }

        internal static Uri ResolveEndpoint(string catalogUri)
        {
            if (!Uri.TryCreate(catalogUri, UriKind.Absolute, out var catalog) ||
                catalog.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(catalog.UserInfo)) return null;
            return new Uri(catalog.GetLeftPart(UriPartial.Authority) + "/api/v1/overlay-reports");
        }

        private void ChangeConsent(bool consented)
        {
            lock (_gate)
            {
                if (_disposed || consented == _consented) return;
                _consented = consented;
                _epoch++;
                _consentCancellation.Cancel();
                _consentCancellation = new CancellationTokenSource();
                _sessions.Clear();
                _pending.Clear();
                _participant = Guid.Empty;
                _purgeRequired = true;
                _retryAfterUtc = default;
            }
            // Always purge the old consent epoch, including an immediate off/on toggle.
            _ = PurgeSafelyAsync();
        }

        internal void Begin(int pid, string gameName, string gamePath, string hookPath,
            string hookBuild, string api, string attachMode, ReportProfile profile)
        {
            lock (_gate)
            {
                if (!_consented || _disposed || pid <= 0) return;
                if (!_sessions.TryGetValue(pid, out var session))
                {
                    if (_sessions.Count >= 4) EndLocked(_sessions.Keys.First(), "target-limit");
                    session = new Session { Pid = pid, GameName = gameName, GamePath = gamePath,
                        HookPath = hookPath, HookBuild = hookBuild, Api = api, AttachMode = attachMode };
                    _sessions.Add(pid, session);
                }
                session.LastProfile = profile;
                AddLocked(session, new ReportEvent { Kind = "plan", Profile = profile });
            }
        }

        internal void Record(int pid, ReportEvent observation, string statusKey = null)
        {
            lock (_gate)
            {
                if (!_consented || _disposed || !_sessions.TryGetValue(pid, out var session)) return;
                long now = Environment.TickCount64;
                observation.ElapsedMs = Math.Max(0, now - session.Started);
                if (statusKey != null)
                {
                    session.LastSample = observation;
                    // Keep transitions immediately, plus cumulative counter samples every 10 s.
                    if (statusKey == session.LastStatusKey && now - session.LastSampleMs < 10000) return;
                    session.LastStatusKey = statusKey;
                    session.LastSampleMs = now;
                }
                if (observation.Profile != null)
                {
                    if (observation.Profile.EvidenceSignature != session.LastProfile?.EvidenceSignature)
                        session.ContextReadUtc = default;
                    // Action proposals must not overwrite the last learned outcome.
                    if (observation.Kind == "profile-outcome") session.LastProfile = observation.Profile;
                }
                AddLocked(session, observation);
            }
        }

        private static void AddLocked(Session session, ReportEvent observation)
        {
            observation.ElapsedMs = Math.Max(0, Environment.TickCount64 - session.Started);
            if (session.Events.Count == MaxEvents)
            {
                // Preserve the initial plan and early decisions as well as the latest state.
                session.Events.RemoveAt(32);
                session.Dropped++;
            }
            session.Events.Add(observation);
        }

        internal void End(int pid, string reason)
        {
            lock (_gate) EndLocked(pid, reason);
        }

        private void EndLocked(int pid, string reason)
        {
            if (!_sessions.Remove(pid, out var session)) return;
            if (_consented) CheckpointLocked(session, reason);
        }

        private void CheckpointLocked(Session session, string reason)
        {
            if (session.Events.Count == 0 && reason == "checkpoint") return;
            if (session.LastSample != null && !session.Events.Contains(session.LastSample))
            {
                if (session.Events.Count == MaxEvents) { session.Events.RemoveAt(32); session.Dropped++; }
                session.Events.Add(session.LastSample);
            }
            var report = new OverlayProfileReport
            {
                ReportId = Guid.NewGuid(), SessionId = session.Id, Sequence = ++session.Sequence,
                CreatedUtc = DateTime.UtcNow, AppVersion = _appVersion, AppChannel = _appChannel,
                OsVersion = Environment.OSVersion.Version.ToString(),
                OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                AttachMode = session.AttachMode ?? "unknown", GraphicsApi = session.Api ?? "unknown",
                HookBuild = session.HookBuild ?? "", Game = new ReportBinary { Name = session.GameName ?? "unknown" },
                EndReason = reason, DurationMs = Math.Max(0, Environment.TickCount64 - session.Started),
                Events = session.Events, DroppedEvents = session.Dropped
            };
            session.Events = new List<ReportEvent>();
            session.Dropped = 0;
            // A checkpoint is self-describing even if another segment was never delivered.
            session.Events.Add(new ReportEvent { Kind = "profile-at-checkpoint", Profile = session.LastProfile,
                ElapsedMs = report.DurationMs });
            if (_pending.Count == MaxPending)
            {
                _pending.Dequeue();
                report.DroppedEvents++;
            }
            _pending.Enqueue(new Pending { Session = session, Report = report, Epoch = _epoch });
        }

        private async Task PumpSafelyAsync()
        {
            if (Interlocked.Exchange(ref _pumpActive, 1) != 0) return;
            try { await FlushAsync().ConfigureAwait(false); }
            catch (Exception ex) { Log.Debug("Overlay reports: background collection unavailable ({type})", ex.GetType().Name); }
            finally { Volatile.Write(ref _pumpActive, 0); }
        }

        internal async Task FlushAsync(bool upload = true)
        {
            await _ioGate.WaitAsync().ConfigureAwait(false);
            try
            {
                PurgeIfRequired();
                CancellationToken token;
                int epoch;
                lock (_gate)
                {
                    if (!_consented || _disposed || _endpoint == null) return;
                    token = _consentCancellation.Token;
                    epoch = _epoch;
                    foreach (var session in _sessions.Values) CheckpointLocked(session, "checkpoint");
                }
                EnsureFolder();
                if (_participant == Guid.Empty)
                {
                    string identityPath = Path.Combine(_folder, "participant.id");
                    if (!File.Exists(identityPath) || !Guid.TryParse(File.ReadAllText(identityPath), out _participant))
                    {
                        _participant = Guid.NewGuid();
                        File.WriteAllText(identityPath, _participant.ToString("D"));
                    }
                }
                while (true)
                {
                    Pending pending;
                    lock (_gate)
                    {
                        if (!_consented || epoch != _epoch || _pending.Count == 0) break;
                        pending = _pending.Peek();
                    }
                    if (pending.Epoch != epoch) { CompletePending(pending); continue; }
                    token.ThrowIfCancellationRequested();
                    Session session = pending.Session;
                    // Re-scan periodically so late-loaded FG providers and overlays are recorded.
                    if (session.Context == null || DateTime.UtcNow - session.ContextReadUtc >= TimeSpan.FromMinutes(2))
                    {
                        try
                        {
                            session.Context = upload ? _capture(session.Pid, session.GamePath, session.HookPath, token)
                                : session.Context ?? new HookProfileReportContext
                                {
                                    Game = new ReportBinary { Name = session.GameName ?? "unknown" },
                                    Status = "host-stopped-before-context"
                                };
                        }
                        catch (Exception ex) when (!(ex is OperationCanceledException))
                        {
                            session.Context = new HookProfileReportContext
                            {
                                Game = new ReportBinary { Name = session.GameName ?? "unknown" }, Status = "unavailable"
                            };
                        }
                        session.ContextReadUtc = DateTime.UtcNow;
                    }
                    var context = session.Context;
                    pending.Report.ParticipantId = _participant;
                    pending.Report.Game = context.Game;
                    if (string.IsNullOrEmpty(pending.Report.Game.Name)) pending.Report.Game.Name = session.GameName ?? "unknown";
                    pending.Report.Hook = context.Hook;
                    pending.Report.Modules = context.Modules;
                    pending.Report.Gpus = context.Gpus;
                    pending.Report.ContextStatus = context.Status;
                    pending.Report.ContextObservedUtc = session.ContextReadUtc;
                    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(pending.Report, JsonOptions);
                    if (bytes.Length > MaxReportBytes)
                    {
                        Log.Debug("Overlay reports: oversized checkpoint discarded");
                        CompletePending(pending);
                        continue;
                    }
                    token.ThrowIfCancellationRequested();
                    string destination = Path.Combine(_folder, pending.Report.ReportId.ToString("N") + ".json");
                    string temporary = destination + ".tmp";
                    await File.WriteAllBytesAsync(temporary, bytes, token).ConfigureAwait(false);
                    File.Move(temporary, destination, true);
                    CompletePending(pending);
                }
                var files = new DirectoryInfo(_folder).GetFiles("*.json")
                    .OrderByDescending(f => f.LastWriteTimeUtc).ToArray();
                foreach (var file in files.Where((f, i) => i >= MaxOutboxFiles ||
                    DateTime.UtcNow - f.LastWriteTimeUtc > TimeSpan.FromDays(7))) file.Delete();
                if (!upload || DateTime.UtcNow < _retryAfterUtc) return;
                foreach (var file in new DirectoryInfo(_folder).GetFiles("*.json").OrderBy(f => f.LastWriteTimeUtc).Take(8))
                {
                    token.ThrowIfCancellationRequested();
                    if (file.Length > MaxReportBytes || (file.Attributes & FileAttributes.ReparsePoint) != 0 ||
                        !Guid.TryParseExact(Path.GetFileNameWithoutExtension(file.Name), "N", out _)) continue;
                    byte[] bytes = await File.ReadAllBytesAsync(file.FullName, token).ConfigureAwait(false);
                    // Read as the allowlisted contract, never forward arbitrary local JSON fields.
                    OverlayProfileReport report;
                    try { report = JsonSerializer.Deserialize<OverlayProfileReport>(bytes, JsonOptions); }
                    catch (JsonException) { file.Delete(); continue; }
                    if (report == null || report.SchemaVersion != 1 || report.ConsentVersion != 1 ||
                        report.ParticipantId != _participant) { file.Delete(); continue; }
                    using var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(report, JsonOptions));
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    HttpResponseMessage response;
                    try { response = await _http.PostAsync(_endpoint, content, token).ConfigureAwait(false); }
                    catch (HttpRequestException) { _retryAfterUtc = DateTime.UtcNow.AddMinutes(5); break; }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    { _retryAfterUtc = DateTime.UtcNow.AddMinutes(5); break; }
                    using (response)
                    {
                        if (response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.OK)
                            file.Delete();
                        else if (response.StatusCode == HttpStatusCode.BadRequest || response.StatusCode == HttpStatusCode.Conflict ||
                                 response.StatusCode == HttpStatusCode.RequestEntityTooLarge)
                            file.Delete();
                        else
                        {
                            var delay = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(5);
                            _retryAfterUtc = DateTime.UtcNow.AddSeconds(Math.Clamp(delay.TotalSeconds, 30, 3600));
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally { _ioGate.Release(); }
        }

        private void EnsureFolder()
        {
            Directory.CreateDirectory(_folder);
            if ((File.GetAttributes(_folder) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Report directory must not be a link");
        }

        private async Task PurgeSafelyAsync()
        {
            await _ioGate.WaitAsync().ConfigureAwait(false);
            try
            {
                PurgeIfRequired();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            finally { _ioGate.Release(); }
        }

        private void CompletePending(Pending pending)
        {
            lock (_gate)
                if (_pending.Count > 0 && ReferenceEquals(_pending.Peek(), pending)) _pending.Dequeue();
        }

        private void PurgeIfRequired()
        {
            // _ioGate serializes this with persistence and uploading. Hold the short consent
            // lock through deletion so an off/on toggle cannot reuse the previous identity.
            lock (_gate)
            {
                if (!_purgeRequired && _consented) return;
                if (Directory.Exists(_folder) && (File.GetAttributes(_folder) & FileAttributes.ReparsePoint) == 0)
                    foreach (string file in Directory.EnumerateFiles(_folder))
                    {
                        string name = Path.GetFileName(file);
                        if (name == "participant.id" || name.EndsWith(".json", StringComparison.Ordinal) ||
                            name.EndsWith(".json.tmp", StringComparison.Ordinal)) File.Delete(file);
                    }
                _purgeRequired = false;
                _participant = Guid.Empty;
            }
        }

        public void Dispose()
        {
            _timer?.Dispose();
            lock (_gate)
            {
                if (_disposed) return;
                _consentCancellation.Cancel();
                _consentCancellation = new CancellationTokenSource();
                foreach (int pid in _sessions.Keys.ToArray()) EndLocked(pid, "host-stopped");
            }
            // Persist final evidence without network or hardware scans during application exit.
            // Periodic checkpoints already cover a forced exit or a slow/full disk.
            try { FlushAsync(upload: false).Wait(TimeSpan.FromSeconds(2)); }
            catch (AggregateException) { }
            lock (_gate)
            {
                _disposed = true;
                _consentCancellation.Cancel();
                _pending.Clear();
            }
            _consentSubscription.Dispose();
            _http.Dispose();
        }
    }
}
