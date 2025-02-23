//
// Copyright (c) 2021 - 2024 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_IPERFORMANCEMONITORING2_H
#define ADLX_IPERFORMANCEMONITORING2_H
#pragma once

#include "IPerformanceMonitoring.h"

#pragma region IADLXGPUMetricsSupport1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPUMetricsSupport1 : public IADLXGPUMetricsSupport
    {
    public:
        ADLX_DECLARE_IID(L"IADLXGPUMetricsSupport1")

        /**
        *@page DOX_IADLXGPUMetricsSupport1_IsSupportedGPUMemoryTemperature IsSupportedGPUMemoryTemperature
        *@ENG_START_DOX @brief Checks if the GPU memory temperature metric reporting is supported on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupportedGPUMemoryTemperature (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of GPU memory temperature metric reporting is returned. The variable is __true__ if the GPU memory temperature metric reporting is supported\. The variable is __false__ if the GPU memory temperature metric reporting is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX If the state of GPU memory temperature metric reporting is successfully returned, __ADLX_OK__ is returned. <br>
        *If the state of GPU memory temperature metric reporting is not successfully returned, an error code is returned. <br>
        *Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *
        *@copydoc IADLXGPUMetricsSupport1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsSupportedGPUMemoryTemperature (adlx_bool* supported) = 0;

        /**
        *@page DOX_IADLXGPUMetricsSupport1_GetGPUMemoryTemperatureRange GetGPUMemoryTemperatureRange
        *@ENG_START_DOX @brief Gets the minimum and maximum GPU memory temperature on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetGPUMemoryTemperatureRange (adlx_int* minValue, adlx_int* maxValue)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],minValue,adlx_int*,@ENG_START_DOX The pointer to a variable where the minimum GPU memory temperature (in °C) is returned. @ENG_END_DOX}
        * @paramrow{2.,[out],maxValue,adlx_int*,@ENG_START_DOX The pointer to a variable where the maximum GPU memory temperature (in °C) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the GPU memory temperature range is successfully returned, __ADLX_OK__ is returned.<br>
        * If the GPU memory temperature range is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The minimum and maximum GPU memory temperature are read only. @ENG_END_DOX
        *
        *
        *@copydoc IADLXGPUMetricsSupport1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetGPUMemoryTemperatureRange (adlx_int* minValue, adlx_int* maxValue) = 0;

        /**
        *@page DOX_IADLXGPUMetricsSupport1_IsSupportedNPUFrequency IsSupportedNPUFrequency
        *@ENG_START_DOX @brief Checks if the NPU frequency metric reporting is supported on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupportedNPUFrequency (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of NPU frequency metric reporting is returned. The variable is __true__ if the NPU frequency metric reporting is supported\. The variable is __false__ if the NPU frequency metric reporting is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX If the state of NPU frequency metric reporting is successfully returned, __ADLX_OK__ is returned. <br>
        *If the state of NPU frequency metric reporting is not successfully returned, an error code is returned. <br>
        *Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *
        *@copydoc IADLXGPUMetricsSupport1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsSupportedNPUFrequency (adlx_bool* supported) = 0;

        /**
        *@page DOX_IADLXGPUMetricsSupport1_GetNPUFrequencyRange GetNPUFrequencyRange
        *@ENG_START_DOX @brief Gets the minimum and maximum NPU frequency on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetNPUFrequencyRange (adlx_int* minValue, adlx_int* maxValue)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],minValue,adlx_int*,@ENG_START_DOX The pointer to a variable where the minimum NPU frequency (in MHZ) is returned. @ENG_END_DOX}
        * @paramrow{2.,[out],maxValue,adlx_int*,@ENG_START_DOX The pointer to a variable where the maximum NPU frequency (in MHZ) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the NPU frequency range is successfully returned, __ADLX_OK__ is returned.<br>
        * If the NPU frequency range is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The minimum and maximum NPU frequency are read only. @ENG_END_DOX
        *
        *
        *@copydoc IADLXGPUMetricsSupport1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetNPUFrequencyRange (adlx_int* minValue, adlx_int* maxValue) = 0;

        /**
        *@page DOX_IADLXGPUMetricsSupport1_IsSupportedNPUActivityLevel IsSupportedNPUActivityLevel
        *@ENG_START_DOX @brief Checks if the NPU activity level metric reporting is supported on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupportedNPUActivityLevel (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of NPU activity level metric reporting is returned. The variable is __true__ if the NPU activity level metric reporting is supported\. The variable is __false__ if the NPU activity level metric reporting is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX If the state of NPU activity level metric reporting is successfully returned, __ADLX_OK__ is returned. <br>
        *If the state of NPU activity level metric reporting is not successfully returned, an error code is returned. <br>
        *Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *
        *@copydoc IADLXGPUMetricsSupport1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsSupportedNPUActivityLevel (adlx_bool* supported) = 0;

        /**
        *@page DOX_IADLXGPUMetricsSupport1_GetNPUActivityLevelRange GetNPUActivityLevelRange
        *@ENG_START_DOX @brief Gets the minimum and maximum NPU activity level on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetNPUActivityLevelRange (adlx_int* minValue, adlx_int* maxValue)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],minValue,adlx_int*,@ENG_START_DOX The pointer to a variable where the minimum NPU activity level (in %) is returned. @ENG_END_DOX}
        * @paramrow{2.,[out],maxValue,adlx_int*,@ENG_START_DOX The pointer to a variable where the maximum NPU activity level (in %) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the NPU activity level range is successfully returned, __ADLX_OK__ is returned.<br>
        * If the NPU activity level range is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The minimum and maximum NPU activity level are read only. @ENG_END_DOX
        *
        *
        *@copydoc IADLXGPUMetricsSupport1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetNPUActivityLevelRange (adlx_int* minValue, adlx_int* maxValue) = 0;
    };
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXGPUMetricsSupport1> IADLXGPUMetricsSupport1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXGPUMetricsSupport1, L"IADLXGPUMetricsSupport1")

typedef struct IADLXGPUMetricsSupport1 IADLXGPUMetricsSupport1;
typedef struct IADLXGPUMetricsSupport1Vtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXGPUMetricsSupport1* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXGPUMetricsSupport1* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXGPUMetricsSupport1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXGPUMetricsSupport
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUUsage)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUClockSpeed)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUVRAMClockSpeed)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUTemperature)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUHotspotTemperature)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUPower)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUTotalBoardPower)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUFanSpeed)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUVRAM)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUVoltage)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);

    ADLX_RESULT(ADLX_STD_CALL* GetGPUUsageRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUClockSpeedRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUVRAMClockSpeedRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUTemperatureRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUHotspotTemperatureRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUPowerRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUFanSpeedRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUVRAMRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUVoltageRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUTotalBoardPowerRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUIntakeTemperatureRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUIntakeTemperature)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);

    //IADLXGPUMetricsSupport1
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedGPUMemoryTemperature)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUMemoryTemperatureRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedNPUFrequency)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetNPUFrequencyRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedNPUActivityLevel)(IADLXGPUMetricsSupport1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetNPUActivityLevelRange)(IADLXGPUMetricsSupport1* pThis, adlx_int* minValue, adlx_int* maxValue);
}IADLXGPUMetricsSupport1Vtbl;
struct IADLXGPUMetricsSupport1 { const IADLXGPUMetricsSupport1Vtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXGPUMetricsSupport1

#pragma region IADLXGPUMetrics1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPUMetrics1 : public IADLXGPUMetrics
    {
    public:
        ADLX_DECLARE_IID (L"IADLXGPUMetrics1")

        /**
        *@page DOX_IADLXGPUMetrics1_GPUMemoryTemperature GPUMemoryTemperature
        *@ENG_START_DOX
        *@brief Gets the GPU memory temperature of a GPU metric sample.
        *@details GPUMemoryTemperature reports the GPU memory temperature.
        *@ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GPUMemoryTemperature (adlx_double* data)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,data,adlx_double* ,@ENG_START_DOX The pointer to a variable where the GPU memory temperature (in °C) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the GPU memory temperature is successfully returned, __ADLX_OK__ is returned. <br>
        * If the GPU memory temperature is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@copydoc IADLXGPUMetrics1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GPUMemoryTemperature(adlx_double* data) = 0;

        /**
        *@page DOX_IADLXGPUMetrics1_NPUFrequency NPUFrequency
        *@ENG_START_DOX
        *@brief Gets the NPU frequency of a GPU metric sample.
        *@ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    NPUFrequency (adlx_int* data)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,data,adlx_int* ,@ENG_START_DOX The pointer to a variable where the NPU frequency (in MHz) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the NPU frequency is successfully returned, __ADLX_OK__ is returned. <br>
        * If the NPU frequency is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@copydoc IADLXGPUMetrics1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL NPUFrequency(adlx_int* data) = 0;

        /**
        *@page DOX_IADLXGPUMetrics1_NPUActivityLevel NPUActivityLevel
        *@ENG_START_DOX
        *@brief Gets the NPU activity level of a GPU metric sample.
        *@ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    NPUActivityLevel (adlx_int* data)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,data,adlx_int* ,@ENG_START_DOX The pointer to a variable where the NPU activity level (in %) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the NPU activity level is successfully returned, __ADLX_OK__ is returned. <br>
        * If the NPU activity level is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@copydoc IADLXGPUMetrics1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL NPUActivityLevel(adlx_int* data) = 0;
    };
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXGPUMetrics1> IADLXGPUMetrics1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXGPUMetrics1, L"IADLXGPUMetrics1")

typedef struct IADLXGPUMetrics1 IADLXGPUMetrics1;
typedef struct IADLXGPUMetrics1Vtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXGPUMetrics1* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXGPUMetrics1* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXGPUMetrics1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXGPUMetrics
    ADLX_RESULT(ADLX_STD_CALL* TimeStamp)(IADLXGPUMetrics1* pThis, adlx_int64* ms);
    ADLX_RESULT(ADLX_STD_CALL* GPUUsage)(IADLXGPUMetrics1* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUClockSpeed)(IADLXGPUMetrics1* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUVRAMClockSpeed)(IADLXGPUMetrics1* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUTemperature)(IADLXGPUMetrics1* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUHotspotTemperature)(IADLXGPUMetrics1* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUPower)(IADLXGPUMetrics1* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUTotalBoardPower)(IADLXGPUMetrics1* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUFanSpeed)(IADLXGPUMetrics1* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUVRAM)(IADLXGPUMetrics1* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUVoltage)(IADLXGPUMetrics1* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* GPUIntakeTemperature)(IADLXGPUMetrics1* pThis, adlx_double* data);

    //IADLXGPUMetrics1
    ADLX_RESULT(ADLX_STD_CALL* GPUMemoryTemperature)(IADLXGPUMetrics1* pThis, adlx_double* data);
    ADLX_RESULT(ADLX_STD_CALL* NPUFrequency)(IADLXGPUMetrics1* pThis, adlx_int* data);
    ADLX_RESULT(ADLX_STD_CALL* NPUActivityLevel)(IADLXGPUMetrics1* pThis, adlx_int* data);
}IADLXGPUMetrics1Vtbl;
struct IADLXGPUMetrics1 { const IADLXGPUMetrics1Vtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXGPUMetrics1

#endif//ADLX_IPERFORMANCEMONITORING2_H
