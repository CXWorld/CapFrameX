using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace CapFrameX.CaptureHelper
{
    public partial class MainWindow : Window
    {
        private const int HotkeyId = 0x4358;

        private readonly Settings _settings = Settings.Load();
        private readonly ObservableCollection<ProcessItem> _processes = new ObservableCollection<ProcessItem>();
        private readonly DispatcherTimer _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        private readonly PresentMonService _presentMon = new PresentMonService(AppInfo.PresentMonPath);
        private readonly Brush _statusBrush;
        private HwndSource _hwndSource;
        private HotkeyOption _hotkey;
        private long _lastProcessPoll;
        private int _analysisRequest;
        private bool _initialized;
        private bool _closing;

        public MainWindow()
        {
            InitializeComponent();
            Title = AppInfo.Title;
            _statusBrush = StatusText.Foreground;

            HotkeyCombo.ItemsSource = HotkeyOption.All;
            HotkeyCombo.SelectedItem = HotkeyOption.Find(_settings.Hotkey);
            DurationCombo.ItemsSource = DurationOption.All;
            DurationCombo.SelectedItem = DurationOption.Find(_settings.DurationSeconds);
            FolderText.Text = _settings.CaptureFolder;
            ProcessList.ItemsSource = _processes;

            _presentMon.Exited += (sender, e) => Dispatcher.BeginInvoke(new Action(OnPresentMonExited));
            _timer.Tick += OnTimerTick;
            Loaded += OnLoaded;
            _initialized = true;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            _hwndSource = (HwndSource)PresentationSource.FromVisual(this);
            _hwndSource.AddHook(WndProc);
            RegisterHotkey((HotkeyOption)HotkeyCombo.SelectedItem);
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            _closing = true;
            _timer.Stop();

            // A running capture keeps what it has so far.
            var capture = _presentMon.TryFinishCapture(force: true);
            if (capture != null && capture.Rows.Count > 0)
            {
                try { CaptureWriter.Write(_settings.CaptureFolder, capture); }
                catch (Exception ex) { Log.Write("Saving the capture at exit failed: " + ex); }
            }

            if (_hotkey != null) NativeMethods.UnregisterHotKey(_hwndSource.Handle, HotkeyId);
            _presentMon.Dispose();
            _settings.Save();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            StartPresentMon();
            RefreshFileTree();
            UpdateProcessList(Array.Empty<ProcessActivity>());
            _lastProcessPoll = Stopwatch.GetTimestamp();
            _timer.Start();
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
            {
                ToggleCapture();
                handled = true;
            }
            return IntPtr.Zero;
        }

        // ---- Capture ------------------------------------------------------------------------------

        private void StartPresentMon()
        {
            RestartButton.Visibility = Visibility.Collapsed;
            if (!File.Exists(_presentMon.ExePath))
            {
                PresentMonText.Text = "PresentMon is missing: " + _presentMon.ExePath;
                return;
            }
            try
            {
                _presentMon.Start();
                PresentMonText.Text = $"PresentMon 1.10.0 ({AppInfo.Bitness}) is running.";
            }
            catch (Exception ex)
            {
                Log.Write("Starting PresentMon failed: " + ex);
                PresentMonText.Text = "PresentMon could not be started: " + ex.Message;
                RestartButton.Visibility = Visibility.Visible;
            }
        }

        private void OnPresentMonExited()
        {
            if (_closing) return;
            var error = _presentMon.LastError;
            PresentMonText.Text = "PresentMon stopped" + (string.IsNullOrEmpty(error) ? "." : ": " + error);
            RestartButton.Visibility = Visibility.Visible;
        }

        private void OnRestartPresentMon(object sender, RoutedEventArgs e) => StartPresentMon();

        private void OnCaptureButton(object sender, RoutedEventArgs e) => ToggleCapture();

        private void ToggleCapture()
        {
            if (_presentMon.IsCapturing)
            {
                _presentMon.RequestCaptureEnd();
                UpdateCaptureStatus();
                return;
            }
            if (!_presentMon.IsRunning)
            {
                ShowStatus("PresentMon is not running, so nothing can be captured.", true);
                SystemSounds.Hand.Play();
                return;
            }

            var target = ResolveTarget();
            if (target == null)
            {
                ShowStatus($"Nothing to capture: the application in the foreground presents no frames. " +
                    $"Press {_hotkey?.Name ?? "the hotkey"} in the game, or select the game in the list.", true);
                SystemSounds.Hand.Play();
                return;
            }

            var duration = (DurationOption)DurationCombo.SelectedItem;
            var openEnded = duration.Seconds == 0;
            _presentMon.StartCapture(target.ProcessId, target.Name,
                openEnded ? DurationOption.OpenEndedLimitSeconds : duration.Seconds, openEnded);
            Log.Write($"Capture started: {target.Name} (pid {target.ProcessId}), {duration.Name}");
            SystemSounds.Asterisk.Play();
            UpdateCaptureStatus();
        }

        // The selected process, else the application in the foreground: the game, when the hotkey is
        // pressed in it. No guess beyond that: background apps (overlays, launchers, tools) often
        // present faster than the game.
        private ProcessItem ResolveTarget()
        {
            if (ProcessList.SelectedItem is ProcessItem selected) return selected;
            var foreground = NativeMethods.GetForegroundProcessId();
            return _processes.FirstOrDefault(process => process.ProcessId == foreground);
        }

        private void OnTimerTick(object sender, EventArgs e)
        {
            var now = Stopwatch.GetTimestamp();
            if (now - _lastProcessPoll >= Stopwatch.Frequency)
            {
                _lastProcessPoll = now;
                UpdateProcessList(_presentMon.PollProcesses());

                // A capture whose target exited ends now, not at its end time (up to 30 min away).
                var progress = _presentMon.GetCaptureProgress();
                if (progress != null && !progress.Ending && !IsRunning(progress.ProcessId, progress.ProcessName))
                {
                    Log.Write($"Capture target {progress.ProcessName} (pid {progress.ProcessId}) exited; ending the capture.");
                    _presentMon.RequestCaptureEnd();
                }
            }

            var capture = _presentMon.TryFinishCapture(force: false);
            if (capture != null)
                SaveCapture(capture);
            UpdateCaptureStatus();
        }

        private void UpdateCaptureStatus()
        {
            var progress = _presentMon.GetCaptureProgress();
            CaptureButton.Content = progress == null ? "Start capture" : "Stop capture";
            if (progress == null) return;

            string text;
            if (progress.Ending)
                text = $"Finishing the capture of {progress.ProcessName}…";
            else if (progress.OpenEnded)
                text = $"Capturing {progress.ProcessName}: {progress.ElapsedSeconds:0} s. Press {_hotkey?.Name ?? "the hotkey"} to stop.";
            else
                text = $"Capturing {progress.ProcessName}: {progress.ElapsedSeconds:0} / {progress.DurationSeconds} s";
            ShowStatus(text, false);
        }

        private async void SaveCapture(CaptureResult capture)
        {
            if (capture.Rows.Count == 0)
            {
                Log.Write($"Capture of {capture.ProcessName} ended without frames; {capture.Describe()}.");
                ShowStatus($"No frames of {capture.ProcessName} arrived during the capture, so nothing was saved.", true);
                SystemSounds.Hand.Play();
                return;
            }

            SystemSounds.Exclamation.Play();
            var folder = _settings.CaptureFolder;
            try
            {
                var path = await Task.Run(() => CaptureWriter.Write(folder, capture));
                Log.Write($"Capture saved: {path} ({capture.Rows.Count} frames; {capture.Describe()})");
                ShowStatus($"Saved {Path.GetFileName(path)} with {capture.Rows.Count:N0} frames.", false);
                RefreshFileTree();
                ShowAnalysis(path);
            }
            catch (Exception ex)
            {
                Log.Write("Saving the capture failed: " + ex);
                ShowStatus("Saving the capture failed: " + ex.Message, true);
            }
        }

        private void ShowStatus(string text, bool isError)
        {
            StatusText.Text = text;
            StatusText.Foreground = isError ? (Brush)FindResource("ErrorBrush") : _statusBrush;
        }

        private void UpdateProcessList(IReadOnlyList<ProcessActivity> activity)
        {
            foreach (var entry in activity)
            {
                var item = _processes.FirstOrDefault(process => process.ProcessId == entry.ProcessId);
                if (item == null)
                {
                    item = new ProcessItem(entry.ProcessId, entry.Name);
                    _processes.Add(item);
                }
                item.Fps = entry.Fps;
            }
            for (int i = _processes.Count - 1; i >= 0; i--)
            {
                var item = _processes[i];
                if (activity.Any(entry => entry.ProcessId == item.ProcessId)) continue;

                // The selected target stays while its process runs, also when it presents nothing for
                // a while (loading screen, minimized fullscreen game); only its exit removes it.
                bool isTarget = item == ProcessList.SelectedItem;
                if (isTarget && IsRunning(item.ProcessId, item.Name))
                {
                    item.Fps = 0;
                    continue;
                }
                _processes.RemoveAt(i);
                if (isTarget)
                {
                    Log.Write($"Target {item.Name} (pid {item.ProcessId}) exited.");
                    if (!_presentMon.IsCapturing)
                        ShowStatus($"The target {item.Name} exited. Select the game again once it runs.", false);
                }
            }
            NoProcessesText.Visibility = _processes.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            UpdateTargetText();
        }

        // Whether the process still runs. The name check guards against a reused process ID; PresentMon
        // names a process it could not open "<error>", which then cannot be checked.
        private static bool IsRunning(int processId, string name)
        {
            try
            {
                using (var process = Process.GetProcessById(processId))
                {
                    return name.StartsWith("<", StringComparison.Ordinal)
                        || string.Equals(process.ProcessName, name, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(process.ProcessName + ".exe", name, StringComparison.OrdinalIgnoreCase);
                }
            }
            catch (ArgumentException)
            {
                return false; // no process with this ID
            }
            catch (InvalidOperationException)
            {
                return false; // exited in the meantime
            }
        }

        private void UpdateTargetText()
        {
            var hotkey = _hotkey?.Name ?? "the hotkey";
            TargetText.Text = ProcessList.SelectedItem is ProcessItem selected
                ? $"Target: {selected.Name} (PID {selected.ProcessId}), whichever window is in the foreground. " +
                  "It stays the target while it runs."
                : $"Target: automatic. {hotkey} captures the application in the foreground. " +
                  "For Start capture, select the process first.";
        }

        private void OnTargetChanged(object sender, SelectionChangedEventArgs e) => UpdateTargetText();

        private void OnAutomaticTarget(object sender, RoutedEventArgs e) => ProcessList.SelectedItem = null;

        // ---- Settings ------------------------------------------------------------------------------

        private void RegisterHotkey(HotkeyOption hotkey)
        {
            if (_hwndSource == null || hotkey == null) return;
            if (_hotkey != null)
            {
                NativeMethods.UnregisterHotKey(_hwndSource.Handle, HotkeyId);
                _hotkey = null;
            }

            if (NativeMethods.RegisterHotKey(_hwndSource.Handle, HotkeyId, hotkey.Modifiers | HotkeyOption.NoRepeat, hotkey.Key))
            {
                _hotkey = hotkey;
                HotkeyWarning.Visibility = Visibility.Collapsed;
            }
            else
            {
                Log.Write($"RegisterHotKey({hotkey.Name}) failed with error {Marshal.GetLastWin32Error()}.");
                HotkeyWarning.Text = $"{hotkey.Name} is taken by another program. Please choose another hotkey.";
                HotkeyWarning.Visibility = Visibility.Visible;
            }
            UpdateTargetText();
        }

        private void OnHotkeyChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized || !(HotkeyCombo.SelectedItem is HotkeyOption hotkey)) return;
            _settings.Hotkey = hotkey.Name;
            _settings.Save();
            RegisterHotkey(hotkey);
        }

        private void OnDurationChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_initialized || !(DurationCombo.SelectedItem is DurationOption duration)) return;
            _settings.DurationSeconds = duration.Seconds;
            _settings.Save();
        }

        private void OnBrowseFolder(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Folder for the capture files",
                SelectedPath = _settings.CaptureFolder,
                ShowNewFolderButton = true,
            })
            {
                if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                _settings.CaptureFolder = dialog.SelectedPath;
            }
            _settings.Save();
            FolderText.Text = _settings.CaptureFolder;
            RefreshFileTree();
        }

        private void OnOpenFolder(object sender, RoutedEventArgs e)
        {
            try
            {
                Directory.CreateDirectory(_settings.CaptureFolder);
                Process.Start("explorer.exe", "\"" + _settings.CaptureFolder + "\"");
            }
            catch (Exception ex)
            {
                ShowStatus("The folder could not be opened: " + ex.Message, true);
            }
        }

        // ---- Analysis ------------------------------------------------------------------------------

        private void RefreshFileTree()
        {
            var root = new FileNode(_settings.CaptureFolder, true) { IsExpanded = true };
            FileTree.ItemsSource = new[] { root };
        }

        private void OnRefreshTree(object sender, RoutedEventArgs e) => RefreshFileTree();

        private void OnFileSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FileNode node && !node.IsDirectory)
                ShowAnalysis(node.FullPath);
        }

        private void OnOpenFile(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "PresentMon CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                InitialDirectory = Directory.Exists(_settings.CaptureFolder) ? _settings.CaptureFolder : null,
            };
            if (dialog.ShowDialog(this) == true)
                ShowAnalysis(dialog.FileName);
        }

        private async void ShowAnalysis(string path)
        {
            var request = ++_analysisRequest;
            AnalysisTitle.Text = Path.GetFileName(path);
            AnalysisInfo.Text = "Loading…";
            try
            {
                var file = await Task.Run(() => CaptureFile.Load(path));
                if (request != _analysisRequest) return;

                var frametimes = file.Frametimes;
                if (frametimes.Length < 2)
                {
                    ClearMetrics();
                    AnalysisInfo.Text = "The file contains no frames.";
                    Graph.Clear("No frames to show.");
                    return;
                }

                AverageText.Text = FormatFps(FrameStatistics.AverageFps(frametimes));
                P1Text.Text = FormatFps(FrameStatistics.PercentileFps(frametimes, 0.01));
                P02Text.Text = FormatFps(FrameStatistics.PercentileFps(frametimes, 0.002));
                var process = string.IsNullOrEmpty(file.ProcessName) ? "" : file.ProcessName + "  ·  ";
                AnalysisInfo.Text = $"{process}{frametimes.Length:N0} frames  ·  {frametimes.Sum() / 1000:0.0} s";
                Graph.SetFrametimes(frametimes);
            }
            catch (Exception ex)
            {
                if (request != _analysisRequest) return;
                ClearMetrics();
                AnalysisInfo.Text = "The file could not be read: " + ex.Message;
                Graph.Clear("No frames to show.");
            }
        }

        private void ClearMetrics()
        {
            AverageText.ClearValue(TextBlock.TextProperty);
            P1Text.ClearValue(TextBlock.TextProperty);
            P02Text.ClearValue(TextBlock.TextProperty);
        }

        private static string FormatFps(double fps) => fps.ToString("0.0", CultureInfo.CurrentCulture);
    }
}
