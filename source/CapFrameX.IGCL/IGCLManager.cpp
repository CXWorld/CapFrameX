#include "pch.h"
#include "IGCLManager.h"
#include <crtdbg.h>
#include <stdio.h>
#include <stdlib.h>

ctl_api_handle_t hAPIHandle;
ctl_device_adapter_handle_t* hDevices;

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

		if (CTL_RESULT_SUCCESS != result)
		{
			CloseIgcl();
		}
	}

	return adapter_count;
}

IgclDeviceInfo GetDeviceInfo(uint32_t index)
{
	IgclDeviceInfo deviceInfo;

	if (NULL != hDevices[index])
	{
		ctl_result_t result;
		ctl_device_adapter_properties_t StDeviceAdapterProperties = { 0 };

		StDeviceAdapterProperties.Size = sizeof(ctl_device_adapter_properties_t);
		StDeviceAdapterProperties.pDeviceID = malloc(sizeof(LUID));
		StDeviceAdapterProperties.device_id_size = sizeof(LUID);

		if (NULL == StDeviceAdapterProperties.pDeviceID)
		{
			return deviceInfo;
		}

		result = ctlGetDeviceProperties(hDevices[index], &StDeviceAdapterProperties);

		if (result != CTL_RESULT_SUCCESS)
		{
			return deviceInfo;
		}

		if (CTL_DEVICE_TYPE_GRAPHICS != StDeviceAdapterProperties.device_type)
		{
			if (NULL != StDeviceAdapterProperties.pDeviceID)
			{
				free(StDeviceAdapterProperties.pDeviceID);
			}

			return deviceInfo;
		}

		if (NULL != StDeviceAdapterProperties.pDeviceID)
		{
			deviceInfo.AdapterID = (reinterpret_cast<LUID*>(StDeviceAdapterProperties.pDeviceID))->LowPart;
		}

		strncpy_s(deviceInfo.DeviceName, StDeviceAdapterProperties.name, CTL_MAX_DEVICE_NAME_LEN);

		deviceInfo.Pci_vendor_id = StDeviceAdapterProperties.pci_vendor_id;
		deviceInfo.Pci_device_id = StDeviceAdapterProperties.pci_device_id;
		deviceInfo.Rev_id = StDeviceAdapterProperties.rev_id;
	}

	deviceInfo.Isvalid = true;
	return deviceInfo;
}


IgclTelemetryData GetIgclTelemetryData(uint32_t index)
{
	IgclTelemetryData telemetryData;
	ctl_power_telemetry_t pPowerTelemetry = {};
	pPowerTelemetry.Size = sizeof(ctl_power_telemetry_t);

	if (NULL != hDevices[index])
	{
		ctl_result_t status = ctlPowerTelemetryGet(hDevices[index], &pPowerTelemetry);

		if (status == ctl_result_t::CTL_RESULT_SUCCESS)
		{
			telemetryData.gpuEnergyCounterSupported = pPowerTelemetry.gpuEnergyCounter.bSupported;
			telemetryData.gpuEnergyCounterValue = pPowerTelemetry.gpuEnergyCounter.value.datafloat;

			telemetryData.gpuVoltageSupported = pPowerTelemetry.gpuVoltage.bSupported;
			telemetryData.gpuVoltagValue = pPowerTelemetry.gpuVoltage.value.datafloat;

			telemetryData.gpuCurrentClockFrequencySupported = pPowerTelemetry.gpuCurrentClockFrequency.bSupported;
			telemetryData.gpuCurrentClockFrequencyValue = pPowerTelemetry.gpuCurrentClockFrequency.value.datafloat;

			telemetryData.gpuCurrentTemperatureSupported = pPowerTelemetry.gpuCurrentTemperature.bSupported;
			telemetryData.gpuCurrentTemperatureValue = pPowerTelemetry.gpuCurrentTemperature.value.datafloat;

			telemetryData.globalActivityCounterSupported = pPowerTelemetry.globalActivityCounter.bSupported;
			telemetryData.globalActivityCounterValue = pPowerTelemetry.globalActivityCounter.value.datafloat;

			telemetryData.renderComputeActivityCounterSupported = pPowerTelemetry.renderComputeActivityCounter.bSupported;
			telemetryData.renderComputeActivityCounterValue = pPowerTelemetry.renderComputeActivityCounter.value.datafloat;

			telemetryData.mediaActivityCounterSupported = pPowerTelemetry.mediaActivityCounter.bSupported;
			telemetryData.mediaActivityCounterValue = pPowerTelemetry.mediaActivityCounter.value.datafloat;

			telemetryData.vramEnergyCounterSupported = pPowerTelemetry.vramEnergyCounter.bSupported;
			telemetryData.vramEnergyCounterValue = pPowerTelemetry.vramEnergyCounter.value.datafloat;

			telemetryData.vramVoltageSupported = pPowerTelemetry.vramVoltage.bSupported;
			telemetryData.vramVoltageValue = pPowerTelemetry.vramVoltage.value.datafloat;

			telemetryData.vramCurrentClockFrequencySupported = pPowerTelemetry.vramCurrentClockFrequency.bSupported;
			telemetryData.vramCurrentClockFrequencyValue = pPowerTelemetry.vramCurrentClockFrequency.value.datafloat;

			telemetryData.vramReadBandwidthCounterSupported = pPowerTelemetry.vramReadBandwidthCounter.bSupported;
			telemetryData.vramReadBandwidthCounterValue = pPowerTelemetry.vramReadBandwidthCounter.value.datafloat;

			telemetryData.vramWriteBandwidthCounterSupported = pPowerTelemetry.vramWriteBandwidthCounter.bSupported;
			telemetryData.vramWriteBandwidthCounterValue = pPowerTelemetry.vramWriteBandwidthCounter.value.datafloat;

			telemetryData.vramCurrentTemperatureSupported = pPowerTelemetry.vramCurrentTemperature.bSupported;
			telemetryData.vramCurrentTemperatureValue = pPowerTelemetry.vramCurrentTemperature.value.datafloat;

			telemetryData.fanSpeedSupported = pPowerTelemetry.fanSpeed[0].bSupported;
			telemetryData.fanSpeedValue = pPowerTelemetry.fanSpeed[0].value.datafloat;
		}
	}

	return telemetryData;
}