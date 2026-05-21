//
// Copyright Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_I3DSETTINGS3_H
#define ADLX_I3DSETTINGS3_H
#pragma once

#include "ADLXStructures.h"
#include "I3DSettings2.h"
#include "ICollections.h"


//-------------------------------------------------------------------------------------------------
//I3DSetting3.h - Interfaces for ADLX GPU 3DSetting functionality

//AFMF2.1 interface
//3DAMDFluidMotionFrames1 interface
#pragma region IADLX3DAMDFluidMotionFrames1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLX3DAMDFluidMotionFrames1 : public IADLX3DAMDFluidMotionFrames
    {
    public:
        ADLX_DECLARE_IID(L"IADLX3DAMDFluidMotionFrames1")
        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames1_IsSupportedAlgorithm  IsSupportedAlgorithm 
        *@ENG_START_DOX @brief Checks if the AMD Fluid Motion Frames algorithm is supported. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupportedAlgorithm  (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of AMD Fluid Motion Frames algorithm is returned. The variable is __true__ if configure AMD Fluid Motion Frames algorithm is supported. The variable is __false__ if configure AMD Fluid Motion Frames algorithm is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the state of AMD Fluid Motion Frames algorithm support is successfully returned, __ADLX_OK__ is returned.<br>
        * If the state of AMD Fluid Motion Frames algorithm is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        * @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsSupportedAlgorithm (adlx_bool* supported) = 0;
        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames1_GetAlgorithm GetAlgorithm
        *@ENG_START_DOX @brief Gets the algorithm used by AMD Fluid Motion Frames. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetAlgorithm(@ref ADLX_AFMF_ALGORITHM* algorithm)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],algorithm,@ref ADLX_AFMF_ALGORITHM*,@ENG_START_DOX The pointer to a variable where the algorithm used by AMD Fluid Motion Frames is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the algorithm used by AMD Fluid Motion Frames is successfully returned, __ADLX_OK__ is returned.<br>
        * If the algorithm used by AMD Fluid Motion Frames is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetAlgorithm(ADLX_AFMF_ALGORITHM* algorithm) = 0;
        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames1_SetAlgorithm SetAlgorithm
        *@ENG_START_DOX @brief Sets the algorithm used by AMD Fluid Motion Frames. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    SetAlgorithm(@ref ADLX_AFMF_ALGORITHM algorithm)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in],mode,@ref ADLX_AFMF_ALGORITHM,@ENG_START_DOX The variable where the algorithm used by AMD Fluid Motion Frames is set. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the algorithm used by AMD Fluid Motion Frames is successfully set, __ADLX_OK__ is returned.<br>
        * If the algorithm used by AMD Fluid Motion Frames is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL SetAlgorithm(ADLX_AFMF_ALGORITHM algorithm) = 0;

        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames1_GetSearchMode GetSearchMode
        *@ENG_START_DOX @brief Gets the search mode used by AMD Fluid Motion Frames. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetSearchMode(@ref ADLX_AFMF_SEARCH_MODE_TYPE* mode)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],mode,@ref ADLX_AFMF_SEARCH_MODE_TYPE*,@ENG_START_DOX The pointer to a variable where the search mode used by AMD Fluid Motion Frames is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the AMD Fluid Motion Frames search mode is successfully returned, __ADLX_OK__ is returned.<br>
        * If the AMD Fluid Motion Frames search mode is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetSearchMode(ADLX_AFMF_SEARCH_MODE_TYPE* mode) = 0;
        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames1_SetSearchMode SetSearchMode
        *@ENG_START_DOX @brief Sets the search mode used by AMD Fluid Motion Frames. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    SetSearchMode(@ref ADLX_AFMF_SEARCH_MODE_TYPE mode)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in],mode,@ref ADLX_AFMF_SEARCH_MODE_TYPE,@ENG_START_DOX The desired search mode to be used by AMD Fluid Motion Frames. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the AMD Fluid Motion Frames search mode is successfully set, __ADLX_OK__ is returned.<br>
        * If the AMD Fluid Motion Frames search mode is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL SetSearchMode(ADLX_AFMF_SEARCH_MODE_TYPE mode) = 0;

        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames1_GetPerformanceMode GetPerformanceMode
        *@ENG_START_DOX @brief Gets the AMD Fluid Motion Frames performance mode. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetPerformanceMode(@ref ADLX_AFMF_PERFORMANCE_MODE_TYPE* mode)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],mode,@ref ADLX_AFMF_PERFORMANCE_MODE_TYPE*,@ENG_START_DOX The pointer to a variable where the AMD Fluid Motion Frames performance mode is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the AMD Fluid Motion Frames performance mode is successfully returned, __ADLX_OK__ is returned.<br>
        * If the AMD Fluid Motion Frames performance mode is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetPerformanceMode(ADLX_AFMF_PERFORMANCE_MODE_TYPE* mode) = 0;
        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames1_SetPerformanceMode SetPerformanceMode
        *@ENG_START_DOX @brief Sets the AMD Fluid Motion Frames performance mode. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    SetPerformanceMode(@ref ADLX_AFMF_PERFORMANCE_MODE_TYPE mode)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in],mode,@ref ADLX_AFMF_PERFORMANCE_MODE_TYPE,@ENG_START_DOX The performance mode of AMD Fluid Motion Frames to be set. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the AMD Fluid Motion Frames performance mode is successfully set, __ADLX_OK__ is returned.<br>
        * If the AMD Fluid Motion Frames performance mode is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL SetPerformanceMode(ADLX_AFMF_PERFORMANCE_MODE_TYPE mode) = 0;

        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames1_GetFastMotionResponse GetFastMotionResponse
        *@ENG_START_DOX @brief Gets the approach that AMD Fluid Motion Frames uses for fast-motion content. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetFastMotionResponse(@ref ADLX_AFMF_FAST_MOTION_RESP* response)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],response,@ref ADLX_AFMF_FAST_MOTION_RESP*,@ENG_START_DOX The pointer to a variable where the AMD Fluid Motion Frames approach to fast-motion content is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the approach that AMD Fluid Motion Frames uses for fast-motion content is successfully returned, __ADLX_OK__ is returned.<br>
        * If the approach that AMD Fluid Motion Frames uses for fast-motion content is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetFastMotionResponse(ADLX_AFMF_FAST_MOTION_RESP* response) = 0;
        /**
        *@page DOX_IADLX3DAMDFluidMotionFrames1_SetFastMotionResponse SetFastMotionResponse
        *@ENG_START_DOX @brief Sets the approach that AMD Fluid Motion Frames uses for fast-motion content. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    SetFastMotionResponse(@ref ADLX_AFMF_FAST_MOTION_RESP response)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in],response,@ref ADLX_AFMF_FAST_MOTION_RESP,@ENG_START_DOX The desired approach of AMD Fluid Motion Frames to fast-motion content. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the desired AMD Fluid Motion Frames approach to fast-motion is successfully set, __ADLX_OK__ is returned.<br>
        * If the desired AMD Fluid Motion Frames approach to fast-motion is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLX3DAMDFluidMotionFrames1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL SetFastMotionResponse(ADLX_AFMF_FAST_MOTION_RESP response) = 0;

    };  //IADLX3DAMDFluidMotionFrames1
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLX3DAMDFluidMotionFrames1> IADLX3DAMDFluidMotionFrames1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLX3DAMDFluidMotionFrames1, L"IADLX3DAMDFluidMotionFrames1")

typedef struct IADLX3DAMDFluidMotionFrames1 IADLX3DAMDFluidMotionFrames1;

typedef struct IADLX3DAMDFluidMotionFrames1Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLX3DAMDFluidMotionFrames1* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLX3DAMDFluidMotionFrames1* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLX3DAMDFluidMotionFrames1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLX3DAMDFluidMotionFrames
    ADLX_RESULT(ADLX_STD_CALL* IsSupported) (IADLX3DAMDFluidMotionFrames1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsEnabled) (IADLX3DAMDFluidMotionFrames1* pThis, adlx_bool* enabled);
    ADLX_RESULT(ADLX_STD_CALL* SetEnabled) (IADLX3DAMDFluidMotionFrames1* pThis, adlx_bool enable);

    //IADLX3DAMDFluidMotionFrames1
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedAlgorithm ) (IADLX3DAMDFluidMotionFrames1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetAlgorithm) (IADLX3DAMDFluidMotionFrames1* pThis, ADLX_AFMF_ALGORITHM* algorithm);
    ADLX_RESULT(ADLX_STD_CALL* SetAlgorithm) (IADLX3DAMDFluidMotionFrames1* pThis, ADLX_AFMF_ALGORITHM algorithm);
    ADLX_RESULT(ADLX_STD_CALL* GetSearchMode) (IADLX3DAMDFluidMotionFrames1* pThis, ADLX_AFMF_SEARCH_MODE_TYPE* mode);
    ADLX_RESULT(ADLX_STD_CALL* SetSearchMode) (IADLX3DAMDFluidMotionFrames1* pThis, ADLX_AFMF_SEARCH_MODE_TYPE mode);
    ADLX_RESULT(ADLX_STD_CALL* GetPerformanceMode) (IADLX3DAMDFluidMotionFrames1* pThis, ADLX_AFMF_PERFORMANCE_MODE_TYPE* mode);
    ADLX_RESULT(ADLX_STD_CALL* SetPerformanceMode) (IADLX3DAMDFluidMotionFrames1* pThis, ADLX_AFMF_PERFORMANCE_MODE_TYPE mode);
    ADLX_RESULT(ADLX_STD_CALL* GetFastMotionResponse) (IADLX3DAMDFluidMotionFrames1* pThis, ADLX_AFMF_FAST_MOTION_RESP* response);
    ADLX_RESULT(ADLX_STD_CALL* SetFastMotionResponse) (IADLX3DAMDFluidMotionFrames1* pThis, ADLX_AFMF_FAST_MOTION_RESP response);
}IADLX3DAMDFluidMotionFrames1Vtbl;

struct IADLX3DAMDFluidMotionFrames1 { const IADLX3DAMDFluidMotionFrames1Vtbl* pVtbl; };

#endif //__cplusplus
#pragma endregion IADLX3DAMDFluidMotionFrames1

#pragma region IADLX3DFidelityFXSuperResolution
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLX3DFidelityFXSuperResolution : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLX3DFidelityFXSuperResolution")

        /**
        *@page DOX_IADLX3DFidelityFXSuperResolution_IsSupported IsSupported
        *@ENG_START_DOX @brief Checks if AMD FidelityFX Super Resolution is supported on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupported (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of AMD FidelityFX Super Resolution is returned. The variable is __true__ if AMD FidelityFX Super Resolution is supported. The variable is __false__ if AMD FidelityFX Super Resolution is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the state of AMD FidelityFX Super Resolution is successfully returned, __ADLX_OK__ is returned.<br>
        * If the state of AMD FidelityFX Super Resolution is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        * __Note:__ Only works in supported games.
        * @ENG_END_DOX
        *
        *@copydoc IADLX3DFidelityFXSuperResolution_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL IsSupported(adlx_bool* supported) = 0;

        /**
        *@page DOX_IADLX3DFidelityFXSuperResolution_IsEnabled IsEnabled
        *@ENG_START_DOX @brief Checks if AMD FidelityFX Super Resolution is enabled on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsEnabled (adlx_bool* enabled)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],enabled,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of AMD FidelityFX Super Resolution is returned. The variable is __true__ if AMD FidelityFX Super Resolution is enabled. The variable is __false__ if AMD FidelityFX Super Resolution is not enabled. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the state of AMD FidelityFX Super Resolution is successfully returned, __ADLX_OK__ is returned.<br>
        * If the state of AMD FidelityFX Super Resolution is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        * @ENG_END_DOX
        *
        *@copydoc IADLX3DFidelityFXSuperResolution_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL IsEnabled(adlx_bool* isEnabled) = 0;

        /**
        *@page DOX_IADLX3DFidelityFXSuperResolution_SetEnabled SetEnabled
        *@ENG_START_DOX @brief Sets AMD FidelityFX Super Resolution to enabled or disabled on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    SetEnabled (adlx_bool enable)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in],enable,adlx_bool,@ENG_START_DOX The new AMD FidelityFX Super Resolution state. Set __true__ to enable AMD FidelityFX Super Resolution. Set __false__ to disable AMD FidelityFX Super Resolution. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the state of AMD FidelityFX Super Resolution is successfully set, __ADLX_OK__ is returned.<br>
        * If the state of AMD FidelityFX Super Resolution is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        * __Note:__ Only works in supported games.<br>
        * @ENG_END_DOX
        *
        *@copydoc IADLX3DFidelityFXSuperResolution_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL SetEnabled(adlx_bool enable) = 0;

    };  //IADLX3DFidelityFXSuperResolution
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLX3DFidelityFXSuperResolution> IADLX3DFidelityFXSuperResolutionPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLX3DFidelityFXSuperResolution, L"IADLX3DFidelityFXSuperResolution")
typedef struct IADLX3DFidelityFXSuperResolution IADLX3DFidelityFXSuperResolution;
typedef struct IADLX3DFidelityFXSuperResolutionVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLX3DFidelityFXSuperResolution* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLX3DFidelityFXSuperResolution* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLX3DFidelityFXSuperResolution* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLX3DFidelityFXSuperResolution
    ADLX_RESULT(ADLX_STD_CALL* IsSupported)(IADLX3DFidelityFXSuperResolution* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsEnabled)(IADLX3DFidelityFXSuperResolution* pThis, adlx_bool* isEnabled);
    ADLX_RESULT(ADLX_STD_CALL* SetEnabled)(IADLX3DFidelityFXSuperResolution* pThis, adlx_bool enable);
} IADLX3DFidelityFXSuperResolutionVtbl;

struct IADLX3DFidelityFXSuperResolution { const IADLX3DFidelityFXSuperResolutionVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLX3DFidelityFXSuperResolution

#pragma region IADLX3DFidelityFXFrameGenUpgradeRatioOption
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLX3DFidelityFXFrameGenUpgradeRatioOptionList;
    class ADLX_NO_VTABLE IADLX3DFidelityFXFrameGenUpgradeRatioOption : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLX3DFidelityFXFrameGenUpgradeRatioOption")
            /**
            * @page DOX_IADLX3DFidelityFXFrameGenUpgradeRatioOption_Ratio Ratio
            * @ENG_START_DOX @brief Gets the frame generation upgrade ratio for this option. @ENG_END_DOX
            *
            * @syntax
            * @codeStart
            * @ref ADLX_RESULT    Ratio (ADLX_FFX_FRAME_GEN_RATIO* ratio)
            * @codeEnd
            *
            * @params
            * @paramrow{1.,[out],ratio,ADLX_FFX_FRAME_GEN_RATIO*,@ENG_START_DOX The pointer to a variable where the frame generation upgrade ratio is returned. @ENG_END_DOX}
            *
            * @retvalues
            * @ENG_START_DOX
            * If the frame generation upgrade ratio is successfully returned, __ADLX_OK__ is returned.<br>
            * If the frame generation upgrade ratio is not successfully returned, an error code is returned.<br>
            * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
            * @ENG_END_DOX
            *
            * @copydoc IADLX3DFidelityFXFrameGenUpgradeRatioOption_REQ_TABLE
            *
            */
            virtual ADLX_RESULT         ADLX_STD_CALL Ratio(ADLX_FFX_FRAME_GEN_RATIO* ratio) = 0;
    };  //IADLX3DFidelityFXFrameGenUpgradeRatioOption
    typedef IADLXInterfacePtr_T<IADLX3DFidelityFXFrameGenUpgradeRatioOption> IADLX3DFidelityFXFrameGenUpgradeRatioOptionPtr;
}//namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLX3DFidelityFXFrameGenUpgradeRatioOption, L"IADLX3DFidelityFXFrameGenUpgradeRatioOption")
typedef struct IADLX3DFidelityFXFrameGenUpgradeRatioOption IADLX3DFidelityFXFrameGenUpgradeRatioOption;
typedef struct IADLX3DFidelityFXFrameGenUpgradeRatioOptionVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLX3DFidelityFXFrameGenUpgradeRatioOption* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLX3DFidelityFXFrameGenUpgradeRatioOption* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLX3DFidelityFXFrameGenUpgradeRatioOption* pThis, const wchar_t* interfaceId, void** ppInterface);
    //IADLX3DFidelityFXFrameGenUpgradeRatioOption
    ADLX_RESULT(ADLX_STD_CALL* Ratio)(IADLX3DFidelityFXFrameGenUpgradeRatioOption* pThis, ADLX_FFX_FRAME_GEN_RATIO* ratio);
} IADLX3DFidelityFXFrameGenUpgradeRatioOptionVtbl;

