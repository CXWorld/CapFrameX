// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) CapFrameX.Service.Monitoring and Contributors.
// All Rights Reserved.

using CapFrameX.Service.Monitoring.Contracts;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using Serilog;

namespace CapFrameX.Service.Monitoring.Hardware.Gpu;

/// <summary>
/// Represents a generic GPU device.
/// </summary>
public abstract class GenericGpu : Hardware
{
    private const int DISPLAY_CHECK_INTERVAL_MS = 2000;
    private const string GPU_PROCESS_MEMORY_CATEGORY_NAME = "GPU Process Memory";
    private const string GPU_PROCESS_LOCAL_USAGE_COUNTER_NAME = "Local Usage";
    private const string GPU_PROCESS_SHARED_USAGE_COUNTER_NAME = "Shared Usage";
    private const float VRAM_USAGE_SCALE = 1024f * 1024f * 1024f;
    private const int PROCESS_MEMORY_DEDICATED_SENSOR_INDEX = 90;
    private const int PROCESS_MEMORY_SHARED_SENSOR_INDEX = 91;

    /// <summary>
    /// Lock object for display-related operations.
    /// </summary>
    protected readonly object _displayLock = new();
    /// <summary>
    /// Lock object for performance counter operations
    protected readonly object _performanceCounterLock = new();
    /// <summary>
    /// Buffer to store refresh rate values.
    /// </summary>
    protected RefreshRateBuffer<float> _refreshRateBuffer;
    /// <summary>
    /// Current display associated with the GPU.
    /// </summary>
    protected Display _display;
    /// <summary>
    /// Current refresh rate of the window handle.
    /// </summary>
    protected float _refreshRateCurrentDisplay;
    /// <summary>
    /// Name of the display device. 
    /// </summary>
    protected string _displayDeviceName;
    private Sensor _processMemoryUsageDedicated;
    private Sensor _processMemoryUsageShared;

    private readonly bool _enableProcessMemorySensors;
    private IDisposable _processSubscription;
    private PerformanceCounterCategory _gpuProcessMemoryCategory;
    private PerformanceCounter _dedicatedVramUsageProcessPerformCounter;
    private PerformanceCounter _sharedVramUsageProcessPerformCounter;
    private string _processMemoryInstanceFilter;
    private int _counterProcessId;
    private int _currentProcessId;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericGpu" /> class.
    /// </summary>
    /// <param name="name">Component name.</param>
    /// <param name="identifier">Identifier that will be assigned to the device. Based on <see cref="Identifier" /></param>
    /// <param name="settings">Additional settings passed by the <see cref="IComputer" />.</param>
    /// <param name="enableProcessMemorySensors">Indicates whether to enable the process memory sensors. These sensors rely on Windows performance counters and may not be supported or may cause errors on certain systems, particularly those with specific GPU drivers or configurations. Enabling this option will attempt to initialize the necessary performance counters and will monitor the current process for changes to update the sensor values accordingly. If any issues are encountered while initializing or updating the performance counters, the sensors will be disabled and will not report values until a new process change is detected or the counters can be successfully reinitialized.</param>
    protected GenericGpu(string name, Identifier identifier, ISettings settings, bool enableProcessMemorySensors = true) : base(name, identifier, settings)
    {
        _refreshRateBuffer = new RefreshRateBuffer<float>(2);
        var processService = ProcessServiceProvider.ProcessService;
        _enableProcessMemorySensors = enableProcessMemorySensors && !Software.OperatingSystem.IsUnix && processService != null;

        InitializeProcessMemorySensors();

        if (processService == null)
            return;

        var processChangeStream = processService
            .ProcessIdStream
            .DistinctUntilChanged()
            .Select(id => (ProcessId: id, IsProcessChange: true));

        var periodicCheckStream = Observable
            .Interval(TimeSpan.FromMilliseconds(DISPLAY_CHECK_INTERVAL_MS))
            .Select(_ => (ProcessId: _currentProcessId, IsProcessChange: false));

        _processSubscription = processChangeStream
            .Merge(periodicCheckStream)
            .Subscribe(args =>
            {
                if (args.IsProcessChange)
                {
                    _currentProcessId = args.ProcessId;
                }

                if (_enableProcessMemorySensors)
                {
                    EnsureProcessMemoryCounters(args.ProcessId, args.IsProcessChange);
                }

                lock (_displayLock)
                {
                    if (args.ProcessId == 0)
                    {
                        if (_display != null)
                        {
                            _refreshRateBuffer.Clear();
                        }

                        _display = null;
                        _refreshRateCurrentDisplay = 0;
                        _displayDeviceName = null;
                    }
                    else
                    {
                        try
                        {
                            var process = Process.GetProcessById(args.ProcessId);
                            var newDisplay = new Display(process.MainWindowHandle);
                            var newDisplayDeviceName = newDisplay.GetDisplayDeviceName();

                            if (_displayDeviceName != newDisplayDeviceName)
                            {
                                _refreshRateBuffer.Clear();
                                _displayDeviceName = newDisplayDeviceName;
                            }

                            _display = newDisplay;
                            _refreshRateCurrentDisplay = _display.GetDisplayRefreshRate();
                        }
                        catch
                        {
                            if (_display != null)
                            {
                                _refreshRateBuffer.Clear();
                            }

                            _display = null;
                            _refreshRateCurrentDisplay = 0;
                            _displayDeviceName = null;
                        }
                    }
                }
            });
    }

