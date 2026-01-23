#include <crtdbg.h>
#include <stdio.h>
#include <stdlib.h>
#include "SDK/ADLXHelper/Windows/Cpp/ADLXHelper.h"
#include "SDK/Include/IPerformanceMonitoring3.h"
#include "ADLXManager.h"
#include <exception>

// Use ADLX namespace
using namespace adlx;

// ADLXHelper instance
// No outstanding interfaces from ADLX must exist when ADLX is destroyed.
// Use global variables to ensure validity of the interface.
static ADLXHelper g_ADLXHelp;

IADLXPerformanceMonitoringServicesPtr _perfMonitoringService;
IADLXGPUListPtr _gpus;

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

// Set GPU temperature(in �C)
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

// Set GPU hotspot temperature(in �C)
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

// Set GPU intake temperature (in °C)
void SetGPUIntakeTemperature(IADLXGPUMetricsSupportPtr gpuMetricsSupport, IADLXGPUMetricsPtr gpuMetrics, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	// Display GPU intake temperature support status
	ADLX_RESULT res = gpuMetricsSupport->IsSupportedGPUIntakeTemperature(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuIntakeTemperatureSupported = supported;
		if (supported)
		{
			adlx_double intakeTemperature = 0;
			res = gpuMetrics->GPUIntakeTemperature(&intakeTemperature);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuIntakeTemperatureValue = intakeTemperature;
		}
	}
}

// Set GPU memory temperature (in °C) - requires IADLXGPUMetrics1
void SetGPUMemoryTemperature(IADLXGPUMetricsSupport1Ptr gpuMetricsSupport1, IADLXGPUMetrics1Ptr gpuMetrics1, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	ADLX_RESULT res = gpuMetricsSupport1->IsSupportedGPUMemoryTemperature(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuMemoryTemperatureSupported = supported;
		if (supported)
		{
			adlx_double memoryTemperature = 0;
			res = gpuMetrics1->GPUMemoryTemperature(&memoryTemperature);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuMemoryTemperatureValue = memoryTemperature;
		}
	}
}

// Set NPU frequency (in MHz) - requires IADLXGPUMetrics1
void SetNPUFrequency(IADLXGPUMetricsSupport1Ptr gpuMetricsSupport1, IADLXGPUMetrics1Ptr gpuMetrics1, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	ADLX_RESULT res = gpuMetricsSupport1->IsSupportedNPUFrequency(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->npuFrequencySupported = supported;
		if (supported)
		{
			adlx_int npuFrequency = 0;
			res = gpuMetrics1->NPUFrequency(&npuFrequency);
			if (ADLX_SUCCEEDED(res))
				telemetryData->npuFrequencyValue = npuFrequency;
		}
	}
}

// Set NPU activity level (in %) - requires IADLXGPUMetrics1
void SetNPUActivityLevel(IADLXGPUMetricsSupport1Ptr gpuMetricsSupport1, IADLXGPUMetrics1Ptr gpuMetrics1, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	ADLX_RESULT res = gpuMetricsSupport1->IsSupportedNPUActivityLevel(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->npuActivityLevelSupported = supported;
		if (supported)
		{
			adlx_int npuActivityLevel = 0;
			res = gpuMetrics1->NPUActivityLevel(&npuActivityLevel);
			if (ADLX_SUCCEEDED(res))
				telemetryData->npuActivityLevelValue = npuActivityLevel;
		}
	}
}

// Set GPU shared memory (in MB) - requires IADLXGPUMetrics2
void SetGPUSharedMemory(IADLXGPUMetricsSupport2Ptr gpuMetricsSupport2, IADLXGPUMetrics2Ptr gpuMetrics2, AdlxTelemetryData* telemetryData)
{
	adlx_bool supported = false;
	ADLX_RESULT res = gpuMetricsSupport2->IsSupportedGPUSharedMemory(&supported);
	if (ADLX_SUCCEEDED(res))
	{
		telemetryData->gpuSharedMemorySupported = supported;
		if (supported)
		{
			adlx_int sharedMemory = 0;
			res = gpuMetrics2->GPUSharedMemory(&sharedMemory);
			if (ADLX_SUCCEEDED(res))
				telemetryData->gpuSharedMemoryValue = sharedMemory;
		}
	}
}

