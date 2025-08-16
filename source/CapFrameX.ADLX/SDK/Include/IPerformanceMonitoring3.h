//
// Copyright (c) 2024 - 2025 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_IPERFORMANCEMONITORING3_H
#define ADLX_IPERFORMANCEMONITORING3_H
#pragma once

#include "IPerformanceMonitoring2.h"

#pragma region IADLXGPUMetricsSupport2
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPUMetricsSupport2 : public IADLXGPUMetricsSupport1
    {
    public:
        ADLX_DECLARE_IID(L"IADLXGPUMetricsSupport2")

        /**
        *@page DOX_IADLXGPUMetricsSupport2_IsSupportedGPUSharedMemory IsSupportedGPUSharedMemory
        *@ENG_START_DOX @brief Checks if the shared GPU memory metric reporting is supported on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupportedGPUSharedMemory (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of shared GPU memory metric reporting is returned. The variable is __true__ if the shared GPU memory metric reporting is supported\. The variable is __false__ if the shared GPU memory metric reporting is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX If the state of shared GPU memory metric reporting is successfully returned, __ADLX_OK__ is returned. <br>
        *If the state of shared GPU memory metric reporting is not successfully returned, an error code is returned. <br>
        *Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *
        *@copydoc IADLXGPUMetricsSupport2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsSupportedGPUSharedMemory (adlx_bool* supported) = 0;

        /**
        *@page DOX_IADLXGPUMetricsSupport2_GetGPUSharedMemoryRange GetGPUSharedMemoryRange
        *@ENG_START_DOX @brief Gets the minimum and maximum shared GPU memory on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetGPUSharedMemoryRange (adlx_int* minValue, adlx_int* maxValue)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],minValue,adlx_int*,@ENG_START_DOX The pointer to a variable where the minimum shared GPU memory (in MB) is returned. @ENG_END_DOX}
        * @paramrow{2.,[out],maxValue,adlx_int*,@ENG_START_DOX The pointer to a variable where the maximum shared GPU memory (in MB) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the shared GPU memory range is successfully returned, __ADLX_OK__ is returned.<br>
        * If the shared GPU memory range is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The minimum and maximum shared GPU memory are read only. @ENG_END_DOX
        *
        *
        *@copydoc IADLXGPUMetricsSupport2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetGPUSharedMemoryRange (adlx_int* minValue, adlx_int* maxValue) = 0;
    };
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXGPUMetricsSupport2> IADLXGPUMetricsSupport2Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXGPUMetricsSupport2, L"IADLXGPUMetricsSupport2")

typedef struct IADLXGPUMetricsSupport2 IADLXGPUMetricsSupport2;
typedef struct IADLXGPUMetricsSupport2Vtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXGPUMetricsSupport2* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXGPUMetricsSupport2* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXGPUMetricsSupport2* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXGPUMetricsSupport
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUUsage)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUClockSpeed)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUVRAMClockSpeed)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUTemperature)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUHotspotTemperature)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUPower)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUTotalBoardPower)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUFanSpeed)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUVRAM)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUVoltage)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);

    ADLX_RESULT(ADLX_STD_CALL* GetGPUUsageRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUClockSpeedRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUVRAMClockSpeedRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUTemperatureRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUHotspotTemperatureRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUPowerRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUFanSpeedRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUVRAMRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUVoltageRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUTotalBoardPowerRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUIntakeTemperatureRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUIntakeTemperature)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);

    //IADLXGPUMetricsSupport1
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUMemoryTemperature)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUMemoryTemperatureRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedNPUFrequency)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetNPUFrequencyRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedNPUActivityLevel)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetNPUActivityLevelRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);

    //IADLXGPUMetricsSupport2
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUSharedMemory)(IADLXGPUMetricsSupport2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUSharedMemoryRange)(IADLXGPUMetricsSupport2* pThis, adlx_int* minValue, adlx_int* maxValue);
}IADLXGPUMetricsSupport2Vtbl;
struct IADLXGPUMetricsSupport2 { const IADLXGPUMetricsSupport2Vtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXGPUMetricsSupport2

#pragma region IADLXGPUMetrics2
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPUMetrics2 : public IADLXGPUMetrics1
    {
    public:
        ADLX_DECLARE_IID (L"IADLXGPUMetrics2")

        /**
        *@page DOX_IADLXGPUMetrics2_GPUSharedMemory GPUSharedMemory
        *@ENG_START_DOX
        *@brief Gets the shared GPU memory of a GPU metric sample.
        *@ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GPUSharedMemory (adlx_int* data)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,data,adlx_int* ,@ENG_START_DOX The pointer to a variable where the shared GPU memory (in MB) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the shared GPU memory is successfully returned, __ADLX_OK__ is returned. <br>
        * If the shared GPU memory is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@copydoc IADLXGPUMetrics2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GPUSharedMemory(adlx_int* data) = 0;
    };
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXGPUMetrics2> IADLXGPUMetrics2Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXGPUMetrics2, L"IADLXGPUMetrics2")

typedef struct IADLXGPUMetrics2 IADLXGPUMetrics2;
typedef struct IADLXGPUMetrics2Vtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXGPUMetrics2* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXGPUMetrics2* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXGPUMetrics2* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXGPUMetrics
    ADLX_RESULT(ADLX_STD_CALL* TimeStamp)(IADLXGPUMetrics2* pThis, adlx_int64* ms);
    ADLX_RESULT(ADLX_STD_CALL* GPUUsage)(IADLXGPUMetrics2* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUClockSpeed)(IADLXGPUMetrics2* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUVRAMClockSpeed)(IADLXGPUMetrics2* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUTemperature)(IADLXGPUMetrics2* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUHotspotTemperature)(IADLXGPUMetrics2* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUPower)(IADLXGPUMetrics2* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUTotalBoardPower)(IADLXGPUMetrics2* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUFanSpeed)(IADLXGPUMetrics2* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUVRAM)(IADLXGPUMetrics2* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUVoltage)(IADLXGPUMetrics2* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUIntakeTemperature)(IADLXGPUMetrics2* pThis, adlx_double* data);

    //IADLXGPUMetrics1
    ADLX_RESULT(ADLX_STD_CALL* GPUMemoryTemperature)(IADLXGPUMetrics2* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* NPUFrequency)(IADLXGPUMetrics2* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* NPUActivityLevel)(IADLXGPUMetrics2* pThis, adlx_int* data);

    //IADLXGPUMetrics2
    ADLX_RESULT(ADLX_STD_CALL* GPUSharedMemory)(IADLXGPUMetrics2* pThis, adlx_int* data);
}IADLXGPUMetrics2Vtbl;
struct IADLXGPUMetrics2 { const IADLXGPUMetrics2Vtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXGPUMetrics2

#endif//ADLX_IPERFORMANCEMONITORING3_H
