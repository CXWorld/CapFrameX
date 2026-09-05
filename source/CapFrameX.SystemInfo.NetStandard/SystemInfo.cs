using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Sensor;
using Microsoft.Extensions.Logging;
using Mixaill.HwInfo.D3D;
using Mixaill.HwInfo.SetupApi;
using Mixaill.HwInfo.SetupApi.Defines;
using Mixaill.HwInfo.Vulkan;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CapFrameX.SystemInfo.NetStandard
{
    public class SystemInfo : ISystemInfo
    {
        private static readonly long ONE_GIB = 1073741824;

        private readonly ISensorService _sensorService;
        private readonly ILogger<SystemInfo> _logger;
        private readonly double _processorCount = Environment.ProcessorCount;

        /// <summary>
        /// Mainboard, BIOS, memory modules, core count and OS caption cannot change while the
        /// application runs, but they are asked for over and over - the info tab, the overlay's
        /// static rows, the custom hardware descriptions, every saved capture and the MCP tools.
        /// Collecting them costs a WMI round trip each, so the whole set is gathered once, with
        /// the independent providers queried concurrently.
        /// </summary>
        private readonly Lazy<StaticHardwareInfo> _staticHardwareInfo;

        private DateTime _lastTime;
        private TimeSpan _lastTotalProcessorTime;
        private DateTime _curTime;
        private TimeSpan _curTotalProcessorTime;
        private Process _cxProcess;

        public ESystemInfoTertiaryStatus ResizableBarHardwareStatus { get; private set; } = ESystemInfoTertiaryStatus.Error;

        public ESystemInfoTertiaryStatus ResizableBarD3DStatus { get; private set; } = ESystemInfoTertiaryStatus.Error;

        public ESystemInfoTertiaryStatus ResizableBarVulkanStatus { get; private set; } = ESystemInfoTertiaryStatus.Error;

        public ESystemInfoTertiaryStatus GameModeStatus { get; private set; } = ESystemInfoTertiaryStatus.Error;

        public ESystemInfoTertiaryStatus HardwareAcceleratedGPUSchedulingStatus { get; private set; } = ESystemInfoTertiaryStatus.Error;

        public ulong PciBarSizeD3D { get; private set; } = 0UL;

        public ulong PciBarSizeHardware { get; private set; } = 0UL;

        public ulong PciBarSizeVulkan { get; private set; } = 0UL;


        public SystemInfo(ISensorService sensorService,
                          ILogger<SystemInfo> logger)
        {
            _sensorService = sensorService;
            _logger = logger;

            _staticHardwareInfo = new Lazy<StaticHardwareInfo>(
                () => StaticHardwareInfo.Collect(_logger), LazyThreadSafetyMode.ExecutionAndPublication);

            _cxProcess = Process.GetProcessesByName("CapFrameX").FirstOrDefault();
            _lastTime = DateTime.UtcNow;
            _lastTotalProcessorTime = _cxProcess == null ? new TimeSpan() : _cxProcess.TotalProcessorTime;
        }

        #region System Info

        /// <summary>
        /// The four probes address unrelated subsystems (setup API, D3D KMT, the Vulkan loader and
        /// the registry) and write disjoint properties, so they run concurrently - creating the
        /// Vulkan instance alone costs more than the other three together.
        /// </summary>
        public void SetSystemInfosStatus()
        {
            Task.WaitAll(
                Task.Run(() => SetSystemInfoSetupApi()),
                Task.Run(() => SetSystemInfoD3D()),
                Task.Run(() => SetSystemInfoVulkan()),
                Task.Run(() => SetSystemInfoRegistry()));
        }

        private void SetSystemInfoSetupApi()
        {
            //PCI Resizable BAR HW support
            try
            {
                using (var displayDevices = new DeviceInfoSet(DeviceClassGuid.Display, _logger))
                {
                    PciBarSizeHardware = displayDevices.Devices.Max(x => (x as DeviceInfoPci)?.DeviceResourceMemory.Max(y => y.AddressEnd - y.AddressStart)) ?? 0UL;
                    var largeMemoryStatus = displayDevices.Devices.Any(x => (x as DeviceInfoPci)?.Pci_LargeMemory == true);
                    ResizableBarHardwareStatus = largeMemoryStatus
                        ? ESystemInfoTertiaryStatus.Enabled : ESystemInfoTertiaryStatus.Disabled;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting Resizable Bar hardware status.");
            }
        }

        private void SetSystemInfoD3D()
        {
            try
            {
                var kmt = new Kmt(_logger);
                var adapters = kmt.GetAdapters();

                //Hardware-Accelerated GPU Scheduling
                if (adapters.Any(x => x.WddmCapabilities_27.HwSchEnabled))
                {
                    HardwareAcceleratedGPUSchedulingStatus = ESystemInfoTertiaryStatus.Enabled;
                }
                else if (adapters.Any(x => x.WddmCapabilities_27.HwSchSupported))
                {
                    HardwareAcceleratedGPUSchedulingStatus = ESystemInfoTertiaryStatus.Disabled;
                }

                //Host Visible Memory
                PciBarSizeD3D = adapters.Max(x => x.HostVisibleMemory);
                ResizableBarD3DStatus = adapters.Any(dev => dev.ResizableBarInUse)
                    ? ESystemInfoTertiaryStatus.Enabled : ESystemInfoTertiaryStatus.Disabled;

                adapters.ForEach(x => x.Dispose());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting D3D KMT info");
            }
        }

        private void SetSystemInfoVulkan()
        {
            //PCI Resizable BAR SW support
            try
            {
                using (var vk = new Vulkan(_logger))
                {
                    var devices = vk.GetPhysicalDevices();
                    PciBarSizeVulkan = devices.Max(x => x.DeviceHostVisibleMemory);
                    ResizableBarVulkanStatus = devices.Any(dev => dev.DeviceResizableBarInUse)
                        ? ESystemInfoTertiaryStatus.Enabled : ESystemInfoTertiaryStatus.Disabled;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting Resizable Bar software status.");
            }
        }

        private void SetSystemInfoRegistry()
        {

            //Windows Game Mode
            try
            {
                const string gameBar = "SOFTWARE\\Microsoft\\GameBar";
                using (RegistryKey gameBarKey = Registry.CurrentUser.OpenSubKey(gameBar, true))
                {
                    var val = gameBarKey?.GetValue("AutoGameModeEnabled");
                    if (val != null)
                    {
                        bool valConverted = Convert.ToBoolean(val);
                        GameModeStatus = valConverted ? ESystemInfoTertiaryStatus.Enabled : ESystemInfoTertiaryStatus.Disabled;
                    }
                    else
                    {
                        // default enabled
                        GameModeStatus = ESystemInfoTertiaryStatus.Enabled;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting Windows Game Mode status.");
            }
        }

        #endregion

        /// <summary>
        /// The computer name, i.e. what Windows shows as "Device name" in Settings → System → About.
        /// </summary>
        public string GetDeviceName()
        {
            try
            {
                return Environment.MachineName;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting device name.");
                return string.Empty;
            }
        }

        public string GetProcessorName() => _sensorService.GetCpuName();

        public string GetGraphicCardName()
        {
            var name = _sensorService.GetGpuName();
            return name == "Unknown" ?
                GetGraphicsCardNameFromWMI() : name;
        }

        public string GetOSVersion() => _staticHardwareInfo.Value.OsVersion;

        public string GetMotherboardName() => _staticHardwareInfo.Value.MainboardName;

        public string GetMotherboardManufacturerBrand() => _staticHardwareInfo.Value.MainboardBrand;

        public string GetBiosVersion() => _staticHardwareInfo.Value.BiosVersion;

        /// <summary>
        /// Many boards report the module vendor as its raw JEDEC manufacturer ID
        /// instead of a name. Best-effort map of the codes common on consumer DDR4/DDR5.
        /// </summary>
        private static readonly Dictionary<string, string> JedecManufacturerIds =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "802C", "Micron" },
                { "2C00", "Micron" },
                { "80AD", "SK Hynix" },
                { "AD00", "SK Hynix" },
                { "80CE", "Samsung" },
                { "CE00", "Samsung" },
                { "859B", "Crucial" },
                { "029E", "Corsair" },
                { "04CB", "ADATA" },
                { "04CD", "G.SKILL" },
                { "04EF", "Team Group" },
                { "0198", "Kingston" },
                { "7F98", "Kingston" },
            };

        public string GetSystemRAMManufacturer() => _staticHardwareInfo.Value.RamManufacturer;

        private static bool LooksLikeJedecId(string value)
        {
            if (value.Length != 4)
                return false;

            bool hasDigit = false;
            foreach (var c in value)
            {
                if (!Uri.IsHexDigit(c))
                    return false;
                hasDigit |= char.IsDigit(c);
            }

            // All-letter strings ("ADATA") are names, not IDs.
            return hasDigit;
        }

        public string GetProcessorCoreCountInfo() => _staticHardwareInfo.Value.ProcessorCoreCountInfo;

        public string GetSystemRAMInfoName() => _staticHardwareInfo.Value.RamName;

        private static string GetGraphicsCardNameFromWMI()
        {
            string propertyDataValue = string.Empty;

            try
            {
                using (var searcher = new ManagementObjectSearcher("select DeviceName from Win32_DisplayConfiguration"))
                {
                    foreach (ManagementBaseObject managementBaseObject in searcher.Get())
                    {
                        propertyDataValue = managementBaseObject["DeviceName"] as string;
                    }
                }
            }
            catch { propertyDataValue = string.Empty; }

            //DeviceName
            return propertyDataValue;
        }

        /// <summary>
        /// One-shot snapshot of the hardware identification WMI can report. Each provider is
        /// queried exactly once and only for the columns actually used - <c>select *</c>
        /// materializes every property of the class, which is what made the operating system
        /// query an order of magnitude more expensive than the rest together.
        /// </summary>
        private sealed class StaticHardwareInfo
        {
            public string OsVersion { get; private set; } = string.Empty;

            public string MainboardName { get; private set; } = string.Empty;

            public string MainboardBrand { get; private set; } = string.Empty;

            public string BiosVersion { get; private set; } = string.Empty;

            public string RamName { get; private set; } = string.Empty;

            public string RamManufacturer { get; private set; } = string.Empty;

            public string ProcessorCoreCountInfo { get; private set; } = string.Empty;

            public static StaticHardwareInfo Collect(ILogger logger)
            {
                var info = new StaticHardwareInfo();

                // The WMI round trip dominates every one of these, and the providers are
                // unrelated, so the four queries overlap instead of adding up. The caller takes
                // one share itself: it would otherwise sit blocked while occupying a pool thread,
                // which on a low core count is exactly the thread the others are waiting for.
                try
                {
                    var mainboard = Task.Run(() => info.ReadMainboardAndBios(logger));
                    var memory = Task.Run(() => info.ReadMemory(logger));
                    var processor = Task.Run(() => info.ReadProcessor(logger));

                    info.ReadOperatingSystem(logger);

                    Task.WaitAll(mainboard, memory, processor);
                }
                catch (Exception ex)
                {
                    // The readers handle their own failures, so reaching here means WMI itself is
                    // unusable. The snapshot is cached for the process lifetime, so it must be
                    // returned half-filled rather than rethrown on every later call.
                    logger?.LogError(ex, "Error while collecting static hardware information.");
                }

                return info;
            }

            private void ReadOperatingSystem(ILogger logger)
            {
                string caption = string.Empty;
                string buildNumber = string.Empty;

                try
                {
                    using (var searcher = new ManagementObjectSearcher(
                        "select Caption, BuildNumber from Win32_OperatingSystem"))
                    {
                        foreach (ManagementBaseObject managementBaseObject in searcher.Get())
                        {
                            caption = managementBaseObject["Caption"] as string;
                            buildNumber = managementBaseObject["BuildNumber"] as string;
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while getting OS version.");
                    caption = "Windows OS";
                }

                OsVersion = $"{caption} Build {buildNumber}";
            }

            private void ReadMainboardAndBios(ILogger logger)
            {
                string manufacturer = string.Empty;
                string product = string.Empty;

                try
                {
                    using (var searcher = new ManagementObjectSearcher(
                        "select Manufacturer, Product from Win32_BaseBoard"))
                    {
                        foreach (ManagementBaseObject managementBaseObject in searcher.Get())
                        {
                            manufacturer = managementBaseObject["Manufacturer"] as string;
                            product = managementBaseObject["Product"] as string;

                            if (MainboardBrand.Length == 0)
                                MainboardBrand = MainboardNameShortener.ToBrand(manufacturer);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while getting mainboard information.");
                    manufacturer = string.Empty;
                    product = string.Empty;
                }

                //Manufacturer + Product, shortened to the brand people actually use
                MainboardName = MainboardNameShortener.Shorten(manufacturer, product);

                // Same firmware table behind it, so it shares the mainboard's query slot.
                try
                {
                    using (var searcher = new ManagementObjectSearcher(
                        "select SMBIOSBIOSVersion from Win32_BIOS"))
                    {
                        foreach (ManagementBaseObject managementBaseObject in searcher.Get())
                        {
                            var version = managementBaseObject["SMBIOSBIOSVersion"] as string;

                            if (!string.IsNullOrWhiteSpace(version))
                            {
                                BiosVersion = version.Trim();
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while getting BIOS version.");
                }
            }

            private void ReadMemory(ILogger logger)
            {
                var moduleSetting = new Dictionary<long, int>();
                var manufacturers = new List<string>();
                string speed = "unknown";

                try
                {
                    using (var searcher = new ManagementObjectSearcher(
                        "select Capacity, ConfiguredClockSpeed, Manufacturer from Win32_PhysicalMemory"))
                    {
                        foreach (ManagementBaseObject managementBaseObject in searcher.Get())
                        {
                            var configuredClockSpeed = managementBaseObject["ConfiguredClockSpeed"];
                            if (configuredClockSpeed != null)
                                speed = configuredClockSpeed.ToString();

                            var capacity = managementBaseObject["Capacity"];
                            if (capacity != null)
                            {
                                var currentCapacity = Convert.ToInt64(capacity);
                                if (moduleSetting.ContainsKey(currentCapacity))
                                    moduleSetting[currentCapacity]++;
                                else
                                    moduleSetting.Add(currentCapacity, 1);
                            }

                            AddMemoryManufacturer(managementBaseObject["Manufacturer"] as string, manufacturers);
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while getting memory information.");
                    speed = "unknown";
                    moduleSetting.Clear();
                    moduleSetting.Add(0, 1);
                }

                RamManufacturer = string.Join(" / ", manufacturers);

                if (!moduleSetting.Any())
                    moduleSetting.Add(0, 0);

                //RAM size + data rate
                // example: 48GB (4x4GB+4x8GB)
                var infoString = string.Empty;
                long wholeCapacity = 0;

                foreach (var item in moduleSetting)
                {
                    wholeCapacity += item.Value * item.Key;
                    infoString += $"{item.Value}x{item.Key / ONE_GIB}GB+";
                }

                RamName = $"{wholeCapacity / ONE_GIB}GB ({infoString.Remove(infoString.Length - 1)}) {speed}MT/s";
            }

            private static void AddMemoryManufacturer(string rawManufacturer, List<string> manufacturers)
            {
                var raw = rawManufacturer?.Trim() ?? string.Empty;

                string brand;
                if (JedecManufacturerIds.TryGetValue(raw, out var mapped))
                    brand = mapped;
                else if (LooksLikeJedecId(raw))
                    brand = string.Empty; // unmapped raw ID carries no display value
                else
                    // ToBrand also drops the placeholder strings modules ship with
                    // ("Unknown", "To be filled by O.E.M.", ...).
                    brand = MainboardNameShortener.ToBrand(raw);

                if (brand.Length > 0 && !manufacturers.Contains(brand))
                    manufacturers.Add(brand);
            }

            private void ReadProcessor(ILogger logger)
            {
                try
                {
                    uint cores = 0;
                    uint threads = 0;

                    using (var searcher = new ManagementObjectSearcher(
                        "select NumberOfCores, NumberOfLogicalProcessors from Win32_Processor"))
                    {
                        foreach (ManagementBaseObject managementBaseObject in searcher.Get())
                        {
                            cores += Convert.ToUInt32(managementBaseObject["NumberOfCores"]);
                            threads += Convert.ToUInt32(managementBaseObject["NumberOfLogicalProcessors"]);
                        }
                    }

                    if (cores > 0 && threads > 0)
                        ProcessorCoreCountInfo = $"{cores} Cores / {threads} Threads";
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error while getting processor core count.");
                }
            }
        }

        public double GetCapFrameXAppCpuUsage()
        {
            double cpuUsage = 0;

            if (_cxProcess != null)
            {
                _curTime = DateTime.UtcNow;

                try
                {
                    _curTotalProcessorTime = _cxProcess.TotalProcessorTime;
                }
                catch { }

                cpuUsage = (_curTotalProcessorTime.TotalMilliseconds - _lastTotalProcessorTime.TotalMilliseconds)
                    / _curTime.Subtract(_lastTime).TotalMilliseconds / _processorCount;

                _lastTime = _curTime;
                _lastTotalProcessorTime = _curTotalProcessorTime;
            }

            return cpuUsage * 100d;
        }
    }
}