bool IntializeAdlx()
{
	ADLX_RESULT res = ADLX_FAIL;
	bool check = false;

	try
	{
		// Initialize ADLX
		res = g_ADLXHelp.Initialize();

		if (ADLX_SUCCEEDED(res))
		{
			// Get Performance Monitoring services
			res = g_ADLXHelp.GetSystemServices()->GetPerformanceMonitoringServices(&_perfMonitoringService);
			if (ADLX_SUCCEEDED(res))
			{
				// Get GPU list
				res = g_ADLXHelp.GetSystemServices()->GetGPUs(&_gpus);
				if (ADLX_SUCCEEDED(res))
				{
					IADLXGPUPtr gpu;
					// Use the first GPU in the list
					res = _gpus->At(_gpus->Begin(), &gpu);
					if (ADLX_SUCCEEDED(res))
					{
						res = _perfMonitoringService->ClearPerformanceMetricsHistory();

						if (ADLX_SUCCEEDED(res))
						{
							res = _perfMonitoringService->StartPerformanceMetricsTracking();
							check = true;
						}
					}
				}
			}
		}

		if (!check)
		{
			g_ADLXHelp.Terminate();
		}
	}
	catch (const std::exception& e)
	{
		g_ADLXHelp.Terminate();  // Clean up resources
		return false; // Return false on any exception
	}
	catch (...)
	{
		// Catch any other types of exceptions
		g_ADLXHelp.Terminate();  // Clean up resources
		return false; // Return false on any unknown exception
	}

	return check;
}

void CloseAdlx()
{
	_perfMonitoringService->StopPerformanceMetricsTracking();
	g_ADLXHelp.Terminate();
}

adlx_uint GetAtiAdpaterCount()
{
	if (_gpus == nullptr)
		return 0u;

	adlx_uint size = 0;
	size = _gpus->Size();
	return size;
}

bool GetAdlxTelemetry(const adlx_uint index, const adlx_uint historyLength, AdlxTelemetryData* adlxTelemetryData)
{
	bool check = false;

	try
	{
		IADLXGPUPtr gpu;
		ADLX_RESULT resGetGPU = _gpus->At(index, &gpu);

		if (ADLX_SUCCEEDED(resGetGPU))
		{
			IADLXGPUMetricsListPtr gpuMetricsList;
			ADLX_RESULT resGetHistory = _perfMonitoringService->GetGPUMetricsHistory(gpu, historyLength, 0, &gpuMetricsList);

			if (ADLX_SUCCEEDED(resGetHistory))
			{
				// Take last element
				// 
				// Tests with sample code showed that gpuMetricsList
				// only has 1 element, when history interval length
				// <= 1000ms. No need to calculate an average.  
				adlx_uint pos = gpuMetricsList->Size() - 1;

				if (pos >= 0)
				{
					IADLXGPUMetricsSupportPtr gpuMetricsSupport;
					ADLX_RESULT resGetSupportedMetrics = _perfMonitoringService->GetSupportedGPUMetrics(gpu, &gpuMetricsSupport);

					if (ADLX_SUCCEEDED(resGetSupportedMetrics))
					{
						IADLXGPUMetricsPtr gpuMetrics;
						ADLX_RESULT resGpuMetrics = gpuMetricsList->At(pos, &gpuMetrics);

						if (ADLX_SUCCEEDED(resGpuMetrics))
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
							SetGPUIntakeTemperature(gpuMetricsSupport, gpuMetrics, adlxTelemetryData);

							// Query for IADLXGPUMetricsSupport1 and IADLXGPUMetrics1 interfaces
							IADLXGPUMetricsSupport1Ptr gpuMetricsSupport1;
							IADLXGPUMetrics1Ptr gpuMetrics1;
							if (ADLX_SUCCEEDED(gpuMetricsSupport->QueryInterface(IADLXGPUMetricsSupport1::IID(), reinterpret_cast<void**>(&gpuMetricsSupport1))) &&
								ADLX_SUCCEEDED(gpuMetrics->QueryInterface(IADLXGPUMetrics1::IID(), reinterpret_cast<void**>(&gpuMetrics1))))
							{
								SetGPUMemoryTemperature(gpuMetricsSupport1, gpuMetrics1, adlxTelemetryData);
								SetNPUFrequency(gpuMetricsSupport1, gpuMetrics1, adlxTelemetryData);
								SetNPUActivityLevel(gpuMetricsSupport1, gpuMetrics1, adlxTelemetryData);

								// Query for IADLXGPUMetricsSupport2 and IADLXGPUMetrics2 interfaces
								IADLXGPUMetricsSupport2Ptr gpuMetricsSupport2;
								IADLXGPUMetrics2Ptr gpuMetrics2;
								if (ADLX_SUCCEEDED(gpuMetricsSupport1->QueryInterface(IADLXGPUMetricsSupport2::IID(), reinterpret_cast<void**>(&gpuMetricsSupport2))) &&
									ADLX_SUCCEEDED(gpuMetrics1->QueryInterface(IADLXGPUMetrics2::IID(), reinterpret_cast<void**>(&gpuMetrics2))))
								{
									SetGPUSharedMemory(gpuMetricsSupport2, gpuMetrics2, adlxTelemetryData);
								}
							}

							check = true;
						}
					}
				}
			}
		};
	}
	catch (const std::exception& e)
	{
		return false; // Return false on any exception
	}
	catch (...)
	{
		return false; // Return false on any unknown exception
	}

	return check;
}

