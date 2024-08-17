#pragma once
#include <cstdint>
#include "igcl_api.h"

#define CTL_MAX_DRIVER_VERSION_LEN  25

typedef struct IgclTelemetryData
{
	// GPU TDP
	bool gpuEnergySupported = false;
	double gpuEnergyValue;

	// GPU Voltage
	bool gpuVoltageSupported = false;
	double gpuVoltagValue;

	// GPU Core Frequency
	bool gpuCurrentClockFrequencySupported = false;
	double gpuCurrentClockFrequencyValue;

	// GPU Core Temperature
	bool gpuCurrentTemperatureSupported = false;
	double gpuCurrentTemperatureValue;

	// GPU Usage
	bool globalActivitySupported = false;
	double globalActivityValue;

	// Render Engine Usage
	bool renderComputeActivitySupported = false;
	double renderComputeActivityValue;

	// Media Engine Usage
	bool mediaActivitySupported = false;
	double mediaActivityValue;

	// VRAM Power Consumption
	bool vramEnergySupported = false;
	double vramEnergyValue;

	// VRAM Voltage
	bool vramVoltageSupported = false;
	double vramVoltageValue;

	// VRAM Frequency
	bool vramCurrentClockFrequencySupported = false;
	double vramCurrentClockFrequencyValue;

	// VRAM Read Bandwidth
	bool vramReadBandwidthSupported = false;
	double vramReadBandwidthValue;

	// VRAM Write Bandwidth
	bool vramWriteBandwidthSupported = false;
	double vramWriteBandwidthValue;

	// VRAM Temperature
	bool vramCurrentTemperatureSupported = false;
	double vramCurrentTemperatureValue;

	// Fanspeed (n Fans)
	bool fanSpeedSupported = false;
	double fanSpeedValue;
};

typedef struct IgclDeviceInfo
{
	char DeviceName[CTL_MAX_DEVICE_NAME_LEN];
	DWORD AdapterID;
	uint32_t Pci_vendor_id;
	uint32_t Pci_device_id;
	uint32_t Rev_id;
	char DriverVersion[CTL_MAX_DRIVER_VERSION_LEN];
};

#define IGCL_API __declspec(dllimport)

extern "C" IGCL_API bool IntializeIgcl();

extern "C" IGCL_API void CloseIgcl();

extern "C" IGCL_API uint32_t GetAdpaterCount();

extern "C" IGCL_API uint32_t GetBusWidth(const uint32_t index);

extern "C" IGCL_API bool GetDeviceInfo(const uint32_t index, IgclDeviceInfo *igclDeviceInfo);

extern "C" IGCL_API bool GetIgclTelemetryData(const uint32_t index, IgclTelemetryData *igclTelemetryData);

