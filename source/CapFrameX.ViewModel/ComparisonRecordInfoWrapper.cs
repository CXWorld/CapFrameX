using CapFrameX.Contracts.MVVM;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using LiveCharts.Wpf;
using OxyPlot;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
// Aliased instead of imported: the namespace also holds a LineSeries, which would collide with
// the LiveCharts one used for the L-shapes below.
using SeriesHighlightAnnotation = CapFrameX.Statistics.PlotBuilder.SeriesHighlightAnnotation;

namespace CapFrameX.ViewModel
{
    public class ComparisonRecordInfoWrapper : BindableBase, IMouseEventHandler
    {
        private const double HIGHLIGHT_STROKE_DELTA = 2;

        private PubSubEvent<ViewMessages.SetFileRecordInfoExternal> _setFileRecordInfoExternalEvent;

        private Color? _frametimeGraphColor;
        private SolidColorBrush _color = Brushes.Transparent;
        private ComparisonViewModel _viewModel;
        private bool _isHideModeSelected;
        private bool _isHighlighted;

        public Color? FrametimeGraphColor
        {
            get { return _frametimeGraphColor; }
            set
            {
                Color? previousColor = _frametimeGraphColor;
                _frametimeGraphColor = value;
                _color = CreateBrush(value);
                RaisePropertyChanged();
                RaisePropertyChanged(nameof(Color));

                if (previousColor.HasValue)
                    OnColorChanged(previousColor.Value);
            }
        }

