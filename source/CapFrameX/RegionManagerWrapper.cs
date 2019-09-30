using Prism.Regions;
using System;
using System.Windows;

namespace CapFrameX
{
    public class RegionManagerWrapper
    {
        public static RegionManagerWrapper Singleton { get; } = new RegionManagerWrapper();

        internal IRegionManager RegionManager { get; }

        public RegionManagerWrapper()
        {
            RegionManager = Prism.Regions.RegionManager.GetRegionManager(Application.Current.MainWindow);
        }

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
