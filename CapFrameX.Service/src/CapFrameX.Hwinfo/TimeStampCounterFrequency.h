#pragma once
#include <cstdint>

#define HWINFO_API __declspec(dllimport)

extern "C" HWINFO_API uint64_t GetTimeStampCounterFrequency();

uint64_t RoundSmart(uint64_t i, uint64_t nearest);

uint64_t Timestamp(void);