#include "pch.h"
#include "IGCLManager.h"
#include <crtdbg.h>
#include <stdio.h>
#include <stdlib.h>
#include <stdexcept>
#include <vector>
#include <cstdint>
#include <cmath>

double deltatimestamp = 0;
double prevtimestamp = 0;
double curtimestamp = 0;
double prevgpuEnergyCounter = 0;
double curgpuEnergyCounter = 0;
double prevtotalCardEnergyCounter = 0;
double curtotalCardEnergyCounter = 0;
double curglobalActivityCounter = 0;
double prevglobalActivityCounter = 0;
double currenderComputeActivityCounter = 0;
double prevrenderComputeActivityCounter = 0;
double curmediaActivityCounter = 0;
double prevmediaActivityCounter = 0;
double curvramEnergyCounter = 0;
double prevvramEnergyCounter = 0;
double curvramReadBandwidthCounter = 0;
double prevvramReadBandwidthCounter = 0;
double curvramWriteBandwidthCounter = 0;
double prevvramWriteBandwidthCounter = 0;

ctl_api_handle_t hAPIHandle;
ctl_device_adapter_handle_t* hDevices;

// Highest ctl_power_telemetry_t version documented by igcl_api.h. Version 1 adds the VR
// temperatures, the effective GPU clock, the power/thermal/overvoltage percentages and the
// direct VRAM bandwidth; hardware without them still reports bSupported = false.
static constexpr uint8_t MaxPowerTelemetryVersion = 1;

// Keep ALL "prev/cur" values per device index.
struct TelemetryDeltaState
{
    bool   initialized = false;

    // Structure version requested from the driver; lowered once a driver rejects it.
    uint8_t telemetryVersion = MaxPowerTelemetryVersion;

    double prevTimestamp = 0.0;
    double curTimestamp = 0.0;

    double prevGpuEnergy = 0.0;
    double curGpuEnergy = 0.0;

    double prevTotalCardEnergy = 0.0;
    double curTotalCardEnergy = 0.0;

    double prevGlobalActivity = 0.0;
    double curGlobalActivity = 0.0;

    double prevRenderComputeActivity = 0.0;
    double curRenderComputeActivity = 0.0;

    double prevMediaActivity = 0.0;
    double curMediaActivity = 0.0;

    double prevVramEnergy = 0.0;
    double curVramEnergy = 0.0;

    double prevPsuEnergy[CTL_PSU_COUNT] = {};
    double curPsuEnergy[CTL_PSU_COUNT] = {};
};

// One state entry per hDevices[] slot.
// Ensure this is sized appropriately when you discover/enumerate devices.
static std::vector<TelemetryDeltaState> g_state;

bool IntializeIgcl()
{
	ctl_result_t result = CTL_RESULT_SUCCESS;

	_CrtSetDbgFlag(_CRTDBG_ALLOC_MEM_DF | _CRTDBG_LEAK_CHECK_DF);

	ctl_init_args_t ctlInitArgs;
	ctlInitArgs.AppVersion = CTL_MAKE_VERSION(CTL_IMPL_MAJOR_VERSION, CTL_IMPL_MINOR_VERSION);
	ctlInitArgs.flags = CTL_INIT_FLAG_USE_LEVEL_ZERO;
	ctlInitArgs.Size = sizeof(ctlInitArgs);
	ctlInitArgs.Version = 0;
	ZeroMemory(&ctlInitArgs.ApplicationUID, sizeof(ctl_application_id_t));
	result = ctlInit(&ctlInitArgs, &hAPIHandle);

	return result == CTL_RESULT_SUCCESS;
}

void CloseIgcl()
{
	ctlClose(hAPIHandle);

	if (hDevices != nullptr)
	{
		free(hDevices);
		hDevices = nullptr;
	}
}

uint32_t GetAdpaterCount()
{
	ctl_result_t result;
	uint32_t adapter_count = 0;

	result = ctlEnumerateDevices(hAPIHandle, &adapter_count, hDevices);

	if (CTL_RESULT_SUCCESS == result)
	{
		hDevices = (ctl_device_adapter_handle_t*)malloc(sizeof(ctl_device_adapter_handle_t) * adapter_count);
		if (hDevices == NULL)
		{
			return 0;
		}

		result = ctlEnumerateDevices(hAPIHandle, &adapter_count, hDevices);
        g_state.resize(adapter_count);

		if (CTL_RESULT_SUCCESS != result)
		{
			CloseIgcl();
		}
	}

	return adapter_count;
}

