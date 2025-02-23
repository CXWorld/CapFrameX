//
// Copyright (c) 2023 - 2024 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_I3DSETTINGS1_H
#define ADLX_I3DSETTINGS1_H
#pragma once

#include "ADLXStructures.h"
#include "I3DSettings.h"


//-------------------------------------------------------------------------------------------------
//I3DSetting.h - Interfaces for ADLX GPU 3DSetting functionality

//3DAMDFluidMotionFrames interface
#pragma region IADLX3DAMDFluidMotionFrames
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLX3DAMDFluidMotionFrames : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLX3DAMDFluidMotionFrames")

        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames_IsSupported IsSupported
        *@ENG_START_DOX @brief Checks if AMD Fluid Motion Frames is supported. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupported (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of AMD Fluid Motion Frames is returned. The variable is __true__ if AMD Fluid Motion Frames is supported. The variable is __false__ if AMD Fluid Motion Frames is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the state of AMD Fluid Motion Frames is successfully returned, __ADLX_OK__ is returned.<br>
        * If the state of AMD Fluid Motion Frames is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * AMD Fluid Motion Frames is a frame generating feature to achieve higher in-game frame rates. <br>
        *
        * @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames_REQ_TABLE
        *
        */
        virtual ADLX_RESULT     ADLX_STD_CALL IsSupported(adlx_bool* supported) = 0;

        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames_IsEnabled IsEnabled
        *@ENG_START_DOX @brief Checks if AMD Fluid Motion Frames is enabled. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsEnabled (adlx_bool* enabled)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],enabled,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of AMD Fluid Motion Frames is returned. The variable is __true__ if AMD Fluid Motion Frames is enabled. The variable is __false__ if AMD Fluid Motion Frames is not enabled. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the state of AMD Fluid Motion Frames is successfully returned, __ADLX_OK__ is returned.<br>
        * If the state of AMD Fluid Motion Frames is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * AMD Fluid Motion Frames is a frame generating feature to achieve higher in-game frame rates. <br>
        *
        * @depifc
        * AMD Fluid Motion Frames cannot be simultaneously enabled with @ref DOX_IADLX3DChill "AMD Radeon Chill".<br>
        *
        * When a mutually exclusive feature is enabled, AMD Fluid Motion Frames is automatically disabled.<br>
        * @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames_REQ_TABLE
        *
        */
        virtual ADLX_RESULT     ADLX_STD_CALL IsEnabled(adlx_bool* enabled) = 0;

        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames_SetEnabled SetEnabled
        *@ENG_START_DOX @brief Sets the activation status of AMD Fluid Motion Frames. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    SetEnabled (adlx_bool enable)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in],enable,adlx_bool,@ENG_START_DOX The new AMD Fluid Motion Frames state. Set __true__ to enable AMD Fluid Motion Frames. Set __false__ to disable AMD Fluid Motion Frames. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the state of AMD Fluid Motion Frames is successfully set, __ADLX_OK__ is returned.<br>
        * If the state of AMD Fluid Motion Frames is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * AMD Fluid Motion Frames is a frame generating feature to achieve higher in-game frame rates. <br>
        *
        * @depifc
        * AMD Fluid Motion Frames cannot be simultaneously enabled with @ref DOX_IADLX3DChill "AMD Radeon Chill".<br>
        *
        * If AMD Fluid Motion Frames is enabled, the mutually exclusive features are automatically disabled.
        * When a mutually exclusive feature is re-enabled, its previous configuration settings are restored.<br>
        * @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames_REQ_TABLE
        *
        */
        virtual ADLX_RESULT     ADLX_STD_CALL SetEnabled(adlx_bool enable) = 0;
    };  //IADLX3DAMDFluidMotionFrames
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLX3DAMDFluidMotionFrames> IADLX3DAMDFluidMotionFramesPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLX3DAMDFluidMotionFrames, L"IADLX3DAMDFluidMotionFrames")

typedef struct IADLX3DAMDFluidMotionFrames IADLX3DAMDFluidMotionFrames;

