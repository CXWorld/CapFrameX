#include <crtdbg.h>
#include <stdio.h>
#include <stdlib.h>
#include "SDK/ADLXHelper/Windows/Cpp/ADLXHelper.h"
#include "SDK/Include/IPerformanceMonitoring.h"
#include "ADLXManager.h"

// Use ADLX namespace
using namespace adlx;

// ADLXHelper instance
// No outstanding interfaces from ADLX must exist when ADLX is destroyed.
// Use global variables to ensure validity of the interface.
static ADLXHelper g_ADLXHelp;

IADLXPerformanceMonitoringServicesPtr perfMonitoringService;
IADLXGPUListPtr gpus;

void GetTimeStamp(IADLXGPUMetricsPtr gpuMetrics)
{
	adlx_int64 timeStamp = 0;
	ADLX_RESULT res = gpuMetrics->TimeStamp(&timeStamp);
}

// Set GPU usage (in %)
void SetGPUUsage(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	// Display GPU usage support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUUsage(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuUsageSupported = supported;
		if (supported)
		{
			adlx_double usage = 0;
			res = gpuMetrics->GPUUsage(&usage);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuUsageValue = usage;
		}
	}
}

// Set GPU clock speed (in MHz)
void SetGPUClockSpeed(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	// Display GPU clock speed support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUClockSpeed(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuClockSpeedSupported = supported;
		if (supported)
		{
			adlx_int gpuClock = 0;
			res = gpuMetrics->GPUClockSpeed(&gpuClock);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuClockSpeedValue = gpuClock;
		}
	}
}

// Set GPU VRAM clock speed (in MHz)
void SetGPUVRAMClockSpeed(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	// Display the GPU VRAM clock speed support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUVRAMClockSpeed(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuVRAMClockSpeedSupported = supported;
		if (supported)
		{
			adlx_int memoryClock = 0;
			res = gpuMetrics->GPUVRAMClockSpeed(&memoryClock);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuVRAMClockSpeedValue = memoryClock;
		}
	}
}

// Set GPU temperature(in °C)
void SetGPUTemperature(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;

	// Display the GPU temperature support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUTemperature(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuTemperatureSupported = supported;
		if (supported)
		{
			adlx_double temperature = 0;
			res = gpuMetrics->GPUTemperature(&temperature);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuTemperatureValue = temperature;
		}
	}
}

// Set GPU hotspot temperature(in °C)
void SetGPUHotspotTemperature(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;

	// Display GPU hotspot temperature support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUHotspotTemperature(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuHotspotTemperatureSupported = supported;
		if (supported)
		{
			adlx_double hotspotTemperature = 0;
			res = gpuMetrics->GPUHotspotTemperature(&hotspotTemperature);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuHotspotTemperatureValue = hotspotTemperature;
		}
	}
}

// Set GPU power(in W)
void SetGPUPower(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	// Display GPU power support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUPower(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuPowerSupported = supported;
		if (supported)
		{
			adlx_double power = 0;
			res = gpuMetrics->GPUPower(&power);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuPowerValue = power;
		}
	}
}

// Set GPU total board power(in W)
void SetGPUTotalBoardPower(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	// Display GPU total board power support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUTotalBoardPower(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuTotalBoardPowerSupported = supported;
		if (supported)
		{
			adlx_double power = 0;
			res = gpuMetrics->GPUTotalBoardPower(&power);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuTotalBoardPowerValue = power;
		}
	}
}

// Set GPU fan speed (in RPM)
void SetGPUFanSpeed(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	// Display GPU fan speed support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUFanSpeed(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuFanSpeedSupported = supported;
		if (supported)
		{
			adlx_int fanSpeed = 0;
			res = gpuMetrics->GPUFanSpeed(&fanSpeed);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuFanSpeedValue = fanSpeed;
		}
	}
}

// Set GPU VRAM (in MB)
void SetGPUVRAM(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	// Display GPU VRAM support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUVRAM(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuVramSupported = supported;
		if (supported)
		{
			adlx_int VRAM = 0;
			res = gpuMetrics->GPUVRAM(&VRAM);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuVramValue = VRAM;
		}
	}
}

