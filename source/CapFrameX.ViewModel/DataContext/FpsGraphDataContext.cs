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
using Prism.Events;

namespace CapFrameX.ViewModel.DataContext
{
	public class FpsGraphDataContext : GraphDataContextBase
	{
		public PlotModel FpsModel { get => PlotModel; }

		public ICommand CopyFpsValuesCommand { get; }
		public ICommand CopyFpsPointsCommand { get; }
		public ICommand SavePlotAsImage { get; }

		private readonly FpsGraphPlotBuilder _fpsPlotBuilder;

		public FpsGraphDataContext(IRecordDataServer recordDataServer,
								   IAppConfiguration appConfiguration, 
								   IStatisticProvider frametimesStatisticProvider, 
								   IEventAggregator eventAggregator) :
			base(appConfiguration, recordDataServer, frametimesStatisticProvider, eventAggregator)
		{
			CopyFpsValuesCommand = new DelegateCommand(OnCopyFpsValues);
			CopyFpsPointsCommand = new DelegateCommand(OnCopyFpsPoints);
			SavePlotAsImage = new DelegateCommand(() => OnSavePlotAsImage("fps"));
			_fpsPlotBuilder = new FpsGraphPlotBuilder(appConfiguration, frametimesStatisticProvider);
		}


		public void BuildPlotmodel(IPlotSettings plotSettings, Action<PlotModel> onFinishAction = null)
		{
			Dispatcher.CurrentDispatcher.Invoke(() => {
				_fpsPlotBuilder.BuildPlotmodel(RecordDataServer.CurrentSession, plotSettings, 
					RecordDataServer.CurrentTime, RecordDataServer.CurrentTime + RecordDataServer.WindowLength, 
					RecordDataServer.RemoveOutlierMethod, RecordDataServer.FilterMode, onFinishAction);

				var plotModel = _fpsPlotBuilder.PlotModel;
				plotModel.TextColor = AppConfiguration.UseDarkMode ? OxyColors.White : OxyColors.Black;
				PlotModel = plotModel;
			});
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
	}
}