uint32_t GetBusWidth(const uint32_t index)
{
	uint32_t busWidth = 0;

	if (NULL != hDevices[index])
	{
		uint32_t MemoryHandlerCount = 0;
		ctl_result_t res = ctlEnumMemoryModules(hDevices[index], &MemoryHandlerCount, nullptr);

		if ((res == CTL_RESULT_SUCCESS) && MemoryHandlerCount != 0)
		{
			ctl_mem_handle_t* pMemoryHandle = new ctl_mem_handle_t[MemoryHandlerCount];

			res = ctlEnumMemoryModules(hDevices[index], &MemoryHandlerCount, pMemoryHandle);

			if (res == CTL_RESULT_SUCCESS)
			{
				for (uint32_t i = 0; i < MemoryHandlerCount; i++)
				{
					ctl_mem_properties_t memoryProperties = { 0 };
					memoryProperties.Size = sizeof(ctl_mem_properties_t);
					res = ctlMemoryGetProperties(pMemoryHandle[i], &memoryProperties);

					if (res == CTL_RESULT_SUCCESS)
					{
						if (memoryProperties.busWidth > 0 &&
                            static_cast<uint32_t>(memoryProperties.busWidth) > busWidth)
						{
							busWidth = static_cast<uint32_t>(memoryProperties.busWidth);
						}
					}
				}
			}
		}
	}

	return busWidth;
}

bool GetDeviceInfo(const uint32_t index, IgclDeviceInfo* deviceInfo)
{
	if (NULL != hDevices[index])
	{
		ctl_result_t result;
		ctl_device_adapter_properties_t StDeviceAdapterProperties = { 0 };

		StDeviceAdapterProperties.Size = sizeof(ctl_device_adapter_properties_t);
		StDeviceAdapterProperties.pDeviceID = malloc(sizeof(LUID));
		StDeviceAdapterProperties.device_id_size = sizeof(LUID);

		if (NULL == StDeviceAdapterProperties.pDeviceID)
		{
			return false;
		}

		result = ctlGetDeviceProperties(hDevices[index], &StDeviceAdapterProperties);

		if (result != CTL_RESULT_SUCCESS)
		{
			return false;
		}

		if (CTL_DEVICE_TYPE_GRAPHICS != StDeviceAdapterProperties.device_type)
		{
			if (NULL != StDeviceAdapterProperties.pDeviceID)
			{
				free(StDeviceAdapterProperties.pDeviceID);
			}

			return false;
		}

		if (NULL != StDeviceAdapterProperties.pDeviceID)
		{
			deviceInfo->AdapterID = (reinterpret_cast<LUID*>(StDeviceAdapterProperties.pDeviceID))->LowPart;
		}

		strncpy_s(deviceInfo->DeviceName, StDeviceAdapterProperties.name, CTL_MAX_DEVICE_NAME_LEN);

		deviceInfo->Pci_vendor_id = StDeviceAdapterProperties.pci_vendor_id;
		deviceInfo->Pci_device_id = StDeviceAdapterProperties.pci_device_id;
		deviceInfo->Rev_id = StDeviceAdapterProperties.rev_id;
		deviceInfo->Adapter_Property_Flag = StDeviceAdapterProperties.graphics_adapter_properties;

		char driverVersion[CTL_MAX_DRIVER_VERSION_LEN] = "";
		LARGE_INTEGER LIDriverVersion;
		LIDriverVersion.QuadPart = StDeviceAdapterProperties.driver_version;
		sprintf_s(driverVersion, "%d.%d.%d.%d", HIWORD(LIDriverVersion.HighPart), LOWORD(LIDriverVersion.HighPart), HIWORD(LIDriverVersion.LowPart), LOWORD(LIDriverVersion.LowPart));
		strncpy_s(deviceInfo->DriverVersion, driverVersion, CTL_MAX_DRIVER_VERSION_LEN);
	}

	return true;
}

static inline bool IsValidDelta(double dt)
{
	return std::isfinite(dt) && dt > 0.0;
}

// Telemetry items carry their own data type; the driver is free to report integers.
static double ItemValue(const ctl_oc_telemetry_item_t& item)
{
    switch (item.type)
    {
    case CTL_DATA_TYPE_INT8: return item.value.data8;
    case CTL_DATA_TYPE_UINT8: return item.value.datau8;
    case CTL_DATA_TYPE_INT16: return item.value.data16;
    case CTL_DATA_TYPE_UINT16: return item.value.datau16;
    case CTL_DATA_TYPE_INT32: return item.value.data32;
    case CTL_DATA_TYPE_UINT32: return item.value.datau32;
    case CTL_DATA_TYPE_INT64: return static_cast<double>(item.value.data64);
    case CTL_DATA_TYPE_UINT64: return static_cast<double>(item.value.datau64);
    case CTL_DATA_TYPE_FLOAT: return item.value.datafloat;
    default: return item.value.datadouble;
    }
}

