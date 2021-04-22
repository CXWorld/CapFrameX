using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using CapFrameX.Contracts.PresentMonInterface;
using CapFrameX.Extensions;
using Microsoft.Extensions.Logging;

namespace CapFrameX.PresentMonInterface
{
    public class PresentMonCaptureService : ICaptureService
    {
        public const int Application_INDEX = 0;
        public const int ProcessID_INDEX = 1;
        public const int TimeInSeconds_INDEX = 7;
        public const int MsBetweenPresents_INDEX = 9;
        public const int QPCTime_INDEX = 17;
        public const int VALID_LINE_LENGTH = 18;

        private readonly ISubject<string[]> _outputDataStream;
        private readonly object _listLock = new object();
        private readonly ILogger<PresentMonCaptureService> _logger;
        private HashSet<(string, int)> _presentMonProcesses;
        private bool _isUpdating;
        private IDisposable _hearBeatDisposable;
        private IDisposable _processNameDisposable;

        public IObservable<string[]> RedirectedOutputDataStream
            => _outputDataStream.AsObservable();
        public Subject<bool> IsCaptureModeActiveStream { get; }
        public Subject<bool> IsLoggingActiveStream { get; }

        public PresentMonCaptureService(ILogger<PresentMonCaptureService> logger)
        {
            _outputDataStream = new Subject<string[]>();
            IsCaptureModeActiveStream = new Subject<bool>();
            IsLoggingActiveStream = new Subject<bool>();
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
                    }
                };

                process.EnableRaisingEvents = true;
                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        var split = e.Data.Split(',');
                        if (split.Length == VALID_LINE_LENGTH)
                            _outputDataStream.OnNext(split);
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
                _logger.LogError(e, "Failed to Start CaptureService");
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
                return _presentMonProcesses?.Where(processInfo => !filter.Contains(processInfo.Item1));
        }

        public static void TryKillPresentMon()
        {
            try
            {
                var proc = Process.GetProcessesByName(CaptureServiceConfiguration.PresentMonAppName);
                if (proc.Any())
                {
                    proc[0].Kill();
                }
            }
            catch { }
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
                            processName = lineSplit[Application_INDEX].Replace(".exe", "");
                            processId = Convert.ToInt32(lineSplit[1]);
                        }
                        catch(Exception ex)
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
