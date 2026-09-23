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
        internal const long StableCheckpointIntervalMs = 10 * 60 * 1000;
        internal static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 16
        };
        internal static readonly JsonSerializerOptions CompactJsonOptions = new JsonSerializerOptions(JsonOptions)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };
        private readonly object _gate = new object();
        private readonly SemaphoreSlim _ioGate = new SemaphoreSlim(1);
        private readonly IAppConfiguration _configuration;
        private readonly IDisposable _consentSubscription;
        private readonly string _folder, _appVersion, _appChannel;
        private readonly Uri _endpoint;
        private readonly HttpClient _http;
        private readonly bool _startTimer;
        private readonly TimeProvider _timeProvider;
        private ITimer _timer;
        private readonly Dictionary<int, Session> _sessions = new Dictionary<int, Session>();
        private readonly Queue<Pending> _pending = new Queue<Pending>();
        private readonly Func<int, string, string, CancellationToken, HookProfileReportContext> _capture;
        private readonly Func<long> _tickCount;
        private readonly Func<DateTime> _utcNow;
        private CancellationTokenSource _consentCancellation = new CancellationTokenSource();
        private bool _consented, _disposed, _disposing;
        private int _epoch, _pumpActive;
        private bool _purgeRequired;
        private Guid _participant;
        private DateTime _retryAfterUtc;

        // The default path must return before locks, timestamps, strings or report allocations.
        // Record still checks consent under the lock to handle a concurrent revocation.
        internal bool IsEnabled => Volatile.Read(ref _consented) &&
            !Volatile.Read(ref _disposed) && !Volatile.Read(ref _disposing);

        private sealed class Session
        {
            internal int Pid, Sequence, Dropped;
            internal Guid Id = Guid.NewGuid();
            internal long Started, LastCheckpointMs;
            internal string GameName, GamePath, HookPath, HookBuild, Api, AttachMode;
            internal bool Changed;
            // DXGI and Vulkan can be observed for the same target. Comparing them against
            // one shared key makes every alternating observation look like a transition.
            internal Dictionary<string, (string Key, ReportEvent Latest)> Samples = new();
            internal ReportProfile LastProfile;
            internal HookProfileReportContext Context;
            internal string ContextKey;
            internal int ContextRevision, CapturedContextRevision;
            internal DateTime ContextReadUtc;
            internal string WindowState;
            internal long WindowReadMs;
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
            Func<int, string, string, CancellationToken, HookProfileReportContext> capture, bool startTimer,
            Func<long> tickCount = null, Func<DateTime> utcNow = null, TimeProvider timeProvider = null)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _folder = Path.Combine(configurationFolder, "OverlayProfileReports");
            _appVersion = appVersion;
            _appChannel = appChannel;
            _capture = capture;
            _startTimer = startTimer;
            _timeProvider = timeProvider ?? TimeProvider.System;
            _tickCount = tickCount ?? (() => Environment.TickCount64);
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _endpoint = ResolveEndpoint(updateCatalogUri);
            _http = new HttpClient(handler ?? new HttpClientHandler
            {
                AllowAutoRedirect = false, UseCookies = false
            }) { Timeout = TimeSpan.FromSeconds(15) };
            _consented = configuration.ShareOverlayCompatibilityProfiles;
            _purgeRequired = !_consented;
            _consentSubscription = configuration.OnValueChanged
                .Where(c => c.key == nameof(IAppConfiguration.ShareOverlayCompatibilityProfiles))
                .Subscribe(c => ChangeConsent((bool)c.value));
            lock (_gate) UpdateTimerLocked();
            // Also clear leftovers from a previously opted-out/crashed application on startup.
            if (_purgeRequired) _ = PurgeSafelyAsync();
        }

        internal static Uri ResolveEndpoint(string catalogUri)
        {
            if (!Uri.TryCreate(catalogUri, UriKind.Absolute, out var catalog) ||
                catalog.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(catalog.UserInfo)) return null;
            return new Uri(catalog.GetLeftPart(UriPartial.Authority) + "/api/v2/overlay-reports");
        }

        private void ChangeConsent(bool consented)
        {
            lock (_gate)
            {
                if (_disposed || consented == _consented) return;
                Volatile.Write(ref _consented, consented);
                _epoch++;
                _consentCancellation.Cancel();
                _consentCancellation = new CancellationTokenSource();
                _sessions.Clear();
                _pending.Clear();
                _participant = Guid.Empty;
                _purgeRequired = true;
                _retryAfterUtc = default;
                UpdateTimerLocked();
            }
            // Always purge the old consent epoch, including an immediate off/on toggle.
            _ = PurgeSafelyAsync();
        }

        private void UpdateTimerLocked()
        {
            _timer?.Dispose();
            _timer = null;
            if (_startTimer && IsEnabled && _endpoint != null)
                _timer = _timeProvider.CreateTimer(static state =>
                    _ = ((HookProfileReportService)state).PumpSafelyAsync(), this,
                    TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        internal void Begin(int pid, string gameName, string gamePath, string hookPath,
            string hookBuild, string api, string attachMode, ReportProfile profile)
        {
            if (!IsEnabled) return;
            lock (_gate)
            {
                if (!_consented || _disposed || _disposing || pid <= 0) return;
                if (!_sessions.TryGetValue(pid, out var session))
                {
                    if (_sessions.Count >= 4) EndLocked(_sessions.Keys.First(), "target-limit");
                    session = new Session { Pid = pid, GameName = gameName, GamePath = gamePath,
                        HookPath = hookPath, HookBuild = hookBuild, Api = api, AttachMode = attachMode,
                        Started = _tickCount() };
                    _sessions.Add(pid, session);
                }
                if (profile?.EvidenceSignature != session.LastProfile?.EvidenceSignature)
                    session.ContextRevision++;
                session.LastProfile = profile;
                AddLocked(session, new ReportEvent { Kind = "plan", Profile = profile,
                    ElapsedMs = Math.Max(0, _tickCount() - session.Started) });
            }
        }

        internal void Record(int pid, ReportEvent observation, string statusKey = null)
        {
            if (!IsEnabled) return;
            lock (_gate)
            {
                if (!_consented || _disposed || _disposing || !_sessions.TryGetValue(pid, out var session)) return;
                long now = _tickCount();
                observation.ElapsedMs = Math.Max(0, now - session.Started);
                if (statusKey != null)
                {
                    session.Samples.TryGetValue(observation.Kind, out var previous);
                    session.Samples[observation.Kind] = (statusKey, observation);
                    // Normal counter/heartbeat progress updates only the latest snapshot.
                    // Retain both sides of a transition or counter reset for later analysis.
                    if (statusKey == previous.Key && !CountersReset(previous.Latest, observation)) return;
                    AppendSampleLocked(session, previous.Latest);
                }
                if (observation.Profile != null)
                {
                    if (observation.Profile.EvidenceSignature != session.LastProfile?.EvidenceSignature)
                        session.ContextRevision++;
                    // Action proposals must not overwrite the last learned outcome.
                    if (observation.Kind == "profile-outcome") session.LastProfile = observation.Profile;
                }
                AddLocked(session, observation);
            }
        }

        private static void AddLocked(Session session, ReportEvent observation)
        {
            session.Changed = true;
            if (session.Events.Count == MaxEvents)
            {
                // Preserve the initial plan and early decisions as well as the latest state.
                session.Events.RemoveAt(32);
                session.Dropped++;
            }
            session.Events.Add(observation);
        }

        private static void AppendSampleLocked(Session session, ReportEvent sample)
        {
            if (sample != null && !session.Events.Contains(sample)) AddLocked(session, sample);
        }

        private static bool CountersReset(ReportEvent previous, ReportEvent current)
        {
            if (previous?.Native != null && current.Native != null)
                return current.Native.CoverageAttempts < previous.Native.CoverageAttempts ||
                    current.Native.CoverageSubmitted < previous.Native.CoverageSubmitted ||
                    current.Native.CoverageMissed < previous.Native.CoverageMissed ||
                    previous.Native.Progress != null && current.Native.Progress != null &&
                    (current.Native.Progress.Presents < previous.Native.Progress.Presents ||
                     current.Native.Progress.Draws < previous.Native.Progress.Draws);
            return previous?.Vulkan != null && current.Vulkan != null &&
                current.Vulkan.Successes < previous.Vulkan.Successes;
        }

        internal void End(int pid, string reason)
        {
            if (!IsEnabled) return;
            lock (_gate) EndLocked(pid, reason);
        }

        private void EndLocked(int pid, string reason)
        {
            if (!_sessions.Remove(pid, out var session)) return;
            if (_consented) CheckpointLocked(session, reason);
        }

        private void CheckpointLocked(Session session, string reason)
        {
            long elapsed = Math.Max(0, _tickCount() - session.Started);
            if (reason == "checkpoint" && !session.Changed &&
                elapsed - session.LastCheckpointMs < StableCheckpointIntervalMs) return;
            foreach (var sample in session.Samples.Values) AppendSampleLocked(session, sample.Latest);
            var report = new OverlayProfileReport
            {
                SchemaVersion = 2,
                ReportId = Guid.NewGuid(), SessionId = session.Id, Sequence = ++session.Sequence,
                CreatedUtc = _utcNow(), AppVersion = _appVersion, AppChannel = _appChannel,
                OsVersion = Environment.OSVersion.Version.ToString(),
                OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
                AttachMode = session.AttachMode ?? "unknown", GraphicsApi = session.Api ?? "unknown",
                HookBuild = session.HookBuild ?? "", Game = new ReportBinary { Name = session.GameName ?? "unknown" },
                EndReason = reason, DurationMs = elapsed,
                Events = session.Events.OrderBy(e => e.ElapsedMs).ToList(), DroppedEvents = session.Dropped
            };
            session.Events = new List<ReportEvent>();
            session.Dropped = 0;
            // A checkpoint is self-describing even if another segment was never delivered.
            session.Events.Add(new ReportEvent { Kind = "profile-at-checkpoint", Profile = session.LastProfile,
                ElapsedMs = report.DurationMs });
            // Carry counter baselines without treating them as new evidence. A seeded
            // profile/sample must never trigger another report by itself.
            foreach (var sample in session.Samples.Values) session.Events.Add(sample.Latest);
            session.Changed = false;
            session.LastCheckpointMs = elapsed;
            if (_pending.Count == MaxPending)
            {
                _pending.Dequeue();
                report.DroppedEvents++;
            }
            _pending.Enqueue(new Pending { Session = session, Report = report, Epoch = _epoch });
        }

        private async Task PumpSafelyAsync()
        {
            if (!IsEnabled || Interlocked.Exchange(ref _pumpActive, 1) != 0) return;
            try { await FlushAsync().ConfigureAwait(false); }
            catch (Exception ex) { Log.Debug("Overlay reports: background collection unavailable ({type})", ex.GetType().Name); }
            finally { Volatile.Write(ref _pumpActive, 0); }
        }

        internal async Task FlushAsync(bool upload = true)
        {
            // A one-time purge can still be pending after revocation. Once it is complete,
            // disabled callers do not acquire the I/O gate or touch the report directory.
            if ((!Volatile.Read(ref _consented) || Volatile.Read(ref _disposed)) &&
                !Volatile.Read(ref _purgeRequired)) return;
            await _ioGate.WaitAsync().ConfigureAwait(false);
            try
            {
                PurgeIfRequired();
                CancellationToken token;
                int epoch;
                Session[] sessions;
                lock (_gate)
                {
                    if (!_consented || _disposed || _endpoint == null) return;
                    token = _consentCancellation.Token;
                    epoch = _epoch;
                    sessions = _sessions.Values.ToArray();
                }
                // Metadata changes also trigger a report, even when native state is stable.
                // Keep the two-minute scan independent of the ten-minute stable checkpoint.
                foreach (var session in sessions)
                {
                    RefreshContext(session, upload, token);
                    lock (_gate)
                    {
                        if (!_consented || epoch != _epoch) return;
                        if (_sessions.TryGetValue(session.Pid, out var active) && ReferenceEquals(active, session))
                            CheckpointLocked(session, "checkpoint");
                    }
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
                    RefreshContext(session, upload, token);
                    var context = session.Context;
                    pending.Report.ParticipantId = _participant;
                    pending.Report.Game = context.Game;
                    if (string.IsNullOrEmpty(pending.Report.Game.Name)) pending.Report.Game.Name = session.GameName ?? "unknown";
                    pending.Report.Hook = context.Hook;
                    pending.Report.Modules = context.Modules;
                    pending.Report.Gpus = context.Gpus;
                    pending.Report.ContextStatus = context.Status;
                    pending.Report.ContextObservedUtc = session.ContextReadUtc;
                    byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(pending.Report, CompactJsonOptions);
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
                    _utcNow() - f.LastWriteTimeUtc > TimeSpan.FromDays(7))) file.Delete();
                if (!upload || _utcNow() < _retryAfterUtc) return;
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
                    if (report == null || (report.SchemaVersion != 1 && report.SchemaVersion != 2) || report.ConsentVersion != 1 ||
                        report.ParticipantId != _participant) { file.Delete(); continue; }
                    using var content = new ByteArrayContent(JsonSerializer.SerializeToUtf8Bytes(report,
                        report.SchemaVersion == 1 ? JsonOptions : CompactJsonOptions));
                    content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    HttpResponseMessage response;
                    // A server not upgraded yet returns 404/405 for v2: retain and retry the
                    // report, instead of losing it to v1's strict unknown-field rejection.
                    Uri endpoint = report.SchemaVersion == 1
                        ? new Uri(_endpoint.GetLeftPart(UriPartial.Authority) + "/api/v1/overlay-reports") : _endpoint;
                    try { response = await _http.PostAsync(endpoint, content, token).ConfigureAwait(false); }
                    catch (HttpRequestException) { _retryAfterUtc = _utcNow().AddMinutes(5); break; }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    { _retryAfterUtc = _utcNow().AddMinutes(5); break; }
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
                            _retryAfterUtc = _utcNow().AddSeconds(Math.Clamp(delay.TotalSeconds, 30, 3600));
                            break;
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            finally { _ioGate.Release(); }
        }

        private void RefreshContext(Session session, bool capture, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            int revision;
            lock (_gate)
            {
                if (session.Context != null && (!capture ||
                    (session.CapturedContextRevision == session.ContextRevision &&
                    _utcNow() - session.ContextReadUtc < TimeSpan.FromMinutes(2)))) return;
                revision = session.ContextRevision;
            }
            HookProfileReportContext context;
            try
            {
                context = capture ? _capture(session.Pid, session.GamePath, session.HookPath, token)
                    : new HookProfileReportContext
                    {
                        Game = new ReportBinary { Name = session.GameName ?? "unknown" },
                        Status = "host-stopped-before-context"
                    };
            }
            catch (Exception ex) when (!(ex is OperationCanceledException))
            {
                context = new HookProfileReportContext
                {
                    Game = new ReportBinary { Name = session.GameName ?? "unknown" }, Status = "unavailable"
                };
            }
            // Enumeration order and observation time are not metadata changes. Compare
            // only the allowlisted wire fields, never paths or other capture internals.
            string key = JsonSerializer.Serialize(new
            {
                context.Game, context.Hook, context.Status,
                Modules = context.Modules.Select(m => JsonSerializer.Serialize(m, JsonOptions))
                    .OrderBy(m => m, StringComparer.Ordinal),
                Gpus = context.Gpus.Select(g => JsonSerializer.Serialize(g, JsonOptions))
                    .OrderBy(g => g, StringComparer.Ordinal)
            }, JsonOptions);
            token.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_sessions.TryGetValue(session.Pid, out var active) && ReferenceEquals(active, session) &&
                    session.Context != null &&
                    (session.Context.Status == "complete" || session.Context.Status == "gpu-unavailable") &&
                    (context.Status == "complete" || context.Status == "gpu-unavailable"))
                {
                    var before = session.Context.Modules.GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => string.Join("|", g.Select(m => JsonSerializer.Serialize(m, JsonOptions))
                            .OrderBy(m => m, StringComparer.Ordinal)), StringComparer.OrdinalIgnoreCase);
                    var after = context.Modules.GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => string.Join("|", g.Select(m => JsonSerializer.Serialize(m, JsonOptions))
                            .OrderBy(m => m, StringComparer.Ordinal)), StringComparer.OrdinalIgnoreCase);
                    var change = new ReportModuleChange
                    {
                        Added = after.Keys.Except(before.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList(),
                        Removed = before.Keys.Except(after.Keys, StringComparer.OrdinalIgnoreCase).OrderBy(n => n).ToList(),
                        Updated = after.Keys.Where(n => before.TryGetValue(n, out string old) && old != after[n])
                            .OrderBy(n => n).ToList()
                    };
                    if (change.Added.Count + change.Removed.Count + change.Updated.Count > 0)
                        AddLocked(session, new ReportEvent { Kind = "module-change", ModuleChange = change,
                            ElapsedMs = Math.Max(0, _tickCount() - session.Started) });
                }
                if (key != session.ContextKey) session.Changed = true;
                session.Context = context;
                session.ContextKey = key;
                session.ContextReadUtc = _utcNow();
                session.CapturedContextRevision = revision;
            }
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
                if (!_purgeRequired) return;
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
            lock (_gate)
            {
                if (_disposed || _disposing) return;
                Volatile.Write(ref _disposing, true);
                UpdateTimerLocked();
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
                Volatile.Write(ref _disposed, true);
                _consentCancellation.Cancel();
                _pending.Clear();
            }
            _consentSubscription.Dispose();
            _http.Dispose();
        }
    }
}
