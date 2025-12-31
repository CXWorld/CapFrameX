using CapFrameX.Capture.Contracts;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace CapFrameX.PresentMonInterface
{
    public class PresentMonCaptureService : ICaptureService
    {
        // Column header with PC latency tracking (32 columns)
        public static readonly string COLUMN_HEADER_WITH_PC_LATENCY =
            "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
            "TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI,MsRenderPresentLatency," +
            "MsUntilDisplayed,MsPCLatency,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy,MsCPUWait,MsGPULatency,MsGPUTime,MsGPUBusy," +
            "MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay,EtwBufferFillPct,EtwBuffersInUse,EtwTotalBuffers,EtwEventsLost,EtwBuffersLost";

        // Column header without PC latency tracking (31 columns)
        public static readonly string COLUMN_HEADER_WITHOUT_PC_LATENCY =
            "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
            "TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI,MsRenderPresentLatency," +
            "MsUntilDisplayed,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy,MsCPUWait,MsGPULatency,MsGPUTime,MsGPUBusy," +
            "MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay,EtwBufferFillPct,EtwBuffersInUse,EtwTotalBuffers,EtwEventsLost,EtwBuffersLost";

        // Parsed column arrays for index lookup
        private static readonly string[] ColumnsWithPcLatency = COLUMN_HEADER_WITH_PC_LATENCY.Split(',');
        private static readonly string[] ColumnsWithoutPcLatency = COLUMN_HEADER_WITHOUT_PC_LATENCY.Split(',');

        // Fixed indices (same in both headers)
        public static readonly int ApplicationName_INDEX = Array.IndexOf(ColumnsWithPcLatency, "Application");
        public static readonly int ProcessID_INDEX = Array.IndexOf(ColumnsWithPcLatency, "ProcessID");
        public static readonly int SwapChainAddress_INDEX = Array.IndexOf(ColumnsWithPcLatency, "SwapChainAddress");
        public static readonly int MsBetweenPresents_INDEX = Array.IndexOf(ColumnsWithPcLatency, "MsBetweenPresents");
        public static readonly int MsBetweenDisplayChange_INDEX = Array.IndexOf(ColumnsWithPcLatency, "MsBetweenDisplayChange");
        public static readonly int MsPCLatency_INDEX = Array.IndexOf(ColumnsWithPcLatency, "MsPCLatency");

        private readonly IAppConfiguration _appConfiguration;

        // Dynamic indices - derived from the appropriate column header based on UsePcLatency setting
        private string[] CurrentColumns => _appConfiguration.UsePcLatency ? ColumnsWithPcLatency : ColumnsWithoutPcLatency;

        public int CPUStartQPCTimeInMs_INDEX => Array.IndexOf(CurrentColumns, "CPUStartQPCTimeInMs");
        public int StartTimeInMs_INDEX => Array.IndexOf(CurrentColumns, "CPUStartQPCTimeInMs");
        public int CpuBusy_INDEX => Array.IndexOf(CurrentColumns, "MsCPUBusy");
        public int GpuBusy_INDEX => Array.IndexOf(CurrentColumns, "MsGPUBusy");

        // Custom PresentMon build - ETW tracking columns
        public int EtwBufferFillPct_INDEX => Array.IndexOf(CurrentColumns, "EtwBufferFillPct");
        public int EtwBuffersInUse_INDEX => Array.IndexOf(CurrentColumns, "EtwBuffersInUse");
        public int EtwTotalBuffers_INDEX => Array.IndexOf(CurrentColumns, "EtwTotalBuffers");
        public int EtwEventsLost_INDEX => Array.IndexOf(CurrentColumns, "EtwEventsLost");
        public int EtwBuffersLost_INDEX => Array.IndexOf(CurrentColumns, "EtwBuffersLost");
        public int ValidLineLength => CurrentColumns.Length;

        public string ColumnHeader => _appConfiguration.UsePcLatency
            ? COLUMN_HEADER_WITH_PC_LATENCY
            : COLUMN_HEADER_WITHOUT_PC_LATENCY;

        private readonly ISubject<string[]> _outputDataStream;
        private readonly object _listLock = new object();
        private readonly ILogger<PresentMonCaptureService> _logger;
        private HashSet<(string, int)> _presentMonProcesses;
        private bool _isUpdating;
        private IDisposable _hearBeatDisposable;
        private IDisposable _processNameDisposable;

        public Dictionary<string, int> ParameterNameIndexMapping { get; }

        public IObservable<string[]> FrameDataStream
            => _outputDataStream.AsObservable();
        public Subject<bool> IsCaptureModeActiveStream { get; }

        public PresentMonCaptureService(ILogger<PresentMonCaptureService> logger, IAppConfiguration appConfiguration)
        {
            _outputDataStream = new Subject<string[]>();
            IsCaptureModeActiveStream = new Subject<bool>();
            _presentMonProcesses = new HashSet<(string, int)>();
            _logger = logger;
            _appConfiguration = appConfiguration;
        }

        public bool StartCaptureService(IServiceStartInfo startinfo)
        {
            if (!CaptureServiceInfo.IsCompatibleWithRunningOS)
            {
                return false;
            }

            try
            {
                TryKillPresentMon();
                SubscribeToPresentMonCapturedProcesses();

                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = startinfo.FileName,
                        Arguments = startinfo.Arguments,
                        UseShellExecute = startinfo.UseShellExecute,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        RedirectStandardInput = true, // is it necessary?
                        CreateNoWindow = startinfo.CreateNoWindow,
                        Verb = "runas",
                    },
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        var lineSplit = e.Data.Split(',');
                        if (lineSplit.Length == ValidLineLength)
                        {
                            if (lineSplit[ApplicationName_INDEX] != "<error>")
                            {
                                _outputDataStream.OnNext(lineSplit);
                            }
                        }
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                _logger.LogInformation("PresentMon successfully started");
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to start CaptureService");
                return false;
            }
        }

        public bool StopCaptureService()
        {
            _hearBeatDisposable?.Dispose();
            _processNameDisposable?.Dispose();

            try
            {
                lock (_listLock)
                    _presentMonProcesses?.Clear();

                TryKillPresentMon();
                return true;
            }
            catch { return false; }

        }

        public IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter)
        {
            lock (_listLock)
            {
                return _presentMonProcesses?.Where(processInfo => !filter.Contains(processInfo.Item1));
            }
        }

        public static void TryKillPresentMon()
        {
            try
            {
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine("PresentMon", $"{CaptureServiceConfiguration.PresentMonAppName}.exe"),
                        Arguments = "--terminate_existing_session",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        Verb = "runas",
                    }
                };

                process.Start();
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while killing PresentMon process.");
            }
        }

        private void SubscribeToPresentMonCapturedProcesses()
        {
            try
            {
                bool hasInitialData = false;
                _hearBeatDisposable = Observable.Generate(0, // dummy initialState
                    x => true, // dummy condition
                    x => x, // dummy iterate
                    x => x, // dummy resultSelector
                    x => TimeSpan.FromSeconds(1))
                    .Subscribe(x => UpdateProcessToCaptureList());

                _processNameDisposable = _outputDataStream
                    .Skip(1)
                    .ObserveOn(new EventLoopScheduler())
                    .Where(lineSplit => _isUpdating == false)
                    .Subscribe(lineSplit =>
                    {
                        if (!hasInitialData)
                        {
                            _logger.LogInformation("Process name stream has initial data.");
                            hasInitialData = true;
                        }

                        string processName = string.Empty;
                        int processId = 0;

                        if (lineSplit.Length > Math.Max(ApplicationName_INDEX, ProcessID_INDEX))
                        {
                            processName = lineSplit[ApplicationName_INDEX].Replace(".exe", "");

                            if (!int.TryParse(lineSplit[ProcessID_INDEX], out processId))
                            {
                                _logger.LogError("Failed to parse process ID from line split. {lineSplit}", string.Join(",", lineSplit));
                                return;
                            }
                        }
                        else
                        {
                            _logger.LogError("Invalid line split array length. {lineSplit}", string.Join(",", lineSplit));
                            return;
                        }

                        lock (_listLock)
                        {
                            var processInfo = (processName, processId);
                            if (processName != null && !_presentMonProcesses.Contains(processInfo))
                            {
                                _presentMonProcesses.Add(processInfo);
                            }
                        }
                    });
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to get process resources");
            }
        }

        private void UpdateProcessToCaptureList()
        {
            _isUpdating = true;
            var updatedList = new List<(string, int)>();

            lock (_listLock)
            {
                foreach (var processInfo in _presentMonProcesses)
                {
                    try
                    {
                        if (ProcessHelper.IsProcessAlive(processInfo.Item2))
                        {
                            updatedList.Add(processInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to get process resources from {processInfo.Item1}");
                    }
                }

                _presentMonProcesses = new HashSet<(string, int)>(updatedList);
            }

            _isUpdating = false;
        }
    }
}
