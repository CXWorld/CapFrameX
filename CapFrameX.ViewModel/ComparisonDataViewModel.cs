using CapFrameX.OcatInterface;
using GongSolutions.Wpf.DragDrop;
using Prism.Mvvm;
using Prism.Regions;
using System.Collections.ObjectModel;
using System.Windows;

namespace CapFrameX.ViewModel
{
	public class ComparisonDataViewModel : BindableBase, INavigationAware, IDropTarget
	{
		public ObservableCollection<OcatRecordInfo> Items { get; }
			= new ObservableCollection<OcatRecordInfo>();

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

		void IDropTarget.Drop(IDropInfo dropInfo)
		{
			//throw new System.NotImplementedException();
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
