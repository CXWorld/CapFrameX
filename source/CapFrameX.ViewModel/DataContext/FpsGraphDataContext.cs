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
using CapFrameX.Statistics.NetStandard;
using System.Windows.Forms;
using CapFrameX.Statistics.PlotBuilder;
using CapFrameX.Statistics.PlotBuilder.Contracts;

namespace CapFrameX.ViewModel.DataContext
{
	public class FpsGraphDataContext : GraphDataContextBase
	{
		public PlotModel FpsModel { get => PlotModel; }

		public ICommand CopyFpsValuesCommand { get; }
		public ICommand CopyFpsPointsCommand { get; }

		private readonly FpsGraphPlotBuilder _graphPlotBuilder;

		public FpsGraphDataContext(IRecordDataServer recordDataServer,
			IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider) :
			base(appConfiguration, recordDataServer, frametimesStatisticProvider)
		{
			CopyFpsValuesCommand = new DelegateCommand(OnCopyFpsValues);
			CopyFpsPointsCommand = new DelegateCommand(OnCopyFpsPoints);
			_graphPlotBuilder = new FpsGraphPlotBuilder(appConfiguration, frametimesStatisticProvider);
		}


		public void BuildPlotmodel(IPlotSettings plotSettings, Action<PlotModel> onFinishAction = null)
		{
			Dispatcher.CurrentDispatcher.Invoke(() => {
				_graphPlotBuilder.BuildPlotmodel(RecordDataServer.CurrentSession, plotSettings, 
					RecordDataServer.CurrentTime, RecordDataServer.CurrentTime + RecordDataServer.WindowLength, 
					RecordDataServer.RemoveOutlierMethod, RecordDataServer.FilterMode, onFinishAction);
				PlotModel = _graphPlotBuilder.PlotModel;
			});
		}

		public void Reset()
		{
			Dispatcher.CurrentDispatcher.Invoke(() => {
				_graphPlotBuilder.Reset();
				PlotModel = _graphPlotBuilder.PlotModel;
			});
		}

		public void UpdateAxis(EPlotAxis axis, Action<Axis> action)
		{
			_graphPlotBuilder.UpdateAxis(axis, action);
		}

		private void OnCopyFpsValues()
		{
			if (RecordSession == null)
				return;

			var fps = RecordDataServer.GetFpsTimeWindow();
			StringBuilder builder = new StringBuilder();

			foreach (var framerate in fps)
			{
				builder.Append(framerate.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
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
				builder.Append(fpsPoints[i].X.ToString(CultureInfo.InvariantCulture) + "\t" +
					fpsPoints[i].Y.ToString(CultureInfo.InvariantCulture) + Environment.NewLine);
			}

			Clipboard.SetDataObject(builder.ToString(), false);
		}
	}
}
