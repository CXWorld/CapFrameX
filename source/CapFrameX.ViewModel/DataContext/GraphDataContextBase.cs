using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CapFrameX.ViewModel.DataContext
{
    public class GraphDataContextBase : BindableBase
    {
        public const int SCALE_RESOLUTION = 200;

        protected readonly IStatisticProvider _frametimesStatisticProvider;
        protected readonly IEventAggregator _eventAggregator;

        protected PlotModel PlotModel { get; set; }

        protected IAppConfiguration AppConfiguration { get; }

        protected IRecordDataServer RecordDataServer { get; }

        public GraphDataContextBase(IAppConfiguration appConfiguration,
            IRecordDataServer recordDataServer,
            IStatisticProvider frametimesStatisticProvider,
            IEventAggregator eventAggregator)
        {
            AppConfiguration = appConfiguration;
            RecordDataServer = recordDataServer;
            _frametimesStatisticProvider = frametimesStatisticProvider;
            _eventAggregator = eventAggregator;

            SubscribeToAggregatorEvents();
        }

        public ISession RecordSession
        {
            get => RecordDataServer.CurrentSession;
            set
            {
                RecordDataServer.CurrentSession = value;
            }
        }

        protected void OnSavePlotAsImage(string plotType, string fileFormat)
        {
            var filename = string.Join("-", new string[] {
                    string.IsNullOrWhiteSpace(RecordSession.Info.GameName) ? RecordSession.Info.ProcessName: RecordSession.Info.GameName,
                    RecordSession.Info.Processor,
                    RecordSession.Info.GPU,
                    RecordSession.Info.SystemRam,
                    RecordSession.Info.Comment,
                    plotType
                }.Where(filenamePart => !string.IsNullOrWhiteSpace(filenamePart)));
            if (fileFormat == "svg")
            {
                ImageExport.SavePlotAsSVG(PlotModel, filename, AppConfiguration.HorizontalGraphExportRes, AppConfiguration.VerticalGraphExportRes);
            }
            else if (fileFormat == "png")
            {
                ImageExport.SavePlotAsPNG(PlotModel, filename, AppConfiguration.HorizontalGraphExportRes, AppConfiguration.VerticalGraphExportRes, AppConfiguration.UseDarkMode);
            }
        }

        private void SubscribeToAggregatorEvents()
        {
            _eventAggregator.GetEvent<PubSubEvent<ViewMessages.ThemeChanged>>()
                .Subscribe(msg =>
                {
                    try
                    {
                        if (PlotModel is not null)
                        {
                            PlotModel.TextColor = AppConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black;
                            PlotModel.InvalidatePlot(false);
                        }
                    }
                    catch { }
                });
        }

        // Both series originate from the same TimeInSeconds source, but validity
        // filtering can drop different samples from each, so pair by timestamp
        // intersection instead of by index.
        protected static IList<Tuple<Point, Point>> GetAlignedFinitePoints(
            IList<Point> fpsPoints, IList<Point> gpuActiveFpsPoints)
        {
            var alignedPoints = new List<Tuple<Point, Point>>();
            if (fpsPoints == null || gpuActiveFpsPoints == null)
                return alignedPoints;

            int fpsIndex = 0;
            int gpuIndex = 0;
            while (fpsIndex < fpsPoints.Count && gpuIndex < gpuActiveFpsPoints.Count)
            {
                Point fpsPoint = fpsPoints[fpsIndex];
                Point gpuPoint = gpuActiveFpsPoints[gpuIndex];
                if (fpsPoint.X < gpuPoint.X)
                {
                    fpsIndex++;
                    continue;
                }
                if (gpuPoint.X < fpsPoint.X)
                {
                    gpuIndex++;
                    continue;
                }

                if (IsFinite(fpsPoint.Y) && IsFinite(gpuPoint.Y))
                    alignedPoints.Add(Tuple.Create(fpsPoint, gpuPoint));
                fpsIndex++;
                gpuIndex++;
            }

            return alignedPoints;
        }

        private static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }

    public class VisibleGraphs : IPlotSettings
    {
        public bool ShowGpuLoad { get; private set; }
        public bool ShowCpuLoad { get; private set; }
        public bool ShowCpuMaxThreadLoad { get; private set; }
        public bool ShowGpuPowerLimit { get; private set; }
        public bool ShowPcLatency { get; private set; }
        public bool ShowAnimationError { get; private set; }
        public bool ShowAggregationSeparators { get; private set; }
        public bool ShowThresholds { get; private set; }
        public double StutteringFactor { get; private set; }
        public double LowFPSThreshold { get; private set; }
        public bool ShowGpuActiveCharts { get; private set; }
        public bool ShowCpuActiveCharts { get; private set; }
        public bool ShowDisplayTimes { get; private set; }


        public bool IsAnyPercentageGraphVisible => ShowGpuLoad || ShowCpuLoad || ShowCpuMaxThreadLoad || ShowGpuPowerLimit;

        public VisibleGraphs(bool gpuLoad, bool cpuLoad, bool cpuMaxThreadLoad, bool gpuPowerLimit, bool pcLatency, bool animationError,
            bool aggregationSeparators, bool showThresholds, double stutteringFactor, double lowFPSThreshold, bool gpuActiveCharts, bool cpuActiveCharts, bool showDisplayTimes)
        {
            ShowGpuLoad = gpuLoad;
            ShowCpuLoad = cpuLoad;
            ShowCpuMaxThreadLoad = cpuMaxThreadLoad;
            ShowGpuPowerLimit = gpuPowerLimit;
            ShowPcLatency = pcLatency;
            ShowAnimationError = animationError;
            ShowAggregationSeparators = aggregationSeparators;
            ShowThresholds = showThresholds;
            StutteringFactor = stutteringFactor;
            LowFPSThreshold = lowFPSThreshold;
            ShowGpuActiveCharts = gpuActiveCharts;
            ShowCpuActiveCharts = cpuActiveCharts;
            ShowDisplayTimes = showDisplayTimes;
        }
    }
}
