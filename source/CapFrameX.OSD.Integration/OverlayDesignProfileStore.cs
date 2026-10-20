using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using CapFrameX.Contracts.Localization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CapFrameX.OSD.Integration
{
    public sealed class OverlayDesignProfile
    {
        internal OverlayDesignProfile(string id, string name, string designJson, DateTime modifiedUtc)
        {
            Id = id;
            Name = name;
            DesignJson = designJson;
            ModifiedUtc = modifiedUtc;
        }

        public string Id { get; }
        public string Name { get; }
        public string DesignJson { get; }
        public DateTime ModifiedUtc { get; }
        public override string ToString() => Name;
    }

    /// <summary>
    /// Owns the designer's profile library, not the renderer's active in-game configuration.
    /// Display names never become file names. Mutations replace the whole library atomically,
    /// so profile contents and the last selected profile cannot become inconsistent.
    /// </summary>
    public sealed class OverlayDesignProfileStore
    {
        public const int MaximumProfiles = 128;
        public const int MaximumNameLength = 80;
        public const int MaximumDesignBytes = 1024 * 1024;
        public const int MaximumLibraryBytes = 16 * 1024 * 1024;
        private const int FormatVersion = 1;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        private static readonly object StarterInitializationGate = new object();
        private readonly Func<string, string> _canonicalize;
        private readonly object _gate = new object();
        private readonly string _directory;
        private readonly string _filePath;
        private readonly string _backupPath;
        private IReadOnlyList<OverlayDesignProfile> _profiles = Array.Empty<OverlayDesignProfile>();
        private string _expectedFileHash;
        private bool _recovered;
        private int _starterCatalogVersion;

        public OverlayDesignProfileStore(string configurationFolder, Func<string, string> canonicalize)
        {
            if (string.IsNullOrWhiteSpace(configurationFolder))
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ConfigurationFolder"), message => new ArgumentException(message, nameof(configurationFolder)));
            _canonicalize = canonicalize ?? throw new ArgumentNullException(nameof(canonicalize));
            _directory = Path.Combine(Path.GetFullPath(configurationFolder), "OverlayDesigns");
            _filePath = Path.Combine(_directory, "profiles.json");
            _backupPath = _filePath + ".bak";
            Load();
        }

        public IReadOnlyList<OverlayDesignProfile> Profiles => _profiles;
        public string ActiveProfileId { get; private set; }
        private LocalizedText _recoveryWarning;
        private LocalizedText _starterInitializationWarning;
        public string RecoveryWarning => _recoveryWarning?.Resolve();
        public string StarterInitializationWarning => _starterInitializationWarning?.Resolve();

        internal bool EnsureStarterProfiles(IReadOnlyList<OverlayDesignStarterProfile> starters, int catalogVersion)
        {
            lock (_gate)
            lock (StarterInitializationGate)
            {
                if (_starterCatalogVersion >= catalogVersion) return true;
                _starterInitializationWarning = null;
                try
                {
                    // A service and an editor may open the same library before either seeds it.
                    // Refresh only for this explicit bootstrap operation; ordinary edits retain
                    // their stale-writer protection and never discard an open working document.
                    Reload();
                    if (_starterCatalogVersion >= catalogVersion) return true;
                    if (_recovered)
                    {
                        _starterInitializationWarning = new LocalizedText("OverlayDesignStatus_StartersRecovered");
                        return false;
                    }
                    var profiles = _profiles.ToList();
                    foreach (var starter in starters)
                    {
                        int existingIndex = profiles.FindIndex(profile => profile.Id == starter.Id);
                        if (existingIndex >= 0 && starter.CanUpdate(profiles[existingIndex]))
                        {
                            // A correction may rename the design. When another profile already uses the
                            // new name, keep the old design: a library never holds one name twice.
                            if (profiles.Any(profile => profile.Id != starter.Id && string.Equals(profile.Name, starter.Name, StringComparison.OrdinalIgnoreCase)))
                                continue;
                            profiles[existingIndex] = new OverlayDesignProfile(starter.Id, starter.Name,
                                WithName(starter.DesignJson, starter.Name), DateTime.UtcNow);
                            continue;
                        }
                        // A catalog upgrade adds its new designs only. Older starters may have
                        // been deliberately deleted or renamed; their absence is authoritative.
                        if (starter.IntroducedInVersion <= _starterCatalogVersion) continue;
                        if (profiles.Count >= MaximumProfiles) break;
                        // A name already used by the user is authoritative, even when its ID
                        // or contents differ from the shipped design. Never rename or replace it.
                        if (profiles.Any(profile => profile.Id == starter.Id || string.Equals(profile.Name, starter.Name, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        profiles.Add(new OverlayDesignProfile(starter.Id, starter.Name,
                            WithName(starter.DesignJson, starter.Name), DateTime.UtcNow));
                    }
                    Persist(profiles, ActiveProfileId ?? profiles.FirstOrDefault()?.Id, catalogVersion);
                    return true;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || IsInvalidData(error))
                {
                    // Read-only and concurrently edited libraries remain usable. The marker is
                    // written in the same atomic replacement as the profiles, so retrying later
                    // cannot duplicate a partially installed catalog.
                    _starterInitializationWarning = new LocalizedText("OverlayDesignStatus_StartersFailed", OverlayDesignText.Details(error));
                    return false;
                }
            }
        }

        private void Reload()
        {
            var profiles = _profiles;
            string activeId = ActiveProfileId, expectedHash = _expectedFileHash;
            var warning = _recoveryWarning;
            int catalogVersion = _starterCatalogVersion;
            bool recovered = _recovered;
            _profiles = Array.Empty<OverlayDesignProfile>();
            ActiveProfileId = null;
            _starterCatalogVersion = 0;
            _recovered = false;
            _recoveryWarning = null;
            try { Load(); }
            catch
            {
                _profiles = profiles; ActiveProfileId = activeId; _expectedFileHash = expectedHash;
                _starterCatalogVersion = catalogVersion; _recovered = recovered; _recoveryWarning = warning;
                throw;
            }
        }

        public OverlayDesignProfile Get(string id)
        {
            ValidateId(id);
            return _profiles.FirstOrDefault(profile => profile.Id == id)
                ?? throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ProfileMissing"), message => new KeyNotFoundException(message));
        }

        public OverlayDesignProfile Create(string name, string designJson)
        {
            lock (_gate)
            {
                if (_profiles.Count >= MaximumProfiles)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ProfileLimit", MaximumProfiles), message => new InvalidOperationException(message));
                name = ValidateName(name);
                EnsureUniqueName(name, null);
                var profile = new OverlayDesignProfile(Guid.NewGuid().ToString("N"), name,
                    WithName(designJson, name), DateTime.UtcNow);
                Persist(_profiles.Concat(new[] { profile }).ToArray(), profile.Id);
                return profile;
            }
        }

        public OverlayDesignProfile Duplicate(string id, string name) => Create(name, Get(id).DesignJson);

        public OverlayDesignProfile Rename(string id, string name)
        {
            lock (_gate)
            {
                var current = Get(id);
                name = ValidateName(name);
                EnsureUniqueName(name, id);
                var updated = new OverlayDesignProfile(id, name, WithName(current.DesignJson, name), DateTime.UtcNow);
                Persist(_profiles.Select(profile => profile.Id == id ? updated : profile).ToArray(), ActiveProfileId);
                return updated;
            }
        }

        public OverlayDesignProfile Save(string id, string designJson)
        {
            lock (_gate)
            {
                var current = Get(id);
                var updated = new OverlayDesignProfile(id, current.Name, WithName(designJson, current.Name), DateTime.UtcNow);
                Persist(_profiles.Select(profile => profile.Id == id ? updated : profile).ToArray(), ActiveProfileId);
                return updated;
            }
        }

        public void Delete(string id)
        {
            lock (_gate)
            {
                Get(id);
                if (_profiles.Count < 2)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_KeepProfile"), message => new InvalidOperationException(message));
                var remaining = _profiles.Where(profile => profile.Id != id).ToArray();
                Persist(remaining, ActiveProfileId == id ? remaining.FirstOrDefault()?.Id : ActiveProfileId);
            }
        }

        public void SetActive(string id)
        {
            lock (_gate)
            {
                Get(id);
                if (ActiveProfileId != id || _recovered)
                    Persist(_profiles, id);
            }
        }

        internal string Canonicalize(string designJson)
        {
            CheckDesignSize(designJson);
            string canonical = _canonicalize(designJson);
            CheckDesignSize(canonical);
            return canonical;
        }

        internal string WithName(string designJson, string name)
        {
            var document = JObject.Parse(Canonicalize(designJson));
            document["name"] = ValidateName(name);
            return Canonicalize(document.ToString(Formatting.None));
        }

        private void Load()
        {
            byte[] primary = ReadBytes(_filePath);
            _expectedFileHash = Hash(primary);
            if (primary == null && !File.Exists(_backupPath))
                return;
            try
            {
                if (primary == null)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_LibraryMissing"), message => new InvalidDataException(message));
                Apply(Parse(primary));
            }
            catch (Exception error) when (IsInvalidData(error))
            {
                var backup = ReadBytes(_backupPath);
                if (backup == null)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_DamagedNoBackup"), message => new InvalidDataException(message, error));
                Library recovered;
                try
                {
                    recovered = Parse(backup);
                }
                catch (Exception backupError) when (IsInvalidData(backupError))
                {
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_DamagedBackup"), message => new InvalidDataException(message, backupError));
                }
                Apply(recovered);
                _recovered = true;
                _recoveryWarning = new LocalizedText("OverlayDesignStatus_Recovered");
            }
        }

        private Library Parse(byte[] bytes)
        {
            var document = JObject.Parse(Utf8.GetString(bytes), new JsonLoadSettings
            {
                DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error
            });
            // A newer library must never be replaced with an older backup by this version.
            if (document.Value<int?>(nameof(Library.Version)) > FormatVersion)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_NewerLibrary"), message => new NotSupportedException(message));
            var library = document.ToObject<Library>(JsonSerializer.Create(new JsonSerializerSettings
            {
                MaxDepth = 64,
                TypeNameHandling = TypeNameHandling.None
            }));
            if (library.Version != FormatVersion || library.Profiles == null || library.Profiles.Count > MaximumProfiles || library.StarterCatalogVersion < 0)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_InvalidLibrary"), message => new InvalidDataException(message));
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var profile in library.Profiles)
            {
                if (profile == null)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_EmptyProfile"), message => new InvalidDataException(message));
                ValidateId(profile.Id);
                profile.Name = ValidateName(profile.Name);
                if (!ids.Add(profile.Id) || !names.Add(profile.Name))
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_DuplicateProfiles"), message => new InvalidDataException(message));
                profile.DesignJson = Canonicalize(profile.DesignJson);
                if (profile.ModifiedUtc == default)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ModifiedDate"), message => new InvalidDataException(message));
            }
            if (library.Profiles.Count == 0 ? library.ActiveProfileId != null : !ids.Contains(library.ActiveProfileId ?? string.Empty))
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_SelectedMissing"), message => new InvalidDataException(message));
            return library;
        }

        private void Persist(IReadOnlyList<OverlayDesignProfile> profiles, string activeId, int? starterCatalogVersion = null)
        {
            var library = new Library
            {
                Version = FormatVersion,
                ActiveProfileId = activeId,
                StarterCatalogVersion = starterCatalogVersion ?? _starterCatalogVersion,
                Profiles = profiles.Select(profile => new StoredProfile
                {
                    Id = profile.Id,
                    Name = profile.Name,
                    DesignJson = profile.DesignJson,
                    ModifiedUtc = profile.ModifiedUtc
                }).ToList()
            };
            byte[] bytes = Utf8.GetBytes(JsonConvert.SerializeObject(library, Formatting.Indented));
            if (bytes.Length > MaximumLibraryBytes)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_LibraryTooLarge"), message => new InvalidOperationException(message));
            Directory.CreateDirectory(_directory);
            string temporary = Path.Combine(_directory, Guid.NewGuid().ToString("N") + ".tmp");
            // Separate instances cooperate through this lock and detect stale snapshots. They
            // must never overwrite each other's edits just because an editor stayed open.
            using (var writerLock = new FileStream(Path.Combine(_directory, "profiles.lock"),
                FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                if (Hash(ReadBytes(_filePath)) != _expectedFileHash)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_ConcurrentChange"), message => new IOException(message));
                try
                {
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        output.Write(bytes, 0, bytes.Length);
                        output.Flush(true);
                    }
                    if (File.Exists(_filePath))
                    {
                        if (_recovered)
                        {
                            string retained = Path.Combine(_directory, "profiles.corrupt-" + Guid.NewGuid().ToString("N") + ".json");
                            File.Copy(_filePath, retained, false);
                            // Keep the known good backup; do not replace it with the corrupt file.
                            File.Replace(temporary, _filePath, null);
                        }
                        else
                        {
                            File.Replace(temporary, _filePath, _backupPath);
                        }
                    }
                    else
                    {
                        File.Move(temporary, _filePath);
                    }
                    _expectedFileHash = Hash(bytes);
                    _profiles = Array.AsReadOnly(profiles.ToArray());
                    ActiveProfileId = activeId;
                    _starterCatalogVersion = library.StarterCatalogVersion;
                    _recovered = false;
                    _recoveryWarning = null;
                }
                finally
                {
                    // Cleanup failures must not mask a useful save error or report a completed
                    // atomic replacement as failed. A leftover temporary file is never loaded.
                    try { if (File.Exists(temporary)) File.Delete(temporary); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
        }

        private void Apply(Library library)
        {
            _profiles = Array.AsReadOnly(library.Profiles.Select(profile => new OverlayDesignProfile(
                profile.Id, profile.Name, profile.DesignJson, profile.ModifiedUtc)).ToArray());
            ActiveProfileId = library.ActiveProfileId;
            _starterCatalogVersion = library.StarterCatalogVersion;
        }

        private void EnsureUniqueName(string name, string exceptId)
        {
            if (_profiles.Any(profile => profile.Id != exceptId && string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase)))
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_NameExists"), message => new ArgumentException(message, nameof(name)));
        }

        private static string ValidateName(string name)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name) || name.Length > MaximumNameLength || name.Any(char.IsControl) || name.IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_NameInvalid", MaximumNameLength), message => new ArgumentException(message, nameof(name)));
            return name;
        }

        private static void ValidateId(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _))
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_IdInvalid"), message => new ArgumentException(message, nameof(id)));
        }

        private static void CheckDesignSize(string designJson)
        {
            if (string.IsNullOrWhiteSpace(designJson) || Utf8.GetByteCount(designJson) > MaximumDesignBytes)
                throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_DesignSize"), message => new ArgumentException(message, nameof(designJson)));
        }

        private static byte[] ReadBytes(string path)
        {
            if (!File.Exists(path))
                return null;
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > MaximumLibraryBytes)
                    throw OverlayDesignText.Error(new LocalizedText("OverlayDesignError_LibrarySize"), message => new InvalidDataException(message));
                var bytes = new byte[(int)input.Length];
                input.ReadExactly(bytes);
                return bytes;
            }
        }

        private static string Hash(byte[] bytes) => bytes == null ? null : Convert.ToHexString(SHA256.HashData(bytes));

        private static bool IsInvalidData(Exception error) => error is JsonException || error is System.Text.Json.JsonException || error is ArgumentException
            || error is InvalidDataException || error is InvalidOperationException || error is FormatException;

        private sealed class Library
        {
            public int Version { get; set; }
            public string ActiveProfileId { get; set; }
            public int StarterCatalogVersion { get; set; }
            public List<StoredProfile> Profiles { get; set; }
        }

        private sealed class StoredProfile
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string DesignJson { get; set; }
            public DateTime ModifiedUtc { get; set; }
        }
    }
}
