using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.RTSS;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Data.Session.Converters;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Prism.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.Data
{
    public class RecordManager : IRecordManager
    {
        private const string IGNOREFLAGMARKER = "//Ignore=true";
        private const int SESSION_CACHE_CAPACITY = 16;
        private const int RECORD_INFO_CACHE_CAPACITY = 4096;
        private sealed class SessionCacheEntry
        {
            public long Length;
            public long LastWriteTimeUtcTicks;
            // Session models are mutable. A weak reference avoids extending their lifetime and
            // lets memory pressure discard large captures while active views can still reuse them.
            public WeakReference<ISession> Session;
            public Lazy<ISession> LoadingSession;
            public long LastAccess;
        }

        private sealed class RecordInfoCacheEntry
        {
            public long Length;
            public long LastWriteTimeUtcTicks;
            public IFileRecordInfo RecordInfo;
            public long LastAccess;
        }

        private readonly TimeSpan _fileAccessIntervalTimespan = TimeSpan.FromMilliseconds(200);
        private readonly int _fileAccessIntervalRetryLimit = 50;
        private readonly ILogger<RecordManager> _logger;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IRecordDirectoryObserver _recordObserver;
        private readonly IAppVersionProvider _appVersionProvider;
        private readonly ISensorService _sensorService;
        private readonly ISystemInfo _systemInfo;
        private readonly ProcessList _processList;
        private readonly IRTSSService _rTSSService;
        private readonly IEventAggregator _eventAggregator;
        private readonly ICaptureService _captureService;
        private readonly IHookOverlayStatusService _hookOverlayStatusService;
        private readonly object _sessionCacheSync = new object();
        private readonly Dictionary<string, SessionCacheEntry> _sessionCache =
            new Dictionary<string, SessionCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, RecordInfoCacheEntry> _recordInfoCache =
            new Dictionary<string, RecordInfoCacheEntry>(StringComparer.OrdinalIgnoreCase);
        private long _sessionCacheAccessSequence;
        private long _recordInfoCacheAccessSequence;
        private PubSubEvent<ViewMessages.UpdateSystemInfo> _updateSystemInfoEvent;

        public RecordManager(ILogger<RecordManager> logger,
            IAppConfiguration appConfiguration,
            IRecordDirectoryObserver recordObserver,
            IAppVersionProvider appVersionProvider,
            ISensorService sensorService,
            ISystemInfo systemInfo,
            ProcessList processList,
            IRTSSService rTSSService,
            IEventAggregator eventAggregator,
            ICaptureService captureService,
            IHookOverlayStatusService hookOverlayStatusService)
        {
            _hookOverlayStatusService = hookOverlayStatusService;
            _logger = logger;
            _appConfiguration = appConfiguration;
            _recordObserver = recordObserver;
            _appVersionProvider = appVersionProvider;
            _sensorService = sensorService;
            _systemInfo = systemInfo;
            _processList = processList;
            _rTSSService = rTSSService;
            _eventAggregator = eventAggregator;
            _captureService = captureService;
            _updateSystemInfoEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSystemInfo>>();
        }

        public void UpdateCustomData(IFileRecordInfo recordInfo,
            string customCpuInfo, string customGpuInfo,
            string customRamInfo, string customMainboardInfo,
            string customGameName, string customComment,
            string customResolution = null)
        {
            if (recordInfo == null) return;

            customCpuInfo = customCpuInfo ?? string.Empty;
            customGpuInfo = customGpuInfo ?? string.Empty;
            customRamInfo = customRamInfo ?? string.Empty;
            customMainboardInfo = customMainboardInfo ?? string.Empty;
            customGameName = customGameName ?? string.Empty;
            customComment = customComment ?? string.Empty;

            try
            {
                if (recordInfo.FileInfo.Extension == ".json")
                {
                    var session = LoadSessionFromJSON(recordInfo.FileInfo);
                    session.Info.Processor = customCpuInfo;
                    session.Info.GPU = customGpuInfo;
                    session.Info.SystemRam = customRamInfo;
                    session.Info.Motherboard = customMainboardInfo;
                    session.Info.GameName = customGameName;
                    session.Info.Comment = customComment;
                    // null = leave unchanged; CSV records have no resolution header,
                    // so the resolution is only persisted for JSON sessions
                    if (customResolution != null)
                        session.Info.ResolutionInfo = customResolution;

                    SaveSessionToFile(recordInfo.FileInfo.FullName, session);

                }
                else if (recordInfo.FileInfo.Extension == ".csv")
                {
                    string[] lines = File.ReadAllLines(recordInfo.FileInfo.FullName);

                    if (recordInfo.HasInfoHeader)
                    {
                        // Processor
                        int processorNameHeaderIndex = GetHeaderIndex(lines, "Processor");
                        lines[processorNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}Processor{FileRecordInfo.INFO_SEPERATOR}{customCpuInfo}";

                        // GPU
                        int graphicCardNameHeaderIndex = GetHeaderIndex(lines, "GPU");
                        lines[graphicCardNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}GPU{FileRecordInfo.INFO_SEPERATOR}{customGpuInfo}";

                        // RAM
                        int systemRamNameHeaderIndex = GetHeaderIndex(lines, "System RAM");
                        lines[systemRamNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}System RAM{FileRecordInfo.INFO_SEPERATOR}{customRamInfo}";

                        // Motherboard — records written before the mainboard became editable may
                        // not carry the header at all, so skip it instead of losing the whole edit
                        int motherboardNameHeaderIndex = FindHeaderIndex(lines, "Motherboard");
                        if (motherboardNameHeaderIndex > -1)
                            lines[motherboardNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}Motherboard{FileRecordInfo.INFO_SEPERATOR}{customMainboardInfo}";

                        // GameName
                        int gameNameHeaderIndex = GetHeaderIndex(lines, "GameName");
                        lines[gameNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}GameName{FileRecordInfo.INFO_SEPERATOR}{customGameName}";

                        // Comment
                        int commentNameHeaderIndex = GetHeaderIndex(lines, "Comment");
                        lines[commentNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}Comment{FileRecordInfo.INFO_SEPERATOR}{customComment}";

                        File.WriteAllLines(recordInfo.FullPath, lines);
                        InvalidateSessionCache(recordInfo.FullPath);
                    }
                    else
                    {
                        // Create header
                        var headerLines = new List<string>()
                        {
                            $"{FileRecordInfo.HEADER_MARKER}GameName{FileRecordInfo.INFO_SEPERATOR}{customGameName}",
                            $"{FileRecordInfo.HEADER_MARKER}ProcessName{FileRecordInfo.INFO_SEPERATOR}{recordInfo.ProcessName}",
                            $"{FileRecordInfo.HEADER_MARKER}CreationDate{FileRecordInfo.INFO_SEPERATOR}{recordInfo.CreationDate}",
                            $"{FileRecordInfo.HEADER_MARKER}CreationTime{FileRecordInfo.INFO_SEPERATOR}{recordInfo.CreationTime}",
                            $"{FileRecordInfo.HEADER_MARKER}Motherboard{FileRecordInfo.INFO_SEPERATOR}{customMainboardInfo}",
                            $"{FileRecordInfo.HEADER_MARKER}OS{FileRecordInfo.INFO_SEPERATOR}{recordInfo.OsVersion}",
                            $"{FileRecordInfo.HEADER_MARKER}Processor{FileRecordInfo.INFO_SEPERATOR}{customCpuInfo}",
                            $"{FileRecordInfo.HEADER_MARKER}System RAM{FileRecordInfo.INFO_SEPERATOR}{customRamInfo}",
                            $"{FileRecordInfo.HEADER_MARKER}Base Driver Version{FileRecordInfo.INFO_SEPERATOR}{recordInfo.BaseDriverVersion}",
                            $"{FileRecordInfo.HEADER_MARKER}Driver Package{FileRecordInfo.INFO_SEPERATOR}{recordInfo.DriverPackage}",
                            $"{FileRecordInfo.HEADER_MARKER}GPU{FileRecordInfo.INFO_SEPERATOR}{customGpuInfo}",
                            $"{FileRecordInfo.HEADER_MARKER}GPU #{FileRecordInfo.INFO_SEPERATOR}{recordInfo.NumberGPUs}",
                            $"{FileRecordInfo.HEADER_MARKER}GPU Core Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUCoreClock}",
                            $"{FileRecordInfo.HEADER_MARKER}GPU Memory Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUMemoryClock}",
                            $"{FileRecordInfo.HEADER_MARKER}GPU Memory (MB){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUMemory}",
                            $"{FileRecordInfo.HEADER_MARKER}Comment{FileRecordInfo.INFO_SEPERATOR}{customComment}"
                        };

                        recordInfo.HasInfoHeader = true;
                        File.WriteAllLines(recordInfo.FullPath, headerLines.Concat(lines));
                        InvalidateSessionCache(recordInfo.FullPath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing Lines");
            }
        }

        private int GetHeaderIndex(string[] lines, string headerEntry)
        {
            int index = 0;
            while (!lines[index].Contains(headerEntry))
            {
                index++;
            }
            return index;
        }

        /// <summary>
        /// Like <see cref="GetHeaderIndex(string[], string)"/>, but returns -1 for an entry the
        /// header does not contain instead of running off the end of the file.
        /// </summary>
        private int FindHeaderIndex(string[] lines, string headerEntry)
        {
            for (int index = 0; index < lines.Length; index++)
            {
                if (!lines[index].StartsWith(FileRecordInfo.HEADER_MARKER))
                    break;

                if (lines[index].Contains(headerEntry))
                    return index;
            }

            return -1;
        }

        public List<ISystemInfoEntry> GetSystemInfos(IFileRecordInfo recordInfo)
        {
            _logger.LogInformation("Getting Systeminfos");
            var systemInfos = new List<ISystemInfoEntry>();

            if (!string.IsNullOrWhiteSpace(recordInfo.CreationDate))
                systemInfos.Add(new SystemInfoEntry() { Key = "Creation Date & Time", Value = recordInfo.CreationDate + "  |  " + recordInfo.CreationTime });
            if (!string.IsNullOrWhiteSpace(recordInfo.Comment))
                systemInfos.Add(new SystemInfoEntry() { Key = "Comment", Value = recordInfo.Comment });
            if (!string.IsNullOrWhiteSpace(recordInfo.DeviceName))
                systemInfos.Add(new SystemInfoEntry() { Key = "Device Name", Value = recordInfo.DeviceName });
            if (!string.IsNullOrWhiteSpace(recordInfo.ProcessorName))
                systemInfos.Add(new SystemInfoEntry() { Key = "Processor", Value = recordInfo.ProcessorName });
            if (!string.IsNullOrWhiteSpace(recordInfo.SystemRamInfo))
                systemInfos.Add(new SystemInfoEntry() { Key = "System RAM", Value = recordInfo.SystemRamInfo });
            if (!string.IsNullOrWhiteSpace(recordInfo.GraphicCardName))
                systemInfos.Add(new SystemInfoEntry() { Key = "Graphics Card", Value = recordInfo.GraphicCardName });
            if (!string.IsNullOrWhiteSpace(recordInfo.MotherboardName))
                systemInfos.Add(new SystemInfoEntry() { Key = "Motherboard", Value = recordInfo.MotherboardName });
            if (!string.IsNullOrWhiteSpace(recordInfo.OsVersion))
                systemInfos.Add(new SystemInfoEntry() { Key = "OS Version", Value = recordInfo.OsVersion });
            if (!string.IsNullOrWhiteSpace(recordInfo.NumberGPUs))
                systemInfos.Add(new SystemInfoEntry() { Key = "GPU #", Value = recordInfo.NumberGPUs });
            if (!string.IsNullOrWhiteSpace(recordInfo.GPUCoreClock))
                systemInfos.Add(new SystemInfoEntry() { Key = "GPU Core Clock (MHz)", Value = recordInfo.GPUCoreClock });
            if (!string.IsNullOrWhiteSpace(recordInfo.GPUMemoryClock))
                systemInfos.Add(new SystemInfoEntry() { Key = "GPU Memory Clock (MHz)", Value = recordInfo.GPUMemoryClock });
            if (!string.IsNullOrWhiteSpace(recordInfo.GPUMemory))
                systemInfos.Add(new SystemInfoEntry() { Key = "GPU Memory (MB)", Value = recordInfo.GPUMemory });
            if (!string.IsNullOrWhiteSpace(recordInfo.BaseDriverVersion))
                systemInfos.Add(new SystemInfoEntry() { Key = "Base Driver Version", Value = recordInfo.BaseDriverVersion });
            if (!string.IsNullOrWhiteSpace(recordInfo.GPUDriverVersion))
                systemInfos.Add(new SystemInfoEntry() { Key = "GPU Driver Version", Value = recordInfo.GPUDriverVersion });
            if (!string.IsNullOrWhiteSpace(recordInfo.DriverPackage))
                systemInfos.Add(new SystemInfoEntry() { Key = "Driver Package", Value = recordInfo.DriverPackage });
            if (!string.IsNullOrWhiteSpace(recordInfo.ApiInfo))
                systemInfos.Add(new SystemInfoEntry() { Key = "API", Value = recordInfo.ApiInfo });
            if (!string.IsNullOrWhiteSpace(recordInfo.ResizableBar))
                systemInfos.Add(new SystemInfoEntry() { Key = "Resizable BAR", Value = recordInfo.ResizableBar });
            if (!string.IsNullOrWhiteSpace(recordInfo.WinGameMode))
                systemInfos.Add(new SystemInfoEntry() { Key = "Windows Game Mode", Value = recordInfo.WinGameMode });
            if (!string.IsNullOrWhiteSpace(recordInfo.HAGS))
                systemInfos.Add(new SystemInfoEntry() { Key = "HAGS", Value = recordInfo.HAGS });
            if (!string.IsNullOrWhiteSpace(recordInfo.PresentationMode))
                systemInfos.Add(new SystemInfoEntry() { Key = "Presentation Mode", Value = recordInfo.PresentationMode });
            if (!string.IsNullOrWhiteSpace(recordInfo.Resolution))
                systemInfos.Add(new SystemInfoEntry() { Key = "Resolution", Value = recordInfo.Resolution });

            return systemInfos;
        }

        public ISession LoadData(string path)
        {
            _logger.LogDebug("Loading data from: {path}", path);
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            try
            {
                var normalizedPath = Path.GetFullPath(path);
                var fileInfo = new FileInfo(normalizedPath);

                if (!fileInfo.Exists || fileInfo.Length == 0)
                {
                    InvalidateSessionCache(normalizedPath);
                    return null;
                }

                if (fileInfo.Extension != ".json" && fileInfo.Extension != ".csv")
                {
                    return null;
                }

                long length = fileInfo.Length;
                long lastWriteTimeUtcTicks = fileInfo.LastWriteTimeUtc.Ticks;
                SessionCacheEntry cacheEntry;
                Lazy<ISession> loadingSession;

                lock (_sessionCacheSync)
                {
                    if (_sessionCache.TryGetValue(normalizedPath, out cacheEntry)
                        && cacheEntry.Length == length
                        && cacheEntry.LastWriteTimeUtcTicks == lastWriteTimeUtcTicks)
                    {
                        cacheEntry.LastAccess = ++_sessionCacheAccessSequence;
                        if (cacheEntry.Session != null
                            && cacheEntry.Session.TryGetTarget(out var cachedSession))
                        {
                            return cachedSession;
                        }

                        if (cacheEntry.LoadingSession == null)
                        {
                            cacheEntry.LoadingSession = CreateSessionLoader(normalizedPath);
                        }
                    }
                    else
                    {
                        cacheEntry = new SessionCacheEntry
                        {
                            Length = length,
                            LastWriteTimeUtcTicks = lastWriteTimeUtcTicks,
                            LastAccess = ++_sessionCacheAccessSequence,
                            LoadingSession = CreateSessionLoader(normalizedPath)
                        };
                        _sessionCache[normalizedPath] = cacheEntry;
                        TrimSessionCacheLocked(normalizedPath);
                    }

                    loadingSession = cacheEntry.LoadingSession;
                }

                ISession loadedSession;
                try
                {
                    // Lazy<T> makes concurrent requests for the same file share one parse.
                    loadedSession = loadingSession.Value;
                }
                catch
                {
                    RemoveSessionCacheEntry(normalizedPath, cacheEntry);
                    throw;
                }

                fileInfo.Refresh();
                bool fileUnchanged = fileInfo.Exists
                    && fileInfo.Length == length
                    && fileInfo.LastWriteTimeUtc.Ticks == lastWriteTimeUtcTicks;

                lock (_sessionCacheSync)
                {
                    if (_sessionCache.TryGetValue(normalizedPath, out var currentEntry)
                        && ReferenceEquals(currentEntry, cacheEntry))
                    {
                        currentEntry.LoadingSession = null;
                        if (loadedSession != null && fileUnchanged)
                        {
                            currentEntry.Session = new WeakReference<ISession>(loadedSession);
                            currentEntry.LastAccess = ++_sessionCacheAccessSequence;
                        }
                        else
                        {
                            _sessionCache.Remove(normalizedPath);
                        }
                    }
                }

                return loadedSession;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading {path}", path);
                return null;
            }
        }

        private Lazy<ISession> CreateSessionLoader(string normalizedPath)
        {
            return new Lazy<ISession>(() =>
            {
                var fileInfo = new FileInfo(normalizedPath);
                switch (fileInfo.Extension)
                {
                    case ".json":
                        return LoadSessionFromJSON(fileInfo);
                    case ".csv":
                        return LoadSessionFromCSV(fileInfo);
                    default:
                        return null;
                }
            }, LazyThreadSafetyMode.ExecutionAndPublication);
        }

        private void InvalidateSessionCache(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                string normalizedPath = Path.GetFullPath(path);
                lock (_sessionCacheSync)
                {
                    _sessionCache.Remove(normalizedPath);
                    _recordInfoCache.Remove(normalizedPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to invalidate cached session for {path}", path);
            }
        }

        private void RemoveSessionCacheEntry(string normalizedPath, SessionCacheEntry expectedEntry)
        {
            lock (_sessionCacheSync)
            {
                if (_sessionCache.TryGetValue(normalizedPath, out var currentEntry)
                    && ReferenceEquals(currentEntry, expectedEntry))
                {
                    _sessionCache.Remove(normalizedPath);
                }
            }
        }

        private void TrimSessionCacheLocked(string preservedPath)
        {
            while (_sessionCache.Count > SESSION_CACHE_CAPACITY)
            {
                var candidate = _sessionCache
                    .Where(pair => !string.Equals(pair.Key, preservedPath, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(pair => pair.Value.LastAccess)
                    .FirstOrDefault();

                if (candidate.Key == null)
                {
                    return;
                }

                _sessionCache.Remove(candidate.Key);
            }
        }

        private ISession LoadSessionFromJSON(FileInfo fileInfo)
        {
            try
            {
                using (var stream = new StreamReader(fileInfo.FullName))
                {
                    using (JsonReader jsonReader = new JsonTextReader(stream))
                    {
                        JsonSerializer serializer = new JsonSerializer();
                        var session = serializer.Deserialize<Session.Classes.Session>(jsonReader);

                        // Handle corrupt/incomplete JSON files (e.g., from disk full during write)
                        if (session?.Runs == null)
                        {
                            _logger.LogWarning("Failed to load session from {path}: file is corrupt or incomplete", fileInfo.FullName);
                            return null;
                        }

                        foreach (var sessionrun in session.Runs)
                        {
                            if (sessionrun.SensorData != null && sessionrun.SensorData2 == null)
                            {
                                SessionSensorDataConverter.ConvertToSensorData2(sessionrun);
                            }
                        }
                        return session;
                    }
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        private ISession LoadSessionFromCSV(FileInfo csvFile)
        {
            return LoadSessionFromCSV(csvFile, out _);
        }

        private ISession LoadSessionFromCSV(FileInfo csvFile, out IFileRecordInfo recordedFileInfo)
        {
            recordedFileInfo = null;

            // Exception: ignore Nv FrameView summary file
            if (csvFile.Name == "FrameView_Summary.csv")
                return null;

            // Ignore new PresentMon stats files
            if (csvFile.Name.Contains("-stats.csv"))
                return null;

            var lines = File.ReadAllLines(csvFile.FullName);
            if (lines.First().Equals(IGNOREFLAGMARKER))
            {
                throw new HasIgnoreFlagException();
            }

            IEnumerable<string> skippedLines = null;

            // MangoHud capture file
            if (lines.First().Contains("cpuscheduler") && lines.First().Contains("kernel"))
            {
                skippedLines = lines.Skip(2);

            }
            // Standard CSV with header
            else
            {
                skippedLines = lines.SkipWhile(line => line.Contains(FileRecordInfo.HEADER_MARKER));

                if (FileRecordInfo.IsMangoHudFile(skippedLines.First()))
                {
                    skippedLines = skippedLines.Skip(2);
                }
            }

            var sessionRun = ConvertPresentDataLinesToSessionRun(skippedLines);
            var sessionHash = sessionRun.Hash.GetSha1();
            recordedFileInfo = FileRecordInfo.Create(csvFile, sessionHash, lines);

            return new Session.Classes.Session()
            {
                Hash = sessionHash,
                Runs = new List<ISessionRun>() { sessionRun },
                Info = new SessionInfo()
                {
                    ProcessName = recordedFileInfo.ProcessName,
                    Processor = recordedFileInfo.ProcessorName,
                    GPU = recordedFileInfo.GraphicCardName,
                    BaseDriverVersion = recordedFileInfo.BaseDriverVersion,
                    GameName = recordedFileInfo.GameName,
                    Comment = recordedFileInfo.Comment,
                    Id = Guid.TryParse(recordedFileInfo.Id, out var guidId) ? guidId : Guid.NewGuid(),
                    OS = recordedFileInfo.OsVersion,
                    GpuCoreClock = recordedFileInfo.GPUCoreClock,
                    GPUCount = recordedFileInfo.NumberGPUs,
                    SystemRam = recordedFileInfo.SystemRamInfo,
                    DeviceName = recordedFileInfo.DeviceName,
                    Motherboard = recordedFileInfo.MotherboardName,
                    DriverPackage = recordedFileInfo.DriverPackage,
                    GpuMemoryClock = recordedFileInfo.GPUMemoryClock,
                    CreationDate = DateTime.TryParse(recordedFileInfo.CreationDate + "T" + recordedFileInfo.CreationTime, out var creationDate) ? creationDate : new DateTime(),
                    AppVersion = new Version(),
                    ApiInfo = recordedFileInfo.ApiInfo
                }
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetStringFromArray(string[] array, int index)
        {
            if (index < array.Length && index > -1)
            {
                return array[index] ?? string.Empty;
            }
            return string.Empty;
        }

        public async Task<IFileRecordInfo> GetFileRecordInfo(FileInfo fileInfo)
        {
            fileInfo.Refresh();
            if (TryGetCachedRecordInfo(fileInfo, out var cachedRecordInfo))
            {
                return cachedRecordInfo;
            }
            long expectedLength = fileInfo.Length;
            long expectedLastWriteTimeUtcTicks = fileInfo.LastWriteTimeUtc.Ticks;

            var recordInfo = await Observable.Timer(_fileAccessIntervalTimespan)
                .SelectMany(_ =>
                {
                    switch (fileInfo.Extension)
                    {
                        case ".csv":
                            var sessionFromCSV = LoadSessionFromCSV(fileInfo, out var recordInfoFromCSV);

                            if (sessionFromCSV == null)
                                return Observable.Empty<IFileRecordInfo>();

                            return Observable.Return(recordInfoFromCSV);
                        case ".json":
                            var recordInfoFromJSON = LoadRecordInfoFromJSON(fileInfo);
                            return recordInfoFromJSON == null
                                ? Observable.Empty<IFileRecordInfo>()
                                : Observable.Return(recordInfoFromJSON);
                        default:
                            return Observable.Empty<IFileRecordInfo>();
                    }
                })
                .Catch<IFileRecordInfo, Exception>(e =>
                {
                    if (e is IOException)
                    { // If e is IOException we will throw it again, so the retry will execute the function again
                        return Observable.Throw<IFileRecordInfo>(e);
                    }
                    else
                    {// otherwise, we return empty
                        if (!(e is HasIgnoreFlagException))
                        {
                            _logger.LogError(e, "Error Creating FileRecordInfo of {path}", fileInfo.FullName);
                        }
                        return Observable.Empty<IFileRecordInfo>();
                    }
                })
                .Retry(_fileAccessIntervalRetryLimit)
                .DefaultIfEmpty()
                .Do(fileRecordInfo =>
                {
                    if (fileRecordInfo != null)
                    {
                        bool usesProcessListName = fileRecordInfo.ProcessName == fileRecordInfo.GameName
                            || fileRecordInfo.GameName.IsNullOrEmpty();
                        if (fileRecordInfo is FileRecordInfo concreteRecordInfo)
                            concreteRecordInfo.UsesProcessListGameName = usesProcessListName;
                        if (usesProcessListName)
                            fileRecordInfo.GameName = GetGameNameFromProcessList(fileRecordInfo.ProcessName);
                    }
                });

            if (recordInfo != null)
            {
                StoreRecordInfo(fileInfo, recordInfo, expectedLength, expectedLastWriteTimeUtcTicks);
            }

            return recordInfo;
        }

        private IFileRecordInfo LoadRecordInfoFromJSON(FileInfo fileInfo)
        {
            using (var stream = new StreamReader(fileInfo.FullName))
            using (var jsonReader = new JsonTextReader(stream))
            {
                var serializer = new JsonSerializer();
                string hash = null;
                ISessionInfo sessionInfo = null;
                int runCount = 0;
                double recordTime = 0;

                while (jsonReader.Read())
                {
                    if (jsonReader.TokenType != JsonToken.PropertyName)
                    {
                        continue;
                    }

                    string propertyName = Convert.ToString(jsonReader.Value, CultureInfo.InvariantCulture);
                    if (!jsonReader.Read())
                    {
                        break;
                    }

                    if (string.Equals(propertyName, "Hash", StringComparison.OrdinalIgnoreCase))
                    {
                        hash = Convert.ToString(jsonReader.Value, CultureInfo.InvariantCulture);
                    }
                    else if (string.Equals(propertyName, "Info", StringComparison.OrdinalIgnoreCase))
                    {
                        sessionInfo = serializer.Deserialize<SessionInfo>(jsonReader);
                    }
                    else if (string.Equals(propertyName, "Runs", StringComparison.OrdinalIgnoreCase)
                        && jsonReader.TokenType == JsonToken.StartArray)
                    {
                        ReadRunMetadata(jsonReader, ref runCount, ref recordTime);
                    }
                    else
                    {
                        jsonReader.Skip();
                    }
                }

                return sessionInfo == null || runCount == 0
                    ? null
                    : FileRecordInfo.Create(fileInfo, sessionInfo, hash, runCount, recordTime);
            }
        }

        private static void ReadRunMetadata(JsonReader reader, ref int runCount, ref double recordTime)
        {
            int arrayDepth = reader.Depth;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndArray && reader.Depth == arrayDepth)
                {
                    return;
                }

                if (reader.TokenType == JsonToken.StartObject && reader.Depth == arrayDepth + 1)
                {
                    runCount++;
                    ReadSingleRunMetadata(reader, ref recordTime);
                }
            }
        }

        private static void ReadSingleRunMetadata(JsonReader reader, ref double recordTime)
        {
            int objectDepth = reader.Depth;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject && reader.Depth == objectDepth)
                {
                    return;
                }

                if (reader.TokenType != JsonToken.PropertyName)
                {
                    continue;
                }

                string propertyName = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
                if (!reader.Read())
                {
                    return;
                }

                if (string.Equals(propertyName, "CaptureData", StringComparison.OrdinalIgnoreCase)
                    && reader.TokenType == JsonToken.StartObject)
                {
                    ReadCaptureRecordTime(reader, ref recordTime);
                }
                else
                {
                    reader.Skip();
                }
            }
        }

        private static void ReadCaptureRecordTime(JsonReader reader, ref double recordTime)
        {
            int objectDepth = reader.Depth;
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.EndObject && reader.Depth == objectDepth)
                {
                    return;
                }

                if (reader.TokenType != JsonToken.PropertyName)
                {
                    continue;
                }

                string propertyName = Convert.ToString(reader.Value, CultureInfo.InvariantCulture);
                if (!reader.Read())
                {
                    return;
                }

                if (string.Equals(propertyName, "TimeInSeconds", StringComparison.OrdinalIgnoreCase)
                    && reader.TokenType == JsonToken.StartArray)
                {
                    int arrayDepth = reader.Depth;
                    while (reader.Read())
                    {
                        if (reader.TokenType == JsonToken.EndArray && reader.Depth == arrayDepth)
                        {
                            break;
                        }

                        if (reader.TokenType == JsonToken.Float || reader.TokenType == JsonToken.Integer)
                        {
                            recordTime = Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
                        }
                    }
                }
                else
                {
                    reader.Skip();
                }
            }
        }

        private bool TryGetCachedRecordInfo(FileInfo fileInfo, out IFileRecordInfo recordInfo)
        {
            recordInfo = null;
            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                return false;
            }

            string path = Path.GetFullPath(fileInfo.FullName);
            lock (_sessionCacheSync)
            {
                if (_recordInfoCache.TryGetValue(path, out var entry)
                    && entry.Length == fileInfo.Length
                    && entry.LastWriteTimeUtcTicks == fileInfo.LastWriteTimeUtc.Ticks)
                {
                    entry.LastAccess = ++_recordInfoCacheAccessSequence;
                    recordInfo = entry.RecordInfo;
                    if (recordInfo is FileRecordInfo concreteRecordInfo
                        && concreteRecordInfo.UsesProcessListGameName)
                    {
                        recordInfo.GameName = GetGameNameFromProcessList(recordInfo.ProcessName);
                    }
                    return recordInfo != null;
                }

                _recordInfoCache.Remove(path);
            }
            return false;
        }

        private void StoreRecordInfo(FileInfo fileInfo, IFileRecordInfo recordInfo,
            long expectedLength, long expectedLastWriteTimeUtcTicks)
        {
            fileInfo.Refresh();
            if (!fileInfo.Exists || fileInfo.Length != expectedLength
                || fileInfo.LastWriteTimeUtc.Ticks != expectedLastWriteTimeUtcTicks)
            {
                return;
            }

            string path = Path.GetFullPath(fileInfo.FullName);
            lock (_sessionCacheSync)
            {
                _recordInfoCache[path] = new RecordInfoCacheEntry
                {
                    Length = fileInfo.Length,
                    LastWriteTimeUtcTicks = fileInfo.LastWriteTimeUtc.Ticks,
                    RecordInfo = recordInfo,
                    LastAccess = ++_recordInfoCacheAccessSequence
                };

                while (_recordInfoCache.Count > RECORD_INFO_CACHE_CAPACITY)
                {
                    var oldest = _recordInfoCache.OrderBy(pair => pair.Value.LastAccess).First();
                    _recordInfoCache.Remove(oldest.Key);
                }
            }
        }

        public async Task SavePresentmonRawToFile(IEnumerable<string> lines, string process, string recordDirectory = null)
        {
            try
            {
                var filePath = await GetOutputFilename(process, recordDirectory);
                lines = new string[] { IGNOREFLAGMARKER, _captureService.ColumnHeader }.Concat(lines);
                File.WriteAllLines(filePath + ".csv", lines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving PresentMon raw file.");
            }
        }

        /// <summary>
        /// Render resolution of the captured game as "WxH", empty when no source can supply it.
        ///
        /// Two independent sources are needed because neither covers every overlay mode. RTSS
        /// fills dwResolutionX/Y only for processes it has hooked ITSELF — and while the in-game
        /// overlay renders, CapFrameX never even starts RTSS, which is what left this field (and
        /// ApiInfo) empty in every capture taken that way. The in-game hook knows the extent
        /// first hand: it is the swapchain it presents into, so it is also the more direct
        /// answer whenever it is available.
        /// </summary>
        private string GetRenderResolution(Process process)
        {
            if (process == null) return string.Empty;

            string fromHook = HookStatusFor(process)?.RenderResolution;
            if (!string.IsNullOrEmpty(fromHook)) return fromHook;

            return _rTSSService.GetResolution(process.Id) ?? string.Empty;
        }

        /// <summary>
        /// Graphics API of the captured game — "DX11", "DX12", "Vulkan" — or "unknown".
        ///
        /// Same problem as the render resolution: RTSS answers only for processes it hooked
        /// itself and is not running at all while the in-game overlay renders, which is why this
        /// field came out empty rather than as the documented "unknown". The in-game renderer
        /// proves the device type from the swapchain it presents into, and the Vulkan layer
        /// settles it by being loaded, so both spell it the way RTSS does.
        /// </summary>
        private string GetApiInfo(Process process, IEnumerable<ISessionRun> runs)
        {
            string fromHook = HookStatusFor(process)?.RenderApi;
            if (!string.IsNullOrEmpty(fromHook)) return fromHook;

            string fromRtss = process != null ? _rTSSService.GetApiInfo(process.Id) : null;
            // RTSS reports its own "unknown", but an absent RTSS yields an empty string — treat
            // both as no answer, otherwise the fallback below never runs.
            if (!string.IsNullOrWhiteSpace(fromRtss) && fromRtss != "unknown") return fromRtss;

            string fromPresentMon = runs.FirstOrDefault()?.PresentMonRuntime;
            return string.IsNullOrWhiteSpace(fromPresentMon) ? "unknown" : fromPresentMon;
        }

        // Bind the status to the captured process: a status left over from a previously selected
        // game would otherwise be written into this capture.
        private HookOverlayStatus HookStatusFor(Process process)
        {
            if (process == null) return null;
            var status = _hookOverlayStatusService?.Current;
            return status != null && status.ProcessId == process.Id ? status : null;
        }

        public async Task<bool> SaveSessionRunsToFile(IEnumerable<ISessionRun> runs, string processName, string comment, string recordDirectory = null, List<ISessionInfo> hwInfo = null)
        {
            var filePath = await GetOutputFilename(processName, recordDirectory);

            try
            {
                if (runs.Count() > 1)
                {
                    var json = JsonConvert.SerializeObject(runs);
                    runs = JsonConvert.DeserializeObject<SessionRun[]>(json);
                    NormalizeStartTimesOfSessionRuns(runs);
                }
                var csv = new StringBuilder();
                var datetime = DateTime.Now;


                // manage system info
                string deviceName = string.Empty;
                string cpuInfo = string.Empty;
                string gpuInfo = string.Empty;
                string ramInfo = string.Empty;
                string mbInfo = string.Empty;
                string osInfo = string.Empty;
                string gpuDriverInfo = string.Empty;
                string apiInfo = string.Empty;
                string resizableBar = string.Empty;
                string winGameMode = string.Empty;
                string hAGS = string.Empty;
                string resolutionInfo = string.Empty;
                Version appVersion = new Version();


                if (hwInfo != null)
                {
                    deviceName = hwInfo?.First().DeviceName;
                    cpuInfo = hwInfo?.First().Processor;
                    gpuInfo = hwInfo?.First().GPU;
                    ramInfo = hwInfo?.First().SystemRam;
                    mbInfo = hwInfo?.First().Motherboard;
                    osInfo = hwInfo?.First().OS;
                    gpuDriverInfo = hwInfo?.First().GPUDriverVersion;
                    appVersion = hwInfo?.First().AppVersion;
                    apiInfo = hwInfo?.First().ApiInfo;
                    resizableBar = hwInfo?.First().ResizableBar;
                    winGameMode = hwInfo?.First().WinGameMode;
                    hAGS = hwInfo?.First().HAGS;
                    resolutionInfo = hwInfo?.First().ResolutionInfo;
                }

                else
                {
                    bool hasCustomInfo = _appConfiguration.HardwareInfoSource
                        .ConvertToEnum<EHardwareInfoSource>() == EHardwareInfoSource.Custom;

                    if (hasCustomInfo)
                    {
                        cpuInfo = _appConfiguration.CustomCpuDescription;
                        gpuInfo = _appConfiguration.CustomGpuDescription;
                        ramInfo = _appConfiguration.CustomRamDescription;
                        mbInfo = _appConfiguration.CustomMainboardDescription;
                    }
                    else
                    {
                        cpuInfo = _systemInfo.GetProcessorName();
                        gpuInfo = _systemInfo.GetGraphicCardName();
                        ramInfo = _systemInfo.GetSystemRAMInfoName();
                        mbInfo = _systemInfo.GetMotherboardName();
                    }

                    // Like the OS version, the device name identifies the machine rather than a
                    // hardware component, so custom hardware labels do not override it.
                    deviceName = _systemInfo.GetDeviceName();
                    osInfo = _systemInfo.GetOSVersion();
                    gpuDriverInfo = _sensorService.GetGpuDriverVersion();
                    appVersion = _appVersionProvider.GetAppVersion();

                    var process = Process.GetProcessesByName(processName).FirstOrDefault();
                    apiInfo = GetApiInfo(process, runs);

                    resolutionInfo = GetRenderResolution(process);


                    _systemInfo.SetSystemInfosStatus();
                    _updateSystemInfoEvent.Publish(new ViewMessages.UpdateSystemInfo());

                    if (_systemInfo.ResizableBarHardwareStatus != ESystemInfoTertiaryStatus.Error && (_systemInfo.ResizableBarD3DStatus != ESystemInfoTertiaryStatus.Error || _systemInfo.ResizableBarVulkanStatus != ESystemInfoTertiaryStatus.Error))
                    {
                        resizableBar = "Disabled";
                        if (_systemInfo.ResizableBarHardwareStatus == ESystemInfoTertiaryStatus.Enabled)
                        {
                            if (_systemInfo.ResizableBarD3DStatus == ESystemInfoTertiaryStatus.Enabled && _systemInfo.ResizableBarVulkanStatus == ESystemInfoTertiaryStatus.Enabled)
                            {
                                resizableBar = "Enabled";
                            }
                            else if (_systemInfo.ResizableBarD3DStatus == ESystemInfoTertiaryStatus.Enabled || _systemInfo.ResizableBarVulkanStatus == ESystemInfoTertiaryStatus.Enabled)
                            {
                                resizableBar = "Partial";
                            }
                        }
                    }

                    if (_systemInfo.GameModeStatus != ESystemInfoTertiaryStatus.Error)
                        winGameMode = _systemInfo.GameModeStatus == ESystemInfoTertiaryStatus.Enabled ? "Enabled" : "Disabled";

                    if (_systemInfo.HardwareAcceleratedGPUSchedulingStatus != ESystemInfoTertiaryStatus.Error)
                        hAGS = _systemInfo.HardwareAcceleratedGPUSchedulingStatus == ESystemInfoTertiaryStatus.Enabled ? "Enabled" : "Disabled";
                }

                IList<string> headerLines = Enumerable.Empty<string>().ToList();
                var session = new Session.Classes.Session()
                {
                    Hash = string.Join(",", runs.Select(r => r.Hash).OrderBy(h => h)).GetSha1(),
                    Runs = runs.ToList(),
                    Info = new SessionInfo()
                    {
                        Id = Guid.NewGuid(),
                        ProcessName = processName.Contains(".exe") ? processName : $"{processName}.exe",
                        GameName = GetGameNameFromFileDescription(processName),
                        CreationDate = DateTime.UtcNow,
                        DeviceName = deviceName,
                        Motherboard = mbInfo,
                        OS = osInfo,
                        Processor = cpuInfo,
                        SystemRam = ramInfo,
                        GPU = gpuInfo,
                        GPUDriverVersion = gpuDriverInfo,
                        AppVersion = appVersion,
                        ApiInfo = apiInfo,
                        ResizableBar = resizableBar,
                        WinGameMode = winGameMode,
                        HAGS = hAGS,
                        PresentationMode = runs.GetPresentationMode(),
                        Comment = comment,
                        ResolutionInfo = resolutionInfo
                    }
                };

                SaveSessionToFile(filePath, session);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating {filePath}", filePath);
                return false;
            }
        }

        public async Task DuplicateSession(ISession session, bool inverse, double startTime = 0, double endTime = double.PositiveInfinity)
        {
            if (session == null)
            {
                _logger.LogError("Error duplicating session. No session found.");
                return;
            }

            try
            {
                var json = JsonConvert.SerializeObject(session);
                var clone = JsonConvert.DeserializeObject<Session.Classes.Session>(json);

                var dataPropertyInfos = typeof(SessionCaptureData).GetProperties().Where(pi => pi.PropertyType.IsArray);

                void SetArray(IEnumerable<PropertyInfo> propertyInfos, object sourceObject, object targetObject, IEnumerable<int> indicesToKeep)
                {
                    foreach (var dataPi in propertyInfos)
                    {
                        var type = dataPi.PropertyType.GetElementType();
                        var array = Array.CreateInstance(type, 0);
                        if (dataPi.GetValue(sourceObject) is Array source && source.Length > 0)
                        {
                            array = Array.CreateInstance(type, indicesToKeep.Count());
                            int targetIndex = 0;
                            foreach (var indexToKeep in indicesToKeep)
                            {
                                array.SetValue(source.GetValue(indexToKeep), targetIndex++);
                            }
                        }
                        dataPi.SetValue(targetObject, array);
                    }
                }

                int[] DetermineIndicesToKeep(double[] reference)
                {
                    if (reference == null)
                        return new int[0];

                    var indicesToKeep = new List<int>();
                    for (int index = 0; index < reference.Count(); index++)
                    {
                        if (!inverse)
                        {
                            if (reference[index] >= startTime && reference[index] <= endTime)
                            {
                                indicesToKeep.Add(index);
                            }
                        }
                        else
                        {
                            if (reference[index] < startTime || reference[index] > endTime)
                            {
                                indicesToKeep.Add(index);
                            }
                        }
                    }
                    return indicesToKeep.ToArray();
                }

                for (int sessionRunIndex = 0; sessionRunIndex < clone.Runs.Count; sessionRunIndex++)
                {
                    var sourceSessionRun = session.Runs[sessionRunIndex];
                    var targetSessionRun = clone.Runs[sessionRunIndex];
                    var dataIndicesToKeep = DetermineIndicesToKeep(sourceSessionRun.CaptureData.TimeInSeconds);
                    SetArray(dataPropertyInfos, sourceSessionRun.CaptureData, clone.Runs[sessionRunIndex].CaptureData, dataIndicesToKeep);

                    if (sourceSessionRun.SensorData2 != null)
                    {
                        var sensorIndicesToKeep = DetermineIndicesToKeep(sourceSessionRun.SensorData2.MeasureTime.Values.ToArray());
                        clone.Runs[sessionRunIndex].SensorData2 = new SessionSensorData2(initialAdd: false);
                        foreach (var collection in sourceSessionRun.SensorData2)
                        {

                            var clonedSensorEntry = new SessionSensorEntry(collection.Value.Name, collection.Value.Type);

                            if (collection.Value.Values.Count() >= sensorIndicesToKeep.LastOrDefault())
                            {
                                foreach (var indexToKeep in sensorIndicesToKeep)
                                {
                                    clonedSensorEntry.Values.AddLast(collection.Value.Values.ElementAt(indexToKeep));
                                }
                            }

                            clone.Runs[sessionRunIndex].SensorData2.Add(collection.Key, clonedSensorEntry);
                        }
                    }

                    // Dirty Hack weil (weil Alex Hacks mag) Rohdaten nicht mehr vorhanden.
                    // Hash ist nicht vergleichbar mit dem Hash, welcher aus den PresentMonLines erstellt wird
                    targetSessionRun.Hash = Convert.ToString(targetSessionRun.GetHashCode());
                }

                // remove runs without data
                clone.Runs = clone.Runs.Where(r => r.CaptureData.TimeInSeconds.Length != 0).ToList();

                if (!clone.Runs.Any())
                    return;

                clone.Hash = string.Join(",", clone.Runs.Select(r => r.Hash).OrderBy(h => h)).GetSha1();
                clone.Info.Id = Guid.NewGuid();
                NormalizeStartTimesOfSessionRuns(clone.Runs);
                clone.Info.Comment = $"(Cut) {clone.Info.Comment}";
                var filePath = await GetOutputFilename(clone.Info.ProcessName.StripExeExtension(), null);
                SaveSessionToFile(filePath, clone);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error duplicating session");
            }
        }

        private void SaveSessionToFile(string filePath, ISession session)
        {
            // Serialize to memory first to get exact size
            using (var memoryStream = new MemoryStream())
            {
                using (var streamWriter = new StreamWriter(memoryStream, Encoding.UTF8, 1024, leaveOpen: true))
                using (var jsonWriter = new JsonTextWriter(streamWriter))
                {
                    var serializer = new JsonSerializer();
                    serializer.Serialize(jsonWriter, session);
                }

                // Check disk space with a safety buffer
                var requiredBytes = memoryStream.Length;
                EnsureSufficientDiskSpace(filePath, requiredBytes);

                // Now write to disk
                try
                {
                    memoryStream.Position = 0;
                    using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write))
                    {
                        memoryStream.CopyTo(fileStream);
                    }

                    _logger.LogInformation("{FilePath} successfully written", filePath);
                    InvalidateSessionCache(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error writing session to file {FilePath}", filePath);
                    throw;
                }
            }
        }

        private void EnsureSufficientDiskSpace(string filePath, long requiredBytes)
        {
            const long MinimumBuffer = 10 * 1024 * 1024; // 10 MB minimum buffer

            var fullPath = Path.GetFullPath(filePath);

            // DriveInfo does not support UNC paths (network shares)
            if (fullPath.StartsWith(@"\\"))
            {
                return;
            }

            var root = Path.GetPathRoot(fullPath);
            var driveInfo = new DriveInfo(root);
            var required = requiredBytes + MinimumBuffer;

            if (driveInfo.AvailableFreeSpace < required)
            {
                throw new IOException(
                    string.Format(
                        "Insufficient disk space on {0}. Need {1:N0} bytes, have {2:N0} bytes.",
                        root,
                        required,
                        driveInfo.AvailableFreeSpace));
            }
        }

        private string GetGameNameFromProcessList(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return "Unknown";
            }

            var processNameExtStripped = processName.StripExeExtension();
            return _processList?.FindProcessByName(processName)?.DisplayName ?? processNameExtStripped;
        }

        private string GetGameNameFromFileDescription(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return "Unknown";
            }

            var processNameStripped = processName.StripExeExtension();
            Process[] processes = Process.GetProcessesByName(processNameStripped);

            if (processes.Any())
            {
                // prefer getting game name from process list
                var gameName = _processList.FindProcessByName(processName)?.DisplayName;

                if (gameName != null)
                {
                    return gameName;
                }

                try
                {
                    string mainWindoTitle = processes.First()?.MainWindowTitle?.TrimEnd();
                    string fileDescription = processes.First()?.MainModule?.FileVersionInfo?.FileDescription?.TrimEnd();

                    // prefer file description
                    if (!fileDescription.IsNullOrEmpty())
                    {
                        if (processNameStripped != fileDescription)
                        {
                            return fileDescription;
                        }
                    }
                    else if (!mainWindoTitle.IsNullOrEmpty())
                    {
                        if (processNameStripped != mainWindoTitle)
                        {
                            return mainWindoTitle;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error getting game name from process info");
                }
            }

            return GetGameNameFromProcessList(processName);
        }

        private async Task<string> GetOutputFilename(string processName, string recordDirectory)
        {
            var filename = CaptureServiceConfiguration.GetCaptureFilename(processName);
            var directory = recordDirectory is null ? await _recordObserver.ObservingDirectoryStream.Take(1) : new DirectoryInfo(recordDirectory);
            return Path.Combine(directory.FullName, filename);
        }

        public ISessionRun ConvertPresentDataLinesToSessionRun(IEnumerable<string> presentLines)
        {
            try
            {
                int indexFrameStart = -1;
                int indexFrameTimes = -1;
                int indexUntilDisplayedTimes = -1;
                int indexAppMissed = -1;
                int indexPresentMode = -1;
                int indexMsInPresentAPI = -1;
                int indexDisplayTimes = -1;
                int indexQPCTimes = -1;
                int indexRuntime = -1;
                int indexAllowsTearing = -1;
                int indexSyncInterval = -1;
                int indexFrameType = -1;
                int indexPcLatency = -1;
                int indexMsAnimationError = -1;
                int indexmsGPUActive = -1;
                int indexmsCPUActive = -1;
                int indexCPUStartQPCTime = -1;
                int indexCPUStartQPCTimeInMs = -1;

                string headerLine;
                string firstLine = presentLines.First();

                if (firstLine.Contains("frametime") || firstLine.StartsWith("Application"))
                {
                    headerLine = firstLine;
                    presentLines = presentLines.Skip(1);
                }
                else
                {
                    headerLine = _captureService.ColumnHeader;
                }

                // Filter lines by dominant process and SwapChainAddress
                var dataLines = FilterByDominantSwapChain(presentLines, headerLine);

                var sessionRun = new SessionRun()
                {
                    Hash = GetPresentLinesSha1(dataLines),
                    PresentMonRuntime = "unknown"
                };

                // With FrameType and app timing enabled
                // Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode,
                // FrameType,TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI,MsRenderPresentLatency,
                // MsUntilDisplayed,MsPCLatency,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy,MsCPUWait,MsGPULatency,MsGPUTime,MsGPUBusy,
                // MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay,MsInstrumentedLatency

                string frameStartUnit = "s";
                var metrics = Array.ConvertAll(headerLine.Split(','), p => p.Trim());
                for (int i = 0; i < metrics.Length; i++)
                {
                    if (string.Compare(metrics[i], "AppRenderStart") == 0 || string.Compare(metrics[i], "TimeInSeconds") == 0
                         || string.Compare(metrics[i], "TimeInMs") == 0)
                    {
                        indexFrameStart = i;

                        if (string.Compare(metrics[i], "TimeInMs") == 0)
                        {
                            frameStartUnit = "ms";
                        }
                    }
                    if (string.Compare(metrics[i], "MsBetweenAppPresents", true) == 0
                        || string.Compare(metrics[i], "msBetweenPresents", true) == 0
                        // MangoHud frame times column
                        || string.Compare(metrics[i], "frametime", true) == 0
                        // PresentMon >= v2.2
                        || string.Compare(metrics[i], "FrameTime", true) == 0)
                    {
                        indexFrameTimes = i;
                    }
                    if (string.Compare(metrics[i], "msUntilDisplayed", true) == 0 || string.Compare(metrics[i], "DisplayLatency", true) == 0)
                    {
                        indexUntilDisplayedTimes = i;
                    }
                    if (string.Compare(metrics[i], "AppMissed") == 0 || string.Compare(metrics[i], "Dropped") == 0)
                    {
                        indexAppMissed = i;
                    }
                    if (string.Compare(metrics[i], "msInPresentAPI", true) == 0 || string.Compare(metrics[i], "PresentRuntime", true) == 0)
                    {
                        indexMsInPresentAPI = i;
                    }
                    if (string.Compare(metrics[i], "msBetweenDisplayChange", true) == 0 || string.Compare(metrics[i], "DisplayedTime", true) == 0)
                    {
                        indexDisplayTimes = i;
                    }
                    if (string.Compare(metrics[i], "QPCTime") == 0)
                    {
                        indexQPCTimes = i;
                    }
                    if (string.Compare(metrics[i], "PresentMode") == 0)
                    {
                        indexPresentMode = i;
                    }
                    if (string.Compare(metrics[i], "Runtime") == 0)
                    {
                        indexRuntime = i;
                    }
                    if (string.Compare(metrics[i], "AllowsTearing") == 0)
                    {
                        indexAllowsTearing = i;
                    }
                    if (string.Compare(metrics[i], "SyncInterval") == 0)
                    {
                        indexSyncInterval = i;
                    }
                    if (string.Compare(metrics[i], "FrameType", true) == 0)
                    {
                        indexFrameType = i;
                    }
                    if (string.Compare(metrics[i], "MsPCLatency") == 0)
                    {
                        indexPcLatency = i;
                    }
                    if (string.Compare(metrics[i], "MsAnimationError") == 0 || string.Compare(metrics[i], "AnimationError") == 0)
                    {
                        indexMsAnimationError = i;
                    }
                    if (string.Compare(metrics[i], "msGPUActive") == 0 || string.Compare(metrics[i], "GPUBusy") == 0 || string.Compare(metrics[i], "MsGPUBusy") == 0)
                    {
                        indexmsGPUActive = i;
                    }
                    if (string.Compare(metrics[i], "MsCPUBusy") == 0)
                    {
                        indexmsCPUActive = i;
                    }
                    if (string.Compare(metrics[i], "CPUStartQPCTime") == 0)
                    {
                        indexCPUStartQPCTime = i;
                    }
                    if (string.Compare(metrics[i], "CPUStartQPCTimeInMs") == 0 || (string.Compare(metrics[i], "CPUStartTimeInMs") == 0))
                    {
                        indexCPUStartQPCTimeInMs = i;
                    }
                }

                var presentLineCount = dataLines.Count;
                var captureData = new SessionCaptureData(presentLineCount)
                {
                    AnimationError = Enumerable.Repeat(double.NaN, presentLineCount).ToArray()
                };

                // When latency data is available initialize array
                if (indexPcLatency > 0)
                {
                    captureData.PcLatency = new double[presentLineCount];
                }

                var presentModeMapping = Enum.GetValues(typeof(EPresentMode)).Cast<EPresentMode>()
                    .ToDictionary(e => e.GetDescription(), e => (int)e);
                var requiredColumns = new bool[metrics.Length];
                MarkRequiredColumns(requiredColumns,
                    indexFrameStart, indexFrameTimes, indexUntilDisplayedTimes, indexAppMissed,
                    indexPresentMode, indexMsInPresentAPI, indexDisplayTimes, indexQPCTimes,
                    indexRuntime, indexAllowsTearing, indexSyncInterval, indexFrameType,
                    indexPcLatency, indexMsAnimationError, indexmsGPUActive, indexmsCPUActive,
                    indexCPUStartQPCTime, indexCPUStartQPCTimeInMs);
                var values = new string[metrics.Length];

                for (int lineIndex = 0; lineIndex < dataLines.Count; lineIndex++)
                {
                    string line = dataLines[lineIndex];
                    if (line.Length == 0)
                    {
                        continue;
                    }
                    ParseCsvFields(line, values, requiredColumns);
                    double frameStart = 0;

                    if (lineIndex == 0)
                    {
                        sessionRun.PresentMonRuntime = GetStringFromArray(values, indexRuntime);
                    }

                    if (indexFrameStart > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexFrameStart), NumberStyles.Any, CultureInfo.InvariantCulture, out frameStart))
                        {
                            if (frameStartUnit == "ms")
                            {
                                captureData.TimeInSeconds[lineIndex] = frameStart * 1E-03;
                            }
                            else
                            {
                                captureData.TimeInSeconds[lineIndex] = frameStart;
                            }
                        }
                    }

                    if (indexCPUStartQPCTime > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexCPUStartQPCTime), NumberStyles.Any, CultureInfo.InvariantCulture, out frameStart))
                        {
                            captureData.TimeInSeconds[lineIndex] = frameStart * 1E-03;
                        }
                    }

                    if (indexCPUStartQPCTimeInMs > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexCPUStartQPCTimeInMs), NumberStyles.Any, CultureInfo.InvariantCulture, out frameStart))
                        {
                            captureData.TimeInSeconds[lineIndex] = frameStart * 1E-03; ;
                        }
                    }

                    if (indexFrameTimes > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexFrameTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var frameTime))
                        {
                            captureData.MsBetweenPresents[lineIndex] = frameTime;
                        }
                    }

                    if (indexAppMissed > -1)
                    {
                        if (int.TryParse(GetStringFromArray(values, indexAppMissed), NumberStyles.Any, CultureInfo.InvariantCulture, out var appMissed))
                        {
                            captureData.Dropped[lineIndex] = Convert.ToBoolean(appMissed);
                        }
                        else
                        {
                            captureData.Dropped[lineIndex] = true;
                        }
                    }

                    if (indexDisplayTimes > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexDisplayTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var displayTime))
                        {
                            captureData.MsBetweenDisplayChange[lineIndex] = displayTime;
                        }
                    }

                    if (indexUntilDisplayedTimes > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexUntilDisplayedTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var untilDisplayTime))
                        {
                            captureData.MsUntilDisplayed[lineIndex] = untilDisplayTime;
                        }
                    }

                    if (indexMsInPresentAPI > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexMsInPresentAPI), NumberStyles.Any, CultureInfo.InvariantCulture, out var inPresentAPITime))
                        {
                            captureData.MsInPresentAPI[lineIndex] = inPresentAPITime;
                        }
                    }

                    if (indexQPCTimes > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexQPCTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var qPCTime))
                        {
                            captureData.QPCTime[lineIndex] = qPCTime;
                        }
                    }

                    if (indexPresentMode > -1)
                    {
                        if (presentModeMapping.TryGetValue(GetStringFromArray(values, indexPresentMode), out var presentMode))
                        {
                            captureData.PresentMode[lineIndex] = presentMode;
                        }
                    }

                    if (indexAllowsTearing > -1)
                    {
                        if (int.TryParse(GetStringFromArray(values, indexAllowsTearing), NumberStyles.Any, CultureInfo.InvariantCulture, out var allowsTearing))
                        {
                            captureData.AllowsTearing[lineIndex] = allowsTearing;
                        }
                    }
                    if (indexSyncInterval > -1)
                    {
                        if (int.TryParse(GetStringFromArray(values, indexSyncInterval), NumberStyles.Any, CultureInfo.InvariantCulture, out var syncInterval))
                        {
                            captureData.SyncInterval[lineIndex] = syncInterval;
                        }
                    }
                    if (indexFrameType > -1)
                    {
                        captureData.FrameType[lineIndex] = GetStringFromArray(values, indexFrameType);
                    }
                    if (indexPcLatency > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexPcLatency), NumberStyles.Any, CultureInfo.InvariantCulture, out var pcLatency))
                        {
                            captureData.PcLatency[lineIndex] = pcLatency;
                        }
                        else
                        {
                            captureData.PcLatency[lineIndex] = double.NaN;
                        }
                    }
                    if (indexMsAnimationError > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexMsAnimationError), NumberStyles.Any, CultureInfo.InvariantCulture, out var animationError))
                        {
                            captureData.AnimationError[lineIndex] = animationError;
                        }
                    }
                    if (indexmsGPUActive > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexmsGPUActive), NumberStyles.Any, CultureInfo.InvariantCulture, out var gpuActive))
                        {
                            captureData.GpuActive[lineIndex] = gpuActive;
                        }
                    }
                    if (indexmsCPUActive > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexmsCPUActive), NumberStyles.Any, CultureInfo.InvariantCulture, out var cpuActive))
                        {
                            captureData.CpuActive[lineIndex] = cpuActive;
                        }
                    }
                }

                //Normalize times
                if (indexFrameStart > -1)
                {
                    var startTime = captureData.TimeInSeconds[0];
                    captureData.TimeInSeconds = captureData.TimeInSeconds.Select(time => time - startTime).ToArray();
                }
                // Get render times from frame time data
                else
                {
                    var normalizedTimes = captureData.MsBetweenPresents
                        .Skip(1)
                        .Prepend(0)
                        .ToArray();

                    captureData.TimeInSeconds = normalizedTimes
                        .Aggregate(new List<double>(), (a, x) =>
                        {
                            a.Add(a.LastOrDefault() + x);
                            return a;
                        })
                        .Select(x => x / 1000d)
                        .ToArray();
                }

                // Take over sensor data from CSV file
                sessionRun.SensorData2 = new SessionSensorData2(initialAdd: true);
                captureData.TimeInSeconds.ForEach(time => sessionRun.SensorData2["MeasureTime"].Values.AddLast(time));

                sessionRun.CaptureData = captureData;
                return sessionRun;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error converting PresentData");
                throw;
            }
        }

        /// <summary>
        /// Filters present data lines to include only the dominant process and its dominant SwapChainAddress.
        /// This handles scenarios where multiple processes or multiple swap chains per process are present.
        /// </summary>
        private List<string> FilterByDominantSwapChain(IEnumerable<string> presentLines, string headerLine)
        {
            var linesList = presentLines.ToList();
            if (linesList.Count == 0)
                return linesList;

            // Find SwapChainAddress index from header
            var metrics = Array.ConvertAll(headerLine.Split(','), p => p.Trim());
            int swapChainIndex = -1;
            int processNameIndex = -1;

            for (int i = 0; i < metrics.Length; i++)
            {
                if (string.Compare(metrics[i], "SwapChainAddress", true) == 0)
                    swapChainIndex = i;
                if (string.Compare(metrics[i], "Application", true) == 0)
                    processNameIndex = i;
            }

            // If SwapChainAddress column not found, return lines as-is
            if (swapChainIndex < 0 || processNameIndex < 0)
                return linesList;

            // Extract only the two fields needed here. Splitting every row into every CSV
            // column doubles most of the parser's allocations before the real parse begins.
            var processSwapChainCounts = new Dictionary<(string ProcessName, string SwapChain), int>();
            var pairOrder = new List<(string ProcessName, string SwapChain)>();

            foreach (var line in linesList)
            {
                if (!TryGetCsvFields(line, processNameIndex, swapChainIndex,
                    out var processName, out var swapChain))
                {
                    continue;
                }

                var key = (processName, swapChain);

                if (processSwapChainCounts.TryGetValue(key, out var count))
                {
                    processSwapChainCounts[key] = count + 1;
                }
                else
                {
                    processSwapChainCounts[key] = 1;
                    pairOrder.Add(key);
                }
            }

            if (processSwapChainCounts.Count == 0)
                return linesList;

            var processOrder = new List<string>();
            var processTotals = new Dictionary<string, int>();
            var dominantPairs = new Dictionary<string, (string SwapChain, int Count)>();
            foreach (var pair in pairOrder)
            {
                int count = processSwapChainCounts[pair];
                if (processTotals.TryGetValue(pair.ProcessName, out var total))
                {
                    processTotals[pair.ProcessName] = total + count;
                }
                else
                {
                    processTotals[pair.ProcessName] = count;
                    processOrder.Add(pair.ProcessName);
                }

                if (!dominantPairs.TryGetValue(pair.ProcessName, out var dominant)
                    || count > dominant.Count)
                {
                    dominantPairs[pair.ProcessName] = (pair.SwapChain, count);
                }
            }

            string selectedProcessName = processOrder[0];
            var selectedDominant = dominantPairs[selectedProcessName];
            foreach (var processName in processOrder.Skip(1))
            {
                var candidate = dominantPairs[processName];
                if (candidate.Count > selectedDominant.Count)
                {
                    selectedProcessName = processName;
                    selectedDominant = candidate;
                }
            }

            var selectedSwapChain = selectedDominant.SwapChain;

            _logger.LogDebug("SwapChain filtering: Selected process {process} with SwapChain {swapChain} " +
                "({selectedFrames} frames out of {totalFrames} total for this process)",
                selectedProcessName, selectedSwapChain, selectedDominant.Count, processTotals[selectedProcessName]);

            // Log if we're filtering out other processes or swap chains
            if (processOrder.Count > 1)
            {
                _logger.LogDebug("SwapChain filtering: Filtered out {processCount} other process(es)",
                    processOrder.Count - 1);
            }

            var otherSwapChainsForProcess = processSwapChainCounts
                .Where(kvp => kvp.Key.ProcessName == selectedProcessName && kvp.Key.SwapChain != selectedSwapChain)
                .ToList();

            if (otherSwapChainsForProcess.Any())
            {
                var filteredCount = otherSwapChainsForProcess.Sum(x => x.Value);
                _logger.LogDebug("SwapChain filtering: Filtered out {frameCount} frames from " +
                    "{swapChainCount} other SwapChain(s) for process {process}",
                    filteredCount, otherSwapChainsForProcess.Count, selectedProcessName);
            }

            // Filter lines to only include the selected process and SwapChain
            var filteredLines = new List<string>(selectedDominant.Count);
            foreach (var line in linesList)
            {
                if (TryGetCsvFields(line, processNameIndex, swapChainIndex,
                    out var processName, out var swapChain)
                    && processName == selectedProcessName
                    && swapChain == selectedSwapChain)
                {
                    filteredLines.Add(line);
                }
            }

            return filteredLines;
        }

        private static bool TryGetCsvFields(string line, int firstIndex, int secondIndex,
            out string firstValue, out string secondValue)
        {
            firstValue = null;
            secondValue = null;
            int highestIndex = Math.Max(firstIndex, secondIndex);
            int position = 0;

            for (int fieldIndex = 0; fieldIndex <= highestIndex; fieldIndex++)
            {
                bool required = fieldIndex == firstIndex || fieldIndex == secondIndex;
                if (!TryReadCsvField(line, ref position, required, out var value))
                {
                    return false;
                }

                if (fieldIndex == firstIndex)
                {
                    firstValue = value;
                }
                if (fieldIndex == secondIndex)
                {
                    secondValue = value;
                }
            }

            return true;
        }

        private static void MarkRequiredColumns(bool[] requiredColumns, params int[] indices)
        {
            foreach (int index in indices)
            {
                if (index >= 0 && index < requiredColumns.Length)
                {
                    requiredColumns[index] = true;
                }
            }
        }

        private static void ParseCsvFields(string line, string[] values, bool[] requiredColumns)
        {
            Array.Clear(values, 0, values.Length);
            int position = 0;
            for (int fieldIndex = 0; fieldIndex < values.Length; fieldIndex++)
            {
                if (!TryReadCsvField(line, ref position, requiredColumns[fieldIndex], out var value))
                {
                    return;
                }

                if (requiredColumns[fieldIndex])
                {
                    values[fieldIndex] = value;
                }
            }
        }

        private static bool TryReadCsvField(string line, ref int position, bool materialize, out string value)
        {
            value = null;
            if (line == null || position > line.Length)
            {
                return false;
            }

            if (position < line.Length && line[position] == '"')
            {
                position++;
                int segmentStart = position;
                StringBuilder builder = materialize ? new StringBuilder() : null;
                bool closed = false;

                while (position < line.Length)
                {
                    if (line[position] != '"')
                    {
                        position++;
                        continue;
                    }

                    if (position + 1 < line.Length && line[position + 1] == '"')
                    {
                        if (materialize)
                        {
                            builder.Append(line, segmentStart, position - segmentStart);
                            builder.Append('"');
                        }
                        position += 2;
                        segmentStart = position;
                        continue;
                    }

                    if (materialize)
                    {
                        builder.Append(line, segmentStart, position - segmentStart);
                    }
                    position++;
                    closed = true;
                    break;
                }

                if (!closed && materialize)
                {
                    builder.Append(line, segmentStart, position - segmentStart);
                }

                while (position < line.Length && line[position] != ',')
                {
                    if (materialize)
                    {
                        builder.Append(line[position]);
                    }
                    position++;
                }

                if (materialize)
                {
                    value = builder.ToString();
                }
            }
            else
            {
                int fieldStart = position;
                while (position < line.Length && line[position] != ',')
                {
                    position++;
                }

                if (materialize)
                {
                    value = line.Substring(fieldStart, position - fieldStart);
                }
            }

            if (position < line.Length && line[position] == ',')
            {
                position++;
            }
            else
            {
                position = line.Length + 1;
            }

            return true;
        }

        private static string GetPresentLinesSha1(IList<string> lines)
        {
            var encoding = Encoding.ASCII;
            var buffer = new byte[256];
            var delimiter = new byte[] { (byte)',' };

            using (var sha1 = new SHA1Managed())
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (i > 0)
                    {
                        sha1.TransformBlock(delimiter, 0, delimiter.Length, delimiter, 0);
                    }

                    string line = lines[i] ?? string.Empty;
                    int requiredLength = encoding.GetMaxByteCount(line.Length);
                    if (buffer.Length < requiredLength)
                    {
                        buffer = new byte[requiredLength];
                    }

                    int byteCount = encoding.GetBytes(line, 0, line.Length, buffer, 0);
                    if (byteCount > 0)
                    {
                        sha1.TransformBlock(buffer, 0, byteCount, buffer, 0);
                    }
                }

                sha1.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var hash = new StringBuilder(sha1.Hash.Length * 2);
                foreach (byte value in sha1.Hash)
                {
                    hash.Append(value.ToString("X2"));
                }
                return hash.ToString();
            }
        }

        public void NormalizeStartTimesOfSessionRuns(IEnumerable<ISessionRun> sessionRuns)
        {
            double startTimePresents = 0;
            double lastSensorMeasureTime = 0;

            foreach (var sessionRun in sessionRuns)
            {
                for (int i = 0; i < sessionRun.CaptureData.MsBetweenPresents.Count(); i++)
                {
                    sessionRun.CaptureData.TimeInSeconds[i] = startTimePresents;
                    var frameTimeInMs = 1E-03 * sessionRun.CaptureData.MsBetweenPresents[i];
                    startTimePresents += frameTimeInMs;
                }
            }

            if (sessionRuns.All(sr => sr.SensorData2 != null))
            {
                foreach (var sessionRun in sessionRuns)
                {
                    var tmpMeasureTimeArray = new double[sessionRun.SensorData2.BetweenMeasureTimes.Count()];
                    for (int i = 0; i < sessionRun.SensorData2.BetweenMeasureTimes.Count(); i++)
                    {
                        lastSensorMeasureTime += sessionRun.SensorData2.BetweenMeasureTimes[i];
                        tmpMeasureTimeArray[i] = lastSensorMeasureTime;
                    }
                    sessionRun.SensorData2.MeasureTime.Values.Clear();
                    tmpMeasureTimeArray.ForEach(x => sessionRun.SensorData2.MeasureTime.Values.AddLast(x));
                }
            }
            else
            {
                sessionRuns.ForEach(sr => sr.SensorData2 = null);
            }
        }
    }

    class HasIgnoreFlagException : Exception { }
}
