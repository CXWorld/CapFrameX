//
// Copyright (c) 2021 - 2024 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_IDISPLAYS1_H
#define ADLX_IDISPLAYS1_H
#pragma once

#include "IDisplays.h"

//-------------------------------------------------------------------------------------------------
//IDisplays1.h - Interfaces for ADLX Display Information functionality

#pragma region IADLXDisplayServices1

#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXDisplayConnectivityExperience;
    class ADLX_NO_VTABLE IADLXDisplayBlanking;
    class ADLX_NO_VTABLE IADLXDisplayServices1 : public IADLXDisplayServices
    {
    public:
        ADLX_DECLARE_IID(L"IADLXDisplayServices1")

        /**
        *@page DOX_IADLXDisplayServices1_GetDisplayBlanking GetDisplayBlanking
        *@ENG_START_DOX @brief Gets the reference counted display blanking interface of a display. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetDisplayBlanking (IADLXDisplay* pDisplay, @ref DOX_IADLXDisplayBlanking** ppDisplayBlanking)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in],pDisplay,@ref DOX_IADLXDisplay*,@ENG_START_DOX The pointer to the display interface. @ENG_END_DOX}
        *@paramrow{2.,[out],ppDisplayBlanking,@ref DOX_IADLXDisplayBlanking **,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppDisplayBlanking__ to __nullptr__. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX
        * If the interface is successfully returned, __ADLX_OK__ is returned. <br>
        * If the interface is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it's no longer needed. @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        *When using ADLX interfaces as smart pointers in C++, it isn't necessary to call @ref DOX_IADLXInterface_Release as it's called by smart pointers in the internal implementation. @ENG_END_DOX
        *
        *@copydoc IADLXDisplayServices1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetDisplayBlanking(IADLXDisplay* pDisplay, IADLXDisplayBlanking** ppDisplayBlanking) = 0;

    };  //IADLXDisplayServices1
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXDisplayServices1> IADLXDisplayServices1Ptr;
} // namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXDisplayServices1, L"IADLXDisplayServices1")
typedef struct IADLXDisplayServices1 IADLXDisplayServices1;

typedef struct IADLXDisplayFreeSync IADLXDisplayFreeSync;
typedef struct IADLXDisplayVSR IADLXDisplayVSR;
typedef struct IADLXDisplayGPUScaling IADLXDisplayGPUScaling;
typedef struct IADLXDisplayScalingMode IADLXDisplayScalingMode;
typedef struct IADLXDisplayIntegerScaling IADLXDisplayIntegerScaling;
typedef struct IADLXDisplayColorDepth IADLXDisplayColorDepth;
typedef struct IADLXDisplayPixelFormat IADLXDisplayPixelFormat;
typedef struct IADLXDisplayCustomColor IADLXDisplayCustomColor;
typedef struct IADLXDisplayHDCP IADLXDisplayHDCP;
typedef struct IADLXDisplayCustomResolution IADLXDisplayCustomResolution;
typedef struct IADLXDisplayChangedHandling IADLXDisplayChangedHandling;
typedef struct IADLXDisplayVariBright IADLXDisplayVariBright;
typedef struct IADLXDisplayConnectivityExperience IADLXDisplayConnectivityExperience;
typedef struct IADLXDisplayBlanking IADLXDisplayBlanking;

typedef struct IADLXDisplayServices1Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXDisplayServices1* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXDisplayServices1* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXDisplayServices1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXDisplayServices
    ADLX_RESULT(ADLX_STD_CALL* GetNumberOfDisplays)(IADLXDisplayServices1* pThis, adlx_uint* numDisplays);
    ADLX_RESULT(ADLX_STD_CALL* GetDisplays)(IADLXDisplayServices1* pThis, IADLXDisplayList** ppDisplays);
    ADLX_RESULT(ADLX_STD_CALL* Get3DLUT)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplay3DLUT** ppDisp3DLUT);
    ADLX_RESULT(ADLX_STD_CALL* GetGamut)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayGamut** ppDispGamut);
    ADLX_RESULT(ADLX_STD_CALL* GetGamma)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayGamma** ppDispGamma);
    ADLX_RESULT(ADLX_STD_CALL* GetDisplayChangedHandling)(IADLXDisplayServices1* pThis, IADLXDisplayChangedHandling** ppDisplayChangedHandling);
    ADLX_RESULT(ADLX_STD_CALL* GetFreeSync)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayFreeSync** ppFreeSync);
    ADLX_RESULT(ADLX_STD_CALL* GetVirtualSuperResolution)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayVSR** ppVSR);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUScaling)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayGPUScaling** ppGPUScaling);
    ADLX_RESULT(ADLX_STD_CALL* GetScalingMode)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayScalingMode** ppScalingMode);
    ADLX_RESULT(ADLX_STD_CALL* GetIntegerScaling)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayIntegerScaling** ppIntegerScaling);
    ADLX_RESULT(ADLX_STD_CALL* GetColorDepth)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayColorDepth** ppColorDepth);
    ADLX_RESULT(ADLX_STD_CALL* GetPixelFormat)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayPixelFormat** ppPixelFormat);
    ADLX_RESULT(ADLX_STD_CALL* GetCustomColor)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayCustomColor** ppCustomColor);
    ADLX_RESULT(ADLX_STD_CALL* GetHDCP)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayHDCP** ppHDCP);
    ADLX_RESULT(ADLX_STD_CALL* GetCustomResolution)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayCustomResolution** ppCustomResolution);
    ADLX_RESULT(ADLX_STD_CALL* GetVariBright)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayVariBright** ppVariBright);

    //IADLXDisplayServices1
    ADLX_RESULT(ADLX_STD_CALL* GetDisplayBlanking)(IADLXDisplayServices1* pThis, IADLXDisplay* pDisplay, IADLXDisplayBlanking** ppDisplayBlanking);
} IADLXDisplayServices1Vtbl;

struct IADLXDisplayServices1 { const IADLXDisplayServices1Vtbl* pVtbl; };
#endif

#pragma endregion IADLXDisplayServices1

#pragma region IADLXDisplaySettingsChangedEvent1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXDisplaySettingsChangedEvent1 : public IADLXDisplaySettingsChangedEvent
    {
    public:
        ADLX_DECLARE_IID(L"IADLXDisplaySettingsChangedEvent1")

        /**
        *@page DOX_IADLXDisplaySettingsChangedEvent1_IsDisplayBlankingChanged IsDisplayBlankingChanged
        *@ENG_START_DOX @brief Checks if the display blanking of the display is changed. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsDisplayBlankingChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX
        * If the display blanking settings are changed, __true__ is returned. <br>
        * If the display blanking settings are not changed, __false__ is returned. @ENG_END_DOX
        *
        *@copydoc IADLXDisplaySettingsChangedEvent1_REQ_TABLE
        *
        */
        virtual adlx_bool ADLX_STD_CALL IsDisplayBlankingChanged() = 0;
    }; //IADLXDisplaySettingsChangedEvent1
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXDisplaySettingsChangedEvent1> IADLXDisplaySettingsChangedEvent1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXDisplaySettingsChangedEvent1, L"IADLXDisplaySettingsChangedEvent1")
typedef struct IADLXDisplaySettingsChangedEvent1 IADLXDisplaySettingsChangedEvent1;