typedef struct IADLX3DAMDFluidMotionFramesVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLX3DAMDFluidMotionFrames* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLX3DAMDFluidMotionFrames* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLX3DAMDFluidMotionFrames* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLX3DAMDFluidMotionFrames
    ADLX_RESULT(ADLX_STD_CALL* IsSupported) (IADLX3DAMDFluidMotionFrames* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsEnabled) (IADLX3DAMDFluidMotionFrames* pThis, adlx_bool* enabled);
    ADLX_RESULT(ADLX_STD_CALL* SetEnabled) (IADLX3DAMDFluidMotionFrames* pThis, adlx_bool enable);
}IADLX3DAMDFluidMotionFramesVtbl;

struct IADLX3DAMDFluidMotionFrames { const IADLX3DAMDFluidMotionFramesVtbl* pVtbl; };

#endif //__cplusplus
#pragma endregion IADLX3DAMDFluidMotionFrames

//3DSetting Services interface
#pragma region IADLX3DSettingsServices1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLX3DAMDFluidMotionFrames;
    class ADLX_NO_VTABLE IADLX3DSettingsServices1 : public IADLX3DSettingsServices
    {
    public:
        ADLX_DECLARE_IID(L"IADLX3DSettingsServices1")

        /**
        *@page DOX_IADLX3DSettingsServices1_GetAMDFluidMotionFrames GetAMDFluidMotionFrames
        *@ENG_START_DOX @brief Gets the reference-counted AMD Fluid Motion Frames interface. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetAMDFluidMotionFrames (@ref DOX_IADLX3DAMDFluidMotionFrames** pp3DAMDFluidMotionFrames)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,pp3DAMDFluidMotionFrames,@ref DOX_IADLX3DAMDFluidMotionFrames** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*pp3DAMDFluidMotionFrames__ to __nullptr__. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the interface is successfully returned, __ADLX_OK__ is returned.<br>
        * If the interface is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed. @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. @ENG_END_DOX
        *
        *@copydoc IADLX3DSettingsServices1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL GetAMDFluidMotionFrames(IADLX3DAMDFluidMotionFrames** pp3DAMDFluidMotionFrames) = 0;

    };  //IADLX3DSettingsServices1
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLX3DSettingsServices1> IADLX3DSettingsServices1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLX3DSettingsServices1, L"IADLX3DSettingsServices1")
typedef struct IADLX3DSettingsServices1 IADLX3DSettingsServices1;

typedef struct IADLX3DSettingsServices1Vtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLX3DSettingsServices1* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLX3DSettingsServices1* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLX3DSettingsServices1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLX3DSettingsServices
    ADLX_RESULT (ADLX_STD_CALL *GetAntiLag)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DAntiLag** pp3DAntiLag);
    ADLX_RESULT (ADLX_STD_CALL *GetChill)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DChill** pp3DChill);
    ADLX_RESULT (ADLX_STD_CALL *GetBoost)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DBoost** pp3DBoost);
    ADLX_RESULT (ADLX_STD_CALL *GetImageSharpening)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DImageSharpening** pp3DImageSharpening);
    ADLX_RESULT (ADLX_STD_CALL *GetEnhancedSync)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DEnhancedSync** pp3DEnhancedSync);
    ADLX_RESULT (ADLX_STD_CALL *GetWaitForVerticalRefresh)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DWaitForVerticalRefresh** pp3DWaitForVerticalRefresh);
    ADLX_RESULT (ADLX_STD_CALL *GetFrameRateTargetControl)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DFrameRateTargetControl** pp3DFrameRateTargetControl);
    ADLX_RESULT (ADLX_STD_CALL *GetAntiAliasing)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DAntiAliasing** pp3DAntiAliasing);
    ADLX_RESULT (ADLX_STD_CALL *GetMorphologicalAntiAliasing)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DMorphologicalAntiAliasing** pp3DMorphologicalAntiAliasing);
    ADLX_RESULT (ADLX_STD_CALL *GetAnisotropicFiltering)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DAnisotropicFiltering** pp3DAnisotropicFiltering);
    ADLX_RESULT (ADLX_STD_CALL *GetTessellation)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DTessellation** pp3DTessellation);
    ADLX_RESULT (ADLX_STD_CALL *GetRadeonSuperResolution) (IADLX3DSettingsServices1* pThis, IADLX3DRadeonSuperResolution** pp3DRadeonSuperResolution);
    ADLX_RESULT (ADLX_STD_CALL *GetResetShaderCache)(IADLX3DSettingsServices1* pThis, IADLXGPU* pGPU, IADLX3DResetShaderCache** pp3DResetShaderCache);
    ADLX_RESULT (ADLX_STD_CALL *Get3DSettingsChangedHandling)(IADLX3DSettingsServices1* pThis, IADLX3DSettingsChangedHandling** pp3DSettingsChangedHandling);
    ADLX_RESULT(ADLX_STD_CALL* GetAMDFluidMotionFrames) (IADLX3DSettingsServices1* pThis, IADLX3DAMDFluidMotionFrames** pp3DGetAMDFluidMotionFrames);
}IADLX3DSettingsServices1Vtbl;

struct IADLX3DSettingsServices1 { const IADLX3DSettingsServices1Vtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLX3DSettingsServices1


//Interface with information on 3D setting changes on a display. ADLX passes this to application that registered for 3D setting changed event in the IADLX3DSettingsChangedListener::On3DSettingsChanged()
#pragma region IADLX3DSettingsChangedEvent1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPU;
    class ADLX_NO_VTABLE IADLX3DSettingsChangedEvent1 : public IADLX3DSettingsChangedEvent
    {
    public:
        ADLX_DECLARE_IID(L"IADLX3DSettingsChangedEvent1")

        /**
        *@page DOX_IADLX3DSettingsChangedEvent1_IsAMDFluidMotionFramesChanged IsAMDFluidMotionFramesChanged
        *@ENG_START_DOX @brief Checks for changes to the AMD Fluid Motion Frames settings. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsAMDFluidMotionFramesChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX  If there are any changes to the AMD Fluid Motion Frames settings, __true__ is returned.<br>
        * If there are no changes to the AMD Fluid Motion Frames settings, __false__ is returned.<br> @ENG_END_DOX
        *
        *
        *@addinfo
        *@ENG_START_DOX
        * __Note:__ AMD Fluid Motion Frames settings are global for all the supported GPUs. For this event notification, @ref DOX_IADLX3DSettingsChangedEvent_GetGPU returns __nullpr__.
        * @ENG_END_DOX
        *
        *@copydoc IADLX3DSettingsChangedEvent1_REQ_TABLE
        *
        */
        virtual adlx_bool ADLX_STD_CALL IsAMDFluidMotionFramesChanged() = 0;

    }; //IADLX3DSettingsChangedEvent
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLX3DSettingsChangedEvent1> IADLX3DSettingsChangedEvent1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLX3DSettingsChangedEvent1, L"IADLX3DSettingsChangedEvent1")
typedef struct IADLX3DSettingsChangedEvent1 IADLX3DSettingsChangedEvent1;

