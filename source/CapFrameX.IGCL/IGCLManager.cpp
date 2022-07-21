#include "pch.h"
#include "IGCLManager.h"
#include <crtdbg.h>
#include <stdio.h>
#include <stdlib.h>

double deltatimestamp = 0;
double prevtimestamp = 0;
double curtimestamp = 0;
double prevgpuEnergyCounter = 0;
double curgpuEnergyCounter = 0;
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

		char driverVersion[CTL_MAX_DRIVER_VERSION_LEN] = "";
		LARGE_INTEGER LIDriverVersion;
		LIDriverVersion.QuadPart = StDeviceAdapterProperties.driver_version;
		sprintf_s(driverVersion, "%d.%d.%d.%d", HIWORD(LIDriverVersion.HighPart), LOWORD(LIDriverVersion.HighPart), HIWORD(LIDriverVersion.LowPart), LOWORD(LIDriverVersion.LowPart));
		strncpy_s(deviceInfo->DriverVersion, driverVersion, CTL_MAX_DRIVER_VERSION_LEN);
	}

	return true;
}

bool GetIgclTelemetryData(const uint32_t index, IgclTelemetryData* telemetryData)
{
	bool check = true;
	ctl_power_telemetry_t pPowerTelemetry = {};
	pPowerTelemetry.Size = sizeof(ctl_power_telemetry_t);

	if (NULL != hDevices[index])
	{
		ctl_result_t status = ctlPowerTelemetryGet(hDevices[index], &pPowerTelemetry);

		if (status == ctl_result_t::CTL_RESULT_SUCCESS)
		{
			prevtimestamp = curtimestamp;
			curtimestamp = pPowerTelemetry.timeStamp.value.datadouble;
			deltatimestamp = curtimestamp - prevtimestamp;

			if (pPowerTelemetry.gpuEnergyCounter.bSupported)
			{
				telemetryData->gpuEnergySupported = true;
				prevgpuEnergyCounter = curgpuEnergyCounter;
				curgpuEnergyCounter = pPowerTelemetry.gpuEnergyCounter.value.datadouble;

				telemetryData->gpuEnergyValue = (curgpuEnergyCounter - prevgpuEnergyCounter) / deltatimestamp;
			}

			telemetryData->gpuVoltageSupported = pPowerTelemetry.gpuVoltage.bSupported;
			telemetryData->gpuVoltagValue = pPowerTelemetry.gpuVoltage.value.datadouble;

			telemetryData->gpuCurrentClockFrequencySupported = pPowerTelemetry.gpuCurrentClockFrequency.bSupported;
			telemetryData->gpuCurrentClockFrequencyValue = pPowerTelemetry.gpuCurrentClockFrequency.value.datadouble;

			telemetryData->gpuCurrentTemperatureSupported = pPowerTelemetry.gpuCurrentTemperature.bSupported;
			telemetryData->gpuCurrentTemperatureValue = pPowerTelemetry.gpuCurrentTemperature.value.datadouble;

			if (pPowerTelemetry.globalActivityCounter.bSupported)
			{
				telemetryData->globalActivitySupported = true;
				prevglobalActivityCounter = curglobalActivityCounter;
				curglobalActivityCounter = pPowerTelemetry.globalActivityCounter.value.datadouble;

				telemetryData->globalActivityValue = 100 * (curglobalActivityCounter - prevglobalActivityCounter) / deltatimestamp;
			}

			if (pPowerTelemetry.renderComputeActivityCounter.bSupported)
			{
				telemetryData->renderComputeActivitySupported = true;
				prevrenderComputeActivityCounter = currenderComputeActivityCounter;
				currenderComputeActivityCounter = pPowerTelemetry.renderComputeActivityCounter.value.datadouble;

				telemetryData->renderComputeActivityValue = 100 * (currenderComputeActivityCounter - prevrenderComputeActivityCounter) / deltatimestamp;
			}

			if (pPowerTelemetry.mediaActivityCounter.bSupported)
			{
				telemetryData->mediaActivitySupported = true;
				prevmediaActivityCounter = curmediaActivityCounter;
				curmediaActivityCounter = pPowerTelemetry.mediaActivityCounter.value.datadouble;

				telemetryData->mediaActivityValue = 100 * (curmediaActivityCounter - prevmediaActivityCounter) / deltatimestamp;
			}

			if (pPowerTelemetry.vramEnergyCounter.bSupported)
			{
				telemetryData->vramEnergySupported = true;
				prevvramEnergyCounter = curvramEnergyCounter;
				curvramEnergyCounter = pPowerTelemetry.vramEnergyCounter.value.datadouble;

				telemetryData->vramEnergyValue = 100 * (curvramEnergyCounter - prevvramEnergyCounter) / deltatimestamp;
			}

			telemetryData->vramVoltageSupported = pPowerTelemetry.vramVoltage.bSupported;
			telemetryData->vramVoltageValue = pPowerTelemetry.vramVoltage.value.datadouble;

			telemetryData->vramCurrentClockFrequencySupported = pPowerTelemetry.vramCurrentClockFrequency.bSupported;
			telemetryData->vramCurrentClockFrequencyValue = pPowerTelemetry.vramCurrentClockFrequency.value.datadouble;

			if (pPowerTelemetry.vramReadBandwidthCounter.bSupported)
			{
				telemetryData->vramReadBandwidthSupported = true;
				prevvramReadBandwidthCounter = curvramReadBandwidthCounter;
				curvramReadBandwidthCounter = pPowerTelemetry.vramReadBandwidthCounter.value.datadouble;

				telemetryData->vramReadBandwidthValue = (curvramReadBandwidthCounter - prevvramReadBandwidthCounter)/ deltatimestamp;
			}

			if (pPowerTelemetry.vramWriteBandwidthCounter.bSupported)
			{
				telemetryData->vramWriteBandwidthSupported = true;
				prevvramWriteBandwidthCounter = curvramWriteBandwidthCounter;
				curvramWriteBandwidthCounter = pPowerTelemetry.vramWriteBandwidthCounter.value.datadouble;

				telemetryData->vramWriteBandwidthValue = (curvramWriteBandwidthCounter - prevvramWriteBandwidthCounter) / deltatimestamp;
			}

			telemetryData->vramCurrentTemperatureSupported = pPowerTelemetry.vramCurrentTemperature.bSupported;
			telemetryData->vramCurrentTemperatureValue = pPowerTelemetry.vramCurrentTemperature.value.datadouble;

			telemetryData->fanSpeedSupported = pPowerTelemetry.fanSpeed[0].bSupported;
			telemetryData->fanSpeedValue = pPowerTelemetry.fanSpeed[0].value.datadouble;
		}
		else
		{
			check = false;
		}
	}
	else
	{
		check = false;
	}

	return check;
}