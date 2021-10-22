using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using CapFrameX.Data;
using GongSolutions.Wpf.DragDrop;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Regions;
using System.Collections.Specialized;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Sensor.Reporting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Windows.Controls;
using System.ComponentModel;
using CapFrameX.Statistics.NetStandard;

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
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();

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

            stopwatch.Stop();
            _logger.LogInformation(this.GetType().Name + " {initializationTime}s initialization time",
                Math.Round(stopwatch.ElapsedMilliseconds * 1E-03, 1));
        }


        public void RemoveReportEntry(ReportInfo selectedItem)
        {
            if (selectedItem.Game.Equals("Averaged values"))
                return;
            else
            {
                ReportInfoCollection.Remove(selectedItem);

                if(ReportShowAverageRow)
                    AddAverageReportInfo(ReportInfoCollection);
            }
        }

        private void OnCopyTableData()
        {
            if (!ReportInfoCollection.Any())
                return;
            
            StringBuilder builder = new StringBuilder();

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
            var displayNameZeroDotTwoPercentQuantileFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.ZeroDotTwoPercentQuantileFps);
            var displayNameZeroDotOnePercentQuantileFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.ZeroDotOnePercentQuantileFps);
            var displayNameZeroDotOnePercentLowAverageFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.ZeroDotOnePercentLowAverageFps);
            var displayNameMinFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.MinFps);
            var displayNameAdaptiveSTDFps = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.AdaptiveSTDFps);
            var displayNameCpuFpsPerWatt = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CpuFpsPerWatt);
            var displayNameGpuFpsPerWatt = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuFpsPerWatt);
            var displayNameAppLatency = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.AppLatency);
            var displayNameCpuUsage = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CpuMaxUsage);
            var displayNameCpuPower = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CpuPower);
            var displayNameCpuClock = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CpuMaxClock);
            var displayNameCpuTemp = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CpuTemp);
            var displayNameGpuUsage = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuUsage);
            var displayNameGpuPower = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuPower);
            var displayNameGpuClock = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuClock);
            var displayNameGpuTemp = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.GpuTemp);
            var displayNameCustomComment = ReflectionExtensions.GetPropertyDisplayName<ReportInfo>(x => x.CustomComment);

            builder.Append(displayNameGame + 
                (ShowCreationDate ? "\t" + displayNameDate : "") + 
                (ShowCreationTime ? "\t" + displayNameTime : "") + 
                (ShowNumberOfSamples ?  "\t" + displayNameNumberOfSamples : "" )+ 
                (ShowRecordTime ?  "\t" + displayNameRecordTime : "") + 
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
                (ShowP1LowFPS ? "\t" + displayNameOnePercentLowAverageFps : "") + 
                (ShowP0Dot2FPS ? "\t" + displayNameZeroDotTwoPercentQuantileFps : "") + 
                (ShowP0Dot1FPS ? "\t" + displayNameZeroDotOnePercentQuantileFps : "") + 
                (ShowP0Dot1LowFPS ? "\t" + displayNameZeroDotOnePercentLowAverageFps : "") + 
                (ShowMinFPS ? "\t" + displayNameMinFps : "") + 
                (ShowAdaptiveSTD ? "\t" + displayNameAdaptiveSTDFps : "") +
                (ShowCpuFpsPerWatt ? "\t" + displayNameCpuFpsPerWatt : "") +
                (ShowGpuFpsPerWatt ? "\t" + displayNameGpuFpsPerWatt : "") + 
                (ShowAppLatency ? "\t" + displayNameAppLatency : "") + 
                (ShowCpuMaxUsage ? "\t" + displayNameCpuUsage : "") + 
                (ShowCpuPower ? "\t" + displayNameCpuPower : "") + 
                (ShowCpuMaxClock ? "\t" + displayNameCpuClock : "") + 
                (ShowCpuTemp ? "\t" + displayNameCpuTemp : "") + 
                (ShowGpuUsage ? "\t" + displayNameGpuUsage : "") + 
                (ShowGpuPower ? "\t" + displayNameGpuPower : "") + 
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
                    (ShowRecordTime ? "\t" + reportInfo.RecordTime?.ToString(cultureInfo) : "") +
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
                    (ShowP1LowFPS ? "\t" + reportInfo.OnePercentLowAverageFps.ToString(cultureInfo) : "") +
                    (ShowP0Dot2FPS ? "\t" + reportInfo.ZeroDotTwoPercentQuantileFps.ToString(cultureInfo) : "") +
                    (ShowP0Dot1FPS ? "\t" + reportInfo.ZeroDotOnePercentQuantileFps.ToString(cultureInfo) : "") +
                    (ShowP0Dot1LowFPS ? "\t" + reportInfo.ZeroDotOnePercentLowAverageFps.ToString(cultureInfo) : "") +
                    (ShowMinFPS ? "\t" + reportInfo.MinFps.ToString(cultureInfo) : "") +
                    (ShowAdaptiveSTD ? "\t" + reportInfo.AdaptiveSTDFps.ToString(cultureInfo) : "") +
                    (ShowCpuFpsPerWatt ? "\t" + reportInfo.CpuFpsPerWatt.ToString(cultureInfo) : "") +
                    (ShowGpuFpsPerWatt ? "\t" + reportInfo.GpuFpsPerWatt.ToString(cultureInfo) : "") +
                    (ShowAppLatency ? "\t" + reportInfo.AppLatency.ToString(cultureInfo) : "") +
                    (ShowCpuMaxUsage ? "\t" + reportInfo.CpuMaxUsage.ToString(cultureInfo) : "") +
                    (ShowCpuPower ? "\t" + reportInfo.CpuPower.ToString(cultureInfo) : "") +
                    (ShowCpuMaxClock ? "\t" + reportInfo.CpuMaxClock.ToString(cultureInfo) : "") +
                    (ShowCpuTemp ? "\t" + reportInfo.CpuTemp.ToString(cultureInfo) : "") +
                    (ShowGpuUsage ? "\t" + reportInfo.GpuUsage.ToString(cultureInfo) : "") +
                    (ShowGpuPower ? "\t" + reportInfo.GpuPower.ToString(cultureInfo) : "") +
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

            double GeMetricValue(IList<double> sequence, EMetric metric) =>
                    _frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);
            var frameTimes = session.Runs.SelectMany(r => r.CaptureData.MsBetweenPresents).ToList();
            var recordTime = session.Runs.SelectMany(r => r.CaptureData.TimeInSeconds).Last();
            var inputLagTimes = session.CalculateInputLagTimes(EInputLagType.Expected).Where(t => !double.IsNaN(t));

            var max = GeMetricValue(frameTimes, EMetric.Max);
            var p99_quantile = GeMetricValue(frameTimes, EMetric.P99);
            var p95_quantile = GeMetricValue(frameTimes, EMetric.P95);
            var average = GeMetricValue(frameTimes, EMetric.Average);
            var median = GeMetricValue(frameTimes, EMetric.Median);
            var p0dot1_quantile = GeMetricValue(frameTimes, EMetric.P0dot1);
            var p0dot2_quantile = GeMetricValue(frameTimes, EMetric.P0dot2);
            var p1_quantile = GeMetricValue(frameTimes, EMetric.P1);
            var p5_quantile = GeMetricValue(frameTimes, EMetric.P5);
            var p1_averageLow = GeMetricValue(frameTimes, EMetric.OnePercentLow);
            var p0dot1_averageLow = GeMetricValue(frameTimes, EMetric.ZerodotOnePercentLow);
            var min = GeMetricValue(frameTimes, EMetric.Min);
            var adaptiveStandardDeviation = GeMetricValue(frameTimes, EMetric.AdaptiveStd);
            var cpuFpsPerWatt = _frametimeStatisticProvider
                .GetPhysicalMetricValue(frameTimes, EMetric.CpuFpsPerWatt,
                SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower,
                0, double.PositiveInfinity));
            var gpuFpsPerWatt = _frametimeStatisticProvider
                .GetPhysicalMetricValue(frameTimes, EMetric.GpuFpsPerWatt,
                SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower,
                0, double.PositiveInfinity));
            var cpuUsage = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuMaxThreadUsage, 0, double.PositiveInfinity);
            var cpuPower = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuPower, 0, double.PositiveInfinity);
            var cpuClock = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuMaxClock, 0, double.PositiveInfinity);
            var cpuTemp = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.CpuTemp, 0, double.PositiveInfinity);
            var gpuUsage = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuUsage, 0, double.PositiveInfinity);
            var gpuPower = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuPower, 0, double.PositiveInfinity);
            var gpuClock = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuClock, 0, double.PositiveInfinity);
            var gpuTemp = SensorReport.GetAverageSensorValues(session.Runs.Select(run => run.SensorData2), EReportSensorName.GpuTemp, 0, double.PositiveInfinity);
            var appLatency = inputLagTimes.Any() ? Math.Round(inputLagTimes.Average(), 1) : 0;

            var reportInfo = new ReportInfo()
            {
                Game = recordInfo.GameName,
                Date = recordInfo.CreationDate,
                Time = recordInfo.CreationTime,
                NumberOfSamples = frameTimes.Count.ToString(),
                RecordTime = Math.Round(recordTime, 2).ToString(),
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
                OnePercentLowAverageFps = p1_averageLow,
                ZeroDotTwoPercentQuantileFps = p0dot2_quantile,
                ZeroDotOnePercentQuantileFps = p0dot1_quantile,
                ZeroDotOnePercentLowAverageFps = p0dot1_averageLow,
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
                GpuClock = gpuClock,
                GpuTemp = gpuTemp,
                AppLatency = appLatency,
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

                var report = new ReportInfo();

                report.Game = "Averaged values";
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

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            if (dropInfo != null)
            {
                if (dropInfo.VisualTarget is FrameworkElement frameworkElement)
                {
                    if (frameworkElement.Name == "ReportDataGrid")
                    {
                        if (dropInfo.Data is IFileRecordInfo recordInfo)
                        {
                            ReportInfo reportInfo = GetReportInfoFromRecordInfo(recordInfo);
                            AddReportRecord(reportInfo);
                        }
                        else if (dropInfo.Data is IEnumerable<IFileRecordInfo> recordInfos)
                        {
                            foreach (var item in recordInfos)
                            {
                                ReportInfo reportInfo = GetReportInfoFromRecordInfo(item);
                                AddReportRecord(reportInfo);
                            }
                        }
                    }
                }
            }
        }
    }
}
