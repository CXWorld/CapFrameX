using CapFrameX.Contracts.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ComparisonCollection = System.Collections.ObjectModel.ObservableCollection<CapFrameX.ViewModel.ComparisonRecordInfoWrapper>;

namespace CapFrameX.ViewModel
{
	public partial class ComparisonViewModel
	{
		private void AddComparisonItem(IFileRecordInfo recordInfo)
		{
			if (ComparisonRecords.Count < _comparisonBrushes.Count())
			{
				var comparisonRecordInfo = GetComparisonRecordInfoFromFileRecordInfo(recordInfo);
				var wrappedComparisonRecordInfo = GetWrappedRecordInfo(comparisonRecordInfo);

				//Update list and index
				ComparisonRecords.Add(wrappedComparisonRecordInfo);
				wrappedComparisonRecordInfo.CollectionIndex = ComparisonRecords.Count - 1;

				var color = _freeColors.First();
				_freeColors.Remove(color);
				wrappedComparisonRecordInfo.Color = color;
				wrappedComparisonRecordInfo.FrametimeGraphColor = color.Color;

				InitialIconVisibility = !ComparisonRecords.Any();
				BarChartVisibility = ComparisonRecords.Any();
				ComparisonItemControlHeight = ComparisonRecords.Any() ? "Auto" : "300";

				//ToDo: Update height of bar chart control here
				BarChartHeight = 40 + (2 * BarChartMaxRowHeight + 12) * ComparisonRecords.Count;

				UpdateCuttingParameter();

				//Draw charts and performance parameter
				AddToCharts(wrappedComparisonRecordInfo);
			}
		}

		public void RemoveComparisonItem(ComparisonRecordInfoWrapper wrappedComparisonRecordInfo)
		{
			_freeColors.Add(wrappedComparisonRecordInfo.Color);
			ComparisonRecords.Remove(wrappedComparisonRecordInfo);
			UpdateIndicesAfterRemoveItem(ComparisonRecords);
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
		}

		public void RemoveAllComparisonItems(bool manageVisibility = true)
		{
			foreach (var record in ComparisonRecords)
			{
				_freeColors.Add(record.Color);
			}

			ComparisonRecords.Clear();

			UpdateCharts();

			if (manageVisibility)
			{
				InitialIconVisibility = true;
				BarChartVisibility = false;
			}
		}

		public void SortComparisonItems()
		{
			IEnumerable<ComparisonRecordInfoWrapper> comparisonRecordList = null;
			if (IsSortModeAscendingActive)
				comparisonRecordList = ComparisonRecords.ToList()
					.Select(info => info.Clone())
					.OrderBy(info => info.WrappedRecordInfo.SortCriteriaParameter);
			else
				comparisonRecordList = ComparisonRecords.ToList()
					.Select(info => info.Clone())
					.OrderByDescending(info => info.WrappedRecordInfo.SortCriteriaParameter);

			RemoveAllComparisonItems(false);

			foreach (var item in comparisonRecordList)
			{
				ComparisonRecords.Add(item);
			}

			RaisePropertyChanged(nameof(ComparisonRecords));

			UpdateCharts();
		}

		private void UpdateIndicesAfterRemoveItem(ComparisonCollection comparisonRecords)
		{
			for (int i = 0; i < comparisonRecords.Count; i++)
			{
				comparisonRecords[i].CollectionIndex = i;
			}
		}
	}
}
