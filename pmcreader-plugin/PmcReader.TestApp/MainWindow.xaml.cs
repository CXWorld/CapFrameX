using CapFrameX.Contracts.Sensor;
using CapFrameX.PmcReader.Plugin;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reactive.Subjects;
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
