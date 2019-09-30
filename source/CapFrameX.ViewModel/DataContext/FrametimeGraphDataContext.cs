using CapFrameX.Contracts.Configuration;
using CapFrameX.OcatInterface;
using CapFrameX.Statistics;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace CapFrameX.ViewModel.DataContext
{
    public class FrametimeGraphDataContext : GraphDataContextBase
    {
        private PlotModel _frametimeModel;

        public PlotModel FrametimeModel
        {
            get { return _frametimeModel; }
            set
            {
                _frametimeModel = value;
                RaisePropertyChanged();
            }
        }

        public ICommand CopyFrametimeValuesCommand { get; }

        public ICommand CopyFrametimePointsCommand { get; }

        public FrametimeGraphDataContext(IRecordDataServer recordDataServer, IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider) :
            base(recordDataServer, appConfiguration, frametimesStatisticProvider)
        {
            CopyFrametimeValuesCommand = new DelegateCommand(OnCopyFrametimeValues);
            CopyFrametimePointsCommand = new DelegateCommand(OnCopyFrametimePoints);

            // Update Chart after changing index slider
            RecordDataServer.FrametimeDataStream.Subscribe(sequence =>
            {
                SetFrametimeChart(sequence);
                GraphNumberSamples = sequence.Count;
            });

            FrametimeModel = new PlotModel
            {
                PlotMargins = new OxyThickness(40, 0, 0, 40),
                PlotAreaBorderColor = OxyColor.FromArgb(64, 204, 204, 204),
                LegendPosition = LegendPosition.TopCenter,
                LegendOrientation = LegendOrientation.Horizontal
            };

            //Axes
            //X
            FrametimeModel.Axes.Add(new LinearAxis()
            {
                Key = "xAxis",
                Position = AxisPosition.Bottom,
                Title = "Samples",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
                MinorTickSize = 0,
                MajorTickSize = 0
            });

            //Y
            FrametimeModel.Axes.Add(new LinearAxis()
            {
                Key = "yAxis",
                Position = AxisPosition.Left,
                Title = "Frametime [ms]",
                MajorGridlineStyle = LineStyle.Solid,
                MajorGridlineThickness = 1,
                MajorGridlineColor = OxyColor.FromArgb(64, 204, 204, 204),
                MinorTickSize = 0,
                MajorTickSize = 0
            });
        }

        public void SetFrametimeChart(IList<double> frametimes)
        {
            if (frametimes == null || !frametimes.Any())
                return;

            int count = frametimes.Count;
            var frameTimeDataPoints = frametimes.Select((x, i) => new DataPoint(i, x));
            var yMin = frametimes.Min();
            var yMax = frametimes.Max();
            var movingAverage = FrametimesStatisticProvider.GetMovingAverage(frametimes, AppConfiguration.MovingAverageWindowSize);

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                FrametimeModel.Series.Clear();
                var frametimeSeries = new LineSeries
                {
                    Title = "Frametimes",
                    StrokeThickness = 1,
                    LegendStrokeThickness = 4,
                    Color = ColorRessource.FrametimeStroke
                };
                var movingAverageSeries = new LineSeries
                {
                    Title = string.Format(CultureInfo.InvariantCulture,
                    "Moving average (window size = {0})", AppConfiguration.MovingAverageWindowSize),
                    StrokeThickness = 2,
                    LegendStrokeThickness = 4,
                    Color = ColorRessource.FrametimeMovingAverageStroke
                };

                frametimeSeries.Points.AddRange(frameTimeDataPoints);
                movingAverageSeries.Points.AddRange(movingAverage.Select((x, i) => new DataPoint(i, x)));

                var xAxis = FrametimeModel.GetAxisOrDefault("xAxis", null);
                var yAxis = FrametimeModel.GetAxisOrDefault("yAxis", null);

                xAxis.Minimum = 0;
                xAxis.Maximum = count;
                yAxis.Minimum = yMin - (yMax - yMin) / 6;
                yAxis.Maximum = yMax + (yMax - yMin) / 6;

                FrametimeModel.Series.Add(frametimeSeries);
                FrametimeModel.Series.Add(movingAverageSeries);

                FrametimeModel.InvalidatePlot(true);
            }));
        }

        private void OnCopyFrametimeValues()
        {
            if (RecordSession == null)
                return;

            RecordDataServer.RemoveOutlierMethod
                = UseRemovingOutlier ? ERemoveOutlierMethod.DeciPercentile : ERemoveOutlierMethod.None;
            var frametimes = RecordDataServer.GetFrametimeSampleWindow();
            StringBuilder builder = new StringBuilder();

            foreach (var frametime in frametimes)
            {
                builder.Append(frametime.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }

        private void OnCopyFrametimePoints()
        {
            if (RecordSession == null)
                return;

            RecordDataServer.RemoveOutlierMethod
                = UseRemovingOutlier ? ERemoveOutlierMethod.DeciPercentile : ERemoveOutlierMethod.None;
            var frametimePoints = RecordDataServer.GetFrametimePointSampleWindow();
            StringBuilder builder = new StringBuilder();

            for (int i = 0; i < frametimePoints.Count; i++)
            {
                builder.Append(frametimePoints[i].X.ToString(CultureInfo.InvariantCulture) + "\t" +
                    frametimePoints[i].Y.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
            }

            Clipboard.SetDataObject(builder.ToString(), false);
        }
    }
}
