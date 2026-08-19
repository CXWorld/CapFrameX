// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// Copyright (C) LibreHardwareMonitor and Contributors.
// Partial Copyright (C) Michael Möller <mmoeller@openhardwaremonitor.org> and Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

using CapFrameX.Monitoring.Contracts;
using LibreHardwareMonitor.Interop;
using LibreHardwareMonitor.PawnIo;
using Microsoft.Win32;
using Serilog;
using static LibreHardwareMonitor.Interop.NvApi;

namespace LibreHardwareMonitor.Hardware.Gpu;

internal sealed class NvidiaGpu : GenericGpu
{
    private const int LoadIndexNvApiBase = 0;
    private const int LoadIndexPowerBase = 100;
    private const int LoadIndexMemory = 300;

    // Throughput sensor indices (PCIe Rx = 0, PCIe Tx = 1).
    private const int ThroughputIndexMemoryBandwidth = 2;

    private const float MiB = 1024f * 1024f;
    private const float GiB = 1024f * 1024f * 1024f;

    // NVAPI reports the memory clock on a generation-dependent basis: GDDR5/5X/6 are reported
    // at a low (~1-2 GHz) clock with the per-cycle data rate folded into the 4x/8x multiplier
    // from GetMemoryDataRateMultiplier(), whereas GDDR7 is reported at its true ~15 GHz I/O
    // clock, whose data-rate multiplier is just 2 (plain DDR). The NvGpuMemoryType enum can't
    // tell these generations apart (it tops out at GDDR5X), so a reported memory clock above
    // this threshold (well above the ~2.6 GHz any pre-GDDR7 card reports) is taken as the
    // GDDR7 high-clock basis and forced to the DDR multiplier of 2.
    private const float Gddr7MemoryClockBasisThresholdMHz = 5000f;
    private const float Gddr7DataRateMultiplier = 2f;

    // GDDR6X uses PAM4 signalling and transfers 16 data words per reported NVAPI memory-clock
    // cycle - twice the 8x of GDDR6/GDDR5X. NvAPI_GPU_GetRamType cannot report GDDR6X (its enum
    // tops out at GDDR5X), so the affected boards are identified by PCI device id, see
    // IsGddr6xMemory(). Example: RTX 4090 at ~1313 MHz x 16 x 384 bit / 8000 = ~1008 GB/s.
    private const float Gddr6xDataRateMultiplier = 16f;

    private uint _lastBlankCounter;
    private ulong _lastPowerSampleTimestamp;
    private bool _powerSamplesFallbackLogged;

    private readonly Stopwatch _stopwatch;
    private readonly int _adapterIndex;
    private readonly Sensor[] _clocks;
    private readonly int _clockVersion;
    private readonly Sensor[] _controls;
    private NvDisplayHandle? _displayHandle;
    private readonly IReadOnlyList<NvDisplayHandleInfo> _displayHandleInfos;
    private readonly Control[] _fanControls;
    private readonly Sensor[] _fans;
    private readonly Sensor _gpuDedicatedMemoryUsage;
    //private readonly Sensor[] _gpuNodeUsage;
    //private readonly DateTime[] _gpuNodeUsagePrevTick;
    //private readonly long[] _gpuNodeUsagePrevValue;
    private readonly Sensor _gpuSharedMemoryUsage;
    private readonly NvApi.NvPhysicalGpuHandle _handle;
    private readonly Sensor _hotSpotTemperature;
    private readonly Sensor[] _loads;
    private readonly Sensor _memoryFree;
    private readonly Sensor[] _memoryTemperatures;
    private readonly Sensor _memoryJunctionTemperature;
    private readonly Sensor _memoryTotal;
    private readonly Sensor _memoryUsed;
    private readonly Sensor _memoryLoad;
    private readonly Sensor _memoryBandwidth;
    private readonly Sensor _memoryClock;
    private readonly Sensor _memoryControllerLoad;
    private readonly uint _memoryBusWidth;
    private readonly float _memoryDataRateMultiplier;
    private readonly float?[] _directMemoryTemperatures = new float?[NvidiaThermal.MemoryTemperatureSensorCount];
    private readonly NvidiaThermal _nvidiaThermal;
    private readonly NvidiaML.NvmlDevice? _nvmlDevice;
    private readonly Sensor _pcieThroughputRx;
    private readonly Sensor _pcieThroughputTx;
    private readonly Sensor[] _powers;
    private readonly Sensor _powerUsage;
    private readonly Sensor _voltage;
    private readonly Sensor[] _temperatures;
    private readonly uint _thermalSensorsMask;
    private readonly Sensor _monitorRefreshRate;
    private readonly Sensor _powerLimit;
    private readonly Sensor _temperatureLimit;
    private readonly Sensor _voltageLimit;
    private string _activeDisplayDeviceName;

