//
// Copyright (c) 2023 - 2025 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_POWERTUNING1_H
#define ADLX_POWERTUNING1_H
#pragma once

#include "IPowerTuning.h"
#include "ICollections.h"

#pragma region IADLXSmartShiftEco
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXSmartShiftEco : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLXSmartShiftEco")

        /**
        *@page DOX_IADLXSmartShiftEco_IsSupported IsSupported
        *@ENG_START_DOX @brief Checks if AMD SmartShift Eco is supported. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupported (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the support status of AMD SmartShift Eco is returned. The variable is __true__ if AMD SmartShift Eco is supported. The variable is __false__ if AMD SmartShift Eco is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the support status of AMD SmartShift Eco is successfully returned, __ADLX_OK__ is returned.<br>
        * If the support status of AMD SmartShift Eco is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftEco_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsSupported (adlx_bool* supported) = 0;
        
        /**
        *@page DOX_IADLXSmartShiftEco_IsEnabled IsEnabled
        *@ENG_START_DOX @brief Checks if AMD SmartShift Eco is enabled. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsEnabled (adlx_bool* enabled)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],enabled,adlx_bool*,@ENG_START_DOX The pointer to a variable where the enabled state of AMD Smartshift Eco is returned. The variable is __true__ if AMD Smartshift Eco is enabled. The variable is __false__ if AMD Smartshift Eco is not enabled. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the enabled state of AMD Smartshift Eco is successfully returned, __ADLX_OK__ is returned.<br>
        * If the enabled state of AMD Smartshift Eco is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftEco_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsEnabled (adlx_bool* enabled) = 0;

        /**
        *@page DOX_IADLXSmartShiftEco_SetEnabled SetEnabled
        *@ENG_START_DOX @brief Sets AMD Smartshift Eco to enabled or disabled. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    SetEnabled (adlx_bool enable)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in],enable,adlx_bool,@ENG_START_DOX The new AMD Smartshift Eco state. Set __true__ to enable AMD Smartshift Eco. Set __false__ to disable AMD Smartshift Eco. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the state of AMD Smartshift Eco is successfully set, __ADLX_OK__ is returned.<br>
        * If the state of AMD Smartshift Eco is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftEco_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL SetEnabled (adlx_bool enabled) = 0;

        /**
        *@page DOX_IADLXSmartShiftEco_IsInactive IsInactive
        *@ENG_START_DOX @brief Checks if AMD SmartShift Eco is inactive. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsInactive (adlx_bool* inactive)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],inactive,adlx_bool*,@ENG_START_DOX The pointer to a variable where the inactive state of AMD Smartshift Eco is returned. The variable is __true__ if AMD Smartshift Eco is inactive. The variable is __false__ if AMD Smartshift Eco is active. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the inactive state of AMD Smartshift Eco is successfully returned, __ADLX_OK__ is returned.<br>
        * If the inactive state of AMD Smartshift Eco is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftEco_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsInactive (adlx_bool* inactive) = 0;

        /**
        *@page DOX_IADLXSmartShiftEco_GetInactiveReason GetInactiveReason
        *@ENG_START_DOX @brief Gets the reason why AMD SmartShift Eco is inactive. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetInactiveReason (@ref ADLX_SMARTSHIFT_ECO_INACTIVE_REASON* reason)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],reason,@ref ADLX_SMARTSHIFT_ECO_INACTIVE_REASON*,@ENG_START_DOX The pointer to a variable where the AMD SmartShift Eco inactive reason is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the inactive reason of AMD Smartshift Eco is successfully returned, __ADLX_OK__ is returned.<br>
        * If the inactive reason of AMD Smartshift Eco is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftEco_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetInactiveReason(ADLX_SMARTSHIFT_ECO_INACTIVE_REASON* reason) = 0;
    };  //IADLXSmartShiftEco
     //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXSmartShiftEco> IADLXSmartShiftEcoPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXSmartShiftEco, L"IADLXSmartShiftEco")
typedef struct IADLXSmartShiftEco IADLXSmartShiftEco;
typedef struct IADLXSmartShiftEcoVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXSmartShiftEco* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXSmartShiftEco* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXSmartShiftEco* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXSmartShiftEco
    ADLX_RESULT(ADLX_STD_CALL* IsSupported)(IADLXSmartShiftEco* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsEnabled)(IADLXSmartShiftEco* pThis, adlx_bool* enabled);
    ADLX_RESULT(ADLX_STD_CALL* SetEnabled)(IADLXSmartShiftEco* pThis, adlx_bool enabled);
    ADLX_RESULT(ADLX_STD_CALL* IsInactive)(IADLXSmartShiftEco* pThis, adlx_bool* inactive);
    ADLX_RESULT(ADLX_STD_CALL* GetInactiveReason)(IADLXSmartShiftEco* pThis, ADLX_SMARTSHIFT_ECO_INACTIVE_REASON* reason);
} IADLXSmartShiftEcoVtbl;
struct IADLXSmartShiftEco { const IADLXSmartShiftEcoVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXSmartShiftEco

#pragma region IADLXGPUConnectChangedEvent
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPU2;
    class ADLX_NO_VTABLE IADLXGPUConnectChangedEvent : public IADLXChangedEvent
    {
    public:
        ADLX_DECLARE_IID(L"IADLXGPUConnectChangedEvent")

        /**
        *@page DOX_IADLXGPUConnectChangedEvent_GetGPU GetGPU
        *@ENG_START_DOX @brief Gets the reference counted GPU interface on which the GPU Connect is changed. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetGPU (@ref DOX_IADLXGPU2** ppGPU)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,ppGPU,@ref DOX_IADLXGPU** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppGPU__ to __nullptr__. @ENG_END_DOX}
        *
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
        *@ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. <br>
        * __Note:__ @ref DOX_IADLXGPUConnectChangedEvent_GetGPU returns the reference counted GPU interface used by all the methods in this interface to check if there are any changes in GPU Connect.
        *@ENG_END_DOX
        *
        *@copydoc IADLXGPUConnectChangedEvent_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetGPU(IADLXGPU2** ppGPU) = 0;

        /**
        *@page DOX_IADLXGPUConnectChangedEvent_IsGPUAppsListChanged IsGPUAppsListChanged
        *@ENG_START_DOX @brief Checks if the list of applications created in the context of a GPU is changed. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsGPUAppsListChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX  If the GPU applications list is changed, __true__ is returned.<br>
        * If the GPU applications list is not changed, __false__ is returned.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        * __Note:__ To obtain the GPU, use @ref DOX_IADLXGPUConnectChangedEvent_GetGPU.
        *@ENG_END_DOX
        *
        *@copydoc IADLXGPUConnectChangedEvent_REQ_TABLE
        *
        */
        virtual adlx_bool   ADLX_STD_CALL IsGPUAppsListChanged() = 0;

        /**
        *@page DOX_IADLXGPUConnectChangedEvent_IsGPUPowerChanged IsGPUPowerChanged
        *@ENG_START_DOX @brief Checks if the power state of a GPU changed. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsGPUPowerChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX  If the GPU power state are changed, __true__ is returned.<br>
        * If the GPU power state are not changed, __false__ is returned.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        * __Note:__ To obtain the GPU, use @ref DOX_IADLXGPUConnectChangedEvent_GetGPU.
        *@ENG_END_DOX
        *
        *@copydoc IADLXGPUConnectChangedEvent_REQ_TABLE
        *
        */
        virtual adlx_bool   ADLX_STD_CALL IsGPUPowerChanged() = 0;

        /**
        *@page DOX_IADLXGPUConnectChangedEvent_IsGPUPowerChangeError IsGPUPowerChangeError
        *@ENG_START_DOX @brief Checks if an error occurred during powering off a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsGPUPowerChangeError ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX  If an error occurred during powering off a GPU, __true__ is returned.<br>
        * If no error occurred during powering off a GPU, __false__ is returned.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        * __Note:__ To obtain the GPU, use @ref DOX_IADLXGPUConnectChangedEvent_GetGPU.
        *@ENG_END_DOX
        *
        *@copydoc IADLXGPUConnectChangedEvent_REQ_TABLE
        *
        */
        virtual adlx_bool   ADLX_STD_CALL IsGPUPowerChangeError(ADLX_RESULT* pPowerChangeError) = 0;
    }; // IADLXGPUConnectChangedEvent
    typedef IADLXInterfacePtr_T<IADLXGPUConnectChangedEvent> IADLXGPUConnectChangedEventPtr;
} // namespace adlx
#else // __cplusplus
ADLX_DECLARE_IID(ADLXGPUConnectChangedEvent, L"ADLXGPUConnectChangedEvent")
typedef struct IADLXGPU2 IADLXGPU2;
typedef struct IADLXGPUConnectChangedEvent IADLXGPUConnectChangedEvent;
typedef struct IADLXGPUConnectChangedEventVtbl
{
    // IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXGPUConnectChangedEvent* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXGPUConnectChangedEvent* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXGPUConnectChangedEvent* pThis, const wchar_t* interfaceId, void** ppInterface);

    // IADLXChangedEvent
    ADLX_SYNC_ORIGIN(ADLX_STD_CALL* GetOrigin)(IADLXChangedEvent* pThis);

    // IADLXGPUConnectChangedEvent
    ADLX_RESULT(ADLX_STD_CALL* GetGPU)(IADLXGPUConnectChangedEvent* pThis, IADLXGPU2** ppGPU);
    adlx_bool(ADLX_STD_CALL* IsGPUAppsListChanged)(IADLXGPUConnectChangedEvent* pThis);
    adlx_bool(ADLX_STD_CALL* IsGPUPowerChanged)(IADLXGPUConnectChangedEvent* pThis);
    adlx_bool(ADLX_STD_CALL* IsGPUPowerChangeError)(IADLXGPUConnectChangedEvent* pThis, ADLX_RESULT* pPowerChangeError);
} IADLXGPUConnectChangedEventVtbl;
struct IADLXGPUConnectChangedEvent { const IADLXGPUConnectChangedEventVtbl* pVtbl; };
#endif
#pragma endregion IADLXGPUConnectChangedEvent

