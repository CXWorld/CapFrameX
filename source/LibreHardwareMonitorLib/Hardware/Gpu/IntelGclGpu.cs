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

    private readonly Sensor _powerTdp;
    private readonly Sensor _powerTbp;

    private readonly Sensor _powerVram;

    private readonly Sensor _clockCore;
    private readonly Sensor _clockVram;

    private readonly Sensor _voltageCore;
    private readonly Sensor _voltageVram;

    private readonly Sensor _usageCore;
    private readonly Sensor _usageRenderEngine;
    private readonly Sensor _usageMediaEngine;

    private readonly Sensor _bandwidthReadVram;
    private readonly Sensor _bandwidthWriteVram;

    // ToDo: get all fans info
    private readonly Sensor _speedFan;

    public IntelGclGpu(uint index, IgclDeviceInfo deviceInfo, ISettings settings)
        : base(deviceInfo.DeviceName, new Identifier("gpu-intel", index.ToString()), settings)
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

        _bandwidthReadVram = new Sensor("GPU Memory Read", 4, SensorType.Throughput, this, settings)
        { PresentationSortKey = $"{index}_6_0" };
        _bandwidthWriteVram = new Sensor("GPU Memory Write", 5, SensorType.Throughput, this, settings)
        { PresentationSortKey = $"{index}_6_1" };

        _speedFan = new Sensor("GPU Fan", 0, SensorType.Fan, this, settings)
        { PresentationSortKey = $"{index}_5_0" };

        Update();
    }

    public override string DeviceId => Identifier.ToString();

    public override HardwareType HardwareType => HardwareType.GpuIntel;

    public bool IsValid { get; private set; } = true;

    public override string GetDriverVersion()
        => !string.IsNullOrWhiteSpace(_driverVersion) ? _driverVersion.ToString() : "Unknown";

    public override void Update()
    {
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

        // VRAM Read Bandwidth
        if (igclTelemetryData.vramReadBandwidthSupported)
        {
            _bandwidthReadVram.Value = (float)(igclTelemetryData.vramReadBandwidthValue * _busWidth / 1024);
            ActivateSensor(_bandwidthReadVram);
        }
        else
        {
            _bandwidthReadVram.Value = null;
        }

        // VRAM Write Bandwidth
        if (igclTelemetryData.vramWriteBandwidthSupported)
        {
            _bandwidthWriteVram.Value = (float)(igclTelemetryData.vramWriteBandwidthValue * _busWidth / 1024);
            ActivateSensor(_bandwidthWriteVram);
        }
        else
        {
            _bandwidthWriteVram.Value = null;
        }

        // ToDo: get all fans info
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
    }
}
