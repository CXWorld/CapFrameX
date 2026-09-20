#pragma once
#include <cstdint>

#ifdef CAPFRAMEXHWINFO_EXPORTS
#define HWINFO_API __declspec(dllexport)
#else
#define HWINFO_API __declspec(dllimport)
#endif

extern "C" HWINFO_API uint64_t GetTimeStampCounterFrequency();

uint64_t RoundSmart(uint64_t i, uint64_t nearest);

uint64_t Timestamp(void);