#pragma region IADLXGPUConnectChangedListener
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPUConnectChangedListener
    {
    public:
        /**
        *@page DOX_IADLXGPUConnectChangedListener_OnGPUConnectChanged OnGPUConnectChanged
        *@ENG_START_DOX @brief __OnGPUConnectChanged__ is called by ADLX when GPU connect settings change. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    OnGPUConnectChanged (@ref DOX_IADLXGPUConnectChangedEvent* pGPUConnectChangedEvent)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pGPUConnectChangedEvent,@ref DOX_IADLXGPUConnectChangedEvent* ,@ENG_START_DOX The pointer to a GPU connect settings change event. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the application requires ADLX to continue notifying the next listener, __true__ must be returned.<br>
        * If the application requires ADLX to stop notifying the next listener, __false__ must be returned.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX  Once the application registers to the notification with @ref DOX_IADLXGPU2_StartPowerOff, ADLX will call this method when AMD GPUConnect state changes.
        * This method should return quickly to not block the execution of ADLX. If the method requires a long processing of the event notification, the application must hold onto
        * a reference to the AMD GPUConnect change event with @ref DOX_IADLXInterface_Acquire and make it available on an asynchronous thread and return immediately.
        * When the asynchronous thread is done processing, it must discard the AMD GPUConnect change event with @ref DOX_IADLXInterface_Release. @ENG_END_DOX
        *
        *@copydoc IADLXGPUConnectChangedListener_REQ_TABLE
        *
        */
        virtual adlx_bool ADLX_STD_CALL OnGPUConnectChanged(IADLXGPUConnectChangedEvent* pGPUConnectChangedEvent) = 0;
    }; //IADLXGPUConnectChangedListener
} //namespace adlx
#else //__cplusplus
typedef struct IADLXGPUConnectChangedListener IADLXGPUConnectChangedListener;
typedef struct IADLXGPUConnectChangedListenerVtbl
{
    adlx_bool(ADLX_STD_CALL* OnGPUConnectChanged)(IADLXGPUConnectChangedListener* pThis, IADLXGPUConnectChangedEvent* pGPUConnectChangedEvent);
} IADLXGPUConnectChangedListenerVtbl;
struct IADLXGPUConnectChangedListener { const IADLXGPUConnectChangedListenerVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXGPUConnectChangedListener

#pragma region IADLXPowerTuningChangedEvent1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXPowerTuningChangedEvent1 : public IADLXPowerTuningChangedEvent
    {
    public:
        ADLX_DECLARE_IID (L"IADLXPowerTuningChangedEvent1")

        /**
        *@page DOX_IADLXPowerTuningChangedEvent1_IsSmartShiftEcoChanged IsSmartShiftEcoChanged
        *@ENG_START_DOX @brief Checks for changes to the AMD SmartShift Eco enable/disable and active/inactive state. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsSmartShiftEcoChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX  If there are any changes to the AMD SmartShift Eco state, __true__ is returned.<br>
        * If there are on changes to the AMD SmartShift Eco state, __false__ is returned.<br> @ENG_END_DOX
        *
        *@copydoc IADLXPowerTuningChangedEvent1_REQ_TABLE
        *
        */
        virtual adlx_bool   ADLX_STD_CALL IsSmartShiftEcoChanged () = 0;
    }; //IADLXPowerTuningChangedEvent1
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXPowerTuningChangedEvent1> IADLXPowerTuningChangedEvent1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXPowerTuningChangedEvent1, L"IADLXPowerTuningChangedEvent1")
typedef struct IADLXPowerTuningChangedEvent1 IADLXPowerTuningChangedEvent1;
typedef struct IADLXPowerTuningChangedEvent1Vtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXPowerTuningChangedEvent1* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXPowerTuningChangedEvent1* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXPowerTuningChangedEvent1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXChangedEvent
    ADLX_SYNC_ORIGIN(ADLX_STD_CALL* GetOrigin)(IADLXPowerTuningChangedEvent1* pThis);

    //IADLXPowerTuningChangedEvent
    adlx_bool (ADLX_STD_CALL *IsSmartShiftMaxChanged)(IADLXPowerTuningChangedEvent1* pThis);
    
    //IADLXPowerTuningChangedEvent1
    adlx_bool (ADLX_STD_CALL *IsSmartShiftEcoChanged)(IADLXPowerTuningChangedEvent1* pThis);
}IADLXPowerTuningChangedEvent1Vtbl;
struct IADLXPowerTuningChangedEvent1 { const IADLXPowerTuningChangedEvent1Vtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXPowerTuningChangedEvent1

#pragma region IADLXPowerTuningServices1
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPU2List;
    class ADLX_NO_VTABLE IADLXPowerTuningServices1 : public IADLXPowerTuningServices
    {
    public:
        ADLX_DECLARE_IID(L"IADLXPowerTuningServices1")

