//
// Copyright (c) 2021 - 2025 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_IDISPLAYS3_H
#define ADLX_IDISPLAYS3_H
#pragma once

#include "IDisplays2.h"

//-------------------------------------------------------------------------------------------------
//IDisplays3.h - Interfaces for ADLX Display dynamic refresh rate control functionality

#pragma region IADLXDisplayServices3

#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXDisplayDynamicRefreshRateControl;
    class ADLX_NO_VTABLE IADLXDisplayFreeSyncColorAccuracy;
    class ADLX_NO_VTABLE IADLXDisplayServices3 : public IADLXDisplayServices2
    {
    public:
        ADLX_DECLARE_IID(L"IADLXDisplayServices3")

        /**
        *@page DOX_IADLXDisplayServices3_GetDynamicRefreshRateControl GetDynamicRefreshRateControl
        *@ENG_START_DOX @brief Gets the reference counted Dynamic Refresh Rate Control interface for a display. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetDynamicRefreshRateControl (@ref DOX_IADLXDisplay* pDisplay, @ref DOX_IADLXDisplayDynamicRefreshRateControl** ppDRRC)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in],pDisplay,@ref DOX_IADLXDisplay*,@ENG_START_DOX The pointer to the display interface. @ENG_END_DOX}
        *@paramrow{2.,[out],ppDRRC,@ref DOX_IADLXDisplayDynamicRefreshRateControl**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppDRRC__ to __nullptr__. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX
        * If the interface is successfully returned, __ADLX_OK__ is returned. <br>
        * If the interface is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed. @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        *In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. @ENG_END_DOX
        *
        *@copydoc IADLXDisplayServices3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetDynamicRefreshRateControl (IADLXDisplay* pDisplay, IADLXDisplayDynamicRefreshRateControl** ppDRRC) = 0;

        /**
        *@page DOX_IADLXDisplayServices3_GetFreeSyncColorAccuracy GetFreeSyncColorAccuracy
        *@ENG_START_DOX @brief Gets the reference counted FreeSync color accuracy interface for a display. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetFreeSyncColorAccuracy (@ref DOX_IADLXDisplay* pDisplay, @ref DOX_IADLXDisplayFreeSyncColorAccuracy** ppFSCA)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in],pDisplay,@ref DOX_IADLXDisplay*,@ENG_START_DOX The pointer to the display interface. @ENG_END_DOX}
        *@paramrow{2.,[out],ppFSCA,@ref DOX_IADLXDisplayFreeSyncColorAccuracy**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppFSCA to __nullptr__. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX
        * If the interface is successfully returned, __ADLX_OK__ is returned. <br>
        * If the interface is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed. @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        *In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. @ENG_END_DOX
        *
        *@copydoc IADLXDisplayServices3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetFreeSyncColorAccuracy(IADLXDisplay* pDisplay, IADLXDisplayFreeSyncColorAccuracy** ppFSCA) = 0;
    };  //IADLXDisplayServices3
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXDisplayServices3> IADLXDisplayServices3Ptr;
} // namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXDisplayServices3, L"IADLXDisplayServices3")
typedef struct IADLXDisplayServices3 IADLXDisplayServices3;

typedef struct IADLXDisplayDynamicRefreshRateControl IADLXDisplayDynamicRefreshRateControl;
typedef struct IADLXDisplayFreeSyncColorAccuracy IADLXDisplayFreeSyncColorAccuracy;


typedef struct IADLXDisplayServices3Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXDisplayServices3* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXDisplayServices3* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXDisplayServices3* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXDisplayServices
    ADLX_RESULT(ADLX_STD_CALL* GetNumberOfDisplays)(IADLXDisplayServices3* pThis, adlx_uint* numDisplays);
    ADLX_RESULT(ADLX_STD_CALL* GetDisplays)(IADLXDisplayServices3* pThis, IADLXDisplayList** ppDisplays);
    ADLX_RESULT(ADLX_STD_CALL* Get3DLUT)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplay3DLUT** ppDisp3DLUT);
    ADLX_RESULT(ADLX_STD_CALL* GetGamut)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayGamut** ppDispGamut);
    ADLX_RESULT(ADLX_STD_CALL* GetGamma)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayGamma** ppDispGamma);
    ADLX_RESULT(ADLX_STD_CALL* GetDisplayChangedHandling)(IADLXDisplayServices3* pThis, IADLXDisplayChangedHandling** ppDisplayChangedHandling);
    ADLX_RESULT(ADLX_STD_CALL* GetFreeSync)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayFreeSync** ppFreeSync);
    ADLX_RESULT(ADLX_STD_CALL* GetVirtualSuperResolution)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayVSR** ppVSR);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUScaling)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayGPUScaling** ppGPUScaling);
    ADLX_RESULT(ADLX_STD_CALL* GetScalingMode)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayScalingMode** ppScalingMode);
    ADLX_RESULT(ADLX_STD_CALL* GetIntegerScaling)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayIntegerScaling** ppIntegerScaling);
    ADLX_RESULT(ADLX_STD_CALL* GetColorDepth)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayColorDepth** ppColorDepth);
    ADLX_RESULT(ADLX_STD_CALL* GetPixelFormat)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayPixelFormat** ppPixelFormat);
    ADLX_RESULT(ADLX_STD_CALL* GetCustomColor)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayCustomColor** ppCustomColor);
    ADLX_RESULT(ADLX_STD_CALL* GetHDCP)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayHDCP** ppHDCP);
    ADLX_RESULT(ADLX_STD_CALL* GetCustomResolution)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayCustomResolution** ppCustomResolution);
    ADLX_RESULT(ADLX_STD_CALL* GetVariBright)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayVariBright** ppVariBright);

    //IADLXDisplayServices1
    ADLX_RESULT(ADLX_STD_CALL* GetDisplayBlanking)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayBlanking** ppDisplayBlanking);

    //IADLXDisplayServices2
    ADLX_RESULT(ADLX_STD_CALL* GetDisplayConnectivityExperience)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayConnectivityExperience** ppDisplayConnectivityExperience);

    //IADLXDisplayServices3
    ADLX_RESULT(ADLX_STD_CALL* GetDynamicRefreshRateControl)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayDynamicRefreshRateControl** ppDRRC);
    ADLX_RESULT(ADLX_STD_CALL* GetFreeSyncColorAccuracy)(IADLXDisplayServices3* pThis, IADLXDisplay* pDisplay, IADLXDisplayFreeSyncColorAccuracy** ppFSCA);
} IADLXDisplayServices3Vtbl;

struct IADLXDisplayServices3 { const IADLXDisplayServices3Vtbl* pVtbl; };
#endif

#pragma endregion IADLXDisplayServices3

#pragma region IADLXDisplaySettingsChangedEvent3
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXDisplaySettingsChangedEvent3 : public IADLXDisplaySettingsChangedEvent2
    {
    public:
        ADLX_DECLARE_IID(L"IADLXDisplaySettingsChangedEvent3")

        /**
        *@page DOX_IADLXDisplaySettingsChangedEvent3_IsDisplayDynamicRefreshRateControlChanged  IsDisplayDynamicRefreshRateControlChanged
        *@ENG_START_DOX @brief Checks if the dynamic refresh rate control settings of the display are changed. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsDisplayDynamicRefreshRateControlChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX
        * If the Dynamic Refresh Rate Control settings are changed, __true__ is returned. <br>
        * If the Dynamic Refresh Rate Control settings are not changed, __false__ is returned. @ENG_END_DOX
        *
        *
        *@addinfo
        *@ENG_START_DOX
        * __Note:__ To obtain the display, use @ref DOX_IADLXDisplaySettingsChangedEvent_GetDisplay.
        *@ENG_END_DOX
        *
        *@copydoc IADLXDisplaySettingsChangedEvent3_REQ_TABLE
        */
        virtual adlx_bool ADLX_STD_CALL IsDisplayDynamicRefreshRateControlChanged () = 0;

        /**
        *@page DOX_IADLXDisplaySettingsChangedEvent3_IsFreeSyncColorAccuracyChanged  IsFreeSyncColorAccuracyChanged
        *@ENG_START_DOX @brief Checks for changes to the configuration of the HDR media profile on a display. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsFreeSyncColorAccuracyChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX
        * If the FreeSync color accuracy settings are changed, __true__ is returned. <br>
        * If the FreeSync color accuracy settings are not changed, __false__ is returned. @ENG_END_DOX
        *
        *
        *@addinfo
        *@ENG_START_DOX
        * __Note:__ To obtain the display, use @ref DOX_IADLXDisplaySettingsChangedEvent_GetDisplay.
        *@ENG_END_DOX
        *
        *@copydoc IADLXDisplaySettingsChangedEvent3_REQ_TABLE
        */
        virtual adlx_bool ADLX_STD_CALL IsFreeSyncColorAccuracyChanged() = 0;
    }; //IADLXDisplaySettingsChangedEvent3
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXDisplaySettingsChangedEvent3> IADLXDisplaySettingsChangedEvent3Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXDisplaySettingsChangedEvent3, L"IADLXDisplaySettingsChangedEvent3")
typedef struct IADLXDisplaySettingsChangedEvent3 IADLXDisplaySettingsChangedEvent3;

