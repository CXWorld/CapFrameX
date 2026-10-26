using CapFrameX.Data.CaptureTarget;
using CapFrameX.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.ViewModel
{
    /// <summary>
    /// The automatic target selection: while the user selected no process, the capture and the
    /// overlays follow the process <see cref="CaptureTargetResolver"/> picks from the list.
    /// </summary>
    public partial class CaptureViewModel
    {
        private static readonly CaptureTargetResolution NoAutoTarget =
            new CaptureTargetResolution(null, ECaptureTargetReason.NoCandidate, "no process listed");

        // Kept while a single process is listed too, so that it stays the target when a second
        // one appears.
        private CaptureTargetResolution _autoTarget = NoAutoTarget;
        private readonly Dictionary<(string, int), bool> _gameLibraryCache = new Dictionary<(string, int), bool>();
        // By process name; cleared with every change of the process list (RefreshProcessListCaches).
        private readonly Dictionary<string, bool> _knownGameCache = new Dictionary<string, bool>();
        private string _loggedAutoTargetState;
        private ForegroundProcessTracker _foregroundTracker = new ForegroundProcessTracker();

        /// <summary>
        /// Active while the list has entries. A replaced tracker is disposed.
        /// </summary>
        internal ForegroundProcessTracker ForegroundTracker
        {
            get => _foregroundTracker;
            set
            {
                _foregroundTracker?.Dispose();
                _foregroundTracker = value;
            }
        }

        internal Func<int, string> ImagePathProvider { get; set; } =
            processId => ProcessHelper.TryGetImagePath(processId, out string imagePath) ? imagePath : null;

        /// <summary>
        /// Updates the automatic target for the processes listed now: with every list update
        /// (once per second) and when the selection is cleared. The rules only run while at least
        /// two processes are listed and the user selected none.
        /// </summary>
        private void UpdateAutoTarget()
        {
            var processes = ProcessesInfo.ToArray();
            // The hook follows the foreground without polling, and only while something is listed.
            ForegroundTracker.SetActive(processes.Length > 0);

            if (!string.IsNullOrWhiteSpace(SelectedProcessToCapture))
            {
                // The selection decides. Clearing it resolves again (SelectedProcessToCapture).
                _autoTarget = NoAutoTarget;
                _loggedAutoTargetState = null;
                return;
            }

            if (processes.Length < 2)
            {
                _autoTarget = processes.Length == 0 ? NoAutoTarget : SingleTarget(processes[0]);
                _loggedAutoTargetState = null;
                return;
            }

            var candidates = CreateCandidates(processes, foregroundNow: 0);
            _autoTarget = CaptureTargetResolver.Resolve(candidates, _lastPublishedProcessId, captureRequest: false);
            LogAutoTarget("Capture target", candidates);
        }

        // Kept while the same process stays alone, so the update allocates nothing per second.
        private CaptureTargetResolution SingleTarget((string, int) process)
        {
            var current = _autoTarget.Target;
            if (_autoTarget.Reason == ECaptureTargetReason.OnlyCandidate &&
                current.Name == process.Item1 && current.ProcessId == process.Item2)
                return _autoTarget;

            return new CaptureTargetResolution(new CaptureTargetCandidate(process.Item1, process.Item2),
                ECaptureTargetReason.OnlyCandidate, "only listed process");
        }

        /// <summary>
        /// The process a capture request starts: the selected one, else the only listed one, else
        /// the automatic target with the process in the foreground first - the hotkey is pressed
        /// in the game. A target that
        /// differs from the current one becomes the automatic target, so the overlays follow it.
        /// Name null when nothing decides.
        /// </summary>
        private (string, int) GetCaptureRequestTarget()
        {
            if (!string.IsNullOrWhiteSpace(SelectedProcessToCapture))
                return (SelectedProcessToCapture,
                    ProcessesInfo.FirstOrDefault(info => info.Item1 == SelectedProcessToCapture).Item2);

            var processes = ProcessesInfo.ToArray();
            if (processes.Length < 2)
                return processes.Length == 1 ? processes[0] : default;

            var candidates = CreateCandidates(processes, ForegroundTracker.ForegroundProcessId);
            var resolution = CaptureTargetResolver.Resolve(candidates, _lastPublishedProcessId, captureRequest: true);
            if (resolution.Target == null)
            {
                LogAutoTarget("Capture request", candidates, resolution);
                return default;
            }

            if (resolution.Target.ProcessId != _autoTarget.Target?.ProcessId)
            {
                _autoTarget = resolution;
                LogAutoTarget("Capture request", candidates);
                OnSelectedProcessToCaptureChanged();
            }

            return (resolution.Target.Name, resolution.Target.ProcessId);
        }

        private List<CaptureTargetCandidate> CreateCandidates((string, int)[] processes, int foregroundNow)
        {
            var candidates = processes
                .Select(process => new CaptureTargetCandidate(process.Item1, process.Item2))
                .ToList();

            // A capture request may come before the list update that follows a change.
            RefreshProcessListCaches();
            var activity = _captureManager.GetProcessActivity();
            foreach (var candidate in candidates)
            {
                candidate.Activity = activity.FirstOrDefault(entry =>
                    entry.ProcessId == candidate.ProcessId && entry.Name == candidate.Name);
                candidate.SecondsSinceForeground = ForegroundTracker.SecondsSinceForeground(candidate.ProcessId);
                candidate.IsForegroundNow = foregroundNow != 0 && candidate.ProcessId == foregroundNow;
                candidate.IsKnownGame = IsKnownGame(candidate.Name);
                candidate.IsInGameLibrary = IsInGameLibrary(candidate.Name, candidate.ProcessId);
            }

            foreach (var forgotten in _gameLibraryCache.Keys.Where(key => !processes.Contains(key)).ToArray())
                _gameLibraryCache.Remove(forgotten);
            foreach (var forgotten in _knownGameCache.Keys.Where(name => !processes.Any(process => process.Item1 == name)).ToArray())
                _knownGameCache.Remove(forgotten);

            return candidates;
        }

        private bool IsKnownGame(string processName)
        {
            // FindProcessByName copies and scans the whole list, about a thousand entries.
            if (_knownGameCache.TryGetValue(processName, out bool known))
                return known;

            var entry = _processList.FindProcessByName(processName);
            known = entry != null && !entry.IsBlacklisted && !string.IsNullOrWhiteSpace(entry.DisplayName);
            _knownGameCache[processName] = known;
            return known;
        }

        private bool IsInGameLibrary(string processName, int processId)
        {
            if (_gameLibraryCache.TryGetValue((processName, processId), out bool inLibrary))
                return inLibrary;

            try
            {
                inLibrary = CaptureTargetResolver.IsGameLibraryPath(ImagePathProvider(processId));
            }
            catch (Exception)
            {
                inLibrary = false;
            }
            _gameLibraryCache[(processName, processId)] = inLibrary;
            return inLibrary;
        }

        /// <summary>
        /// One line per change of the target or of the listed processes while several are
        /// listed, with what every candidate measured: the material to calibrate the thresholds.
        /// </summary>
        private void LogAutoTarget(string prefix, IReadOnlyList<CaptureTargetCandidate> candidates,
            CaptureTargetResolution resolution = null)
        {
            resolution = resolution ?? _autoTarget;
            if (candidates.Count < 2)
            {
                _loggedAutoTargetState = null;
                return;
            }

            string state = $"{resolution.Target?.ProcessId ?? 0}|" +
                string.Join(",", candidates.Select(candidate => candidate.ProcessId).OrderBy(processId => processId));
            if (prefix == "Capture target" && state == _loggedAutoTargetState)
                return;
            _loggedAutoTargetState = state;

            string decision = resolution.Target == null
                ? $"none of {candidates.Count} processes"
                : $"'{resolution.Target.Name}' (PID {resolution.Target.ProcessId})";
            _logger.LogInformation("{prefix}: {decision} - {reason}: {detail}. Candidates: {candidates}",
                prefix, decision, resolution.Reason, resolution.Detail,
                string.Join("; ", candidates.Select(CaptureTargetResolver.Describe)));
        }
    }
}
