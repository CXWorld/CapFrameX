//
// Copyright (c) 2021 - 2024 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_IDISPLAYS2_H
#define ADLX_IDISPLAYS2_H
#pragma once

#include "IDisplays1.h"

//-------------------------------------------------------------------------------------------------
//IDisplays2.h - Interfaces for ADLX Display Information functionality

#pragma region IADLXDisplayServices2

#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXDisplayConnectivityExperience;
    class ADLX_NO_VTABLE IADLXDisplayServices2 : public IADLXDisplayServices1
    {
    public:
        ADLX_DECLARE_IID(L"IADLXDisplayServices2")

        /**
        *@page DOX_IADLXDisplayServices2_GetDisplayConnectivityExperience GetDisplayConnectivityExperience
        *@ENG_START_DOX @brief Gets the reference counted DCE interface for a display. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetDisplayConnectivityExperience (@ref DOX_IADLXDisplay* pDisplay, @ref DOX_IADLXDisplayConnectivityExperience ** ppDisplayConnectivityExperience)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in],pDisplay,@ref DOX_IADLXDisplay*,@ENG_START_DOX The pointer to the display interface. @ENG_END_DOX}
        *@paramrow{2.,[out],ppDisplayConnectivityExperience,@ref DOX_IADLXDisplayConnectivityExperience **,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppDisplayConnectivityExperience__ to __nullptr__. @ENG_END_DOX}
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
        *@copydoc IADLXDisplayServices2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetDisplayConnectivityExperience(IADLXDisplay* pDisplay, IADLXDisplayConnectivityExperience** ppDisplayConnectivityExperience) = 0;
    };  //IADLXDisplayServices2
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXDisplayServices2> IADLXDisplayServices2Ptr;
} // namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXDisplayServices2, L"IADLXDisplayServices2")
typedef struct IADLXDisplayServices2 IADLXDisplayServices2;

typedef struct IADLXDisplayConnectivityExperience IADLXDisplayConnectivityExperience;

typedef struct IADLXDisplayServices2Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXDisplayServices2* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXDisplayServices2* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXDisplayServices2* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXDisplayServices
    ADLX_RESULT(ADLX_STD_CALL* GetNumberOfDisplays)(IADLXDisplayServices2* pThis, adlx_uint* numDisplays);
    ADLX_RESULT(ADLX_STD_CALL* GetDisplays)(IADLXDisplayServices2* pThis, IADLXDisplayList** ppDisplays);
    ADLX_RESULT(ADLX_STD_CALL* Get3DLUT)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplay3DLUT** ppDisp3DLUT);
    ADLX_RESULT(ADLX_STD_CALL* GetGamut)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayGamut** ppDispGamut);
    ADLX_RESULT(ADLX_STD_CALL* GetGamma)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayGamma** ppDispGamma);
    ADLX_RESULT(ADLX_STD_CALL* GetDisplayChangedHandling)(IADLXDisplayServices2* pThis, IADLXDisplayChangedHandling** ppDisplayChangedHandling);
    ADLX_RESULT(ADLX_STD_CALL* GetFreeSync)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayFreeSync** ppFreeSync);
    ADLX_RESULT(ADLX_STD_CALL* GetVirtualSuperResolution)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayVSR** ppVSR);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUScaling)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayGPUScaling** ppGPUScaling);
    ADLX_RESULT(ADLX_STD_CALL* GetScalingMode)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayScalingMode** ppScalingMode);
    ADLX_RESULT(ADLX_STD_CALL* GetIntegerScaling)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayIntegerScaling** ppIntegerScaling);
    ADLX_RESULT(ADLX_STD_CALL* GetColorDepth)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayColorDepth** ppColorDepth);
    ADLX_RESULT(ADLX_STD_CALL* GetPixelFormat)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayPixelFormat** ppPixelFormat);
    ADLX_RESULT(ADLX_STD_CALL* GetCustomColor)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayCustomColor** ppCustomColor);
    ADLX_RESULT(ADLX_STD_CALL* GetHDCP)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayHDCP** ppHDCP);
    ADLX_RESULT(ADLX_STD_CALL* GetCustomResolution)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayCustomResolution** ppCustomResolution);
    ADLX_RESULT(ADLX_STD_CALL* GetVariBright)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayVariBright** ppVariBright);

    //IADLXDisplayServices1
    ADLX_RESULT(ADLX_STD_CALL* GetDisplayBlanking)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayBlanking** ppDisplayBlanking);

    //IADLXDisplayServices2
    ADLX_RESULT(ADLX_STD_CALL* GetDisplayConnectivityExperience)(IADLXDisplayServices2* pThis, IADLXDisplay* pDisplay, IADLXDisplayConnectivityExperience** ppDisplayConnectivityExperience);
} IADLXDisplayServices2Vtbl;

struct IADLXDisplayServices2 { const IADLXDisplayServices2Vtbl* pVtbl; };
#endif

#pragma endregion IADLXDisplayServices2

#pragma region IADLXDisplaySettingsChangedEvent2
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXDisplaySettingsChangedEvent2 : public IADLXDisplaySettingsChangedEvent1
    {
    public:
        ADLX_DECLARE_IID(L"IADLXDisplaySettingsChangedEvent2")

        /**
        *@page DOX_IADLXDisplaySettingsChangedEvent2_IsConnectivityExperienceChanged IsConnectivityExperienceChanged
        *@ENG_START_DOX @brief Checks if the DCE settings of the display are changed. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsDisplayConnectivityExperienceChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX
        * If the DCE settings are changed, __true__ is returned. <br>
        * If the DCE settings are not changed, __false__ is returned. @ENG_END_DOX
        *
        *
        *@addinfo
        *@ENG_START_DOX
        * __Note:__ To obtain the display, use @ref DOX_IADLXDisplaySettingsChangedEvent_GetDisplay.
        *@ENG_END_DOX
        *
        *@copydoc IADLXDisplaySettingsChangedEvent2_REQ_TABLE
        */
        virtual adlx_bool ADLX_STD_CALL IsDisplayConnectivityExperienceChanged() = 0;
    }; //IADLXDisplaySettingsChangedEvent2
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXDisplaySettingsChangedEvent2> IADLXDisplaySettingsChangedEvent2Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXDisplaySettingsChangedEvent2, L"IADLXDisplaySettingsChangedEvent2")
typedef struct IADLXDisplaySettingsChangedEvent2 IADLXDisplaySettingsChangedEvent2;

