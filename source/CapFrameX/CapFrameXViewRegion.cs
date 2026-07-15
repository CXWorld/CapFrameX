using System;
using CapFrameX.PresentMonInterface;
using CapFrameX.View;
using Prism.Modularity;

namespace CapFrameX
{
    public class CapFrameXViewRegion : IModule
    {
        public void Initialize()
        {
            using (StartupPerformanceLogger.Measure("CapFrameX view module initialization total"))
            {
                RegisterViewWithTiming("ColorbarRegion", typeof(ColorbarView));
                RegisterViewWithTiming("ControlRegion", typeof(ControlView));

                bool isCaptureServiceCompatible;
                using (StartupPerformanceLogger.Measure("PresentMon OS compatibility check"))
                {
                    isCaptureServiceCompatible = CaptureServiceInfo.IsCompatibleWithRunningOS;
                }

                if (isCaptureServiceCompatible)
                    RegisterViewWithTiming("DataRegion", typeof(CaptureView));
                RegisterViewWithTiming("DataRegion", typeof(OverlayView));
                RegisterViewWithTiming("DataRegion", typeof(DataView));
                RegisterViewWithTiming("DataRegion", typeof(AggregationView));
                RegisterViewWithTiming("DataRegion", typeof(ComparisonView));
                RegisterViewWithTiming("DataRegion", typeof(SensorView));
                RegisterViewWithTiming("DataRegion", typeof(PmdView));
                RegisterViewWithTiming("DataRegion", typeof(ReportView));
                RegisterViewWithTiming("DataRegion", typeof(SynchronizationView));
                RegisterViewWithTiming("DataRegion", typeof(CloudView));
                RegisterViewWithTiming("StateRegion", typeof(StateView));
            }
        }

        private static void RegisterViewWithTiming(string regionName, Type viewType)
        {
            using (StartupPerformanceLogger.Measure("Region registration/activation: " + viewType.Name))
            {
                RegionManagerWrapper.Singleton.RegisterViewWithRegion(regionName, viewType);
            }
        }
    }
}
