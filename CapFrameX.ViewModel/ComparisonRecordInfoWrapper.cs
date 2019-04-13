using CapFrameX.OcatInterface;
using LiveCharts;
using LiveCharts.Geared;
using OxyPlot;
using OxyPlot.Series;
using Prism.Mvvm;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public class ComparisonRecordInfoWrapper : BindableBase
	{
		readonly PlotModel _frametimesmodel;
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

		public ComparisonRecordInfoWrapper(ComparisonRecordInfo info,
			PlotModel frametimesmodel, SeriesCollection lShapesCollection)
		{
			WrappedRecordInfo = info;

			_frametimesmodel = frametimesmodel;
			_lShapesCollection = lShapesCollection;
		}

		private void OnColorChanged()
		{
			if (FrametimeGraphColor.HasValue && CollectionIndex >= 0 &&
				_frametimesmodel.Series.Count > CollectionIndex &&
				_lShapesCollection.Count > CollectionIndex)
			{
				Color color = FrametimeGraphColor.Value;

				var frametimesChart = _frametimesmodel.Series[CollectionIndex] as LineSeries;
				var lShapeChart = _lShapesCollection[CollectionIndex] as GLineSeries;

				var solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
				frametimesChart.Color = OxyColor.FromArgb(color.A, color.R, color.G, color.B);
				lShapeChart.Stroke = solidColorBrush;
				Color = solidColorBrush;
			}
		}
	}
}
