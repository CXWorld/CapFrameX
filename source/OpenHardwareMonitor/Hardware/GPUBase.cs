using Serilog;
using System;
using System.Diagnostics;
using System.Linq;

namespace OpenHardwareMonitor.Hardware
{
    internal abstract class GPUBase : Hardware
    {
        protected  Sensor memoryUsageDedicated;
        protected  Sensor memoryUsageShared;

        protected readonly PerformanceCounter dedicatedVramUsagePerformCounter;
        protected readonly PerformanceCounter sharedVramUsagePerformCounter;

        public GPUBase(string name, Identifier identifier, ISettings settings) : base(name, identifier, settings)
        {
            try
            {
                if (PerformanceCounterCategory.Exists("GPU Adapter Memory"))
                {
                    var gpuCatCategories = PerformanceCounterCategory.GetCategories().Where(cat => cat.CategoryName.Contains("GPU"));

                    var category = new PerformanceCounterCategory("GPU Adapter Memory");
                    var instances = category.GetInstanceNames();

                    if (instances.Any())
                    {
                        long maxRawValue = 0;
                        int maxIndex = 0;
                        for (int i = 0; i < instances.Length; i++)
                        {
                            try
                            {
                                var currentPerfCounterPerProcess = new PerformanceCounter("GPU Process Memory", "Dedicated Usage", "FurMark");
                                var currentPerfCounter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instances[i]);

                                if (currentPerfCounter.RawValue > maxRawValue)
                                {
                                    maxRawValue = currentPerfCounter.RawValue;
                                    maxIndex = i;
                                }

                            }
                            catch (Exception ex)
                            {
                                Log.Logger.Error(ex, $"Error while creating performance counter with instance {i}.");
                            }
                        }

                        dedicatedVramUsagePerformCounter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instances[maxIndex]);
                        sharedVramUsagePerformCounter = new PerformanceCounter("GPU Adapter Memory", "Shared Usage", instances[maxIndex]);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while creating GPU memory performance counter.");
            }
        }
    }
}
