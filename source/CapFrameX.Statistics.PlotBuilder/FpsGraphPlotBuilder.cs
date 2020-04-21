using CapFrameX.Data.Session.Contracts;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.NetStandard.Contracts;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CapFrameX.Statistics.PlotBuilder
{
	public class FpsGraphPlotBuilder : PlotBuilder
	{
		public FpsGraphPlotBuilder(IFrametimeStatisticProviderOptions options, IStatisticProvider frametimeStatisticProvider) : base(options, frametimeStatisticProvider) { }
		public void BuildPlotmodel(ISession session, IPlotSettings plotSettings, double startTime, double endTime, ERemoveOutlierMethod eRemoveOutlinerMethod, Action<PlotModel> onFinishAction = null)
		{
			var plotModel = PlotModel;
			Reset();
			if(session == null)
			{
				return;
			}
			plotModel.Axes.Add(AxisDefinitions[EPlotAxis.XAXIS]);
			plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISFPS]);

			SetFpsChart(plotModel, session.GetFpsPointTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod), session.GetFrametimeTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod));

			if (plotSettings.IsAnyGraphVisible && session.HasValidSensorData())
			{
				plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISPERCENTAGE]);

				if (plotSettings.ShowGpuLoad)
					SetGPULoadChart(plotModel, session.GetGPULoadPointTimeWindow());
				if (plotSettings.ShowCpuLoad)
					SetCPULoadChart(plotModel, session.GetCPULoadPointTimeWindow());
				if (plotSettings.ShowCpuMaxThreadLoad)
					SetCPUMaxThreadLoadChart(plotModel, session.GetCPUMaxThreadLoadPointTimeWindow());
			}

			onFinishAction?.Invoke(plotModel);
			plotModel.InvalidatePlot(true);
		}

		public void SetFpsChart(PlotModel plotModel, IList<Point> fpsPoints, IList<double> frametimes)
		{
			if (fpsPoints == null || !fpsPoints.Any())
				return;

			int count = fpsPoints.Count;
			var fpsDataPoints = fpsPoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
			var yMin = fpsPoints.Min(pnt => pnt.Y);
			var yMax = fpsPoints.Max(pnt => pnt.Y);
			double average = frametimes.Count * 1000 / frametimes.Sum();
			var averageDataPoints = fpsPoints.Select(pnt => new DataPoint(pnt.X, average));

			plotModel.Series.Clear();

			var fpsSeries = new LineSeries
			{
				Title = "FPS",
				StrokeThickness = 1,
				Color = Constants.FpsStroke
			};

			var averageSeries = new LineSeries
			{
				Title = "Average FPS",
				StrokeThickness = 2,
				Color = Constants.FpsAverageStroke
			};

			fpsSeries.Points.AddRange(fpsDataPoints);
			averageSeries.Points.AddRange(averageDataPoints);


			UpdateAxis(EPlotAxis.XAXIS, (axis) =>
			{
				axis.Minimum = fpsPoints.First().X;
				axis.Maximum = fpsPoints.Last().X;
			});

			UpdateAxis(EPlotAxis.YAXISFPS, (axis) =>
			{
				axis.Minimum = yMin - (yMax - yMin) / 6;
				axis.Maximum = yMax + (yMax - yMin) / 6;
			});



			plotModel.Series.Add(fpsSeries);
			plotModel.Series.Add(averageSeries);

			plotModel.InvalidatePlot(true);
		}
	}
}
