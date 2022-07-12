#pragma once
#include <cstdint>
#include "igcl_api.h"

typedef struct IgclTelemetryData
{
	// GPU TDP
	bool gpuEnergyCounterSupported = false;
	float gpuEnergyCounterValue;

	// GPU Voltage
	bool gpuVoltageSupported = false;
	float gpuVoltagValue;

	// GPU Core Frequency
	bool gpuCurrentClockFrequencySupported = false;
	float gpuCurrentClockFrequencyValue;

	// GPU Core Temperature
	bool gpuCurrentTemperatureSupported = false;
	float gpuCurrentTemperatureValue;

	// GPU Usage
	bool globalActivityCounterSupported = false;
	float globalActivityCounterValue;

	// Render Engine Usage
	bool renderComputeActivityCounterSupported = false;
	float renderComputeActivityCounterValue;

	// Media Engine Usage
	bool mediaActivityCounterSupported = false;
	float mediaActivityCounterValue;

	// VRAM Power Consumption
	bool vramEnergyCounterSupported = false;
	float vramEnergyCounterValue;

	// VRAM Voltage
	bool vramVoltageSupported = false;
	float vramVoltageValue;

	// VRAM Frequency
	bool vramCurrentClockFrequencySupported = false;
	float vramCurrentClockFrequencyValue;

	// VRAM Read Bandwidth
	bool vramReadBandwidthCounterSupported = false;
	float vramReadBandwidthCounterValue;

	// VRAM Write Bandwidth
	bool vramWriteBandwidthCounterSupported = false;
	float vramWriteBandwidthCounterValue;

	// VRAM Temperature
	bool vramCurrentTemperatureSupported = false;
	float vramCurrentTemperatureValue;

	// Fanspeed (n Fans)
	bool fanSpeedSupported = false;
	float fanSpeedValue;
};

typedef struct IgclDeviceInfo
{
	char DeviceName[CTL_MAX_DEVICE_NAME_LEN];
	DWORD AdapterID;
	uint32_t Pci_vendor_id;
	uint32_t Pci_device_id;
	uint32_t Rev_id;
	bool Isvalid = false;
};

#define IGCL_API __declspec(dllimport)

extern "C" IGCL_API bool IntializeIgcl();

extern "C" IGCL_API void CloseIgcl();

extern "C" IGCL_API uint32_t GetAdpaterCount();

extern "C" IGCL_API IgclDeviceInfo GetDeviceInfo(uint32_t index);

extern "C" IGCL_API IgclTelemetryData GetIgclTelemetryData(uint32_t index);

