using CapFrameX.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.InteropServices;

namespace CapFrameX.PresentMonInterface
{
    /// <summary>
    /// The processes PresentMon reports presents for. A data row adds its process if the
    /// process is alive; a pass once per second drops the processes that exited. PresentMon
    /// can deliver rows after their process exited (on 2026-10-25 one nine seconds after a
    /// test app closed, under the name "&lt;unknown&gt;"). Listed again, the process stood
    /// beside the game until the next pass, and with two entries the automatic target
    /// selection picks none, which hid the overlay for up to a second.
    /// </summary>
    internal sealed class PresentMonProcessList
    {
        private static readonly TimeSpan PruneInterval = TimeSpan.FromSeconds(1);

        private readonly ILogger _logger;
        private readonly Func<int, bool> _isProcessAlive;
        private readonly Func<EventLoopScheduler> _createRowScheduler;
        private readonly IScheduler _pruneScheduler;
        private readonly object _listLock = new object();
        private HashSet<(string, int)> _processes = new HashSet<(string, int)>();
        // Rows whose process had exited, so that a burst of late rows costs one check; the next
        // pass clears it, after which a reused PID is checked again.
        private readonly HashSet<(string, int)> _exitedRows = new HashSet<(string, int)>();
        private volatile bool _isUpdating;
        private bool _hasInitialData;
        private IDisposable _pruneSubscription;
        private IDisposable _rowSubscription;
        private EventLoopScheduler _rowScheduler;

        public PresentMonProcessList(ILogger logger)
            : this(logger, ProcessHelper.IsProcessAlive, () => new EventLoopScheduler(),
                Scheduler.Default)
        {
        }

        internal PresentMonProcessList(ILogger logger, Func<int, bool> isProcessAlive,
            Func<EventLoopScheduler> createRowScheduler, IScheduler pruneScheduler)
        {
            _logger = logger;
            _isProcessAlive = isProcessAlive;
            _createRowScheduler = createRowScheduler;
            _pruneScheduler = pruneScheduler;
        }

        /// <summary>
        /// Follows the output of a newly started PresentMon instance, whose first row is the
        /// CSV header. A previous instance's subscription is released first.
        /// </summary>
        public void Start(IObservable<string[]> rows)
        {
            ReleaseSubscriptions();

            try
            {
                _hasInitialData = false;
                _pruneSubscription = Observable.Interval(PruneInterval, _pruneScheduler)
                    .Subscribe(_ => RemoveExitedProcesses());

                _rowScheduler = _createRowScheduler();
                _rowSubscription = rows
                    .Skip(1)
                    .ObserveOn(_rowScheduler)
                    .Where(lineSplit => _isUpdating == false)
                    .Subscribe(AddProcess);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to get process resources");
            }
        }

        public void Stop()
        {
            ReleaseSubscriptions();

            lock (_listLock)
            {
                _processes.Clear();
                _exitedRows.Clear();
            }
        }

        public IEnumerable<(string, int)> GetAllFilteredProcesses(HashSet<string> filter)
        {
            // A snapshot taken inside the lock. A lazy query ran after the lock was released,
            // while the row thread kept adding to the same set ("Collection was modified").
            lock (_listLock)
            {
                return _processes.Where(processInfo => !filter.Contains(processInfo.Item1)).ToArray();
            }
        }

        private void ReleaseSubscriptions()
        {
            _pruneSubscription?.Dispose();
            _pruneSubscription = null;
            _rowSubscription?.Dispose();
            _rowSubscription = null;
            // An EventLoopScheduler keeps its thread parked until it is disposed. Disposing only
            // the subscription leaked one thread per PresentMon restart.
            _rowScheduler?.Dispose();
            _rowScheduler = null;
        }

        private void AddProcess(string[] lineSplit)
        {
            if (!_hasInitialData)
            {
                _logger.LogInformation("Process name stream has initial data.");
                _hasInitialData = true;
            }

            string processName = lineSplit[PresentMonCaptureService.ApplicationName_INDEX].Replace(".exe", "");

            if (!int.TryParse(lineSplit[PresentMonCaptureService.ProcessID_INDEX], out int processId))
            {
                _logger.LogError("Failed to parse process ID from line split. {lineSplit}", string.Join(",", lineSplit));
                return;
            }

            lock (_listLock)
            {
                var processInfo = (processName, processId);
                if (processName == null || _processes.Contains(processInfo) ||
                    _exitedRows.Contains(processInfo))
                    return;

                // Only a new entry is checked, so the rows of a listed process cost a lookup.
                if (_isProcessAlive(processId))
                {
                    _processes.Add(processInfo);
                }
                else
                {
                    _exitedRows.Add(processInfo);
                    if (Environment.GetEnvironmentVariable("CFX_OSD_VERBOSE_LOG") == "1")
                        _logger.LogInformation(
                            "Process list: ignored a row of '{process}' (PID {pid}) - the process has exited",
                            processName, processId);
                }
            }
        }

        private void RemoveExitedProcesses()
        {
            _isUpdating = true;
            var updatedList = new List<(string, int)>();
            // Extended OSD logging (the switch ExtendedOsdLoggingController mirrors into the
            // process environment); evaluated once per 1 s pass, never per entry.
            bool logRemovals = Environment.GetEnvironmentVariable("CFX_OSD_VERBOSE_LOG") == "1";

            lock (_listLock)
            {
                foreach (var processInfo in _processes)
                {
                    try
                    {
                        if (_isProcessAlive(processInfo.Item2))
                        {
                            updatedList.Add(processInfo);
                        }
                        else if (logRemovals)
                        {
                            // A removal changes the detected list and may change the target, so
                            // every one is worth a line.
                            _logger.LogInformation(
                                "Process list: removed '{process}' (PID {pid}) - exited (Win32 error {error})",
                                processInfo.Item1, processInfo.Item2, Marshal.GetLastWin32Error());
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Failed to get process resources from {processInfo.Item1}");
                    }
                }

                _processes = new HashSet<(string, int)>(updatedList);
                _exitedRows.Clear();
            }

            _isUpdating = false;
        }
    }
}
