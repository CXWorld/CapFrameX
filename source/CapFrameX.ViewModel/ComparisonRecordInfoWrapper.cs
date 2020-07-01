using CapFrameX.Contracts.MVVM;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using LiveCharts.Wpf;
using OxyPlot;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public class ComparisonRecordInfoWrapper : BindableBase, IMouseEventHandler
	{
		private PubSubEvent<ViewMessages.SetFileRecordInfoExternal> _setFileRecordInfoExternalEvent;

		private Color? _frametimeGraphColor;
		private SolidColorBrush _color;
		private ComparisonViewModel _viewModel;
		private bool _isHideModeSelected;

		public Color? FrametimeGraphColor
		{
			get { return _frametimeGraphColor; }
			set
			{
				bool onChanged = _frametimeGraphColor != null;
				_frametimeGraphColor = value;
				RaisePropertyChanged();
				if (onChanged)
					OnColorChanged();
			}
		}

		public SolidColorBrush Color
		{
			get { return _color; }
			set
			{
				_color = value;
				RaisePropertyChanged();
			}
		}

		private bool _myBool;

		public bool MyBool
		{
			get { return _myBool; }
			set
			{
				_myBool = value;
				RaisePropertyChanged();
			}
		}

		public bool IsHideModeSelected
		{
			get { return _isHideModeSelected; }
			set
			{
				_isHideModeSelected = value;
				RaisePropertyChanged();
				OnHideModeChanged();
			}
		}

		public ComparisonRecordInfo WrappedRecordInfo { get; }

		public ICommand RemoveCommand { get; }

		public ICommand MouseDownCommand { get; }

		public ComparisonRecordInfoWrapper(ComparisonRecordInfo info, ComparisonViewModel viewModel)
		{
			WrappedRecordInfo = info;
			_viewModel = viewModel;

			_setFileRecordInfoExternalEvent =
				viewModel.EventAggregator.GetEvent<PubSubEvent<ViewMessages.SetFileRecordInfoExternal>>();

			RemoveCommand = new DelegateCommand(OnRemove);
			MouseDownCommand = new DelegateCommand(OnMouseDown);
		}

		private void OnMouseDown()
			=> _setFileRecordInfoExternalEvent
				.Publish(new ViewMessages
				.SetFileRecordInfoExternal(WrappedRecordInfo.FileRecordInfo));

		private void OnRemove()
		{
			if (!_viewModel.ComparisonRecords.Any())
				return;

			_viewModel.RemoveComparisonItem(this);
		}

		private void OnColorChanged()
		{
			if (FrametimeGraphColor.HasValue && _viewModel.ComparisonRecords.Any()
				&& _viewModel.ComparisonFrametimesModel.Series.Any() && _viewModel.ComparisonLShapeCollection.Any())
			{
				Color color = FrametimeGraphColor.Value;

				var tag = WrappedRecordInfo.FileRecordInfo.Id;
				var frametimesChart = _viewModel.ComparisonFrametimesModel
					.Series.FirstOrDefault(chart => chart.Tag == tag) as OxyPlot.Series.LineSeries;
				var lShapeChart = _viewModel.ComparisonLShapeCollection
					.FirstOrDefault(chart => chart.Id == tag) as LineSeries;

				if (frametimesChart == null || lShapeChart == null)
					return;

				var solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
				frametimesChart.Color = OxyColor.FromArgb(color.A, color.R, color.G, color.B);
				lShapeChart.Stroke = solidColorBrush;

				_viewModel.ComparisonColorManager.FreeColor(Color);				
				Color = solidColorBrush;
				_viewModel.ComparisonColorManager.LockColorOnChange(Color);

				_viewModel.ComparisonFrametimesModel.InvalidatePlot(true);
			}
		}

		private void OnHideModeChanged()
		{
			if (FrametimeGraphColor.HasValue && _viewModel.ComparisonRecords.Any()
				&& _viewModel.ComparisonFrametimesModel.Series.Any() && _viewModel.ComparisonLShapeCollection.Any())
			{
				var tag = WrappedRecordInfo.FileRecordInfo.Id;
				var frametimesChart = _viewModel.ComparisonFrametimesModel
					.Series.FirstOrDefault(chart => chart.Tag == tag) as OxyPlot.Series.LineSeries;

				var fpsChart = _viewModel.ComparisonFpsModel
					.Series.FirstOrDefault(chart => chart.Tag == tag) as OxyPlot.Series.LineSeries;

				var lShapeChart = _viewModel.ComparisonLShapeCollection
					.FirstOrDefault(chart => chart.Id == tag) as LineSeries;

				if (frametimesChart == null || lShapeChart == null)
					return;

				if (IsHideModeSelected)
				{
					frametimesChart.Color = OxyColors.Transparent;
					fpsChart.Color = OxyColors.Transparent;
					lShapeChart.Stroke = Brushes.Transparent;
					lShapeChart.PointForeground = Brushes.Transparent;
					frametimesChart.Title = string.Empty;
					fpsChart.Title = string.Empty;
				}
				else
				{
					Color color = FrametimeGraphColor.Value;
					var solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
					frametimesChart.Color = OxyColor.FromArgb(color.A, color.R, color.G, color.B);
					fpsChart.Color = OxyColor.FromArgb(color.A, color.R, color.G, color.B);
					lShapeChart.Stroke = solidColorBrush;
					lShapeChart.PointForeground = solidColorBrush;
					frametimesChart.Title = _viewModel.GetChartLabel(WrappedRecordInfo).Context;
					fpsChart.Title = _viewModel.GetChartLabel(WrappedRecordInfo).Context;
				}

				_viewModel.ComparisonFrametimesModel.InvalidatePlot(true);
				_viewModel.ComparisonFpsModel.InvalidatePlot(true);
			}
		}

		public ComparisonRecordInfoWrapper Clone()
		{
			return new ComparisonRecordInfoWrapper(WrappedRecordInfo, _viewModel)
			{
				Color = Color,
				FrametimeGraphColor = FrametimeGraphColor,
			};
		}

		void IMouseEventHandler.OnMouseEnter()
		{
			if (!_viewModel.ComparisonRecords.Any())
				return;

			if (_viewModel.ComparisonFrametimesModel.Series.Any())
			{
				var tag = WrappedRecordInfo.FileRecordInfo.Id;
				var frametimesChart = _viewModel.ComparisonFrametimesModel
					.Series.FirstOrDefault(chart => chart.Tag == tag) as OxyPlot.Series.LineSeries;
				var fpsChart = _viewModel.ComparisonFpsModel
					.Series.FirstOrDefault(chart => chart.Tag == tag) as OxyPlot.Series.LineSeries;

				if (frametimesChart == null
					|| fpsChart == null)
					return;

				frametimesChart.StrokeThickness = 2;
				fpsChart.StrokeThickness = 2;

				int indexFrametimes = _viewModel.ComparisonFrametimesModel.Series.IndexOf(frametimesChart);
				int indexFps = _viewModel.ComparisonFpsModel.Series.IndexOf(fpsChart);

				//Move to end
				_viewModel.ComparisonFrametimesModel.Series.Move(indexFrametimes, _viewModel.ComparisonFrametimesModel.Series.Count - 1);
				_viewModel.ComparisonFpsModel.Series.Move(indexFps, _viewModel.ComparisonFpsModel.Series.Count - 1);

				// Update plot
				_viewModel.ComparisonFrametimesModel.InvalidatePlot(true);
				_viewModel.ComparisonFpsModel.InvalidatePlot(true);
			}

			if (_viewModel.ComparisonRowChartSeriesCollection.Any())
			{
				// highlight bar chart chartpoint
				var series = _viewModel.ComparisonRowChartSeriesCollection;
				var index = _viewModel.ComparisonRecords.IndexOf(this);

				foreach (var item in series)
				{
					var rowSeries = item as RowSeries;
					rowSeries.HighlightChartPoint(_viewModel.ComparisonRecords.Count - index - 1);
				}
			}
		}

		void IMouseEventHandler.OnMouseLeave()
		{
			if (!_viewModel.ComparisonRecords.Any())
				return;

			var index = _viewModel.ComparisonRecords.IndexOf(this);

			if (_viewModel.ComparisonFrametimesModel.Series.Any())
			{
				var tag = WrappedRecordInfo.FileRecordInfo.Id;
				var frametimesChart = _viewModel.ComparisonFrametimesModel
					.Series.FirstOrDefault(chart => chart.Tag == tag) as OxyPlot.Series.LineSeries;

				var fpsChart = _viewModel.ComparisonFpsModel
					.Series.FirstOrDefault(chart => chart.Tag == tag) as OxyPlot.Series.LineSeries;

				if (frametimesChart == null
					|| fpsChart == null)
					return;

				frametimesChart.StrokeThickness = 1;
				fpsChart.StrokeThickness = 1;

				// Update plot
				_viewModel.ComparisonFrametimesModel.InvalidatePlot(true);
				_viewModel.ComparisonFpsModel.InvalidatePlot(true);
			}

			if (_viewModel.ComparisonRowChartSeriesCollection.Any())
			{
				// unhighlight bar chart chartpoint
				var series = _viewModel.ComparisonRowChartSeriesCollection;

				foreach (var item in series)
				{
					var rowSeries = item as RowSeries;
					rowSeries.UnHighlightChartPoint(_viewModel.ComparisonRecords.Count - index - 1);
				}
			}
		}
	}
}