    public NvidiaGpu(int adapterIndex, NvApi.NvPhysicalGpuHandle handle, IReadOnlyList<NvDisplayHandleInfo> displayHandles, ISettings settings, ISensorConfig sensorConfig = null)
        : base(
            GetName(handle),
            new Identifier("gpu-nvidia", adapterIndex.ToString(CultureInfo.InvariantCulture)),
            settings,
            sensorConfig: sensorConfig,
            dedicatedMemoryPresentationSortKey: $"{adapterIndex}_8_0",
            sharedMemoryPresentationSortKey: $"{adapterIndex}_8_1")
    {
        _adapterIndex = adapterIndex;
        _handle = handle;
        _displayHandleInfos = displayHandles ?? Array.Empty<NvDisplayHandleInfo>();
        _displayHandle = _displayHandleInfos.Count > 0 ? _displayHandleInfos[0].Handle : null;
        _stopwatch = new Stopwatch();

        bool hasBusId = NvApi.NvAPI_GPU_GetBusId(handle, out uint busId) == NvApi.NvStatus.OK;
        uint pciDevice = 0;
        uint pciFunction = 0;

        // Thermal settings
        NvApi.NvThermalSettings thermalSettings = GetThermalSettings(out NvApi.NvStatus status);
        if (status == NvApi.NvStatus.OK && thermalSettings.Count > 0)
        {
            _temperatures = new Sensor[thermalSettings.Count];

            for (int i = 0; i < thermalSettings.Count; i++)
            {
                NvApi.NvSensor sensor = thermalSettings.Sensor[i];

                string name = sensor.Target switch
                {
                    NvApi.NvThermalTarget.Gpu => "GPU Core",
                    NvApi.NvThermalTarget.Memory => "GPU Memory",
                    NvApi.NvThermalTarget.PowerSupply => "GPU Power Supply",
                    NvApi.NvThermalTarget.Board => "GPU Board",
                    NvApi.NvThermalTarget.VisualComputingBoard => "GPU Visual Computing Board",
                    NvApi.NvThermalTarget.VisualComputingInlet => "GPU Visual Computing Inlet",
                    NvApi.NvThermalTarget.VisualComputingOutlet => "GPU Visual Computing Outlet",
                    _ => "GPU"
                };

                _temperatures[i] = new Sensor(name, i, SensorType.Temperature, this, [], settings)
                {
                    IsPresentationDefault = name == "GPU Core",
                    PresentationSortKey = $"{adapterIndex}_2_0_{i}"
                };
                ActivateSensor(_temperatures[i]);
            }
        }

        // Thermal sensors
        _hotSpotTemperature = new Sensor("GPU Hot Spot", (int)thermalSettings.Count + 1, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{adapterIndex}_2_0_{thermalSettings.Count + 1}" };
        _memoryJunctionTemperature = new Sensor("GPU Memory Junction", (int)thermalSettings.Count + 2, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{adapterIndex}_2_0_{thermalSettings.Count + 2}" };
        _memoryTemperatures = new Sensor[NvidiaThermal.MemoryTemperatureSensorCount];
        for (int i = 0; i < _memoryTemperatures.Length; i++)
        {
            int sensorIndex = (int)thermalSettings.Count + 3 + i;
            _memoryTemperatures[i] = new Sensor($"GPU Memory Temperature #{i + 1}", sensorIndex, SensorType.Temperature, this, settings)
            { PresentationSortKey = $"{adapterIndex}_2_0_{sensorIndex}" };
        }

        bool hasAnyThermalSensor = false;

        for (int thermalSensorsMaxBit = 0; thermalSensorsMaxBit < 32; thermalSensorsMaxBit++)
        {
            // Find the maximum thermal sensor mask value.
            _thermalSensorsMask = 1u << thermalSensorsMaxBit;

            GetThermalSensors(_thermalSensorsMask, out NvApi.NvStatus thermalSensorsStatus);
            if (thermalSensorsStatus == NvApi.NvStatus.OK)
            {
                hasAnyThermalSensor = true;
                continue;
            }

            _thermalSensorsMask--;
            break;
        }

        if (!hasAnyThermalSensor)
        {
            _thermalSensorsMask = 0;
        }

        // Clock frequencies
        for (int clockVersion = 1; clockVersion <= 3; clockVersion++)
        {
            _clockVersion = clockVersion;

            NvApi.NvGpuClockFrequencies clockFrequencies = GetClockFrequencies(out status);
            if (status == NvApi.NvStatus.OK)
            {
                var clocks = new List<Sensor>();
                for (int i = 0; i < clockFrequencies.Clocks.Length; i++)
                {
                    NvApi.NvGpuClockFrequenciesDomain clock = clockFrequencies.Clocks[i];
                    if (clock.IsPresent && Enum.IsDefined(typeof(NvApi.NvGpuPublicClockId), i))
                    {
                        var clockId = (NvApi.NvGpuPublicClockId)i;
                        string name = clockId switch
                        {
                            NvApi.NvGpuPublicClockId.Graphics => "GPU Core",
                            NvApi.NvGpuPublicClockId.Memory => "GPU Memory",
                            NvApi.NvGpuPublicClockId.Processor => "GPU Shader",
                            NvApi.NvGpuPublicClockId.Video => "GPU Video",
                            _ => null
                        };

                        if (name != null)
                        {
                            clocks.Add(new Sensor(name, i, SensorType.Clock, this, settings)
                            {
                                IsPresentationDefault = name == "GPU Core" || name == "GPU Memory",
                                PresentationSortKey = $"{adapterIndex}_0_{i}"
                            });
                        }
                    }
                }

                if (clocks.Count > 0)
                {
                    _clocks = clocks.ToArray();

                    foreach (Sensor sensor in clocks)
                        ActivateSensor(sensor);

                    break;
                }
            }
        }

        // Fans + controllers
        NvApi.NvFanCoolersStatus fanCoolers = GetFanCoolersStatus(out status);
        if (status == NvApi.NvStatus.OK && fanCoolers.Count > 0)
        {
            _fans = new Sensor[fanCoolers.Count];

            for (int i = 0; i < fanCoolers.Count; i++)
            {
                NvApi.NvFanCoolersStatusItem item = fanCoolers.Items[i];

                string name = "GPU Fan" + (fanCoolers.Count > 1 ? " " + (i + 1) : string.Empty);

                _fans[i] = new Sensor(name, (int)item.CoolerId, SensorType.Fan, this, settings)
                {
                    PresentationSortKey = $"{adapterIndex}_5_{i}"
                };
                ActivateSensor(_fans[i]);
            }
        }
        else
        {
            GetTachReading(out status);
            if (status == NvApi.NvStatus.OK)
            {
                _fans = [new Sensor("GPU", 1, SensorType.Fan, this, settings) { PresentationSortKey = $"{adapterIndex}_5" }];
                ActivateSensor(_fans[0]);
            }
        }

        NvApi.NvFanCoolerControl fanControllers = GetFanCoolersControllers(out status);
        if (status == NvApi.NvStatus.OK && fanControllers.Count > 0 && fanCoolers.Count > 0)
        {
            _controls = new Sensor[fanControllers.Count];
            _fanControls = new Control[fanControllers.Count];

            for (int i = 0; i < fanControllers.Count; i++)
            {
                NvApi.NvFanCoolerControlItem item = fanControllers.Items[i];

                string name = "GPU Fan" + (fanControllers.Count > 1 ? " " + (i + 1) : string.Empty);

                NvApi.NvFanCoolersStatusItem fanItem = Array.Find(fanCoolers.Items, x => x.CoolerId == item.CoolerId);
                if (!fanItem.Equals(default(NvApi.NvFanCoolersStatusItem)))
                {
                    _controls[i] = new Sensor(name, (int)item.CoolerId, SensorType.Control, this, settings)
                    { PresentationSortKey = $"{adapterIndex}_6_{i}" };
                    ActivateSensor(_controls[i]);

                    _fanControls[i] = new Control(_controls[i], settings, fanItem.CurrentMinLevel, fanItem.CurrentMaxLevel);
                    _fanControls[i].ControlModeChanged += ControlModeChanged;
                    _fanControls[i].SoftwareControlValueChanged += SoftwareControlValueChanged;
                    _controls[i].Control = _fanControls[i];

                    ControlModeChanged(_fanControls[i]);
                }
            }
        }
        else
        {
            NvApi.NvCoolerSettings coolerSettings = GetCoolerSettings(out status);
            if (status == NvApi.NvStatus.OK && coolerSettings.Count > 0)
            {
                _controls = new Sensor[coolerSettings.Count];
                _fanControls = new Control[coolerSettings.Count];

                for (int i = 0; i < coolerSettings.Count; i++)
                {
                    NvApi.NvCooler cooler = coolerSettings.Cooler[i];
                    string name = "GPU Fan" + (coolerSettings.Count > 1 ? " " + cooler.Controller : string.Empty);

                    _controls[i] = new Sensor(name, i, SensorType.Control, this, settings)
                    { PresentationSortKey = $"{adapterIndex}_6_{i}" };
                    ActivateSensor(_controls[i]);

                    _fanControls[i] = new Control(_controls[i], settings, cooler.DefaultMin, cooler.DefaultMax);
                    _fanControls[i].ControlModeChanged += ControlModeChanged;
                    _fanControls[i].SoftwareControlValueChanged += SoftwareControlValueChanged;
                    _controls[i].Control = _fanControls[i];

                    ControlModeChanged(_fanControls[i]);
                }
            }
        }

        // Load usages
        NvApi.NvDynamicPStatesInfo pStatesInfo = GetDynamicPstatesInfoEx(out status);
        if (status == NvApi.NvStatus.OK)
        {
            Sensor[] loads = new Sensor[NvApi.MAX_GPU_UTILIZATIONS];
            for (int index = 0; index < pStatesInfo.Utilizations.Length; index++)
            {
                NvApi.NvDynamicPState load = pStatesInfo.Utilizations[index];
                if (load.IsPresent && Enum.IsDefined(typeof(NvApi.NvUtilizationDomain), index))
                {
                    var utilizationDomain = (NvApi.NvUtilizationDomain)index;
                    string name = GetUtilizationDomainName(utilizationDomain);

                    if (name != null)
                    {
                        loads[index] = new Sensor(name, LoadIndexNvApiBase + index, SensorType.Load, this, settings)
                        { IsPresentationDefault = name == "GPU Core", PresentationSortKey = $"{adapterIndex}_1_{index}" };
                    }
                }
            }

            if (loads.Any(sensor => sensor != null))
            {
                _loads = loads;

                // The memory-controller (FrameBuffer) load is activated on demand in Update()
                // (mirroring the PCIe throughput sensors), so it is not unconditionally activated here.
                for (int i = 0; i < loads.Length; i++)
                {
                    if (loads[i] != null && i != (int)NvApi.NvUtilizationDomain.FrameBuffer)
                        ActivateSensor(loads[i]);
                }
            }
        }
        else
        {
            NvApi.NvUsages usages = GetUsages(out status);
            if (status == NvApi.NvStatus.OK)
            {
                Sensor[] loads = new Sensor[usages.Entries.Length];
                for (int index = 0; index < usages.Entries.Length; index++)
                {
                    NvApi.NvUsagesEntry load = usages.Entries[index];
                    if (load.IsPresent > 0 && Enum.IsDefined(typeof(NvApi.NvUtilizationDomain), index))
                    {
                        var utilizationDomain = (NvApi.NvUtilizationDomain)index;
                        string name = GetUtilizationDomainName(utilizationDomain);

                        if (name != null)
                        {
                            loads[index] = new Sensor(name, LoadIndexNvApiBase + index, SensorType.Load, this, settings)
                            { PresentationSortKey = $"{adapterIndex}_1_{index}" };
                        }
                    }
                }

                if (loads.Any(sensor => sensor != null))
                {
                    _loads = loads;

                    // The memory-controller (FrameBuffer) load is activated on demand in Update()
                    // (mirroring the PCIe throughput sensors), so it is not unconditionally activated here.
                    for (int i = 0; i < loads.Length; i++)
                    {
                        if (loads[i] != null && i != (int)NvApi.NvUtilizationDomain.FrameBuffer)
                            ActivateSensor(loads[i]);
                    }
                }
            }
        }

        // Power
        NvApi.NvPowerTopology powerTopology = GetPowerTopology(out NvApi.NvStatus powerStatus);
        if (powerStatus == NvApi.NvStatus.OK && powerTopology.Count > 0)
        {
            _powers = new Sensor[powerTopology.Count];
            for (int i = 0; i < powerTopology.Count; i++)
            {
                NvApi.NvPowerTopologyEntry entry = powerTopology.Entries[i];
                string name = entry.Domain switch
                {
                    NvApi.NvPowerTopologyDomain.Gpu => "GPU Power",
                    NvApi.NvPowerTopologyDomain.Board => "GPU Board Power",
                    _ => null
                };

                if (name != null)
                {
                    _powers[i] = new Sensor(name, LoadIndexPowerBase + i, SensorType.Load, this, settings)
                    {
                        PresentationSortKey = $"{adapterIndex}_3_1_{i}"
                    };
                    ActivateSensor(_powers[i]);
                }
            }
        }

        // Voltage
        NvApi.NvGpuVoltageStatus voltageStatus = GetVoltageStatus(out NvApi.NvStatus nvStatus);
        if (nvStatus == NvApi.NvStatus.OK)
        {
            _voltage = new Sensor("GPU Voltage", 0, SensorType.Voltage, this, settings)
            { PresentationSortKey = $"{adapterIndex}_4" };
            _voltage.Value = voltageStatus.ValueInuV / 1E06f;
            ActivateSensor(_voltage);
        }

        // Monitor Refresh Rate
        if (_displayHandle.HasValue && NvApi.NvAPI_GetVBlankCounter != null)
        {
            NvApi.NvStatus pCounterStatus = NvApi.NvAPI_GetVBlankCounter(_displayHandle.Value, out uint pCounter);
            if (pCounterStatus == NvApi.NvStatus.OK)
            {
                _monitorRefreshRate = new Sensor("Monitor Refresh Rate", 0, SensorType.Frequency, this, settings)
                { PresentationSortKey = $"{adapterIndex}_9" };
                _monitorRefreshRate.Value = 0;
                ActivateSensor(_monitorRefreshRate);
            }
        }

        // Performance Limits
        if (NvApi.NvAPI_GPU_PerfGetStatus != null)
        {
            NvApi.NvPerformanceStatus perfStatus = GetPerformanceStatus(out status);
            if (status == NvApi.NvStatus.OK)
            {
                _powerLimit = new Sensor("GPU Power Limit", 0, SensorType.Factor, this, settings)
                { PresentationSortKey = $"{adapterIndex}_10_0" };
                _temperatureLimit = new Sensor("GPU Thermal Limit", 1, SensorType.Factor, this, settings)
                { PresentationSortKey = $"{adapterIndex}_10_1" };
                _voltageLimit = new Sensor("GPU Voltage Limit", 2, SensorType.Factor, this, settings)
                { PresentationSortKey = $"{adapterIndex}_10_2" };
                ActivateSensor(_powerLimit);
                ActivateSensor(_temperatureLimit);
                ActivateSensor(_voltageLimit);
            }
        }

        if (NvidiaML.IsAvailable || NvidiaML.Initialize())
        {
            if (hasBusId)
                _nvmlDevice = NvidiaML.NvmlDeviceGetHandleByPciBusId($" 0000:{busId:X2}:00.0") ?? NvidiaML.NvmlDeviceGetHandleByIndex(_adapterIndex);
            else
                _nvmlDevice = NvidiaML.NvmlDeviceGetHandleByIndex(_adapterIndex);

            if (_nvmlDevice.HasValue)
            {
                _powerUsage = new Sensor("GPU Power", 0, SensorType.Power, this, settings) 
                { IsPresentationDefault = true, PresentationSortKey = $"{adapterIndex}_3_0" };

                _pcieThroughputRx = new Sensor("GPU PCIe Rx", 0, SensorType.Throughput, this, settings)
                { PresentationSortKey = $"{adapterIndex}_7_0" };
                _pcieThroughputTx = new Sensor("GPU PCIe Tx", 1, SensorType.Throughput, this, settings)
                { PresentationSortKey = $"{adapterIndex}_7_1" };

                if (!Software.OperatingSystem.IsUnix)
                {
                    NvidiaML.NvmlPciInfo? pciInfo = NvidiaML.NvmlDeviceGetPciInfo(_nvmlDevice.Value);

                    if (pciInfo is { } pci)
                    {
                        pciDevice = pci.device;
                        pciFunction = GetPciFunction(pci.busId);

                        string[] deviceIds = D3DDisplayDevice.GetDeviceIdentifiers();
                        if (deviceIds != null)
                        {
                            //bool d3dDeviceInitialized = false;

                            foreach (string deviceId in deviceIds)
                            {
                                //if (d3dDeviceInitialized)
                                //    break;

                                if (deviceId.IndexOf("VEN_" + pci.pciVendorId.ToString("X"), StringComparison.OrdinalIgnoreCase) != -1 &&
                                    deviceId.IndexOf("DEV_" + pci.pciDeviceId.ToString("X"), StringComparison.OrdinalIgnoreCase) != -1 &&
                                    deviceId.IndexOf("SUBSYS_" + pci.pciSubSystemId.ToString("X"), StringComparison.OrdinalIgnoreCase) != -1)
                                {
                                    bool isMatch = false;

                                    string actualDeviceId = D3DDisplayDevice.GetActualDeviceIdentifier(deviceId);

                                    try
                                    {
                                        if (Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\nvlddmkm\Enum", adapterIndex.ToString(), null) is string adapterPnpId)
                                        {
                                            if (actualDeviceId.IndexOf(adapterPnpId, StringComparison.OrdinalIgnoreCase) != -1 ||
                                                adapterPnpId.IndexOf(actualDeviceId, StringComparison.OrdinalIgnoreCase) != -1)
                                            {
                                                isMatch = true;
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        // Ignored.
                                    }

                                    if (!isMatch)
                                    {
                                        try
                                        {
                                            string path = actualDeviceId;
                                            path = @"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Enum\" + path;

                                            if (Registry.GetValue(path, "LocationInformation", null) is string locationInformation)
                                            {
                                                // For example:
                                                // @System32\drivers\pci.sys,#65536;PCI bus %1, device %2, function %3;(38,0,0)

                                                int index = locationInformation.IndexOf('(');
                                                if (index != -1)
                                                {
                                                    index++;
                                                    int secondIndex = locationInformation.IndexOf(',', index);
                                                    if (secondIndex != -1)
                                                    {
                                                        string bus = locationInformation.Substring(index, secondIndex - index);

                                                        if (pci.bus.ToString() == bus)
                                                            isMatch = true;
                                                    }
                                                }
                                            }
                                        }
                                        catch
                                        {
                                            // Ignored.
                                        }
                                    }

                                    if (isMatch && D3DDisplayDevice.GetDeviceInfoByIdentifier(deviceId, out D3DDisplayDevice.D3DDeviceInfo deviceInfo))
                                    {
                                        int smallDataSensorIndex = 3; // There are three normal GPU memory sensors.

                                        _gpuDedicatedMemoryUsage = new Sensor("GPU Memory Dedicated", smallDataSensorIndex++, SensorType.Data, this, settings)
                                        { PresentationSortKey = $"{adapterIndex}_8_0" };
                                        _gpuSharedMemoryUsage = new Sensor("GPU Memory Shared", smallDataSensorIndex++, SensorType.Data, this, settings)
                                        { PresentationSortKey = $"{adapterIndex}_8_1" };
                                        InitializeWddmDevice(
                                            deviceId,
                                            deviceInfo.AdapterLuidInstanceName,
                                            smallDataSensorIndex,
                                            $"{adapterIndex}_8_0_1");

                                        //_gpuNodeUsage = new Sensor[deviceInfo.Nodes.Length];
                                        //_gpuNodeUsagePrevValue = new long[deviceInfo.Nodes.Length];
                                        //_gpuNodeUsagePrevTick = new DateTime[deviceInfo.Nodes.Length];

                                        //foreach (D3DDisplayDevice.D3DDeviceNodeInfo node in deviceInfo.Nodes.OrderBy(x => x.Name))
                                        //{
                                        //    int nodeSensorIndex = LoadIndexD3DNodeBase + (int)node.Id;
                                        //    _gpuNodeUsage[node.Id] = new Sensor(node.Name, nodeSensorIndex, SensorType.Load, this, settings)
                                        //    { PresentationSortKey = $"{adapterIndex}_10_{node.Id}" };
                                        //    _gpuNodeUsagePrevValue[node.Id] = node.RunningTime;
                                        //    _gpuNodeUsagePrevTick[node.Id] = node.QueryTime;
                                        //}

                                        //d3dDeviceInitialized = true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        if (hasBusId && !Software.OperatingSystem.IsUnix)
            _nvidiaThermal = new NvidiaThermal(busId, pciDevice, pciFunction);

        _memoryFree = new Sensor("GPU Memory Free", 0, SensorType.Data, this, settings)
        { PresentationSortKey = $"{adapterIndex}_8_2" };
        _memoryUsed = new Sensor("GPU Memory Used", 1, SensorType.Data, this, settings)
        { PresentationSortKey = $"{adapterIndex}_8_3" };
        _memoryTotal = new Sensor("GPU Memory Total", 2, SensorType.Data, this, settings)
        { PresentationSortKey = $"{adapterIndex}_8_4" };
        _memoryLoad = new Sensor("GPU Memory", LoadIndexMemory, SensorType.Load, this, settings)
        { PresentationSortKey = $"{adapterIndex}_8_5" };

        // Momentary memory bandwidth (concept ported from Special K's GPU monitor):
        //   bandwidth = (theoretical peak) * (memory-controller utilization).
        // The peak is derived from the memory clock, the data rate per clock cycle
        // (depends on the memory type) and the memory bus width:
        //   peak [B/s] = memClock[Hz] * dataRateMultiplier * busWidth[bit] / 8
        // The dynamic part is the "GPU Memory Controller" load (NvUtilizationDomain.FrameBuffer).
        _memoryBusWidth = GetMemoryBusWidth();
        // GDDR6X (PAM4) is not reported by NvAPI_GPU_GetRamType, so detect those boards by PCI
        // device id and use the PAM4 multiplier (16) instead of the GDDR5X/6 fallback value (8),
        // which would otherwise under-report their bandwidth by 2x.
        _memoryDataRateMultiplier = IsGddr6xMemory()
            ? Gddr6xDataRateMultiplier
            : GetMemoryDataRateMultiplier(GetMemoryType());

        // Reuse the already-created memory clock / memory-controller load sensors as inputs.
        _memoryClock = _clocks?.FirstOrDefault(s => s.Index == (int)NvApi.NvGpuPublicClockId.Memory);
        _memoryControllerLoad = _loads != null && _loads.Length > (int)NvApi.NvUtilizationDomain.FrameBuffer
            ? _loads[(int)NvApi.NvUtilizationDomain.FrameBuffer]
            : null;

        if (_memoryBusWidth > 0 && _memoryClock != null && _memoryControllerLoad != null)
        {
            _memoryBandwidth = new Sensor("GPU Memory Bandwidth", ThroughputIndexMemoryBandwidth, SensorType.Throughput, this, settings)
            { PresentationSortKey = $"{adapterIndex}_8_6" };
        }

        Update();
    }

    /// <inheritdoc />
    public override string DeviceId
    {
        get
        {
            return WddmDeviceId != null ? D3DDisplayDevice.GetActualDeviceIdentifier(WddmDeviceId) : null;
        }
    }

    public override HardwareType HardwareType
    {
        get { return HardwareType.GpuNvidia; }
    }

    public override void Update()
    {
        UpdateProcessMemorySensors();
        UpdateDisplayHandleIfNeeded();

        if (TryUpdateWddmMemorySensors(ShouldEvaluateAnyD3DSensor(), out D3DDisplayDevice.D3DDeviceInfo deviceInfo))
        {
            _gpuDedicatedMemoryUsage.Value = deviceInfo.GpuDedicatedUsed / GiB;
            _gpuSharedMemoryUsage.Value = deviceInfo.GpuSharedUsed / GiB;
            ActivateSensor(_gpuDedicatedMemoryUsage);
            ActivateSensor(_gpuSharedMemoryUsage);

            //foreach (D3DDisplayDevice.D3DDeviceNodeInfo node in deviceInfo.Nodes)
            //{
            //    long runningTimeDiff = node.RunningTime - _gpuNodeUsagePrevValue[node.Id];
            //    long timeDiff = node.QueryTime.Ticks - _gpuNodeUsagePrevTick[node.Id].Ticks;

            //    _gpuNodeUsage[node.Id].Value = 100f * runningTimeDiff / timeDiff;
            //    _gpuNodeUsagePrevValue[node.Id] = node.RunningTime;
            //    _gpuNodeUsagePrevTick[node.Id] = node.QueryTime;
            //    ActivateSensor(_gpuNodeUsage[node.Id]);
            //}
        }

        NvApi.NvStatus status;

        if (_temperatures is { Length: > 0 })
        {
            NvApi.NvThermalSettings settings = GetThermalSettings(out status);
            // settings.Count is 0 when no valid data available, this happens when you try to read out this value with a high polling interval.
            if (status == NvApi.NvStatus.OK && settings.Count > 0)
            {
                foreach (Sensor sensor in _temperatures)
                    sensor.Value = settings.Sensor[sensor.Index].CurrentTemp;
            }
        }

        float? directHotSpot = null;
        float? directMemoryJunction = null;
        bool directMemoryReadSucceeded = false;
        bool hasDirectThermalData = ShouldEvaluateDirectThermalSensors() &&
            _nvidiaThermal.TryRead(
                out directHotSpot,
                out directMemoryJunction,
                _directMemoryTemperatures,
                out directMemoryReadSucceeded);
        bool hasDirectHotSpot = hasDirectThermalData && directHotSpot.HasValue;
        bool hasDirectMemoryJunction = hasDirectThermalData && directMemoryJunction.HasValue;

        if (hasDirectHotSpot)
            _hotSpotTemperature.Value = directHotSpot;
        else if (_nvidiaThermal != null && Name.StartsWith("NVIDIA GeForce RTX 50", StringComparison.OrdinalIgnoreCase))
            _hotSpotTemperature.Value = null;

        if (hasDirectMemoryJunction)
            _memoryJunctionTemperature.Value = directMemoryJunction;

        if (hasDirectThermalData && directMemoryReadSucceeded)
        {
            for (int i = 0; i < _memoryTemperatures.Length; i++)
            {
                Sensor memoryTemperature = _memoryTemperatures[i];
                memoryTemperature.Value = _directMemoryTemperatures[i];
                if (memoryTemperature.Value.HasValue)
                    ActivateSensor(memoryTemperature);
            }
        }

        if (_thermalSensorsMask > 0)
        {
            NvApi.NvThermalSensors thermalSensors = GetThermalSensors(_thermalSensorsMask, out status);

            if (status == NvApi.NvStatus.OK)
            {
                // RTX 50xx series
                if (Name.StartsWith("NVIDIA GeForce RTX 50", StringComparison.OrdinalIgnoreCase))
                {
                    _temperatures[0].Value = thermalSensors.Temperatures[1] / 256.0f;
                    if (!hasDirectMemoryJunction)
                        _memoryJunctionTemperature.Value = thermalSensors.Temperatures[2] / 256.0f;
                }
                // RTX 40xx series
                else if (Name.StartsWith("NVIDIA GeForce RTX 40", StringComparison.OrdinalIgnoreCase))
                {
                    if (!hasDirectHotSpot)
                        _hotSpotTemperature.Value = thermalSensors.Temperatures[1] / 256.0f;
                    if (!hasDirectMemoryJunction)
                        _memoryJunctionTemperature.Value = thermalSensors.Temperatures[7] / 256.0f;
                }
                else
                {
                    if (!hasDirectHotSpot)
                        _hotSpotTemperature.Value = thermalSensors.Temperatures[1] / 256.0f;
                    if (!hasDirectMemoryJunction)
                        _memoryJunctionTemperature.Value = thermalSensors.Temperatures[9] / 256.0f;
                }
            }
        }
        else
        {
            if (!hasDirectHotSpot)
                _hotSpotTemperature.Value = null;
            if (!hasDirectMemoryJunction)
                _memoryJunctionTemperature.Value = null;
        }

        if (_hotSpotTemperature.Value is > 0)
            ActivateSensor(_hotSpotTemperature);

        if (_memoryJunctionTemperature.Value is > 0)
            ActivateSensor(_memoryJunctionTemperature);

        if (_clocks is { Length: > 0 })
        {
            NvApi.NvGpuClockFrequencies clockFrequencies = GetClockFrequencies(out status);
            if (status == NvApi.NvStatus.OK)
            {
                int current = 0;
                for (int i = 0; i < clockFrequencies.Clocks.Length; i++)
                {
                    NvApi.NvGpuClockFrequenciesDomain clock = clockFrequencies.Clocks[i];
                    if (clock.IsPresent && Enum.IsDefined(typeof(NvApi.NvGpuPublicClockId), i))
                        _clocks[current++].Value = clock.Frequency / 1000f;
                }
            }
        }

        if (_fans is { Length: > 0 })
        {
            NvApi.NvFanCoolersStatus fanCoolers = GetFanCoolersStatus(out status);
            if (status == NvApi.NvStatus.OK && fanCoolers.Count > 0)
            {
                for (int i = 0; i < fanCoolers.Count; i++)
                {
                    NvApi.NvFanCoolersStatusItem item = fanCoolers.Items[i];
                    _fans[i].Value = item.CurrentRpm;
                }
            }
            else
            {
                int tachReading = GetTachReading(out status);
                if (status == NvApi.NvStatus.OK)
                    _fans[0].Value = tachReading;
            }
        }

        if (_controls is { Length: > 0 })
        {
            NvApi.NvFanCoolersStatus fanCoolers = GetFanCoolersStatus(out status);
            if (status == NvApi.NvStatus.OK && fanCoolers.Count > 0 && fanCoolers.Count == _controls.Length)
            {
                for (int i = 0; i < fanCoolers.Count; i++)
                {
                    NvApi.NvFanCoolersStatusItem item = fanCoolers.Items[i];

                    if (Array.Find(_controls, c => c.Index == item.CoolerId) is { } control)
                        control.Value = item.CurrentLevel;
                }
            }
            else
            {
                NvApi.NvCoolerSettings coolerSettings = GetCoolerSettings(out status);
                if (status == NvApi.NvStatus.OK && coolerSettings.Count > 0)
                {
                    for (int i = 0; i < coolerSettings.Count; i++)
                    {
                        NvApi.NvCooler cooler = coolerSettings.Cooler[i];
                        _controls[i].Value = cooler.CurrentLevel;
                    }
                }
            }
        }

        if (_loads is { Length: > 0 })
        {
            NvApi.NvDynamicPStatesInfo pStatesInfo = GetDynamicPstatesInfoEx(out status);
            if (status == NvApi.NvStatus.OK)
            {
                for (int index = 0; index < pStatesInfo.Utilizations.Length; index++)
                {
                    NvApi.NvDynamicPState load = pStatesInfo.Utilizations[index];
                    if (load.IsPresent && Enum.IsDefined(typeof(NvApi.NvUtilizationDomain), index))
                    {
                        if (index < _loads.Length && _loads[index] != null)
                            _loads[index].Value = load.Percentage;
                    }
                }
            }
            else
            {
                NvApi.NvUsages usages = GetUsages(out status);
                if (status == NvApi.NvStatus.OK)
                {
                    for (int index = 0; index < usages.Entries.Length; index++)
                    {
                        NvApi.NvUsagesEntry load = usages.Entries[index];
                        if (load.IsPresent > 0 && Enum.IsDefined(typeof(NvApi.NvUtilizationDomain), index))
                        {
                            if (index < _loads.Length && _loads[index] != null)
                                _loads[index].Value = load.Percentage;
                        }
                    }
                }
            }

            // Expose the memory-controller load like the PCIe throughput sensors: its value is
            // always refreshed above (the bandwidth sensor consumes it), but it is only surfaced
            // when selected for logging/overlay (GetSensorEvaluate returns true on first sight).
            if (_memoryControllerLoad != null && ShouldEvaluateMemoryControllerSensor())
                ActivateSensor(_memoryControllerLoad);
        }

        if (_memoryBandwidth != null && ShouldEvaluateMemoryBandwidthSensor())
        {
            // _memoryClock holds the reported memory clock in MHz, _memoryControllerLoad the
            // memory-controller utilization in %. Compute the momentary bandwidth in GB/s
            // (Throughput sensors are presented as GB/s). Done in floating point to keep the
            // sub-percent resolution of the utilization value.
            float? memClockMHz = _memoryClock.Value;
            float? controllerLoadPercent = _memoryControllerLoad.Value;

            if (memClockMHz.HasValue && controllerLoadPercent.HasValue)
            {
                // A reported memory clock in the GDDR7 range uses the DDR multiplier of 2; the
                // memory-type based multiplier only applies to the low clock basis NVAPI reports
                // for GDDR5/5X/6 (see Gddr7MemoryClockBasisThresholdMHz). This is evaluated per
                // tick rather than once because the memory clock downclocks at idle.
                float dataRateMultiplier = memClockMHz.Value > Gddr7MemoryClockBasisThresholdMHz
                    ? Gddr7DataRateMultiplier
                    : _memoryDataRateMultiplier;

                // peak [GB/s] = memClock[MHz] * 1e6 * multiplier * busWidth[bit] / 8 / 1e9
                //             = memClock[MHz] * multiplier * busWidth / 8000
                float peakGBs = memClockMHz.Value * dataRateMultiplier * _memoryBusWidth / 8000f;

                _memoryBandwidth.Value = peakGBs * (controllerLoadPercent.Value / 100f);
                ActivateSensor(_memoryBandwidth);
            }
        }

        if (_powers is { Length: > 0 } && ShouldEvaluateAnyPowerSensor())
        {
            NvApi.NvPowerTopology powerTopology = GetPowerTopology(out status);
            if (status == NvApi.NvStatus.OK && powerTopology.Count > 0)
            {
                for (int i = 0; i < powerTopology.Count; i++)
                {
                    NvApi.NvPowerTopologyEntry entry = powerTopology.Entries[i];
                    _powers[i].Value = entry.PowerUsage / 1000f;
                }
            }
        }

        if (_displayHandle is not null)
        {
            NvApi.NvMemoryInfo memoryInfo = GetMemoryInfo(out status);
            if (status == NvApi.NvStatus.OK)
            {
                uint free = memoryInfo.CurrentAvailableDedicatedVideoMemory;
                uint total = memoryInfo.DedicatedVideoMemory;
                float used = Math.Max(total - free, 0);

                _memoryTotal.Value = total / MiB;
                ActivateSensor(_memoryTotal);

                _memoryFree.Value = free / MiB;
                ActivateSensor(_memoryFree);

                _memoryUsed.Value = used / MiB;
                ActivateSensor(_memoryUsed);

                _memoryLoad.Value = ((float)(total - free) / total) * 100;
                ActivateSensor(_memoryLoad);
            }
        }

        if (_voltage is not null)
        {
            NvApi.NvGpuVoltageStatus voltageStatus = GetVoltageStatus(out status);
            if (status == NvApi.NvStatus.OK)
            {
                _voltage.Value = voltageStatus.ValueInuV / 1E06f;
            }
        }

        if (_monitorRefreshRate is not null && _displayHandle.HasValue && ShouldEvaluateMonitorRefreshRateSensor())
        {
            NvApi.NvStatus blankCounterStatus = NvApi.NvAPI_GetVBlankCounter(_displayHandle.Value, out uint blankCounter);
            if (blankCounterStatus == NvApi.NvStatus.OK)
            {
                var deltaTicks = _stopwatch.ElapsedTicks;
                _stopwatch.Restart();

                lock (_displayLock)
                {
                    var currentRefreshRate = (float)(blankCounter - _lastBlankCounter) / deltaTicks * Stopwatch.Frequency;
                    _refreshRateBuffer.Add(currentRefreshRate);
                    var refreshRateFiltered = (float)Math.Ceiling(_refreshRateBuffer.RefreshRates.Average());
                    _monitorRefreshRate.Value = refreshRateFiltered > _refreshRateCurrentDisplay ? _refreshRateCurrentDisplay : refreshRateFiltered;
                }

                _lastBlankCounter = blankCounter;
            }
        }

        if (NvidiaML.IsAvailable && _nvmlDevice.HasValue)
        {
            if (ShouldEvaluatePowerUsageSensor())
            {
                int? result = NvidiaML.NvmlDeviceGetPowerUsage(_nvmlDevice.Value, out NvidiaML.NvmlReturn powerUsageStatus);
                if (!result.HasValue)
                {
                    result = NvidiaML.NvmlDeviceGetPowerUsageFromSamples(_nvmlDevice.Value, ref _lastPowerSampleTimestamp);
                    if (result.HasValue && !_powerSamplesFallbackLogged)
                    {
                        Log.Logger.Information(
                            "NVIDIA GPU power monitoring is using NVML samples because nvmlDeviceGetPowerUsage returned {Status}.",
                            powerUsageStatus);
                        _powerSamplesFallbackLogged = true;
                    }
                }

                if (result.HasValue)
                {
                    _powerUsage.Value = result.Value / 1000f;
                    ActivateSensor(_powerUsage);
                }
            }

            // In MB/s, throughput sensors are passed as in KB/s.
            if (ShouldEvaluatePcieRxSensor())
            {
                uint? rx = NvidiaML.NvmlDeviceGetPcieThroughput(_nvmlDevice.Value, NvidiaML.NvmlPcieUtilCounter.RxBytes);
                if (rx.HasValue)
                {
                    _pcieThroughputRx.Value = rx / MiB;
                    ActivateSensor(_pcieThroughputRx);
                }
            }

            if (ShouldEvaluatePcieTxSensor())
            {
                uint? tx = NvidiaML.NvmlDeviceGetPcieThroughput(_nvmlDevice.Value, NvidiaML.NvmlPcieUtilCounter.TxBytes);
                if (tx.HasValue)
                {
                    _pcieThroughputTx.Value = tx / MiB;
                    ActivateSensor(_pcieThroughputTx);
                }
            }
        }

        // Performance limits
        if (_powerLimit != null && _temperatureLimit != null && _voltageLimit != null && ShouldEvaluateAnyLimitSensor())
        {
            NvApi.NvPerformanceStatus perfStatus = GetPerformanceStatus(out status);
            if (status == NvApi.NvStatus.OK)
            {
                var currentActiveLimit = perfStatus.PerformanceLimit;
                _powerLimit.Value = (currentActiveLimit & NvApi.NvPerformanceLimit.PowerLimit) == NvApi.NvPerformanceLimit.PowerLimit ? 1 : 0;
                ActivateSensor(_powerLimit);
                _temperatureLimit.Value = (currentActiveLimit & NvApi.NvPerformanceLimit.TemperatureLimit) == NvApi.NvPerformanceLimit.TemperatureLimit ? 1 : 0;
                ActivateSensor(_temperatureLimit);
                _voltageLimit.Value = (currentActiveLimit & NvApi.NvPerformanceLimit.VoltageLimit) == NvApi.NvPerformanceLimit.VoltageLimit ? 1 : 0;
                ActivateSensor(_voltageLimit);
            }
        }
        else if (_powerLimit != null && _temperatureLimit != null && _voltageLimit != null)
        {
            _powerLimit.Value = null;
            _temperatureLimit.Value = null;
            _voltageLimit.Value = null;
        }
    }

    public override string GetDriverVersion()
    {
        var r = new StringBuilder();

        if (_displayHandle.HasValue && NvApi.NvAPI_GetDisplayDriverVersion != null)
        {
            NvApi.NvDisplayDriverVersion driverVersion = new()
            {
                Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvDisplayDriverVersion>(1)
            };

            if (NvApi.NvAPI_GetDisplayDriverVersion(_displayHandle.Value, ref driverVersion) == NvStatus.OK)
            {
                r.Append(driverVersion.DriverVersion / 100);
                r.Append(".");
                r.Append((driverVersion.DriverVersion % 100).ToString("00",
                  CultureInfo.InvariantCulture));
            }
        }
        else
            return base.GetDriverVersion();

        return r.ToString();
    }

    private bool ShouldEvaluateAnyD3DSensor()
    {
        if (_sensorConfig == null)
            return true;

        bool evaluate = false;
        if (_gpuDedicatedMemoryUsage != null)
            evaluate |= _sensorConfig.GetSensorEvaluate(_gpuDedicatedMemoryUsage.Identifier.ToString());

        if (_gpuSharedMemoryUsage != null)
            evaluate |= _sensorConfig.GetSensorEvaluate(_gpuSharedMemoryUsage.Identifier.ToString());

        //if (_gpuNodeUsage is { Length: > 0 })
        //{
        //    foreach (Sensor sensor in _gpuNodeUsage)
        //    {
        //        if (sensor != null && _sensorConfig.GetSensorEvaluate(sensor.Identifier.ToString()))
        //            return true;
        //    }
        //}

        return evaluate;
    }

    private bool ShouldEvaluateDirectThermalSensors()
    {
        if (_nvidiaThermal == null)
            return false;

        if (_sensorConfig == null)
            return true;

        // GetSensorEvaluate permits the initial discovery read; NeedsInitialSample keeps the
        // asynchronous reader eligible until a valid result has been consumed. Afterwards,
        // polling requires logging or overlay usage.
        bool evaluate = _nvidiaThermal.NeedsInitialSample;
        evaluate |= _sensorConfig.GetSensorEvaluate(_hotSpotTemperature.Identifier.ToString());
        evaluate |= _sensorConfig.GetSensorEvaluate(_memoryJunctionTemperature.Identifier.ToString());

        foreach (Sensor memoryTemperature in _memoryTemperatures)
            evaluate |= _sensorConfig.GetSensorEvaluate(memoryTemperature.Identifier.ToString());

        return evaluate;
    }

    private bool ShouldEvaluateAnyPowerSensor()
    {
        if (_sensorConfig == null)
            return true;

        foreach (Sensor sensor in _powers)
        {
            if (sensor != null && _sensorConfig.GetSensorEvaluate(sensor.Identifier.ToString()))
                return true;
        }

        return false;
    }

    private bool ShouldEvaluatePowerUsageSensor()
    {
        if (_powerUsage == null)
            return false;

        if (_sensorConfig == null)
            return true;

        return _sensorConfig.GetSensorEvaluate(_powerUsage.Identifier.ToString());
    }

    private bool ShouldEvaluatePcieRxSensor()
    {
        if (_pcieThroughputRx == null)
            return false;

        if (_sensorConfig == null)
            return true;

        return _sensorConfig.GetSensorEvaluate(_pcieThroughputRx.Identifier.ToString());
    }

    private bool ShouldEvaluatePcieTxSensor()
    {
        if (_pcieThroughputTx == null)
            return false;

        if (_sensorConfig == null)
            return true;

        return _sensorConfig.GetSensorEvaluate(_pcieThroughputTx.Identifier.ToString());
    }

    private bool ShouldEvaluateMemoryControllerSensor()
    {
        if (_memoryControllerLoad == null)
            return false;

        if (_sensorConfig == null)
            return true;

        return _sensorConfig.GetSensorEvaluate(_memoryControllerLoad.Identifier.ToString());
    }

    private bool ShouldEvaluateAnyLimitSensor()
    {
        if (_sensorConfig == null)
            return true;

        if (_powerLimit != null && _sensorConfig.GetSensorEvaluate(_powerLimit.Identifier.ToString()))
            return true;

        if (_temperatureLimit != null && _sensorConfig.GetSensorEvaluate(_temperatureLimit.Identifier.ToString()))
            return true;

        if (_voltageLimit != null && _sensorConfig.GetSensorEvaluate(_voltageLimit.Identifier.ToString()))
            return true;

        return false;
    }

    private bool ShouldEvaluateMonitorRefreshRateSensor()
    {
        if (_monitorRefreshRate == null)
            return false;

        if (_sensorConfig == null)
            return true;

        return _sensorConfig.GetSensorEvaluate(_monitorRefreshRate.Identifier.ToString());
    }

    private bool ShouldEvaluateMemoryBandwidthSensor()
    {
        if (_memoryBandwidth == null)
            return false;

        if (_sensorConfig == null)
            return true;

        return _sensorConfig.GetSensorEvaluate(_memoryBandwidth.Identifier.ToString());
    }

    public override string GetReport()
    {
        StringBuilder r = new();

        r.AppendLine("Nvidia GPU");
        r.AppendLine();
        r.AppendFormat("Name: {0}{1}", _name, Environment.NewLine);
        r.AppendFormat("Index: {0}{1}", _adapterIndex, Environment.NewLine);

        if (_displayHandle.HasValue && NvApi.NvAPI_GetDisplayDriverVersion != null)
        {
            NvApi.NvDisplayDriverVersion driverVersion = new()
            {
                Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvDisplayDriverVersion>(1)
            };

            if (NvApi.NvAPI_GetDisplayDriverVersion(_displayHandle.Value, ref driverVersion) == NvApi.NvStatus.OK)
            {
                r.Append("Driver Version: ");
                r.Append(driverVersion.DriverVersion / 100);
                r.Append(".");
                r.Append((driverVersion.DriverVersion % 100).ToString("00", CultureInfo.InvariantCulture));
                r.AppendLine();
                r.Append("Driver Branch: ");
                r.AppendLine(driverVersion.BuildBranch);
            }
        }

        if (NvApi.NvAPI_GPU_GetPCIIdentifiers != null)
        {
            NvApi.NvStatus status = NvApi.NvAPI_GPU_GetPCIIdentifiers(_handle, out uint deviceId, out uint subSystemId, out uint revisionId, out uint extDeviceId);
            if (status == NvApi.NvStatus.OK)
            {
                r.Append("DeviceID: 0x");
                r.AppendLine(deviceId.ToString("X", CultureInfo.InvariantCulture));
                r.Append("SubSystemID: 0x");
                r.AppendLine(subSystemId.ToString("X", CultureInfo.InvariantCulture));
                r.Append("RevisionID: 0x");
                r.AppendLine(revisionId.ToString("X", CultureInfo.InvariantCulture));
                r.Append("ExtDeviceID: 0x");
                r.AppendLine(extDeviceId.ToString("X", CultureInfo.InvariantCulture));
                r.AppendLine();
            }
        }

        if (NvApi.NvAPI_GPU_GetThermalSettings != null)
        {
            NvApi.NvThermalSettings thermalSettings = GetThermalSettings(out NvApi.NvStatus status);

            r.AppendLine("Thermal Settings");
            r.AppendLine();

            if (status == NvApi.NvStatus.OK)
            {
                for (int i = 0; i < thermalSettings.Count; i++)
                {
                    r.AppendFormat(" Sensor[{0}].Controller: {1}{2}", i, thermalSettings.Sensor[i].Controller, Environment.NewLine);
                    r.AppendFormat(" Sensor[{0}].DefaultMinTemp: {1}{2}", i, thermalSettings.Sensor[i].DefaultMinTemp, Environment.NewLine);
                    r.AppendFormat(" Sensor[{0}].DefaultMaxTemp: {1}{2}", i, thermalSettings.Sensor[i].DefaultMaxTemp, Environment.NewLine);
                    r.AppendFormat(" Sensor[{0}].CurrentTemp: {1}{2}", i, thermalSettings.Sensor[i].CurrentTemp, Environment.NewLine);
                    r.AppendFormat(" Sensor[{0}].Target: {1}{2}", i, thermalSettings.Sensor[i].Target, Environment.NewLine);
                }
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (NvApi.NvAPI_GPU_GetAllClocks != null)
        {
            NvApi.NvGpuClockFrequencies clocks = GetClockFrequencies(out NvApi.NvStatus status);

            r.AppendLine("Clocks");
            r.AppendLine();
            if (status == NvApi.NvStatus.OK)
            {
                for (int i = 0; i < clocks.Clocks.Length; i++)
                {
                    if (clocks.Clocks[i].IsPresent)
                        r.AppendFormat(" Clock[{0}]: {1}{2}", i, clocks.Clocks[i].Frequency, Environment.NewLine);
                }
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (NvApi.NvAPI_GPU_GetTachReading != null)
        {
            NvApi.NvStatus status = NvApi.NvAPI_GPU_GetTachReading(_handle, out int tachValue);

            r.AppendLine("Tachometer");
            r.AppendLine();
            if (status == NvApi.NvStatus.OK)
            {
                r.AppendFormat(" Value: {0}{1}", tachValue, Environment.NewLine);
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (NvApi.NvAPI_GPU_GetDynamicPstatesInfoEx != null)
        {
            NvApi.NvDynamicPStatesInfo pStatesInfo = GetDynamicPstatesInfoEx(out NvApi.NvStatus status);

            r.AppendLine("P-States");
            r.AppendLine();
            if (status == NvApi.NvStatus.OK)
            {
                for (int i = 0; i < pStatesInfo.Utilizations.Length; i++)
                {
                    if (pStatesInfo.Utilizations[i].IsPresent)
                        r.AppendFormat(" Percentage[{0}]: {1}{2}", i, pStatesInfo.Utilizations[i].Percentage, Environment.NewLine);
                }
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (NvApi.NvAPI_GPU_GetUsages != null)
        {
            NvApi.NvUsages usages = GetUsages(out NvApi.NvStatus status);

            r.AppendLine("Usages");
            r.AppendLine();
            if (status == NvApi.NvStatus.OK)
            {
                for (int i = 0; i < usages.Entries.Length; i++)
                {
                    if (usages.Entries[i].IsPresent > 0)
                        r.AppendFormat(" Usage[{0}]: {1}{2}", i, usages.Entries[i].Percentage, Environment.NewLine);
                }
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (NvApi.NvAPI_GPU_GetCoolerSettings != null)
        {
            NvApi.NvCoolerSettings coolerSettings = GetCoolerSettings(out NvApi.NvStatus status);
            r.AppendLine("Cooler Settings");
            r.AppendLine();
            if (status == NvApi.NvStatus.OK)
            {
                for (int i = 0; i < coolerSettings.Count; i++)
                {
                    r.AppendFormat(" Cooler[{0}].Type: {1}{2}", i, coolerSettings.Cooler[i].Type, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].Controller: {1}{2}", i, coolerSettings.Cooler[i].Controller, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].DefaultMin: {1}{2}", i, coolerSettings.Cooler[i].DefaultMin, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].DefaultMax: {1}{2}", i, coolerSettings.Cooler[i].DefaultMax, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].CurrentMin: {1}{2}", i, coolerSettings.Cooler[i].CurrentMin, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].CurrentMax: {1}{2}", i, coolerSettings.Cooler[i].CurrentMax, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].CurrentLevel: {1}{2}", i, coolerSettings.Cooler[i].CurrentLevel, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].DefaultPolicy: {1}{2}", i, coolerSettings.Cooler[i].DefaultPolicy, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].CurrentPolicy: {1}{2}", i, coolerSettings.Cooler[i].CurrentPolicy, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].Target: {1}{2}", i, coolerSettings.Cooler[i].Target, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].ControlType: {1}{2}", i, coolerSettings.Cooler[i].ControlType, Environment.NewLine);
                    r.AppendFormat(" Cooler[{0}].Active: {1}{2}", i, coolerSettings.Cooler[i].Active, Environment.NewLine);
                }
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (NvApi.NvAPI_GPU_ClientFanCoolersGetStatus != null)
        {
            NvApi.NvFanCoolersStatus coolers = GetFanCoolersStatus(out NvApi.NvStatus status);

            r.AppendLine("Fan Coolers Status");
            r.AppendLine();
            if (status == NvApi.NvStatus.OK)
            {
                for (int i = 0; i < coolers.Count; i++)
                {
                    r.AppendFormat(" Items[{0}].CoolerId: {1}{2}",
                                   i,
                                   coolers.Items[i].CoolerId,
                                   Environment.NewLine);

                    r.AppendFormat(" Items[{0}].CurrentRpm: {1}{2}",
                                   i,
                                   coolers.Items[i].CurrentRpm,
                                   Environment.NewLine);

                    r.AppendFormat(" Items[{0}].CurrentMinLevel: {1}{2}",
                                   i,
                                   coolers.Items[i].CurrentMinLevel,
                                   Environment.NewLine);

                    r.AppendFormat(" Items[{0}].CurrentMaxLevel: {1}{2}",
                                   i,
                                   coolers.Items[i].CurrentMaxLevel,
                                   Environment.NewLine);

                    r.AppendFormat(" Items[{0}].CurrentLevel: {1}{2}",
                                   i,
                                   coolers.Items[i].CurrentLevel,
                                   Environment.NewLine);
                }
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (NvApi.NvAPI_GPU_ClientPowerTopologyGetStatus != null)
        {
            NvApi.NvPowerTopology powerTopology = GetPowerTopology(out NvApi.NvStatus status);

            r.AppendLine("Power Topology");
            r.AppendLine();

            if (status == NvApi.NvStatus.OK)
            {
                for (int i = 0; i < powerTopology.Count; i++)
                {
                    NvApi.NvPowerTopologyEntry entry = powerTopology.Entries[i];
                    _powers[i].Value = entry.PowerUsage / 1000f;

                    r.AppendFormat(" Entries[{0}].Domain: {1}{2}", i, entry.Domain, Environment.NewLine);
                    r.AppendFormat(" Entries[{0}].PowerUsage: {1}{2}", i, entry.PowerUsage, Environment.NewLine);
                }
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (NvApi.NvAPI_GPU_GetMemoryInfo != null)
        {
            NvApi.NvMemoryInfo memoryInfo = GetMemoryInfo(out NvApi.NvStatus status);

            r.AppendLine("Memory Info");
            r.AppendLine();
            if (status == NvApi.NvStatus.OK)
            {
                r.AppendFormat(" AvailableDedicatedVideoMemory: {0}{1}", memoryInfo.AvailableDedicatedVideoMemory, Environment.NewLine);
                r.AppendFormat(" DedicatedVideoMemory: {0}{1}", memoryInfo.DedicatedVideoMemory, Environment.NewLine);
                r.AppendFormat(" CurrentAvailableDedicatedVideoMemory: {0}{1}", memoryInfo.CurrentAvailableDedicatedVideoMemory, Environment.NewLine);
                r.AppendFormat(" SharedSystemMemory: {0}{1}", memoryInfo.SharedSystemMemory, Environment.NewLine);
                r.AppendFormat(" SystemVideoMemory: {0}{1}", memoryInfo.SystemVideoMemory, Environment.NewLine);
            }
            else
            {
                r.Append(" Status: ");
                r.AppendLine(status.ToString());
            }

            r.AppendLine();
        }

        if (NvApi.NvAPI_GPU_GetFBWidthAndLocation != null || NvApi.NvAPI_GPU_GetRamType != null)
        {
            r.AppendLine("Memory Bandwidth");
            r.AppendLine();
            r.AppendFormat(" Bus Width: {0} bit{1}", _memoryBusWidth, Environment.NewLine);
            r.AppendFormat(" Memory Type: {0} (raw {1}){2}", GetMemoryType(), (uint)GetMemoryType(), Environment.NewLine);
            r.AppendFormat(" Data Rate Multiplier: {0}{1}", _memoryDataRateMultiplier, Environment.NewLine);
            r.AppendLine();
        }

        if (WddmDeviceId != null)
        {
            r.AppendLine("D3D");
            r.AppendLine();
            r.AppendLine(" Id: " + WddmDeviceId);

            r.AppendLine();
        }

        return r.ToString();
    }

    private static string GetName(NvApi.NvPhysicalGpuHandle handle)
    {
        if (NvApi.NvAPI_GPU_GetFullName(handle, out string gpuName) == NvApi.NvStatus.OK)
        {
            string name = gpuName.Trim();
            return name.StartsWith("NVIDIA", StringComparison.OrdinalIgnoreCase) ? name : "NVIDIA " + name;
        }

        return "NVIDIA";
    }

    private static uint GetPciFunction(string busId)
    {
        if (string.IsNullOrEmpty(busId))
            return 0;

        int separator = busId.LastIndexOf('.');
        return separator >= 0 &&
               uint.TryParse(busId.Substring(separator + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint function) &&
               function <= 7
            ? function
            : 0;
    }

    private NvApi.NvMemoryInfo GetMemoryInfo(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_GetMemoryInfo == null || _displayHandle == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        NvApi.NvMemoryInfo memoryInfo = new()
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvMemoryInfo>(2)
        };

        status = NvApi.NvAPI_GPU_GetMemoryInfo(_displayHandle.Value, ref memoryInfo);
        return status == NvApi.NvStatus.OK ? memoryInfo : default;
    }

    private NvApi.NvGpuClockFrequencies GetClockFrequencies(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_GetAllClockFrequencies == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        NvApi.NvGpuClockFrequencies clockFrequencies = new()
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvGpuClockFrequencies>(_clockVersion)
        };

        status = NvApi.NvAPI_GPU_GetAllClockFrequencies(_handle, ref clockFrequencies);
        return status == NvApi.NvStatus.OK ? clockFrequencies : default;
    }

    private NvApi.NvThermalSettings GetThermalSettings(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_GetThermalSettings == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        NvApi.NvThermalSettings settings = new()
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvThermalSettings>(2),
            Count = NvApi.MAX_THERMAL_SENSORS_PER_GPU
        };

        status = NvApi.NvAPI_GPU_GetThermalSettings(_handle, (int)NvApi.NvThermalTarget.All, ref settings);
        return status == NvApi.NvStatus.OK ? settings : default;
    }

    private NvApi.NvThermalSensors GetThermalSensors(uint mask, out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_GetThermalSensors == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        var thermalSensors = new NvApi.NvThermalSensors
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvThermalSensors>(2),
            Mask = mask
        };

        status = NvApi.NvAPI_GPU_GetThermalSensors(_handle, ref thermalSensors);
        return status == NvApi.NvStatus.OK ? thermalSensors : default;
    }

    private NvApi.NvFanCoolersStatus GetFanCoolersStatus(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_ClientFanCoolersGetStatus == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        var coolers = new NvApi.NvFanCoolersStatus
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvFanCoolersStatus>(1),
            Items = new NvApi.NvFanCoolersStatusItem[NvApi.MAX_FAN_COOLERS_STATUS_ITEMS]
        };

        status = NvApi.NvAPI_GPU_ClientFanCoolersGetStatus(_handle, ref coolers);
        return status == NvApi.NvStatus.OK ? coolers : default;
    }

    private NvApi.NvFanCoolerControl GetFanCoolersControllers(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_ClientFanCoolersGetControl == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        var controllers = new NvApi.NvFanCoolerControl
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvFanCoolerControl>(1)
        };

        status = NvApi.NvAPI_GPU_ClientFanCoolersGetControl(_handle, ref controllers);
        return status == NvApi.NvStatus.OK ? controllers : default;
    }

    private NvApi.NvCoolerSettings GetCoolerSettings(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_GetCoolerSettings == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        NvApi.NvCoolerSettings settings = new()
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvCoolerSettings>(2),
            Cooler = new NvApi.NvCooler[NvApi.MAX_COOLERS_PER_GPU]
        };

        status = NvApi.NvAPI_GPU_GetCoolerSettings(_handle, NvApi.NvCoolerTarget.All, ref settings);
        return status == NvApi.NvStatus.OK ? settings : default;
    }

    private NvApi.NvDynamicPStatesInfo GetDynamicPstatesInfoEx(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_GetDynamicPstatesInfoEx == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        NvApi.NvDynamicPStatesInfo pStatesInfo = new()
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvDynamicPStatesInfo>(1),
            Utilizations = new NvApi.NvDynamicPState[NvApi.MAX_GPU_UTILIZATIONS]
        };

        status = NvApi.NvAPI_GPU_GetDynamicPstatesInfoEx(_handle, ref pStatesInfo);
        return status == NvApi.NvStatus.OK ? pStatesInfo : default;
    }

    private NvApi.NvUsages GetUsages(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_GetUsages == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        NvApi.NvUsages usages = new()
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvUsages>(1)
        };

        status = NvApi.NvAPI_GPU_GetUsages(_handle, ref usages);
        return status == NvApi.NvStatus.OK ? usages : default;
    }

    private NvApi.NvPowerTopology GetPowerTopology(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_ClientPowerTopologyGetStatus == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        NvApi.NvPowerTopology powerTopology = new()
        {
            Version = NvApi.MAKE_NVAPI_VERSION<NvApi.NvPowerTopology>(1)
        };

        status = NvApi.NvAPI_GPU_ClientPowerTopologyGetStatus(_handle, ref powerTopology);
        return status == NvApi.NvStatus.OK ? powerTopology : default;
    }

    private NvApi.NvPerformanceStatus GetPerformanceStatus(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_PerfGetStatus == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        NvApi.NvPerformanceStatus perfStatus = new()
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvPerformanceStatus>(1),
            TimersInNanoSecond = new ulong[NvApi.PERFORMANCE_STATUS_TIMER_COUNT],
            Unknown5 = new uint[NvApi.PERFORMANCE_STATUS_UNKNOWN_COUNT]
        };

        status = NvApi.NvAPI_GPU_PerfGetStatus(_handle, ref perfStatus);
        return status == NvApi.NvStatus.OK ? perfStatus : default;
    }

    private NvApi.NvGpuVoltageStatus GetVoltageStatus(out NvApi.NvStatus voltageStatus)
    {
        if (NvApi.NvAPI_GPU_GetCurrentVoltage == null)
        {
            voltageStatus = NvApi.NvStatus.Error;
            return default;
        }
        NvApi.NvGpuVoltageStatus statusInfo = new()
        {
            Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvGpuVoltageStatus>(1)
        };
        voltageStatus = NvApi.NvAPI_GPU_GetCurrentVoltage(_handle, ref statusInfo);
        return voltageStatus == NvApi.NvStatus.OK ? statusInfo : default;
    }

    private int GetTachReading(out NvApi.NvStatus status)
    {
        if (NvApi.NvAPI_GPU_GetTachReading == null)
        {
            status = NvApi.NvStatus.Error;
            return default;
        }

        status = NvApi.NvAPI_GPU_GetTachReading(_handle, out int value);
        return value;
    }

    private uint GetMemoryBusWidth()
    {
        if (NvApi.NvAPI_GPU_GetFBWidthAndLocation == null)
            return 0;

        return NvApi.NvAPI_GPU_GetFBWidthAndLocation(_handle, out uint width, out _) == NvApi.NvStatus.OK
            ? width
            : 0;
    }

    private NvApi.NvGpuMemoryType GetMemoryType()
    {
        if (NvApi.NvAPI_GPU_GetRamType == null)
            return NvApi.NvGpuMemoryType.Unknown;

        return NvApi.NvAPI_GPU_GetRamType(_handle, out uint memType) == NvApi.NvStatus.OK
            ? (NvApi.NvGpuMemoryType)memType
            : NvApi.NvGpuMemoryType.Unknown;
    }

    // Known desktop boards that use GDDR6X (PAM4) memory. NvAPI_GPU_GetRamType cannot report
    // GDDR6X (its NvGpuMemoryType enum tops out at GDDR5X and returns GDDR5X/Unknown for it), so
    // these are matched by PCI device id and forced to the PAM4 data-rate multiplier (16x), i.e.
    // twice the GDDR5X/GDDR6 value of 8x. Device ids verified against the pci.ids repository.
    private bool IsGddr6xMemory()
    {
        if (NvApi.NvAPI_GPU_GetPCIIdentifiers == null ||
            NvApi.NvAPI_GPU_GetPCIIdentifiers(_handle, out uint deviceId, out _, out _, out _) != NvApi.NvStatus.OK)
            return false;

        // NvAPI packs the identifier as (pciDeviceId << 16) | pciVendorId (0x10DE for NVIDIA);
        // the vendor check keeps the extraction correct even if a driver ever returns the bare id.
        uint pciDeviceId = (deviceId & 0xFFFF) == 0x10DE ? deviceId >> 16 : deviceId & 0xFFFF;

        switch (pciDeviceId)
        {
            // Ampere (GA102 / GA104)
            case 0x2203: // RTX 3090 Ti
            case 0x2204: // RTX 3090
            case 0x2206: // RTX 3080
            case 0x2208: // RTX 3080 Ti
            case 0x220A: // RTX 3080 12GB
            case 0x2216: // RTX 3080 (Lite Hash Rate)
            case 0x2482: // RTX 3070 Ti
            case 0x24C9: // RTX 3060 Ti GDDR6X (distinct id from the GDDR6 0x2489)
            // Ada (AD102 / AD103 / AD104)
            case 0x2684: // RTX 4090
            case 0x2685: // RTX 4090 D
            case 0x2702: // RTX 4080 SUPER
            case 0x2704: // RTX 4080
            case 0x2705: // RTX 4070 Ti SUPER
            case 0x2782: // RTX 4070 Ti
            case 0x2783: // RTX 4070 SUPER
            case 0x2786: // RTX 4070 (GDDR6X launch part; the later GDDR6 / AD103 0x2709 variants are excluded)
                return true;
            default:
                return false;
        }
    }

    private static float GetMemoryDataRateMultiplier(NvApi.NvGpuMemoryType memoryType)
    {
        // Multiplier = effective data transfers per *reported* NVAPI memory clock cycle.
        // NVAPI reports the memory I/O command clock; multiplying yields the per-pin data rate.
        switch (memoryType)
        {
            case NvApi.NvGpuMemoryType.Sdram:
            case NvApi.NvGpuMemoryType.Ddr1:
            case NvApi.NvGpuMemoryType.Ddr2:
            case NvApi.NvGpuMemoryType.Ddr3:
            case NvApi.NvGpuMemoryType.Gddr2:
            case NvApi.NvGpuMemoryType.Gddr3:
            case NvApi.NvGpuMemoryType.Lpddr2:
                return 2f; // (LP/G)DDR: double data rate
            case NvApi.NvGpuMemoryType.Gddr4:
            case NvApi.NvGpuMemoryType.Gddr5:
                return 4f; // GDDR5: quad data rate
            case NvApi.NvGpuMemoryType.Gddr5x:
                return 8f; // GDDR5X (and commonly reported for GDDR6)
            default:
                // GDDR6 / GDDR6X are frequently NOT distinguished by NvAPI_GPU_GetRamType
                // (it tops out at GDDR5X) and may report Unknown or a newer, higher value.
                // Assume a modern GDDR data rate when the raw value is >= GDDR5, otherwise plain DDR.
                // NOTE: GDDR6 maps to 8x here; known GDDR6X (PAM4, 16x) boards are handled earlier
                // by IsGddr6xMemory() via PCI device id, since this fallback would under-report 2x.
                return (uint)memoryType >= (uint)NvApi.NvGpuMemoryType.Gddr5 ? 8f : 2f;
        }
    }

    private static string GetUtilizationDomainName(NvApi.NvUtilizationDomain utilizationDomain) => utilizationDomain switch
    {
        NvApi.NvUtilizationDomain.Gpu => "GPU Core",
        NvApi.NvUtilizationDomain.FrameBuffer => "GPU Memory Controller",
        NvApi.NvUtilizationDomain.VideoEngine => "GPU Video Engine",
        NvApi.NvUtilizationDomain.BusInterface => "GPU Bus",
        _ => null
    };

    private void UpdateDisplayHandleIfNeeded()
    {
        string displayDeviceName;
        lock (_displayLock)
        {
            displayDeviceName = _displayDeviceName;

            if (string.Equals(displayDeviceName, _activeDisplayDeviceName, StringComparison.OrdinalIgnoreCase))
                return;

            _activeDisplayDeviceName = displayDeviceName;
            NvDisplayHandle? selectedHandle = SelectDisplayHandle(displayDeviceName);

            if (!HandlesEqual(_displayHandle, selectedHandle))
            {
                _displayHandle = selectedHandle;
                _lastBlankCounter = 0;
                _stopwatch.Reset();
                _refreshRateBuffer.Clear();
            }
        }
    }

    private NvApi.NvDisplayHandle? SelectDisplayHandle(string displayDeviceName)
    {
        if (_displayHandleInfos.Count == 0)
            return null;

        if (string.IsNullOrWhiteSpace(displayDeviceName))
            return _displayHandleInfos[0].Handle;

        string normalizedDeviceName = NormalizeDisplayName(displayDeviceName);
        for (int i = 0; i < _displayHandleInfos.Count; i++)
        {
            string normalizedHandleName = NormalizeDisplayName(_displayHandleInfos[i].DisplayName);
            if (normalizedHandleName != null &&
                string.Equals(normalizedHandleName, normalizedDeviceName, StringComparison.OrdinalIgnoreCase))
            {
                return _displayHandleInfos[i].Handle;
            }
        }

        return _displayHandleInfos[0].Handle;
    }

    private static string NormalizeDisplayName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string trimmed = name.Trim();
        if (!trimmed.StartsWith(@"\\.\", StringComparison.OrdinalIgnoreCase) &&
            trimmed.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = @"\\.\" + trimmed;
        }

        return trimmed.ToUpperInvariant();
    }

    private static bool HandlesEqual(NvApi.NvDisplayHandle? left, NvApi.NvDisplayHandle? right)
    {
        if (!left.HasValue && !right.HasValue)
            return true;
        if (!left.HasValue || !right.HasValue)
            return false;

        return left.Value.Equals(right.Value);
    }

    private void ControlModeChanged(IControl control)
    {
        switch (control.ControlMode)
        {
            case ControlMode.Default:
                RestoreDefaultFanBehavior(control.Sensor.Index);
                break;
            case ControlMode.Software:
                SoftwareControlValueChanged(control);
                break;
        }
    }

    private void SoftwareControlValueChanged(IControl control)
    {
        int index = control.Sensor?.Index ?? 0;

        NvApi.NvCoolerLevels coolerLevels = new() { Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvCoolerLevels>(1), Levels = new NvApi.NvLevel[NvApi.MAX_COOLERS_PER_GPU] };
        coolerLevels.Levels[0].Level = (int)control.SoftwareValue;
        coolerLevels.Levels[0].Policy = NvApi.NvLevelPolicy.Manual;
        if (NvApi.NvAPI_GPU_SetCoolerLevels(_handle, index, ref coolerLevels) == NvApi.NvStatus.OK)
            return;

        NvApi.NvFanCoolerControl fanCoolersControllers = GetFanCoolersControllers(out _);

        for (int i = 0; i < fanCoolersControllers.Count; i++)
        {
            NvApi.NvFanCoolerControlItem nvFanCoolerControlItem = fanCoolersControllers.Items[i];
            if (nvFanCoolerControlItem.CoolerId == index)
            {
                nvFanCoolerControlItem.ControlMode = NvApi.NvFanControlMode.Manual;
                nvFanCoolerControlItem.Level = (uint)control.SoftwareValue;

                fanCoolersControllers.Items[i] = nvFanCoolerControlItem;
            }
        }

        NvApi.NvAPI_GPU_ClientFanCoolersSetControl(_handle, ref fanCoolersControllers);
    }

    private void RestoreDefaultFanBehavior(int index)
    {
        NvApi.NvCoolerLevels coolerLevels = new() { Version = (uint)NvApi.MAKE_NVAPI_VERSION<NvApi.NvCoolerLevels>(1), Levels = new NvApi.NvLevel[NvApi.MAX_COOLERS_PER_GPU] };
        coolerLevels.Levels[0].Policy = NvApi.NvLevelPolicy.Auto;
        if (NvApi.NvAPI_GPU_SetCoolerLevels(_handle, index, ref coolerLevels) == NvApi.NvStatus.OK)
            return;

        NvApi.NvFanCoolerControl fanCoolersControllers = GetFanCoolersControllers(out _);

        for (int i = 0; i < fanCoolersControllers.Count; i++)
        {
            NvApi.NvFanCoolerControlItem nvFanCoolerControlItem = fanCoolersControllers.Items[i];
            if (nvFanCoolerControlItem.CoolerId == index)
            {
                nvFanCoolerControlItem.ControlMode = NvApi.NvFanControlMode.Auto;
                nvFanCoolerControlItem.Level = 0;

                fanCoolersControllers.Items[i] = nvFanCoolerControlItem;
            }
        }

        NvApi.NvAPI_GPU_ClientFanCoolersSetControl(_handle, ref fanCoolersControllers);
    }

    public override void Close()
    {
        _nvidiaThermal?.Close();

        if (_fanControls != null)
        {
            for (int i = 0; i < _fanControls.Length; i++)
            {
                _fanControls[i].ControlModeChanged -= ControlModeChanged;
                _fanControls[i].SoftwareControlValueChanged -= SoftwareControlValueChanged;

                if (_fanControls[i].ControlMode != ControlMode.Undefined)
                    RestoreDefaultFanBehavior(i);
            }
        }

        base.Close();
    }
}
