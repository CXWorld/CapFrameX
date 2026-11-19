#pragma once
#include <windows.h>
#include <algorithm>
#include <vector>

/////////////////////////////////////////////////////////////////////////////
// RTSS keeps the latest frame times of an application in a ring of 1024 values in microseconds
// (RTSS_SHARED_MEMORY_APP_ENTRY::dwStatFrameTimeBuf). dwStatFrameTimeBufPos is the slot RTSS
// writes next, so the newest frame time sits right before it, as the overlay's frametime reads it.
/////////////////////////////////////////////////////////////////////////////
namespace RTSSFrameTimeRing
{
	constexpr DWORD Size = 1024;

	// The frame times in ms, oldest first, of the newest frames that together stay below the
	// interval. The walk goes around the ring at most once and stops at the first slot RTSS has
	// not written yet. Before, it started at the slot RTSS writes next (the oldest frame time),
	// and its DWORD index never fell below zero: above about 1024 FPS the ring covers less than
	// the interval, so the frames were taken twice, and a ring of zeros never ended.
	inline std::vector<float> Collect(const DWORD* ring, DWORD position, DWORD microseconds)
	{
		std::vector<float> frameTimes;
		DWORD newest = position - 1;
		DWORD frameTimeSum = 0;

		for (DWORD age = 0; age < Size; age++)
		{
			DWORD frameTime = ring[(newest - age) & (Size - 1)];
			if (frameTime == 0)
				break;

			frameTimeSum += frameTime;
			if (frameTimeSum >= microseconds)
				break;

			frameTimes.push_back(frameTime / 1000.0f);
		}

		// Collected newest first.
		std::reverse(frameTimes.begin(), frameTimes.end());
		return frameTimes;
	}
}
