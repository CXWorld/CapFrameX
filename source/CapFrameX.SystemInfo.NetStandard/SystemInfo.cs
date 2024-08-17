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

namespace CapFrameX.SystemInfo.NetStandard
{
    public class SystemInfo : ISystemInfo
    {
        private static readonly long ONE_GIB = 1073741824;

        private readonly ISensorService _sensorService;
        private readonly ILogger<SystemInfo> _logger;
        private readonly double _processorCount = Environment.ProcessorCount;

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

            _cxProcess = Process.GetProcessesByName("CapFrameX").FirstOrDefault();
            _lastTime = DateTime.UtcNow;
            _lastTotalProcessorTime = _cxProcess == null ? new TimeSpan() : _cxProcess.TotalProcessorTime;

            SetSystemInfosStatus();
        }

        #region System Info

        public void SetSystemInfosStatus()
        {
            SetSystemInfoSetupApi();
            SetSystemInfoD3D();
            SetSystemInfoVulkan();
            SetSystemInfoRegistry();
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
                    var val = gameBarKey.GetValue("AutoGameModeEnabled");
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

        public string GetProcessorName()
            => _sensorService.GetCpuName();

        public string GetGraphicCardName()
        {
            var name = _sensorService.GetGpuName();
            return name == "Unknown" ?
                GetGraphicsCardNameFromWMI() : name;
        }

        public string GetOSVersion()
        {
            string propertyDataValueCaption = string.Empty;
            const string propertyDataNameCaption = "Caption";
            string propertyDataValueBuildNumber = string.Empty;
            const string propertyDataNameBuildNumber = "BuildNumber";

            var win32DeviceClassName = "Win32_OperatingSystem";
            var query = string.Format("select * from {0}", win32DeviceClassName);

            try
            {
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    ManagementObjectCollection objectCollection = searcher.Get();

                    foreach (ManagementBaseObject managementBaseObject in objectCollection)
                    {
                        foreach (PropertyData propertyData in managementBaseObject.Properties)
                        {
                            if (propertyData.Name == propertyDataNameCaption)
                            {
                                propertyDataValueCaption = (string)propertyData.Value;
                            }

                            if (propertyData.Name == propertyDataNameBuildNumber)
                            {
                                propertyDataValueBuildNumber = (string)propertyData.Value;

                            }
                        }
                    }
                }
            }
            catch { propertyDataValueCaption = "Windows OS"; }

            return $"{propertyDataValueCaption} Build {propertyDataValueBuildNumber}";
        }

        public string GetMotherboardName()
        {
            string propertyDataValueManufacturer = string.Empty;
            const string propertyDataNameManufacturer = "Manufacturer";
            string propertyDataValueProduct = string.Empty;
            const string propertyDataNameProduct = "Product";

            var win32DeviceClassName = "Win32_BaseBoard";
            var query = string.Format("select * from {0}", win32DeviceClassName);

            try
            {
                //Manufacturer + Product
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    ManagementObjectCollection objectCollection = searcher.Get();

                    foreach (ManagementBaseObject managementBaseObject in objectCollection)
                    {
                        foreach (PropertyData propertyData in managementBaseObject.Properties)
                        {
                            if (propertyData.Name == propertyDataNameManufacturer)
                            {
                                propertyDataValueManufacturer = (string)propertyData.Value;
                            }

                            if (propertyData.Name == propertyDataNameProduct)
                            {
                                propertyDataValueProduct = (string)propertyData.Value;

                            }
                        }
                    }
                }
            }
            catch { propertyDataValueManufacturer = string.Empty; propertyDataValueProduct = string.Empty; }

            //Manufacturer + Product
            string result = $"{propertyDataValueManufacturer} {propertyDataValueProduct}";
            return result.Replace(",", "");
        }

        public string GetSystemRAMInfoName()
        {
            const string propertyDataNameCapacity = "Capacity";
            string propertyDataValueSpeed = "unknown";
            const string propertyDataNameSpeed = "ConfiguredClockSpeed";

            var win32DeviceClassName = "Win32_PhysicalMemory";
            var query = string.Format("select * from {0}", win32DeviceClassName);
            var moduleSetting = new Dictionary<long, int>();

            try
            {
                //Manufacturer + Product
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    ManagementObjectCollection objectCollection = searcher.Get();

                    foreach (ManagementBaseObject managementBaseObject in objectCollection)
                    {
                        foreach (PropertyData propertyData in managementBaseObject.Properties)
                        {
                            if (propertyDataNameSpeed == propertyData.Name)
                            {
                                var value = propertyData.Value;

                                if (value != null)
                                    propertyDataValueSpeed = value.ToString();
                            }

                            if (propertyDataNameCapacity == propertyData.Name)
                            {
                                var value = propertyData.Value;

                                if (value != null)
                                {
                                    var currentCapacity = Convert.ToInt64(value);
                                    if (moduleSetting.ContainsKey(currentCapacity))
                                        moduleSetting[currentCapacity]++;
                                    else
                                        moduleSetting.Add(currentCapacity, 1);
                                }
                            }
                        }
                    }
                }
            }
            catch { propertyDataValueSpeed = "unknown"; moduleSetting.Add(0, 1); }

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

            return $"{wholeCapacity / ONE_GIB}GB ({infoString.Remove(infoString.Length - 1)}) {propertyDataValueSpeed}MT/s";
        }

        private static string GetGraphicsCardNameFromWMI()
        {
            string propertyDataValue = string.Empty;
            const string propertyDataName = "DeviceName";

            var win32DeviceClassName = "Win32_DisplayConfiguration";
            var query = string.Format("select * from {0}", win32DeviceClassName);

            try
            {
                using (var searcher = new ManagementObjectSearcher(query))
                {
                    ManagementObjectCollection objectCollection = searcher.Get();

                    foreach (ManagementBaseObject managementBaseObject in objectCollection)
                    {
                        foreach (PropertyData propertyData in managementBaseObject.Properties)
                        {
                            if (propertyData.Name == propertyDataName)
                            {
                                propertyDataValue = (string)propertyData.Value;
                                break;
                            }
                        }
                    }
                }
            }
            catch { propertyDataValue = string.Empty; }

            //DeviceName
            return propertyDataValue;
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
