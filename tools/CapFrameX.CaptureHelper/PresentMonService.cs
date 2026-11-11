using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace CapFrameX.CaptureHelper
{
    /// <summary>
    /// Runs PresentMon 1.10.0 for every process and reads its CSV rows from stdout. ETW hands the rows
    /// over in batches, up to about two seconds after their present, so a capture is buffered as in the
    /// main app's CaptureManager and cut by the present time (QPCTime) afterwards: an archive keeps the
    /// last rows of every process, a capture collects every row of its target from the start request
    /// on, waits 2.5 s after its end for the late rows and then takes its frames by CaptureWindow.
    /// PresentMon's session clock is the QPC, the clock of Stopwatch.GetTimestamp().
    /// The target filter is applied here, not in PresentMon: -process_id would need a PresentMon
    /// restart for every target, after which the first rows arrived only 1 to 4 s later (measured with
    /// vkcube), and the process list would lose every other process. -process_name only filters
    /// PresentMon's output.
    /// </summary>
    internal sealed class PresentMonService : IDisposable
    {
        private const string SessionName = "CapFrameXCaptureHelper";

        // As CaptureManager.ARCHIVE_LENGTH.
        private const int ArchiveLength = 500;

        // As CaptureManager.PRESICE_OFFSET: a capture is cut this long after its end, when the rows of
        // its last frames have arrived.
        private static readonly long GatherOffsetTicks = Stopwatch.Frequency * 5 / 2;

        // A set duration counts as run when the capture was stopped at most this much earlier, as in
        // GetAdjustedCaptureData.
        private static readonly long DurationToleranceTicks = Stopwatch.Frequency / 5;

        // Processes without a row for this long leave the process list.
        private static readonly long ProcessTimeoutTicks = 3 * Stopwatch.Frequency;

        private readonly object _gate = new object();
        private readonly Dictionary<int, ProcessEntry> _processes = new Dictionary<int, ProcessEntry>();
        private readonly RowArchive _archive = new RowArchive(ArchiveLength);
        private volatile Process _process;
        private string _header;
        private int _applicationColumn = -1, _processIdColumn = -1, _qpcColumn = -1;
        private Capture _capture;
        private string _lastError;

        public PresentMonService(string exePath) => ExePath = exePath;

        public string ExePath { get; }

        /// <summary>PresentMon exited without being stopped. Raised on a thread-pool thread.</summary>
        public event EventHandler Exited;

        public bool IsRunning
        {
            get
            {
                var process = _process;
                try { return process != null && !process.HasExited; }
                catch (InvalidOperationException) { return false; }
            }
        }

        /// <summary>The last "error: ..." line PresentMon wrote to stderr.</summary>
        public string LastError { get { lock (_gate) return _lastError; } }

        public bool IsCapturing { get { lock (_gate) return _capture != null; } }

        public void Start()
        {
            Stop();
            EndLeftoverInstances();

            // -no_track_display: a present counts as complete once the system took it over, without
            // waiting until it was displayed. The frame times need nothing more, and 1.10.0 does not
            // know the flip events of current Windows 11 builds, where it completes hardly any present
            // with display tracking. OpenGL and Vulkan games still show up through DxgKrnl's
            // PresentHistory events, which 1.10.0 also enables without display tracking, on Windows 7
            // through the Win7 DxgKrnl provider.
            var arguments = string.Join(" ",
                "-session_name", SessionName, "-stop_existing_session",
                "-output_stdout", "-qpc_time", "-no_track_display",
                "-exclude", "dwm.exe", "-exclude", Quote(OwnExecutableName));
            var startInfo = new ProcessStartInfo(ExePath, arguments)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.Default,
                StandardErrorEncoding = Encoding.Default,
                WorkingDirectory = Path.GetDirectoryName(ExePath),
            };

            lock (_gate)
            {
                _header = null;
                _applicationColumn = _processIdColumn = _qpcColumn = -1;
                _processes.Clear();
                _archive.Clear();
                _capture = null;
                _lastError = null;
            }

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += OnOutputLine;
            process.ErrorDataReceived += OnErrorLine;
            process.Exited += OnProcessExited;
            // Current before it starts, so that an exit right after the start still counts.
            _process = process;
            try
            {
                process.Start();
            }
            catch
            {
                _process = null;
                process.Dispose();
                throw;
            }
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            Log.Write($"PresentMon started (pid {process.Id}): \"{ExePath}\" {arguments}");
        }

        public void Stop()
        {
            var process = _process;
            if (process == null) return;
            // From here on its exit is expected, so OnProcessExited reports nothing.
            _process = null;
            try
            {
                if (!process.HasExited)
                {
                    // PresentMon ends by itself once its ETW session is stopped. Killing it would leave the
                    // session running, which the PresentMon README names as a cause of instability on
                    // Windows 7. The output reader keeps draining stdout meanwhile, so PresentMon cannot
                    // block on a full pipe while it shuts down.
                    StopSession();
                    if (!process.WaitForExit(5000))
                    {
                        Log.Write("PresentMon did not exit after its session was stopped; ending it.");
                        process.Kill();
                        process.WaitForExit(2000);
                    }
                }
                Log.Write($"PresentMon stopped (exit code {process.ExitCode}).");
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is Win32Exception)
            {
                Log.Write("Stopping PresentMon failed: " + ex.Message);
            }
            finally
            {
                process.Dispose();
            }
        }

        public void Dispose() => Stop();

        /// <summary>
        /// The processes that delivered rows within the last three seconds, with the frame rate of
        /// the rows that arrived since the previous call.
        /// </summary>
        public IReadOnlyList<ProcessActivity> PollProcesses()
        {
            var now = Stopwatch.GetTimestamp();
            var result = new List<ProcessActivity>();
            lock (_gate)
            {
                var stale = new List<int>();
                foreach (var entry in _processes.Values)
                {
                    if (now - entry.LastArrival > ProcessTimeoutTicks)
                    {
                        stale.Add(entry.ProcessId);
                        continue;
                    }
                    // ETW hands rows over in batches of about a second, so the rate comes from the
                    // QPC span of the batch, and a poll between two batches keeps the previous rate.
                    if (entry.Rows >= 2 && entry.LastQpc > entry.FirstQpc)
                        entry.Fps = (entry.Rows - 1) * (double)Stopwatch.Frequency / (entry.LastQpc - entry.FirstQpc);
                    entry.Rows = 0;
                    result.Add(new ProcessActivity(entry.ProcessId, entry.Name, entry.Fps));
                }
                foreach (var processId in stale)
                    _processes.Remove(processId);
            }
            return result;
        }

        public void StartCapture(int processId, string processName, int durationSeconds, bool openEnded)
        {
            var start = Stopwatch.GetTimestamp();
            lock (_gate)
            {
                // As in the main app, the capture starts with the target's rows of the archive. Frames
                // presented shortly before the request arrive after it and land in the capture anyway; the
                // archive adds the earlier ones, so the frame before the start, the reference of the window,
                // is there even after a pause in the target's frames. Archive and capture meet under this
                // lock, with neither a gap nor an overlap between them (the main app removes an overlap by
                // QPC, because its archive fills on until its asynchronous capture subscription runs).
                var capture = new Capture(processId, processName, start, durationSeconds, openEnded);
                _archive.CopyTo(processId, capture.Rows);
                capture.ArchivedRows = capture.Rows.Count;
                _capture = capture;
            }
        }

        /// <summary>Ends the capture now instead of at its set end; rows still on their way are kept.</summary>
        public void RequestCaptureEnd()
        {
            var now = Stopwatch.GetTimestamp();
            lock (_gate)
            {
                if (_capture != null && now < _capture.StopQpc)
                    _capture.StopQpc = now;
            }
        }

        public CaptureProgress GetCaptureProgress()
        {
            var now = Stopwatch.GetTimestamp();
            lock (_gate)
            {
                var capture = _capture;
                if (capture == null) return null;
                var elapsed = (Math.Min(now, capture.StopQpc) - capture.StartQpc) / (double)Stopwatch.Frequency;
                return new CaptureProgress(capture.ProcessId, capture.ProcessName, elapsed, capture.DurationSeconds,
                    capture.OpenEnded, now >= capture.StopQpc, capture.Rows.Count);
            }
        }

        /// <summary>
        /// Ends the capture 2.5 s after its end, when the rows of its last frames have arrived, and cuts
        /// its frames by their present time; <paramref name="force"/> ends it at once. Null while the
        /// capture runs or without one.
        /// </summary>
        public CaptureResult TryFinishCapture(bool force)
        {
            var now = Stopwatch.GetTimestamp();
            Capture capture;
            string header;
            lock (_gate)
            {
                capture = _capture;
                if (capture == null) return null;
                if (!force && now - capture.StopQpc < GatherOffsetTicks) return null;
                if (now < capture.StopQpc) capture.StopQpc = now;
                _capture = null;
                header = _header;
            }

            // The rows are no longer shared, so the cut runs outside the lock.
            long elapsed = capture.StopQpc - capture.StartQpc;
            long duration = capture.DurationSeconds * Stopwatch.Frequency;
            bool timed = !capture.OpenEnded && elapsed >= duration - DurationToleranceTicks;
            var cut = CaptureWindow.Cut(capture.Rows, capture.StartQpc, elapsed, duration, timed);
            return new CaptureResult(capture.ProcessName, capture.StartTime, header, cut, capture.StartQpc, capture.ArchivedRows);
        }

        private void OnOutputLine(object sender, DataReceivedEventArgs e)
        {
            var line = e.Data;
            if (string.IsNullOrEmpty(line)) return;
            var arrival = Stopwatch.GetTimestamp();
            lock (_gate)
            {
                if (_header == null)
                {
                    // The header is PresentMon's first stdout line; warnings and errors go to stderr.
                    if (!line.StartsWith("Application,", StringComparison.OrdinalIgnoreCase)) return;
                    var columns = line.Split(',');
                    _header = line;
                    _applicationColumn = IndexOf(columns, "Application");
                    _processIdColumn = IndexOf(columns, "ProcessID");
                    _qpcColumn = IndexOf(columns, "QPCTime");
                    return;
                }

                // Thousands of rows per second: only the two numbers are read, without splitting the row.
                if (!TryParseField(line, _processIdColumn, out long processId)
                    || !TryParseField(line, _qpcColumn, out long qpc))
                    return;

                var row = new PresentRow((int)processId, qpc, line);
                _archive.Add(row);

                if (!_processes.TryGetValue(row.ProcessId, out var entry))
                {
                    entry = new ProcessEntry(row.ProcessId, GetField(line, _applicationColumn));
                    _processes.Add(entry.ProcessId, entry);
                }
                if (entry.Rows == 0) entry.FirstQpc = qpc;
                entry.Rows++;
                entry.LastQpc = qpc;
                entry.LastArrival = arrival;

                var capture = _capture;
                if (capture != null && capture.ProcessId == row.ProcessId)
                    capture.Rows.Add(row);
            }
        }

        private void OnErrorLine(object sender, DataReceivedEventArgs e)
        {
            var line = e.Data;
            if (string.IsNullOrWhiteSpace(line)) return;
            Log.Write("PresentMon: " + line);
            if (line.TrimStart().StartsWith("error", StringComparison.OrdinalIgnoreCase))
            {
                lock (_gate)
                    _lastError = line.Trim();
            }
        }

        private void OnProcessExited(object sender, EventArgs e)
        {
            // Only the running instance counts. One being stopped logs its exit in Stop(), and .NET
            // Framework may raise Exited a second time after Dispose (Close() resets its guard while the
            // exit callback is still pending), possibly after a restart already started the next one.
            if (!ReferenceEquals(sender, _process)) return;
            string exitCode;
            try { exitCode = ((Process)sender).ExitCode.ToString(CultureInfo.InvariantCulture); }
            catch (InvalidOperationException) { exitCode = "?"; }
            Log.Write($"PresentMon exited unexpectedly with code {exitCode}.");
            Exited?.Invoke(this, EventArgs.Empty);
        }

        // A PresentMon that a crashed or killed helper left behind still runs this session. Taking the
        // session over with -stop_existing_session alone fails: the old instance then shuts down and
        // stops its session through its old handle, which apparently hits the new session of the same
        // name. The new PresentMon exited 0.1 s after its start (Windows 11). So end the old session
        // first and give the old process time to exit before the new one starts.
        private void EndLeftoverInstances()
        {
            var leftovers = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(ExePath));
            try
            {
                if (leftovers.Length == 0) return;
                Log.Write($"Ending the session of {leftovers.Length} PresentMon instance(s) still running.");
                StopSession();
                foreach (var leftover in leftovers)
                    leftover.WaitForExit(3000);
            }
            finally
            {
                foreach (var leftover in leftovers)
                    leftover.Dispose();
            }
        }

        private void StopSession()
        {
            var startInfo = new ProcessStartInfo(ExePath, "-terminate_existing -session_name " + SessionName)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using (var stopper = Process.Start(startInfo))
            {
                if (!stopper.WaitForExit(5000))
                    stopper.Kill();
            }
        }

        private static string OwnExecutableName => Path.GetFileName(Assembly.GetEntryAssembly().Location);

        private static string Quote(string argument) => argument.IndexOf(' ') >= 0 ? "\"" + argument + "\"" : argument;

        private static int IndexOf(string[] fields, string name) =>
            Array.FindIndex(fields, field => string.Equals(field.Trim(), name, StringComparison.OrdinalIgnoreCase));

        // The start and length of a column of a CSV row, found by counting commas.
        private static bool TryFindField(string line, int column, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (column < 0) return false;
            for (int i = 0; i < column; i++)
            {
                int comma = line.IndexOf(',', start);
                if (comma < 0) return false;
                start = comma + 1;
            }
            int end = line.IndexOf(',', start);
            length = (end < 0 ? line.Length : end) - start;
            return true;
        }

        // A non-negative integer column (process ID, QPC ticks), parsed in place.
        private static bool TryParseField(string line, int column, out long value)
        {
            value = 0;
            if (!TryFindField(line, column, out int start, out int length) || length == 0 || length > 18) return false;
            for (int i = start; i < start + length; i++)
            {
                int digit = line[i] - '0';
                if ((uint)digit > 9) return false;
                value = value * 10 + digit;
            }
            return true;
        }

        private static string GetField(string line, int column) =>
            TryFindField(line, column, out int start, out int length) ? line.Substring(start, length) : "?";

        private sealed class ProcessEntry
        {
            public ProcessEntry(int processId, string name)
            {
                ProcessId = processId;
                Name = name;
            }

            public int ProcessId { get; }
            public string Name { get; }
            public int Rows;
            public long FirstQpc, LastQpc, LastArrival;
            public double Fps;
        }

        private sealed class Capture
        {
            public Capture(int processId, string processName, long startQpc, int durationSeconds, bool openEnded)
            {
                ProcessId = processId;
                ProcessName = processName;
                StartTime = DateTime.Now;
                StartQpc = startQpc;
                StopQpc = startQpc + durationSeconds * Stopwatch.Frequency;
                DurationSeconds = durationSeconds;
                OpenEnded = openEnded;
            }

            public int ProcessId { get; }
            public string ProcessName { get; }
            public DateTime StartTime { get; }
            public long StartQpc { get; }

            /// <summary>The set end, or an earlier stop request.</summary>
            public long StopQpc { get; set; }

            public int DurationSeconds { get; }
            public bool OpenEnded { get; }
            public int ArchivedRows { get; set; }

            /// <summary>The target's rows of the archive, then every row of it from the start request on.</summary>
            public List<PresentRow> Rows { get; } = new List<PresentRow>();
        }
    }

    internal sealed class ProcessActivity
    {
        public ProcessActivity(int processId, string name, double fps)
        {
            ProcessId = processId;
            Name = name;
            Fps = fps;
        }

        public int ProcessId { get; }
        public string Name { get; }
        public double Fps { get; }
    }

    internal sealed class CaptureProgress
    {
        public CaptureProgress(int processId, string processName, double elapsedSeconds, int durationSeconds, bool openEnded, bool ending, int frames)
        {
            ProcessId = processId;
            ProcessName = processName;
            ElapsedSeconds = elapsedSeconds;
            DurationSeconds = durationSeconds;
            OpenEnded = openEnded;
            Ending = ending;
            Frames = frames;
        }

        public int ProcessId { get; }
        public string ProcessName { get; }
        public double ElapsedSeconds { get; }
        public int DurationSeconds { get; }
        public bool OpenEnded { get; }

        /// <summary>The capture window is over; the last rows are still on their way.</summary>
        public bool Ending { get; }

        public int Frames { get; }
    }

    internal sealed class CaptureResult
    {
        public CaptureResult(string processName, DateTime startTime, string header, CaptureCut cut, long startQpc, int archivedRows)
        {
            ProcessName = processName;
            StartTime = startTime;
            Header = header;
            Cut = cut;
            StartQpc = startQpc;
            ArchivedRows = archivedRows;
        }

        public string ProcessName { get; }
        public DateTime StartTime { get; }
        public string Header { get; }
        public CaptureCut Cut { get; }
        public IReadOnlyList<string> Rows => Cut.Lines;

        /// <summary>The start request on the QPC clock.</summary>
        public long StartQpc { get; }

        /// <summary>Rows of the target the capture took from the archive.</summary>
        public int ArchivedRows { get; }

        /// <summary>The window, for the log.</summary>
        public string Describe() => string.Format(CultureInfo.InvariantCulture,
            "{0} window: start request {1}, reference {2}, first frame {3}, end {4}; {5} rows from the archive",
            Cut.Timed ? "timed" : "stopped", StartQpc, Cut.ReferenceQpc?.ToString(CultureInfo.InvariantCulture) ?? "none",
            Cut.FirstQpc == long.MaxValue ? "none" : Cut.FirstQpc.ToString(CultureInfo.InvariantCulture), Cut.EndQpc, ArchivedRows);
    }
}
