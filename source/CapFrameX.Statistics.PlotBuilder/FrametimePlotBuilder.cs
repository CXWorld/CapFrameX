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
	public class FrametimePlotBuilder : PlotBuilder
	{
		public FrametimePlotBuilder(IFrametimeStatisticProviderOptions options, IStatisticProvider frametimeStatisticProvider) : base(options, frametimeStatisticProvider) { }

		public void BuildPlotmodel(ISession session, IPlotSettings plotSettings, double startTime, double endTime, ERemoveOutlierMethod eRemoveOutlinerMethod, Action<PlotModel> onFinishAction = null)
		{
			var plotModel = PlotModel;
			Reset();
			if (session == null)
			{
				return;
			}

			plotModel.Axes.Add(AxisDefinitions[EPlotAxis.XAXIS]);
			plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISFRAMETIMES]);

			SetFrametimeChart(plotModel, session.GetFrametimePointsTimeWindow(startTime, endTime, _frametimeStatisticProviderOptions, eRemoveOutlinerMethod));

			if (plotSettings.IsAnyGraphVisible && session.HasValidSensorData())
			{
				plotModel.Axes.Add(AxisDefinitions[EPlotAxis.YAXISPERCENTAGE]);

				if (plotSettings.ShowGpuLoad)
					SetGPULoadChart(plotModel, session.GetGPULoadPointTimeWindow());
				if (plotSettings.ShowCpuLoad)
					SetCPULoadChart(plotModel, session.GetCPULoadPointTimeWindow());
				if (plotSettings.ShowCpuMaxThreadLoad)
					SetCPUMaxThreadLoadChart(plotModel, session.GetCPUMaxThreadLoadPointTimeWindow());
				if (plotSettings.ShowGpuPowerLimit)
					SetGpuPowerLimitChart(plotModel, session.GetGpuPowerLimitPointTimeWindow());
			}
			onFinishAction?.Invoke(plotModel);
			plotModel.InvalidatePlot(true);
		}

		private void SetFrametimeChart(PlotModel plotModel, IList<Point> frametimePoints)
		{
			if (frametimePoints == null || !frametimePoints.Any())
				return;

			int count = frametimePoints.Count;
			var frametimeDataPoints = frametimePoints.Select(pnt => new DataPoint(pnt.X, pnt.Y));
			var yMin = frametimePoints.Min(pnt => pnt.Y);
			var yMax = frametimePoints.Max(pnt => pnt.Y);
			var movingAverage = _frametimesStatisticProvider.GetMovingAverage(frametimePoints.Select(pnt => pnt.Y).ToList());

			plotModel.Series.Clear();

			var frametimeSeries = new LineSeries
			{
				Title = "Frametimes",
				StrokeThickness = 1,
				LegendStrokeThickness = 4,
				Color = Constants.FrametimeStroke
			};
			var movingAverageSeries = new LineSeries
			{
				Title = "Moving average",
				StrokeThickness = 2,
				LegendStrokeThickness = 4,
				Color = Constants.FrametimeMovingAverageStroke
			};

			frametimeSeries.Points.AddRange(frametimeDataPoints);
			movingAverageSeries.Points.AddRange(movingAverage.Select((y, i) => new DataPoint(frametimePoints[i].X, y)));

			UpdateAxis(EPlotAxis.XAXIS, (axis) =>
			{
				axis.Minimum = frametimePoints.First().X;
				axis.Maximum = frametimePoints.Last().X;
			});
			//var yAxis = FrametimeModel.GetAxisOrDefault("yAxis", null);


			//yAxis.Minimum = yMin - (yMax - yMin) / 6;
			//yAxis.Maximum = yMax + (yMax - yMin) / 6;

			plotModel.Series.Add(frametimeSeries);
			plotModel.Series.Add(movingAverageSeries);

			plotModel.InvalidatePlot(true);
		}
	}
}