struct IADLX3DFidelityFXFrameGenUpgradeRatioOption { const IADLX3DFidelityFXFrameGenUpgradeRatioOptionVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLX3DFidelityFXFrameGenUpgradeRatioOption

#pragma region IADLX3DFidelityFXFrameGenUpgradeRatioOptionList
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLX3DFidelityFXFrameGenUpgradeRatioOptionList : public IADLXList
    {
    public:
        ADLX_DECLARE_IID(L"IADLX3DFidelityFXFrameGenUpgradeRatioOptionList")
        //Lists must declare the type of items it holds - what was passed as ADLX_DECLARE_IID() in that interface
        ADLX_DECLARE_ITEM_IID(IADLX3DFidelityFXFrameGenUpgradeRatioOption::IID())
        ADLX_DECLARE_LIST_METHODS
        /**
        * @page DOX_IADLX3DFidelityFXFrameGenUpgradeRatioOptionList_At At
        * @ENG_START_DOX
        * @brief Returns the reference counted interface at the requested location in the list.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    At (const adlx_uint location, @ref DOX_IADLX3DFidelityFXFrameGenUpgradeRatioOption** ppItem)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[in],location,const adlx_uint,@ENG_START_DOX The location of the requested interface. @ENG_END_DOX}
        * @paramrow{2.,[out],ppItem,@ref DOX_IADLX3DFidelityFXFrameGenUpgradeRatioOption**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppItem__ to __nullptr__. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the location is within the list bounds, __ADLX_OK__ is returned.<br>
        * If the location is not within the list bounds, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @detaileddesc
        * @ENG_START_DOX
        * @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed.
        * @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation.
        * @ENG_END_DOX
        *
        * @copydoc IADLX3DFidelityFXFrameGenUpgradeRatioOptionList_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL At(const adlx_uint location, IADLX3DFidelityFXFrameGenUpgradeRatioOption** ppItem) = 0;

        /**
        * @page DOX_IADLX3DFidelityFXFrameGenUpgradeRatioOptionList_Add_Back Add_Back
        * @ENG_START_DOX
        * @brief Adds an interface to the end of the list.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    Add_Back (@ref DOX_IADLX3DFidelityFXFrameGenUpgradeRatioOption* pItem)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[in],pItem,@ref DOX_IADLX3DFidelityFXFrameGenUpgradeRatioOption*,@ENG_START_DOX The pointer to the interface to be added to the list. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the interface is added successfully to the end of the list, __ADLX_OK__ is returned.<br>
        * If the interface is not added to the end of the list, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @detaileddesc
        * @ENG_START_DOX
        * @details @ENG_END_DOX
        *
        * @copydoc IADLX3DFidelityFXFrameGenUpgradeRatioOptionList_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL Add_Back(IADLX3DFidelityFXFrameGenUpgradeRatioOption* pItem) = 0;
    };  //IADLX3DFidelityFXFrameGenUpgradeRatioOptionList
    typedef IADLXInterfacePtr_T<IADLX3DFidelityFXFrameGenUpgradeRatioOptionList> IADLX3DFidelityFXFrameGenUpgradeRatioOptionListPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList, L"IADLX3DFidelityFXFrameGenUpgradeRatioOptionList")
typedef struct IADLX3DFidelityFXFrameGenUpgradeRatioOptionList IADLX3DFidelityFXFrameGenUpgradeRatioOptionList;
typedef struct IADLX3DFidelityFXFrameGenUpgradeRatioOptionListVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXList
    adlx_uint(ADLX_STD_CALL* Size)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis);
    adlx_bool(ADLX_STD_CALL* Empty)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis);
    adlx_uint(ADLX_STD_CALL* Begin)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis);
    adlx_uint(ADLX_STD_CALL* End)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* At)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis, const adlx_uint location, IADLXInterface** ppItem);
    ADLX_RESULT(ADLX_STD_CALL* Clear)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* Remove_Back)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* Add_Back)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis, IADLXInterface* pItem);

    //IADLX3DFidelityFXFrameGenUpgradeRatioOptionList
    ADLX_RESULT(ADLX_STD_CALL* At_OptionList)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis, const adlx_uint location, IADLX3DFidelityFXFrameGenUpgradeRatioOption** ppItem);
    ADLX_RESULT(ADLX_STD_CALL* Add_Back_OptionList)(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList* pThis, IADLX3DFidelityFXFrameGenUpgradeRatioOption* pItem);
} IADLX3DFidelityFXFrameGenUpgradeRatioOptionListVtbl;

struct IADLX3DFidelityFXFrameGenUpgradeRatioOptionList { const IADLX3DFidelityFXFrameGenUpgradeRatioOptionListVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLX3DFidelityFXFrameGenUpgradeRatioOptionList

#pragma region IADLX3DFidelityFXFrameGenUpgrade
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLX3DFidelityFXFrameGenUpgrade : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLX3DFidelityFXFrameGenUpgrade")
        /**
        * @page DOX_IADLX3DFidelityFXFrameGenUpgrade_GetAvailableRatios GetAvailableRatios
        * @ENG_START_DOX
        * @brief Gets the reference counted list of all supported frame generation upgrade ratios.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    GetAvailableRatios (@ref DOX_IADLX3DFidelityFXFrameGenUpgradeRatioOptionList** ratios)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],ratios,@ref DOX_IADLX3DFidelityFXFrameGenUpgradeRatioOptionList**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ratios__ to __nullptr__. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the interface is successfully returned, __ADLX_OK__ is returned.<br>
        * If the interface is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @detaileddesc
        * @ENG_START_DOX
        * @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed.
        * @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation.
        * @ENG_END_DOX
        *
        * @copydoc IADLX3DFidelityFXFrameGenUpgrade_REQ_TABLE
        */
        virtual ADLX_RESULT         ADLX_STD_CALL GetAvailableRatios(IADLX3DFidelityFXFrameGenUpgradeRatioOptionList** ratios) = 0;

        /**
        * @page DOX_IADLX3DFidelityFXFrameGenUpgrade_GetRatio GetRatio
        * @ENG_START_DOX
        * @brief Gets the current frame generation upgrade ratio.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    GetRatio (ADLX_FFX_FRAME_GEN_RATIO* ratio)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],ratio,ADLX_FFX_FRAME_GEN_RATIO*,@ENG_START_DOX The pointer to a variable where the current frame generation upgrade ratio is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the current frame generation upgrade ratio is successfully returned, __ADLX_OK__ is returned.<br>
        * If the current frame generation upgrade ratio is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLX3DFidelityFXFrameGenUpgrade_REQ_TABLE
        */
        virtual ADLX_RESULT         ADLX_STD_CALL GetRatio(ADLX_FFX_FRAME_GEN_RATIO* ratio) = 0;

        /**
        * @page DOX_IADLX3DFidelityFXFrameGenUpgrade_IsSupported IsSupported
        * @ENG_START_DOX
        * @brief Checks if FidelityFX Frame Generation Upgrade is supported.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    IsSupported (adlx_bool* supported)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the support state is returned. The variable is __true__ if supported\, __false__ otherwise. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the support state is successfully returned, __ADLX_OK__ is returned.<br>
        * If the support state is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * Only works in supported games.
        * @ENG_END_DOX
        *
        * @copydoc IADLX3DFidelityFXFrameGenUpgrade_REQ_TABLE
        */
        virtual ADLX_RESULT         ADLX_STD_CALL IsSupported(adlx_bool* supported) = 0;

        /**
        * @page DOX_IADLX3DFidelityFXFrameGenUpgrade_IsEnabled IsEnabled
        * @ENG_START_DOX
        * @brief Checks if FidelityFX Frame Generation Upgrade is enabled.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    IsEnabled (adlx_bool* isEnabled)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],isEnabled,adlx_bool*,@ENG_START_DOX The pointer to a variable where the enabled state is returned. The variable is __true__ if enabled\, __false__ otherwise. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the enabled state is successfully returned, __ADLX_OK__ is returned.<br>
        * If the enabled state is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * Only works in supported games.
        * @ENG_END_DOX
        *
        * @copydoc IADLX3DFidelityFXFrameGenUpgrade_REQ_TABLE
        */
        virtual ADLX_RESULT         ADLX_STD_CALL IsEnabled(adlx_bool* isEnabled) = 0;

        /**
        * @page DOX_IADLX3DFidelityFXFrameGenUpgrade_SetEnabled SetEnabled
        * @ENG_START_DOX
        * @brief Sets FidelityFX Frame Generation Upgrade to enabled or disabled.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    SetEnabled (adlx_bool enable)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[in],enable,adlx_bool,@ENG_START_DOX The new enabled state. Set __true__ to enable\, __false__ to disable. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the enabled state is successfully set, __ADLX_OK__ is returned.<br>
        * If the enabled state is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * Only works in supported games.
        * @ENG_END_DOX
        *
        * @copydoc IADLX3DFidelityFXFrameGenUpgrade_REQ_TABLE
        */
        virtual ADLX_RESULT         ADLX_STD_CALL SetEnabled(adlx_bool enable) = 0;

        /**
        * @page DOX_IADLX3DFidelityFXFrameGenUpgrade_SetRatio SetRatio
        * @ENG_START_DOX
        * @brief Sets the frame generation upgrade ratio.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    SetRatio (ADLX_FFX_FRAME_GEN_RATIO ratio)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[in],ratio,ADLX_FFX_FRAME_GEN_RATIO,@ENG_START_DOX The frame generation upgrade ratio to be set. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the frame generation upgrade ratio is successfully set, __ADLX_OK__ is returned.<br>
        * If the frame generation upgrade ratio is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLX3DFidelityFXFrameGenUpgrade_REQ_TABLE
        */
        virtual ADLX_RESULT         ADLX_STD_CALL SetRatio(ADLX_FFX_FRAME_GEN_RATIO ratio) = 0;
    };  //IADLX3DFidelityFXFrameGenUpgrade
    typedef IADLXInterfacePtr_T<IADLX3DFidelityFXFrameGenUpgrade> IADLX3DFidelityFXFrameGenUpgradePtr;
}    //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLX3DFidelityFXFrameGenUpgrade, L"IADLX3DFidelityFXFrameGenUpgrade")
typedef struct IADLX3DFidelityFXFrameGenUpgrade IADLX3DFidelityFXFrameGenUpgrade;
typedef struct IADLX3DFidelityFXFrameGenUpgradeVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLX3DFidelityFXFrameGenUpgrade* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLX3DFidelityFXFrameGenUpgrade* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLX3DFidelityFXFrameGenUpgrade* pThis, const wchar_t* interfaceId, void** ppInterface);
    //IADLX3DFidelityFXFrameGenUpgrade
    ADLX_RESULT(ADLX_STD_CALL* GetAvailableRatios)(IADLX3DFidelityFXFrameGenUpgrade* pThis, IADLX3DFidelityFXFrameGenUpgradeRatioOptionList** ratios);
    ADLX_RESULT(ADLX_STD_CALL* GetRatio)(IADLX3DFidelityFXFrameGenUpgrade* pThis, ADLX_FFX_FRAME_GEN_RATIO* ratio);
    ADLX_RESULT(ADLX_STD_CALL* IsSupported)(IADLX3DFidelityFXFrameGenUpgrade* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsEnabled)(IADLX3DFidelityFXFrameGenUpgrade* pThis, adlx_bool* isEnabled);
    ADLX_RESULT(ADLX_STD_CALL* SetEnabled)(IADLX3DFidelityFXFrameGenUpgrade* pThis, adlx_bool enable);
    ADLX_RESULT(ADLX_STD_CALL* SetRatio)(IADLX3DFidelityFXFrameGenUpgrade* pThis, ADLX_FFX_FRAME_GEN_RATIO ratio);
} IADLX3DFidelityFXFrameGenUpgradeVtbl;

