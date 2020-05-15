using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Overlay;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Extensions;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.PresentMonInterface;
using CapFrameX.Statistics.NetStandard;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Data
{
    public class RecordManager : IRecordManager
    {
        private const string IGNOREFLAGMARKER = "//Ignore=true";
        public Func<uint, string> GetApiInfoFunc { get; set; }

        private readonly TimeSpan _fileAccessIntervalTimespan = TimeSpan.FromMilliseconds(200);
        private readonly int _fileAccessIntervalRetryLimit = 50;
        private readonly ILogger<RecordManager> _logger;
        private readonly IAppConfiguration _appConfiguration;
        private readonly IRecordDirectoryObserver _recordObserver;
        private readonly IAppVersionProvider _appVersionProvider;
        private readonly ISensorService _sensorService;
        private readonly ProcessList _processList;

        public RecordManager(ILogger<RecordManager> logger,
                             IAppConfiguration appConfiguration,
                             IRecordDirectoryObserver recordObserver,
                             IAppVersionProvider appVersionProvider,
                             ISensorService sensorService,
                             ProcessList processList)
        {
            _logger = logger;
            _appConfiguration = appConfiguration;
            _recordObserver = recordObserver;
            _appVersionProvider = appVersionProvider;
            _sensorService = sensorService;
            _processList = processList;
        }
        public void UpdateCustomData(IFileRecordInfo recordInfo, string customCpuInfo, string customGpuInfo, string customRamInfo, string customGameName, string customComment)
        {
            if (recordInfo == null || customCpuInfo == null ||
                customGpuInfo == null || customRamInfo == null || customGameName == null ||
                customComment == null)
                return;

            try
            {
                if (recordInfo.FileInfo.Extension == ".json")
                {
                    var session = LoadSessionFromJSON(recordInfo.FileInfo);
                    session.Info.Processor = customCpuInfo;
                    session.Info.GPU = customGpuInfo;
                    session.Info.SystemRam = customRamInfo;
                    session.Info.GameName = customGameName;
                    session.Info.Comment = customComment;

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

                        // GameName
                        int gameNameHeaderIndex = GetHeaderIndex(lines, "GameName");
                        lines[gameNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}GameName{FileRecordInfo.INFO_SEPERATOR}{customGameName}";

                        // Comment
                        int commentNameHeaderIndex = GetHeaderIndex(lines, "Comment");
                        lines[commentNameHeaderIndex] = $"{FileRecordInfo.HEADER_MARKER}Comment{FileRecordInfo.INFO_SEPERATOR}{customComment}";

                        File.WriteAllLines(recordInfo.FullPath, lines);
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
                        $"{FileRecordInfo.HEADER_MARKER}Motherboard{FileRecordInfo.INFO_SEPERATOR}{recordInfo.MotherboardName}",
                        $"{FileRecordInfo.HEADER_MARKER}OS{FileRecordInfo.INFO_SEPERATOR}{recordInfo.OsVersion}",
                        $"{FileRecordInfo.HEADER_MARKER}Processor{FileRecordInfo.INFO_SEPERATOR}{customCpuInfo}",
                        $"{FileRecordInfo.HEADER_MARKER}System RAM{FileRecordInfo.INFO_SEPERATOR}{recordInfo.SystemRamInfo}",
                        $"{FileRecordInfo.HEADER_MARKER}Base Driver Version{FileRecordInfo.INFO_SEPERATOR}{recordInfo.BaseDriverVersion}",
                        $"{FileRecordInfo.HEADER_MARKER}Driver Package{FileRecordInfo.INFO_SEPERATOR}{recordInfo.DriverPackage}",
                        $"{FileRecordInfo.HEADER_MARKER}GPU{FileRecordInfo.INFO_SEPERATOR}{customGpuInfo}",
                        $"{FileRecordInfo.HEADER_MARKER}GPU #{FileRecordInfo.INFO_SEPERATOR}{recordInfo.NumberGPUs}",
                        $"{FileRecordInfo.HEADER_MARKER}GPU Core Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUCoreClock}",
                        $"{FileRecordInfo.HEADER_MARKER}GPU Memory Clock (MHz){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUMemoryClock}",
                        $"{FileRecordInfo.HEADER_MARKER}GPU Memory (MB){FileRecordInfo.INFO_SEPERATOR}{recordInfo.GPUMemory}",
                        $"{FileRecordInfo.HEADER_MARKER}Comment{FileRecordInfo.INFO_SEPERATOR}{customComment}"
                    };

                        File.WriteAllLines(recordInfo.FullPath, headerLines.Concat(lines));
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

        public List<ISystemInfoEntry> GetSystemInfos(IFileRecordInfo recordInfo)
        {
            _logger.LogInformation("Getting Systeminfos");
            var systemInfos = new List<ISystemInfoEntry>();

            if (!string.IsNullOrWhiteSpace(recordInfo.CreationDate))
                systemInfos.Add(new SystemInfoEntry() { Key = "Creation Date & Time", Value = recordInfo.CreationDate + "  |  " + recordInfo.CreationTime });
            if (!string.IsNullOrWhiteSpace(recordInfo.Comment))
                systemInfos.Add(new SystemInfoEntry() { Key = "Comment", Value = recordInfo.Comment });
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
            if (!string.IsNullOrWhiteSpace(recordInfo.PresentationMode))
                systemInfos.Add(new SystemInfoEntry() { Key = "Presentation Mode", Value = recordInfo.PresentationMode });

            return systemInfos;
        }

        public ISession LoadData(string path)
        {
            _logger.LogInformation("Loading data from: {path}", path);
            if (string.IsNullOrWhiteSpace(path))
            {
                return null;
            }
            var fileInfo = new FileInfo(path);

            if (!fileInfo.Exists || fileInfo.Length == 0)
            {
                return null;
            }

            try
            {
                switch (fileInfo.Extension)
                {
                    case ".json":
                        return LoadSessionFromJSON(fileInfo);
                    case ".csv":
                        return LoadSessionFromCSV(fileInfo);
                    default:
                        return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while loading {path}", path);
                return null;
            }
        }

        private ISession LoadSessionFromJSON(FileInfo fileInfo)
        {
            using (var stream = new StreamReader(fileInfo.FullName))
            {
                using (JsonReader jsonReader = new JsonTextReader(stream))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    var session = serializer.Deserialize<Session.Classes.Session>(jsonReader);
                    return session;
                }
            }
        }

        private ISession LoadSessionFromCSV(FileInfo csvFile)
        {
            using (var reader = new StreamReader(csvFile.FullName))
            {
                    var lines = File.ReadAllLines(csvFile.FullName);
                    if(lines.First().Equals(IGNOREFLAGMARKER))
                    {
                        throw new HasIgnoreFlagException();
                    }
                    var sessionRun = ConvertPresentDataLinesToSessionRun(lines.SkipWhile(line => line.Contains(FileRecordInfo.HEADER_MARKER)));
                    var recordedFileInfo = FileRecordInfo.Create(csvFile, sessionRun.Hash);
                    var systemInfos = GetSystemInfos(recordedFileInfo);

                    return new Session.Classes.Session()
                    {
                        Hash = string.Join(",", new string[] { sessionRun.Hash }).GetSha1(),
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
                            Motherboard = recordedFileInfo.MotherboardName,
                            DriverPackage = recordedFileInfo.DriverPackage,
                            GpuMemoryClock = recordedFileInfo.GPUMemoryClock,
                            CreationDate = DateTime.TryParse(recordedFileInfo.CreationDate + "T" + recordedFileInfo.CreationTime, out var creationDate) ? creationDate : new DateTime(),
                            AppVersion = new Version(),
                            ApiInfo = recordedFileInfo.ApiInfo
                        }
                    };
                
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private string GetStringFromArray(string[] array, int index)
        {
            if (index < array.Length)
            {
                return array[index];
            }
            return string.Empty;
        }

        private static readonly string COLUMN_HEADER =
            $"Application,ProcessID,SwapChainAddress,Runtime,SyncInterval,PresentFlags," +
            $"AllowsTearing,PresentMode,WasBatched,DwmNotified,Dropped,TimeInSeconds,MsBetweenPresents," +
            $"MsBetweenDisplayChange,MsInPresentAPI,MsUntilRenderComplete,MsUntilDisplayed,QPCTime";

        public async Task<IFileRecordInfo> GetFileRecordInfo(FileInfo fileInfo)
        {
            return await Observable.Timer(_fileAccessIntervalTimespan)
                .SelectMany(_ =>
                {
                    switch (fileInfo.Extension)
                    {
                        case ".csv":
                            var sessionFromCSV = LoadSessionFromCSV(fileInfo);
                            return Observable.Return(FileRecordInfo.Create(fileInfo, sessionFromCSV.Hash));
                        case ".json":
                            var sessionFromJSON = LoadSessionFromJSON(fileInfo);
                            return Observable.Return(FileRecordInfo.Create(fileInfo, sessionFromJSON));
                        default:
                            return Observable.Empty<IFileRecordInfo>();
                    }
                })
                .Catch<IFileRecordInfo, Exception>(e =>
                {
                    if (e is IOException)
                    { // If e is IOException we will throw it again, so the retry will execute the function again
                        return Observable.Throw<IFileRecordInfo>(e);
                    } else {// otherwise, we return empty
                        if (!(e is HasIgnoreFlagException))
                        {
                            _logger.LogError(e, "Error Creating FileRecordInfo of {path}", fileInfo.FullName);
                        }
                        return Observable.Empty<IFileRecordInfo>();
                    }
                })
                .Retry(_fileAccessIntervalRetryLimit)
                .Do(fileRecordInfo =>
                {
                    if (fileRecordInfo is IFileRecordInfo)
                    {
                        fileRecordInfo.GameName = GetGamenameForProcess(fileRecordInfo.ProcessName);
                    }
                });
        }

        public async Task SavePresentmonRawToFile(IEnumerable<string> lines, string process)
        {
            try {
                var filePath = await GetOutputFilename(process);
                lines = new string[] { IGNOREFLAGMARKER, COLUMN_HEADER}.Concat(lines);
                File.WriteAllLines(filePath + ".csv", lines);
            } catch(Exception) { }
        }

        public async Task<bool> SaveSessionRunsToFile(IEnumerable<ISessionRun> runs, string processName)
        {
            var filePath = await GetOutputFilename(processName);
            try
            {
                if (runs.Count() > 1)
                {
                    NormalizeStartTimesOfAggragationRuns(runs);
                }
                var csv = new StringBuilder();
                var datetime = DateTime.Now;

                // manage custom hardware info
                bool hasCustomInfo = _appConfiguration.HardwareInfoSource
                    .ConvertToEnum<EHardwareInfoSource>() == EHardwareInfoSource.Custom;

                var cpuInfo = string.Empty;
                var gpuInfo = string.Empty;
                var ramInfo = string.Empty;

                if (hasCustomInfo)
                {
                    cpuInfo = _appConfiguration.CustomCpuDescription;
                    gpuInfo = _appConfiguration.CustomGpuDescription;
                    ramInfo = _appConfiguration.CustomRamDescription;
                }
                else
                {
                    cpuInfo = SystemInfo.GetProcessorName();
                    gpuInfo = SystemInfo.GetGraphicCardName();
                    ramInfo = SystemInfo.GetSystemRAMInfoName();
                }

                IList<string> headerLines = Enumerable.Empty<string>().ToList();
                var process = Process.GetProcessesByName(processName).FirstOrDefault();
                string apiInfo = process != null ? GetApiInfoFunc?.Invoke((uint)process.Id) : "unknown";

                var session = new Session.Classes.Session()
                {
                    Hash = string.Join(",", runs.Select(r => r.Hash).OrderBy(h => h)).GetSha1(),
                    Runs = runs.ToList(),
                    Info = new SessionInfo()
                    {
                        Id = Guid.NewGuid(),
                        ProcessName = processName.Contains(".exe") ? processName : $"{processName}.exe",
                        GameName = GetGamenameForProcess(processName),
                        CreationDate = DateTime.UtcNow,
                        Motherboard = SystemInfo.GetMotherboardName(),
                        OS = SystemInfo.GetOSVersion(),
                        Processor = cpuInfo,
                        SystemRam = ramInfo,
                        GPU = gpuInfo,
                        GPUDriverVersion = _sensorService.GetGpuDriverVersion(),
                        AppVersion = _appVersionProvider.GetAppVersion(),
                        ApiInfo = apiInfo,
                        PresentationMode = runs.GetPresentationMode()
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

        private void SaveSessionToFile(string filePath, ISession session)
        {
            try
            {
                using (var streamWriter = new StreamWriter(filePath))
                {
                    using (JsonWriter jsonWriter = new JsonTextWriter(streamWriter))
                    {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(jsonWriter, session);
                        _logger.LogInformation("{filePath} successfully written", filePath);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error writing JSON file");
            }
        }

        private string GetGamenameForProcess(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return string.Empty;
            }

            return _processList.FindProcessByProcessName(processName)?.DisplayName ?? processName.Replace(".exe", string.Empty);
        }

        private async Task<string> GetOutputFilename(string processName)
        {
            var filename = CaptureServiceConfiguration.GetCaptureFilename(processName);
            var directory = await _recordObserver.ObservingDirectoryStream.Take(1);
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

                string headerLine;
                if (!presentLines.First().StartsWith("Application"))
                {
                    headerLine = COLUMN_HEADER;
                }
                else
                {
                    headerLine = presentLines.First();
                    presentLines = presentLines.Skip(1);
                }

                var sessionRun = new SessionRun()
                {
                    Hash = string.Join(",", presentLines).GetSha1(),
                    PresentMonRuntime = "unknown"
                };

                var metrics = headerLine.Split(',');
                for (int i = 0; i < metrics.Count(); i++)
                {
                    if (string.Compare(metrics[i], "AppRenderStart") == 0 || string.Compare(metrics[i], "TimeInSeconds") == 0)
                    {
                        indexFrameStart = i;
                    }
                    if (string.Compare(metrics[i], "MsBetweenAppPresents") == 0 || string.Compare(metrics[i], "MsBetweenPresents") == 0)
                    {
                        indexFrameTimes = i;
                    }
                    if (string.Compare(metrics[i], "MsUntilDisplayed") == 0)
                    {
                        indexUntilDisplayedTimes = i;
                    }
                    if (string.Compare(metrics[i], "AppMissed") == 0 || string.Compare(metrics[i], "Dropped") == 0)
                    {
                        indexAppMissed = i;
                    }
                    if (string.Compare(metrics[i], "MsInPresentAPI") == 0)
                    {
                        indexMsInPresentAPI = i;
                    }
                    if (string.Compare(metrics[i], "MsBetweenDisplayChange") == 0)
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
                }

                var captureData = new SessionCaptureData(presentLines.Count());

                var dataLines = presentLines.ToArray();

                var presentModeMapping = Enum.GetValues(typeof(EPresentMode)).Cast<EPresentMode>().ToDictionary(e => e.GetDescription(), e => (int)e);
                for (int lineNo = 0; lineNo < dataLines.Count(); lineNo++)
                {
                    string line = dataLines[lineNo];
                    if (!line.Any())
                    {
                        continue;
                    }
                    var lineCharList = new List<char>();
                    string[] values = Array.Empty<string>();

                    if (lineNo == 0)
                    {
                        int isInner = -1;
                        for (int i = 0; i < line.Length; i++)
                        {
                            if (line[i] == '"')
                                isInner *= -1;

                            if (!(line[i] == ',' && isInner == 1))
                                lineCharList.Add(line[i]);

                        }

                        line = new string(lineCharList.ToArray());
                    }

                    values = line.Split(',');
                    double frameStart = 0;

                    if (lineNo == 0)
                    {
                        sessionRun.PresentMonRuntime = GetStringFromArray(values, indexRuntime);
                    }
                    if (indexFrameStart > 0 && indexFrameTimes > 0)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexFrameStart), NumberStyles.Any, CultureInfo.InvariantCulture, out frameStart)
                            && double.TryParse(GetStringFromArray(values, indexFrameTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var frameTime))
                        {
                            captureData.TimeInSeconds[lineNo] = frameStart;
                            captureData.MsBetweenPresents[lineNo] = frameTime;
                        }
                    }

                    if (indexAppMissed > 0)
                    {
                        if (int.TryParse(GetStringFromArray(values, indexAppMissed), NumberStyles.Any, CultureInfo.InvariantCulture, out var appMissed))
                        {
                            captureData.Dropped[lineNo] = Convert.ToBoolean(appMissed);
                        }
                        else
                        {
                            captureData.Dropped[lineNo] = true;
                        }
                    }

                    if (indexDisplayTimes > 0)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexDisplayTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var displayTime))
                        {
                            captureData.MsBetweenDisplayChange[lineNo] = displayTime;
                        }
                    }

                    if (indexUntilDisplayedTimes > 0)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexUntilDisplayedTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var untilDisplayTime))
                        {
                            captureData.MsUntilDisplayed[lineNo] = untilDisplayTime;
                        }
                    }

                    if (indexMsInPresentAPI > 0)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexMsInPresentAPI), NumberStyles.Any, CultureInfo.InvariantCulture, out var inPresentAPITime))
                        {
                            captureData.MsInPresentAPI[lineNo] = inPresentAPITime;
                        }
                    }

                    if (indexQPCTimes > 0)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexQPCTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var qPCTime))
                        {
                            captureData.QPCTime[lineNo] = qPCTime;
                        }
                    }

                    if (indexPresentMode > 0)
                    {
                        if (presentModeMapping.TryGetValue(GetStringFromArray(values, indexPresentMode), out var presentMode))
                        {
                            captureData.PresentMode[lineNo] = presentMode;
                        }
                    }

                    if (indexAllowsTearing > 0)
                    {
                        if (int.TryParse(GetStringFromArray(values, indexAllowsTearing), NumberStyles.Any, CultureInfo.InvariantCulture, out var allowsTearing))
                        {
                            captureData.AllowsTearing[lineNo] = allowsTearing;
                        }
                    }
                    if (indexSyncInterval > 0)
                    {
                        if (int.TryParse(GetStringFromArray(values, indexSyncInterval), NumberStyles.Any, CultureInfo.InvariantCulture, out var syncInterval))
                        {
                            captureData.SyncInterval[lineNo] = syncInterval;
                        }
                    }
                }
                sessionRun.CaptureData = captureData;
                return sessionRun;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error converting PresentData");
                throw;
            }
        }

        private void NormalizeStartTimesOfAggragationRuns(IEnumerable<ISessionRun> sessionRuns)
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

            if (sessionRuns.All(sr => sr.SensorData != null))
            {
                foreach (var sessionRun in sessionRuns)
                {
                    for (int i = 0; i < sessionRun.SensorData.BetweenMeasureTimes.Count(); i++)
                    {
                        lastSensorMeasureTime += sessionRun.SensorData.BetweenMeasureTimes[i];
                        sessionRun.SensorData.MeasureTime[i] = lastSensorMeasureTime;
                    }
                }
            }
            else
            {
                sessionRuns.ForEach(sr => sr.SensorData = null);
            }
        }
    }

    class HasIgnoreFlagException: Exception
    {

    }
}
