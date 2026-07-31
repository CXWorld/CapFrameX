//=============================================================================
// Copyright (c) 2023 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_timer.h
/// @brief  FLM High precision timer interface
//=============================================================================

#ifndef FLM_TIMER_H
#define FLM_TIMER_H

#define USE_AMF_TIMER

#include "flm.h"

#ifdef USE_AMF_TIMER
#pragma warning(push)
#pragma warning(disable : 4996)
#include "public/common/CurrentTimeImpl.h"
#pragma warning(pop)

using namespace amf;
#else
#include <Windows.h>
#endif

static int depth = 0;
static int debug_stack_level = 2;

class FLM_Profile_Timer
{
public:
    FLM_Profile_Timer(std::string caller)
    {
        m_caller = caller;
        depth++;
        QueryPerformanceFrequency(&m_freqCountPerSecond);
        QueryPerformanceCounter(&m_start);
    }

    ~FLM_Profile_Timer()
    {
        LARGE_INTEGER now;
        QueryPerformanceCounter(&now);
        LARGE_INTEGER diff_us;
        diff_us.QuadPart = now.QuadPart - m_start.QuadPart;
        diff_us.QuadPart *= 1000000;
        diff_us.QuadPart /= m_freqCountPerSecond.QuadPart;
        depth--;
        printf("%2d %*s %-s %6.6f ms\n",depth, depth*4," ", m_caller.c_str(), diff_us.QuadPart / 1000.0);

    }

private:
    std::string   m_caller;
    LARGE_INTEGER m_freqCountPerSecond = {1, 1};
    LARGE_INTEGER m_start              = {0, 0};
};


class FLM_Performance_Timer
{
public:
    FLM_Performance_Timer()
    {
        QueryPerformanceFrequency(&m_freqCountPerSecond);
        Start();
    }

    LONGLONG now()
    {
        LARGE_INTEGER now;
        QueryPerformanceCounter(&now);
        return now.QuadPart;
    }

    void Start()
    {
        QueryPerformanceCounter(&m_start);
    }

    double Stop_ms()
    {
        LARGE_INTEGER diff_us;
        LARGE_INTEGER now;
        QueryPerformanceCounter(&now);
        diff_us.QuadPart = now.QuadPart - m_start.QuadPart;
        diff_us.QuadPart *= 1000000;
        diff_us.QuadPart /= m_freqCountPerSecond.QuadPart;
        return diff_us.QuadPart / 1000.0;
    }

    LARGE_INTEGER GetFrequency()
    {
        return m_freqCountPerSecond;
    }

private:
    LARGE_INTEGER m_freqCountPerSecond = {1, 1};
    LARGE_INTEGER m_start              = {0, 0};
};

class FLM_Timer_AMF
{
public:
    bool Init();
    void Close();

#ifdef USE_AMF_TIMER
    amf::AMFCurrentTimePtr GetCurrentTimer()
    {
        if (m_pAMF_CurrentTimer.GetPtr() != nullptr)
            return m_pAMF_CurrentTimer;
        else
            return nullptr;
    }

    amf_pts now()
    {
        if (m_pAMF_CurrentTimer.GetPtr() != nullptr)
            return m_pAMF_CurrentTimer->Get();
        else
            return 0;
    }

    int64_t UpdateAmfTimeToPerformanceCounterOffset();
    int64_t TranslateAmfTimeToPerformanceCounter(int64_t iiAmfTime);
#endif

    void PrecisionSleepMS(float fTimeToSleepMS, int64_t iiSleepStart);

private:
    void PrecisionSleepUntilTimestamp(int64_t iiSleepEnd);

#ifdef USE_AMF_TIMER
    amf::AMFCurrentTimePtr m_pAMF_CurrentTimer;
#endif

    int64_t m_iiFreqCountPerSecond                    = 0;
    int64_t m_iiCountPerOneMS                         = 0;
    int64_t m_iiAmfTimeToPerformanceCounterTimeOffset = 0;
};

#endif
