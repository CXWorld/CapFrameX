using CapFrameX.Contracts.MVVM;
using CapFrameX.OcatInterface;
using LiveCharts.Wpf;
using OxyPlot;
using Prism.Commands;
using Prism.Mvvm;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public class ComparisonRecordInfoWrapper : BindableBase, IMouseEventHandler
	{
		private Color? _frametimeGraphColor;
		private SolidColorBrush _color;
		private ComparisonViewModel _viewModel;

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


		public ComparisonRecordInfo WrappedRecordInfo { get; }

		public ICommand RemoveCommand { get; }

		public ComparisonRecordInfoWrapper(ComparisonRecordInfo info, ComparisonViewModel viewModel)
		{
			WrappedRecordInfo = info;
			_viewModel = viewModel;

			RemoveCommand = new DelegateCommand(OnRemove);
		}

		private void OnRemove()
		{
			if (!_viewModel.ComparisonModel.Series.Any())
				return;

			_viewModel.RemoveComparisonItem(this);
		}

		private void OnColorChanged()
		{
			if (FrametimeGraphColor.HasValue && _viewModel.ComparisonRecords.Any()
				&& _viewModel.ComparisonModel.Series.Any() && _viewModel.ComparisonLShapeCollection.Any())
			{
				Color color = FrametimeGraphColor.Value;
				var index = _viewModel.ComparisonRecords.IndexOf(this);

				if (index < _viewModel.ComparisonModel.Series.Count && 
					index < _viewModel.ComparisonLShapeCollection.Count)
				{
					var frametimesChart = _viewModel.ComparisonModel.Series[index] as OxyPlot.Series.LineSeries;
					var lShapeChart = _viewModel.ComparisonLShapeCollection[index] as LineSeries;

					var solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
					frametimesChart.Color = OxyColor.FromArgb(color.A, color.R, color.G, color.B);
					lShapeChart.Stroke = solidColorBrush;
					Color = solidColorBrush;

					_viewModel.ComparisonModel.InvalidatePlot(true);
				}
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
			if (!_viewModel.ComparisonModel.Series.Any())
				return;

			var index = _viewModel.ComparisonRecords.IndexOf(this);

			var frametimesChart = _viewModel.ComparisonModel.Series[index] as OxyPlot.Series.LineSeries;
			frametimesChart.StrokeThickness = 2;
			_viewModel.ComparisonModel.InvalidatePlot(true);

			// highlight bar chart chartpoint
			var series = _viewModel.ComparisonRowChartSeriesCollection;

			foreach (var item in series)
			{
				var rowSeries = item as RowSeries;
				rowSeries.HighlightChartPoint(_viewModel.ComparisonRecords.Count - index - 1);
			}
		}

		void IMouseEventHandler.OnMouseLeave()
		{
			if (!_viewModel.ComparisonModel.Series.Any())
				return;

			var index = _viewModel.ComparisonRecords.IndexOf(this);

			var frametimesChart = _viewModel.ComparisonModel.Series[index] as OxyPlot.Series.LineSeries;
			frametimesChart.StrokeThickness = 1;
			_viewModel.ComparisonModel.InvalidatePlot(true);

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
