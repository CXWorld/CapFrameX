#pragma once
#include <cstdint>
#include "igcl_api.h"

#define CTL_MAX_DRIVER_VERSION_LEN  25

struct IgclTelemetryItem
{
	bool supported = false;
	double value;
};

struct IgclPsuRail
{
	// ctl_psu_type_t: PCIe slot, 6-pin or 8-pin connector (0 = unknown)
	int32_t type;

	// Average power over the last sample interval (W)
	IgclTelemetryItem power;

	// Voltage (V)
	IgclTelemetryItem voltage;
};

// The layout is mirrored by LibreHardwareMonitorLib/Interop/IGCL.cs.
static_assert(sizeof(IgclTelemetryItem) == 16, "IgclTelemetryItem layout changed");
static_assert(sizeof(IgclPsuRail) == 40, "IgclPsuRail layout changed");

struct IgclTelemetryData
{
	// GPU TDP
	bool gpuEnergySupported = false;
	double gpuEnergyValue;

	// GPU TBP
	bool totalCardEnergySupported = false;
	double totalCardEnergyValue;

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

	// The items below require ctl_power_telemetry_t version 1 or newer.

	// GPU VR Temperature
	bool gpuVrTemperatureSupported = false;
	double gpuVrTemperatureValue;

	// VRAM VR Temperature
	bool vramVrTemperatureSupported = false;
	double vramVrTemperatureValue;

	// System Agent VR Temperature
	bool saVrTemperatureSupported = false;
	double saVrTemperatureValue;

	// GPU Effective Frequency
	bool gpuEffectiveClockSupported = false;
	double gpuEffectiveClockValue;

	// GPU Overvoltage (% of the maximum over-voltage increment)
	bool gpuOverVoltagePercentSupported = false;
	double gpuOverVoltagePercentValue;

	// GPU Power (% of the default maximum power)
	bool gpuPowerPercentSupported = false;
	double gpuPowerPercentValue;

	// GPU Temperature (% of the thermal margin)
	bool gpuTemperaturePercentSupported = false;
	double gpuTemperaturePercentValue;

	// VRAM Read Bandwidth (GB/s)
	bool vramReadBandwidthGBpsSupported = false;
	double vramReadBandwidthGBpsValue;

	// VRAM Write Bandwidth (GB/s)
	bool vramWriteBandwidthGBpsSupported = false;
	double vramWriteBandwidthGBpsValue;

	// Fans 2..5; fan 1 is fanSpeedSupported/fanSpeedValue above.
	IgclTelemetryItem fan2Speed;
	IgclTelemetryItem fan3Speed;
	IgclTelemetryItem fan4Speed;
	IgclTelemetryItem fan5Speed;

	// Power supply rails (CTL_PSU_COUNT)
	IgclPsuRail psu1;
	IgclPsuRail psu2;
	IgclPsuRail psu3;
	IgclPsuRail psu4;
	IgclPsuRail psu5;
};

// Pinned on the managed side as well (IgclInteropLayoutTest); change both together.
static_assert(sizeof(IgclTelemetryData) == 648, "IgclTelemetryData layout changed");

struct IgclDeviceInfo
{
	char DeviceName[CTL_MAX_DEVICE_NAME_LEN];
	DWORD AdapterID;
	uint32_t Pci_vendor_id;
	uint32_t Pci_device_id;
	uint32_t Rev_id;
	char DriverVersion[CTL_MAX_DRIVER_VERSION_LEN]; 
	uint32_t Adapter_Property_Flag;
};

#ifdef CAPFRAMEXIGCL_EXPORTS
#define IGCL_API __declspec(dllexport)
#else
#define IGCL_API __declspec(dllimport)
#endif

extern "C" IGCL_API bool IntializeIgcl();

extern "C" IGCL_API void CloseIgcl();

extern "C" IGCL_API uint32_t GetAdpaterCount();

extern "C" IGCL_API uint32_t GetBusWidth(const uint32_t index);

extern "C" IGCL_API bool GetDeviceInfo(const uint32_t index, IgclDeviceInfo *igclDeviceInfo);

extern "C" IGCL_API bool GetIgclTelemetryData(const uint32_t index, IgclTelemetryData *igclTelemetryData);
