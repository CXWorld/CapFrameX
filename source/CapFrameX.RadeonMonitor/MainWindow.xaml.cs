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
        private static readonly HashSet<(string Group, string Name)> AdlMetricsSupersededByNavi21ToolTable =
            new()
            {
                ("Clocks", "GFX clock"),
                ("Clocks", "Memory clock"),
                ("Temperature", "Edge"),
                ("Temperature", "Memory"),
                ("Temperature", "VR VDDC"),
                ("Temperature", "VR memory"),
                ("Temperature", "VR SOC"),
                ("Temperature", "VR memory 0"),
                ("Temperature", "VR memory 1"),
                ("Temperature", "Hotspot"),
                ("Fan", "Fan speed"),
                ("Activity", "GFX activity"),
                ("Voltage", "SOC voltage"),
                ("Voltage", "GFX voltage"),
                ("Voltage", "Memory voltage"),
                ("Power", "SOC power"),
                ("Power", "GFX power"),
                ("Power", "Board power"),
                ("Current", "SOC current"),
                ("Current", "GFX current")
            };

        private static readonly HashSet<(string Group, string Name)> AdlMetricsSupersededByRdna3ToolTable =
            new()
            {
                ("Clocks", "Memory clock"),
                ("Clocks", "SOC clock"),
                ("Clocks", "Fabric clock"),
                ("Temperature", "Edge"),
                ("Temperature", "Memory"),
                ("Temperature", "VR VDDC"),
                ("Temperature", "VR memory"),
                ("Temperature", "VR SOC"),
                ("Temperature", "VR memory 0"),
                ("Temperature", "VR memory 1"),
                ("Temperature", "Hotspot"),
                ("Temperature", "Hotspot GCD"),
                ("Temperature", "Hotspot MCD"),
                ("Fan", "Fan speed"),
                ("Activity", "GFX activity"),
                ("Voltage", "SOC voltage"),
                ("Voltage", "GFX voltage"),
                ("Voltage", "Memory voltage"),
                ("Power", "SOC power"),
                ("Power", "GFX power"),
                ("Power", "Board power"),
                ("Current", "SOC current"),
                ("Current", "GFX current")
            };

        private static readonly HashSet<(string Group, string Name)> AdlMetricsSupersededByRdna4ToolTable =
            new();

        private readonly DispatcherTimer pollingTimer;
        private readonly ObservableCollection<MetricReading> readings = new();
        private readonly MetricStatisticsTracker statisticsTracker = new();

        private RadeonSmuMonitor? monitor;
        private RadeonDeviceInfo? deviceInfo;
        private AdlPmLogClient? adlPmLogClient;
        private RadeonToolTableTelemetry? lastRdna4ToolTableTelemetry;
        private uint? lastRdna4ToolTableVersion;
        private string? statisticsScope;
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
            Rdna3LayoutComboBox.ItemsSource = new[] { "Auto", "SMU 13.0.0 layout (Navi 31/32)", "SMU 13.0.7" };
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
                SetStatus(
                    "Module loaded. RDNA tool-table reads use fixed, bounded SMU mailbox commands.");

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

            ResetStatistics();
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

        private void ResetStatisticsButton_Click(object sender, RoutedEventArgs e)
        {
            if (readings.Count == 0)
            {
                return;
            }

            MetricReading[] currentReadings = readings
                .Select(reading => reading with
                {
                    MinimumValue = "\u2014",
                    MaximumValue = "\u2014",
                    AverageValue = "\u2014"
                })
                .ToArray();
            statisticsTracker.Reset();
            IReadOnlyList<MetricReading> resetReadings = statisticsTracker.Update(currentReadings);

            readings.Clear();
            foreach (MetricReading reading in resetReadings)
            {
                readings.Add(reading);
            }

            SetStatus("Statistics reset to the current values.");
        }

        private void GenerationComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateLayoutState();
            if (monitor is not null)
            {
                ResetStatistics();
            }
        }

        private void LayoutComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (monitor is not null)
            {
                ResetStatistics();
            }
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
                if (deviceInfo.MetricsPhysicalAddress == 0)
                {
                    RadeonDeviceInfo refreshedInfo = await Task.Run(monitor.GetDeviceInfo);
                    deviceInfo = refreshedInfo;
                    DeviceInfoText.Text = FormatDeviceInfo(refreshedInfo);

                    if (refreshedInfo.MetricsPhysicalAddress == 0)
                    {
                        if (SupportsAdlPmLogFallback(generation))
                        {
                            await PollAdlPmLogAsync(refreshedInfo, generation);
                            return;
                        }

                        StopPolling();
                        SetStatus(DescribeUnavailableMetricsAddress(generation), isError: true);
                        return;
                    }
                }

                string? refreshError = await RefreshPublicMetricsAsync(deviceInfo, generation);
                uint[]? raw = await Task.Run(() => monitor.TryReadMetrics(generation, rdna3Layout));
                if (raw is null)
                {
                    if (SupportsAdlPmLogFallback(generation))
                    {
                        await PollAdlPmLogAsync(deviceInfo, generation);
                        return;
                    }

                    StopPolling();
                    SetStatus(DescribeUnavailableMetricsAddress(generation), isError: true);
                    return;
                }

                IReadOnlyList<MetricReading> parsed =
                    MetricsParser.Parse(raw, generation, rdna2Layout, rdna3Layout);

                RadeonToolTableSnapshot? toolTableSnapshot = null;
                RadeonToolTableTelemetry? toolTableTelemetry = null;
                string? toolTableError = null;
                if (generation == RadeonGeneration.Rdna4 &&
                    RadeonDeviceClassifier.IsRdna4(deviceInfo.DeviceId))
                {
                    try
                    {
                        toolTableSnapshot = await Task.Run(monitor.ReadToolTable);
                        toolTableTelemetry = Rdna4ToolTableParser.Parse(toolTableSnapshot);
                        lastRdna4ToolTableTelemetry = toolTableTelemetry;
                        lastRdna4ToolTableVersion = toolTableSnapshot.Version;
                    }
                    catch (Exception ex)
                    {
                        toolTableError = ex.Message;
                        if (lastRdna4ToolTableTelemetry is not null)
                        {
                            toolTableTelemetry = MarkTelemetryUnavailable(lastRdna4ToolTableTelemetry);
                        }
                    }
                }

                IReadOnlyList<MetricReading> combinedReadings = toolTableTelemetry is null
                    ? parsed
                    : MergeToolTableReadings(parsed, toolTableTelemetry.Readings, generation);
                string toolScope = generation == RadeonGeneration.Rdna4 &&
                    RadeonDeviceClassifier.IsRdna4(deviceInfo.DeviceId)
                        ? $":Tool:{lastRdna4ToolTableVersion?.ToString("X8", CultureInfo.InvariantCulture) ?? "pending"}"
                        : string.Empty;

                UpdateReadings(
                    combinedReadings,
                    $"PawnIO:{generation}:{rdna2Layout}:{rdna3Layout}{toolScope}");

                if (toolTableSnapshot is null)
                {
                    RawDumpTextBox.Text = toolTableError is null
                        ? FormatRawDump(raw)
                        : $"RDNA4 private SMU tool table unavailable: {toolTableError}{Environment.NewLine}{Environment.NewLine}" +
                            FormatRawDump(raw);
                    RawDumpExpander.Header = "Raw DWORD dump";
                }
                else
                {
                    RawDumpTextBox.Text =
                        $"RDNA4 private SMU tool table{Environment.NewLine}" +
                        $"Version 0x{toolTableSnapshot.Version:X8}, layout {toolTableSnapshot.Layout}, " +
                        $"GPU/MC 0x{toolTableSnapshot.GpuAddress:X}, " +
                        $"framebuffer [0x{toolTableSnapshot.FramebufferBase:X}, " +
                        $"0x{toolTableSnapshot.FramebufferTop:X}){Environment.NewLine}" +
                        FormatRawDump(toolTableSnapshot.Dwords) +
                        Environment.NewLine + Environment.NewLine +
                        "RDNA4 public SMU metrics" + Environment.NewLine +
                        FormatRawDump(raw);
                    RawDumpExpander.Header = "RDNA4 SMU tool table + public metrics dump";
                }
                LastUpdateText.Text =
                    $"{DateTime.Now:HH:mm:ss.fff} · {generation.ToString().ToUpperInvariant()}" +
                    (generation == RadeonGeneration.Rdna2 ? $" {rdna2Layout}" : string.Empty) +
                    (generation == RadeonGeneration.Rdna3 ? $" {rdna3Layout}" : string.Empty) +
                    $" · {combinedReadings.Count} sensors";

                if (refreshError is not null)
                {
                    SetStatus(
                        $"Read {combinedReadings.Count} decoded values. Driver refresh failed: {refreshError}",
                        isError: true);
                }
                else if (toolTableError is not null)
                {
                    SetStatus(
                        $"Read {parsed.Count} public values. Effective clocks unavailable: {toolTableError}",
                        isError: true);
                }
                else if (!pollingTimer.IsEnabled)
                {
                    SetStatus($"Read {combinedReadings.Count} decoded values.");
                }
            }
            catch (Exception ex)
            {
                StopPolling();
                SetStatus($"Metrics read failed: {ex.Message}", isError: true);
            }
            finally
            {
                readInProgress = false;
                ReadOnceButton.IsEnabled = monitor is not null;
            }
        }

        private async Task<string?> RefreshPublicMetricsAsync(
            RadeonDeviceInfo info,
            RadeonGeneration generation)
        {
            if (generation != RadeonGeneration.Rdna4)
            {
                return null;
            }

            try
            {
                if (adlPmLogClient is null)
                {
                    adlPmLogClient = await Task.Run(() => AdlPmLogClient.Open(info));
                }

                await Task.Run(adlPmLogClient.RefreshMetrics);
                return null;
            }
            catch (Exception ex)
            {
                adlPmLogClient?.Dispose();
                adlPmLogClient = null;
                return ex.Message;
            }
        }

        private async Task PollAdlPmLogAsync(RadeonDeviceInfo info, RadeonGeneration generation)
        {
            try
            {
                if (adlPmLogClient is null)
                {
                    adlPmLogClient = await Task.Run(() => AdlPmLogClient.Open(info));
                }

                DeviceInfoText.Text =
                    $"{FormatDeviceInfo(info)} · ADL PMLog adapter {adlPmLogClient.AdapterIndex}: " +
                    adlPmLogClient.AdapterName;

                AdlPmLogSnapshot snapshot = await Task.Run(adlPmLogClient.ReadMetrics);
                List<MetricReading> combinedReadings = new(snapshot.Readings);
                RadeonToolTableSnapshot? toolTableSnapshot = null;
                string? toolTableError = null;
                RadeonToolTableTelemetry? toolTableTelemetry = null;
                string? toolTableDecodeError = null;
                Navi21SviSnapshot? sviSnapshot = null;
                string? sviError = null;
                string toolTableName = generation switch
                {
                    RadeonGeneration.Rdna3 => "RDNA3",
                    RadeonGeneration.Rdna4 => "RDNA4",
                    _ => "Navi21"
                };
                bool navi21Supported =
                    generation == RadeonGeneration.Rdna2 &&
                    Navi21SviTelemetry.IsSupportedDevice(info.DeviceId);
                bool toolTableSupported =
                    (generation == RadeonGeneration.Rdna2 &&
                        RadeonDeviceClassifier.IsNavi21(info.DeviceId)) ||
                    (generation == RadeonGeneration.Rdna3 &&
                        RadeonDeviceClassifier.IsRdna3(info.DeviceId)) ||
                    (generation == RadeonGeneration.Rdna4 &&
                        RadeonDeviceClassifier.IsRdna4(info.DeviceId));

                if (toolTableSupported)
                {
                    RadeonSmuMonitor currentMonitor = monitor ?? throw new InvalidOperationException(
                        "The Radeon monitor was closed while reading private SMU telemetry.");

                    try
                    {
                        toolTableSnapshot = await Task.Run(currentMonitor.ReadToolTable);
                        if (navi21Supported)
                        {
                            try
                            {
                                toolTableTelemetry = Navi21ToolTableParser.Parse(toolTableSnapshot);
                                combinedReadings = MergeToolTableReadings(
                                    snapshot.Readings,
                                    toolTableTelemetry.Readings,
                                    generation);
                            }
                            catch (Exception ex)
                            {
                                // Keep raw data for unmapped firmware layouts.
                                toolTableDecodeError = ex.Message;
                            }
                        }
                        else if (generation == RadeonGeneration.Rdna3)
                        {
                            try
                            {
                                toolTableTelemetry = Rdna3ToolTableParser.Parse(toolTableSnapshot);
                                combinedReadings = MergeToolTableReadings(
                                    snapshot.Readings,
                                    toolTableTelemetry.Readings,
                                    generation);
                            }
                            catch (Exception ex)
                            {
                                // Keep ADL and raw data for unmapped layouts.
                                toolTableDecodeError = ex.Message;
                            }
                        }
                        else if (generation == RadeonGeneration.Rdna4)
                        {
                            try
                            {
                                toolTableTelemetry = Rdna4ToolTableParser.Parse(toolTableSnapshot);
                                lastRdna4ToolTableTelemetry = toolTableTelemetry;
                                lastRdna4ToolTableVersion = toolTableSnapshot.Version;
                                combinedReadings = MergeToolTableReadings(
                                    snapshot.Readings,
                                    toolTableTelemetry.Readings,
                                    generation);
                            }
                            catch (Exception ex)
                            {
                                // Keep ADL and raw data for unmapped layouts.
                                toolTableDecodeError = ex.Message;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        toolTableError = ex.Message;
                    }
                }

                if (generation == RadeonGeneration.Rdna4 &&
                    toolTableSnapshot is null &&
                    lastRdna4ToolTableTelemetry is not null)
                {
                    toolTableTelemetry = MarkTelemetryUnavailable(lastRdna4ToolTableTelemetry);
                    combinedReadings = MergeToolTableReadings(
                        snapshot.Readings,
                        toolTableTelemetry.Readings,
                        generation);
                }

                if (navi21Supported)
                {
                    RadeonSmuMonitor currentMonitor = monitor ?? throw new InvalidOperationException(
                        "The Radeon monitor was closed while reading Navi21 SVI telemetry.");
                    try
                    {
                        uint[] sviRegisters = await Task.Run(currentMonitor.ReadNavi21SviTelemetry);
                        sviSnapshot = Navi21SviTelemetry.Parse(sviRegisters, info);
                        if (toolTableTelemetry is null)
                        {
                            combinedReadings.AddRange(sviSnapshot.Readings);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Optional telemetry must not disable ADL.
                        sviError = ex.Message;
                    }
                }

                uint? displayedToolTableVersion = toolTableSnapshot?.Version ??
                    (generation == RadeonGeneration.Rdna4 ? lastRdna4ToolTableVersion : null);
                string statisticsSource = toolTableTelemetry is not null
                    ? $"ADL+{toolTableName}Tool:{displayedToolTableVersion:X8}"
                    : navi21Supported
                        ? "ADL+Navi21SVI"
                        : "ADL";
                UpdateReadings(combinedReadings, $"{statisticsSource}:{generation}");

                StringBuilder rawDump = new();
                if (toolTableSnapshot is not null)
                {
                    rawDump.AppendLine($"{toolTableName} private SMU tool table");
                    rawDump.AppendLine(
                        $"Version 0x{toolTableSnapshot.Version:X8}, layout {toolTableSnapshot.Layout}, " +
                        $"GPU/MC 0x{toolTableSnapshot.GpuAddress:X}, " +
                        $"framebuffer [0x{toolTableSnapshot.FramebufferBase:X}, " +
                        $"0x{toolTableSnapshot.FramebufferTop:X})");
                    rawDump.AppendLine(FormatRawDump(toolTableSnapshot.Dwords));
                    if (toolTableDecodeError is not null)
                    {
                        rawDump.AppendLine();
                        rawDump.Append("Sensor decoding unavailable: ");
                        rawDump.AppendLine(toolTableDecodeError);
                    }
                    rawDump.AppendLine();
                    rawDump.AppendLine();
                }
                else if (toolTableError is not null)
                {
                    rawDump.Append($"{toolTableName} private SMU tool table unavailable: ");
                    rawDump.AppendLine(toolTableError);
                    rawDump.AppendLine();
                }

                rawDump.Append(snapshot.SensorDump);
                if (sviSnapshot is not null)
                {
                    rawDump.AppendLine();
                    rawDump.AppendLine();
                    rawDump.Append(sviSnapshot.RegisterDump);
                    RawDumpExpander.Header = toolTableSnapshot is null
                        ? "ADL PMLog + Navi21 SVI sensor dump"
                        : "Navi21 SMU tool table + ADL/SVI dump";

                    string calibration = sviSnapshot.IsCurrentCalibrated
                        ? $"calibrated ({sviSnapshot.CalibrationProfileName})"
                        : "uncalibrated physical-plane voltages";
                    DeviceInfoText.Text += $" · direct Navi21 SVI {calibration}";
                    LastUpdateText.Text =
                        $"{DateTime.Now:HH:mm:ss.fff} · {generation.ToString().ToUpperInvariant()} · " +
                        $"{snapshot.Readings.Count} ADL + {sviSnapshot.Readings.Count} SVI sensors";
                }
                else
                {
                    RawDumpExpander.Header = toolTableSnapshot is null
                        ? "ADL PMLog sensor dump"
                        : $"{toolTableName} SMU tool table + ADL PMLog dump";
                    LastUpdateText.Text =
                        $"{DateTime.Now:HH:mm:ss.fff} · {generation.ToString().ToUpperInvariant()} · " +
                        $"AMD ADL PMLog · {snapshot.Readings.Count} sensors" +
                        (toolTableSnapshot is null
                            ? string.Empty
                            : $" · raw SMU table 0x{toolTableSnapshot.Version:X8}");

                    if (sviError is not null)
                    {
                        rawDump.AppendLine();
                        rawDump.AppendLine();
                        rawDump.Append("Navi21 SVI telemetry unavailable: ");
                        rawDump.Append(sviError);
                    }
                }

                if (toolTableTelemetry is not null)
                {
                    DeviceInfoText.Text =
                        $"{FormatDeviceInfo(info)} \u00B7 ADL PMLog adapter {adlPmLogClient.AdapterIndex}: " +
                        $"{adlPmLogClient.AdapterName} \u00B7 {toolTableName} SMU table " +
                        $"0x{displayedToolTableVersion:X8}";
                    LastUpdateText.Text =
                        $"{DateTime.Now:HH:mm:ss.fff} \u00B7 {generation.ToString().ToUpperInvariant()} \u00B7 " +
                        $"{toolTableTelemetry.Readings.Count} SMU table + " +
                        $"{combinedReadings.Count - toolTableTelemetry.Readings.Count} ADL sensors";
                }
                else if (toolTableSnapshot is not null)
                {
                    DeviceInfoText.Text =
                        $"{FormatDeviceInfo(info)} \u00B7 ADL PMLog adapter {adlPmLogClient.AdapterIndex}: " +
                        $"{adlPmLogClient.AdapterName} \u00B7 {toolTableName} SMU table " +
                        $"0x{toolTableSnapshot.Version:X8}";
                }

                RawDumpTextBox.Text = rawDump.ToString();

                if (!pollingTimer.IsEnabled)
                {
                    if (toolTableTelemetry is not null)
                    {
                        string invalidDescription = toolTableTelemetry.InvalidValueCount == 0
                            ? string.Empty
                            : $" {toolTableTelemetry.InvalidValueCount} temporarily invalid rows remain visible as unavailable.";
                        SetStatus(
                            $"Read {toolTableTelemetry.Readings.Count} mapped values from {toolTableName} SMU table " +
                            $"0x{displayedToolTableVersion:X8} and retained " +
                            $"{combinedReadings.Count - toolTableTelemetry.Readings.Count} complementary ADL values." +
                            invalidDescription);
                    }
                    else if (sviSnapshot is not null)
                    {
                        string sviDescription = sviSnapshot.IsCurrentCalibrated
                            ? "voltage, calibrated current, and derived rail power"
                            : "physical-plane voltage";
                        string tableDescription = DescribeToolTableStatus(
                            toolTableSnapshot,
                            toolTableError,
                            toolTableDecodeError);
                        SetStatus(
                            $"Read {snapshot.Readings.Count} ADL PMLog and {sviSnapshot.Readings.Count} direct " +
                            $"Navi21 SVI values ({sviDescription})." + tableDescription);
                    }
                    else
                    {
                        string suffix = sviError is null ? string.Empty : $" Navi21 SVI failed: {sviError}";
                        string tableSuffix = DescribeToolTableStatus(
                            toolTableSnapshot,
                            toolTableError,
                            toolTableDecodeError);
                        SetStatus(
                            $"Read {snapshot.Readings.Count} AMD ADL PMLog values." +
                            tableSuffix + suffix);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"The Radeon driver does not expose the raw {generation.ToString().ToUpperInvariant()} " +
                    "table address, and the ADL PMLog fallback failed: " +
                    ex.Message,
                    ex);
            }
        }

        private static bool SupportsAdlPmLogFallback(RadeonGeneration generation)
        {
            return generation is RadeonGeneration.Rdna2 or RadeonGeneration.Rdna3 or RadeonGeneration.Rdna4;
        }

        private static List<MetricReading> MergeToolTableReadings(
            IReadOnlyList<MetricReading> adlReadings,
            IReadOnlyList<MetricReading> toolTableReadings,
            RadeonGeneration generation)
        {
            IReadOnlySet<(string Group, string Name)> supersededMetrics =
                generation switch
                {
                    RadeonGeneration.Rdna3 => AdlMetricsSupersededByRdna3ToolTable,
                    RadeonGeneration.Rdna4 => AdlMetricsSupersededByRdna4ToolTable,
                    _ => AdlMetricsSupersededByNavi21ToolTable
                };
            List<MetricReading> result = new(toolTableReadings.Count + adlReadings.Count);
            result.AddRange(toolTableReadings);
            result.AddRange(adlReadings.Where(reading =>
                !supersededMetrics.Contains((reading.Group, reading.Name))));
            return result;
        }

        private static RadeonToolTableTelemetry MarkTelemetryUnavailable(
            RadeonToolTableTelemetry telemetry)
        {
            MetricReading[] readings = telemetry.Readings
                .Select(reading => reading with
                {
                    CurrentValue = "\u2014",
                    Raw = "unavailable",
                    NumericValue = null
                })
                .ToArray();
            return new RadeonToolTableTelemetry(readings, readings.Length);
        }

        private static string DescribeToolTableStatus(
            RadeonToolTableSnapshot? snapshot,
            string? readError,
            string? decodeError)
        {
            if (snapshot is not null)
            {
                return decodeError is null
                    ? $" Private SMU table 0x{snapshot.Version:X8}, layout {snapshot.Layout}, was read successfully."
                    : $" Private SMU table 0x{snapshot.Version:X8} was read, but decoding is unavailable: {decodeError}";
            }

            return readError is null ? string.Empty : $" Private SMU table failed: {readError}";
        }

        private void UpdateReadings(IReadOnlyList<MetricReading> samples, string scope)
        {
            if (!string.Equals(statisticsScope, scope, StringComparison.Ordinal))
            {
                statisticsTracker.Reset();
                statisticsScope = scope;
            }

            IReadOnlyList<MetricReading> updatedReadings = statisticsTracker.Update(samples);
            readings.Clear();
            foreach (MetricReading reading in updatedReadings)
            {
                readings.Add(reading);
            }

            ResetStatisticsButton.IsEnabled = readings.Count > 0;
        }

        private void ResetStatistics()
        {
            statisticsTracker.Reset();
            lastRdna4ToolTableTelemetry = null;
            lastRdna4ToolTableVersion = null;
            statisticsScope = null;
            readings.Clear();
            ResetStatisticsButton.IsEnabled = false;
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
                ResetStatisticsButton.IsEnabled = false;
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
                ? "not exposed through C2PMSG_80/81"
                : $"GPU 0x{info.MetricsGpuAddress:X} / VRAM +0x{info.MetricsVramOffset:X}";

            return
                $"AMD 1002:{info.DeviceId:X4} rev {info.RevisionId:X2}, subsystem {info.SubsystemVendorId:X4}:{info.SubsystemDeviceId:X4}, " +
                $"PCI {info.PciAddress}, {generation} · VRAM BAR 0x{info.VramBar:X} ({FormatByteSize(info.VramBarSize)}) · " +
                $"metrics {metricsAddress} · module ABI {info.ModuleAbi}, PawnIOLib {info.PawnIoVersion}";
        }

        private static string DescribeUnavailableMetricsAddress(RadeonGeneration generation)
        {
            if (generation is RadeonGeneration.Rdna2 or RadeonGeneration.Rdna3)
            {
                return $"Metrics unavailable: this {generation.ToString().ToUpperInvariant()} driver does not expose " +
                    "the table address through C2PMSG_80/81.";
            }

            return "Metrics unavailable: C2PMSG_80/81 do not currently contain a valid table address.";
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
            adlPmLogClient?.Dispose();
            adlPmLogClient = null;
            monitor?.Dispose();
            monitor = null;
            deviceInfo = null;
            ResetStatistics();
            RawDumpTextBox.Clear();
            RawDumpExpander.Header = "Raw DWORD dump";
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
