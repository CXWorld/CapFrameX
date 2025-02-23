//
// Copyright (c) 2021 - 2024 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_IPERFORMANCEMONITORING1_H
#define ADLX_IPERFORMANCEMONITORING1_H
#pragma once

#include "IPerformanceMonitoring.h"

#pragma region IADLXSystemMetricsSupport1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXSystemMetricsSupport1 : public IADLXSystemMetricsSupport
    {
    public:
        ADLX_DECLARE_IID (L"IADLXSystemMetricsSupport1")

        /**
        *@page DOX_IADLXSystemMetricsSupport1_IsSupportedPowerDistribution IsSupportedPowerDistribution
        *@ENG_START_DOX @brief Checks if reporting of power distribution between CPU and GPU is supported on the system. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupportedPowerDistribution (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of reporting of power distribution is returned. The variable is __true__ if the power distribution metric reporting is supported\. The variable is __false__ if the power distribution metric reporting is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX If the state of power distribution metric reporting is successfully returned, __ADLX_OK__ is returned. <br>
        *If the state of power distribution metric reporting is not successfully returned, an error code is returned. <br>
        *Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@copydoc IADLXSystemMetricsSupport1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsSupportedPowerDistribution(adlx_bool* supported) = 0;
    };
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXSystemMetricsSupport1> IADLXSystemMetricsSupport1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXSystemMetricsSupport1, L"IADLXSystemMetricsSupport1")

typedef struct IADLXSystemMetricsSupport1 IADLXSystemMetricsSupport1;
typedef struct IADLXSystemMetricsSupport1Vtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXSystemMetricsSupport1* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXSystemMetricsSupport1* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXSystemMetricsSupport1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXSystemMetricsSupport
    ADLX_RESULT (ADLX_STD_CALL *IsSupportedCPUUsage)(IADLXSystemMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL *IsSupportedSystemRAM)(IADLXSystemMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL *IsSupportedSmartShift)(IADLXSystemMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL *GetCPUUsageRange)(IADLXSystemMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL *GetSystemRAMRange)(IADLXSystemMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT (ADLX_STD_CALL *GetSmartShiftRange)(IADLXSystemMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);

    //IADLXSystemMetricsSupport1
    ADLX_RESULT (ADLX_STD_CALL *IsSupportedPowerDistribution)(IADLXSystemMetricsSupport1* pThis, adlx_bool* supported);
}IADLXSystemMetricsSupport1Vtbl;
struct IADLXSystemMetricsSupport1 { const IADLXSystemMetricsSupport1Vtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXSystemMetricsSupport1

#pragma region IADLXSystemMetrics1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXSystemMetrics1 : public IADLXSystemMetrics
    {
    public:
        ADLX_DECLARE_IID (L"IADLXSystemMetrics1")
                
        /**
        *@page DOX_IADLXSystemMetrics1_PowerDistribution PowerDistribution
        *@ENG_START_DOX @brief Gets the distribution of power between CPU and GPU of a system metric sample. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    PowerDistribution (adlx_int* apuShiftValue, adlx_int* gpuShiftValue, adlx_int* apuShiftLimit, adlx_int* gpuShiftLimit, adlx_int* totalShiftLimit)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,apuShiftValue,adlx_int* ,@ENG_START_DOX The pointer to a variable where the apu shift alue value is returned. @ENG_END_DOX}
        *@paramrow{1.,[out] ,gpuShiftValue,adlx_int* ,@ENG_START_DOX The pointer to a variable where the gpu shift value is returned. @ENG_END_DOX}
        *@paramrow{1.,[out] ,apuShiftLimit,adlx_int* ,@ENG_START_DOX The pointer to a variable where the apu shift limit value is returned. @ENG_END_DOX}
        *@paramrow{1.,[out] ,gpuShiftLimit,adlx_int* ,@ENG_START_DOX The pointer to a variable where the gpu shift limit value is returned. @ENG_END_DOX}
        *@paramrow{1.,[out] ,totalShiftLimit,adlx_int* ,@ENG_START_DOX The pointer to a variable where the total shift limit value is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the distribution of power is successfully returned, __ADLX_OK__ is returned. <br>
        * If the distribution of power is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@copydoc IADLXSystemMetrics1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL PowerDistribution(adlx_int* apuShiftValue, adlx_int* gpuShiftValue, adlx_int* apuShiftLimit, adlx_int* gpuShiftLimit, adlx_int* totalShiftLimit) = 0;
    };
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXSystemMetrics1> IADLXSystemMetrics1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXSystemMetrics1, L"IADLXSystemMetrics1")

typedef struct IADLXSystemMetrics1 IADLXSystemMetrics1;
typedef struct IADLXSystemMetrics1Vtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXSystemMetrics1* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXSystemMetrics1* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXSystemMetrics1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXSystemMetrics
    ADLX_RESULT (ADLX_STD_CALL *TimeStamp)(IADLXSystemMetrics1* pThis, adlx_int64* ms);
    ADLX_RESULT (ADLX_STD_CALL *CPUUsage)(IADLXSystemMetrics1* pThis, adlx_double* data);
    ADLX_RESULT (ADLX_STD_CALL *SystemRAM)(IADLXSystemMetrics1* pThis, adlx_int* data);
    ADLX_RESULT (ADLX_STD_CALL *SmartShift)(IADLXSystemMetrics1* pThis, adlx_int* data);
    
    //IADLXSystemMetrics1
    ADLX_RESULT (ADLX_STD_CALL *PowerDistribution)(IADLXSystemMetrics1* pThis, adlx_int* apuShiftValue, adlx_int* gpuShiftValue, adlx_int* apuShiftLimit, adlx_int* gpuShiftLimit, adlx_int* totalShiftLimit);
}IADLXSystemMetrics1Vtbl;
struct IADLXSystemMetrics1 { const IADLXSystemMetrics1Vtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXSystemMetrics1

#endif//ADLX_IPERFORMANCEMONITORING1_H
