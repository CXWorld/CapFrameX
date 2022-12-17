#pragma once
#include <cstdint>

typedef struct AdlxTelemetryData
{
	// GPU Usage
	bool gpuUsageSupported = false;
	double gpuUsageValue;

	// GPU Core Frequency
	bool gpuClockSpeedSupported = false;
	double gpuClockSpeedValue;

	// GPU VRAM Frequency
	bool gpuVRAMClockSpeedSupported = false;
	double gpuVRAMClockSpeedValue;

	// GPU Core Temperature
	bool gpuTemperatureSupported = false;
	double gpuTemperatureValue;

	// GPU Hotspot Temperature
	bool gpuHotspotTemperatureSupported = false;
	double gpuHotspotTemperatureValue;

	// GPU Power
	bool gpuPowerSupported = false;
	double gpuPowerValue;

	// Fan Speed
	bool gpuFanSpeedSupported = false;
	double gpuFanSpeeValue;

	// VRAM Usage
	bool gpuVramSupported = false;
	double gpuVramValue;

	// GPU Voltage
	bool gpuVoltageSupported = false;
	double gpuVoltageValue;

	// GPU TBP
	bool gpuTotalBoardPowerSupported = false;
	double gpuTotalBoardPowerValue;
};

#define ADLX_API __declspec(dllimport)

extern "C" ADLX_API bool IntializeAdlx();

extern "C" ADLX_API adlx_uint GetAtiAdpaterCount();

extern "C" ADLX_API bool GetAdlxTelemetry(const adlx_uint index, AdlxTelemetryData * telemetryData);
