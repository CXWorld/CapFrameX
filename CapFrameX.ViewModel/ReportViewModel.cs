using GongSolutions.Wpf.DragDrop;
using Prism.Mvvm;
using Prism.Regions;

namespace CapFrameX.ViewModel
{
    public class ReportViewModel : BindableBase, INavigationAware, IDropTarget
    {
        public bool IsNavigationTarget(NavigationContext navigationContext)
        {
            throw new System.NotImplementedException();
        }

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            throw new System.NotImplementedException();
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            throw new System.NotImplementedException();
        }

        void IDropTarget.DragOver(IDropInfo dropInfo)
        {
            throw new System.NotImplementedException();
        }

        void IDropTarget.Drop(IDropInfo dropInfo)
        {
            throw new System.NotImplementedException();
        }
    }
}
