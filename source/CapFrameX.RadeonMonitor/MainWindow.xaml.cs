using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;

namespace CapFrameX.RadeonMonitor
{
    public partial class MainWindow : Window
    {
        private readonly DispatcherTimer pollingTimer;
        private readonly ObservableCollection<MetricReading> readings = new();

        private RadeonSmuMonitor? monitor;
        private RadeonDeviceInfo? deviceInfo;
        private bool readInProgress;

        public MainWindow()
        {
            InitializeComponent();

            GenerationComboBox.ItemsSource = new[]
            {
                "Auto (PCI device ID)",
                "RDNA2 / SMU11",
                "RDNA3 / SMU13",
                "RDNA4 / SMU14"
            };
            Rdna2LayoutComboBox.ItemsSource = new[] { "Auto", "Base", "V2", "V3", "V4" };
            Rdna3LayoutComboBox.ItemsSource = new[] { "Auto", "SMU 13.0.0 / 13.0.10", "SMU 13.0.7" };
            GenerationComboBox.SelectedIndex = 0;
            Rdna2LayoutComboBox.SelectedIndex = 0;
            Rdna3LayoutComboBox.SelectedIndex = 0;
            MetricsGrid.ItemsSource = readings;

            string[] arguments = Environment.GetCommandLineArgs();
            ModulePathTextBox.Text = arguments.Length > 1
                ? Path.GetFullPath(arguments[1])
                : Path.Combine(AppContext.BaseDirectory, "RadeonSMU.bin");

            pollingTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            pollingTimer.Tick += PollingTimer_Tick;

            Closed += MainWindow_Closed;
            UpdateLayoutState();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new()
            {
                Title = "Select compiled RadeonSMU PawnIO module",
                Filter = "PawnIO modules (*.bin;*.amx)|*.bin;*.amx|All files (*.*)|*.*",
                CheckFileExists = true,
                FileName = Path.GetFileName(ModulePathTextBox.Text)
            };

            string? directory = Path.GetDirectoryName(ModulePathTextBox.Text);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }

            if (dialog.ShowDialog(this) == true)
            {
                ModulePathTextBox.Text = dialog.FileName;
            }
        }

        private async void LoadModuleButton_Click(object sender, RoutedEventArgs e)
        {
            string modulePath;
            try
            {
                modulePath = Path.GetFullPath(ModulePathTextBox.Text.Trim());
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, isError: true);
                return;
            }

            if (!File.Exists(modulePath))
            {
                SetStatus($"Module not found: {modulePath}", isError: true);
                return;
            }

            StopPolling();
            DisposeMonitor();
            SetControlsWhileLoading(isLoading: true);
            SetStatus("Opening PawnIO and loading the module...");

