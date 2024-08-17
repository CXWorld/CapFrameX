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
    public class FpsGraphDataContext : GraphDataContextBase
    {
        private bool _showGpuActiveCommands;

        public PlotModel FpsModel => PlotModel;

        public bool ShowGpuActiveCommands
        {
            get { return _showGpuActiveCommands; }
            set
            {
                _showGpuActiveCommands = value;
                RaisePropertyChanged();
            }
        }

        public ICommand CopyFpsValuesCommand { get; }
        public ICommand CopyFpsPointsCommand { get; }
        public ICommand CopyGpuActiveFpsValuesCommand { get; }
        public ICommand CopyGpuActiveFpsPointsCommand { get; }
        public ICommand SavePlotAsSVG { get; }
        public ICommand SavePlotAsPNG { get; }


        private readonly FpsGraphPlotBuilder _fpsPlotBuilder;

        public FpsGraphDataContext(IRecordDataServer recordDataServer,
                                   IAppConfiguration appConfiguration,
                                   IStatisticProvider frametimesStatisticProvider,
                                   IEventAggregator eventAggregator) :
            base(appConfiguration, recordDataServer, frametimesStatisticProvider, eventAggregator)
        {
            CopyFpsValuesCommand = new DelegateCommand(OnCopyFpsValues);
            CopyFpsPointsCommand = new DelegateCommand(OnCopyFpsPoints);

            CopyGpuActiveFpsValuesCommand = new DelegateCommand(OnCopyGpuActiveFpsValues);
            CopyGpuActiveFpsPointsCommand = new DelegateCommand(OnCopyGpuActiveFpsPoints);

            SavePlotAsSVG = new DelegateCommand(() => OnSavePlotAsImage("fps", "svg"));
            SavePlotAsPNG = new DelegateCommand(() => OnSavePlotAsImage("fps", "png"));
            _fpsPlotBuilder = new FpsGraphPlotBuilder(appConfiguration, frametimesStatisticProvider);
        }

        public void BuildPlotmodel(IPlotSettings plotSettings, Action<PlotModel> onFinishAction = null)
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                _fpsPlotBuilder.BuildPlotmodel(RecordDataServer.CurrentSession, plotSettings,
                    RecordDataServer.CurrentTime, RecordDataServer.CurrentTime + RecordDataServer.WindowLength,
                    RecordDataServer.RemoveOutlierMethod, RecordDataServer.FilterMode, onFinishAction);

                var plotModel = _fpsPlotBuilder.PlotModel;
                plotModel.TextColor = AppConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black;
                plotModel.PlotAreaBorderColor = AppConfiguration.UseDarkMode ? OxyColor.FromArgb(100, 204, 204, 204) : OxyColor.FromArgb(50, 30, 30, 30);

                var xAxis = plotModel.GetAxisOrDefault(EPlotAxis.XAXIS.GetDescription(), null);
                if (xAxis != null)
                    xAxis.MajorGridlineColor = AppConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30);

                var yAxis = plotModel.GetAxisOrDefault(EPlotAxis.YAXISFPS.GetDescription(), null);
                if (yAxis != null)
                    yAxis.MajorGridlineColor = AppConfiguration.UseDarkMode ? OxyColor.FromArgb(40, 204, 204, 204) : OxyColor.FromArgb(20, 30, 30, 30);

                PlotModel = plotModel;
            });

            ShowGpuActiveCommands = plotSettings.ShowGpuActiveCharts;
        }

        public void Reset()
        {
            Dispatcher.CurrentDispatcher.Invoke(() =>
            {
                _fpsPlotBuilder.Reset();
                var plotModel = _fpsPlotBuilder.PlotModel;
                plotModel.TextColor = AppConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black;
                PlotModel = plotModel;
            });
        }

        public void UpdateAxis(EPlotAxis axis, Action<Axis> action)
        {
            _fpsPlotBuilder.UpdateAxis(axis, action);
        }

        private void OnCopyFpsValues()
        {
            if (RecordSession == null)
                return;

            var fps = RecordDataServer.GetFpsTimeWindow();
            StringBuilder builder = new StringBuilder();

            foreach (var framerate in fps)
            {
                builder.Append(Math.Round(framerate, 2).ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopyGpuActiveFpsValues()
        {
            if (RecordSession == null)
                return;

            var fps = RecordDataServer.GetFpsTimeWindow();
            var gpuActiveFps = RecordDataServer.GetGpuActiveFpsTimeWindow();
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < fps.Count; i++)
            {
                builder.Append(Math.Round(fps[i], 2).ToString(CultureInfo.InvariantCulture) + "\t" +
                    Math.Round(gpuActiveFps[i], 2).ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopyFpsPoints()
        {
            if (RecordSession == null)
                return;

            var fpsPoints = RecordDataServer.GetFpsPointTimeWindow();
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < fpsPoints.Count; i++)
            {
                builder.Append(Math.Round(fpsPoints[i].X, 2).ToString(CultureInfo.InvariantCulture) + "\t" +
                    Math.Round(fpsPoints[i].Y, 2).ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopyGpuActiveFpsPoints()
        {
            if (RecordSession == null)
                return;

            var fpsPoints = RecordDataServer.GetFpsPointTimeWindow();
            var gpuActiveFpsPoints = RecordDataServer.GetGpuActiveFpsPointTimeWindow();
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < fpsPoints.Count; i++)
            {
                builder.Append(Math.Round(fpsPoints[i].X, 2).ToString(CultureInfo.InvariantCulture) + "\t" +
                    Math.Round(fpsPoints[i].Y, 2).ToString(CultureInfo.InvariantCulture) + "\t"+
                    Math.Round(gpuActiveFpsPoints[i].Y, 2).ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }
    }
}
