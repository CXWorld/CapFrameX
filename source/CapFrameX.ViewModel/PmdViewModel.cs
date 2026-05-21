using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.PMD;
using CapFrameX.PMD.Benchlab;
using CapFrameX.PMD.Powenetics;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.ViewModel.SubModels;
using Microsoft.Extensions.Logging;
using OxyPlot;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
    public class PmdViewModel : BindableBase, INavigationAware
    {
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<PmdViewModel> _logger;
        private readonly IEventAggregator _eventAggregator;
        private readonly ISystemInfo _systemInfo;

        private PmdAnalysisChartManager _pmdAnalysisChartManager = new PmdAnalysisChartManager();

        private string _selectedChartView = "Frametimes";
        private bool _useUpdateSession = false;
        private ISession _session;
        private ISession _previousSession;
        private string _cpuName;
        private string _gpuName;

        public string CpuName
        {
            get => _cpuName;
            set
            {
                _cpuName = value;
                RaisePropertyChanged();
            }
        }

        public string GpuName
        {
            get => _gpuName;
            set
            {
                _gpuName = value;
                RaisePropertyChanged();
            }
        }

        public PlotModel CpuAnalysisModel => _pmdAnalysisChartManager.CpuAnalysisModel;

        public PlotModel GpuAnalysisModel => _pmdAnalysisChartManager.GpuAnalysisModel;

        public PlotModel FrametimeModel => _pmdAnalysisChartManager.PerformanceModel;

        public string SessionCpuName => _session?.Info.Processor;
        public string SessionGpuName => _session?.Info.GPU;

        public ICommand CopyGpuPowerValuesCommand { get; }

        public ICommand CopyGpuPowerPointsCommand { get; }

        public ICommand CopyCpuPowerValuesCommand { get; }

        public ICommand CopyCpuPowerPointsCommand { get; }

        public ICommand CopyCpuPowerFrameTimesCommand { get; }

        public ICommand CopyGpuPowerFrameTimesCommand { get; }


        public string AvgPmdGPUPower { get; set; } = "NaN W";
        public string AvgPmdCPUPower { get; set; } = "NaN W";
        public string AvgPmdSystemPower { get; set; } = "NaN W";
        public string AvgSensorGPUPower { get; set; } = "NaN W";
        public string AvgSensorCPUPower { get; set; } = "NaN W";
        public double AvgFPS { get; set; } = double.NaN;
        public double GpuEfficiency { get; set; } = double.NaN;
        public double CpuEfficiency { get; set; } = double.NaN;
        public double SystemEfficiency { get; set; } = double.NaN;


        public bool UseLogging
        {
            get => _appConfiguration.UsePmdDataLogging;
            set
            {
                _appConfiguration.UsePmdDataLogging = value;
                RaisePropertyChanged();
            }
        }

        public bool DrawPerformanceChart
        {
            get => _pmdAnalysisChartManager.DrawPerformanceChart;
            set
            {
                _pmdAnalysisChartManager.DrawPerformanceChart = value;
                RaisePropertyChanged();
                _pmdAnalysisChartManager.UpdatePerformanceChart(_session, _selectedChartView);
            }
        }

        public bool DrawPmdPower
        {
            get => _pmdAnalysisChartManager.DrawPmdPower;
            set
            {
                _pmdAnalysisChartManager.DrawPmdPower = value;
                RaisePropertyChanged();
                _pmdAnalysisChartManager.UpdateCpuPowerChart(_session);
                _pmdAnalysisChartManager.UpdateGpuPowerChart(_session, _appConfiguration.UseTBPSim);
            }
        }
        public bool DrawAvgPmdPower
        {
            get => _pmdAnalysisChartManager.DrawAvgPmdPower;
            set
            {
                _pmdAnalysisChartManager.DrawAvgPmdPower = value;
                RaisePropertyChanged();
                _pmdAnalysisChartManager.UpdateCpuPowerChart(_session);
                _pmdAnalysisChartManager.UpdateGpuPowerChart(_session, _appConfiguration.UseTBPSim);
            }
        }

        public bool DrawSensorPower
        {
            get => _pmdAnalysisChartManager.DrawSensorPower;
            set
            {
                _pmdAnalysisChartManager.DrawSensorPower = value;
                RaisePropertyChanged();
                _pmdAnalysisChartManager.UpdateCpuPowerChart(_session);
                _pmdAnalysisChartManager.UpdateGpuPowerChart(_session, _appConfiguration.UseTBPSim);
            }
        }

        public string SelectedChartView
        {
            get { return _selectedChartView; }
            set
            {
                _selectedChartView = value;
                RaisePropertyChanged();
                _pmdAnalysisChartManager.UpdatePerformanceChart(_session, value);
            }
        }

        public PoweneticsViewModel PoweneticsViewModel { get; }

        public BenchlabViewModel BenchlabViewModel { get; }

        public PmdViewModel(IPoweneticsService poweneticsService, IBenchlabService benchlabService, IAppConfiguration appConfiguration,
            ISensorService sensorService, ILogger<PmdViewModel> logger, IEventAggregator eventAggregator, ISystemInfo systemInfo)
        {
            _appConfiguration = appConfiguration;
            _eventAggregator = eventAggregator;
            _logger = logger;
            _systemInfo = systemInfo;

            PoweneticsViewModel = new PoweneticsViewModel(poweneticsService, appConfiguration);
            BenchlabViewModel = new BenchlabViewModel(benchlabService, appConfiguration);

            CopyGpuPowerValuesCommand = new DelegateCommand(OnCopyGpuPowerValues);
            CopyGpuPowerPointsCommand = new DelegateCommand(OnCopyGpuPowerPoints);
            CopyCpuPowerValuesCommand = new DelegateCommand(OnCopyCpuPowerValues);
            CopyCpuPowerPointsCommand = new DelegateCommand(OnCopyCpuPowerPoints);
            CopyCpuPowerFrameTimesCommand = new DelegateCommand(CopyCpuPowerFrameTimes);
            CopyGpuPowerFrameTimesCommand = new DelegateCommand(CopyGpuPowerFrameTimes);

            _pmdAnalysisChartManager.UseDarkMode = _appConfiguration.UseDarkMode;
            _pmdAnalysisChartManager.UpdateChartsTheme();

            SubscribeToAggregatorEvents();

            Task.Factory.StartNew(async () =>
            {
                await sensorService.SensorServiceCompletionSource.Task;
                await Task.Delay(500);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    CpuName = _systemInfo.GetProcessorName();
                    GpuName = _systemInfo.GetGraphicCardName();
                });
            });

            Task.Factory.StartNew(async () =>
            {
                await Task.Delay(1000);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    BenchlabViewModel.AutoStartPmdService();
                });
            });        
        }

        private void SubscribeToAggregatorEvents()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.UpdateSession>>()
                .Subscribe(msg =>
                {
                    _session = msg.CurrentSession;

                    if (_useUpdateSession)
                    {
                        UpdatePMDAnalysis();
                    }
                });

            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>()
                .Subscribe(msg =>
                {
                    _pmdAnalysisChartManager.UseDarkMode = _appConfiguration.UseDarkMode;
                    _pmdAnalysisChartManager.UpdateChartsTheme();
                });
        }

        private void OnCopyCpuPowerPoints()
        {
            if (_session == null) return;

            var pmdCpuPowerPoints = _session.GetPmdPowerPoints("CPU");

            if (pmdCpuPowerPoints.IsNullOrEmpty()) return;

            StringBuilder builder = new StringBuilder();
            builder.Append("Time [s]" + "\t" + "CPU Power [W]" + Environment.NewLine);

            for (int i = 0; i < pmdCpuPowerPoints.Count; i++)
            {
                builder.Append(RoundAndToString(pmdCpuPowerPoints[i].X, 3) + "\t" +
                    RoundAndToString(pmdCpuPowerPoints[i].Y, 2) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);

        }

        private void OnCopyCpuPowerValues()
        {
            if (_session == null) return;

            var pmdCpuPowerPoints = _session.GetPmdPowerPoints("CPU");

            if (pmdCpuPowerPoints.IsNullOrEmpty()) return;

            StringBuilder builder = new StringBuilder();
            builder.Append("CPU Power [W]" + Environment.NewLine);

            foreach (var powerValue in pmdCpuPowerPoints)
            {
                builder.Append(RoundAndToString(powerValue.Y, 2) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopyGpuPowerPoints()
        {
            if (_session == null) return;

            var pmdGpuPowerPoints = _session.GetPmdPowerPoints("GPU");

            if (pmdGpuPowerPoints.IsNullOrEmpty()) return;

            StringBuilder builder = new StringBuilder();
            builder.Append("Time [s]" + "\t" + "GPU Power [W]" + Environment.NewLine);

            for (int i = 0; i < pmdGpuPowerPoints.Count; i++)
            {
                builder.Append(RoundAndToString(pmdGpuPowerPoints[i].X, 3) + "\t" +
                    RoundAndToString(pmdGpuPowerPoints[i].Y, 2) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopyGpuPowerValues()
        {
            if (_session == null) return;

            var pmdGpuPowerPoints = _session.GetPmdPowerPoints("GPU");

            if (pmdGpuPowerPoints.IsNullOrEmpty()) return;

            StringBuilder builder = new StringBuilder();
            builder.Append("GPU Power [W]" + Environment.NewLine);

            foreach (var powerValue in pmdGpuPowerPoints)
            {
                builder.Append(RoundAndToString(powerValue.Y, 2) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }


        private void CopyGpuPowerFrameTimes()
        {
            if (_session == null) return;

            var pmdGpuPowerPoints = _session.GetPmdPowerPoints("GPU");

            if (pmdGpuPowerPoints.IsNullOrEmpty()) return;

            var frameTimePoints = _session.GetFrametimePoints();

            if (frameTimePoints.IsNullOrEmpty()) return;

            var pmdSamples = pmdGpuPowerPoints
                .Select(point => new PoweneticsSample() { Time = point.X, Value = point.Y })
                .ToArray();

            var frameTimeSamples = frameTimePoints
                .Select(point => new PoweneticsSample() { Time = point.X, Value = point.Y })
                .ToArray();

            var mappedSamples = PoweneticsDataProcessing.GetMappedPmdData(frameTimeSamples, pmdSamples);
            StringBuilder builder = new StringBuilder();
            builder.Append("Time [s]" + "\t" + "Frametime [ms]" + "\t" + "GPU Power [W]" + Environment.NewLine);

            for (int i = 0; i < mappedSamples.Length; i++)
            {
                builder.Append(RoundAndToString(frameTimeSamples[i].Time, 3) + "\t" +
                    RoundAndToString(frameTimeSamples[i].Value, 2) + "\t" +
                    RoundAndToString(mappedSamples[i].Value, 2) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void CopyCpuPowerFrameTimes()
        {
            if (_session == null) return;

            var pmdCpuPowerPoints = _session.GetPmdPowerPoints("CPU");

            if (pmdCpuPowerPoints.IsNullOrEmpty()) return;

            var frameTimePoints = _session.GetFrametimePoints();

            if (frameTimePoints.IsNullOrEmpty()) return;

            var pmdSamples = pmdCpuPowerPoints
                .Select(point => new PoweneticsSample() { Time = point.X, Value = point.Y })
                .ToArray();

            var frameTimeSamples = frameTimePoints
                .Select(point => new PoweneticsSample() { Time = point.X, Value = point.Y })
                .ToArray();

            var mappedSamples = PoweneticsDataProcessing.GetMappedPmdData(frameTimeSamples, pmdSamples);
            StringBuilder builder = new StringBuilder();
            builder.Append("Time [s]" + "\t" + "Frametime [ms]" + "\t" + "CPU Power [W]" + Environment.NewLine);

            for (int i = 0; i < mappedSamples.Length; i++)
            {
                builder.Append(RoundAndToString(frameTimeSamples[i].Time, 3) + "\t" +
                    RoundAndToString(frameTimeSamples[i].Value, 2) + "\t" +
                    RoundAndToString(mappedSamples[i].Value, 2) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private string RoundAndToString(double value, int digits)
            => Math.Round(value, digits).ToString(CultureInfo.InvariantCulture);

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _useUpdateSession = false;
            PoweneticsViewModel.ManageChartsUpdate();
            BenchlabViewModel.ManageChartsUpdate();
            _previousSession = _session;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _useUpdateSession = true;
            PoweneticsViewModel.ManageChartsUpdate();
            BenchlabViewModel.ManageChartsUpdate();

            CpuName = _systemInfo.GetProcessorName();
            GpuName = _systemInfo.GetGraphicCardName();

            if (_session == null || _session?.Hash != _previousSession?.Hash)
            {
                UpdatePMDAnalysis();
            }
        }

        private void UpdatePMDAnalysis()
        {
            _pmdAnalysisChartManager.UpdateGpuPowerChart(_session, _appConfiguration.UseTBPSim);
            _pmdAnalysisChartManager.UpdateCpuPowerChart(_session);
            _pmdAnalysisChartManager.UpdatePerformanceChart(_session, _selectedChartView);
            _pmdAnalysisChartManager.ResetAnalysisPlotModels();
            RaisePropertyChanged(nameof(SessionCpuName));
            RaisePropertyChanged(nameof(SessionGpuName));
            GetAverageMetrics();
        }


        private void GetAverageMetrics()
        {
            //Initialize values
            double cpuPmdAverage = double.NaN;
            double gpuPmdAverage = double.NaN;
            double systemPmdAverage = double.NaN;
            double cpuSensorAverage = double.NaN;
            double gpuSensorAverage = double.NaN;
            double cpuEfficiency = double.NaN;
            double gpuEfficiency = double.NaN;
            double systemEfficiency = double.NaN;
            double fpsaverage = double.NaN;

            void UpdateMetrics()
            {
                AvgPmdGPUPower = $"{gpuPmdAverage} W";
                AvgPmdCPUPower = $"{cpuPmdAverage} W";
                AvgPmdSystemPower = $"{Math.Round(systemPmdAverage, 0)} W";
                AvgSensorGPUPower = $"{gpuSensorAverage} W";
                AvgSensorCPUPower = $"{cpuSensorAverage} W";
                AvgFPS = fpsaverage;
                GpuEfficiency = Math.Round(gpuEfficiency, 2);
                CpuEfficiency = Math.Round(cpuEfficiency, 2);
                SystemEfficiency = Math.Round(systemEfficiency, 2);

                RaisePropertyChanged(nameof(AvgPmdGPUPower));
                RaisePropertyChanged(nameof(AvgPmdCPUPower));
                RaisePropertyChanged(nameof(AvgSensorGPUPower));
                RaisePropertyChanged(nameof(AvgSensorCPUPower));
                RaisePropertyChanged(nameof(AvgPmdSystemPower));
                RaisePropertyChanged(nameof(AvgFPS));
                RaisePropertyChanged(nameof(GpuEfficiency));
                RaisePropertyChanged(nameof(CpuEfficiency));
                RaisePropertyChanged(nameof(SystemEfficiency));
            }

            if (_session == null || !_session.Runs.Where(r => r.SensorData2 != null).Any())
            {
                UpdateMetrics();
                return;
            }

            // Power & Performance Metrics

            // CPU PMD Power           
            var cpuPmdPowers = _session.Runs.Where(r => !r.PmdCpuPower.IsNullOrEmpty());
            if (cpuPmdPowers != null && cpuPmdPowers.Any())
            {
                cpuPmdAverage = Math.Round(cpuPmdPowers.SelectMany(x => x.PmdCpuPower).Average(), 0);
            }

            // GPU PMD Power           
            var gpuPmdPowers = _session.Runs.Where(r => !r.PmdGpuPower.IsNullOrEmpty());
            if (gpuPmdPowers != null && gpuPmdPowers.Any())
            {
                gpuPmdAverage = Math.Round(gpuPmdPowers.SelectMany(x => x.PmdGpuPower).Average(), 0);
            }

            // System PMD Power           
            var systemPmdPowers = _session.Runs.Where(r => !r.PmdSystemPower.IsNullOrEmpty());
            if (systemPmdPowers != null && systemPmdPowers.Any())
            {
                systemPmdAverage = Math.Round(systemPmdPowers.SelectMany(x => x.PmdSystemPower).Average(), 0);
            }

            // CPU Sensor Power
            var cpuSensorPowers = _session.Runs.Where(r => !r.SensorData2.CpuPower.IsNullOrEmpty());
            if (cpuSensorPowers != null && cpuSensorPowers.Any())
            {
                cpuSensorAverage = Math.Round(cpuSensorPowers.SelectMany(x => x.SensorData2.CpuPower).Average(), 0);
            }

            // GPU Sensor Power
            var gpuSensorPowers = _session.Runs.Where(r => !r.SensorData2.GpuPower.IsNullOrEmpty());

            if (_appConfiguration.UseTBPSim)
            {
                var gpuTBPSimPowers = _session.Runs.Where(r => !r.SensorData2.GpuTBPSim.IsNullOrEmpty());

                if (gpuTBPSimPowers != null && gpuTBPSimPowers.Any())
                {
                    gpuSensorAverage = Math.Round(gpuSensorPowers.SelectMany(x => x.SensorData2.GpuTBPSim).Average(), 0);
                }
                else if (gpuSensorPowers != null && gpuSensorPowers.Any())
                {
                    gpuSensorAverage = Math.Round(gpuSensorPowers.SelectMany(x => x.SensorData2.GpuPower).Average(), 0);
                }

            }
            else if (gpuSensorPowers != null && gpuSensorPowers.Any())
            {
                gpuSensorAverage = Math.Round(gpuSensorPowers.SelectMany(x => x.SensorData2.GpuPower).Average(), 0);
            }

            // FPS
            double frametimes = _session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).Average();
            fpsaverage = Math.Round(1000 / frametimes, 1);

            // Efficiency Metrics

            if (!double.IsNaN(gpuPmdAverage))
                gpuEfficiency = fpsaverage / gpuPmdAverage * 10;
            else if (!double.IsNaN(gpuSensorAverage))
                gpuEfficiency = fpsaverage / gpuSensorAverage * 10;

            if (!double.IsNaN(cpuPmdAverage))
                cpuEfficiency = fpsaverage / cpuPmdAverage * 10;
            else if (!double.IsNaN(cpuSensorAverage))
                cpuEfficiency = fpsaverage / cpuSensorAverage * 10;

            if (!double.IsNaN(systemPmdAverage))
                systemEfficiency = fpsaverage / systemPmdAverage * 10;

            UpdateMetrics();
        }
    }
}
