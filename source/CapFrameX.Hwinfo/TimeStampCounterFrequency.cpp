#include "pch.h"
#include <string>
#include <intrin.h>
#include <Windows.h>
#include "TimeStampCounterFrequency.h"

uint64_t GetTimeStampCounterFrequency()
{
	uint64_t  frequency = 0;
	size_t repeats = 100;

	//get QPC freq
	LARGE_INTEGER Frequency{};
	QueryPerformanceFrequency(&Frequency);

	for (size_t i = 0; i < repeats; i++)
	{
		LARGE_INTEGER qpc_ts_1{};
		LARGE_INTEGER qpc_ts_2{};
		LARGE_INTEGER qpc_ts_target{};

		uint64_t rdtsc_start = Timestamp();

		QueryPerformanceCounter(&qpc_ts_1);
		qpc_ts_target.QuadPart = qpc_ts_1.QuadPart + Frequency.QuadPart / 1000;

		while (qpc_ts_2.QuadPart < qpc_ts_target.QuadPart) {
			QueryPerformanceCounter(&qpc_ts_2);
		}

		uint64_t qpc_diff = qpc_ts_2.QuadPart - qpc_ts_1.QuadPart;
		uint64_t rdtsc_stop = Timestamp();
		uint64_t ticks_ms = rdtsc_stop - rdtsc_start;
		if (i == 0 || ticks_ms < frequency)
			frequency = ticks_ms;
	}

	return RoundSmart(frequency, 100000) * 1000;
}

uint64_t RoundSmart(uint64_t i, uint64_t nearest)
{
	if (nearest <= 0 || nearest % 10 != 0) {
		return 0;
	}

	return (i + 5 * nearest / 10) / nearest * nearest;
}

uint64_t Timestamp(void) {
	unsigned int ui;
	return __rdtscp(&ui);
}