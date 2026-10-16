using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace CapFrameX.Data
{
    /// <summary>
    /// The metadata of one record file as the record list shows it, with the file stamp it was
    /// read at. The game name is the one stored in the file; the process list's display name is
    /// applied when the record is listed, as for a freshly read file.
    /// </summary>
    internal sealed class RecordInfoIndexEntry
    {
        [JsonProperty("path")] public string Path { get; set; }
        [JsonProperty("length")] public long Length { get; set; }
        [JsonProperty("lastWriteUtcTicks")] public long LastWriteTimeUtcTicks { get; set; }
        [JsonProperty("lastUsedUtcTicks")] public long LastUsedUtcTicks { get; set; }

        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("hash")] public string Hash { get; set; }
        [JsonProperty("gameName")] public string GameName { get; set; }
        [JsonProperty("processName")] public string ProcessName { get; set; }
        [JsonProperty("creationDate")] public string CreationDate { get; set; }
        [JsonProperty("creationTime")] public string CreationTime { get; set; }
        [JsonProperty("recordTime")] public double RecordTime { get; set; }
        [JsonProperty("combinedInfo")] public string CombinedInfo { get; set; }
        [JsonProperty("deviceName")] public string DeviceName { get; set; }
        [JsonProperty("motherboard")] public string MotherboardName { get; set; }
        [JsonProperty("os")] public string OsVersion { get; set; }
        [JsonProperty("processor")] public string ProcessorName { get; set; }
        [JsonProperty("systemRam")] public string SystemRamInfo { get; set; }
        [JsonProperty("baseDriverVersion")] public string BaseDriverVersion { get; set; }
        [JsonProperty("driverPackage")] public string DriverPackage { get; set; }
        [JsonProperty("gpuCount")] public string NumberGPUs { get; set; }
        [JsonProperty("gpu")] public string GraphicCardName { get; set; }
        [JsonProperty("gpuCoreClock")] public string GPUCoreClock { get; set; }
        [JsonProperty("gpuMemoryClock")] public string GPUMemoryClock { get; set; }
        [JsonProperty("gpuMemory")] public string GPUMemory { get; set; }
        [JsonProperty("gpuDriverVersion")] public string GPUDriverVersion { get; set; }
        [JsonProperty("comment")] public string Comment { get; set; }
        [JsonProperty("isAggregated")] public string IsAggregated { get; set; }
        [JsonProperty("isValid")] public bool IsValid { get; set; }
        [JsonProperty("hasInfoHeader")] public bool HasInfoHeader { get; set; }
        [JsonProperty("apiInfo")] public string ApiInfo { get; set; }
        [JsonProperty("resizableBar")] public string ResizableBar { get; set; }
        [JsonProperty("winGameMode")] public string WinGameMode { get; set; }
        [JsonProperty("hags")] public string HAGS { get; set; }
        [JsonProperty("presentationMode")] public string PresentationMode { get; set; }
        [JsonProperty("displayLayer")] public string DisplayLayer { get; set; }
        [JsonProperty("resolution")] public string Resolution { get; set; }

        internal RecordInfoIndexEntry Clone() => (RecordInfoIndexEntry)MemberwiseClone();
    }

    /// <summary>
    /// Remembers the metadata of the record files that were read, keyed by path, size and
    /// modification time, in a JSON file in the configuration folder. On the next start a record
    /// folder is listed from the index instead of being read file by file: a JSON record is
    /// tokenized to its end for the run count and record time, a CSV record is parsed and hashed
    /// in full. An index hit does not touch the file; a changed file misses its entry and is read
    /// again. The index is written a moment after the last change and when the app exits. It is
    /// a cache: a missing or unreadable index only costs one full read of the folder.
    /// </summary>
    public sealed class RecordInfoIndex : IDisposable
    {
        internal const int FormatVersion = 1;
        internal const int DefaultCapacity = 20_000;
        // A save waits for the changes of a folder listing to settle, but not forever.
        internal static readonly TimeSpan SaveDelay = TimeSpan.FromSeconds(2);
        internal static readonly TimeSpan MaxSaveDelay = TimeSpan.FromSeconds(30);
        // Entries not used for this long are dropped. A hit refreshes the use stamp at most once
        // a day, so listing an unchanged folder does not rewrite the index every time.
        internal static readonly TimeSpan Retention = TimeSpan.FromDays(180);
        internal static readonly TimeSpan UseStampResolution = TimeSpan.FromDays(1);

        private sealed class IndexFile
        {
            [JsonProperty("version")] public int Version { get; set; }
            [JsonProperty("entries")] public List<RecordInfoIndexEntry> Entries { get; set; }
        }

        private readonly ILogger _logger;
        private readonly object _sync = new object();
        private readonly object _saveSync = new object();
        private readonly Timer _saveTimer;
        private Dictionary<string, RecordInfoIndexEntry> _entries;
        private bool _dirty, _disposed;
        private DateTime? _firstChangeUtc;

        /// <param name="filePath">Where the index is kept; null keeps nothing.</param>
        public RecordInfoIndex(string filePath, ILogger<RecordInfoIndex> logger = null)
        {
            FilePath = string.IsNullOrWhiteSpace(filePath) ? null : Path.GetFullPath(filePath);
            _logger = logger;
            _saveTimer = new Timer(_ => Save(), null, Timeout.Infinite, Timeout.Infinite);
        }

        public string FilePath { get; }

        internal int Capacity { get; set; } = DefaultCapacity;

        internal Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

        internal int Count
        {
            get
            {
                if (FilePath == null) return 0;
                lock (_sync)
                {
                    EnsureLoadedLocked();
                    return _entries.Count;
                }
            }
        }

        /// <summary>The entry of the file as it was read, if the file still has that stamp.</summary>
        internal bool TryGet(string path, long length, long lastWriteTimeUtcTicks, out RecordInfoIndexEntry entry)
        {
            entry = null;
            if (FilePath == null) return false;
            string key = Normalize(path);
            lock (_sync)
            {
                EnsureLoadedLocked();
                if (!_entries.TryGetValue(key, out var found)
                    || found.Length != length
                    || found.LastWriteTimeUtcTicks != lastWriteTimeUtcTicks)
                {
                    return false;
                }

                long now = Clock().Ticks;
                if (now - found.LastUsedUtcTicks >= UseStampResolution.Ticks)
                {
                    found = found.Clone();
                    found.LastUsedUtcTicks = now;
                    _entries[key] = found;
                    MarkDirtyLocked();
                }
                entry = found;
                return true;
            }
        }

        /// <summary>Remembers what was read from the file with the given stamp.</summary>
        internal void Store(string path, long length, long lastWriteTimeUtcTicks, RecordInfoIndexEntry entry)
        {
            if (FilePath == null || entry == null) return;
            var stored = entry.Clone();
            stored.Path = Normalize(path);
            stored.Length = length;
            stored.LastWriteTimeUtcTicks = lastWriteTimeUtcTicks;
            stored.LastUsedUtcTicks = Clock().Ticks;
            lock (_sync)
            {
                EnsureLoadedLocked();
                _entries[stored.Path] = stored;
                TrimLocked();
                MarkDirtyLocked();
            }
        }

        /// <summary>Writes the pending changes now. The app calls this when it exits.</summary>
        public void Flush()
        {
            try { _saveTimer.Change(Timeout.Infinite, Timeout.Infinite); }
            catch (ObjectDisposedException) { }
            Save();
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (_disposed) return;
                _disposed = true;
            }
            _saveTimer.Dispose();
            Save();
        }

        private static string Normalize(string path) => Path.GetFullPath(path);

        private void EnsureLoadedLocked()
        {
            if (_entries != null) return;
            _entries = new Dictionary<string, RecordInfoIndexEntry>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FilePath)) return;
                var file = JsonConvert.DeserializeObject<IndexFile>(File.ReadAllText(FilePath));
                if (file == null || file.Version != FormatVersion || file.Entries == null)
                {
                    _logger?.LogInformation("The record index {path} has another format and is rebuilt", FilePath);
                    return;
                }

                long cutoff = (Clock() - Retention).Ticks;
                foreach (var entry in file.Entries)
                {
                    if (entry?.Path == null || entry.LastUsedUtcTicks < cutoff) continue;
                    _entries[entry.Path] = entry;
                }
                if (_entries.Count != file.Entries.Count)
                    MarkDirtyLocked();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "The record index {path} could not be read and is rebuilt", FilePath);
                _entries.Clear();
            }
        }

        private void TrimLocked()
        {
            if (_entries.Count <= Capacity) return;
            // Drop the least recently used entries, a batch at a time.
            int surplus = _entries.Count - Capacity + Math.Max(1, Capacity / 20);
            foreach (string key in _entries.OrderBy(pair => pair.Value.LastUsedUtcTicks)
                .Take(surplus).Select(pair => pair.Key).ToArray())
            {
                _entries.Remove(key);
            }
        }

        private void MarkDirtyLocked()
        {
            _dirty = true;
            if (_disposed) return;
            var now = Clock();
            _firstChangeUtc ??= now;
            var due = now + SaveDelay;
            var latest = _firstChangeUtc.Value + MaxSaveDelay;
            if (due > latest) due = latest;
            var delay = due - now;
            if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
            try { _saveTimer.Change(delay, Timeout.InfiniteTimeSpan); }
            catch (ObjectDisposedException) { }
        }

        private void Save()
        {
            if (FilePath == null) return;
            lock (_saveSync)
            {
                RecordInfoIndexEntry[] entries;
                lock (_sync)
                {
                    if (!_dirty || _entries == null) return;
                    _dirty = false;
                    _firstChangeUtc = null;
                    entries = _entries.Values.ToArray();
                }

                try
                {
                    string directory = Path.GetDirectoryName(FilePath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    string temporary = Path.Combine(directory ?? string.Empty,
                        $".{Path.GetFileName(FilePath)}.{Guid.NewGuid():N}.tmp");
                    try
                    {
                        using (var writer = new StreamWriter(temporary))
                        using (var json = new JsonTextWriter(writer))
                        {
                            new JsonSerializer { NullValueHandling = NullValueHandling.Ignore }
                                .Serialize(json, new IndexFile { Version = FormatVersion, Entries = entries.ToList() });
                        }
                        File.Move(temporary, FilePath, true);
                    }
                    finally
                    {
                        if (File.Exists(temporary)) File.Delete(temporary);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "The record index {path} could not be written", FilePath);
                    lock (_sync) _dirty = true;
                }
            }
        }
    }
}