    /// <summary>
    /// Gets the device identifier.
    /// </summary>
    public abstract string DeviceId { get; }

    /// <summary>
    /// Gets a value indicating whether the GPU is a discrete GPU.
    /// </summary>
    public bool IsDiscreteGpu { get; set; } = true;

    /// <summary>
    /// Sets the filter string used to identify the correct process memory performance counter instance for the current process. This is necessary in cases where multiple instances exist for the same process, which can happen with certain GPU drivers or configurations. The filter string should be a unique substring of the instance name that corresponds to the current process. If multiple instances match the filter, the one with the highest dedicated memory usage will be selected. If no instances match, the process memory sensors will not report values until a matching instance is found.
    /// </summary>
    /// <param name="processMemoryInstanceFilter"></param>
    protected void SetProcessMemoryInstanceFilter(string processMemoryInstanceFilter)
    {
        if (string.Equals(_processMemoryInstanceFilter, processMemoryInstanceFilter, StringComparison.OrdinalIgnoreCase))
            return;

        lock (_performanceCounterLock)
        {
            _processMemoryInstanceFilter = processMemoryInstanceFilter;
            DisposeProcessMemoryCounters();
            _counterProcessId = 0;
        }
    }

    /// <summary>
    /// Updates the process memory sensors with the latest values from the performance counters. This method should be called periodically, for example during the hardware update cycle, to ensure that the sensor values are up to date. If the performance counters are not available or an error occurs while reading them, the sensor values will be set to zero and the counters will be disposed to prevent further errors until a new process change is detected or the counters can be successfully reinitialized.
    /// </summary>
    protected void UpdateProcessMemorySensors()
    {
        if (!_enableProcessMemorySensors || _processMemoryUsageDedicated == null || _processMemoryUsageShared == null)
            return;

        if (_currentProcessId != 0)
        {
            EnsureProcessMemoryCounters(_currentProcessId, false);
        }

        lock (_performanceCounterLock)
        {
            try
            {
                _processMemoryUsageDedicated.Value = _dedicatedVramUsageProcessPerformCounter?.NextValue() / VRAM_USAGE_SCALE ?? 0f;
                _processMemoryUsageShared.Value = _sharedVramUsageProcessPerformCounter?.NextValue() / VRAM_USAGE_SCALE ?? 0f;
            }
            catch
            {
                DisposeProcessMemoryCounters();
                _counterProcessId = 0;
                _processMemoryUsageDedicated.Value = 0f;
                _processMemoryUsageShared.Value = 0f;
            }
        }
    }

    /// <summary>
    /// Closes the hardware and disposes of all resources, including the process subscription and performance counters. This method should be called when the hardware is no longer needed to ensure that all resources are properly released. If any errors occur during disposal, they will be caught and ignored to prevent exceptions from being thrown during the closing process.
    /// </summary>
    public override void Close()
    {
        try
        {
            _processSubscription?.Dispose();
        }
        catch
        {
            // Ignored.
        }

        lock (_performanceCounterLock)
        {
            DisposeProcessMemoryCounters();
        }

        base.Close();
    }

