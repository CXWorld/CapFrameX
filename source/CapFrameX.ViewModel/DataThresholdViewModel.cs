using CapFrameX.Statistics;
using LiveCharts;
using LiveCharts.Wpf;
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

		public bool ThresholdToggleButtonIsChecked
		{
			get { return _appConfiguration.AreThresholdsReversed; }
			set
			{
				_appConfiguration.AreThresholdsReversed = value;
				OnThresholdDirectionChanged();
				RaisePropertyChanged();
			}
		}
		public ICommand CopyFPSThresholdDataCommand { get; }

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

		private void OnThresholdDirectionChanged()
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

			Application.Current.Dispatcher.Invoke(new Action(() =>
			{
				FPSThresholdCollection = new SeriesCollection
				{
					new ColumnSeries
					{
						Values = thresholdCountValues,
						Fill = new SolidColorBrush(Color.FromRgb(34, 151, 243)),
						DataLabels = true,
						LabelPoint = p => (frametimes.Count* p.Y).ToString(),
						MaxColumnWidth = 40
					}
				};
			}));
		}
	}
}
