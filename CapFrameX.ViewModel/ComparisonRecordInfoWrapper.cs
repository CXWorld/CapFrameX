using CapFrameX.OcatInterface;
using LiveCharts;
using LiveCharts.Geared;
using Prism.Mvvm;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
	public class ComparisonRecordInfoWrapper : BindableBase
	{
		readonly SeriesCollection _frametimesCollection;
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
			SeriesCollection frametimesCollection, SeriesCollection lShapesCollection)
		{
			WrappedRecordInfo = info;

			_frametimesCollection = frametimesCollection;
			_lShapesCollection = lShapesCollection;
		}

		private void OnColorChanged()
		{
			if (FrametimeGraphColor.HasValue && CollectionIndex >= 0 &&
				_frametimesCollection.Count > CollectionIndex &&
				_lShapesCollection.Count > CollectionIndex)
			{
				Color color = FrametimeGraphColor.Value;

				var frametimesChart = _frametimesCollection[CollectionIndex] as GLineSeries;
				var lShapeChart = _lShapesCollection[CollectionIndex] as GLineSeries;

				var solidColorBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
				frametimesChart.Stroke = solidColorBrush;
				lShapeChart.Stroke = solidColorBrush;
				Color = solidColorBrush;
			}
		}
	}
}
