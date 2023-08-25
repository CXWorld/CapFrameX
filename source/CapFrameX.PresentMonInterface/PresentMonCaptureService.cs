using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CapFrameX.Capture.Contracts;
using CapFrameX.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;

namespace CapFrameX.PresentMonInterface
{
    public class PresentMonCaptureService : ICaptureService
    {
        public const int ApplicationName_INDEX = 0;
        public const int ProcessID_INDEX = 1;
        public const int Dropped_INDEX = 6;
        public const int TimeInSeconds_INDEX = 7;
        public const int MsInPresentAPI_INDEX = 8;
		public const int MsBetweenPresents_INDEX = 9;
		public const int MsUntilRenderComplete_INDEX = 12;
		public const int UntilDisplayedTimes_INDEX = 13;
        // PresentMon version >=1.9
        public const int GpuActive_INDEX = 18;
        public const int QPCTime_INDEX = 19;
        // PresentMon version >=1.9
        public const int VALID_LINE_LENGTH = 20;

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

        public PresentMonCaptureService(ILogger<PresentMonCaptureService> logger)
        {
            _outputDataStream = new Subject<string[]>();
            IsCaptureModeActiveStream = new Subject<bool>();
            _presentMonProcesses = new HashSet<(string, int)>();
            _logger = logger;
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
                        RedirectStandardInput = true, // is it a MUST??
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
                        if (lineSplit.Length == VALID_LINE_LENGTH)
                        {
                            if (lineSplit[ApplicationName_INDEX] != "<error>" && !lineSplit[ApplicationName_INDEX].Contains(@"\"))
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
                        Arguments = "-terminate_existing",
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

                        try
                        {
                            processName = lineSplit[ApplicationName_INDEX].Replace(".exe", "");
                            processId = Convert.ToInt32(lineSplit[ProcessID_INDEX]);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error while extracting process name and ID from line split. {lineSplit}", string.Join(",", lineSplit));
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
