using CapFrameX.Contracts.Configuration;
using OxyPlot;
using OxyPlot.Axes;
using Prism.Commands;
using System;
using System.Text;
using System.Windows.Input;
using System.Globalization;
using CapFrameX.Data;
using System.Windows.Threading;
using System.Windows.Forms;
using CapFrameX.Statistics.PlotBuilder;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using Prism.Events;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;

namespace CapFrameX.ViewModel.DataContext
{
    public class FrametimeDistributionGraphDataContext : GraphDataContextBase
    {
        public PlotModel FrametimeDistributionModel => PlotModel;

        public ICommand CopyDistributionPointsCommand { get; }

        public ICommand SavePlotAsSVG { get; }
        public ICommand SavePlotAsPNG { get; }


        private readonly FrametimeDistributionPlotBuilder _frametimeDistributionPlotBuilder;

        public FrametimeDistributionGraphDataContext(IRecordDataServer recordDataServer,
                                   IAppConfiguration appConfiguration,
                                   IStatisticProvider frametimesStatisticProvider,
                                   IEventAggregator eventAggregator) :
            base(appConfiguration, recordDataServer, frametimesStatisticProvider, eventAggregator)
        {

            CopyDistributionPointsCommand = new DelegateCommand(OnCopyDistributionPoints);

            SavePlotAsSVG = new DelegateCommand(() => OnSavePlotAsImage("fps", "svg"));
            SavePlotAsPNG = new DelegateCommand(() => OnSavePlotAsImage("fps", "png"));
            _frametimeDistributionPlotBuilder = new FrametimeDistributionPlotBuilder(appConfiguration, frametimesStatisticProvider);
        }

        public void BuildPlotmodel(IPlotSettings plotSettings, Action<PlotModel> onFinishAction = null)
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                _frametimeDistributionPlotBuilder.BuildPlotmodel(RecordDataServer.CurrentSession, plotSettings,
                    RecordDataServer.CurrentTime, RecordDataServer.CurrentTime + RecordDataServer.WindowLength,
                    RecordDataServer.RemoveOutlierMethod, onFinishAction);

                var plotModel = _frametimeDistributionPlotBuilder.PlotModel;
                plotModel.TextColor = AppConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black;
                plotModel.PlotAreaBorderColor = AppConfiguration.UseDarkMode ? OxyColor.FromArgb(100, 204, 204, 204) : OxyColor.FromArgb(50, 30, 30, 30);

                var xAxis = plotModel.GetAxisOrDefault(EPlotAxis.XAXISFRAMETIMES.GetDescription(), null);
                if (xAxis != null)
                    xAxis.MajorGridlineColor = AppConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30);

                var yAxis = plotModel.GetAxisOrDefault(EPlotAxis.YAXISDISTRIBUTION.GetDescription(), null);
                if (yAxis != null)
                    yAxis.MajorGridlineColor = AppConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30);

                PlotModel = plotModel;
            });

        }

        public void Reset()
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                _frametimeDistributionPlotBuilder.Reset();
                var plotModel = _frametimeDistributionPlotBuilder.PlotModel;
                plotModel.TextColor = AppConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black;
                PlotModel = plotModel;
            });
        }

        public void UpdateAxis(EPlotAxis axis, Action<Axis> action)
        {
            _frametimeDistributionPlotBuilder.UpdateAxis(axis, action);
        }


        private void OnCopyDistributionPoints()
        {
            if (RecordSession == null)
                return;

            var distributionPoints = RecordDataServer.GetDistributionPointTimeWindow();
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < distributionPoints.Count; i++)
            {
                builder.Append(Math.Round(distributionPoints[i].X, 2).ToString(CultureInfo.InvariantCulture) + "\t" +
                    Math.Round(distributionPoints[i].Y, 2).ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }
    }
}
