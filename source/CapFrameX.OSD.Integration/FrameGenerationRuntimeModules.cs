using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Serilog;

namespace CapFrameX.OSD.Integration
{
    /// <summary>
    /// A frame generation runtime whose version is the version of the library that runs it.
    /// FSR is not one of them: its library holds several providers, and which one runs is a
    /// property of the context (see <see cref="HookFidelityFxVersionProbe"/>).
    /// </summary>
    internal enum EFrameGenerationRuntime
    {
        DlssFrameGeneration,
        XessFrameGeneration
    }

    internal readonly struct RuntimeFileVersion : IComparable<RuntimeFileVersion>
    {
        internal RuntimeFileVersion(int major, int minor, int build, int revision,
            string description, string module = null)
        {
            Major = major;
            Minor = minor;
            Build = build;
            Revision = revision;
            Description = description;
            Module = module;
        }

        internal int Major { get; }
        internal int Minor { get; }
        internal int Build { get; }
        internal int Revision { get; }
        internal string Description { get; }
        /// <summary>The file name of the module the version was read from, for the log.</summary>
        internal string Module { get; }

        internal bool IsEmpty => Major == 0 && Minor == 0 && Build == 0 && Revision == 0;

        internal RuntimeFileVersion WithModule(string module)
            => new RuntimeFileVersion(Major, Minor, Build, Revision, Description, module);

        /// <summary>
        /// Three components, as NVIDIA and Intel name their releases (310.7.130, 1.3.1); the
        /// fourth is a build counter.
        /// </summary>
        internal string ToDisplayString() => $"{Major}.{Minor}.{Build}";

        public int CompareTo(RuntimeFileVersion other)
        {
            int result = Major.CompareTo(other.Major);
            if (result == 0) result = Minor.CompareTo(other.Minor);
            if (result == 0) result = Build.CompareTo(other.Build);
            return result != 0 ? result : Revision.CompareTo(other.Revision);
        }
    }

    /// <summary>
    /// Finds the frame generation runtimes among a process' modules. Pure: the version of a file
    /// comes from <paramref name="readVersion"/>.
    /// </summary>
    internal static class FrameGenerationRuntimeModuleClassifier
    {
        internal const string DlssFrameGenerationModule = "nvngx_dlssg.dll";
        internal const string XessFrameGenerationModule = "libxess_fg.dll";
        /// <summary>
        /// The XeSS-FG runtime of the Intel driver, in its driver store folder. The SDK asks it
        /// whether the driver runs the context and then hands every call to it; when the SDK
        /// keeps the context, it unloads the driver's copy in the same millisecond.
        /// </summary>
        internal const string XessDriverFrameGenerationModule = "igxess_fg.dll";
        // NGX keeps the copies it downloads as ...\NVIDIA\NGX\models\dlssg\versions\<n>\files\
        // <id>.bin and loads them under that name; the folders of an override start alike.
        internal const string NgxDlssFrameGenerationFolder = @"\ngx\models\dlssg";
        internal const string DlssFrameGenerationDescription = "NVIDIA DLSS-G";

        internal static bool TryClassify(HookModuleRecord module, out EFrameGenerationRuntime runtime)
            => TryClassify(module, out runtime, out _);

