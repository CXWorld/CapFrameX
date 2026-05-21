using CapFrameX.Contracts.Data;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Test.Mocks
{
    /// <summary>
    /// Mock implementation of IRecordManager for unit testing.
    /// Stores JSON files in memory instead of disk for fast, isolated tests.
    /// </summary>
    public class MockRecordManager : IRecordManager
    {
        private readonly ConcurrentDictionary<string, string> _inMemoryFiles;
        private readonly ConcurrentDictionary<string, MockFileRecordInfo> _recordInfoCache;
        private int _fileCounter;

        /// <summary>
        /// Access to the in-memory file storage for test verification.
        /// Key is the virtual file path, value is the JSON content.
        /// </summary>
        public IReadOnlyDictionary<string, string> InMemoryFiles => _inMemoryFiles;

        /// <summary>
        /// Number of files currently stored in memory.
        /// </summary>
        public int FileCount => _inMemoryFiles.Count;

        public MockRecordManager()
        {
            _inMemoryFiles = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            _recordInfoCache = new ConcurrentDictionary<string, MockFileRecordInfo>(StringComparer.OrdinalIgnoreCase);
            _fileCounter = 0;
        }

        /// <summary>
        /// Clears all in-memory files and caches.
        /// </summary>
        public void Clear()
        {
            _inMemoryFiles.Clear();
            _recordInfoCache.Clear();
            _fileCounter = 0;
        }

        /// <summary>
        /// Pre-populates a session in memory for testing load operations.
        /// </summary>
        /// <returns>The virtual file path that can be used with LoadData().</returns>
        public string AddSession(ISession session, string fileName = null)
        {
            fileName = fileName ?? GenerateFileName(session.Info?.ProcessName ?? "TestProcess");
            var json = SerializeSession(session);
            _inMemoryFiles[fileName] = json;
            return fileName;
        }

        /// <summary>
        /// Pre-populates a session from raw JSON for testing.
        /// </summary>
        public string AddSessionJson(string json, string fileName = null)
        {
            fileName = fileName ?? $"TestSession_{++_fileCounter}.json";
            _inMemoryFiles[fileName] = json;
            return fileName;
        }

        /// <summary>
        /// Gets the raw JSON content for a stored session.
        /// </summary>
        public string GetSessionJson(string filePath)
        {
            return _inMemoryFiles.TryGetValue(filePath, out var json) ? json : null;
        }

        /// <summary>
        /// Checks if a file exists in the in-memory storage.
        /// </summary>
        public bool FileExists(string filePath)
        {
            return _inMemoryFiles.ContainsKey(filePath);
        }

        #region IRecordManager Implementation

        public Task<IFileRecordInfo> GetFileRecordInfo(FileInfo fileInfo)
        {
            var path = fileInfo.FullName;

            if (_recordInfoCache.TryGetValue(path, out var cached))
            {
                return Task.FromResult<IFileRecordInfo>(cached);
            }

            if (_inMemoryFiles.TryGetValue(path, out var json))
            {
                var session = DeserializeSession(json);
                var recordInfo = new MockFileRecordInfo(fileInfo, session);
                _recordInfoCache[path] = recordInfo;
                return Task.FromResult<IFileRecordInfo>(recordInfo);
            }

            // Return a minimal valid record info for non-existent files
            var emptyInfo = new MockFileRecordInfo(fileInfo);
            return Task.FromResult<IFileRecordInfo>(emptyInfo);
        }

        public Task<bool> SaveSessionRunsToFile(IEnumerable<ISessionRun> runs, string processName,
            string comment, string recordDirectory, List<ISessionInfo> hwInfo)
        {
            try
            {
                var runsList = runs.ToList();
                var session = new Session
                {
                    Hash = ComputeSessionHash(runsList),
                    Info = CreateSessionInfo(processName, comment, hwInfo),
                    Runs = new List<ISessionRun>(runsList)
                };

                var fileName = GenerateFileName(processName);
                var fullPath = Path.Combine(recordDirectory ?? "Records", fileName);
                var json = SerializeSession(session);

                _inMemoryFiles[fullPath] = json;

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public void UpdateCustomData(IFileRecordInfo recordInfo, string customCpuInfo,
            string customGpuInfo, string customRamInfo, string customGameName, string customComment)
        {
            var path = recordInfo.FullPath;

            if (_inMemoryFiles.TryGetValue(path, out var json))
            {
                var session = DeserializeSession(json);

                if (session.Info != null)
                {
                    if (!string.IsNullOrEmpty(customCpuInfo))
                        session.Info.Processor = customCpuInfo;
                    if (!string.IsNullOrEmpty(customGpuInfo))
                        session.Info.GPU = customGpuInfo;
                    if (!string.IsNullOrEmpty(customRamInfo))
                        session.Info.SystemRam = customRamInfo;
                    if (!string.IsNullOrEmpty(customGameName))
                        session.Info.GameName = customGameName;
                    if (!string.IsNullOrEmpty(customComment))
                        session.Info.Comment = customComment;
                }

                _inMemoryFiles[path] = SerializeSession(session);

                // Update cache
                if (_recordInfoCache.TryGetValue(path, out var cached))
                {
                    cached.UpdateFromSession(session);
                }
            }
        }

        public List<ISystemInfoEntry> GetSystemInfos(IFileRecordInfo recordInfo)
        {
            var entries = new List<ISystemInfoEntry>();

            if (recordInfo == null) return entries;

            AddSystemInfoEntry(entries, "Game", recordInfo.GameName);
            AddSystemInfoEntry(entries, "Process", recordInfo.ProcessName);
            AddSystemInfoEntry(entries, "CPU", recordInfo.ProcessorName);
            AddSystemInfoEntry(entries, "GPU", recordInfo.GraphicCardName);
            AddSystemInfoEntry(entries, "RAM", recordInfo.SystemRamInfo);
            AddSystemInfoEntry(entries, "Motherboard", recordInfo.MotherboardName);
            AddSystemInfoEntry(entries, "OS", recordInfo.OsVersion);
            AddSystemInfoEntry(entries, "GPU Driver", recordInfo.GPUDriverVersion);
            AddSystemInfoEntry(entries, "API", recordInfo.ApiInfo);
            AddSystemInfoEntry(entries, "Resolution", recordInfo.Resolution);
            AddSystemInfoEntry(entries, "Comment", recordInfo.Comment);

            return entries;
        }

        public ISession LoadData(string file)
        {
            if (_inMemoryFiles.TryGetValue(file, out var json))
            {
                return DeserializeSession(json);
            }

            // Return empty session for non-existent files
            return new Session();
        }

        public ISessionRun ConvertPresentDataLinesToSessionRun(IEnumerable<string> presentLines)
        {
            var lines = presentLines.ToList();
            if (lines.Count == 0)
            {
                return new SessionRun
                {
                    CaptureData = new SessionCaptureData(0),
                    SensorData2 = new SessionSensorData2()
                };
            }

            // Skip header line if present
            var dataLines = lines[0].StartsWith("Application") || lines[0].Contains(",")
                ? lines.Skip(1).ToList()
                : lines;

            var frameCount = dataLines.Count;
            var captureData = new SessionCaptureData(frameCount);

            double cumulativeTime = 0;
            for (int i = 0; i < frameCount; i++)
            {
                var parts = dataLines[i].Split(',');
                if (parts.Length >= 11)
                {
                    // Parse frame time (MsBetweenPresents is typically at index 10)
                    if (double.TryParse(parts[10], out var frameTime))
                    {
                        captureData.MsBetweenPresents[i] = frameTime;
                        cumulativeTime += frameTime / 1000.0;
                        captureData.TimeInSeconds[i] = cumulativeTime;
                    }

                    // Parse display change time (typically at index 11)
                    if (parts.Length > 11 && double.TryParse(parts[11], out var displayChange))
                    {
                        captureData.MsBetweenDisplayChange[i] = displayChange;
                    }
                }
            }

            // Normalize time to start at 0
            if (frameCount > 0)
            {
                var startTime = captureData.TimeInSeconds[0];
                for (int i = 0; i < frameCount; i++)
                {
                    captureData.TimeInSeconds[i] -= startTime;
                }
            }

            return new SessionRun
            {
                Hash = ComputeHash(string.Join("\n", dataLines)),
                CaptureData = captureData,
                SensorData2 = new SessionSensorData2()
            };
        }

        public Task SavePresentmonRawToFile(IEnumerable<string> lines, string process, string recordDirectory)
        {
            var fileName = $"CapFrameX-{process}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
            var fullPath = Path.Combine(recordDirectory ?? "Records", fileName);

            var content = string.Join(Environment.NewLine, lines);
            _inMemoryFiles[fullPath] = content;

            return Task.CompletedTask;
        }

        public void NormalizeStartTimesOfSessionRuns(IEnumerable<ISessionRun> sessionRuns)
        {
            double accumulatedTime = 0;

            foreach (var run in sessionRuns)
            {
                if (run.CaptureData?.TimeInSeconds == null || run.CaptureData.TimeInSeconds.Length == 0)
                    continue;

                var times = run.CaptureData.TimeInSeconds;
                var startTime = times[0];

                // Normalize this run's times and offset by accumulated time
                for (int i = 0; i < times.Length; i++)
                {
                    times[i] = times[i] - startTime + accumulatedTime;
                }

                // Update accumulated time for next run
                if (times.Length > 0)
                {
                    accumulatedTime = times[times.Length - 1];
                    if (run.CaptureData.MsBetweenPresents?.Length > 0)
                    {
                        accumulatedTime += run.CaptureData.MsBetweenPresents[times.Length - 1] / 1000.0;
                    }
                }
            }
        }

        #endregion

        #region Helper Methods

        private string GenerateFileName(string processName)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            return $"CapFrameX-{processName}-{timestamp}-{++_fileCounter}.json";
        }

        private string SerializeSession(ISession session)
        {
            var settings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
            return JsonConvert.SerializeObject(session, settings);
        }

        private ISession DeserializeSession(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<Session>(json);
            }
            catch
            {
                return new Session();
            }
        }

        private ISessionInfo CreateSessionInfo(string processName, string comment, List<ISessionInfo> hwInfo)
        {
            var info = new SessionInfo
            {
                Id = Guid.NewGuid(),
                CreationDate = DateTime.UtcNow,
                ProcessName = processName,
                GameName = processName,
                Comment = comment,
                AppVersion = new Version(1, 7, 7)
            };

            // Merge hardware info if provided
            if (hwInfo?.Count > 0)
            {
                var first = hwInfo[0];
                info.Processor = first.Processor;
                info.GPU = first.GPU;
                info.SystemRam = first.SystemRam;
                info.Motherboard = first.Motherboard;
                info.OS = first.OS;
                info.GPUDriverVersion = first.GPUDriverVersion;
                info.ApiInfo = first.ApiInfo;
            }

            return info;
        }

        private string ComputeSessionHash(IList<ISessionRun> runs)
        {
            var combined = string.Join(",", runs.Select(r => r.Hash ?? ""));
            return ComputeHash(combined);
        }

        private string ComputeHash(string input)
        {
            using (var sha1 = SHA1.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(input);
                var hash = sha1.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private void AddSystemInfoEntry(List<ISystemInfoEntry> entries, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                entries.Add(new MockSystemInfoEntry { Key = key, Value = value });
            }
        }

        #endregion
    }

    /// <summary>
    /// Mock implementation of IFileRecordInfo for in-memory testing.
    /// </summary>
    public class MockFileRecordInfo : IFileRecordInfo
    {
        private string _gameName;
        private string _processName;
        private string _processorName;
        private string _systemRamInfo;
        private string _graphicCardName;
        private string _comment;

        public event PropertyChangedEventHandler PropertyChanged;

        public string GameName
        {
            get => _gameName;
            set { _gameName = value; OnPropertyChanged(nameof(GameName)); }
        }

        public string ProcessName
        {
            get => _processName;
            set { _processName = value; OnPropertyChanged(nameof(ProcessName)); }
        }

        public string ProcessorName
        {
            get => _processorName;
            set { _processorName = value; OnPropertyChanged(nameof(ProcessorName)); }
        }

        public string SystemRamInfo
        {
            get => _systemRamInfo;
            set { _systemRamInfo = value; OnPropertyChanged(nameof(SystemRamInfo)); }
        }

        public string GraphicCardName
        {
            get => _graphicCardName;
            set { _graphicCardName = value; OnPropertyChanged(nameof(GraphicCardName)); }
        }

        public string Comment
        {
            get => _comment;
            set { _comment = value; OnPropertyChanged(nameof(Comment)); }
        }

        public string CreationDate { get; private set; }
        public string CreationTime { get; private set; }
        public double RecordTime { get; private set; }
        public string FullPath { get; private set; }
        public FileInfo FileInfo { get; private set; }
        public string CombinedInfo => $"{GameName} {ProcessName} {ProcessorName} {GraphicCardName}";
        public string MotherboardName { get; private set; }
        public string OsVersion { get; private set; }
        public string BaseDriverVersion { get; private set; }
        public string DriverPackage { get; private set; }
        public string NumberGPUs { get; private set; }
        public string GPUCoreClock { get; private set; }
        public string GPUMemoryClock { get; private set; }
        public string GPUMemory { get; private set; }
        public string GPUDriverVersion { get; private set; }
        public string IsAggregated { get; private set; }
        public bool IsValid { get; private set; }
        public bool HasInfoHeader { get; set; }
        public string Id { get; private set; }
        public string Hash { get; private set; }
        public string ApiInfo { get; private set; }
        public string ResizableBar { get; private set; }
        public string WinGameMode { get; private set; }
        public string HAGS { get; private set; }
        public string PresentationMode { get; private set; }
        public string Resolution { get; private set; }

        public MockFileRecordInfo(FileInfo fileInfo, ISession session = null)
        {
            FileInfo = fileInfo;
            FullPath = fileInfo?.FullName ?? "";
            CreationDate = DateTime.Now.ToString("yyyy-MM-dd");
            CreationTime = DateTime.Now.ToString("HH:mm:ss");
            IsValid = true;
            HasInfoHeader = session != null;

            if (session != null)
            {
                UpdateFromSession(session);
            }
        }

        public void UpdateFromSession(ISession session)
        {
            if (session?.Info == null) return;

            var info = session.Info;
            _gameName = info.GameName;
            _processName = info.ProcessName;
            _processorName = info.Processor;
            _graphicCardName = info.GPU;
            _systemRamInfo = info.SystemRam;
            _comment = info.Comment;

            MotherboardName = info.Motherboard;
            OsVersion = info.OS;
            GPUDriverVersion = info.GPUDriverVersion;
            BaseDriverVersion = info.BaseDriverVersion;
            DriverPackage = info.DriverPackage;
            NumberGPUs = info.GPUCount;
            GPUCoreClock = info.GpuCoreClock;
            GPUMemoryClock = info.GpuMemoryClock;
            ApiInfo = info.ApiInfo;
            ResizableBar = info.ResizableBar;
            WinGameMode = info.WinGameMode;
            HAGS = info.HAGS;
            PresentationMode = info.PresentationMode;
            Resolution = info.ResolutionInfo;

            Id = info.Id.ToString();
            Hash = session.Hash;
            CreationDate = info.CreationDate.ToString("yyyy-MM-dd");
            CreationTime = info.CreationDate.ToString("HH:mm:ss");

            // Calculate record time from runs
            if (session.Runs?.Count > 0)
            {
                RecordTime = session.Runs
                    .Where(r => r.CaptureData?.TimeInSeconds?.Length > 0)
                    .Sum(r => r.CaptureData.TimeInSeconds.Last());
            }

            IsAggregated = session.Runs?.Count > 1 ? "Yes" : "No";
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Mock implementation of ISystemInfoEntry.
    /// </summary>
    public class MockSystemInfoEntry : ISystemInfoEntry
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string Letter => Key?.FirstOrDefault().ToString() ?? "";
        public string IsSelected { get; set; }
    }

    /// <summary>
    /// Helper class to create test sessions with realistic data.
    /// </summary>
    public static class MockSessionFactory
    {
        /// <summary>
        /// Creates a test session with specified frame data.
        /// </summary>
        public static ISession CreateSession(
            string processName,
            double[] frameTimes,
            string cpuName = "Test CPU",
            string gpuName = "Test GPU",
            string comment = null)
        {
            var captureData = new SessionCaptureData(frameTimes.Length);
            double cumulativeTime = 0;

            for (int i = 0; i < frameTimes.Length; i++)
            {
                captureData.MsBetweenPresents[i] = frameTimes[i];
                captureData.MsBetweenDisplayChange[i] = frameTimes[i] + 0.5;
                captureData.TimeInSeconds[i] = cumulativeTime;
                cumulativeTime += frameTimes[i] / 1000.0;
            }

            var run = new SessionRun
            {
                Hash = Guid.NewGuid().ToString("N"),
                PresentMonRuntime = "D3D11",
                CaptureData = captureData,
                SensorData2 = new SessionSensorData2()
            };

            var info = new SessionInfo
            {
                Id = Guid.NewGuid(),
                CreationDate = DateTime.UtcNow,
                ProcessName = processName,
                GameName = processName,
                Processor = cpuName,
                GPU = gpuName,
                Comment = comment,
                AppVersion = new Version(1, 7, 7),
                SystemRam = "32 GB",
                OS = "Windows 11",
                Motherboard = "Test Motherboard"
            };

            return new Session
            {
                Hash = run.Hash,
                Info = info,
                Runs = new List<ISessionRun> { run }
            };
        }

        /// <summary>
        /// Creates a session with stable 60 FPS frame data.
        /// </summary>
        public static ISession CreateStable60FpsSession(string processName, int frameCount = 1000)
        {
            var random = new Random(42);
            var frameTimes = new double[frameCount];

            for (int i = 0; i < frameCount; i++)
            {
                // ~16.67ms with small variance
                frameTimes[i] = 16.67 + (random.NextDouble() - 0.5) * 1.0;
            }

            return CreateSession(processName, frameTimes);
        }

        /// <summary>
        /// Creates a session with stuttering frame data.
        /// </summary>
        public static ISession CreateStutteringSession(string processName, int frameCount = 1000)
        {
            var random = new Random(42);
            var frameTimes = new double[frameCount];

            for (int i = 0; i < frameCount; i++)
            {
                // Every ~30 frames, add a stutter
                bool isStutter = random.NextDouble() < 0.033;
                frameTimes[i] = isStutter
                    ? 50.0 + random.NextDouble() * 100.0
                    : 16.67 + (random.NextDouble() - 0.5) * 2.0;
            }

            return CreateSession(processName, frameTimes);
        }
    }
}