// Direct VRAM bandwidth comes in MB/s or GB/s. Anything else is reported as unsupported
// rather than published with a wrong scale.
static bool ReadBandwidthGBps(const ctl_oc_telemetry_item_t& item, double* gbps)
{
    if (!item.bSupported) return false;

    switch (item.units)
    {
    case CTL_UNITS_MEM_SPEED_GBPS:
        *gbps = ItemValue(item);
        return true;
    case CTL_UNITS_BANDWIDTH_MBPS:
        *gbps = ItemValue(item) / 1000.0;
        return true;
    default:
        return false;
    }
}

// Voltages are reported in volts unless the item says millivolts.
static double ItemVolts(const ctl_oc_telemetry_item_t& item)
{
    const double value = ItemValue(item);
    return item.units == CTL_UNITS_VOLTAGE_MILLIVOLTS ? value / 1000.0 : value;
}

// Errors that describe the device, not the requested structure version.
static bool IsDeviceStateError(ctl_result_t status)
{
    return status == CTL_RESULT_ERROR_DEVICE_LOST
        || status == CTL_RESULT_ERROR_DEVICE_UNAVAILABLE
        || status == CTL_RESULT_ERROR_UNINITIALIZED
        || status == CTL_RESULT_ERROR_INVALID_NULL_HANDLE;
}

// Requests the newest telemetry version the driver accepts. A lower version is only kept once
// it has actually succeeded, so a transient failure cannot cost the newer items for the session.
static ctl_result_t QueryPowerTelemetry(ctl_device_adapter_handle_t hDevice, TelemetryDeltaState& st,
    ctl_power_telemetry_t* telemetry)
{
    ctl_result_t status = CTL_RESULT_ERROR_NOT_INITIALIZED;

    for (int version = st.telemetryVersion; version >= 0; --version)
    {
        *telemetry = {};
        telemetry->Size = sizeof(ctl_power_telemetry_t);
        telemetry->Version = static_cast<uint8_t>(version);

        status = ctlPowerTelemetryGet(hDevice, telemetry);
        if (status == CTL_RESULT_SUCCESS)
        {
            st.telemetryVersion = static_cast<uint8_t>(version);
            break;
        }

        if (IsDeviceStateError(status))
            break;
    }

    return status;
}