bool GetAdlxTelemetrySupport(const adlx_uint index, AdlxTelemetrySupport* adlxTelemetrySupport)
{
	bool check = false;

	try
	{
		IADLXGPUPtr gpu;
		ADLX_RESULT resGetGPU = _gpus->At(index, &gpu);

		if (ADLX_SUCCEEDED(resGetGPU))
		{
			IADLXGPUMetricsSupportPtr gpuMetricsSupport;
			ADLX_RESULT resGetSupportedMetrics = _perfMonitoringService->GetSupportedGPUMetrics(gpu, &gpuMetricsSupport);

			if (ADLX_SUCCEEDED(resGetSupportedMetrics))
			{
				adlx_bool supported = false;

				// Query base metrics support
				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUUsage(&supported)))
					adlxTelemetrySupport->gpuUsageSupported = supported;

				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUClockSpeed(&supported)))
					adlxTelemetrySupport->gpuClockSpeedSupported = supported;

				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUVRAMClockSpeed(&supported)))
					adlxTelemetrySupport->gpuVRAMClockSpeedSupported = supported;

				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUTemperature(&supported)))
					adlxTelemetrySupport->gpuTemperatureSupported = supported;

				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUHotspotTemperature(&supported)))
					adlxTelemetrySupport->gpuHotspotTemperatureSupported = supported;

				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUPower(&supported)))
					adlxTelemetrySupport->gpuPowerSupported = supported;

				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUFanSpeed(&supported)))
					adlxTelemetrySupport->gpuFanSpeedSupported = supported;

				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUVRAM(&supported)))
					adlxTelemetrySupport->gpuVramSupported = supported;

				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUVoltage(&supported)))
					adlxTelemetrySupport->gpuVoltageSupported = supported;

				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUTotalBoardPower(&supported)))
					adlxTelemetrySupport->gpuTotalBoardPowerSupported = supported;

				if (ADLX_SUCCEEDED(gpuMetricsSupport->IsSupportedGPUIntakeTemperature(&supported)))
					adlxTelemetrySupport->gpuIntakeTemperatureSupported = supported;

				// Query IADLXGPUMetricsSupport1 for extended metrics
				IADLXGPUMetricsSupport1Ptr gpuMetricsSupport1;
				if (ADLX_SUCCEEDED(gpuMetricsSupport->QueryInterface(IADLXGPUMetricsSupport1::IID(), reinterpret_cast<void**>(&gpuMetricsSupport1))))
				{
					if (ADLX_SUCCEEDED(gpuMetricsSupport1->IsSupportedGPUMemoryTemperature(&supported)))
						adlxTelemetrySupport->gpuMemoryTemperatureSupported = supported;

					if (ADLX_SUCCEEDED(gpuMetricsSupport1->IsSupportedNPUFrequency(&supported)))
						adlxTelemetrySupport->npuFrequencySupported = supported;

					if (ADLX_SUCCEEDED(gpuMetricsSupport1->IsSupportedNPUActivityLevel(&supported)))
						adlxTelemetrySupport->npuActivityLevelSupported = supported;

					// Query IADLXGPUMetricsSupport2 for more extended metrics
					IADLXGPUMetricsSupport2Ptr gpuMetricsSupport2;
					if (ADLX_SUCCEEDED(gpuMetricsSupport1->QueryInterface(IADLXGPUMetricsSupport2::IID(), reinterpret_cast<void**>(&gpuMetricsSupport2))))
					{
						if (ADLX_SUCCEEDED(gpuMetricsSupport2->IsSupportedGPUSharedMemory(&supported)))
							adlxTelemetrySupport->gpuSharedMemorySupported = supported;
					}
				}

				check = true;
			}
		}
	}
	catch (const std::exception& e)
	{
		return false;
	}
	catch (...)
	{
		return false;
	}

	return check;
}

bool GetAdlxDeviceInfo(const adlx_uint index, AdlxDeviceInfo* adlxDeviceInfo)
{
	ADLX_RESULT res = ADLX_FAIL;
	ADLX_RESULT ret;
	bool check = false;

	try
	{
		IADLXGPUPtr gpu;
		res = _gpus->At(index, &gpu);

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
	}
	catch (const std::exception& e)
	{
		return false; // Return false on any exception
	}
	catch (...)
	{
		return false; // Return false on any unknown exception
	}

	return check;
}