using CapFrameX.Contracts.Sensor;
using CapFrameX.PmcReader.Plugin;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PmcReader.TestApp
{
    public partial class MainWindow : Window
    {
        private static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(1);

        private readonly BehaviorSubject<TimeSpan> _updateIntervalSubject = new BehaviorSubject<TimeSpan>(DefaultInterval);
        private readonly ObservableCollection<SensorRow> _sensors = new ObservableCollection<SensorRow>();
        private PmcReaderSensorPlugin _plugin;
        private IDisposable _subscription;
        private TimeSpan _currentInterval = DefaultInterval;

        public ObservableCollection<SensorRow> Sensors => _sensors;

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        private async void Window_OnLoaded(object sender, RoutedEventArgs e)
        {
            StatusText.Text = "Initializing...";
            await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            string diagnosticsText = WriteDiagnostics(out string diagnosticsPath);
            if (!string.IsNullOrWhiteSpace(diagnosticsText))
            {
                DiagnosticsTextBox.Text = diagnosticsText;
            }
            if (!string.IsNullOrWhiteSpace(diagnosticsPath))
            {
                Trace.WriteLine($"PmcReader diagnostics written to {diagnosticsPath}");
            }

            _plugin = new PmcReaderSensorPlugin();
            await _plugin.InitializeAsync(_updateIntervalSubject);

            _subscription = _plugin.SensorSnapshotStream.Subscribe(snapshot =>
            {
                Dispatcher.Invoke(() =>
                {
                    UpdateSnapshot(snapshot.Item1, snapshot.Item2);
                });
            });

            StatusText.Text = "Running";
            _updateIntervalSubject.OnNext(_currentInterval);
        }

        private static string WriteDiagnostics(out string diagnosticsPath)
        {
            diagnosticsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pmcreader-diagnostics.txt");
            var log = new StringBuilder();
            log.AppendLine("PmcReader diagnostics");
            log.AppendLine($"Timestamp (UTC): {DateTime.UtcNow:O}");

            int threadCount = Environment.ProcessorCount;
            int coreCount = GetCoreCount();
            log.AppendLine($"ThreadCount: {threadCount}");
            log.AppendLine($"CoreCount: {coreCount}");

            try
            {
                PmcReaderInterop.Open();
                string manufacturer = PmcReaderInterop.GetManufacturerId();
                PmcReaderInterop.GetProcessorVersion(out byte family, out byte model, out byte stepping);
                log.AppendLine($"Manufacturer: {manufacturer}");
                log.AppendLine($"Family: 0x{family:X2}, Model: 0x{model:X2}, Stepping: 0x{stepping:X2}");

                var ccxIndexMap = new Dictionary<int, int>();
                var threadsByCcx = new Dictionary<int, List<int>>();

                for (int threadIdx = 0; threadIdx < threadCount; threadIdx++)
                {
                    string affinity = threadIdx < 64 ? $"0x{(1UL << threadIdx):X}" : "n/a";
                    if (!PmcReaderInterop.TryGetExtendedTopology(threadIdx, out uint eax, out uint ebx, out uint ecx, out uint edx))
                    {
                        log.AppendLine($"Thread {threadIdx}: affinity={affinity} cpuid=failed");
                        continue;
                    }

                    uint extendedApicId = eax;
                    int? rawCcxId = TryGetCcxId(family, extendedApicId);
                    int? normalizedCcxId = null;
                    if (rawCcxId.HasValue)
                    {
                        if (!ccxIndexMap.TryGetValue(rawCcxId.Value, out int ccxIndex))
                        {
                            ccxIndex = ccxIndexMap.Count;
                            ccxIndexMap.Add(rawCcxId.Value, ccxIndex);
                        }

                        normalizedCcxId = ccxIndexMap[rawCcxId.Value];
                        if (!threadsByCcx.TryGetValue(normalizedCcxId.Value, out var ccxThreads))
                        {
                            ccxThreads = new List<int>();
                            threadsByCcx.Add(normalizedCcxId.Value, ccxThreads);
                        }

                        ccxThreads.Add(threadIdx);
                    }

                    log.AppendLine(
                        $"Thread {threadIdx}: affinity={affinity} eax=0x{eax:X8} ebx=0x{ebx:X8} ecx=0x{ecx:X8} edx=0x{edx:X8} " +
                        $"extApicId=0x{extendedApicId:X8} rawCcx={FormatOptional(rawCcxId)} ccx={FormatOptional(normalizedCcxId)}");
                }

                if (threadsByCcx.Count > 0)
                {
                    log.AppendLine("CCX thread groups:");
                    foreach (var entry in threadsByCcx.OrderBy(pair => pair.Key))
                    {
                        log.AppendLine($"  CCX {entry.Key}: {string.Join(", ", entry.Value.OrderBy(value => value))}");
                    }
                }
            }
            catch (Exception ex)
            {
                log.AppendLine("Diagnostics failed:");
                log.AppendLine(ex.ToString());
            }
            finally
            {
                try
                {
                    PmcReaderInterop.Close();
                }
                catch (Exception ex)
                {
                    log.AppendLine("Failed to close PmcReaderInterop:");
                    log.AppendLine(ex.Message);
                }
            }

            try
            {
                File.WriteAllText(diagnosticsPath, log.ToString());
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Failed to write diagnostics: {ex}");
                return log.ToString();
            }

            return log.ToString();
        }

        private static int GetCoreCount()
        {
            try
            {
                int coreCount = 0;
                foreach (var item in new ManagementObjectSearcher("Select * from Win32_Processor").Get())
                {
                    coreCount += int.Parse(item["NumberOfCores"].ToString());
                }

                return coreCount;
            }
            catch
            {
                return 0;
            }
        }

        private static int? TryGetCcxId(byte family, uint extendedApicId)
        {
            if (family == 0x17)
                return (int)(extendedApicId >> 3);
            if (family == 0x19 || family == 0x1A)
                return (int)(extendedApicId >> 4);

            return null;
        }

        private static string FormatOptional(int? value)
        {
            return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "n/a";
        }

        private void UpdateSnapshot(DateTime timestamp, Dictionary<ISensorEntry, float> values)
        {
            LastUpdateText.Text = timestamp.ToLocalTime()
                .ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

            _sensors.Clear();
            foreach (var pair in values.OrderBy(p => p.Key.Name))
            {
                var display = FormatValue(pair.Key, pair.Value);
                _sensors.Add(new SensorRow
                {
                    Name = pair.Key.Name,
                    Value = display.ValueText,
                    Unit = display.Unit
                });
            }

            StatusText.Text = values.Count == 0
                ? "No metrics available (unsupported CPU or driver access failed)."
                : string.Empty;
        }

        private void IntervalComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IntervalComboBox.SelectedItem is ComboBoxItem item
                && item.Tag is string tag
                && TimeSpan.TryParse(tag, CultureInfo.InvariantCulture, out var interval))
            {
                _currentInterval = interval;
                _updateIntervalSubject.OnNext(_currentInterval);
            }
        }

        private void RefreshButton_OnClick(object sender, RoutedEventArgs e)
        {
            _updateIntervalSubject.OnNext(_currentInterval);
        }

        private void Window_OnClosed(object sender, EventArgs e)
        {
            _subscription?.Dispose();
            _updateIntervalSubject.Dispose();
        }

        private static SensorDisplayValue FormatValue(ISensorEntry entry, float value)
        {
            string sensorType = entry?.SensorType?.ToString();
            switch (sensorType)
            {
                case "Load":
                    return new SensorDisplayValue(value.ToString("0.00", CultureInfo.InvariantCulture), "%");
                case "Throughput":
                    return new SensorDisplayValue(value.ToString("0.###", CultureInfo.InvariantCulture), "GiB/s");
                case "Timing":
                case "Latency":
                    return new SensorDisplayValue(value.ToString("0.###", CultureInfo.InvariantCulture), "ns");
                default:
                    return new SensorDisplayValue(value.ToString("0.###", CultureInfo.InvariantCulture), string.Empty);
            }
        }

        private readonly struct SensorDisplayValue
        {
            public SensorDisplayValue(string valueText, string unit)
            {
                ValueText = valueText;
                Unit = unit;
            }

            public string ValueText { get; }
            public string Unit { get; }
        }
    }
}