struct IADLX3DFidelityFXFrameGenUpgrade { const IADLX3DFidelityFXFrameGenUpgradeVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLX3DFidelityFXFrameGenUpgrade

#pragma region IADLX3DSettingsServices3
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLX3DSettingsServices3 : public IADLX3DSettingsServices2
    {
    public:
        ADLX_DECLARE_IID(L"IADLX3DSettingsServices3")
        /**
        *@page DOX_IADLX3DSettingsServices3_GetFidelityFXSuperResolution GetFidelityFXSuperResolution
        *@ENG_START_DOX @brief Gets the AMD FidelityFX Super Resolution interface of a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetFidelityFXSuperResolution (@ref DOX_IADLXGPU* pGPU, @ref DOX_IADLX3DFidelityFXSuperResolution** ppFidelityFXSuperResolution)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in] ,pGPU,@ref DOX_IADLXGPU* ,@ENG_START_DOX The pointer to the GPU interface. @ENG_END_DOX}
        * @paramrow{2.,[out],ppFidelityFXSuperResolution,IADLX3DFidelityFXSuperResolution**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppFidelityFXSuperResolution__ to __nullptr__. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the interface is successfully returned, __ADLX_OK__ is returned.<br>
        * If not, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed. @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. @ENG_END_DOX
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL GetFidelityFXSuperResolution(IADLXGPU* pGPU, IADLX3DFidelityFXSuperResolution** ppFidelityFXSuperResolution) = 0;

        /**
        *@page DOX_IADLX3DSettingsServices3_GetFidelityFXFrameGenUpgrade GetFidelityFXFrameGenUpgrade
        *@ENG_START_DOX @brief Gets the FidelityFX Frame Generation Upgrade interface of a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetFidelityFXFrameGenUpgrade (@ref DOX_IADLXGPU* pGPU, @ref DOX_IADLX3DFidelityFXFrameGenUpgrade** ppFidelityFXFrameGenUpgrade)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in] ,pGPU,@ref DOX_IADLXGPU* ,@ENG_START_DOX The pointer to the GPU interface. @ENG_END_DOX}
        * @paramrow{2.,[out],ppFidelityFXFrameGenUpgrade,IADLX3DFidelityFXFrameGenUpgrade**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppFidelityFXFrameGenUpgrade__ to __nullptr__. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the interface is successfully returned, __ADLX_OK__ is returned.<br>
        * If not, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed. @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. @ENG_END_DOX
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL GetFidelityFXFrameGenUpgrade(IADLXGPU* pGPU, IADLX3DFidelityFXFrameGenUpgrade** ppFidelityFXFrameGenUpgrade) = 0;
    };  //IADLX3DSettingsServices3
    typedef IADLXInterfacePtr_T<IADLX3DSettingsServices3> IADLX3DSettingsServices3Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLX3DSettingsServices3, L"IADLX3DSettingsServices3")

typedef struct IADLX3DSettingsServices3 IADLX3DSettingsServices3;
typedef struct IADLX3DSettingsServices3Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLX3DSettingsServices3* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLX3DSettingsServices3* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLX3DSettingsServices3* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLX3DSettingsServices
    ADLX_RESULT(ADLX_STD_CALL* GetAntiLag)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DAntiLag** pp3DAntiLag);
    ADLX_RESULT(ADLX_STD_CALL* GetChill)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DChill** pp3DChill);
    ADLX_RESULT(ADLX_STD_CALL* GetBoost)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DBoost** pp3DBoost);
    ADLX_RESULT(ADLX_STD_CALL* GetImageSharpening)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DImageSharpening** pp3DImageSharpening);
    ADLX_RESULT(ADLX_STD_CALL* GetEnhancedSync)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DEnhancedSync** pp3DEnhancedSync);
    ADLX_RESULT(ADLX_STD_CALL* GetWaitForVerticalRefresh)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DWaitForVerticalRefresh** pp3DWaitForVerticalRefresh);
    ADLX_RESULT(ADLX_STD_CALL* GetFrameRateTargetControl)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DFrameRateTargetControl** pp3DFrameRateTargetControl);
    ADLX_RESULT(ADLX_STD_CALL* GetAntiAliasing)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DAntiAliasing** pp3DAntiAliasing);
    ADLX_RESULT(ADLX_STD_CALL* GetMorphologicalAntiAliasing)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DMorphologicalAntiAliasing** pp3DMorphologicalAntiAliasing);
    ADLX_RESULT(ADLX_STD_CALL* GetAnisotropicFiltering)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DAnisotropicFiltering** pp3DAnisotropicFiltering);
    ADLX_RESULT(ADLX_STD_CALL* GetTessellation)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DTessellation** pp3DTessellation);
    ADLX_RESULT(ADLX_STD_CALL* GetRadeonSuperResolution) (IADLX3DSettingsServices3* pThis, IADLX3DRadeonSuperResolution** pp3DRadeonSuperResolution);
    ADLX_RESULT(ADLX_STD_CALL* GetResetShaderCache)(IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DResetShaderCache** pp3DResetShaderCache);
    ADLX_RESULT(ADLX_STD_CALL* Get3DSettingsChangedHandling)(IADLX3DSettingsServices3* pThis, IADLX3DSettingsChangedHandling** pp3DSettingsChangedHandling);
    ADLX_RESULT(ADLX_STD_CALL* GetAMDFluidMotionFrames) (IADLX3DSettingsServices3* pThis, IADLX3DAMDFluidMotionFrames** pp3DGetAMDFluidMotionFrames);
    ADLX_RESULT(ADLX_STD_CALL* GetImageSharpenDesktop) (IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DImageSharpenDesktop** pp3DImageSharpenDesktop);

    // IADLX3DSettingsServices3
    ADLX_RESULT(ADLX_STD_CALL* GetFidelityFXSuperResolution) (IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DFidelityFXSuperResolution** ppFidelityFXSuperResolution);
    ADLX_RESULT(ADLX_STD_CALL* GetFidelityFXFrameGenUpgrade) (IADLX3DSettingsServices3* pThis, IADLXGPU* pGPU, IADLX3DFidelityFXFrameGenUpgrade** ppFidelityFXFrameGenUpgrade);
} IADLX3DSettingsServices3Vtbl;
struct IADLX3DSettingsServices3 { const IADLX3DSettingsServices3Vtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLX3DSettingsServices3

#pragma region IADLX3DSettingsChangedEvent3
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLX3DSettingsChangedEvent3 : public IADLX3DSettingsChangedEvent2
    {
    public:
        ADLX_DECLARE_IID(L"IADLX3DSettingsChangedEvent3")
        /**
        *@page DOX_IADLX3DSettingsChangedEvent3_IsFidelityFXSuperResolutionChanged IsFidelityFXSuperResolutionChanged
        *@ENG_START_DOX @brief Checks for changes to the FidelityFX Super Resolution. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsFidelityFXSuperResolutionChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX  If there are any changes to the FidelityFX Super Resolution settings, __true__ is returned.<br>
        * If there are no changes to the FidelityFX Super Resolution settings, __false__ is returned.<br> @ENG_END_DOX
        *
        *
        *@copydoc IADLX3DSettingsChangedEvent3_REQ_TABLE
        *
        */
        virtual adlx_bool         ADLX_STD_CALL IsFidelityFXSuperResolutionChanged() = 0;

        /**
        *@page DOX_IADLX3DSettingsChangedEvent3_IsFidelityFXFrameGenUpgradeChanged IsFidelityFXFrameGenUpgradeChanged
        *@ENG_START_DOX @brief Checks for changes to the FidelityFX Frame Generation Upgrade. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsFidelityFXFrameGenUpgradeChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX  If there are any changes to the FidelityFX Frame Generation Upgrade settings, __true__ is returned.<br>
        * If there are no changes to the FidelityFX Frame Generation Upgrade settings, __false__ is returned.<br> @ENG_END_DOX
        *
        *
        *@copydoc IADLX3DSettingsChangedEvent3_REQ_TABLE
        *
        */
        virtual adlx_bool         ADLX_STD_CALL IsFidelityFXFrameGenUpgradeChanged() = 0;
    };  //IADLX3DSettingsChangedEvent3
    typedef IADLXInterfacePtr_T<IADLX3DSettingsChangedEvent3> IADLX3DSettingsChangedEvent3Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLX3DSettingsChangedEvent3, L"IADLX3DSettingsChangedEvent3")
