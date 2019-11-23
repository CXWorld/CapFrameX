using CapFrameX.Contracts.Data;
using CapFrameX.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace CapFrameX.ViewModel
{
	public partial class ComparisonViewModel
	{
		private void AddComparisonItem(IFileRecordInfo recordInfo)
		{
			var stopwatchData = new Stopwatch();
			stopwatchData.Start();
			var comparisonRecordInfo = GetComparisonRecordInfoFromFileRecordInfo(recordInfo);
			var wrappedComparisonRecordInfo = GetWrappedRecordInfo(comparisonRecordInfo);

			// Insert into list (sorted)
			SetMetrics(wrappedComparisonRecordInfo);
			InsertComparisonRecordsSorted(wrappedComparisonRecordInfo);

			HasComparisonItems = ComparisonRecords.Any();

			// Manage game name header
			HasUniqueGameNames = GetHasUniqueGameNames();
			CurrentGameName = comparisonRecordInfo.Game;

			// Update height of bar chart control here
			UpdateBarChartHeight();
			UpdateCuttingParameter();

			//Draw charts and performance parameter
			UpdateCharts();
		}

		private void SetMetrics(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
		{
			double startTime = FirstSeconds;
			double lastFrameStart = wrappedComparisonRecordInfo.WrappedRecordInfo.Session.FrameStart.Last();
			double endTime = LastSeconds > lastFrameStart ? lastFrameStart : lastFrameStart - LastSeconds;
			var frametimeTimeWindow = wrappedComparisonRecordInfo.WrappedRecordInfo.Session.GetFrametimeTimeWindow(startTime, endTime, ERemoveOutlierMethod.None);
			double GeMetricValue(IList<double> sequence, EMetric metric) =>
					_frametimeStatisticProvider.GetFpsMetricValue(sequence, metric);

			wrappedComparisonRecordInfo.WrappedRecordInfo.FirstMetric
				= GeMetricValue(frametimeTimeWindow, EMetric.Average);

			wrappedComparisonRecordInfo.WrappedRecordInfo.SecondMetric
				= GeMetricValue(frametimeTimeWindow, SelectedSecondaryMetric);

			wrappedComparisonRecordInfo.WrappedRecordInfo.ThirdMetric
				= GeMetricValue(frametimeTimeWindow, SelectedThirdMetric);
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

			List<ComparisonRecordInfoWrapper> orderedList = null;

			if (UseComparisonGrouping)
			{
				orderedList = IsSortModeAscending ? list.OrderBy(x => x.WrappedRecordInfo.Game).ThenBy(x => x.WrappedRecordInfo.FirstMetric).ToList() :
					list.OrderBy(x => x.WrappedRecordInfo.Game).ThenByDescending(x => x.WrappedRecordInfo.FirstMetric).ToList();
			}
			else
			{
				orderedList = IsSortModeAscending ? list.OrderBy(x => x.WrappedRecordInfo.FirstMetric).ToList() :
					list.OrderByDescending(x => x.WrappedRecordInfo.FirstMetric).ToList();
			}

			if (orderedList != null)
			{
				var index = orderedList.IndexOf(wrappedComparisonRecordInfo);
				ComparisonRecords.Insert(index, wrappedComparisonRecordInfo);
			}
		}

		public void RemoveComparisonItem(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
		{
			_comparisonColorManager.FreeColor(wrappedComparisonRecordInfo.Color);
			ComparisonRecords.Remove(wrappedComparisonRecordInfo);

			HasComparisonItems = ComparisonRecords.Any();
			UpdateCuttingParameter();
			UpdateCharts();
			UpdateBarChartHeight();

			// Manage game name header		
			HasUniqueGameNames = GetHasUniqueGameNames();
			if (HasUniqueGameNames)
				CurrentGameName = ComparisonRecords.First().WrappedRecordInfo.Game;

			ComparisonModel.InvalidatePlot(true);
		}

		private bool GetHasUniqueGameNames()
		{
			if (!ComparisonRecords.Any())
				return false;

			var firstName = ComparisonRecords.First().WrappedRecordInfo.Game;

			return !ComparisonRecords.Any(record => record.WrappedRecordInfo.Game != firstName);
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

			// Manage game name header
			HasUniqueGameNames = false;
			CurrentGameName = string.Empty;

			RemainingRecordingTime = "0.0 s";
			UpdateCuttingParameter();
			ComparisonModel.InvalidatePlot(true);
		}

		public void SortComparisonItems()
		{
			if (!ComparisonRecords.Any())
				return;

			IEnumerable<ComparisonRecordInfoWrapper> comparisonRecordList = null;

			if (UseComparisonGrouping)
			{
				comparisonRecordList = IsSortModeAscending ? ComparisonRecords.ToList()
					.Select(info => info.Clone()).OrderBy(x => x.WrappedRecordInfo.Game).ThenBy(x => x.WrappedRecordInfo.FirstMetric) :
					ComparisonRecords.ToList().Select(info => info.Clone()).OrderBy(x => x.WrappedRecordInfo.Game).ThenByDescending(x => x.WrappedRecordInfo.FirstMetric);
			}
			else
			{
				comparisonRecordList = IsSortModeAscending ? ComparisonRecords.ToList()
					.Select(info => info.Clone()).OrderBy(x => x.WrappedRecordInfo.FirstMetric) :
					ComparisonRecords.ToList().Select(info => info.Clone()).OrderByDescending(x => x.WrappedRecordInfo.FirstMetric);
			}

			if (comparisonRecordList != null)
			{
				ComparisonRecords.Clear();

				foreach (var item in comparisonRecordList)
				{
					ComparisonRecords.Add(item);
				}

				//Draw charts and performance parameter
				UpdateCharts();
			}
		}
	}
}
