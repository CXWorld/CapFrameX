using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CapFrameX.Data
{
    [StructLayout(LayoutKind.Sequential)]
    public struct WindowRect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    public class RecordManager : IRecordManager
    {
        private const string IGNOREFLAGMARKER = "//Ignore=true";

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
        private PubSubEvent<ViewMessages.UpdateSystemInfo> _updateSystemInfoEvent;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool GetWindowRect(IntPtr hWnd, ref WindowRect Rect);

        public RecordManager(ILogger<RecordManager> logger,
            IAppConfiguration appConfiguration,
            IRecordDirectoryObserver recordObserver,
            IAppVersionProvider appVersionProvider,
            ISensorService sensorService,
            ISystemInfo systemInfo,
            ProcessList processList,
            IRTSSService rTSSService,
            IEventAggregator eventAggregator)
        {
            _logger = logger;
            _appConfiguration = appConfiguration;
            _recordObserver = recordObserver;
            _appVersionProvider = appVersionProvider;
            _sensorService = sensorService;
            _systemInfo = systemInfo;
            _processList = processList;
            _rTSSService = rTSSService;
            _eventAggregator = eventAggregator;
            _updateSystemInfoEvent = _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSystemInfo>>();
        }

        public void UpdateCustomData(IFileRecordInfo recordInfo,
            string customCpuInfo, string customGpuInfo,
            string customRamInfo, string customGameName,
            string customComment)
        {
            if (recordInfo == null) return;

            customCpuInfo = customCpuInfo ?? string.Empty;
            customGpuInfo = customGpuInfo ?? string.Empty;
            customRamInfo = customRamInfo ?? string.Empty;
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

        private ISession LoadSessionFromCSV(FileInfo csvFile)
        {
            // Exception: ignore Nv FrameView summary file
            if (csvFile.Name == "FrameView_Summary.csv")
                return null;

            // Ignore new PresentMon stats files
            if (csvFile.Name.Contains("-stats.csv"))
                return null;

            using (var reader = new StreamReader(csvFile.FullName))
            {
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
            if (index < array.Length && index > -1)
            {
                return array[index];
            }
            return string.Empty;
        }

        public async Task<IFileRecordInfo> GetFileRecordInfo(FileInfo fileInfo)
        {
            return await Observable.Timer(_fileAccessIntervalTimespan)
                .SelectMany(_ =>
                {
                    switch (fileInfo.Extension)
                    {
                        case ".csv":
                            var sessionFromCSV = LoadSessionFromCSV(fileInfo);

                            if (sessionFromCSV == null)
                                return Observable.Empty<IFileRecordInfo>();

                            return Observable.Return(FileRecordInfo.Create(fileInfo, sessionFromCSV.Hash));
                        case ".json":
                            var sessionFromJSON = LoadSessionFromJSON(fileInfo);

                            if (sessionFromJSON == null)
                                return Observable.Empty<IFileRecordInfo>();

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
                .Do(fileRecordInfo =>
                {
                    if (fileRecordInfo is IFileRecordInfo)
                    {
                        if (fileRecordInfo.ProcessName == fileRecordInfo.GameName)
                        {
                            fileRecordInfo.GameName = GetGameNameFromProcessList(fileRecordInfo.ProcessName);
                        }
                        else
                        {
                            if (fileRecordInfo.GameName.IsNullOrEmpty())
                            {
                                fileRecordInfo.GameName = GetGameNameFromProcessList(fileRecordInfo.ProcessName);
                            }
                        }
                    }
                });
        }

        public async Task SavePresentmonRawToFile(IEnumerable<string> lines, string process, string recordDirectory = null)
        {
            try
            {
                var filePath = await GetOutputFilename(process, recordDirectory);
                lines = new string[] { IGNOREFLAGMARKER, PresentMonCaptureService.COLUMN_HEADER }.Concat(lines);
                File.WriteAllLines(filePath + ".csv", lines);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while saving PresentMon raw file.");
            }
        }

        public async Task<bool> SaveSessionRunsToFile(IEnumerable<ISessionRun> runs, string processName, string comment, string recordDirectory = null, List<ISessionInfo> HWInfo = null)
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


                if (HWInfo != null)
                {
                    cpuInfo = HWInfo?.First().Processor;
                    gpuInfo = HWInfo?.First().GPU;
                    ramInfo = HWInfo?.First().SystemRam;
                    mbInfo = HWInfo?.First().Motherboard;
                    osInfo = HWInfo?.First().OS;
                    gpuDriverInfo = HWInfo?.First().GPUDriverVersion;
                    appVersion = HWInfo?.First().AppVersion;
                    apiInfo = HWInfo?.First().ApiInfo;
                    resizableBar = HWInfo?.First().ResizableBar;
                    winGameMode = HWInfo?.First().WinGameMode;
                    hAGS = HWInfo?.First().HAGS;
                    resolutionInfo = HWInfo?.First().ResolutionInfo;
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
                    }
                    else
                    {
                        cpuInfo = _systemInfo.GetProcessorName();
                        gpuInfo = _systemInfo.GetGraphicCardName();
                        ramInfo = _systemInfo.GetSystemRAMInfoName();
                    }

                    mbInfo = _systemInfo.GetMotherboardName();
                    osInfo = _systemInfo.GetOSVersion();
                    gpuDriverInfo = _sensorService.GetGpuDriverVersion();
                    appVersion = _appVersionProvider.GetAppVersion();

                    var process = Process.GetProcessesByName(processName).FirstOrDefault();
                    apiInfo = process != null ? _rTSSService.GetApiInfo(process.Id) : "unknown";

                    if (apiInfo == "unknown")
                        apiInfo = runs.First().PresentMonRuntime;


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

                    // ToDo: muste be improved, doesn't work in many cases
                    //string resolutionInfo = "unknown";
                    //if (process != null)
                    //{
                    //    WindowRect wndRect = new WindowRect();
                    //    GetWindowRect(process.MainWindowHandle, ref wndRect);
                    //    resolutionInfo = $"{ wndRect.right - wndRect.left}x{wndRect.bottom - wndRect.top}";
                    //}
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
                        //ResolutionInfo = resolutionInfo
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

        private string GetGameNameFromProcessList(string processName)
        {
            if (string.IsNullOrWhiteSpace(processName))
            {
                return "Unknown";
            }

            var processNameExtStripped = processName.StripExeExtension();
            return _processList.FindProcessByName(processName)?.DisplayName ?? processNameExtStripped;
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
                int indexPcLatency = -1;
                int indexmsGPUActive = -1;
                int indexCPUStartQPCTime = -1;

                string headerLine;
                string firstLine = presentLines.First();

                if (firstLine.Contains("frametime") || firstLine.StartsWith("Application"))
                {
                    headerLine = firstLine;
                    presentLines = presentLines.Skip(1);
                }
                else
                {
                    headerLine = PresentMonCaptureService.COLUMN_HEADER;
                }

                var sessionRun = new SessionRun()
                {
                    Hash = string.Join(",", presentLines).GetSha1(),
                    PresentMonRuntime = "unknown"
                };

                // $"Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing," +
                // $"PresentMode,CPUStartQPCTime,FrameTime,CPUBusy,CPUWait,GPULatency,GPUTime,GPUBusy,GPUWait,DisplayLatency," +
                // $"DisplayedTime,AnimationError";

                var metrics = Array.ConvertAll(headerLine.Split(','), p => p.Trim());
                for (int i = 0; i < metrics.Count(); i++)
                {
                    if (string.Compare(metrics[i], "AppRenderStart") == 0 || string.Compare(metrics[i], "TimeInSeconds") == 0)
                    {
                        indexFrameStart = i;
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
                    if (string.Compare(metrics[i], "MsPCLatency") == 0)
                    {
                        indexPcLatency = i;
                    }
                    if (string.Compare(metrics[i], "msGPUActive") == 0 || string.Compare(metrics[i], "GPUBusy") == 0)
                    {
                        indexmsGPUActive = i;
                    }
                    if (string.Compare(metrics[i], "CPUStartQPCTime") == 0)
                    {
                        indexCPUStartQPCTime = i;
                    }
                }

                var captureData = new SessionCaptureData(presentLines.Count());

                // When latency data is available initialize array
                if (indexPcLatency > 0)
                {
                    captureData.PcLatency = new double[presentLines.Count()];
                }

                var dataLines = presentLines.ToArray();

                var presentModeMapping = Enum.GetValues(typeof(EPresentMode)).Cast<EPresentMode>()
                    .ToDictionary(e => e.GetDescription(), e => (int)e);
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

                    if (indexFrameStart > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexFrameStart), NumberStyles.Any, CultureInfo.InvariantCulture, out frameStart))
                        {
                            captureData.TimeInSeconds[lineNo] = frameStart;
                        }
                    }

                    if (indexCPUStartQPCTime > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexCPUStartQPCTime), NumberStyles.Any, CultureInfo.InvariantCulture, out frameStart))
                        {
                            captureData.TimeInSeconds[lineNo] = frameStart * 1E-03;
                        }
                    }

                    if (indexFrameTimes > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexFrameTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var frameTime))
                        {
                            captureData.MsBetweenPresents[lineNo] = frameTime;
                        }
                    }

                    if (indexAppMissed > -1)
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

                    if (indexDisplayTimes > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexDisplayTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var displayTime))
                        {
                            captureData.MsBetweenDisplayChange[lineNo] = displayTime;
                        }
                    }

                    if (indexUntilDisplayedTimes > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexUntilDisplayedTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var untilDisplayTime))
                        {
                            captureData.MsUntilDisplayed[lineNo] = untilDisplayTime;
                        }
                    }

                    if (indexMsInPresentAPI > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexMsInPresentAPI), NumberStyles.Any, CultureInfo.InvariantCulture, out var inPresentAPITime))
                        {
                            captureData.MsInPresentAPI[lineNo] = inPresentAPITime;
                        }
                    }

                    if (indexQPCTimes > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexQPCTimes), NumberStyles.Any, CultureInfo.InvariantCulture, out var qPCTime))
                        {
                            captureData.QPCTime[lineNo] = qPCTime;
                        }
                    }

                    if (indexPresentMode > -1)
                    {
                        if (presentModeMapping.TryGetValue(GetStringFromArray(values, indexPresentMode), out var presentMode))
                        {
                            captureData.PresentMode[lineNo] = presentMode;
                        }
                    }

                    if (indexAllowsTearing > -1)
                    {
                        if (int.TryParse(GetStringFromArray(values, indexAllowsTearing), NumberStyles.Any, CultureInfo.InvariantCulture, out var allowsTearing))
                        {
                            captureData.AllowsTearing[lineNo] = allowsTearing;
                        }
                    }
                    if (indexSyncInterval > -1)
                    {
                        if (int.TryParse(GetStringFromArray(values, indexSyncInterval), NumberStyles.Any, CultureInfo.InvariantCulture, out var syncInterval))
                        {
                            captureData.SyncInterval[lineNo] = syncInterval;
                        }
                    }
                    if (indexPcLatency > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexPcLatency), NumberStyles.Any, CultureInfo.InvariantCulture, out var pcLatency))
                        {
                            captureData.PcLatency[lineNo] = pcLatency;
                        }
                        else
                        {
                            captureData.PcLatency[lineNo] = double.NaN;
                        }
                    }
                    if (indexmsGPUActive > -1)
                    {
                        if (double.TryParse(GetStringFromArray(values, indexmsGPUActive), NumberStyles.Any, CultureInfo.InvariantCulture, out var gpuActive))
                        {
                            captureData.GpuActive[lineNo] = gpuActive;
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
