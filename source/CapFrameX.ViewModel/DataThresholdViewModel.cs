using CapFrameX.Statistics;
using CapFrameX.Statistics.NetStandard;
using LiveCharts;
using LiveCharts.Wpf;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace CapFrameX.ViewModel
{
    public partial class DataViewModel
    {
        private string[] _fPSThresholdLabels;
        private SeriesCollection _fPSThresholdCollection;
        private SeriesCollection _fPSThresholdCollectionCopy;

        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
        /// </summary>
        public Func<double, string> YAxisFormatter { get; } =
            value => value.ToString("P", CultureInfo.InvariantCulture);

        /// <summary>
        /// https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings
        /// </summary>
        public Func<double, string> XAxisFormatter { get; } =
            value => value.ToString("N", CultureInfo.InvariantCulture);

        public string _yAxisLabel;

        public string[] FPSThresholdLabels
        {
            get { return _fPSThresholdLabels; }
            set
            {
                _fPSThresholdLabels = value;
                RaisePropertyChanged();
            }
        }

        public SeriesCollection FPSThresholdCollection
        {
            get { return _fPSThresholdCollection; }
            set
            {
                _fPSThresholdCollection = value;
                RaisePropertyChanged();
            }
        }
        public SeriesCollection FPSThresholdCollectionCopy
        {
            get { return _fPSThresholdCollectionCopy; }
            set
            {
                _fPSThresholdCollectionCopy = value;
                RaisePropertyChanged();
            }
        }

        public bool ThresholdToggleButtonIsChecked
        {
            get { return _appConfiguration.AreThresholdsReversed; }
            set
            {
                _appConfiguration.AreThresholdsReversed = value;
                OnThresholdValuesChanged();
                RaisePropertyChanged();
            }
        }

        public bool ThresholdShowAbsoluteValues
        {
            get { return _appConfiguration.AreThresholdValuesAbsolute; }
            set
            {
                _appConfiguration.AreThresholdValuesAbsolute = value;
                OnThresholdValuesChanged();
                RaisePropertyChanged();
            }
        }

        public bool ShowThresholdTimes
        {
            get { return _appConfiguration.ShowThresholdTimes; }
            set
            {
                _appConfiguration.ShowThresholdTimes = value;
                OnThresholdValuesChanged();
                RaisePropertyChanged();
            }
        }

        public string YAxisLabel
        {
            get { return _yAxisLabel; }
            set
            {
                _yAxisLabel = value;
                RaisePropertyChanged();
            }
        }

        public ICommand CopyFPSThresholdDataCommand { get; }
        public DelegateCommand ThresholdCountsCommand { get; }
        public DelegateCommand ThresholdTimesCommand { get; }

        private void OnCopyFPSThresholdData()
        {
            var subset = GetFrametimesSubset();

            if (subset != null)
            {
                var thresholdCounts = _frametimeStatisticProvider.GetFpsThresholdCounts(subset, ThresholdToggleButtonIsChecked);
                StringBuilder builder = new StringBuilder();

                for (int i = 0; i < FPSThresholdLabels.Length; i++)
                {
                    builder.Append($"{FPSThresholdLabels[i]}\t{thresholdCounts[i]}" + Environment.NewLine);
                }

                Clipboard.SetDataObject(builder.ToString(), false);
            }
        }

        private void OnThresholdValuesChanged()
        {
            var subset = GetFrametimesSubset();

            if (subset != null)
            {
                Task.Factory.StartNew(() => SetFpsThresholdChart(subset));
                SetThresholdLabels();
            }
        }

        private void SetThresholdLabels()
        {
            if (ThresholdToggleButtonIsChecked)
                FPSThresholdLabels = FrametimeStatisticProvider.FPSTHRESHOLDS.Reverse().Select(thres =>
                {
                    return $">{thres}";
                }).ToArray();
            else
                FPSThresholdLabels = FrametimeStatisticProvider.FPSTHRESHOLDS.Select(thres =>
                {
                    return $"<{thres}";
                }).ToArray();
        }

        private void SetFpsThresholdChart(IList<double> frametimes)
        {
            if (frametimes == null || !frametimes.Any())
                return;


            var thresholdCounts = _frametimeStatisticProvider.GetFpsThresholdCounts(frametimes, ThresholdToggleButtonIsChecked);
            var thresholdCountValues = new ChartValues<double>();
            thresholdCountValues.AddRange(thresholdCounts.Select(val => (double)val / frametimes.Count));

            var thresholdTimes = _frametimeStatisticProvider.GetFpsThresholdTimes(frametimes, ThresholdToggleButtonIsChecked);
            var thresholdTimesValues = new ChartValues<double>();
            thresholdTimesValues.AddRange(thresholdTimes.Select(val => val / frametimes.Sum()));

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (!ShowThresholdTimes)
                {
                    YAxisLabel = "Frames";
                    FPSThresholdCollection = new SeriesCollection
                    {
                        new ColumnSeries
                        {
                            Values = thresholdCountValues,
                            Fill = new SolidColorBrush(Color.FromRgb(34, 151, 243)),
                            DataLabels = true,
                            LabelPoint = p => ThresholdShowAbsoluteValues ? (frametimes.Count* p.Y).ToString() :
                                (100 * p.Y).ToString("N1", CultureInfo.InvariantCulture) + "%",
                            MaxColumnWidth = 40
                        }
                    };
                }
                else
                {
                    YAxisLabel = "Time";
                    FPSThresholdCollection = new SeriesCollection
                    {
                        new ColumnSeries
                        {
                            Values = thresholdTimesValues,
                            Fill = new SolidColorBrush(Color.FromRgb(34, 151, 243)),
                            DataLabels = true,
                            LabelPoint = p => ThresholdShowAbsoluteValues ? ((frametimes.Sum()* p.Y) * 1E-03).ToString("N1", CultureInfo.InvariantCulture) + "s" :
                                (100 * p.Y).ToString("N1", CultureInfo.InvariantCulture) + "%",
                            MaxColumnWidth = 40
                        }
                    };
                }
            }));

            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                if (!ShowThresholdTimes)
                {
                    YAxisLabel = "Frames";
                    FPSThresholdCollectionCopy = new SeriesCollection
                    {
                        new ColumnSeries
                        {
                            Values = thresholdCountValues,
                            Fill = new SolidColorBrush(Color.FromRgb(34, 151, 243)),
                            DataLabels = true,
                            LabelPoint = p => ThresholdShowAbsoluteValues ? (frametimes.Count* p.Y).ToString() :
                                (100 * p.Y).ToString("N1", CultureInfo.InvariantCulture) + "%",
                            MaxColumnWidth = 40
                        }
                    };
                }
                else
                {
                    YAxisLabel = "Time";
                    FPSThresholdCollectionCopy = new SeriesCollection
                    {
                        new ColumnSeries
                        {
                            Values = thresholdTimesValues,
                            Fill = new SolidColorBrush(Color.FromRgb(34, 151, 243)),
                            DataLabels = true,
                            LabelPoint = p => ThresholdShowAbsoluteValues ? ((frametimes.Sum()* p.Y) * 1E-03).ToString("N1", CultureInfo.InvariantCulture) + "s" :
                                (100 * p.Y).ToString("N1", CultureInfo.InvariantCulture) + "%",
                            MaxColumnWidth = 40
                        }
                    };
                }
            }));
        }
    }
}