        /**
        *@page DOX_IADLXPowerTuningServices1_GetSmartShiftEco GetSmartShiftEco
        *@ENG_START_DOX @brief Gets the reference counted AMD SmartShift Eco interface. @ENG_END_DOX
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetSmartShiftEco (@ref DOX_IADLXSmartShiftEco** ppSmartShiftEco)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,ppSmartShiftEco,@ref DOX_IADLXSmartShiftEco** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppSmartShiftEco__ to __nullptr__. @ENG_END_DOX}
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
        *@ENG_START_DOX In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. @ENG_END_DOX
        *
        *@copydoc IADLXPowerTuningServices1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetSmartShiftEco (IADLXSmartShiftEco** ppSmartShiftEco) = 0;

        /**
        *@page DOX_IADLXPowerTuningServices1_IsGPUConnectSupported IsGPUConnectSupported
        *@ENG_START_DOX @brief Checks if AMD GPUConnect is supported on this system. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsGPUConnectSupported (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the support status of AMD SmartShift Eco is returned. The variable is __true__ if GPU connect is supported. The variable is __false__ if GPU connect is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the support status of GPU connect is successfully returned, __ADLX_OK__ is returned.<br>
        * If the support status of GPU connect is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftEco_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsGPUConnectSupported (adlx_bool* supported) = 0;
        
        /**
        *@page DOX_IADLXPowerTuningServices1_GetGPUConnectGPUs GetGPUConnectGPUs
        *@ENG_START_DOX @brief Gets the reference counted list of all the GPUs which support AMD GPUConnect. @ENG_END_DOX
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetGPUConnectGPUs (@ref DOX_IADLXGPU2List** ppGPUs)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,ppGPUs,@ref DOX_IADLXGPU2List** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppGPUs__ to __nullptr__. @ENG_END_DOX}
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
        *@ENG_START_DOX In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. @ENG_END_DOX
        *
        *@copydoc IADLXPowerTuningServices1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetGPUConnectGPUs (IADLXGPU2List** ppGPUs) = 0;
    };  //IADLXPowerTuningServices1
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXPowerTuningServices1> IADLXPowerTuningServices1Ptr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXPowerTuningServices1, L"IADLXPowerTuningServices1")
typedef struct IADLXPowerTuningServices1 IADLXPowerTuningServices1;
typedef struct IADLXGPU2List IADLXGPU2List;
typedef struct IADLXPowerTuningServices1Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXPowerTuningServices1* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXPowerTuningServices1* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXPowerTuningServices1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXPowerTuningServices
    ADLX_RESULT(ADLX_STD_CALL* GetPowerTuningChangedHandling)(IADLXPowerTuningServices1* pThis, IADLXPowerTuningChangedHandling** ppPowerTuningChangedHandling);
    ADLX_RESULT(ADLX_STD_CALL* GetSmartShiftMax)(IADLXPowerTuningServices1* pThis, IADLXSmartShiftMax** ppSmartShiftMax);

    //IADLXPowerTuningServices1
    ADLX_RESULT(ADLX_STD_CALL* GetSmartShiftEco)(IADLXPowerTuningServices1* pThis, IADLXSmartShiftEco** ppSmartShiftEco);
    ADLX_RESULT(ADLX_STD_CALL* IsGPUConnectSupported)(IADLXPowerTuningServices1* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUConnectGPUs)(IADLXPowerTuningServices1* pThis, IADLXGPU2List** ppGPUs);
}IADLXPowerTuningServices1Vtbl;
struct IADLXPowerTuningServices1 { const IADLXPowerTuningServices1Vtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXPowerTuningServices1

#endif //ADLX_POWERTUNING1_H