typedef struct IADLXDisplaySettingsChangedEvent2Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXDisplaySettingsChangedEvent2* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXDisplaySettingsChangedEvent2* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXChangedEvent
    ADLX_SYNC_ORIGIN(ADLX_STD_CALL* GetOrigin)(IADLXDisplaySettingsChangedEvent2* pThis);

    // IADLXDisplaySettingsChangedEvent interface
    ADLX_RESULT(ADLX_STD_CALL* GetDisplay)(IADLXDisplaySettingsChangedEvent2* pThis, IADLXDisplay** ppDisplay);
    adlx_bool(ADLX_STD_CALL* IsFreeSyncChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsVSRChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsGPUScalingChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsScalingModeChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsIntegerScalingChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsColorDepthChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsPixelFormatChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsHDCPChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorHueChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorSaturationChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorBrightnessChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorTemperatureChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorContrastChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomResolutionChanged)(IADLXDisplaySettingsChangedEvent2* pThis);
    adlx_bool(ADLX_STD_CALL* IsVariBrightChanged)(IADLXDisplaySettingsChangedEvent2* pThis);

    // IADLXDisplaySettingsChangedEvent1 interface
    adlx_bool(ADLX_STD_CALL* IsDisplayBlankingChanged)(IADLXDisplaySettingsChangedEvent2* pThis);

    // IADLXDisplaySettingsChangedEvent2 interface
    adlx_bool(ADLX_STD_CALL* IsDisplayConnectivityExperienceChanged)(IADLXDisplaySettingsChangedEvent2* pThis);


} IADLXDisplaySettingsChangedEvent2Vtbl;

struct IADLXDisplaySettingsChangedEvent2 { const IADLXDisplaySettingsChangedEvent2Vtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXDisplaySettingsChangedEvent2

#endif //ADLX_IDISPLAYS2_H
