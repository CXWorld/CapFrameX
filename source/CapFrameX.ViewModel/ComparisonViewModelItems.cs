using CapFrameX.Contracts.Data;
using CapFrameX.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX.ViewModel
{
	public partial class ComparisonViewModel
	{
		private void AddComparisonItem(IFileRecordInfo recordInfo)
		{
			var comparisonRecordInfo = GetComparisonRecordInfoFromFileRecordInfo(recordInfo);
			var wrappedComparisonRecordInfo = GetWrappedRecordInfo(comparisonRecordInfo);

			// Insert into list (sorted)
			SetMetrics(wrappedComparisonRecordInfo);
			InsertComparisonRecordsSorted(wrappedComparisonRecordInfo);

			HasComparisonItems = ComparisonRecords.Any();

			// Update height of bar chart control here
			UpdateBarChartHeight();
			UpdateCuttingParameter();

			//Draw charts and performance parameter
			UpdateCharts();
		}

		private void SetMetrics(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
		{
			double startTime = FirstSeconds;
			double endTime = wrappedComparisonRecordInfo.WrappedRecordInfo.Session.FrameStart.Last() - LastSeconds;
			var frametimeTimeWindow = wrappedComparisonRecordInfo.WrappedRecordInfo.Session.GetFrametimeTimeWindow(startTime, endTime, ERemoveOutlierMethod.None);
			double GeMetricValue(IList<double> sequence, EMetric metric) =>
					_frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);

			wrappedComparisonRecordInfo.WrappedRecordInfo.FirstMetric
				= GeMetricValue(frametimeTimeWindow, EMetric.Average);

			wrappedComparisonRecordInfo.WrappedRecordInfo.SecondMetric 
				= GeMetricValue(frametimeTimeWindow, SelectSecondaryMetric);

			// ToDo: implement third metric
		}

		private void InsertComparisonRecordsSorted(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
		{
			if (!ComparisonRecords.Any())
			{
				ComparisonRecords.Add(wrappedComparisonRecordInfo);
				return;
			}

			var list = new List<ComparisonRecordInfoWrapper>(ComparisonRecords)
			{
				wrappedComparisonRecordInfo
			};

			var orderedList = IsSortModeAscending ? list.OrderBy(x => x.WrappedRecordInfo.FirstMetric).ToList() :
				list.OrderByDescending(x => x.WrappedRecordInfo.FirstMetric).ToList();

			var index = orderedList.IndexOf(wrappedComparisonRecordInfo);
			ComparisonRecords.Insert(index, wrappedComparisonRecordInfo);
		}

		public void RemoveComparisonItem(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
		{
			_comparisonColorManager.FreeColor(wrappedComparisonRecordInfo.Color);
			ComparisonRecords.Remove(wrappedComparisonRecordInfo);

			HasComparisonItems = ComparisonRecords.Any();
			UpdateCuttingParameter();
			UpdateCharts();

			// Do with delay
			var context = TaskScheduler.FromCurrentSynchronizationContext();
			Task.Run(async () =>
			{
				await SetTaskDelay().ContinueWith(_ =>
				{
					Application.Current.Dispatcher.Invoke(new Action(() =>
					{
						BarChartHeight = 40 + (2 * BarChartMaxRowHeight + 12) * ComparisonRecords.Count;
					}));
				}, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, context);
			});

			ComparisonModel.InvalidatePlot(true);
		}

		public void RemoveAllComparisonItems(bool manageVisibility = true, bool resetSortMode = false)
		{
			if (resetSortMode)
			{
				_comparisonColorManager.FreeAllColors();
			}

			ComparisonRecords.Clear();
			UpdateCharts();

			if (resetSortMode)
				IsSortModeAscending = false;

			if (manageVisibility)
			{
				HasComparisonItems = false;
			}

			RemainingRecordingTime = "0.0 s";
			UpdateCuttingParameter();
			ComparisonModel.InvalidatePlot(true);
		}

		[Obsolete]
		public void SortComparisonItems()
		{
			if (!ComparisonRecords.Any())
				return;

			IEnumerable<ComparisonRecordInfoWrapper> comparisonRecordList = null;
			if (IsSortModeAscending)
				comparisonRecordList = ComparisonRecords.ToList()
					.Select(info => info.Clone())
					.OrderBy(info => info.WrappedRecordInfo.FirstMetric);
			else
				comparisonRecordList = ComparisonRecords.ToList()
					.Select(info => info.Clone())
					.OrderByDescending(info => info.WrappedRecordInfo.FirstMetric);

			RemoveAllComparisonItems(false, false);

			foreach (var item in comparisonRecordList)
			{
				ComparisonRecords.Add(item);
			}

			RaisePropertyChanged(nameof(ComparisonRecords));

			ResetBarChartSeriesTitles();
			SetColumnChart();
		}
	}
}