typedef struct IADLXDisplaySettingsChangedEvent3Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXDisplaySettingsChangedEvent3* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXDisplaySettingsChangedEvent3* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXChangedEvent
    ADLX_SYNC_ORIGIN(ADLX_STD_CALL* GetOrigin)(IADLXDisplaySettingsChangedEvent3* pThis);

    // IADLXDisplaySettingsChangedEvent interface
    ADLX_RESULT(ADLX_STD_CALL* GetDisplay)(IADLXDisplaySettingsChangedEvent3* pThis, IADLXDisplay** ppDisplay);
    adlx_bool(ADLX_STD_CALL* IsFreeSyncChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsVSRChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsGPUScalingChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsScalingModeChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsIntegerScalingChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsColorDepthChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsPixelFormatChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsHDCPChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorHueChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorSaturationChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorBrightnessChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorTemperatureChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorContrastChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomResolutionChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsVariBrightChanged)(IADLXDisplaySettingsChangedEvent3* pThis);

    // IADLXDisplaySettingsChangedEvent1 interface
    adlx_bool(ADLX_STD_CALL* IsDisplayBlankingChanged)(IADLXDisplaySettingsChangedEvent3* pThis);

    // IADLXDisplaySettingsChangedEvent2 interface
    adlx_bool(ADLX_STD_CALL* IsDisplayConnectivityExperienceChanged)(IADLXDisplaySettingsChangedEvent3* pThis);

    // IADLXDisplaySettingsChangedEvent3 interface
    adlx_bool(ADLX_STD_CALL* IsDisplayDynamicRefreshRateControlChanged)(IADLXDisplaySettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsFreeSyncColorAccuracyChanged)(IADLXDisplaySettingsChangedEvent3* pThis);

} IADLXDisplaySettingsChangedEvent3Vtbl;

struct IADLXDisplaySettingsChangedEvent3 { const IADLXDisplaySettingsChangedEvent3Vtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXDisplaySettingsChangedEvent3

#endif //ADLX_IDISPLAYS3_H
