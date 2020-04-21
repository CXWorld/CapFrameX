﻿using CapFrameX.Contracts.Configuration;
using CapFrameX.Data;
using CapFrameX.Extensions;
using CapFrameX.Statistics;
using CapFrameX.Statistics.NetStandard;
using CapFrameX.Statistics.PlotBuilder;
using CapFrameX.Statistics.PlotBuilder.Contracts;
using OxyPlot;
using OxyPlot.Axes;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;

namespace CapFrameX.ViewModel.DataContext
{
	public class FrametimeGraphDataContext : GraphDataContextBase
	{
		public ICommand CopyFrametimeValuesCommand { get; }

		public ICommand CopyFrametimePointsCommand { get; }

		public PlotModel FrametimeModel { get => PlotModel; }

		private readonly FrametimePlotBuilder _frametimePlotBuilder;

		public FrametimeGraphDataContext(IRecordDataServer recordDataServer, IAppConfiguration appConfiguration, IStatisticProvider frametimesStatisticProvider) :
			base(appConfiguration, recordDataServer, frametimesStatisticProvider)
		{
			CopyFrametimeValuesCommand = new DelegateCommand(OnCopyFrametimeValues);
			CopyFrametimePointsCommand = new DelegateCommand(OnCopyFrametimePoints);
			_frametimePlotBuilder = new FrametimePlotBuilder(appConfiguration, frametimesStatisticProvider);
		}

		public void BuildPlotmodel(IPlotSettings plotSettings, Action<PlotModel> onFinishAction = null)
		{
			Dispatcher.CurrentDispatcher.Invoke(() => {
				_frametimePlotBuilder.BuildPlotmodel(RecordDataServer.CurrentSession, plotSettings, RecordDataServer.CurrentTime, RecordDataServer.CurrentTime + RecordDataServer.WindowLength, RecordDataServer.RemoveOutlierMethod, onFinishAction);
				PlotModel = _frametimePlotBuilder.PlotModel;
			});
		}

		public void Reset()
		{
			Dispatcher.CurrentDispatcher.Invoke(() => {
				_frametimePlotBuilder.Reset();
				PlotModel = _frametimePlotBuilder.PlotModel;
			});
		}

		public void UpdateAxis(EPlotAxis axis, Action<Axis> action) {
			_frametimePlotBuilder.UpdateAxis(axis, action);
		}

		private void OnCopyFrametimeValues()
		{
			if (RecordSession == null)
				return;

			var frametimes = RecordDataServer.GetFrametimeTimeWindow();
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

			var frametimePoints = RecordDataServer.GetFrametimePointTimeWindow();
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