typedef struct IADLX3DSettingsChangedEvent1Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLX3DSettingsChangedEvent1* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLX3DSettingsChangedEvent1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXChangedEvent
    ADLX_SYNC_ORIGIN(ADLX_STD_CALL* GetOrigin)(IADLX3DSettingsChangedEvent1* pThis);

    // IADLX3DSettingsChangedEvent interface
    ADLX_RESULT(ADLX_STD_CALL* GetGPU)(IADLX3DSettingsChangedEvent1* pThis, IADLXGPU** ppGPU);
    adlx_bool(ADLX_STD_CALL* IsAntiLagChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsChillChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsBoostChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsImageSharpeningChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsEnhancedSyncChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsWaitForVerticalRefreshChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsFrameRateTargetControlChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsAntiAliasingChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsMorphologicalAntiAliasingChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsAnisotropicFilteringChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsTessellationModeChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsRadeonSuperResolutionChanged)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsResetShaderCache)(IADLX3DSettingsChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsAMDFluidMotionFramesChanged)(IADLX3DSettingsChangedEvent1* pThis);


} IADLX3DSettingsChangedEvent1Vtbl;

struct IADLX3DSettingsChangedEvent1 { const IADLX3DSettingsChangedEvent1Vtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLX3DSettingsChangedEvent1

#endif //ADLX_I3DSETTINGS1_H