typedef struct IADLX3DSettingsChangedEvent3 IADLX3DSettingsChangedEvent3;
typedef struct IADLX3DSettingsChangedEvent3Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLX3DSettingsChangedEvent3* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLX3DSettingsChangedEvent3* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXChangedEvent
    ADLX_SYNC_ORIGIN(ADLX_STD_CALL* GetOrigin)(IADLX3DSettingsChangedEvent3* pThis);

    // IADLX3DSettingsChangedEvent interface
    ADLX_RESULT(ADLX_STD_CALL* GetGPU)(IADLX3DSettingsChangedEvent3* pThis, IADLXGPU** ppGPU);
    adlx_bool(ADLX_STD_CALL* IsAntiLagChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsChillChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsBoostChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsImageSharpeningChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsEnhancedSyncChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsWaitForVerticalRefreshChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsFrameRateTargetControlChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsAntiAliasingChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsMorphologicalAntiAliasingChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsAnisotropicFilteringChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsTessellationModeChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsRadeonSuperResolutionChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsResetShaderCache)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsAMDFluidMotionFramesChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsImageSharpenDesktopChanged)(IADLX3DSettingsChangedEvent3* pThis);

    // IADLX3DSettingsChangedEvent3 interface
    adlx_bool(ADLX_STD_CALL* IsFidelityFXSuperResolutionChanged)(IADLX3DSettingsChangedEvent3* pThis);
    adlx_bool(ADLX_STD_CALL* IsFidelityFXFrameGenUpgradeChanged)(IADLX3DSettingsChangedEvent3* pThis);
} IADLX3DSettingsChangedEvent3Vtbl;

struct IADLX3DSettingsChangedEvent3 { const IADLX3DSettingsChangedEvent3Vtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLX3DSettingsChangedEvent3

#endif //ADLX_I3DSETTINGS3_H