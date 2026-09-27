using LibreHardwareMonitor.Interop;

namespace LibreHardwareMonitor.Hardware.Gpu;

internal sealed class IntelGclGpu : GenericGpu
{
    private readonly uint _index;
    private readonly int _busNumber;
    private readonly int _deviceNumber;
    private readonly int _busWidth;
    private string _driverVersion;

    private readonly Sensor _temperatureCore;
    private readonly Sensor _temperatureMemory;
    private readonly Sensor _temperatureCoreVr;
    private readonly Sensor _temperatureMemoryVr;
    private readonly Sensor _temperatureSaVr;

    private readonly Sensor _powerTdp;
    private readonly Sensor _powerTbp;

    private readonly Sensor _powerVram;

    private readonly Sensor _clockCore;
    private readonly Sensor _clockVram;
    private readonly Sensor _clockCoreEffective;

    private readonly Sensor _voltageCore;
    private readonly Sensor _voltageVram;

    private readonly Sensor _usageCore;
    private readonly Sensor _usageRenderEngine;
    private readonly Sensor _usageMediaEngine;

    // Percentages of the power, thermal and over-voltage budgets
    private readonly Sensor _budgetPower;
    private readonly Sensor _budgetThermal;
    private readonly Sensor _budgetOverVoltage;

    private readonly Sensor _bandwidthReadVram;
    private readonly Sensor _bandwidthWriteVram;

    private readonly Sensor _speedFan;
    private readonly Sensor[] _speedAdditionalFans;

    // Created when a rail is first reported: their names depend on the connector type.
    private const int PsuRailCount = 5;
    private readonly Sensor[] _powerPsuRails = new Sensor[PsuRailCount];
    private readonly Sensor[] _voltagePsuRails = new Sensor[PsuRailCount];