        /// <param name="driverRuntime">The module is a driver's runtime, which runs the context
        /// whenever it is loaded.</param>
        internal static bool TryClassify(HookModuleRecord module, out EFrameGenerationRuntime runtime,
            out bool driverRuntime)
        {
            runtime = default;
            driverRuntime = false;
            string name = module.Name;
            if (string.IsNullOrWhiteSpace(name)) return false;
            if (string.Equals(name, XessFrameGenerationModule, StringComparison.OrdinalIgnoreCase))
            {
                runtime = EFrameGenerationRuntime.XessFrameGeneration;
                return true;
            }
            if (string.Equals(name, XessDriverFrameGenerationModule, StringComparison.OrdinalIgnoreCase))
            {
                runtime = EFrameGenerationRuntime.XessFrameGeneration;
                driverRuntime = true;
                return true;
            }
            if (string.Equals(name, DlssFrameGenerationModule, StringComparison.OrdinalIgnoreCase) ||
                (module.Path != null && module.Path.Replace('/', '\\').IndexOf(
                    NgxDlssFrameGenerationFolder, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                runtime = EFrameGenerationRuntime.DlssFrameGeneration;
                return true;
            }
            return false;
        }

        /// <summary>
        /// The version of every runtime found. A DLSS frame generation library has to say so in
        /// its version resource: a replacement that maps DLSS-G onto another technique carries
        /// the name but not NVIDIA's description, and its version would name the wrong thing.
        /// Of several copies the newest counts, as NGX loads the newest it finds. A loaded driver
        /// runtime comes before every library of the game, whatever their versions: it stays
        /// loaded only while it runs the context (Intel driver 32.0.101.9175 for the 1.3.1.78
        /// XeSS-FG SDK, 2026-11-22).
        /// </summary>
        internal static IReadOnlyDictionary<EFrameGenerationRuntime, RuntimeFileVersion> Find(
            IEnumerable<HookModuleRecord> modules, Func<string, RuntimeFileVersion?> readVersion)
        {
            var found = new Dictionary<EFrameGenerationRuntime, RuntimeFileVersion>();
            if (modules == null || readVersion == null) return found;
            var foundInDriver = new HashSet<EFrameGenerationRuntime>();
            foreach (HookModuleRecord module in modules)
            {
                if (!TryClassify(module, out EFrameGenerationRuntime runtime, out bool driverRuntime) ||
                    string.IsNullOrWhiteSpace(module.Path))
                    continue;
                RuntimeFileVersion? version = readVersion(module.Path);
                if (version == null || version.Value.IsEmpty) continue;
                if (runtime == EFrameGenerationRuntime.DlssFrameGeneration &&
                    version.Value.Description?.StartsWith(DlssFrameGenerationDescription,
                        StringComparison.OrdinalIgnoreCase) != true)
                    continue;
                bool known = found.TryGetValue(runtime, out RuntimeFileVersion knownVersion);
                bool knownInDriver = foundInDriver.Contains(runtime);
                bool replaces = !known
                    || (driverRuntime && !knownInDriver)
                    || (driverRuntime == knownInDriver && version.Value.CompareTo(knownVersion) > 0);
                if (!replaces) continue;
                found[runtime] = version.Value.WithModule(module.Name);
                if (driverRuntime) foundInDriver.Add(runtime);
            }
            return found;
        }
    }

    /// <summary>
    /// The versions of the frame generation libraries the overlay target has loaded. The answer
    /// comes from the last finished scan; a scan runs in the background, so the overlay refresh
    /// that asks never waits for a module snapshot or a file. Only asked while the hook reports
    /// the technique as switched on, so a process without frame generation is never scanned.
    /// </summary>
    internal sealed class FrameGenerationRuntimeModuleVersions
    {
        /// <summary>A library that is loaded keeps its version; this only notices a reload.</summary>
        internal const long RescanIntervalMs = 30000;
        /// <summary>The technique is on, so its library is about to appear or was missed.</summary>
        internal const long RetryIntervalMs = 2000;

        private readonly object _gate = new object();
        private readonly Func<int, IReadOnlyList<HookModuleRecord>> _enumerate;
        private readonly Func<string, RuntimeFileVersion?> _readVersion;
        private readonly Func<long> _clock;
        private readonly Action<Action> _run;
        private int _processId;
        private bool _scanning;
        private bool _scanned;
        private long _scannedAt;
        private IReadOnlyDictionary<EFrameGenerationRuntime, RuntimeFileVersion> _versions;

        internal FrameGenerationRuntimeModuleVersions()
            : this(EnumerateModules, ReadFileVersion, () => Environment.TickCount64,
                scan => Task.Run(scan))
        {
        }

        internal FrameGenerationRuntimeModuleVersions(
            Func<int, IReadOnlyList<HookModuleRecord>> enumerate,
            Func<string, RuntimeFileVersion?> readVersion, Func<long> clock, Action<Action> run)
        {
            _enumerate = enumerate;
            _readVersion = readVersion;
            _clock = clock;
            _run = run;
        }

        /// <summary>Null until a scan found the runtime in the process.</summary>
        internal RuntimeFileVersion? GetVersion(int processId, EFrameGenerationRuntime runtime)
        {
            if (processId <= 0) return null;
            RuntimeFileVersion? result = null;
            bool start = false;
            lock (_gate)
            {
                if (processId != _processId)
                {
                    // A scan of the former target still running is discarded when it reports.
                    _processId = processId;
                    _versions = null;
                    _scanned = false;
                    _scanning = false;
                }
                if (_versions != null && _versions.TryGetValue(runtime, out RuntimeFileVersion known))
                    result = known;
                long age = _clock() - _scannedAt;
                bool due = !_scanned || age < 0 ||
                    age >= (result.HasValue ? RescanIntervalMs : RetryIntervalMs);
                if (due && !_scanning)
                {
                    _scanning = true;
                    start = true;
                }
            }
            if (start) _run(() => Scan(processId));
            return result;
        }

        private void Scan(int processId)
        {
            IReadOnlyDictionary<EFrameGenerationRuntime, RuntimeFileVersion> versions = null;
            try
            {
                IReadOnlyList<HookModuleRecord> modules = _enumerate(processId);
                // An unreadable module list says nothing: what the last scan found stands.
                if (modules != null)
                    versions = FrameGenerationRuntimeModuleClassifier.Find(modules, _readVersion);
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "Frame generation runtime scan of pid {pid} failed", processId);
            }

            lock (_gate)
            {
                if (processId != _processId) return;
                _scanning = false;
                _scanned = true;
                _scannedAt = _clock();
                if (versions == null) return;
                if (!SameVersions(_versions, versions))
                    Log.Information("Frame generation runtimes of pid {pid}: {runtimes}",
                        processId, Describe(versions));
                _versions = versions;
            }
        }

        private static bool SameVersions(
            IReadOnlyDictionary<EFrameGenerationRuntime, RuntimeFileVersion> a,
            IReadOnlyDictionary<EFrameGenerationRuntime, RuntimeFileVersion> b)
        {
            if (a == null || a.Count != b.Count) return false;
            foreach (var pair in a)
                if (!b.TryGetValue(pair.Key, out RuntimeFileVersion other) ||
                    pair.Value.CompareTo(other) != 0)
                    return false;
            return true;
        }

        private static string Describe(
            IReadOnlyDictionary<EFrameGenerationRuntime, RuntimeFileVersion> versions)
        {
            if (versions.Count == 0) return "none loaded";
            var parts = new List<string>();
            foreach (var pair in versions)
                parts.Add($"{pair.Key} {pair.Value.Major}.{pair.Value.Minor}.{pair.Value.Build}." +
                          $"{pair.Value.Revision}" +
                          (string.IsNullOrEmpty(pair.Value.Module) ? "" : $" ({pair.Value.Module})"));
            return string.Join(", ", parts);
        }

        private static IReadOnlyList<HookModuleRecord> EnumerateModules(int processId)
            => HookModuleScanner.TryEnumerate(processId,
                out IReadOnlyList<HookModuleRecord> modules, out _) ? modules : null;

        internal static RuntimeFileVersion? ReadFileVersion(string path)
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(path);
                return new RuntimeFileVersion(info.FileMajorPart, info.FileMinorPart,
                    info.FileBuildPart, info.FilePrivatePart, info.FileDescription);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                       ex is ArgumentException || ex is NotSupportedException)
            {
                return null;
            }
        }
    }
}
