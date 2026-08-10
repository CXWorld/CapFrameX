using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Sensor;
using CapFrameX.Monitoring.Contracts;
using CapFrameX.ViewModel.SubModels;
using Microsoft.Extensions.Logging;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace CapFrameX.ViewModel
{
    /// <summary>
    /// System information landing page: static hardware identification (CPU, GPU,
    /// mainboard, memory) with vendor badges plus live base telemetry (load, power,
    /// temperature, clocks). While this view is active it sets
    /// <see cref="ISensorConfig.EvaluateAllSensors"/> so the sensor snapshot stream
    /// keeps delivering values even when neither overlay nor logging is running.
    /// </summary>
    public class InfoViewModel : BindableBase, INavigationAware
    {
        private const string NoValue = "–";

        private readonly ISensorService _sensorService;
        private readonly ISensorConfig _sensorConfig;
        private readonly ISystemInfo _systemInfo;
        private readonly IAppConfiguration _appConfiguration;
        private readonly ILogger<InfoViewModel> _logger;

        private string _cpuName = "Detecting...";
        private string _cpuDetails = string.Empty;
        private VendorBadge _cpuVendorBadge;
        private string _gpuName = "Detecting...";
        private string _gpuDetails = string.Empty;
        private VendorBadge _gpuVendorBadge;
        private string _mainboardName = "Detecting...";
        private string _mainboardDetails = string.Empty;
        private VendorBadge _mainboardVendorBadge;
        private string _ramName = "Detecting...";
        private string _ramDetails = string.Empty;
        private VendorBadge _ramVendorBadge;
        private string _osVersion = string.Empty;

        private double _cpuLoad;
        private string _cpuLoadText = NoValue;
        private string _cpuPowerText = NoValue;
        private string _cpuTempText = NoValue;
        private string _cpuClockText = NoValue;

        private double _gpuLoad;
        private string _gpuLoadText = NoValue;
        private string _gpuPowerText = NoValue;
        private string _gpuTempText = NoValue;
        private string _gpuClockText = NoValue;
        private string _gpuVramText = NoValue;

        private double _ramLoad;
        private string _ramLoadText = NoValue;
        private string _ramUsedText = NoValue;
        private string _ramAvailableText = NoValue;

        private string _resizableBarStatusColor = StatusGray;
        private string _hagsStatusColor = StatusGray;
        private string _gameModeStatusColor = StatusGray;

        private const string StatusGreen = "#4CAF50";
        private const string StatusOrange = "#FF9800";
        private const string StatusGray = "#757575";

        public string CpuName { get => _cpuName; set => SetProperty(ref _cpuName, value); }
        public string CpuDetails { get => _cpuDetails; set => SetProperty(ref _cpuDetails, value); }
        public VendorBadge CpuVendorBadge { get => _cpuVendorBadge; set => SetProperty(ref _cpuVendorBadge, value); }
        public string GpuName { get => _gpuName; set => SetProperty(ref _gpuName, value); }
        public string GpuDetails { get => _gpuDetails; set => SetProperty(ref _gpuDetails, value); }
        public VendorBadge GpuVendorBadge { get => _gpuVendorBadge; set => SetProperty(ref _gpuVendorBadge, value); }
        public string MainboardName { get => _mainboardName; set => SetProperty(ref _mainboardName, value); }
        public string MainboardDetails { get => _mainboardDetails; set => SetProperty(ref _mainboardDetails, value); }
        public VendorBadge MainboardVendorBadge { get => _mainboardVendorBadge; set => SetProperty(ref _mainboardVendorBadge, value); }
        public string RamName { get => _ramName; set => SetProperty(ref _ramName, value); }
        public string RamDetails { get => _ramDetails; set => SetProperty(ref _ramDetails, value); }
        public VendorBadge RamVendorBadge { get => _ramVendorBadge; set => SetProperty(ref _ramVendorBadge, value); }
        public string OsVersion { get => _osVersion; set => SetProperty(ref _osVersion, value); }

        public double CpuLoad { get => _cpuLoad; set => SetProperty(ref _cpuLoad, value); }
        public string CpuLoadText { get => _cpuLoadText; set => SetProperty(ref _cpuLoadText, value); }
        public string CpuPowerText { get => _cpuPowerText; set => SetProperty(ref _cpuPowerText, value); }
        public string CpuTempText { get => _cpuTempText; set => SetProperty(ref _cpuTempText, value); }
        public string CpuClockText { get => _cpuClockText; set => SetProperty(ref _cpuClockText, value); }

        public double GpuLoad { get => _gpuLoad; set => SetProperty(ref _gpuLoad, value); }
        public string GpuLoadText { get => _gpuLoadText; set => SetProperty(ref _gpuLoadText, value); }
        public string GpuPowerText { get => _gpuPowerText; set => SetProperty(ref _gpuPowerText, value); }
        public string GpuTempText { get => _gpuTempText; set => SetProperty(ref _gpuTempText, value); }
        public string GpuClockText { get => _gpuClockText; set => SetProperty(ref _gpuClockText, value); }
        public string GpuVramText { get => _gpuVramText; set => SetProperty(ref _gpuVramText, value); }

        public double RamLoad { get => _ramLoad; set => SetProperty(ref _ramLoad, value); }
        public string RamLoadText { get => _ramLoadText; set => SetProperty(ref _ramLoadText, value); }
        public string RamUsedText { get => _ramUsedText; set => SetProperty(ref _ramUsedText, value); }
        public string RamAvailableText { get => _ramAvailableText; set => SetProperty(ref _ramAvailableText, value); }

        public string ResizableBarStatusColor { get => _resizableBarStatusColor; set => SetProperty(ref _resizableBarStatusColor, value); }
        public string HagsStatusColor { get => _hagsStatusColor; set => SetProperty(ref _hagsStatusColor, value); }
        public string GameModeStatusColor { get => _gameModeStatusColor; set => SetProperty(ref _gameModeStatusColor, value); }

        public InfoViewModel(ISensorService sensorService,
                             ISensorConfig sensorConfig,
                             ISystemInfo systemInfo,
                             IAppConfiguration appConfiguration,
                             ILogger<InfoViewModel> logger)
        {
            _sensorService = sensorService;
            _sensorConfig = sensorConfig;
            _systemInfo = systemInfo;
            _appConfiguration = appConfiguration;
            _logger = logger;

            // This view is the startup page and the initial activation does not raise
            // OnNavigatedTo, so telemetry has to be armed here.
            _sensorConfig.EvaluateAllSensors = true;

            _ = Task.Run(InitializeStaticInfoAsync);

            _sensorService.SensorSnapshotStream
                .Sample(TimeSpan.FromMilliseconds(500))
                .ObserveOnDispatcher()
                .Subscribe(snapshot => UpdateLiveMetrics(snapshot.Item2));
        }

        private async Task InitializeStaticInfoAsync()
        {
            try
            {
                await _sensorService.SensorServiceCompletionSource.Task;

                CpuName = _systemInfo.GetProcessorName();
                CpuVendorBadge = VendorBadge.FromCpuVendor(_sensorService.GetCpuVendor());
                CpuDetails = _systemInfo.GetProcessorCoreCountInfo();

                UpdateGpuInfo();

                // Keep the GPU block in sync with the graphics adapter selection
                // (auto mode: discrete GPU, otherwise the configured adapter).
                _appConfiguration.OnValueChanged
                    .Where(change => change.key == nameof(IAppConfiguration.GraphicsAdapter))
                    .Subscribe(_ => UpdateGpuInfo());

                MainboardName = _systemInfo.GetMotherboardName();
                MainboardVendorBadge = VendorBadge.FromVendorName(_systemInfo.GetMotherboardManufacturerBrand());
                var biosVersion = _systemInfo.GetBiosVersion();
                MainboardDetails = IsUsable(biosVersion) ? $"BIOS {biosVersion}" : string.Empty;

                RamName = _systemInfo.GetSystemRAMInfoName();
                var ramManufacturer = _systemInfo.GetSystemRAMManufacturer();
                RamVendorBadge = VendorBadge.FromVendorName(ramManufacturer);
                RamDetails = ramManufacturer;

                OsVersion = _systemInfo.GetOSVersion();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while collecting system information.");
            }
        }

        /// <summary>
        /// Resolves the GPU identity through the sensor service, which applies the same
        /// adapter selection as the GPU sensor filter: auto mode picks the discrete GPU
        /// on iGPU/dGPU systems, otherwise the configured adapter is used. Intentionally
        /// bypasses <see cref="ISystemInfo.GetGraphicCardName"/> — its WMI fallback
        /// reports the display-driving adapter, which is the iGPU on hybrid systems.
        /// </summary>
        private void UpdateGpuInfo()
        {
            try
            {
                GpuName = _sensorService.GetGpuName();
                GpuVendorBadge = VendorBadge.FromGpuVendor(_sensorService.GetGpuVendor());
                var driverVersion = _sensorService.GetGpuDriverVersion();
                GpuDetails = IsUsable(driverVersion) ? $"Driver {driverVersion}" : string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating GPU information.");
            }
        }

        private static bool IsUsable(string value)
            => !string.IsNullOrWhiteSpace(value) && value != "Unknown";

        private void UpdateLiveMetrics(Dictionary<ISensorEntry, float> snapshot)
        {
            try
            {
                float? cpuLoad = null, cpuPower = null, cpuTemp = null, cpuMaxClock = null, cpuCoreClockMax = null;
                float? gpuLoad = null, gpuPower = null, gpuTemp = null, gpuClock = null;
                float? vramUsedGb = null, vramUsedMb = null, vramTotalGb = null, vramTotalMb = null;
                float? d3dDedicatedUsed = null, d3dSharedUsed = null, d3dSharedTotal = null;
                float? ramLoad = null, ramUsed = null, ramAvailable = null;

                foreach (var pair in snapshot)
                {
                    var entry = pair.Key;
                    var value = pair.Value;
                    var name = entry.Name ?? string.Empty;

                    bool isCpu = entry.HardwareType == "Cpu";
                    bool isGpu = entry.HardwareType != null
                        && entry.HardwareType.StartsWith("Gpu", StringComparison.Ordinal);
                    bool isMemory = entry.HardwareType == "Memory";

                    switch (entry.SensorType)
                    {
                        case "Load":
                            if (isCpu && name == "CPU Total")
                                cpuLoad = value;
                            else if (isGpu && name == "GPU Core")
                                gpuLoad = value;
                            else if (isMemory && name == "RAM Usage")
                                ramLoad = value;
                            break;

                        case "Power":
                            if (isCpu && name.StartsWith("CPU Package", StringComparison.Ordinal))
                                cpuPower = value;
                            else if (isGpu && IsGpuBoardPowerSensor(name))
                                gpuPower = gpuPower.HasValue ? Math.Max(gpuPower.Value, value) : value;
                            break;

                        case "Temperature":
                            if (isCpu && name.StartsWith("CPU Package", StringComparison.Ordinal))
                                cpuTemp = cpuTemp ?? value;
                            else if (isGpu && name == "GPU Core")
                                gpuTemp = value;
                            break;

                        case "Clock":
                            if (isCpu)
                            {
                                // Max core clock is "CPU Max" (Intel and AMD alike);
                                // "CPU Max Clock" covers plugin-provided sensors.
                                if (name == "CPU Max" || name == "CPU Max Clock")
                                    cpuMaxClock = value;
                                // Per-core clocks are named "Core #1 P1" / "Core P1" - no
                                // "CPU" prefix. Skip the effective/thread clock variants.
                                else if (name.StartsWith("Core", StringComparison.Ordinal)
                                    && name.IndexOf("Effective", StringComparison.Ordinal) < 0)
                                    cpuCoreClockMax = cpuCoreClockMax.HasValue ? Math.Max(cpuCoreClockMax.Value, value) : value;
                            }
                            else if (isGpu && name == "GPU Core")
                                gpuClock = value;
                            break;

                        case "Data":
                            if (isGpu && name == "GPU Memory Dedicated")
                                vramUsedGb = value;
                            else if (isGpu && name == "GPU Memory Total")
                                vramTotalGb = value;
                            // Intel D3D GPU naming (iGPUs and IGCL-less discrete cards)
                            else if (isGpu && name == "D3D Dedicated Memory Used")
                                d3dDedicatedUsed = value;
                            else if (isGpu && name == "D3D Shared Memory Used")
                                d3dSharedUsed = value;
                            else if (isGpu && name == "D3D Shared Memory Total")
                                d3dSharedTotal = value;
                            else if (isMemory && name == "RAM Used")
                                ramUsed = value;
                            else if (isMemory && name == "RAM Available")
                                ramAvailable = value;
                            break;

                        case "SmallData":
                            // Legacy MB-based variants, kept as fallback.
                            if (isGpu && name == "GPU Memory Dedicated")
                                vramUsedMb = value;
                            else if (isGpu && name == "GPU Memory Total")
                                vramTotalMb = value;
                            break;
                    }
                }

                CpuLoad = cpuLoad ?? 0d;
                CpuLoadText = FormatValue(cpuLoad, "F0", "%");
                CpuPowerText = FormatValue(cpuPower, "F0", " W");
                CpuTempText = FormatValue(cpuTemp, "F0", " °C");
                CpuClockText = FormatValue(cpuMaxClock ?? cpuCoreClockMax, "F0", " MHz");

                GpuLoad = gpuLoad ?? 0d;
                GpuLoadText = FormatValue(gpuLoad, "F0", "%");
                GpuPowerText = FormatValue(gpuPower, "F0", " W");
                GpuTempText = FormatValue(gpuTemp, "F0", " °C");
                GpuClockText = FormatValue(gpuClock, "F0", " MHz");
                GpuVramText = FormatVram(vramUsedGb, vramUsedMb, vramTotalGb, vramTotalMb,
                    d3dDedicatedUsed, d3dSharedUsed, d3dSharedTotal);

                if (!ramLoad.HasValue && ramUsed.HasValue && ramAvailable.HasValue
                    && ramUsed.Value + ramAvailable.Value > 0f)
                {
                    ramLoad = 100f * ramUsed.Value / (ramUsed.Value + ramAvailable.Value);
                }

                RamLoad = ramLoad ?? 0d;
                RamLoadText = FormatValue(ramLoad, "F0", "%");
                RamUsedText = FormatValue(ramUsed, "F1", " GB");
                RamAvailableText = FormatValue(ramAvailable, "F1", " GB");

                UpdateSystemStatus();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating live system metrics.");
            }
        }

        private static bool IsGpuBoardPowerSensor(string name)
        {
            // Board power naming differs per vendor: "GPU Power" (NVIDIA),
            // "GPU Total"/"GPU TBP" (AMD), "GPU TBP"/"GPU TDP" (Intel).
            return (name == "GPU Power" || name == "GPU Total" || name == "GPU TBP" || name == "GPU TDP")
                && !name.Contains("Limit");
        }

        private static string FormatValue(float? value, string format, string unit)
        {
            return value.HasValue
                ? value.Value.ToString(format, CultureInfo.InvariantCulture) + unit
                : NoValue;
        }

        private static string FormatVram(float? usedGb, float? usedMb, float? totalGb, float? totalMb,
            float? d3dDedicatedUsed, float? d3dSharedUsed, float? d3dSharedTotal)
        {
            float? used = usedGb ?? (usedMb / 1024f);
            float? total = totalGb ?? (totalMb / 1024f);

            if (!used.HasValue)
            {
                // Intel D3D naming. A real discrete frame buffer holds gigabytes even at
                // the desktop; integrated GPUs carry at most a tiny dedicated carve-out
                // and their actual budget is shared system memory.
                if (d3dDedicatedUsed >= 1f)
                {
                    used = d3dDedicatedUsed;
                }
                else if (d3dSharedUsed.HasValue)
                {
                    used = d3dSharedUsed;
                    total = d3dSharedTotal;
                }
                else
                {
                    used = d3dDedicatedUsed;
                }
            }

            if (!used.HasValue)
                return NoValue;

            var usedText = used.Value.ToString("F1", CultureInfo.InvariantCulture);

            if (total.HasValue && total.Value > 0f)
            {
                var totalText = total.Value.ToString("F0", CultureInfo.InvariantCulture);
                return $"{usedText} / {totalText} GB";
            }

            return $"{usedText} GB";
        }

        private void UpdateSystemStatus()
        {
            ResizableBarStatusColor = GetStatusColor(_systemInfo.ResizableBarD3DStatus != ESystemInfoTertiaryStatus.Error
                ? _systemInfo.ResizableBarD3DStatus : _systemInfo.ResizableBarHardwareStatus);
            HagsStatusColor = GetStatusColor(_systemInfo.HardwareAcceleratedGPUSchedulingStatus);
            GameModeStatusColor = GetStatusColor(_systemInfo.GameModeStatus);
        }

        private static string GetStatusColor(ESystemInfoTertiaryStatus status)
        {
            switch (status)
            {
                case ESystemInfoTertiaryStatus.Enabled:
                    return StatusGreen;
                case ESystemInfoTertiaryStatus.Disabled:
                    return StatusOrange;
                default:
                    return StatusGray;
            }
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;

        public void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _sensorConfig.EvaluateAllSensors = false;
        }

        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            _sensorConfig.EvaluateAllSensors = true;
        }
    }
}
