using CapFrameX.Contracts.MVVM;
using CapFrameX.Data;
using CapFrameX.EventAggregation.Messages;
using CapFrameX.Extensions;
using LiveCharts.Wpf;
using OxyPlot;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
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

        public ComparisonRecordInfoWrapper Clone()
        {
            return new ComparisonRecordInfoWrapper(WrappedRecordInfo, _viewModel)
            {
                Color = Color,
                FrametimeGraphColor = FrametimeGraphColor,
            };
        }


        private void OnHideModeChanged()
        {
            UpdateChartsColor(FrametimeGraphColor, hideMode: IsHideModeSelected, updateBrush: false);
        }

        private void OnColorChanged()
        {
            UpdateChartsColor(FrametimeGraphColor, hideMode: false, updateBrush: true);
        }
        void IMouseEventHandler.OnMouseEnter()
        {
            UpdateMouseInteraction(isEntering: true);
        }

        void IMouseEventHandler.OnMouseLeave()
        {
            UpdateMouseInteraction(isEntering: false);
        }



        private void UpdateChartsColor(Color? colorOverride, bool hideMode, bool updateBrush)
        {
            if (!colorOverride.HasValue || !_viewModel.ComparisonRecords.Any())
                return;

            _viewModel.SetChartUpdateFlags();

            var color = colorOverride.Value;
            var tag = WrappedRecordInfo.FileRecordInfo.Id;
            var oxyColor = OxyColor.FromArgb(color.A, color.R, color.G, color.B);
            var solidBrush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(color.A, color.R, color.G, color.B));
            var chartTitle = _viewModel.GetChartLabel(WrappedRecordInfo).Context;

            // Frametime + LShape + FPS
            if (_viewModel.ComparisonFrametimesModel.Series.Any() && _viewModel.ComparisonLShapeCollection.Any())
            {
                var frametimesChart = _viewModel.ComparisonFrametimesModel.Series
                    .FirstOrDefault(chart => (string)chart.Tag == tag) as OxyPlot.Series.LineSeries;

                var fpsChart = _viewModel.ComparisonFpsModel.Series
                    .FirstOrDefault(chart => (string)chart.Tag == tag) as OxyPlot.Series.LineSeries;

                var lShapeChart = _viewModel.ComparisonLShapeCollection
                    .FirstOrDefault(chart => chart.Id == tag) as LineSeries;

                if (frametimesChart != null && fpsChart != null && lShapeChart != null)
                {
                    if (hideMode)
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
                        frametimesChart.Color = oxyColor;
                        fpsChart.Color = oxyColor;
                        lShapeChart.Stroke = solidBrush;
                        lShapeChart.PointForeground = solidBrush;
                        frametimesChart.Title = chartTitle;
                        fpsChart.Title = chartTitle;

                        if (updateBrush)
                        {
                            _viewModel.ComparisonColorManager.FreeColor(Color);
                            Color = solidBrush;
                            _viewModel.ComparisonColorManager.LockColorOnChange(Color);
                        }
                    }

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
                    if (hideMode)
                    {
                        distributionChart.Color = OxyColors.Transparent;
                        distributionChart.Title = string.Empty;
                    }
                    else
                    {
                        distributionChart.Color = oxyColor;
                        distributionChart.Title = chartTitle;
                    }

                    _viewModel.ComparisonDistributionModel.InvalidatePlot(true);
                }
            }
        }


        private void UpdateMouseInteraction(bool isEntering)
        {
            if (!_viewModel.ComparisonRecords.Any())
                return;

            var tag = WrappedRecordInfo.FileRecordInfo.Id;
            var index = _viewModel.ComparisonRecords.IndexOf(this);
            int delta = isEntering ? 2 : -2;

            // Frametimes + FPS
            if (_viewModel.ComparisonFrametimesModel.Series.Any())
            {
                var frametimesChart = _viewModel.ComparisonFrametimesModel.Series
                    .FirstOrDefault(chart => (string)chart.Tag == tag) as OxyPlot.Series.LineSeries;

                var fpsChart = _viewModel.ComparisonFpsModel.Series
                    .FirstOrDefault(chart => (string)chart.Tag == tag) as OxyPlot.Series.LineSeries;

                if (frametimesChart != null && fpsChart != null)
                {
                    frametimesChart.StrokeThickness += delta;
                    fpsChart.StrokeThickness += delta;

                    if (isEntering)
                    {
                        int indexFrametimes = _viewModel.ComparisonFrametimesModel.Series.IndexOf(frametimesChart);
                        int indexFps = _viewModel.ComparisonFpsModel.Series.IndexOf(fpsChart);

                        _viewModel.ComparisonFrametimesModel.Series.Move(indexFrametimes, _viewModel.ComparisonFrametimesModel.Series.Count - 1);
                        _viewModel.ComparisonFpsModel.Series.Move(indexFps, _viewModel.ComparisonFpsModel.Series.Count - 1);
                    }

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
                    distributionChart.StrokeThickness += delta;

                    if (isEntering)
                    {
                        int indexDist = _viewModel.ComparisonDistributionModel.Series.IndexOf(distributionChart);
                        _viewModel.ComparisonDistributionModel.Series.Move(indexDist, _viewModel.ComparisonDistributionModel.Series.Count - 1);
                    }

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