            RadeonSmuMonitor? newMonitor = null;
            try
            {
                PawnIoClient client = await Task.Run(() => PawnIoClient.Open(modulePath));
                newMonitor = new RadeonSmuMonitor(client);
                RadeonDeviceInfo info = await Task.Run(newMonitor.GetDeviceInfo);

                monitor = newMonitor;
                deviceInfo = info;
                newMonitor = null;
                ModulePathTextBox.Text = modulePath;
                DeviceInfoText.Text = FormatDeviceInfo(info);
                UpdateLayoutState();
                ReadOnceButton.IsEnabled = true;
                StartButton.IsEnabled = true;
                SetStatus("Module loaded. No SMN writes are issued by this application.");

                if (ResolveGeneration(throwIfUnknown: false) is not null)
                {
                    await PollOnceAsync();
                }
                else
                {
                    SetStatus(
                        $"Module loaded, but PCI device 0x{info.DeviceId:X4} is not in the auto-detection table. Select a generation manually.");
                }
            }
            catch (DllNotFoundException)
            {
                SetStatus("PawnIOLib.dll was not found next to the application.", isError: true);
            }
            catch (BadImageFormatException)
            {
                SetStatus("PawnIOLib.dll has the wrong architecture; this application requires x64.", isError: true);
            }
            catch (Exception ex)
            {
                SetStatus(DescribeLoadError(ex), isError: true);
            }
            finally
            {
                newMonitor?.Dispose();
                SetControlsWhileLoading(isLoading: false);
            }
        }

        private async void ReadOnceButton_Click(object sender, RoutedEventArgs e)
        {
            await PollOnceAsync();
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadInterval(out TimeSpan interval))
            {
                return;
            }

            try
            {
                ResolveGeneration(throwIfUnknown: true);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, isError: true);
                return;
            }

            pollingTimer.Interval = interval;
            pollingTimer.Start();
            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            SetStatus($"Polling every {interval.TotalMilliseconds:F0} ms.");
            await PollOnceAsync();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            StopPolling();
            SetStatus("Polling stopped.");
        }

        private void GenerationComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateLayoutState();
        }

        private async void PollingTimer_Tick(object? sender, EventArgs e)
        {
            await PollOnceAsync();
        }

        private async Task PollOnceAsync()
        {
            if (monitor is null || deviceInfo is null || readInProgress)
            {
                return;
            }

            RadeonGeneration generation;
            Rdna2MetricsLayout rdna2Layout;
            Rdna3MetricsLayout rdna3Layout;
            try
            {
                generation = ResolveGeneration(throwIfUnknown: true)!.Value;
                rdna2Layout = ResolveRdna2Layout(generation);
                rdna3Layout = ResolveRdna3Layout(generation);
            }
            catch (Exception ex)
            {
                SetStatus(ex.Message, isError: true);
                return;
            }

            readInProgress = true;
            ReadOnceButton.IsEnabled = false;
            try
            {
                uint[] raw = await Task.Run(() => monitor.ReadMetrics(generation, rdna3Layout));
                IReadOnlyList<MetricReading> parsed =
                    MetricsParser.Parse(raw, generation, rdna2Layout, rdna3Layout);

                readings.Clear();
                foreach (MetricReading reading in parsed)
                {
                    readings.Add(reading);
                }

                RawDumpTextBox.Text = FormatRawDump(raw);
                LastUpdateText.Text =
                    $"{DateTime.Now:HH:mm:ss.fff} · {generation.ToString().ToUpperInvariant()}" +
                    (generation == RadeonGeneration.Rdna2 ? $" {rdna2Layout}" : string.Empty) +
                    (generation == RadeonGeneration.Rdna3 ? $" {rdna3Layout}" : string.Empty) +
                    $" · {raw.Length} DWORDs";

                if (!pollingTimer.IsEnabled)
                {
                    SetStatus($"Read {parsed.Count} decoded values.");
                }
            }
            catch (Exception ex)
            {
                StopPolling();
                SetStatus(
                    $"Metrics read failed: {ex.Message} The AMD driver may not have published a metrics-table address yet.",
                    isError: true);
            }
            finally
            {
                readInProgress = false;
                ReadOnceButton.IsEnabled = monitor is not null;
            }
        }

        private RadeonGeneration? ResolveGeneration(bool throwIfUnknown)
        {
            RadeonGeneration? generation = GenerationComboBox.SelectedIndex switch
            {
                1 => RadeonGeneration.Rdna2,
                2 => RadeonGeneration.Rdna3,
                3 => RadeonGeneration.Rdna4,
                _ => deviceInfo is null ? null : RadeonDeviceClassifier.DetectGeneration(deviceInfo.DeviceId)
            };

            if (generation is null && throwIfUnknown)
            {
                string deviceId = deviceInfo is null ? "unknown" : $"0x{deviceInfo.DeviceId:X4}";
                throw new InvalidOperationException(
                    $"Generation auto-detection is unavailable for PCI device {deviceId}; select RDNA2, RDNA3 or RDNA4 manually.");
            }

            return generation;
        }

        private Rdna2MetricsLayout ResolveRdna2Layout(RadeonGeneration generation)
        {
            if (generation != RadeonGeneration.Rdna2)
            {
                return Rdna2MetricsLayout.Auto;
            }

            Rdna2MetricsLayout selected = (Rdna2MetricsLayout)Rdna2LayoutComboBox.SelectedIndex;
            if (selected != Rdna2MetricsLayout.Auto)
            {
                return selected;
            }

            if (deviceInfo is null)
            {
                throw new InvalidOperationException("Load a module before resolving the RDNA2 layout.");
            }

            return RadeonDeviceClassifier.DetectRdna2Layout(deviceInfo.DeviceId);
        }

        private Rdna3MetricsLayout ResolveRdna3Layout(RadeonGeneration generation)
        {
            if (generation != RadeonGeneration.Rdna3)
            {
                return Rdna3MetricsLayout.Auto;
            }

            Rdna3MetricsLayout selected = (Rdna3MetricsLayout)Rdna3LayoutComboBox.SelectedIndex;
            if (selected != Rdna3MetricsLayout.Auto)
            {
                return selected;
            }

            if (deviceInfo is null)
            {
                throw new InvalidOperationException("Load a module before resolving the RDNA3 layout.");
            }

            return RadeonDeviceClassifier.DetectRdna3Layout(deviceInfo.DeviceId);
        }

        private void UpdateLayoutState()
        {
            RadeonGeneration? selectedGeneration = GenerationComboBox.SelectedIndex switch
            {
                1 => RadeonGeneration.Rdna2,
                2 => RadeonGeneration.Rdna3,
                3 => RadeonGeneration.Rdna4,
                _ => deviceInfo is null ? null : RadeonDeviceClassifier.DetectGeneration(deviceInfo.DeviceId)
            };
            Rdna2LayoutComboBox.IsEnabled = selectedGeneration == RadeonGeneration.Rdna2;
            Rdna3LayoutComboBox.IsEnabled = selectedGeneration == RadeonGeneration.Rdna3;
        }

        private bool TryReadInterval(out TimeSpan interval)
        {
            interval = default;
            if (!double.TryParse(
                    IntervalTextBox.Text,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out double milliseconds) ||
                milliseconds < 50 ||
                milliseconds > 60_000)
            {
                SetStatus("Polling interval must be between 50 and 60000 milliseconds.", isError: true);
                return false;
            }

            interval = TimeSpan.FromMilliseconds(milliseconds);
            return true;
        }

        private void StopPolling()
        {
            pollingTimer.Stop();
            StartButton.IsEnabled = monitor is not null;
            StopButton.IsEnabled = false;
        }

        private void SetControlsWhileLoading(bool isLoading)
        {
            LoadModuleButton.IsEnabled = !isLoading;
            ModulePathTextBox.IsEnabled = !isLoading;
            if (isLoading)
            {
                ReadOnceButton.IsEnabled = false;
                StartButton.IsEnabled = false;
                StopButton.IsEnabled = false;
            }
            else if (monitor is not null)
            {
                ReadOnceButton.IsEnabled = true;
                StartButton.IsEnabled = !pollingTimer.IsEnabled;
            }
        }

        private void SetStatus(string message, bool isError = false)
        {
            StatusText.Text = message;
            StatusText.Foreground = isError ? Brushes.IndianRed : new SolidColorBrush(Color.FromRgb(154, 163, 178));
        }

        private static string FormatDeviceInfo(RadeonDeviceInfo info)
        {
            RadeonGeneration? detected = RadeonDeviceClassifier.DetectGeneration(info.DeviceId);
            string generation = detected?.ToString().ToUpperInvariant() ?? "unknown generation";
            string metricsAddress = info.MetricsGpuAddress == 0
                ? "not published"
                : $"GPU 0x{info.MetricsGpuAddress:X} / VRAM +0x{info.MetricsVramOffset:X}";

            return
                $"AMD 1002:{info.DeviceId:X4} rev {info.RevisionId:X2}, subsystem {info.SubsystemVendorId:X4}:{info.SubsystemDeviceId:X4}, " +
                $"PCI {info.PciAddress}, {generation} · VRAM BAR 0x{info.VramBar:X} ({FormatByteSize(info.VramBarSize)}) · " +
                $"metrics {metricsAddress} · module ABI {info.ModuleAbi}, PawnIOLib {info.PawnIoVersion}";
        }

        private static string FormatByteSize(ulong bytes)
        {
            const double gibibyte = 1024.0 * 1024.0 * 1024.0;
            const double mebibyte = 1024.0 * 1024.0;
            return bytes >= gibibyte
                ? $"{bytes / gibibyte:F2} GiB"
                : $"{bytes / mebibyte:F0} MiB";
        }

        private static string FormatRawDump(IReadOnlyList<uint> raw)
        {
            StringBuilder builder = new();
            for (int i = 0; i < raw.Count; i++)
            {
                if (i % 8 == 0)
                {
                    if (i != 0)
                    {
                        builder.AppendLine();
                    }

                    builder.Append($"{i * sizeof(uint):X4}: ");
                }

                builder.Append($"{raw[i]:X8} ");
            }

            return builder.ToString().TrimEnd();
        }

        private static string DescribeLoadError(Exception exception)
        {
            if (exception is PawnIoException)
            {
                return exception.Message +
                    " Ensure the signed PawnIO 2.x driver package is installed and its device is running.";
            }

            return exception.Message;
        }

        private void DisposeMonitor()
        {
            monitor?.Dispose();
            monitor = null;
            deviceInfo = null;
            readings.Clear();
            RawDumpTextBox.Clear();
            LastUpdateText.Text = string.Empty;
            DeviceInfoText.Text = "No module loaded.";
            ReadOnceButton.IsEnabled = false;
            StartButton.IsEnabled = false;
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            StopPolling();
            DisposeMonitor();
        }
    }
}