    public IntelGclGpu(uint index, IgclDeviceInfo deviceInfo, ISettings settings)
        : base(deviceInfo.DeviceName, new Identifier("gpu-intel", index.ToString()), settings, enableProcessMemorySensors: false)
    {
        _index = index;

        // See _ctl_adapter_properties_flag_t in igcl_api header for details
        // CTL_ADAPTER_PROPERTIES_FLAG_INTEGRATED = CTL_BIT(0)
        IsDiscreteGpu = deviceInfo.Adapter_Property_Flag != 1;

        _index = index;
        _busNumber = deviceInfo.AdapterID;
        _deviceNumber = (int)deviceInfo.Pci_device_id;
        _busWidth = (int)IGCL.GetBusWidth(index);
        _driverVersion = deviceInfo.DriverVersion;

        _temperatureCore = new Sensor("GPU Core", 0, SensorType.Temperature, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_2_0" };
        _temperatureMemory = new Sensor("GPU Memory", 1, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{index}_2_1" };

        // Names of the additional sensors must not contain "GPU Core": session data and the
        // basic sensor/overlay presets pick the core temperature, clock and load by that name.
        _temperatureCoreVr = new Sensor("GPU VR", 2, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{index}_2_2" };
        _temperatureMemoryVr = new Sensor("GPU Memory VR", 3, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{index}_2_3" };
        _temperatureSaVr = new Sensor("GPU SA VR", 4, SensorType.Temperature, this, settings)
        { PresentationSortKey = $"{index}_2_4" };

        _powerTbp = new Sensor("GPU TBP", 1, SensorType.Power, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_3_0" };
        _powerTdp = new Sensor("GPU TDP", 0, SensorType.Power, this, settings)
        { PresentationSortKey = $"{index}_3_1" };
        _powerVram = new Sensor("GPU VRAM", 2, SensorType.Power, this, settings)
        { PresentationSortKey = $"{index}_3_2" };

        _clockCore = new Sensor("GPU Core", 0, SensorType.Clock, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_0_0" };
        _clockVram = new Sensor("GPU Memory", 1, SensorType.Clock, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_0_1" };
        _clockCoreEffective = new Sensor("GPU Effective", 2, SensorType.Clock, this, settings)
        { PresentationSortKey = $"{index}_0_2" };

        _voltageCore = new Sensor("GPU Core", 0, SensorType.Voltage, this, settings)
        { PresentationSortKey = $"{index}_4_0" };
        _voltageVram = new Sensor("GPU Memory", 1, SensorType.Voltage, this, settings)
        { PresentationSortKey = $"{index}_4_1" };

        _usageCore = new Sensor("GPU Core", 0, SensorType.Load, this, settings)
        { IsPresentationDefault = true, PresentationSortKey = $"{index}_1_0" };
        _usageRenderEngine = new Sensor("GPU Computing", 1, SensorType.Load, this, settings)
        { PresentationSortKey = $"{index}_1_1" };
        _usageMediaEngine = new Sensor("GPU Media Engine", 2, SensorType.Load, this, settings)
        { PresentationSortKey = $"{index}_1_2" };

        _budgetPower = new Sensor("GPU Power Budget", 3, SensorType.Load, this, settings)
        { PresentationSortKey = $"{index}_1_3" };
        _budgetThermal = new Sensor("GPU Thermal Budget", 4, SensorType.Load, this, settings)
        { PresentationSortKey = $"{index}_1_4" };
        _budgetOverVoltage = new Sensor("GPU Overvoltage", 5, SensorType.Load, this, settings)
        { PresentationSortKey = $"{index}_1_5" };

        _bandwidthReadVram = new Sensor("GPU Memory Read", 4, SensorType.Throughput, this, settings)
        { PresentationSortKey = $"{index}_6_0" };
        _bandwidthWriteVram = new Sensor("GPU Memory Write", 5, SensorType.Throughput, this, settings)
        { PresentationSortKey = $"{index}_6_1" };

        _speedFan = new Sensor("GPU Fan", 0, SensorType.Fan, this, settings)
        { PresentationSortKey = $"{index}_5_0" };

        // Numbered from 2 so the first fan keeps its established name (it is part of the
        // stable sensor identifier).
        _speedAdditionalFans = new Sensor[4];
        for (int i = 0; i < _speedAdditionalFans.Length; i++)
        {
            _speedAdditionalFans[i] = new Sensor($"GPU Fan {i + 2}", i + 1, SensorType.Fan, this, settings)
            { PresentationSortKey = $"{index}_5_{i + 1}" };
        }

        Update();
    }

    public override string DeviceId => Identifier.ToString();

    public override HardwareType HardwareType => HardwareType.GpuIntel;

    public bool IsValid { get; private set; } = true;

    public override string GetDriverVersion()
        => !string.IsNullOrWhiteSpace(_driverVersion) ? _driverVersion.ToString() : "Unknown";

    public override void Update()
    {
        UpdateProcessMemorySensors();

        // Get telemetry data from IGCL
        var igclTelemetryData = new IgclTelemetryData();
        try
        {
            IGCL.GetIgclTelemetryData(_index, ref igclTelemetryData);
        }
        catch { return; }

        // GPU Core Temperature
        if (igclTelemetryData.gpuCurrentTemperatureSupported)
        {
            _temperatureCore.Value = (float)igclTelemetryData.gpuCurrentTemperatureValue;
            ActivateSensor(_temperatureCore);
        }
        else
        {
            _temperatureCore.Value = null;
        }

        // VRAM Temperature
        if (igclTelemetryData.vramCurrentTemperatureSupported)
        {
            _temperatureMemory.Value = (float)igclTelemetryData.vramCurrentTemperatureValue;
            ActivateSensor(_temperatureMemory);
        }
        else
        {
            _temperatureMemory.Value = null;
        }

        // VR Temperatures
        UpdateSensor(_temperatureCoreVr, igclTelemetryData.gpuVrTemperatureSupported, igclTelemetryData.gpuVrTemperatureValue);
        UpdateSensor(_temperatureMemoryVr, igclTelemetryData.vramVrTemperatureSupported, igclTelemetryData.vramVrTemperatureValue);
        UpdateSensor(_temperatureSaVr, igclTelemetryData.saVrTemperatureSupported, igclTelemetryData.saVrTemperatureValue);

        // GPU Core Power
        if (igclTelemetryData.gpuEnergySupported)
        {
            _powerTdp.Value = (float)igclTelemetryData.gpuEnergyValue;
            ActivateSensor(_powerTdp);
        }
        else
        {
            _powerTdp.Value = null;
        }

        // GPU Total Board Power
        if (igclTelemetryData.totalCardEnergySupported)
        {
            _powerTbp.Value = (float)igclTelemetryData.totalCardEnergyValue;
            ActivateSensor(_powerTbp);
        }
        else
        {
            _powerTbp.Value = null;
        }

        // VRAM Temperature
        if (igclTelemetryData.vramEnergySupported)
        {
            _powerVram.Value = (float)igclTelemetryData.vramEnergyValue;
            ActivateSensor(_powerVram);
        }
        else
        {
            _powerVram.Value = null;
        }

        // GPU Core Frequency
        if (igclTelemetryData.gpuCurrentClockFrequencySupported)
        {
            _clockCore.Value = (float)igclTelemetryData.gpuCurrentClockFrequencyValue;
            ActivateSensor(_clockCore);
        }
        else
        {
            _clockCore.Value = null;
        }

        // VRAM Frequency
        if (igclTelemetryData.vramCurrentClockFrequencySupported)
        {
            _clockVram.Value = (float)igclTelemetryData.vramCurrentClockFrequencyValue;
            ActivateSensor(_clockVram);
        }
        else
        {
            _clockVram.Value = null;
        }

        // GPU Effective Frequency
        UpdateSensor(_clockCoreEffective, igclTelemetryData.gpuEffectiveClockSupported, igclTelemetryData.gpuEffectiveClockValue);

        // GPU Core Frequency
        if (igclTelemetryData.gpuVoltageSupported)
        {
            _voltageCore.Value = (float)igclTelemetryData.gpuVoltagValue;
            ActivateSensor(_voltageCore);
        }
        else
        {
            _voltageCore.Value = null;
        }

        // VRAM Voltage
        if (igclTelemetryData.vramVoltageSupported)
        {
            _voltageVram.Value = (float)igclTelemetryData.vramVoltageValue;
            ActivateSensor(_voltageVram);
        }
        else
        {
            _voltageVram.Value = null;
        }

        // GPU Usage
        if (igclTelemetryData.globalActivitySupported)
        {
            _usageCore.Value = (float)igclTelemetryData.globalActivityValue;
            ActivateSensor(_usageCore);
        }
        else
        {
            _usageCore.Value = null;
        }

        // Render Engine Usage
        if (igclTelemetryData.renderComputeActivitySupported)
        {
            _usageRenderEngine.Value = (float)igclTelemetryData.renderComputeActivityValue;
            ActivateSensor(_usageRenderEngine);
        }
        else
        {
            _usageRenderEngine.Value = null;
        }

        // Media Engine Usage
        if (igclTelemetryData.mediaActivitySupported)
        {
            _usageMediaEngine.Value = (float)igclTelemetryData.mediaActivityValue;
            ActivateSensor(_usageMediaEngine);
        }
        else
        {
            _usageMediaEngine.Value = null;
        }

        // Power, thermal and over-voltage budgets (%)
        UpdateSensor(_budgetPower, igclTelemetryData.gpuPowerPercentSupported, igclTelemetryData.gpuPowerPercentValue);
        UpdateSensor(_budgetThermal, igclTelemetryData.gpuTemperaturePercentSupported, igclTelemetryData.gpuTemperaturePercentValue);
        UpdateSensor(_budgetOverVoltage, igclTelemetryData.gpuOverVoltagePercentSupported, igclTelemetryData.gpuOverVoltagePercentValue);

        // VRAM Read Bandwidth: prefer the driver's direct GB/s value over the counter-based estimate
        if (igclTelemetryData.vramReadBandwidthGBpsSupported)
        {
            _bandwidthReadVram.Value = (float)igclTelemetryData.vramReadBandwidthGBpsValue;
            ActivateSensor(_bandwidthReadVram);
        }
        else if (igclTelemetryData.vramReadBandwidthSupported)
        {
            _bandwidthReadVram.Value = (float)(igclTelemetryData.vramReadBandwidthValue * _busWidth / 1024);
            ActivateSensor(_bandwidthReadVram);
        }
        else
        {
            _bandwidthReadVram.Value = null;
        }

        // VRAM Write Bandwidth
        if (igclTelemetryData.vramWriteBandwidthGBpsSupported)
        {
            _bandwidthWriteVram.Value = (float)igclTelemetryData.vramWriteBandwidthGBpsValue;
            ActivateSensor(_bandwidthWriteVram);
        }
        else if (igclTelemetryData.vramWriteBandwidthSupported)
        {
            _bandwidthWriteVram.Value = (float)(igclTelemetryData.vramWriteBandwidthValue * _busWidth / 1024);
            ActivateSensor(_bandwidthWriteVram);
        }
        else
        {
            _bandwidthWriteVram.Value = null;
        }

        // Fanspeed (n Fans)
        if (igclTelemetryData.fanSpeedSupported)
        {
            _speedFan.Value = (float)igclTelemetryData.fanSpeedValue;
            ActivateSensor(_speedFan);
        }
        else
        {
            _speedFan.Value = null;
        }

        IgclTelemetryItem[] additionalFans =
        {
            igclTelemetryData.fan2Speed, igclTelemetryData.fan3Speed, igclTelemetryData.fan4Speed, igclTelemetryData.fan5Speed
        };

        for (int i = 0; i < additionalFans.Length; i++)
            UpdateSensor(_speedAdditionalFans[i], additionalFans[i].supported, additionalFans[i].value);

        // Power supply rails (power and voltage per connector)
        IgclPsuRail[] rails =
        {
            igclTelemetryData.psu1, igclTelemetryData.psu2, igclTelemetryData.psu3, igclTelemetryData.psu4, igclTelemetryData.psu5
        };

        for (int i = 0; i < rails.Length; i++)
        {
            if (_powerPsuRails[i] == null)
            {
                if (!IsPsuRailReported(rails[i]))
                    continue;

                string name = GetPsuRailName(i, rails);
                _powerPsuRails[i] = new Sensor(name, 3 + i, SensorType.Power, this, _settings)
                { PresentationSortKey = $"{_index}_3_{3 + i}" };
                _voltagePsuRails[i] = new Sensor(name, 2 + i, SensorType.Voltage, this, _settings)
                { PresentationSortKey = $"{_index}_4_{2 + i}" };
            }

            UpdateSensor(_powerPsuRails[i], rails[i].power.supported, rails[i].power.value);
            UpdateSensor(_voltagePsuRails[i], rails[i].voltage.supported, rails[i].voltage.value);
        }
    }

    private static bool IsPsuRailReported(IgclPsuRail rail) => rail.power.supported || rail.voltage.supported;

    // "GPU 8-Pin", or "GPU 8-Pin 1" / "GPU 8-Pin 2" when the card has several connectors of that type.
    internal static string GetPsuRailName(int rail, IgclPsuRail[] rails)
    {
        int type = rails[rail].type;
        string baseName = type switch
        {
            IGCL.CTL_PSU_TYPE_PSU_PCIE => "GPU PCIe Slot",
            IGCL.CTL_PSU_TYPE_PSU_6PIN => "GPU 6-Pin",
            IGCL.CTL_PSU_TYPE_PSU_8PIN => "GPU 8-Pin",
            _ => "GPU PSU"
        };

        int count = 0;
        int ordinal = 0;
        for (int i = 0; i < rails.Length; i++)
        {
            if (!IsPsuRailReported(rails[i]) || rails[i].type != type)
                continue;

            count++;
            if (i == rail)
                ordinal = count;
        }

        return count > 1 ? $"{baseName} {ordinal}" : baseName;
    }

    private void UpdateSensor(Sensor sensor, bool supported, double value)
    {
        if (supported)
        {
            sensor.Value = (float)value;
            ActivateSensor(sensor);
        }
        else
        {
            sensor.Value = null;
        }
    }
}
