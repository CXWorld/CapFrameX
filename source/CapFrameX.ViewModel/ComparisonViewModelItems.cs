using CapFrameX.Contracts.Data;
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

			//Update list and index
			ComparisonRecords.Add(wrappedComparisonRecordInfo);

			var color = _comparisonColorManager.GetNextFreeColor();
			wrappedComparisonRecordInfo.Color = color;
			wrappedComparisonRecordInfo.FrametimeGraphColor = color.Color;

			InitialIconVisibility = !ComparisonRecords.Any();
			BarChartVisibility = ComparisonRecords.Any();
			ComparisonItemControlHeight = ComparisonRecords.Any() ? "Auto" : "300";

			// Update height of bar chart control here
			BarChartHeight = 60 + (2 * BarChartMaxRowHeight + 12) * ComparisonRecords.Count;

			UpdateCuttingParameter();

			//Draw charts and performance parameter
			AddToCharts(wrappedComparisonRecordInfo);

			SortComparisonItems();
		}

		public void RemoveComparisonItem(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
		{
			_comparisonColorManager.FreeColor(wrappedComparisonRecordInfo.Color);
			ComparisonRecords.Remove(wrappedComparisonRecordInfo);
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
				IsSortModeAscendingActive = false;

			if (manageVisibility)
			{
				InitialIconVisibility = true;
				BarChartVisibility = false;
			}

			ComparisonModel.InvalidatePlot(true);
		}

		public void SortComparisonItems()
		{
			if (!ComparisonRecords.Any())
				return;

			IEnumerable<ComparisonRecordInfoWrapper> comparisonRecordList = null;
			if (IsSortModeAscendingActive)
				comparisonRecordList = ComparisonRecords.ToList()
					.Select(info => info.Clone())
					.OrderBy(info => info.WrappedRecordInfo.SortCriteriaParameter);
			else
				comparisonRecordList = ComparisonRecords.ToList()
					.Select(info => info.Clone())
					.OrderByDescending(info => info.WrappedRecordInfo.SortCriteriaParameter);

			RemoveAllComparisonItems(false, false);

			foreach (var item in comparisonRecordList)
			{
				ComparisonRecords.Add(item);
			}

			RaisePropertyChanged(nameof(ComparisonRecords));

			UpdateCharts();
		}
	}
}
