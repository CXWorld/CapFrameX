using CapFrameX.Contracts.RTSS;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;

namespace OpenHardwareMonitor.Hardware
{
    internal class RefreshRateBuffer
    {
        private int _size;
        private List<float> _buffer;

        public IEnumerable<float> RefreshRates => _buffer;

        public RefreshRateBuffer(int size)
        {
            _size = size;
            _buffer = new List<float>(size + 1);
        }

        public void Add(float sample)
        {
            _buffer.Add(sample);
            if (_buffer.Count > _size)
            {
                _buffer.RemoveAt(0);
            }
        }

        public void Clear()
        {
            _buffer?.Clear();
        }
    }


    internal abstract class GPUBase : Hardware
    {
        protected const float SCALE = 1024 * 1024 * 1024;

        protected readonly object _performanceCounterLock = new object();
        protected readonly object _displayLock = new object();

        protected Sensor memoryUsageDedicated;
        protected Sensor memoryUsageShared;
        protected Sensor processMemoryUsageDedicated;
        protected Sensor processMemoryUsageShared;

        protected readonly PerformanceCounter dedicatedVramUsagePerformCounter;
        protected readonly PerformanceCounter sharedVramUsagePerformCounter;
        protected PerformanceCounter dedicatedVramUsageProcessPerformCounter;
        protected PerformanceCounter sharedVramUsageProcessPerformCounter;

        protected Display display;
        protected float refreshRateCurrentWindowHandle;
        protected RefreshRateBuffer refreshRateBuffer;

        public GPUBase(string name, Identifier identifier, ISettings settings, IRTSSService rTSSService) : base(name, identifier, settings)
        {
            refreshRateBuffer = new RefreshRateBuffer(2);

            try
            {
                if (PerformanceCounterCategory.Exists("GPU Adapter Memory"))
                {
                    var category = new PerformanceCounterCategory("GPU Adapter Memory");
                    var instances = category.GetInstanceNames().Where(inst => inst != string.Empty).ToArray();

                    if (instances.Any())
                    {
                        float maxValue = 0;
                        int maxIndex = 0;
                        for (int i = 0; i < instances.Length; i++)
                        {
                            try
                            {
                                var currentPerfCounter = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instances[i]);

                                if (currentPerfCounter.NextValue() > maxValue)
                                {
                                    maxValue = currentPerfCounter.NextValue();
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
                    else
                    {
                        Log.Logger.Error("Error while creating GPU memory performance counter. No instances found.");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while creating GPU memory performance counter.");
            }

            try
            {
                if (PerformanceCounterCategory.Exists("GPU Process Memory"))
                {
                    var category = new PerformanceCounterCategory("GPU Process Memory");

                    _ = rTSSService
                    .ProcessIdStream
                    .DistinctUntilChanged()
                    .Subscribe(id =>
                    {
                        lock (_performanceCounterLock)
                        {
                            if (id == 0)
                            {
                                dedicatedVramUsageProcessPerformCounter = null;
                                sharedVramUsageProcessPerformCounter = null;
                            }
                            else
                            {
                                string idString = $"pid_{id}_luid";

                                var instances = category.GetInstanceNames();
                                if (instances != null && instances.Any())
                                {
                                    var pids = instances.Where(instance => instance.Contains(idString));

                                    if (pids != null)
                                    {
                                        var pid = GetMaximumPid(pids);
                                        dedicatedVramUsageProcessPerformCounter = new PerformanceCounter("GPU Process Memory", "Dedicated Usage", pid);
                                        sharedVramUsageProcessPerformCounter = new PerformanceCounter("GPU Process Memory", "Shared Usage", pid);
                                    }
                                    else
                                    {
                                        dedicatedVramUsageProcessPerformCounter = null;
                                        sharedVramUsageProcessPerformCounter = null;
                                    }
                                }
                                else
                                {
                                    dedicatedVramUsageProcessPerformCounter = null;
                                    sharedVramUsageProcessPerformCounter = null;
                                    Log.Logger.Error("Error while creating GPU process memory performance counter. No instances found.");
                                }
                            }
                        }

                        lock (_displayLock)
                        {
                            refreshRateBuffer.Clear();

                            if (id == 0)
                            {
                                display = null;
                                refreshRateCurrentWindowHandle = 0;
                            }
                            else
                            {
                                try
                                {
                                    var process = Process.GetProcessById(id);
                                    display = new Display(process.MainWindowHandle);
                                    refreshRateCurrentWindowHandle = display.GetDisplayRefreshRate();
                                }
                                catch
                                {
                                    display = null;
                                    refreshRateCurrentWindowHandle = 0;
                                }
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Logger.Error(ex, "Error while creating GPU process memory performance counter.");
            }
        }

        private string GetMaximumPid(IEnumerable<string> pids)
        {
            string maxPid = string.Empty;
            float maxVramUsage = 0f;
            foreach (var pid in pids)
            {
                var currentDedicatedVramUsageProcessPerformCounter = new PerformanceCounter("GPU Process Memory", "Dedicated Usage", pid);

                float currentVramUsage = currentDedicatedVramUsageProcessPerformCounter.NextValue();
                if (currentVramUsage >= maxVramUsage)
                {
                    maxVramUsage = currentVramUsage;
                    maxPid = pid;
                }
            }

            return maxPid;
        }
    }
}
