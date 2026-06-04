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
        public static readonly string COLUMN_HEADER_WITH_PC_LATENCY =
            "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
            "FrameType,TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI,MsRenderPresentLatency," +
            "MsUntilDisplayed,MsPCLatency,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy,MsCPUWait,MsGPULatency,MsGPUTime,MsGPUBusy," +
            "MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay,MsInstrumentedLatency,EtwBufferFillPct,EtwBuffersInUse,EtwTotalBuffers,EtwEventsLost,EtwBuffersLost";

        public static readonly string COLUMN_HEADER_WITHOUT_PC_LATENCY =
            "Application,ProcessID,SwapChainAddress,PresentRuntime,SyncInterval,PresentFlags,AllowsTearing,PresentMode," +
            "FrameType,TimeInSeconds,MsBetweenSimulationStart,MsBetweenPresents,MsBetweenDisplayChange,MsInPresentAPI,MsRenderPresentLatency," +
            "MsUntilDisplayed,CPUStartQPCTimeInMs,MsBetweenAppStart,MsCPUBusy,MsCPUWait,MsGPULatency,MsGPUTime,MsGPUBusy," +
            "MsGPUWait,MsAnimationError,AnimationTime,MsFlipDelay,MsInstrumentedLatency,EtwBufferFillPct,EtwBuffersInUse,EtwTotalBuffers,EtwEventsLost,EtwBuffersLost";

        private static readonly PresentMonColumnLayout ColumnLayoutWithPcLatency =
            new PresentMonColumnLayout(COLUMN_HEADER_WITH_PC_LATENCY, true);

        private static readonly PresentMonColumnLayout ColumnLayoutWithoutPcLatency =
            new PresentMonColumnLayout(COLUMN_HEADER_WITHOUT_PC_LATENCY, false);

        // Fixed indices before the optional PC latency column. MsPCLatency is only available in the PC latency layout.
        public static readonly int ApplicationName_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "Application");
        public static readonly int ProcessID_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "ProcessID");
        public static readonly int SwapChainAddress_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "SwapChainAddress");
        public static readonly int MsBetweenPresents_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "MsBetweenPresents");
        public static readonly int MsBetweenDisplayChange_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "MsBetweenDisplayChange");
        public static readonly int MsPCLatency_INDEX = Array.IndexOf(ColumnLayoutWithPcLatency.Columns, "MsPCLatency");

        private readonly IAppConfiguration _appConfiguration;

        private PresentMonColumnLayout _activeColumnLayout;

        // Dynamic indices - derived from the active capture layout or current configuration.
        private PresentMonColumnLayout CurrentColumnLayout =>
            _activeColumnLayout ?? GetColumnLayout(_appConfiguration.UsePcLatency);

        public int CPUStartQPCTimeInMs_Index => Array.IndexOf(CurrentColumnLayout.Columns, "CPUStartQPCTimeInMs");
        public int StartTimeInMs_INDEX => Array.IndexOf(CurrentColumnLayout.Columns, "CPUStartQPCTimeInMs");
        public int CpuBusy_Index => Array.IndexOf(CurrentColumnLayout.Columns, "MsCPUBusy");
        public int GpuBusy_Index => Array.IndexOf(CurrentColumnLayout.Columns, "MsGPUBusy");
        public int AnimationError_Index => Array.IndexOf(CurrentColumnLayout.Columns, "MsAnimationError");

        // Custom PresentMon build - ETW tracking columns
        public int EtwBufferFillPct_Index => Array.IndexOf(CurrentColumnLayout.Columns, "EtwBufferFillPct");
        public int EtwBuffersInUse_Index => Array.IndexOf(CurrentColumnLayout.Columns, "EtwBuffersInUse");
        public int EtwTotalBuffers_Index => Array.IndexOf(CurrentColumnLayout.Columns, "EtwTotalBuffers");
        public int EtwEventsLost_Index => Array.IndexOf(CurrentColumnLayout.Columns, "EtwEventsLost");
        public int EtwBuffersLost_Index => Array.IndexOf(CurrentColumnLayout.Columns, "EtwBuffersLost");
        public int ValidLineLength => CurrentColumnLayout.ValidLineLength;

        public string ColumnHeader => CurrentColumnLayout.ColumnHeader;

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
                var captureColumnLayout = GetColumnLayout(IsPcLatencyTrackingEnabled(startinfo));
                _activeColumnLayout = captureColumnLayout;

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
                        if (HasValidLineLength(lineSplit, captureColumnLayout))
                        {
                            if (lineSplit[ApplicationName_INDEX] != "<error>")
                            {
                                _outputDataStream.OnNext(lineSplit);
                            }
                        }
                        else
                        {
                            LogInvalidLineLength(lineSplit.Length, captureColumnLayout, e.Data);
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
                _activeColumnLayout = null;
                _logger.LogError(e, "Failed to start CaptureService");
                return false;
            }
        }

        public bool StopCaptureService()
        {
            _hearBeatDisposable?.Dispose();
            _processNameDisposable?.Dispose();
            _activeColumnLayout = null;

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
                        var currentColumnLayout = CurrentColumnLayout;

                        if (!HasValidLineLength(lineSplit, currentColumnLayout))
                        {
                            LogInvalidLineLength(lineSplit.Length, currentColumnLayout, string.Join(",", lineSplit));
                            return;
                        }

                        processName = lineSplit[ApplicationName_INDEX].Replace(".exe", "");

                        if (!int.TryParse(lineSplit[ProcessID_INDEX], out processId))
                        {
                            _logger.LogError("Failed to parse process ID from line split. {lineSplit}", string.Join(",", lineSplit));
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

        private static PresentMonColumnLayout GetColumnLayout(bool usePcLatency)
        {
            return usePcLatency ? ColumnLayoutWithPcLatency : ColumnLayoutWithoutPcLatency;
        }

        private static bool IsPcLatencyTrackingEnabled(IServiceStartInfo startinfo)
        {
            return (startinfo?.Arguments?.IndexOf("--track_pc_latency", StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
        }

        private static bool HasValidLineLength(string[] lineSplit, PresentMonColumnLayout columnLayout)
        {
            return lineSplit?.Length == columnLayout.ValidLineLength;
        }

        private void LogInvalidLineLength(int actualLineLength, PresentMonColumnLayout columnLayout, string line)
        {
            _logger.LogError(
                "Received PresentMon output line with invalid column count. Expected {ExpectedLineLength}, actual {ActualLineLength}, UsePcLatency {UsePcLatency}. Line: {Line}",
                columnLayout.ValidLineLength,
                actualLineLength,
                columnLayout.UsePcLatency,
                line);
        }

        private sealed class PresentMonColumnLayout
        {
            public PresentMonColumnLayout(string columnHeader, bool usePcLatency)
            {
                ColumnHeader = columnHeader;
                Columns = columnHeader.Split(',');
                ValidLineLength = Columns.Length;
                UsePcLatency = usePcLatency;
            }

            public string ColumnHeader { get; }

            public string[] Columns { get; }

            public int ValidLineLength { get; }

            public bool UsePcLatency { get; }
        }
    }
}
