using LibreHardwareMonitor.Hardware;
using LibreHardwareMonitor.PawnIo;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;

namespace CapFrameX.VoltageMonitor
{
    public partial class MainWindow : Window
    {
        private const uint IA32_THERM_STATUS = 0x019C;
        private const uint MSR_PLATFORM_INFO = 0x00CE;
        private const uint IA32_PERF_STATUS = 0x0198;
        private const uint MSR_PP1_ENERGY_STATUS = 0x0641;

        private readonly IntelMsr _msr;
        private readonly DispatcherTimer _timer;
        private readonly int _processorCount;
        private readonly ObservableCollection<CoreVoltageInfo> _voltageData;

        public MainWindow()
        {
            InitializeComponent();

            _processorCount = Environment.ProcessorCount;
            _voltageData = new ObservableCollection<CoreVoltageInfo>();

            // Initialize data for each core
            for (int i = 0; i < _processorCount; i++)
            {
                _voltageData.Add(new CoreVoltageInfo { Core = i });
            }

            VoltageGrid.ItemsSource = _voltageData;
            ProcessorInfoText.Text = $"Logical Processors: {_processorCount}";

            // Initialize MSR reader
            try
            {
                _msr = new IntelMsr();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize MSR reader: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Setup 1-second polling timer
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            // Initial read
            UpdateVoltages();

            Closed += MainWindow_Closed;
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            _timer?.Stop();
            _msr?.Close();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            UpdateVoltages();
        }

        private void UpdateVoltages()
        {
            for (int core = 0; core < _processorCount; core++)
            {
                var affinity = GroupAffinity.Single(0, core);
                var info = _voltageData[core];

                // Read IA32_THERM_STATUS (0x019C)
                if (_msr.ReadMsr(IA32_THERM_STATUS, out uint eax, out uint edx, affinity))
                {
                    ulong raw = ((ulong)edx << 32) | eax;
                    float voltage = (eax & 0xFFFF) / (float)(1 << 13);
                    info.ThermStatusVoltage = $"{voltage:F4} V";
                    info.ThermStatusRaw = $"0x{raw:X16}";
                }
                else
                {
                    info.ThermStatusVoltage = "N/A";
                    info.ThermStatusRaw = "N/A";
                }

                // Read MSR_PLATFORM_INFO (0x00CE)
                if (_msr.ReadMsr(MSR_PLATFORM_INFO, out eax, out edx, affinity))
                {
                    float voltage = (eax & 0xFFFF) / (float)(1 << 13);
                    info.PlatformInfoVoltage = $"{voltage:F4} V";
                }
                else
                {
                    info.PlatformInfoVoltage = "N/A";
                }

                // Read IA32_PERF_STATUS (0x0198) - EDX contains VID on older CPUs
                if (_msr.ReadMsr(IA32_PERF_STATUS, out eax, out edx, affinity))
                {
                    uint vidEdx = edx & 0xFFFF;
                    float voltageEdx = vidEdx / (float)(1 << 13);

                    if (vidEdx > 0)
                    {
                        info.PerfStatusVoltage = $"{voltageEdx:F4} V";
                    }
                    else
                    {
                        // Try EAX for alternative interpretation
                        float voltageEax = (eax & 0xFFFF) / (float)(1 << 13);
                        info.PerfStatusVoltage = $"{voltageEax:F4} V (EAX)";
                    }
                }
                else
                {
                    info.PerfStatusVoltage = "N/A";
                }

                // Read MSR_PP1_ENERGY_STATUS (0x0641)
                if (_msr.ReadMsr(MSR_PP1_ENERGY_STATUS, out eax, out edx, affinity))
                {
                    float voltage = (eax & 0xFFFF) / (float)(1 << 13);
                    info.Pp1EnergyVoltage = $"{voltage:F4} V";
                }
                else
                {
                    info.Pp1EnergyVoltage = "N/A";
                }
            }

            LastUpdateText.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
        }
    }

    public class CoreVoltageInfo : INotifyPropertyChanged
    {
        private int _core;
        private string _thermStatusVoltage;
        private string _thermStatusRaw;
        private string _platformInfoVoltage;
        private string _perfStatusVoltage;
        private string _pp1EnergyVoltage;

        public int Core
        {
            get => _core;
            set { _core = value; OnPropertyChanged(nameof(Core)); }
        }

        public string ThermStatusVoltage
        {
            get => _thermStatusVoltage;
            set { _thermStatusVoltage = value; OnPropertyChanged(nameof(ThermStatusVoltage)); }
        }

        public string ThermStatusRaw
        {
            get => _thermStatusRaw;
            set { _thermStatusRaw = value; OnPropertyChanged(nameof(ThermStatusRaw)); }
        }

        public string PlatformInfoVoltage
        {
            get => _platformInfoVoltage;
            set { _platformInfoVoltage = value; OnPropertyChanged(nameof(PlatformInfoVoltage)); }
        }

        public string PerfStatusVoltage
        {
            get => _perfStatusVoltage;
            set { _perfStatusVoltage = value; OnPropertyChanged(nameof(PerfStatusVoltage)); }
        }

        public string Pp1EnergyVoltage
        {
            get => _pp1EnergyVoltage;
            set { _pp1EnergyVoltage = value; OnPropertyChanged(nameof(Pp1EnergyVoltage)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