typedef struct IADLXDisplaySettingsChangedEvent1Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXDisplaySettingsChangedEvent1* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXDisplaySettingsChangedEvent1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXChangedEvent
    ADLX_SYNC_ORIGIN(ADLX_STD_CALL* GetOrigin)(IADLXDisplaySettingsChangedEvent1* pThis);

    // IADLXDisplaySettingsChangedEvent interface
    ADLX_RESULT(ADLX_STD_CALL* GetDisplay)(IADLXDisplaySettingsChangedEvent1* pThis, IADLXDisplay** ppDisplay);
    adlx_bool(ADLX_STD_CALL* IsFreeSyncChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsVSRChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsGPUScalingChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsScalingModeChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsIntegerScalingChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsColorDepthChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsPixelFormatChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsHDCPChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorHueChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorSaturationChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorBrightnessChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorTemperatureChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomColorContrastChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsCustomResolutionChanged)(IADLXDisplaySettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsVariBrightChanged)(IADLXDisplaySettingsChangedEvent1* pThis);

    // IADLXDisplaySettingsChangedEvent1 interface
    adlx_bool(ADLX_STD_CALL* IsDisplayBlankingChanged)(IADLXDisplaySettingsChangedEvent1* pThis);

} IADLXDisplaySettingsChangedEvent1Vtbl;

struct IADLXDisplaySettingsChangedEvent1 { const IADLXDisplaySettingsChangedEvent1Vtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXDisplaySettingsChangedEvent1

#endif //ADLX_IDISPLAYS1_H
