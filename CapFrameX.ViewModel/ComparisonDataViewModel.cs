using CapFrameX.OcatInterface;
using GongSolutions.Wpf.DragDrop;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace CapFrameX.ViewModel
{
	public class ComparisonDataViewModel : BindableBase, INavigationAware, IDropTarget
	{
		private bool _initialIconVisibility = true;

		public bool InitialIconVisibility
		{
			get { return _initialIconVisibility; }
			set
			{
				_initialIconVisibility = value;
				RaisePropertyChanged();
			}
		}

		public ObservableCollection<ComparisonRecordInfo> ComparisonRecords { get; }
			= new ObservableCollection<ComparisonRecordInfo>();

		public ComparisonDataViewModel()
		{

		}

		public bool IsNavigationTarget(NavigationContext navigationContext)
		{
			return true;
		}

		public void OnNavigatedFrom(NavigationContext navigationContext)
		{

		}

		public void OnNavigatedTo(NavigationContext navigationContext)
		{

		}

		private ComparisonRecordInfo GetComparisonRecordInfoFromOcatRecordInfo(OcatRecordInfo ocatRecordInfo)
		{
			string infoText = string.Empty;
			var session = RecordManager.LoadData(ocatRecordInfo.FullPath);

			if (session != null)
			{
				var newLine = Environment.NewLine;
				infoText += "creation datetime: " + ocatRecordInfo.FileInfo.CreationTime.ToString() + newLine +
							"capture time: " + Math.Round(session.LastFrameTime, 2).ToString(CultureInfo.InvariantCulture) + " sec" + newLine +
							"number of samples: " + session.FrameTimes.Count.ToString();
			}

			return new ComparisonRecordInfo
			{
				Game = ocatRecordInfo.GameName,
				InfoText = infoText
			};
		}

		void IDropTarget.Drop(IDropInfo dropInfo)
		{
			if (dropInfo != null)
			{
				if (dropInfo.VisualTarget is FrameworkElement frameworkElement)
				{
					if (frameworkElement.Name == "ComparisonRecordItemControl" || 
						frameworkElement.Name == "ComparisonImage")
					{
						if (dropInfo.Data is OcatRecordInfo recordInfo)
						{
							var comparisonInfo = GetComparisonRecordInfoFromOcatRecordInfo(recordInfo);
							ComparisonRecords.Add(comparisonInfo);
							InitialIconVisibility = !ComparisonRecords.Any();
						}
					}
					else if (frameworkElement.Name == "DelteRecordItemControl")
					{
						if (dropInfo.Data is ComparisonRecordInfo comparisonRecordInfo)
						{
							ComparisonRecords.Remove(comparisonRecordInfo);
							InitialIconVisibility = !ComparisonRecords.Any();
						}
					}
				}				
			}
		}

		void IDropTarget.DragOver(IDropInfo dropInfo)
		{
			if (dropInfo != null)
			{
				dropInfo.DropTargetAdorner = DropTargetAdorners.Highlight;
				dropInfo.Effects = DragDropEffects.Move;
			}
		}
	}
}