    private void InitializeProcessMemorySensors()
    {
        if (!_enableProcessMemorySensors)
            return;

        try
        {
            if (!PerformanceCounterCategory.Exists(GPU_PROCESS_MEMORY_CATEGORY_NAME))
                return;

            _gpuProcessMemoryCategory = new PerformanceCounterCategory(GPU_PROCESS_MEMORY_CATEGORY_NAME);

            _processMemoryUsageDedicated = new Sensor("GPU Memory Dedicated Game", PROCESS_MEMORY_DEDICATED_SENSOR_INDEX, SensorType.Data, this, _settings)
            {
                PresentationSortKey = "99_0",
                Value = 0f
            };
            _processMemoryUsageShared = new Sensor("GPU Memory Shared Game", PROCESS_MEMORY_SHARED_SENSOR_INDEX, SensorType.Data, this, _settings)
            {
                PresentationSortKey = "99_1",
                Value = 0f
            };

            ActivateSensor(_processMemoryUsageDedicated);
            ActivateSensor(_processMemoryUsageShared);
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, "Error while creating GPU process memory sensors.");
        }
    }

    private void EnsureProcessMemoryCounters(int processId, bool forceRefresh)
    {
        if (!_enableProcessMemorySensors || _gpuProcessMemoryCategory == null)
            return;

        lock (_performanceCounterLock)
        {
            bool countersMissing = _dedicatedVramUsageProcessPerformCounter == null || _sharedVramUsageProcessPerformCounter == null;

            if (!forceRefresh && !countersMissing && _counterProcessId == processId)
                return;

            DisposeProcessMemoryCounters();
            _counterProcessId = 0;

            if (processId == 0)
                return;

            try
            {
                string idString = $"pid_{processId}_luid";
                string[] instances = [.. _gpuProcessMemoryCategory
                    .GetInstanceNames()
                    .Where(instance => !string.IsNullOrWhiteSpace(instance) &&
                        instance.IndexOf(idString, StringComparison.OrdinalIgnoreCase) >= 0)];

                if (instances.Length == 0)
                    return;

                string instanceName = ResolveProcessMemoryInstance(instances);
                if (string.IsNullOrWhiteSpace(instanceName))
                    return;

                _dedicatedVramUsageProcessPerformCounter = new PerformanceCounter(
                    GPU_PROCESS_MEMORY_CATEGORY_NAME,
                    GPU_PROCESS_LOCAL_USAGE_COUNTER_NAME,
                    instanceName,
                    true);

                _sharedVramUsageProcessPerformCounter = new PerformanceCounter(
                    GPU_PROCESS_MEMORY_CATEGORY_NAME,
                    GPU_PROCESS_SHARED_USAGE_COUNTER_NAME,
                    instanceName,
                    true);

                _counterProcessId = processId;
            }
            catch
            {
                DisposeProcessMemoryCounters();
                _counterProcessId = 0;
            }
        }
    }

    private string ResolveProcessMemoryInstance(string[] instances)
    {
        if (!string.IsNullOrWhiteSpace(_processMemoryInstanceFilter))
        {
            string[] filteredInstances = instances
                .Where(instance => instance.IndexOf(_processMemoryInstanceFilter, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            if (filteredInstances.Length == 1)
                return filteredInstances[0];

            if (filteredInstances.Length > 1)
                return GetMaximumDedicatedUsageInstance(filteredInstances);

            return null;
        }

        if (instances.Length == 1)
            return instances[0];

        return null;
    }

    private static string GetMaximumDedicatedUsageInstance(string[] instances)
    {
        string maxInstance = null;
        float maxUsage = float.MinValue;

        foreach (string instance in instances)
        {
            try
            {
                using var counter = new PerformanceCounter(
                    GPU_PROCESS_MEMORY_CATEGORY_NAME,
                    GPU_PROCESS_LOCAL_USAGE_COUNTER_NAME,
                    instance,
                    true);

                float currentUsage = counter.NextValue();
                if (currentUsage >= maxUsage)
                {
                    maxUsage = currentUsage;
                    maxInstance = instance;
                }
            }
            catch
            {
                // Ignore invalid process-memory instances and keep searching.
            }
        }

        return maxInstance;
    }

    private void DisposeProcessMemoryCounters()
    {
        try
        {
            _dedicatedVramUsageProcessPerformCounter?.Dispose();
        }
        catch
        {
            // Ignored.
        }

        try
        {
            _sharedVramUsageProcessPerformCounter?.Dispose();
        }
        catch
        {
            // Ignored.
        }

        _dedicatedVramUsageProcessPerformCounter = null;
        _sharedVramUsageProcessPerformCounter = null;
    }
}
