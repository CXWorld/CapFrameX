//
// Copyright (c) 2021 - 2024 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_IGPUTUNING1_H
#define ADLX_IGPUTUNING1_H
#pragma once

#include "ADLXStructures.h"
#include "IGPUTuning.h"

//-------------------------------------------------------------------------------------------------
//IGPUTuning1.h - Interfaces for ADLX GPU Tuning functionality

#pragma region IADLXGPUTuningServices1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPU;
    class ADLX_NO_VTABLE IADLXSmartAccessMemory;
    class ADLX_NO_VTABLE IADLXGPUTuningServices1 : public IADLXGPUTuningServices
    {
    public:
        ADLX_DECLARE_IID(L"IADLXGPUTuningServices1")

        /**
        *@page DOX_IADLXGPUTuningServices1_GetSmartAccessMemory GetSmartAccessMemory
        *@ENG_START_DOX @brief Gets the reference counted AMD SmartAccess Memory interface for a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetSmartAccessMemory (@ref DOX_IADLXGPU* pGPU, @ref DOX_IADLXSmartAccessMemory** ppSmartAccessMemory)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pGPU,@ref DOX_IADLXGPU* ,@ENG_START_DOX The pointer to the GPU interface. @ENG_END_DOX}
        *@paramrow{2.,[out],ppSmartAccessMemory,@ref DOX_IADLXSmartAccessMemory**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppSmartAccessMemory__ to __nullptr__. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX
        * If the interface is successfully returned, __ADLX_OK__ is returned. <br>
        * If the interface is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when its no longer needed. @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        *In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. @ENG_END_DOX
        *
        *@copydoc IADLXGPUTuningServices1_REQ_TABLE
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetSmartAccessMemory(IADLXGPU* pGPU, IADLXSmartAccessMemory** ppSmartAccessMemory) = 0;
    }; //IADLXGPUTuningServices1
    typedef IADLXInterfacePtr_T<IADLXGPUTuningServices1> IADLXGPUTuningServices1Ptr;
} // namespace adlx
#else // __cplusplus
ADLX_DECLARE_IID(IADLXGPUTuningServices1, L"IADLXGPUTuningServices1")
typedef struct IADLXSmartAccessMemory IADLXSmartAccessMemory;
typedef struct IADLXGPUTuningServices1 IADLXGPUTuningServices1;

typedef struct IADLXGPUTuningServices1Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXGPUTuningServices1* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXGPUTuningServices1* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXGPUTuningServices1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXGPUTuningServices
    ADLX_RESULT(ADLX_STD_CALL* GetGPUTuningChangedHandling)(IADLXGPUTuningServices1* pThis, IADLXGPUTuningChangedHandling** ppGPUTuningChangedHandling);
    ADLX_RESULT(ADLX_STD_CALL* IsAtFactory)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, adlx_bool* isFactory);
    ADLX_RESULT(ADLX_STD_CALL* ResetToFactory)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU);

    ADLX_RESULT(ADLX_STD_CALL* IsSupportedAutoTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedPresetTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedManualGFXTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedManualVRAMTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedManualFanTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedManualPowerTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, adlx_bool* supported);

    ADLX_RESULT(ADLX_STD_CALL* GetAutoTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, IADLXInterface** ppAutoTuning);
    ADLX_RESULT(ADLX_STD_CALL* GetPresetTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, IADLXInterface** ppPresetTuning);
    ADLX_RESULT(ADLX_STD_CALL* GetManualGFXTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, IADLXInterface** ppManualGFXTuning);
    ADLX_RESULT(ADLX_STD_CALL* GetManualVRAMTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, IADLXInterface** ppManualVRAMTuning);
    ADLX_RESULT(ADLX_STD_CALL* GetManualFanTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, IADLXInterface** ppManualFanTuning);
    ADLX_RESULT(ADLX_STD_CALL* GetManualPowerTuning)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, IADLXInterface** ppManualPowerTuning);

    //IADLXGPUTuningServices1
    ADLX_RESULT(ADLX_STD_CALL* GetSmartAccessMemory)(IADLXGPUTuningServices1* pThis, IADLXGPU* pGPU, IADLXSmartAccessMemory** ppSmartAccessMemory);
}IADLXGPUTuningServices1Vtbl;

struct IADLXGPUTuningServices1 { const IADLXGPUTuningServices1Vtbl* pVtbl; };
#endif // __cplusplus
#pragma endregion IADLXGPUTuningServices1

#pragma region IADLXGPUTuningChangedEvent1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPUTuningChangedEvent1 : public IADLXGPUTuningChangedEvent
    {
    public:
        ADLX_DECLARE_IID(L"IADLXGPUTuningChangedEvent1")

        /**
        *@page DOX_IADLXGPUTuningChangedEvent1_IsSmartAccessMemoryChanged IsSmartAccessMemoryChanged
        *@ENG_START_DOX @brief Checks for changes to the AMD SmartAccess Memory settings. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsSmartAccessMemoryChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX  If AMD SmartAccess Memory settings are changed, __true__ is returned.<br>
        * If AMD SmartAccess Memory settings are not changed, __false__ is returned.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        * __Note:__ To obtain the GPU, use @ref DOX_IADLXGPUTuningChangedEvent_GetGPU.
        * @ENG_END_DOX
        *
        *@copydoc IADLXGPUTuningChangedEvent1_REQ_TABLE
        *
        */
        virtual adlx_bool ADLX_STD_CALL IsSmartAccessMemoryChanged() = 0;

        /**
        *@page DOX_IADLXGPUTuningChangedEvent1_GetSmartAccessMemoryStatus GetSmartAccessMemoryStatus
        *@ENG_START_DOX @brief Gets the current AMD SmartAccess Memory status. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetSmartAccessMemoryStatus (adlx_bool* pEnabled, adlx_bool* pCompleted)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,pEnabled,adlx_bool* ,@ENG_START_DOX The pointer to a variable where the current state of AMD SmartAccess Memory is returned. The variable is __true__ if AMD SmartAccess Memory is enabled. The variable is __false__ if AMD SmartAccess Memory is disabled. @ENG_END_DOX}
        *@paramrow{2.,[out] ,pEnabled,adlx_bool* ,@ENG_START_DOX The pointer to a variable where the complete state of AMD SmartAccess Memory is returned. The variable is __true__ if AMD SmartAccess Memory is completed. The variable is __false__ if AMD SmartAccess Memory is not completed. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the interface is successfully returned, __ADLX_OK__ is returned.<br>
        * If the interface is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        * __Note:__ Use @ref DOX_IADLXGPUTuningChangedEvent_GetGPU to obtain the GPU.
        * @ENG_END_DOX
        *
        *@copydoc IADLXGPUTuningChangedEvent1_REQ_TABLE
        * 
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetSmartAccessMemoryStatus(adlx_bool* pEnabled, adlx_bool* pCompleted) = 0;
    };
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXGPUTuningChangedEvent1> IADLXGPUTuningChangedEvent1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXGPUTuningChangedEvent1, L"IADLXGPUTuningChangedEvent1")

typedef struct IADLXGPUTuningChangedEvent1 IADLXGPUTuningChangedEvent1;

typedef struct IADLXGPUTuningChangedEvent1Vtbl
{
    // IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXGPUTuningChangedEvent1* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXGPUTuningChangedEvent1* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXGPUTuningChangedEvent1* pThis, const wchar_t* interfaceId, void** ppInterface);

    // IADLXChangedEvent
    ADLX_SYNC_ORIGIN(ADLX_STD_CALL* GetOrigin)(IADLXGPUTuningChangedEvent1* pThis);

    // IADLXGPUTuningChangedEvent
    ADLX_RESULT(ADLX_STD_CALL* GetGPU)(IADLXGPUTuningChangedEvent1* pThis, IADLXGPU** ppGPU);
    adlx_bool(ADLX_STD_CALL* IsAutomaticTuningChanged)(IADLXGPUTuningChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsPresetTuningChanged)(IADLXGPUTuningChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsManualGPUCLKTuningChanged)(IADLXGPUTuningChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsManualVRAMTuningChanged)(IADLXGPUTuningChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsManualFanTuningChanged)(IADLXGPUTuningChangedEvent1* pThis);
    adlx_bool(ADLX_STD_CALL* IsManualPowerTuningChanged)(IADLXGPUTuningChangedEvent1* pThis);

    // IADLXGPUTuningChangedEvent1
    adlx_bool(ADLX_STD_CALL* IsSmartAccessMemoryChanged)(IADLXGPUTuningChangedEvent1* pThis);
    ADLX_RESULT(ADLX_STD_CALL* GetSmartAccessMemoryStatus)(IADLXGPUTuningChangedEvent1* pThis, adlx_bool* pEnabled, adlx_bool* pCompleted);
} IADLXGPUTuningChangedEvent1Vtbl;

struct IADLXGPUTuningChangedEvent1 { const IADLXGPUTuningChangedEvent1Vtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXGPUTuningChangedEvent1

#endif // ADLX_IGPUTUNING1_H
