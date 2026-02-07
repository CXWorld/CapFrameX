using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Sensor.Reporting;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using GongSolutions.Wpf.DragDrop;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CapFrameX.ViewModel
{
    public partial class ReportViewModel : BindableBase, INavigationAware, IDropTarget
    {
        private readonly IStatisticProvider _frametimeStatisticProvider;
        private readonly IEventAggregator _eventAggregator;
        private readonly IAppConfiguration _appConfiguration;
        private readonly RecordManager _recordManager;
        private static ILogger<ReportViewModel> _logger;

        private bool _useEventMessages;
        private bool _hasNoReportItems = true;

        public bool HasNoReportItems
        {
            get { return _hasNoReportItems; }
            set
            {
                _hasNoReportItems = value;
                RaisePropertyChanged();
            }
        }

        public bool ReportShowAverageRow
        {
            get { return _appConfiguration.ReportShowAverageRow; }
            set
            {
                _appConfiguration.ReportShowAverageRow = value;
                if (value)
                    AddAverageReportInfo(ReportInfoCollection);
                else
                {
                    if (ReportInfoCollection.Any())
                        ReportInfoCollection.RemoveAt(ReportInfoCollection.Count - 1);
                }

                RaisePropertyChanged();
            }
        }

        public bool ReportUsePMDValues
        {
            get { return _appConfiguration.ReportUsePMDValues; }
            set
            {
                _appConfiguration.ReportUsePMDValues = value;
                ReportInfoCollection.Clear();

                RaisePropertyChanged();
            }
        }

        public ObservableCollection<ReportInfo> ReportInfoCollection { get; }
            = new ObservableCollection<ReportInfo>();

        public ICommand CopyTableDataCommand { get; }

        public ICommand RemoveAllReportEntriesCommand { get; }

        public ReportViewModel(IStatisticProvider frametimeStatisticProvider,
            IEventAggregator eventAggregator,
            IAppConfiguration appConfiguration,
            RecordManager recordManager,
            ILogger<ReportViewModel> logger)
        {
            _frametimeStatisticProvider = frametimeStatisticProvider;
            _eventAggregator = eventAggregator;
            _appConfiguration = appConfiguration;
            _recordManager = recordManager;
            _logger = logger;

            CopyTableDataCommand = new DelegateCommand(OnCopyTableData);
            RemoveAllReportEntriesCommand = new DelegateCommand(() => ReportInfoCollection.Clear());
            ReportInfoCollection.CollectionChanged += new NotifyCollectionChangedEventHandler
                ((sender, eventArg) =>
                {
                    HasNoReportItems = !ReportInfoCollection.Any();
                });

            InitializeReportParameters();
            SubscribeToSelectRecord();
        }

        public void RemoveReportEntry(ReportInfo selectedItem)
        {
            if (selectedItem.Game.Equals("Averaged values"))
                return;
            else
            {
                ReportInfoCollection.Remove(selectedItem);

                if (ReportShowAverageRow)
                    AddAverageReportInfo(ReportInfoCollection);
            }
        }

        private void OnCopyTableData()
        {
            if (!ReportInfoCollection.Any())
                return;

            var builder = new StringBuilder();

            // Header
            var displayNameGame = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.Game);
            var displayNameDate = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.Date);
            var displayNameTime = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.Time);
            var displayNameNumberOfSamples = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.NumberOfSamples);
            var displayNameRecordTime = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.RecordTime);
            var displayNameCpu = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.Cpu);
            var displayNameGraphicCard = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GraphicCard);
            var displayNameRam = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.Ram);
            var displayNameMaxFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.MaxFps);
            var displayNameNinetyNinePercentQuantileFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.NinetyNinePercentQuantileFps);
            var displayNameNinetyFivePercentQuantileFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.NinetyFivePercentQuantileFps);
            var displayNameAverageFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.AverageFps);       
            var displayNameMedianFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.MedianFps);
            var displayNameFivePercentQuantileFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.FivePercentQuantileFps);
            var displayNameOnePercentQuantileFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.OnePercentQuantileFps);
            var displayNameOnePercentLowAverageFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.OnePercentLowAverageFps);
            var displayNameOnePercentLowIntegralFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.OnePercentLowIntegralFps);
            var displayNameZeroDotTwoPercentQuantileFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.ZeroDotTwoPercentQuantileFps);
            var displayNameZeroDotOnePercentQuantileFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.ZeroDotOnePercentQuantileFps);
            var displayNameZeroDotOnePercentLowAverageFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.ZeroDotOnePercentLowAverageFps);
            var displayNameZeroDotOnePercentLowIntegralFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.ZeroDotOnePercentLowIntegralFps);
            var displayNameMinFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.MinFps);
            var displayNameAdaptiveSTDFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.AdaptiveSTDFps);
            var displayNameCpuFpsPerWatt = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CpuFpsPerWatt);
            var displayNameGpuFpsPerWatt = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuFpsPerWatt);
            var displayNameAppLatency = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.AppLatency);
            var displayNameGpuActiveTimeAverage = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuActiveTimeAverage);
            var displayNameGpuActiveTimeDeviation = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuActiveTimeDeviation);
            var displayNameCpuUsage = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CpuMaxUsage);
            var displayNameCpuPower = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CpuPower);
            var displayNameCpuClock = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CpuMaxClock);
            var displayNameCpuTemp = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CpuTemp);
            var displayNameGpuUsage = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuUsage);
            var displayNameGpuPower = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuPower);
            var displayNameGpuTBPSim = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuTBPSim);
            var displayNameGpuClock = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuClock);
            var displayNameGpuTemp = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuTemp);
            var displayNameCustomComment = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CustomComment);

            builder.Append(displayNameGame +
                (ShowCreationDate ? "\t" + displayNameDate : "") +
                (ShowCreationTime ? "\t" + displayNameTime : "") +
                (ShowNumberOfSamples ? "\t" + displayNameNumberOfSamples : "") +
                (ShowRecordTime ? "\t" + displayNameRecordTime : "") +
                (ShowCpuName ? "\t" + displayNameCpu : "") +
                (ShowGpuName ? "\t" + displayNameGraphicCard : "") +
                (ShowRamName ? "\t" + displayNameRam : "") +
                (ShowComment ? "\t" + displayNameCustomComment : "") +
                (ShowMaxFPS ? "\t" + displayNameMaxFps : "") +
                (ShowP99FPS ? "\t" + displayNameNinetyNinePercentQuantileFps : "") +
                (ShowP95FS ? "\t" + displayNameNinetyFivePercentQuantileFps : "") +
                (ShowAverageFPS ? "\t" + displayNameAverageFps : "") +                
                (ShowMedianFPS ? "\t" + displayNameMedianFps : "") +
                (ShowP5FPS ? "\t" + displayNameFivePercentQuantileFps : "") +
                (ShowP1FPS ? "\t" + displayNameOnePercentQuantileFps : "") +
                (ShowP1LowAverageFPS ? "\t" + displayNameOnePercentLowAverageFps : "") +
                (ShowP1LowIntegralFPS ? "\t" + displayNameOnePercentLowIntegralFps : "") +
                (ShowP0Dot2FPS ? "\t" + displayNameZeroDotTwoPercentQuantileFps : "") +
                (ShowP0Dot1FPS ? "\t" + displayNameZeroDotOnePercentQuantileFps : "") +
                (ShowP0Dot1LowAverageFPS ? "\t" + displayNameZeroDotOnePercentLowAverageFps : "") +
                (ShowP0Dot1LowIntegralFPS ? "\t" + displayNameZeroDotOnePercentLowIntegralFps : "") +
                (ShowMinFPS ? "\t" + displayNameMinFps : "") +
                (ShowAdaptiveSTD ? "\t" + displayNameAdaptiveSTDFps : "") +
                (ShowCpuFpsPerWatt ? "\t" + displayNameCpuFpsPerWatt : "") +
                (ShowGpuFpsPerWatt ? "\t" + displayNameGpuFpsPerWatt : "") +
                (ShowAppLatency ? "\t" + displayNameAppLatency : "") +
                (ShowGpuActiveTimeAverage ? "\t" + displayNameGpuActiveTimeAverage : "") +
                (ShowGpuActiveTimeDeviation ? "\t" + displayNameGpuActiveTimeDeviation : "") +
                (ShowCpuMaxUsage ? "\t" + displayNameCpuUsage : "") +
                (ShowCpuPower ? "\t" + displayNameCpuPower : "") +
                (ShowCpuMaxClock ? "\t" + displayNameCpuClock : "") +
                (ShowCpuTemp ? "\t" + displayNameCpuTemp : "") +
                (ShowGpuUsage ? "\t" + displayNameGpuUsage : "") +
                (ShowGpuPower ? "\t" + displayNameGpuPower : "") +
                (ShowGpuTBPSim ? "\t" + displayNameGpuTBPSim : "") +
                (ShowGpuClock ? "\t" + displayNameGpuClock : "") +
                (ShowGpuTemp ? "\t" + displayNameGpuTemp : "") +
                Environment.NewLine);

            var cultureInfo = CultureInfo.CurrentCulture;

            foreach (var reportInfo in ReportInfoCollection)
            {
                builder.Append(reportInfo.Game +
                    (ShowCreationDate ? "\t" + reportInfo.Date?.ToString(cultureInfo) : "") +
                    (ShowCreationTime ? "\t" + reportInfo.Time?.ToString(cultureInfo) : "") +
                    (ShowNumberOfSamples ? "\t" + reportInfo.NumberOfSamples : "") +
                    (ShowRecordTime ? "\t" + reportInfo.RecordTime : "") +
                    (ShowCpuName ? "\t" + reportInfo.Cpu : "") +
                    (ShowGpuName ? "\t" + reportInfo.GraphicCard : "") +
                    (ShowRamName ? "\t" + reportInfo.Ram : "") +
                    (ShowComment ? "\t" + reportInfo.CustomComment : "") +
                    (ShowMaxFPS ? "\t" + reportInfo.MaxFps.ToString(cultureInfo) : "") +
                    (ShowP99FPS ? "\t" + reportInfo.NinetyNinePercentQuantileFps.ToString(cultureInfo) : "") +
                    (ShowP95FS ? "\t" + reportInfo.NinetyFivePercentQuantileFps.ToString(cultureInfo) : "") +
                    (ShowAverageFPS ? "\t" + reportInfo.AverageFps.ToString(cultureInfo) : "") +
                    (ShowMedianFPS ? "\t" + reportInfo.MedianFps.ToString(cultureInfo) : "") +
                    (ShowP5FPS ? "\t" + reportInfo.FivePercentQuantileFps.ToString(cultureInfo) : "") +
                    (ShowP1FPS ? "\t" + reportInfo.OnePercentQuantileFps.ToString(cultureInfo) : "") +
                    (ShowP1LowAverageFPS ? "\t" + reportInfo.OnePercentLowAverageFps.ToString(cultureInfo) : "") +
                    (ShowP1LowIntegralFPS ? "\t" + reportInfo.OnePercentLowIntegralFps.ToString(cultureInfo) : "") +
                    (ShowP0Dot2FPS ? "\t" + reportInfo.ZeroDotTwoPercentQuantileFps.ToString(cultureInfo) : "") +
                    (ShowP0Dot1FPS ? "\t" + reportInfo.ZeroDotOnePercentQuantileFps.ToString(cultureInfo) : "") +
                    (ShowP0Dot1LowAverageFPS ? "\t" + reportInfo.ZeroDotOnePercentLowAverageFps.ToString(cultureInfo) : "") +
                    (ShowP0Dot1LowIntegralFPS ? "\t" + reportInfo.ZeroDotOnePercentLowIntegralFps.ToString(cultureInfo) : "") +
                    (ShowMinFPS ? "\t" + reportInfo.MinFps.ToString(cultureInfo) : "") +
                    (ShowAdaptiveSTD ? "\t" + reportInfo.AdaptiveSTDFps.ToString(cultureInfo) : "") +
                    (ShowCpuFpsPerWatt ? "\t" + reportInfo.CpuFpsPerWatt.ToString(cultureInfo) : "") +
                    (ShowGpuFpsPerWatt ? "\t" + reportInfo.GpuFpsPerWatt.ToString(cultureInfo) : "") +
                    (ShowAppLatency ? "\t" + reportInfo.AppLatency.ToString(cultureInfo) : "") +
                    (ShowGpuActiveTimeAverage ? "\t" + reportInfo.GpuActiveTimeAverage.ToString(cultureInfo) : "") +
                    (ShowGpuActiveTimeDeviation ? "\t" + reportInfo.GpuActiveTimeDeviation.ToString(cultureInfo) : "") +
                    (ShowCpuMaxUsage ? "\t" + reportInfo.CpuMaxUsage.ToString(cultureInfo) : "") +
                    (ShowCpuPower ? "\t" + reportInfo.CpuPower.ToString(cultureInfo) : "") +
                    (ShowCpuMaxClock ? "\t" + reportInfo.CpuMaxClock.ToString(cultureInfo) : "") +
                    (ShowCpuTemp ? "\t" + reportInfo.CpuTemp.ToString(cultureInfo) : "") +
                    (ShowGpuUsage ? "\t" + reportInfo.GpuUsage.ToString(cultureInfo) : "") +
                    (ShowGpuPower ? "\t" + reportInfo.GpuPower.ToString(cultureInfo) : "") +
                    (ShowGpuTBPSim ? "\t" + reportInfo.GpuTBPSim.ToString(cultureInfo) : "") +
                    (ShowGpuClock ? "\t" + reportInfo.GpuClock.ToString(cultureInfo) : "") +
                    (ShowGpuTemp ? "\t" + reportInfo.GpuTemp.ToString(cultureInfo) : "") +
                    Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void SubscribeToSelectRecord()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.SelectSession>>()
                            .Subscribe(msg =>
                            {
                                if (_useEventMessages)
                                {
                                    ReportInfo reportInfo = GetReportInfoFromRecordInfo(msg.RecordInfo);
                                    AddReportRecord(reportInfo);
                                }
                            });
        }

        partial void InitializeReportParameters();


        private ReportInfo GetReportInfoFromRecordInfo(IFileRecordInfo recordInfo)
        {
            var session = _recordManager.LoadData(recordInfo.FullPath);

            double cpuPmdAverage = double.NaN;
            double gpuPmdAverage = double.NaN;

            var cpuPmdPowers = session.Runs.Where(r => !r.PmdCpuPower.IsNullOrEmpty());
            if (cpuPmdPowers != null && cpuPmdPowers.Any())
                cpuPmdAverage = Math.Round(cpuPmdPowers.SelectMany(x => x.PmdCpuPower).Average(), 1);

            var gpuPmdPowers = session.Runs.Where(r => !r.PmdGpuPower.IsNullOrEmpty());
            if (gpuPmdPowers != null && gpuPmdPowers.Any())
                gpuPmdAverage = Math.Round(gpuPmdPowers.SelectMany(x => x.PmdGpuPower).Average(), 1);

            double GeMetricValue(IList<double> sequence, EMetric metric) =>
                _frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);
            var frameTimes = session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToList();
            var displayTimes = session.Runs.SelectMany(r => r.CaptureData.MsBetweenDisplayChange).ToList();
            var samples = _appConfiguration.UseDisplayChangeMetrics ? displayTimes : frameTimes;

            var GpuActiveTimes = session.Runs.SelectMany(r => r.CaptureData.GpuActive).ToList();
            var recordTime = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last();
            var inputLagTimes = session.CalculateInputLagTimes(EInputLagType.Expected).Where(t => !double.IsNaN(t));

            var max = GeMetricValue(samples, EMetric.Max);
            var p99_quantile = GeMetricValue(samples, EMetric.P99);
            var p95_quantile = GeMetricValue(samples, EMetric.P95);
            var average = GeMetricValue(frameTimes, EMetric.Average);
            var GpuActiveTimeAverage = Math.Round(1000 / GeMetricValue(GpuActiveTimes, EMetric.GpuActiveAverage), 1);
            var median = GeMetricValue(samples, EMetric.Median);
            var p0dot1_quantile = GeMetricValue(samples, EMetric.P0dot1);
            var p0dot2_quantile = GeMetricValue(samples, EMetric.P0dot2);
            var p1_quantile = GeMetricValue(samples, EMetric.P1);
            var p5_quantile = GeMetricValue(samples, EMetric.P5);
            var p1_averageLowAverage = GeMetricValue(samples, EMetric.OnePercentLowAverage);
            var p0dot1_averageLowAverage = GeMetricValue(samples, EMetric.ZerodotOnePercentLowAverage);
            var p1_averageLowIntegral = GeMetricValue(samples, EMetric.OnePercentLowIntegral);
            var p0dot1_averageLowIntegral = GeMetricValue(samples, EMetric.ZerodotOnePercentLowIntegral);
            var min = GeMetricValue(samples, EMetric.Min);
            var adaptiveStandardDeviation = GeMetricValue(samples, EMetric.AdaptiveStd);

            var cpuFpsPerWatt = ReportUsePMDValues ? _frametimeStatisticProvider
                .GetPhysicalMetricValue(frameTimes, EMetric.CpuFpsPerWatt, cpuPmdAverage) :
                _frametimeStatisticProvider.GetPhysicalMetricValue(frameTimes, EMetric.CpuFpsPerWatt,
                SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower,
                0, double.PositiveInfinity));
            var gpuFpsPerWatt = ReportUsePMDValues ? _frametimeStatisticProvider
                .GetPhysicalMetricValue(frameTimes, EMetric.GpuFpsPerWatt, gpuPmdAverage) :
                _frametimeStatisticProvider.GetPhysicalMetricValue(frameTimes, EMetric.GpuFpsPerWatt,
                SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower,
                0, double.PositiveInfinity, _appConfiguration.UseTBPSim));

            var cpuUsage = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuMaxThreadUsage, 0, double.PositiveInfinity);
            var cpuPower = ReportUsePMDValues ? cpuPmdAverage : SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower, 0, double.PositiveInfinity);
            var cpuClock = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuMaxClock, 0, double.PositiveInfinity);
            var cpuTemp = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuTemp, 0, double.PositiveInfinity);
            var gpuUsage = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuUsage, 0, double.PositiveInfinity);
            var gpuPower = ReportUsePMDValues ? gpuPmdAverage : SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower, 0, double.PositiveInfinity);
            var gpuTBPSim = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuTBPSim, 0, double.PositiveInfinity);
            var gpuClock = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuClock, 0, double.PositiveInfinity);
            var gpuTemp = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuTemp, 0, double.PositiveInfinity);
            var appLatency = inputLagTimes.Any() ? Math.Round(inputLagTimes.Average(), 1) : 0;
            var gpuActiveTimeDeviation = GpuActiveTimes.Any() ? (Math.Round(Math.Abs(GpuActiveTimes.Average() - frameTimes.Average()) / frameTimes.Average() * 100, 1)) : double.NaN;

            var reportInfo = new ReportInfo()
            {
                Game = recordInfo.GameName,
                Date = recordInfo.CreationDate,
                Time = recordInfo.CreationTime,
                NumberOfSamples = frameTimes.Count,
                RecordTime = Math.Round(recordTime, 2),
                Cpu = recordInfo.ProcessorName == null ? "" : recordInfo.ProcessorName.Trim(new char[] { ' ', '"' }),
                GraphicCard = recordInfo.GraphicCardName == null ? "" : recordInfo.GraphicCardName.Trim(new char[] { ' ', '"' }),
                Ram = recordInfo.SystemRamInfo == null ? "" : recordInfo.SystemRamInfo.Trim(new char[] { ' ', '"' }),
                MaxFps = max,
                NinetyNinePercentQuantileFps = p99_quantile,
                NinetyFivePercentQuantileFps = p95_quantile,
                AverageFps = average,                
                MedianFps = median,
                FivePercentQuantileFps = p5_quantile,
                OnePercentQuantileFps = p1_quantile,
                OnePercentLowAverageFps = p1_averageLowAverage,
                OnePercentLowIntegralFps = p1_averageLowIntegral,
                ZeroDotTwoPercentQuantileFps = p0dot2_quantile,
                ZeroDotOnePercentQuantileFps = p0dot1_quantile,
                ZeroDotOnePercentLowAverageFps = p0dot1_averageLowAverage,
                ZeroDotOnePercentLowIntegralFps = p0dot1_averageLowIntegral,
                MinFps = min,
                AdaptiveSTDFps = adaptiveStandardDeviation,
                CpuFpsPerWatt = cpuFpsPerWatt,
                GpuFpsPerWatt = gpuFpsPerWatt,
                CpuMaxUsage = cpuUsage,
                CpuPower = cpuPower,
                CpuMaxClock = cpuClock,
                CpuTemp = cpuTemp,
                GpuUsage = gpuUsage,
                GpuPower = gpuPower,
                GpuTBPSim = gpuTBPSim,
                GpuClock = gpuClock,
                GpuTemp = gpuTemp,
                AppLatency = appLatency,
                GpuActiveTimeAverage = GpuActiveTimeAverage,
                GpuActiveTimeDeviation = gpuActiveTimeDeviation,
                CustomComment = recordInfo.Comment
            };

            return reportInfo;
        }

        private void AddAverageReportInfo(ObservableCollection<ReportInfo> reportInfoCollection)
        {
            var averageInfo = reportInfoCollection.FirstOrDefault(x => x.Game == "Averaged values");

            if (averageInfo != null)
            {
                reportInfoCollection.Remove(averageInfo);
            }

            if (reportInfoCollection.Count() > 1)
            {
                var propertyInfos = typeof(ReportInfo).GetProperties().Where(pi => pi.PropertyType == typeof(double));

                var report = new ReportInfo
                {
                    Game = "Averaged values"
                };

                foreach (var propertyInfo in propertyInfos)
                {

                    var average = reportInfoCollection.Select(x => propertyInfo.GetValue(x)).Select(x => Convert.ToDouble(x)).Average();
                    propertyInfo.SetValue(report, Math.Round(average, 1));
                }
                reportInfoCollection.Add(report);
            }
        }

        private void AddReportRecord(ReportInfo reportInfo)
        {
            ReportInfoCollection.Add(reportInfo);

            if (ReportShowAverageRow)
                AddAverageReportInfo(ReportInfoCollection);
        }

        public void OnGridSorting(object sender, DataGridSortingEventArgs e)
        {
            switch (e.Column.SortDirection)
            {
                case ListSortDirection.Ascending:
                    e.Column.SortDirection = ListSortDirection.Descending;
                    break;

                case ListSortDirection.Descending:
                    e.Column.SortDirection = ListSortDirection.Ascending;
                    break;

                default:
                    e.Column.SortDirection = ListSortDirection.Ascending;
                    break;
            }

            var column = e.Column.SortMemberPath;
            var propertyInfo = typeof(ReportInfo).GetProperty(column);
            ReportInfoCollection.Sort(c => propertyInfo.GetValue(c), e.Column.SortDirection);

            var averageRow = ReportInfoCollection.FirstOrDefault(x => x.Game == "Averaged values");
            if (averageRow != null)
                ReportInfoCollection.Move(ReportInfoCollection.IndexOf(averageRow), ReportInfoCollection.Count - 1);
        }

        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _useEventMessages = false;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _useEventMessages = true;
        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            if (dropInfo != null)
            {
                dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
                dropInfo.Effects = DragDropEffects.Move;
            }
        }

        async void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (dropInfo != null)
            {
                if (dropInfo.VisualTarget is FrameworkElement frameworkElement)
                {
                    if (frameworkElement.Name == "ReportDataGrid")
                    {
                        foreach (IFileRecordInfo recordInfo in await GetDroppedRecordInfosAsync(dropInfo.Data))
                        {
                            ReportInfo reportInfo = GetReportInfoFromRecordInfo(recordInfo);
                            AddReportRecord(reportInfo);
                        }
                    }
                }
            }
        }

        private async System.Threading.Tasks.Task<List<IFileRecordInfo>> GetDroppedRecordInfosAsync(object droppedData)
        {
            if (droppedData is IFileRecordInfo recordInfo)
            {
                return new List<IFileRecordInfo> { recordInfo };
            }

            if (droppedData is IEnumerable<IFileRecordInfo> recordInfos)
            {
                return recordInfos.Where(info => info != null).ToList();
            }

            if (droppedData is TreeViewItem treeViewItem)
            {
                if (treeViewItem.Tag is DirectoryInfo directoryInfo)
                {
                    return await GetRecordInfosByPathAsync(directoryInfo.FullName);
                }

                if (treeViewItem.Tag is FileInfo fileInfo)
                {
                    return await GetRecordInfosByPathAsync(fileInfo.FullName);
                }
            }

            if (droppedData is DirectoryInfo droppedDirectory)
            {
                return await GetRecordInfosByPathAsync(droppedDirectory.FullName);
            }

            if (droppedData is FileInfo droppedFile)
            {
                return await GetRecordInfosByPathAsync(droppedFile.FullName);
            }

            if (droppedData is string droppedPath)
            {
                return await GetRecordInfosByPathAsync(droppedPath);
            }

            if (droppedData is string[] droppedPaths)
            {
                return await GetRecordInfosByPathsAsync(droppedPaths);
            }

            if (droppedData is System.Windows.IDataObject dataObject && dataObject.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                if (dataObject.GetData(System.Windows.DataFormats.FileDrop) is string[] fileDropPaths)
                {
                    return await GetRecordInfosByPathsAsync(fileDropPaths);
                }
            }

            return new List<IFileRecordInfo>();
        }

        private async System.Threading.Tasks.Task<List<IFileRecordInfo>> GetRecordInfosByPathAsync(string path)
            => await GetRecordInfosByPathsAsync(new[] { path });

        private async System.Threading.Tasks.Task<List<IFileRecordInfo>> GetRecordInfosByPathsAsync(IEnumerable<string> paths)
        {
            if (paths == null)
            {
                return new List<IFileRecordInfo>();
            }

            var collectedPaths = new List<string>();
            foreach (string path in paths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        collectedPaths.AddRange(Directory.EnumerateFiles(path, "*.*", SearchOption.AllDirectories));
                    }
                    else if (File.Exists(path))
                    {
                        collectedPaths.Add(path);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while enumerating dropped path {path}", path);
                }
            }

            List<string> filePathList = collectedPaths
                .Where(filePath =>
                {
                    string extension = Path.GetExtension(filePath);
                    return extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
                        || extension.Equals(".csv", StringComparison.OrdinalIgnoreCase);
                })
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (filePathList.Count == 0)
            {
                return new List<IFileRecordInfo>();
            }

            int maxParallelism = Math.Min(Environment.ProcessorCount, 8);
            var semaphore = new System.Threading.SemaphoreSlim(maxParallelism, maxParallelism);

            var loadingTasks = filePathList.Select(async filePath =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await _recordManager.GetFileRecordInfo(new FileInfo(filePath));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing dropped path {path}", filePath);
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToList();

            try
            {
                IFileRecordInfo[] results = await System.Threading.Tasks.Task.WhenAll(loadingTasks);
                return results.Where(info => info != null).ToList();
            }
            finally
            {
                semaphore.Dispose();
            }
        }
    }
}
