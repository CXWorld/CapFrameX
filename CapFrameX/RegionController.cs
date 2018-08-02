using Prism.Regions;
using System;
using System.Windows;

namespace CapFrameX
{
    public class RegionController
    {
        public RegionController()
        {
            RegionManager = Prism.Regions.RegionManager.GetRegionManager(Application.Current.MainWindow);
        }

        public RegionController(IRegionManager regionManager)
        {
            RegionManager = regionManager;
        }

        public static RegionController Singleton { get; } = new RegionController();

        internal IRegionManager RegionManager { get; }

        public static void ActivateView(string region, string view)
        {
            Singleton.RegionManager.RequestNavigate(region, view);
        }

        public void RegisterViewWithRegion(string regionName, Type viewType)
        {
            RegionManager.RegisterViewWithRegion(regionName, viewType);
        }
    }
}