// Set GPU Voltage (in mV)
void SetGPUVoltage(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	// Display GPU voltage support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUVoltage(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuVoltageSupported = supported;
		if (supported)
		{
			adlx_int voltage = 0;
			res = gpuMetrics->GPUVoltage(&voltage);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuVoltageValue = voltage;
		}
	}
}

bool IntializeAdlx()
{
	ADLX_RESULT res = ADLX_FAIL;
	bool check = false;

	// Initialize ADLX
	res = g_ADLXHelp.Initialize();

	if (ADLX_SUCCEEDED(res))
	{
		// Get Performance Monitoring services
		ADLX_RESULT res = g_ADLXHelp.GetSystemServices()->GetPerformanceMonitoringServices(&perfMonitoringService);
		if (ADLX_SUCCEEDED(res))
		{
			// Get GPU list
			res = g_ADLXHelp.GetSystemServices()->GetGPUs(&gpus);
			if (ADLX_SUCCEEDED(res))
			{
				IADLXGPUPtr gpu;
				// Use the first GPU in the list
				res = gpus->At(gpus->Begin(), &gpu);
				if (ADLX_SUCCEEDED(res))
				{
					check = true;
				}
			}
		}
	}

	// Destroy ADLX
	if (!check)
		res = g_ADLXHelp.Terminate();

	return check;
}

void CloseAdlx()
{
	ADLX_RESULT res = ADLX_FAIL;

	// Terminate ADLX
	res = g_ADLXHelp.Terminate();
}

adlx_uint GetAtiAdpaterCount()
{
	if (gpus == nullptr)
		return 0u;

	adlx_uint size = 0;
	size = gpus->Size();
	return size;
}

bool GetAdlxTelemetry(const adlx_uint index, AdlxTelemetryData* adlxTelemetryData)
{
	ADLX_RESULT res = ADLX_FAIL;
	bool check = false;

	// Get GPU metrics support
	IADLXGPUMetricsSupportPtr gpuMetricsSupport;
	IADLXGPUMetricsPtr gpuMetrics;

	IADLXGPUPtr gpu;
	res = gpus->At(index, &gpu);

	if (ADLX_SUCCEEDED(res))
	{
		ADLX_RESULT res1 = perfMonitoringService->GetSupportedGPUMetrics(gpu, &gpuMetricsSupport);
		ADLX_RESULT res2 = perfMonitoringService->GetCurrentGPUMetrics(gpu, &gpuMetrics);

		// Display timestamp and GPU metrics
		if (ADLX_SUCCEEDED(res1) && ADLX_SUCCEEDED(res2))
		{
			GetTimeStamp(gpuMetrics);
			SetGPUUsage(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);
			SetGPUClockSpeed(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);
			SetGPUVRAMClockSpeed(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);
			SetGPUTemperature(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);
			SetGPUHotspotTemperature(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);
			SetGPUPower(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);
			SetGPUFanSpeed(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);
			SetGPUVRAM(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);
			SetGPUVoltage(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);
			SetGPUTotalBoardPower(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);

			check = true;
		}
	}

	return check;
}

bool GetAdlxDeviceInfo(const adlx_uint index, AdlxDeviceInfo* adlxDeviceInfo)
{
	ADLX_RESULT res = ADLX_FAIL;
	ADLX_RESULT ret;
	bool check = false;

	IADLXGPUPtr gpu;
	res = gpus->At(index, &gpu);

	if (ADLX_SUCCEEDED(res))
	{
		const char* vendorId = nullptr;
		ret = gpu->VendorId(&vendorId);
		strcpy_s(adlxDeviceInfo->VendorId, vendorId);

		ADLX_GPU_TYPE gpuType = GPUTYPE_UNDEFINED;
		ret = gpu->Type(&gpuType);
		adlxDeviceInfo->GpuType = gpuType == GPUTYPE_UNDEFINED ? 0
			: gpuType == GPUTYPE_INTEGRATED ? 1 : 2;

		const char* gpuName = nullptr;
		ret = gpu->Name(&gpuName);
		strcpy_s(adlxDeviceInfo->GpuName, gpuName);

		const char* driverPath = nullptr;
		ret = gpu->DriverPath(&driverPath);
		strcpy_s(adlxDeviceInfo->DriverPath, driverPath);

		adlx_int id;
		ret = gpu->UniqueId(&id);
		adlxDeviceInfo->Id = id;

		check = true;
	}

	return check;
}