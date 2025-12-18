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

// Keep ALL "prev/cur" values per device index.
struct TelemetryDeltaState
{
    bool   initialized = false;

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
						if (memoryProperties.busWidth > busWidth)
						{
							busWidth = memoryProperties.busWidth;
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

bool GetIgclTelemetryData(const uint32_t index, IgclTelemetryData* telemetryData)
{
    if (!telemetryData) return false;
    if (index >= g_state.size()) return false;
    if (hDevices[index] == NULL) return false;

    ctl_power_telemetry_t pPowerTelemetry = {};
    pPowerTelemetry.Size = sizeof(ctl_power_telemetry_t);

    ctl_result_t status = ctlPowerTelemetryGet(hDevices[index], &pPowerTelemetry);
    if (status != ctl_result_t::CTL_RESULT_SUCCESS) return false;

    auto& st = g_state[index];

    // Update timestamps per device
    st.prevTimestamp = st.curTimestamp;
    st.curTimestamp = pPowerTelemetry.timeStamp.value.datadouble;

    const double dt = st.curTimestamp - st.prevTimestamp;

    // First call (or invalid dt): capture counters but do not compute rates yet.
    const bool canComputeRates = st.initialized && IsValidDelta(dt);

    // GPU energy rate
    if (pPowerTelemetry.gpuEnergyCounter.bSupported)
    {
        telemetryData->gpuEnergySupported = true;

        st.prevGpuEnergy = st.curGpuEnergy;
        st.curGpuEnergy = pPowerTelemetry.gpuEnergyCounter.value.datadouble;

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
        st.curTotalCardEnergy = pPowerTelemetry.totalCardEnergyCounter.value.datadouble;

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
    telemetryData->gpuVoltagValue = pPowerTelemetry.gpuVoltage.value.datadouble;

    telemetryData->gpuCurrentClockFrequencySupported = pPowerTelemetry.gpuCurrentClockFrequency.bSupported;
    telemetryData->gpuCurrentClockFrequencyValue = pPowerTelemetry.gpuCurrentClockFrequency.value.datadouble;

    telemetryData->gpuCurrentTemperatureSupported = pPowerTelemetry.gpuCurrentTemperature.bSupported;
    telemetryData->gpuCurrentTemperatureValue = pPowerTelemetry.gpuCurrentTemperature.value.datadouble;

    // Global activity rate
    if (pPowerTelemetry.globalActivityCounter.bSupported)
    {
        telemetryData->globalActivitySupported = true;

        st.prevGlobalActivity = st.curGlobalActivity;
        st.curGlobalActivity = pPowerTelemetry.globalActivityCounter.value.datadouble;

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
        st.curRenderComputeActivity = pPowerTelemetry.renderComputeActivityCounter.value.datadouble;

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
        st.curMediaActivity = pPowerTelemetry.mediaActivityCounter.value.datadouble;

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
        st.curVramEnergy = pPowerTelemetry.vramEnergyCounter.value.datadouble;

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
    telemetryData->vramVoltageValue = pPowerTelemetry.vramVoltage.value.datadouble;

    telemetryData->vramCurrentClockFrequencySupported = pPowerTelemetry.vramCurrentClockFrequency.bSupported;
    telemetryData->vramCurrentClockFrequencyValue = pPowerTelemetry.vramCurrentClockFrequency.value.datadouble;

    telemetryData->vramReadBandwidthSupported = pPowerTelemetry.vramReadBandwidthCounter.bSupported;
    telemetryData->vramReadBandwidthValue = pPowerTelemetry.vramReadBandwidthCounter.value.datadouble;

    telemetryData->vramWriteBandwidthSupported = pPowerTelemetry.vramWriteBandwidthCounter.bSupported;
    telemetryData->vramWriteBandwidthValue = pPowerTelemetry.vramWriteBandwidthCounter.value.datadouble;

    telemetryData->vramCurrentTemperatureSupported = pPowerTelemetry.vramCurrentTemperature.bSupported;
    telemetryData->vramCurrentTemperatureValue = pPowerTelemetry.vramCurrentTemperature.value.datadouble;

    telemetryData->fanSpeedSupported = pPowerTelemetry.fanSpeed[0].bSupported;
    telemetryData->fanSpeedValue = pPowerTelemetry.fanSpeed[0].value.datadouble;

    st.initialized = true;
    return true;
}
