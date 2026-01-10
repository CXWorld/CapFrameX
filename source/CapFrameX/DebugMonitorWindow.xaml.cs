using CapFrameX.Capture.Contracts;
using CapFrameX.PresentMonInterface;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace CapFrameX
{
    public partial class DebugMonitorWindow : Window, INotifyPropertyChanged
    {
        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceCounter(out long lpPerformanceCount);

        [DllImport("kernel32.dll")]
        private static extern bool QueryPerformanceFrequency(out long lpFrequency);

        private readonly ICaptureService _captureService;
        private readonly DispatcherTimer _performanceTimer;
        private IDisposable _frameDataSubscription;
        private readonly double _qpcFrequency;

        private Process _capFrameXProcess;
        private Process _presentMonProcess;
        private DateTime _lastCpuCheckTime;
        private TimeSpan _lastCapFrameXCpuTime;
        private TimeSpan _lastPresentMonCpuTime;

        // CapFrameX metrics
        private double _capFrameXCpuUsage;
        public double CapFrameXCpuUsage
        {
            get => _capFrameXCpuUsage;
            set { _capFrameXCpuUsage = value; OnPropertyChanged(); }
        }

        private double _capFrameXMemoryUsage;
        public double CapFrameXMemoryUsage
        {
            get => _capFrameXMemoryUsage;
            set { _capFrameXMemoryUsage = value; OnPropertyChanged(); }
        }

        // PresentMon metrics
        private double _presentMonCpuUsage;
        public double PresentMonCpuUsage
        {
            get => _presentMonCpuUsage;
            set { _presentMonCpuUsage = value; OnPropertyChanged(); }
        }

        private double _presentMonMemoryUsage;
        public double PresentMonMemoryUsage
        {
            get => _presentMonMemoryUsage;
            set { _presentMonMemoryUsage = value; OnPropertyChanged(); }
        }

        private string _presentMonStatus = "Not Running";
        public string PresentMonStatus
        {
            get => _presentMonStatus;
            set { _presentMonStatus = value; OnPropertyChanged(); }
        }

        // ETW metrics
        private double _etwBufferFillPct;
        public double EtwBufferFillPct
        {
            get => _etwBufferFillPct;
            set { _etwBufferFillPct = value; OnPropertyChanged(); }
        }

        private int _etwBuffersInUse;
        public int EtwBuffersInUse
        {
            get => _etwBuffersInUse;
            set { _etwBuffersInUse = value; OnPropertyChanged(); }
        }

        private int _etwTotalBuffers;
        public int EtwTotalBuffers
        {
            get => _etwTotalBuffers;
            set { _etwTotalBuffers = value; OnPropertyChanged(); }
        }

        private int _etwEventsLost;
        public int EtwEventsLost
        {
            get => _etwEventsLost;
            set { _etwEventsLost = value; OnPropertyChanged(); }
        }

        private int _etwBuffersLost;
        public int EtwBuffersLost
        {
            get => _etwBuffersLost;
            set { _etwBuffersLost = value; OnPropertyChanged(); }
        }

        // Processing delay metric
        private double _processingDelayMs;
        public double ProcessingDelayMs
        {
            get => _processingDelayMs;
            set { _processingDelayMs = value; OnPropertyChanged(); }
        }

        private DateTime _lastUpdateTime;
        public DateTime LastUpdateTime
        {
            get => _lastUpdateTime;
            set { _lastUpdateTime = value; OnPropertyChanged(); }
        }

        private string _presentMonApplicationName;

        public string PresentMonApplicationName
        {
            get => _presentMonApplicationName;
            set { _presentMonApplicationName = value; OnPropertyChanged(); }
        }

        public DebugMonitorWindow(ICaptureService captureService)
        {
            InitializeComponent();
            DataContext = this;

            _captureService = captureService;
            _capFrameXProcess = Process.GetCurrentProcess();
            _lastCpuCheckTime = DateTime.UtcNow;
            _lastCapFrameXCpuTime = _capFrameXProcess.TotalProcessorTime;

            // Set PresentMon application name
            var name = CaptureServiceConfiguration.PresentMonAppName;

            // Replace "_" with "__" in name for display purposes
            PresentMonApplicationName = name.Replace("_", "__"); ;

            // Initialize QPC frequency for delay calculation
            QueryPerformanceFrequency(out long lpFrequency);
            _qpcFrequency = lpFrequency;

            // Setup performance monitoring timer
            _performanceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _performanceTimer.Tick += PerformanceTimer_Tick;
            _performanceTimer.Start();

            // Subscribe to frame data stream for ETW metrics
            SubscribeToFrameData();

            Closed += DebugMonitorWindow_Closed;
        }

        private void SubscribeToFrameData()
        {
            _frameDataSubscription = _captureService.FrameDataStream
                .Sample(TimeSpan.FromMilliseconds(250))
                .ObserveOn(Application.Current.Dispatcher)
                .Subscribe(OnFrameData);
        }

        private void OnFrameData(string[] frameData)
        {
            try
            {
                if (frameData.Length >= _captureService.ValidLineLength)
                {
                    // Calculate processing delay
                    if (double.TryParse(frameData[_captureService.CPUStartQPCTimeInMs_Index],
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double cpuStartQpcTimeMs))
                    {
                        QueryPerformanceCounter(out long currentCounter);
                        double currentQpcTimeMs = (currentCounter / _qpcFrequency) * 1000.0;
                        ProcessingDelayMs = currentQpcTimeMs - cpuStartQpcTimeMs;
                    }

                    if (double.TryParse(frameData[_captureService.EtwBufferFillPct_Index],
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out double bufferFillPct))
                    {
                        EtwBufferFillPct = bufferFillPct;
                    }

                    if (int.TryParse(frameData[_captureService.EtwBuffersInUse_Index], out int buffersInUse))
                    {
                        EtwBuffersInUse = buffersInUse;
                    }

                    if (int.TryParse(frameData[_captureService.EtwTotalBuffers_Index], out int totalBuffers))
                    {
                        EtwTotalBuffers = totalBuffers;
                    }

                    if (int.TryParse(frameData[_captureService.EtwEventsLost_Index], out int eventsLost))
                    {
                        EtwEventsLost = eventsLost;
                    }

                    if (int.TryParse(frameData[_captureService.EtwBuffersLost_Index], out int buffersLost))
                    {
                        EtwBuffersLost = buffersLost;
                    }

                    LastUpdateTime = DateTime.Now;
                }
            }
            catch
            {
                // Ignore parsing errors
            }
        }

        private void PerformanceTimer_Tick(object sender, EventArgs e)
        {
            UpdateCapFrameXMetrics();
            UpdatePresentMonMetrics();
        }

        private void UpdateCapFrameXMetrics()
        {
            try
            {
                _capFrameXProcess.Refresh();

                // Calculate CPU usage
                var currentTime = DateTime.UtcNow;
                var currentCpuTime = _capFrameXProcess.TotalProcessorTime;
                var cpuUsedMs = (currentCpuTime - _lastCapFrameXCpuTime).TotalMilliseconds;
                var totalMsPassed = (currentTime - _lastCpuCheckTime).TotalMilliseconds;

                if (totalMsPassed > 0)
                {
                    CapFrameXCpuUsage = (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;
                }

                _lastCapFrameXCpuTime = currentCpuTime;

                // Memory usage in MB
                CapFrameXMemoryUsage = _capFrameXProcess.WorkingSet64 / (1024.0 * 1024.0);
            }
            catch
            {
                // Process may have exited
            }
        }

        private void UpdatePresentMonMetrics()
        {
            try
            {
                // Find PresentMon process
                var presentMonProcesses = Process.GetProcessesByName(CaptureServiceConfiguration.PresentMonAppName);

                if (presentMonProcesses.Length > 0)
                {
                    var newProcess = presentMonProcesses[0];

                    if (_presentMonProcess == null || _presentMonProcess.Id != newProcess.Id)
                    {
                        _presentMonProcess = newProcess;
                        _lastPresentMonCpuTime = _presentMonProcess.TotalProcessorTime;
                        PresentMonStatus = "Running";
                    }

                    _presentMonProcess.Refresh();

                    // Calculate CPU usage
                    var currentTime = DateTime.UtcNow;
                    var currentCpuTime = _presentMonProcess.TotalProcessorTime;
                    var cpuUsedMs = (currentCpuTime - _lastPresentMonCpuTime).TotalMilliseconds;
                    var totalMsPassed = (currentTime - _lastCpuCheckTime).TotalMilliseconds;

                    if (totalMsPassed > 0)
                    {
                        PresentMonCpuUsage = (cpuUsedMs / (Environment.ProcessorCount * totalMsPassed)) * 100;
                    }

                    _lastPresentMonCpuTime = currentCpuTime;

                    // Memory usage in MB
                    PresentMonMemoryUsage = _presentMonProcess.WorkingSet64 / (1024.0 * 1024.0);
                }
                else
                {
                    _presentMonProcess = null;
                    PresentMonCpuUsage = 0;
                    PresentMonMemoryUsage = 0;
                    PresentMonStatus = "Not Running";
                }

                // Update the last check time
                _lastCpuCheckTime = DateTime.UtcNow;
            }
            catch
            {
                PresentMonStatus = "Error";
            }
        }

        private void DebugMonitorWindow_Closed(object sender, EventArgs e)
        {
            _performanceTimer?.Stop();
            _frameDataSubscription?.Dispose();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
