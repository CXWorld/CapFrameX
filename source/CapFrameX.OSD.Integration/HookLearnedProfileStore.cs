using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CapFrameX.Contracts.Overlay;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// One learned compatibility entry: what the probe concluded for an executable under a
    /// given evidence signature, and what the next launch should start with.
    /// </summary>
    internal sealed class HookLearnedProfileEntry
    {
        [JsonProperty("executableName")]
        public string ExecutableName { get; set; }

        [JsonProperty("executablePath")]
        public string ExecutablePath { get; set; }

        /// <summary>Evidence signature at verdict time (all runtimes resident).</summary>
        [JsonProperty("evidenceSignature")]
        public string EvidenceSignature { get; set; }

        /// <summary>Evidence signature at plan time on the early path (thin), else null.</summary>
        [JsonProperty("earlySignature")]
        public string EarlySignature { get; set; }

        [JsonProperty("stageId")]
        [JsonConverter(typeof(StringEnumConverter))]
        public HookCompatibilityStageId StageId { get; set; }

        [JsonProperty("flags")]
        public uint Flags { get; set; }

        [JsonProperty("earlyInjectionModule")]
        public string EarlyInjectionModule { get; set; }

        [JsonProperty("injectionDelayMs")]
        public int InjectionDelayMs { get; set; }

        [JsonProperty("verified")]
        public bool Verified { get; set; }

        [JsonProperty("exhausted")]
        public bool Exhausted { get; set; }

        [JsonProperty("pendingStageId")]
        [JsonConverter(typeof(StringEnumConverter))]
        public HookCompatibilityStageId? PendingStageId { get; set; }

        [JsonProperty("pendingFlags")]
        public uint PendingFlags { get; set; }

        [JsonProperty("pendingEarlyInjectionModule")]
        public string PendingEarlyInjectionModule { get; set; }

        [JsonProperty("pendingInjectionDelayMs")]
        public int PendingInjectionDelayMs { get; set; }

        [JsonProperty("pendingReason")]
        public string PendingReason { get; set; }

        [JsonProperty("lastVerdict")]
        public string LastVerdict { get; set; }

        [JsonProperty("lastVerdictDetail")]
        public string LastVerdictDetail { get; set; }

        [JsonProperty("attempts")]
        public int Attempts { get; set; }

        [JsonProperty("ladder")]
        public string[] Ladder { get; set; }

        [JsonProperty("hookBuildHash")]
        public string HookBuildHash { get; set; }

        [JsonProperty("createdUtc")]
        public DateTime CreatedUtc { get; set; }

        [JsonProperty("updatedUtc")]
        public DateTime UpdatedUtc { get; set; }

        internal bool MatchesHookBuild(string hookBuildHash)
            => !string.IsNullOrEmpty(hookBuildHash) &&
               string.Equals(HookBuildHash, hookBuildHash, StringComparison.OrdinalIgnoreCase);

        internal HookCompatibilityStage ToStage()
            => new HookCompatibilityStage(StageId,
                unchecked((NativeHookCompatibilityFlags)Flags), EarlyInjectionModule,
                TimeSpan.FromMilliseconds(Math.Max(0, InjectionDelayMs)), "learned");

        internal HookCompatibilityStage PendingStage()
            => PendingStageId == null
                ? null
                : new HookCompatibilityStage(PendingStageId.Value,
                    unchecked((NativeHookCompatibilityFlags)PendingFlags),
                    PendingEarlyInjectionModule,
                    TimeSpan.FromMilliseconds(Math.Max(0, PendingInjectionDelayMs)), "learned");

        internal void SetStage(HookCompatibilityStage stage)
        {
            StageId = stage.Id;
            Flags = (uint)stage.Flags;
            EarlyInjectionModule = stage.EarlyInjectionModule;
            InjectionDelayMs = (int)Math.Min(int.MaxValue, stage.InjectionDelay.TotalMilliseconds);
        }

        internal void SetPending(HookCompatibilityStage stage, string reason)
        {
            if (stage == null)
            {
                PendingStageId = null;
                PendingFlags = 0;
                PendingEarlyInjectionModule = null;
                PendingInjectionDelayMs = 0;
                PendingReason = null;
                return;
            }
            PendingStageId = stage.Id;
            PendingFlags = (uint)stage.Flags;
            PendingEarlyInjectionModule = stage.EarlyInjectionModule;
            PendingInjectionDelayMs =
                (int)Math.Min(int.MaxValue, stage.InjectionDelay.TotalMilliseconds);
            PendingReason = reason;
        }

        internal HookLearnedProfileSummary ToSummary()
            => new HookLearnedProfileSummary(ExecutableName, EvidenceSignature,
                ToStage().DisplayName, Verified, Exhausted, PendingStage()?.DisplayName,
                UpdatedUtc);
    }

    /// <summary>What the manager needs from the store; a file-less instance backs the tests.</summary>
    internal interface IHookLearnedProfileStore
    {
        bool TryGet(string executableName, string evidenceSignature,
            out HookLearnedProfileEntry entry);

        /// <summary>
        /// The early path only sees the gate module at plan time, so its signature is thin.
        /// Match it against what the same title looked like at plan time on an earlier run.
        /// </summary>
        bool TryGetByEarlySignature(string executableName, string earlySignature,
            out HookLearnedProfileEntry entry);

        IReadOnlyList<HookLearnedProfileEntry> GetForExecutable(string executableName);

        /// <summary>Creates or updates the entry under (executable, signature) and saves.</summary>
        HookLearnedProfileEntry Upsert(string executableName, string executablePath,
            string evidenceSignature, string earlySignature, string hookBuildHash,
            Action<HookLearnedProfileEntry> mutate);

        IReadOnlyList<HookLearnedProfileEntry> Snapshot();

        IObservable<int> Changes { get; }

        void Reset();
    }

    /// <summary>
    /// JSON-backed learned profiles, one file in the configuration folder. Whole-file writes
    /// through a temporary file; a corrupt file is set aside and the store starts empty rather
    /// than blocking the overlay.
    /// </summary>
    // Public so the composition root can construct and hand it to HookOverlayManager; the
    // entry-typed store interface is implemented explicitly because those types stay internal.
    public sealed class HookLearnedProfileStore : IHookLearnedProfileStore,
        IHookLearnedProfileService, IDisposable
    {
        internal const string FileName = "HookCompatibilityProfiles.learned.json";
        private const int FormatVersion = 1;

        private readonly object _gate = new object();
        private readonly List<HookLearnedProfileEntry> _entries =
            new List<HookLearnedProfileEntry>();
        private readonly Subject<int> _changes = new Subject<int>();
        private readonly string _path;

        private sealed class StoreFile
        {
            [JsonProperty("version")]
            public int Version { get; set; } = FormatVersion;

            [JsonProperty("entries")]
            public List<HookLearnedProfileEntry> Entries { get; set; } =
                new List<HookLearnedProfileEntry>();
        }

        /// <param name="path">Null keeps the store in memory only.</param>
        internal HookLearnedProfileStore(string path)
        {
            _path = string.IsNullOrWhiteSpace(path) ? null : path;
            Load();
        }

        public static HookLearnedProfileStore Create(string configurationFolder)
        {
            string path = string.IsNullOrWhiteSpace(configurationFolder)
                ? null
                : Path.Combine(configurationFolder, FileName);
            return new HookLearnedProfileStore(path);
        }

        public string StorePath => _path;

        public IObservable<int> Changes => _changes.AsObservable();

        bool IHookLearnedProfileStore.TryGet(string executableName, string evidenceSignature,
            out HookLearnedProfileEntry entry)
        {
            entry = null;
            string key = Normalize(executableName);
            if (key == null || string.IsNullOrEmpty(evidenceSignature)) return false;
            lock (_gate)
            {
                entry = _entries.FirstOrDefault(e =>
                    string.Equals(e.ExecutableName, key, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.EvidenceSignature, evidenceSignature,
                        StringComparison.Ordinal));
            }
            return entry != null;
        }

        bool IHookLearnedProfileStore.TryGetByEarlySignature(string executableName,
            string earlySignature, out HookLearnedProfileEntry entry)
        {
            entry = null;
            string key = Normalize(executableName);
            if (key == null || string.IsNullOrEmpty(earlySignature)) return false;
            lock (_gate)
            {
                entry = _entries
                    .Where(e => string.Equals(e.ExecutableName, key,
                                    StringComparison.OrdinalIgnoreCase) &&
                                string.Equals(e.EarlySignature, earlySignature,
                                    StringComparison.Ordinal))
                    .OrderByDescending(e => e.UpdatedUtc)
                    .FirstOrDefault();
            }
            return entry != null;
        }

        IReadOnlyList<HookLearnedProfileEntry> IHookLearnedProfileStore.GetForExecutable(
            string executableName)
            => GetForExecutableCore(executableName);

        private IReadOnlyList<HookLearnedProfileEntry> GetForExecutableCore(string executableName)
        {
            string key = Normalize(executableName);
            if (key == null) return Array.Empty<HookLearnedProfileEntry>();
            lock (_gate)
            {
                return _entries
                    .Where(e => string.Equals(e.ExecutableName, key,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(e => e.UpdatedUtc)
                    .ToList();
            }
        }

        HookLearnedProfileEntry IHookLearnedProfileStore.Upsert(string executableName,
            string executablePath, string evidenceSignature, string earlySignature,
            string hookBuildHash, Action<HookLearnedProfileEntry> mutate)
        {
            string key = Normalize(executableName);
            if (key == null) throw new ArgumentException("executable name required", nameof(executableName));
            string signature = string.IsNullOrEmpty(evidenceSignature)
                ? HookTargetEvidence.UnknownSignature
                : evidenceSignature;
            HookLearnedProfileEntry entry;
            lock (_gate)
            {
                entry = _entries.FirstOrDefault(e =>
                    string.Equals(e.ExecutableName, key, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(e.EvidenceSignature, signature, StringComparison.Ordinal));
                DateTime now = DateTime.UtcNow;
                if (entry == null)
                {
                    entry = new HookLearnedProfileEntry
                    {
                        ExecutableName = key,
                        EvidenceSignature = signature,
                        CreatedUtc = now
                    };
                    _entries.Add(entry);
                }
                if (!string.IsNullOrWhiteSpace(executablePath))
                    entry.ExecutablePath = executablePath;
                if (!string.IsNullOrEmpty(earlySignature))
                    entry.EarlySignature = earlySignature;
                if (!string.IsNullOrEmpty(hookBuildHash))
                    entry.HookBuildHash = hookBuildHash;
                mutate?.Invoke(entry);
                entry.UpdatedUtc = now;
                SaveLocked();
            }
            _changes.OnNext(0);
            return entry;
        }

        IReadOnlyList<HookLearnedProfileEntry> IHookLearnedProfileStore.Snapshot()
        {
            lock (_gate) return _entries.ToList();
        }

        public void Reset()
        {
            lock (_gate)
            {
                _entries.Clear();
                SaveLocked();
            }
            Log.Information("HookOverlay: learned compatibility profiles were reset");
            _changes.OnNext(0);
        }

        public IReadOnlyList<HookLearnedProfileSummary> GetForProcess(string executableName)
            => GetForExecutableCore(executableName).Select(e => e.ToSummary()).ToList();

        public IReadOnlyList<HookLearnedProfileSummary> GetAll()
        {
            lock (_gate)
            {
                return _entries.OrderBy(e => e.ExecutableName, StringComparer.OrdinalIgnoreCase)
                    .ThenByDescending(e => e.UpdatedUtc)
                    .Select(e => e.ToSummary())
                    .ToList();
            }
        }

        public void Dispose()
        {
            _changes.OnCompleted();
            _changes.Dispose();
        }

        internal static string Normalize(string executableName)
        {
            if (string.IsNullOrWhiteSpace(executableName)) return null;
            try
            {
                string name = Path.GetFileNameWithoutExtension(executableName.Trim());
                return string.IsNullOrWhiteSpace(name) ? null : name.ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private void Load()
        {
            if (_path == null || !File.Exists(_path)) return;
            try
            {
                string json = File.ReadAllText(_path);
                StoreFile file = JsonConvert.DeserializeObject<StoreFile>(json);
                if (file == null || file.Version != FormatVersion)
                    throw new InvalidDataException($"unsupported learned profile format {file?.Version}");
                lock (_gate)
                {
                    _entries.Clear();
                    foreach (HookLearnedProfileEntry entry in file.Entries ?? new List<HookLearnedProfileEntry>())
                    {
                        if (entry == null || Normalize(entry.ExecutableName) == null) continue;
                        entry.ExecutableName = Normalize(entry.ExecutableName);
                        if (string.IsNullOrEmpty(entry.EvidenceSignature))
                            entry.EvidenceSignature = HookTargetEvidence.UnknownSignature;
                        _entries.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                // Never let a damaged file cost the overlay: set it aside and start empty.
                string quarantine = $"{_path}.corrupt-{DateTime.UtcNow:yyyyMMddTHHmmssZ}";
                try { File.Move(_path, quarantine, true); }
                catch (Exception moveEx)
                {
                    Log.Warning(moveEx, "HookOverlay: could not set aside the damaged learned profile file");
                }
                Log.Warning(ex, "HookOverlay: learned compatibility profiles at {path} were unreadable and moved to {quarantine}",
                    _path, quarantine);
            }
        }

        private void SaveLocked()
        {
            if (_path == null) return;
            try
            {
                string directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporary = Path.Combine(directory ?? string.Empty,
                    $".{Path.GetFileName(_path)}.{Guid.NewGuid():N}.tmp");
                var file = new StoreFile
                {
                    Entries = _entries
                        .OrderBy(e => e.ExecutableName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(e => e.EvidenceSignature, StringComparer.Ordinal)
                        .ToList()
                };
                try
                {
                    File.WriteAllText(temporary, JsonConvert.SerializeObject(file, Formatting.Indented));
                    File.Move(temporary, _path, true);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "HookOverlay: could not save the learned compatibility profiles to {path}", _path);
            }
        }
    }
}
