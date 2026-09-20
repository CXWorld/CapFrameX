using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Threading;
using CapFrameX.PresentMonInterface;
using CapFrameX.View;
using Prism.Ioc;
using Prism.Modularity;
using Serilog;

namespace CapFrameX
{
    public class CapFrameXViewRegion : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
        }

        public void OnInitialized(IContainerProvider containerProvider)
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

                // First DataRegion registration = startup view; must match the
                // ColorbarViewModel default (InfoIsChecked).
                RegisterViewWithTiming("DataRegion", typeof(InfoView));
                RegisterViewWithTiming("StateRegion", typeof(StateView));

                // Everything below is a tab nobody is looking at yet. Registering a view builds
                // its whole visual tree and its view model right here on the UI thread, which is
                // the single largest item in the startup profile - and the shell cannot be shown
                // until it is done. The remaining tabs are handed to the dispatcher instead, one
                // per idle turn, so the window comes up first and the queue drains behind it
                // while the user reads the info tab.
                //
                // Two ordering constraints: the capture and overlay view models install the
                // global hotkeys, so they go first; and a ContentControl region only
                // auto-activates a view while none is active - which InfoView above already is,
                // so the later arrivals stay in the background where they belong.
                var deferredViews = new List<Type>();
                if (isCaptureServiceCompatible)
                    deferredViews.Add(typeof(CaptureView));
                deferredViews.Add(typeof(OverlayView));
                deferredViews.Add(typeof(DataView));
                deferredViews.Add(typeof(AggregationView));
                deferredViews.Add(typeof(ComparisonView));
                deferredViews.Add(typeof(SensorView));
                deferredViews.Add(typeof(PmdView));
                deferredViews.Add(typeof(ReportView));
                deferredViews.Add(typeof(SynchronizationView));
                deferredViews.Add(typeof(CloudView));

                RegisterDeferred("DataRegion", deferredViews);
            }
        }

        /// <summary>
        /// Queues one registration per dispatcher idle turn. Splitting them up matters as much
        /// as deferring them: pushed through as a single work item they would block the freshly
        /// revealed window for as long as they used to block its creation.
        /// </summary>
        private static void RegisterDeferred(string regionName, IReadOnlyList<Type> viewTypes)
        {
            var dispatcher = Application.Current?.Dispatcher;

            if (dispatcher == null)
            {
                // No application (unit tests, design time): keep the eager behavior.
                foreach (var viewType in viewTypes)
                    RegisterViewWithTiming(regionName, viewType);

                return;
            }

            RegisterNextDeferred(dispatcher, regionName, viewTypes, 0);
        }

        private static void RegisterNextDeferred(Dispatcher dispatcher, string regionName,
            IReadOnlyList<Type> viewTypes, int index)
        {
            if (index >= viewTypes.Count)
            {
                StartupPerformanceLogger.Mark("Deferred tab registration drained");
                return;
            }

            dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    RegisterViewWithTiming(regionName, viewTypes[index]);
                }
                catch (Exception ex)
                {
                    // A tab that cannot be built must not take the remaining ones with it -
                    // before deferring, this would have been a startup crash instead.
                    Log.Logger.Error(ex, "Error while registering {viewName} with {regionName}.",
                        viewTypes[index].Name, regionName);
                }
                finally
                {
                    RegisterNextDeferred(dispatcher, regionName, viewTypes, index + 1);
                }
            }), DispatcherPriority.ApplicationIdle);
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