bool GetIgclTelemetryData(const uint32_t index, IgclTelemetryData* telemetryData)
{
    if (!telemetryData) return false;
    if (index >= g_state.size()) return false;
    if (hDevices[index] == NULL) return false;

    auto& st = g_state[index];

    ctl_power_telemetry_t pPowerTelemetry;
    ctl_result_t status = QueryPowerTelemetry(hDevices[index], st, &pPowerTelemetry);
    if (status != ctl_result_t::CTL_RESULT_SUCCESS) return false;

    // Update timestamps per device
    st.prevTimestamp = st.curTimestamp;
    st.curTimestamp = ItemValue(pPowerTelemetry.timeStamp);

    const double dt = st.curTimestamp - st.prevTimestamp;

    // First call (or invalid dt): capture counters but do not compute rates yet.
    const bool canComputeRates = st.initialized && IsValidDelta(dt);

    // GPU energy rate
    if (pPowerTelemetry.gpuEnergyCounter.bSupported)
    {
        telemetryData->gpuEnergySupported = true;

        st.prevGpuEnergy = st.curGpuEnergy;
        st.curGpuEnergy = ItemValue(pPowerTelemetry.gpuEnergyCounter);

        if (canComputeRates)
            telemetryData->gpuEnergyValue = (st.curGpuEnergy - st.prevGpuEnergy) / dt;
        else
            telemetryData->gpuEnergyValue = 0.0;
    }
    else
    {
        telemetryData->gpuEnergySupported = false;
    }

    // Total card energy rate
    if (pPowerTelemetry.totalCardEnergyCounter.bSupported)
    {
        telemetryData->totalCardEnergySupported = true;

        st.prevTotalCardEnergy = st.curTotalCardEnergy;
        st.curTotalCardEnergy = ItemValue(pPowerTelemetry.totalCardEnergyCounter);

        if (canComputeRates)
            telemetryData->totalCardEnergyValue = (st.curTotalCardEnergy - st.prevTotalCardEnergy) / dt;
        else
            telemetryData->totalCardEnergyValue = 0.0;
    }
    else
    {
        telemetryData->totalCardEnergySupported = false;
    }

    telemetryData->gpuVoltageSupported = pPowerTelemetry.gpuVoltage.bSupported;
    telemetryData->gpuVoltagValue = ItemValue(pPowerTelemetry.gpuVoltage);

    telemetryData->gpuCurrentClockFrequencySupported = pPowerTelemetry.gpuCurrentClockFrequency.bSupported;
    telemetryData->gpuCurrentClockFrequencyValue = ItemValue(pPowerTelemetry.gpuCurrentClockFrequency);

    telemetryData->gpuCurrentTemperatureSupported = pPowerTelemetry.gpuCurrentTemperature.bSupported;
    telemetryData->gpuCurrentTemperatureValue = ItemValue(pPowerTelemetry.gpuCurrentTemperature);

    // Global activity rate
    if (pPowerTelemetry.globalActivityCounter.bSupported)
    {
        telemetryData->globalActivitySupported = true;

        st.prevGlobalActivity = st.curGlobalActivity;
        st.curGlobalActivity = ItemValue(pPowerTelemetry.globalActivityCounter);

        if (canComputeRates)
            telemetryData->globalActivityValue = 100.0 * (st.curGlobalActivity - st.prevGlobalActivity) / dt;
        else
            telemetryData->globalActivityValue = 0.0;
    }
    else
    {
        telemetryData->globalActivitySupported = false;
    }

    // Render/compute activity rate
    if (pPowerTelemetry.renderComputeActivityCounter.bSupported)
    {
        telemetryData->renderComputeActivitySupported = true;

        st.prevRenderComputeActivity = st.curRenderComputeActivity;
        st.curRenderComputeActivity = ItemValue(pPowerTelemetry.renderComputeActivityCounter);

        if (canComputeRates)
            telemetryData->renderComputeActivityValue =
            100.0 * (st.curRenderComputeActivity - st.prevRenderComputeActivity) / dt;
        else
            telemetryData->renderComputeActivityValue = 0.0;
    }
    else
    {
        telemetryData->renderComputeActivitySupported = false;
    }

    // Media activity rate
    if (pPowerTelemetry.mediaActivityCounter.bSupported)
    {
        telemetryData->mediaActivitySupported = true;

        st.prevMediaActivity = st.curMediaActivity;
        st.curMediaActivity = ItemValue(pPowerTelemetry.mediaActivityCounter);

        if (canComputeRates)
            telemetryData->mediaActivityValue = 100.0 * (st.curMediaActivity - st.prevMediaActivity) / dt;
        else
            telemetryData->mediaActivityValue = 0.0;
    }
    else
    {
        telemetryData->mediaActivitySupported = false;
    }

    // VRAM energy rate
    if (pPowerTelemetry.vramEnergyCounter.bSupported)
    {
        telemetryData->vramEnergySupported = true;

        st.prevVramEnergy = st.curVramEnergy;
        st.curVramEnergy = ItemValue(pPowerTelemetry.vramEnergyCounter);

        if (canComputeRates)
            telemetryData->vramEnergyValue = (st.curVramEnergy - st.prevVramEnergy) / dt;
        else
            telemetryData->vramEnergyValue = 0.0;
    }
    else
    {
        telemetryData->vramEnergySupported = false;
    }

    telemetryData->vramVoltageSupported = pPowerTelemetry.vramVoltage.bSupported;
    telemetryData->vramVoltageValue = ItemValue(pPowerTelemetry.vramVoltage);

    telemetryData->vramCurrentClockFrequencySupported = pPowerTelemetry.vramCurrentClockFrequency.bSupported;
    telemetryData->vramCurrentClockFrequencyValue = ItemValue(pPowerTelemetry.vramCurrentClockFrequency);

    telemetryData->vramReadBandwidthSupported = pPowerTelemetry.vramReadBandwidthCounter.bSupported;
    telemetryData->vramReadBandwidthValue = ItemValue(pPowerTelemetry.vramReadBandwidthCounter);

    telemetryData->vramWriteBandwidthSupported = pPowerTelemetry.vramWriteBandwidthCounter.bSupported;
    telemetryData->vramWriteBandwidthValue = ItemValue(pPowerTelemetry.vramWriteBandwidthCounter);

    telemetryData->vramCurrentTemperatureSupported = pPowerTelemetry.vramCurrentTemperature.bSupported;
    telemetryData->vramCurrentTemperatureValue = ItemValue(pPowerTelemetry.vramCurrentTemperature);

    telemetryData->fanSpeedSupported = pPowerTelemetry.fanSpeed[0].bSupported;
    telemetryData->fanSpeedValue = ItemValue(pPowerTelemetry.fanSpeed[0]);

    // Version 1 items; a driver that fell back to version 0 leaves them unsupported.
    telemetryData->gpuVrTemperatureSupported = pPowerTelemetry.gpuVrTemp.bSupported;
    telemetryData->gpuVrTemperatureValue = ItemValue(pPowerTelemetry.gpuVrTemp);

    telemetryData->vramVrTemperatureSupported = pPowerTelemetry.vramVrTemp.bSupported;
    telemetryData->vramVrTemperatureValue = ItemValue(pPowerTelemetry.vramVrTemp);

    telemetryData->saVrTemperatureSupported = pPowerTelemetry.saVrTemp.bSupported;
    telemetryData->saVrTemperatureValue = ItemValue(pPowerTelemetry.saVrTemp);

    telemetryData->gpuEffectiveClockSupported = pPowerTelemetry.gpuEffectiveClock.bSupported;
    telemetryData->gpuEffectiveClockValue = ItemValue(pPowerTelemetry.gpuEffectiveClock);

    telemetryData->gpuOverVoltagePercentSupported = pPowerTelemetry.gpuOverVoltagePercent.bSupported;
    telemetryData->gpuOverVoltagePercentValue = ItemValue(pPowerTelemetry.gpuOverVoltagePercent);

    telemetryData->gpuPowerPercentSupported = pPowerTelemetry.gpuPowerPercent.bSupported;
    telemetryData->gpuPowerPercentValue = ItemValue(pPowerTelemetry.gpuPowerPercent);

    telemetryData->gpuTemperaturePercentSupported = pPowerTelemetry.gpuTemperaturePercent.bSupported;
    telemetryData->gpuTemperaturePercentValue = ItemValue(pPowerTelemetry.gpuTemperaturePercent);

    telemetryData->vramReadBandwidthGBpsSupported =
        ReadBandwidthGBps(pPowerTelemetry.vramReadBandwidth, &telemetryData->vramReadBandwidthGBpsValue);

    telemetryData->vramWriteBandwidthGBpsSupported =
        ReadBandwidthGBps(pPowerTelemetry.vramWriteBandwidth, &telemetryData->vramWriteBandwidthGBpsValue);

    // Fans 2..5
    static_assert(CTL_FAN_COUNT == 5, "IgclTelemetryData mirrors five fans");
    IgclTelemetryItem* additionalFans[] =
    {
        &telemetryData->fan2Speed, &telemetryData->fan3Speed, &telemetryData->fan4Speed, &telemetryData->fan5Speed
    };

    for (int i = 0; i < CTL_FAN_COUNT - 1; i++)
    {
        const ctl_oc_telemetry_item_t& fan = pPowerTelemetry.fanSpeed[i + 1];
        additionalFans[i]->supported = fan.bSupported;
        additionalFans[i]->value = ItemValue(fan);
    }

    // Power supply rails: energy counter rate like the GPU and card energy above, plus voltage
    static_assert(CTL_PSU_COUNT == 5, "IgclTelemetryData mirrors five PSU rails");
    IgclPsuRail* rails[] =
    {
        &telemetryData->psu1, &telemetryData->psu2, &telemetryData->psu3, &telemetryData->psu4, &telemetryData->psu5
    };

    for (int i = 0; i < CTL_PSU_COUNT; i++)
    {
        const ctl_psu_info_t& psu = pPowerTelemetry.psu[i];
        IgclPsuRail* rail = rails[i];

        rail->type = psu.bSupported ? static_cast<int32_t>(psu.psuType) : CTL_PSU_TYPE_PSU_NONE;

        if (psu.bSupported && psu.energyCounter.bSupported)
        {
            rail->power.supported = true;

            st.prevPsuEnergy[i] = st.curPsuEnergy[i];
            st.curPsuEnergy[i] = ItemValue(psu.energyCounter);

            if (canComputeRates)
                rail->power.value = (st.curPsuEnergy[i] - st.prevPsuEnergy[i]) / dt;
            else
                rail->power.value = 0.0;
        }
        else
        {
            rail->power.supported = false;
        }

        rail->voltage.supported = psu.bSupported && psu.voltage.bSupported;
        rail->voltage.value = ItemVolts(psu.voltage);
    }

    st.initialized = true;
    return true;
}