        /// <summary>
        /// Brush view of <see cref="FrametimeGraphColor"/>, used by the L-shape series while the
        /// OxyPlot series use the color itself. It is derived and not stored separately: a second
        /// stored copy only stays in sync while every chart it is written to happens to exist.
        /// </summary>
        public SolidColorBrush Color
        {
            get { return _color; }
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

        public ComparisonRecordInfoWrapper Clone()
        {
            return new ComparisonRecordInfoWrapper(WrappedRecordInfo, _viewModel)
            {
                FrametimeGraphColor = FrametimeGraphColor,
            };
        }


        private void OnHideModeChanged()
        {
            UpdateChartsColor(hideMode: IsHideModeSelected);
        }

        private void OnColorChanged(Color previousColor)
        {
            // The palette bookkeeping must not depend on which charts happen to be built. The
            // color picker is shown on the distribution tab too, where no L-shape series exist -
            // doing this only when they do leaves the released color marked as used and hands the
            // same color out a second time for the next record.
            _viewModel.ComparisonColorManager.FreeColor(CreateBrush(previousColor));

            if (_frametimeGraphColor.HasValue)
                _viewModel.ComparisonColorManager.LockColorOnChange(Color);

            UpdateChartsColor(hideMode: IsHideModeSelected);
        }

        void IMouseEventHandler.OnMouseEnter()
        {
            UpdateMouseInteraction(isEntering: true);
        }

        void IMouseEventHandler.OnMouseLeave()
        {
            UpdateMouseInteraction(isEntering: false);
        }

        private static SolidColorBrush CreateBrush(Color? color)
        {
            if (!color.HasValue)
                return Brushes.Transparent;

            var brush = new SolidColorBrush(color.Value);
            brush.Freeze();
            return brush;
        }

        private void UpdateChartsColor(bool hideMode)
        {
            if (!_frametimeGraphColor.HasValue || !_viewModel.ComparisonRecords.Any())
                return;

            _viewModel.SetChartUpdateFlags();

            var color = _frametimeGraphColor.Value;
            var tag = WrappedRecordInfo.FileRecordInfo.Id;
            var oxyColor = hideMode
                ? OxyColors.Transparent : OxyColor.FromArgb(color.A, color.R, color.G, color.B);
            var solidBrush = hideMode ? Brushes.Transparent : Color;
            var chartTitle = hideMode
                ? string.Empty : _viewModel.GetChartLabel(WrappedRecordInfo).Context;

            // Every chart is updated on its own: they are built lazily per tab, so requiring all
            // of them to exist would silently skip the ones that do.
            var frametimesChart = _viewModel.ComparisonFrametimesModel.Series
                .FirstOrDefault(chart => (string)chart.Tag == tag) as OxyPlot.Series.LineSeries;

            if (frametimesChart != null)
            {
                frametimesChart.Color = oxyColor;
                frametimesChart.Title = chartTitle;
                _viewModel.ComparisonFrametimesModel.InvalidatePlot(true);
            }

            var fpsChart = _viewModel.ComparisonFpsModel.Series
                .FirstOrDefault(chart => (string)chart.Tag == tag) as OxyPlot.Series.LineSeries;

            if (fpsChart != null)
            {
                fpsChart.Color = oxyColor;
                fpsChart.Title = chartTitle;
                _viewModel.ComparisonFpsModel.InvalidatePlot(true);
            }

            var lShapeChart = _viewModel.ComparisonLShapeCollection
                .FirstOrDefault(chart => chart.Id == tag) as LineSeries;

            if (lShapeChart != null)
            {
                lShapeChart.Stroke = solidBrush;
                lShapeChart.PointForeground = solidBrush;
            }

            var distributionChart = _viewModel.ComparisonDistributionModel.Series
                .FirstOrDefault(chart => (string)chart.Tag == tag) as OxyPlot.Series.LineSeries;

            if (distributionChart != null)
            {
                distributionChart.Color = oxyColor;
                distributionChart.Title = chartTitle;
                _viewModel.ComparisonDistributionModel.InvalidatePlot(true);
            }
        }


        private void UpdateMouseInteraction(bool isEntering)
        {
            // Enter and leave can arrive unbalanced: the item containers are regenerated while the
            // mouse sits on them (sorting) and the charts are rebuilt underneath. Tracking the
            // state here and writing absolute thicknesses keeps a stray leave from thinning a
            // freshly built series below zero, which stops OxyPlot from drawing it at all.
            if (_isHighlighted == isEntering)
                return;

            _isHighlighted = isEntering;

            if (!_viewModel.ComparisonRecords.Any())
                return;

            // The series are brought to the front through an overlay annotation instead of being
            // moved inside the series collection: that collection also drives the order of the
            // legend entries, which must stay aligned with the record list.
            var tag = WrappedRecordInfo.FileRecordInfo.Id;
            var index = _viewModel.ComparisonRecords.IndexOf(this);
            double delta = isEntering ? HIGHLIGHT_STROKE_DELTA : 0;

            // Frametimes + FPS
            if (_viewModel.ComparisonFrametimesModel.Series.Any())
            {
                var frametimesChart = _viewModel.ComparisonFrametimesModel.Series
                    .FirstOrDefault(chart => (string)chart.Tag == tag) as OxyPlot.Series.LineSeries;

                var fpsChart = _viewModel.ComparisonFpsModel.Series
                    .FirstOrDefault(chart => (string)chart.Tag == tag) as OxyPlot.Series.LineSeries;

                if (frametimesChart != null && fpsChart != null)
                {
                    frametimesChart.StrokeThickness = _viewModel.FrametimeSeriesStrokeThickness + delta;
                    fpsChart.StrokeThickness = _viewModel.FpsSeriesStrokeThickness + delta;

                    SeriesHighlightAnnotation.SetHighlight(_viewModel.ComparisonFrametimesModel,
                        isEntering ? frametimesChart : null);
                    SeriesHighlightAnnotation.SetHighlight(_viewModel.ComparisonFpsModel,
                        isEntering ? fpsChart : null);

                    _viewModel.ComparisonFrametimesModel.InvalidatePlot(true);
                    _viewModel.ComparisonFpsModel.InvalidatePlot(true);
                }
            }

            // Distribution
            if (_viewModel.ComparisonDistributionModel.Series.Any())
            {
                var distributionChart = _viewModel.ComparisonDistributionModel.Series
                    .FirstOrDefault(chart => (string)chart.Tag == tag) as OxyPlot.Series.LineSeries;

                if (distributionChart != null)
                {
                    distributionChart.StrokeThickness = _viewModel.DistributionSeriesStrokeThickness + delta;

                    SeriesHighlightAnnotation.SetHighlight(_viewModel.ComparisonDistributionModel,
                        isEntering ? distributionChart : null);

                    _viewModel.ComparisonDistributionModel.InvalidatePlot(true);
                }
            }

            // Row Chart Highlight
            if (_viewModel.ComparisonRowChartSeriesCollection.Any())
            {
                foreach (var item in _viewModel.ComparisonRowChartSeriesCollection)
                {
                    var rowSeries = item as RowSeries;
                    if (isEntering)
                        rowSeries.HighlightChartPoint(_viewModel.ComparisonRecords.Count - index - 1);
                    else
                        rowSeries.UnHighlightChartPoint(_viewModel.ComparisonRecords.Count - index - 1);
                }
            }
        }
   
    }
}
