//=============================================================================
// Copyright (c) 2023 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_timer.cpp
/// @brief  FLM High precision timer
//=============================================================================

#include "flm_timer.h"
#pragma comment(lib, "Winmm.lib")

bool FLM_Timer_AMF::Init()
{
    QueryPerformanceFrequency((LARGE_INTEGER*)&m_iiFreqCountPerSecond);
    m_iiCountPerOneMS = m_iiFreqCountPerSecond / 1000;

#ifdef USE_AMF_TIMER
    m_pAMF_CurrentTimer = new amf::AMFCurrentTimeImpl();
    if (m_pAMF_CurrentTimer == NULL)
        return false;
#endif

    return true;
}

#ifdef USE_AMF_TIMER
int64_t FLM_Timer_AMF::UpdateAmfTimeToPerformanceCounterOffset()
{
    if (m_pAMF_CurrentTimer == NULL)
        return 0;

    int64_t iiTime1;
    QueryPerformanceCounter((LARGE_INTEGER*)&iiTime1);
    int64_t iiAmfNow = m_pAMF_CurrentTimer->Get();  // This command takes about 500 nSec
    int64_t iiTime2;
    QueryPerformanceCounter((LARGE_INTEGER*)&iiTime2);

    int64_t iiNow = (iiTime1 + iiTime2 + 1) / 2;  // Average time to compare to iiAmfNow

    // Note: for the current amf version, m_iiFreqCountPerSecond == AMF_SECOND...
    int64_t iiAmfNowInTicks = (iiAmfNow * m_iiFreqCountPerSecond + AMF_SECOND / 2) / AMF_SECOND;  //with rounding...

    m_iiAmfTimeToPerformanceCounterTimeOffset = iiNow - iiAmfNowInTicks;

    // We need this because of the Sleep(1) used in PrecisionSleepUntilTimestamp().
    // amf_increase_timer_precision();

    return m_iiAmfTimeToPerformanceCounterTimeOffset;
}

int64_t FLM_Timer_AMF::TranslateAmfTimeToPerformanceCounter(int64_t iiAmfTime)
{
    int64_t iiAmfTimeInTicks = (iiAmfTime * m_iiFreqCountPerSecond + AMF_SECOND / 2) / AMF_SECOND;  //with rounding...
    int64_t iiTime           = iiAmfTimeInTicks + m_iiAmfTimeToPerformanceCounterTimeOffset;
    return iiTime;
}
#endif

void FLM_Timer_AMF::PrecisionSleepUntilTimestamp(int64_t iiSleepEnd)
{
    int64_t iiNow;
    QueryPerformanceCounter((LARGE_INTEGER*)&iiNow);

    while (iiNow < iiSleepEnd)
    {
        if ((iiSleepEnd - iiNow) > 17 * m_iiCountPerOneMS)  // More than 17 ms remaining? Sleep(1) (default resolution is about 16 ms)
        {
            Sleep(1);
        }
        else if ((iiSleepEnd - iiNow) >
                 4 * m_iiCountPerOneMS)  // (3 should be enough) More than 4 ms remaining? Sleep(1) with higher precision context switching
        {
            timeBeginPeriod(1);
            Sleep(1);
            timeEndPeriod(1);
        }
        else if ((iiSleepEnd - iiNow) > m_iiCountPerOneMS / 5)  // More than 0.2ms (2000 ticks) remaining? Sleep(0)
        {
            Sleep(0);
        }
        else
        {
            //for (int i = 0; i < PROCESSOR_YIELD_CYCLES; i++)
            //    YieldProcessor();
            #define y YieldProcessor()
            y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;
            y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;
            y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;y;
            #undef y
        }
        if (iiNow < iiSleepEnd)
            QueryPerformanceCounter((LARGE_INTEGER*)&iiNow);
    }
}
//#pragma optimize("", on)

void FLM_Timer_AMF::PrecisionSleepMS(float fTimeToSleepMS, int64_t iiSleepStart)
{
    // FlamePrint("PrecisionSleepMS %4.4f ms\n",fTimeToSleepMS);

    int64_t iiNow;
    QueryPerformanceCounter((LARGE_INTEGER*)&iiNow);

    if (iiSleepStart == 0)
        iiSleepStart = iiNow;

    int64_t iiSleepEnd = iiSleepStart + int64_t(fTimeToSleepMS * m_iiCountPerOneMS);

    // Sanity check
    if (iiSleepEnd >= iiNow)
        PrecisionSleepUntilTimestamp(iiSleepEnd);
    //else
    //    printf("#"); // debug
}

void FLM_Timer_AMF::Close()
{
}
