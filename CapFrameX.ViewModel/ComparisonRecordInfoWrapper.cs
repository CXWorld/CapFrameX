using CapFrameX.OcatInterface;
using LiveCharts;
using LiveCharts.Geared;
using OxyPlot;
using OxyPlot.Series;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public class ComparisonRecordInfoWrapper : BindableBase
	{
		readonly PlotModel _frametimesModel;
		readonly SeriesCollection _lShapesCollection;

		private Color? _frametimeGraphColor;
		private SolidColorBrush _color;

		public Color? FrametimeGraphColor
		{
			get { return _frametimeGraphColor; }
			set
			{
				_frametimeGraphColor = value;
				RaisePropertyChanged();
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

		public ComparisonRecordInfo WrappedRecordInfo { get; }

		public int CollectionIndex { get; set; }

		public ICommand MouseEnterCommand { get; }

		public ICommand MouseLeaveCommand { get; }

		public ComparisonRecordInfoWrapper(ComparisonRecordInfo info,
			PlotModel frametimesModel, SeriesCollection lShapesCollection)
		{
			WrappedRecordInfo = info;

			_frametimesModel = frametimesModel;
			_lShapesCollection = lShapesCollection;

			MouseEnterCommand = new DelegateCommand(OnMouseEnter);
			MouseLeaveCommand = new DelegateCommand(OnMouseLeave);
		}

		private void OnMouseEnter()
		{
			if (!_frametimesModel.Series.Any())
				return;

			var frametimesChart = _frametimesModel.Series[CollectionIndex] as LineSeries;
			frametimesChart.StrokeThickness = 2;
			_frametimesModel.InvalidatePlot(true);
		}

		private void OnMouseLeave()
		{
			if (!_frametimesModel.Series.Any())
				return;

			var frametimesChart = _frametimesModel.Series[CollectionIndex] as LineSeries;
			frametimesChart.StrokeThickness = 1;
			_frametimesModel.InvalidatePlot(true);
		}

		private void OnColorChanged()
		{
			if (FrametimeGraphColor.HasValue && CollectionIndex >= 0 &&
				_frametimesModel.Series.Count > CollectionIndex &&
				_lShapesCollection.Count > CollectionIndex)
			{
				Color color = FrametimeGraphColor.Value;

				var frametimesChart = _frametimesModel.Series[CollectionIndex] as LineSeries;
				var lShapeChart = _lShapesCollection[CollectionIndex] as GLineSeries;

				var solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
				frametimesChart.Color = OxyColor.FromArgb(color.A, color.R, color.G, color.B);
				lShapeChart.Stroke = solidColorBrush;
				Color = solidColorBrush;

				_frametimesModel.InvalidatePlot(true);
			}
		}
	}
}
