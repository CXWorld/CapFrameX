//
// Copyright (c) 2021 - 2024 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_POWERTUNING_H
#define ADLX_POWERTUNING_H
#pragma once

#include "ADLXStructures.h"
#include "IChangedEvent.h"

#pragma region IADLXSmartShiftMax
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXSmartShiftMax : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID (L"IADLXSmartShiftMax")

        /**
        *@page DOX_IADLXSmartShiftMax_IsSupported IsSupported
        *@ENG_START_DOX @brief Checks if AMD SmartShift Max is supported. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupported (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of AMD SmartShift Max is returned. The variable is __true__ if AMD SmartShift Max is supported. The variable is __false__ if AMD SmartShift Max is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the state of AMD SmartShift Max is successfully returned, __ADLX_OK__ is returned.<br>
        * If the state of AMD SmartShift Max is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        *AMD SmartShift Max dynamically shifts power between CPU and GPU to boost performance, depending on workload. 
        *@ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftMax_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL IsSupported (adlx_bool* supported) = 0;

        /**
        *@page DOX_IADLXSmartShiftMax_GetBiasMode GetBiasMode
        *@ENG_START_DOX @brief Gets the AMD SmartShift Max current bias mode. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetBiasMode (@ref ADLX_SSM_BIAS_MODE* mode)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],mode,@ref ADLX_SSM_BIAS_MODE*,@ENG_START_DOX The pointer to a variable where the AMD SmartShift Max current bias mode is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the current bias mode is successfully returned, __ADLX_OK__ is returned.<br>
        * If the current bias mode is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        *AMD SmartShift Max dynamically shifts power between CPU and GPU to boost performance according to workload dependencies.
        *@ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftMax_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL GetBiasMode(ADLX_SSM_BIAS_MODE* mode) = 0;

        /**
        *@page DOX_IADLXSmartShiftMax_SetBiasMode SetBiasMode
        *@ENG_START_DOX @brief Sets AMD SmartShift Max bias mode. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    SetBiasMode (@ref ADLX_SSM_BIAS_MODE mode)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in],mode,@ref ADLX_SSM_BIAS_MODE,@ENG_START_DOX The new AMD SmartShift Max bias mode. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the bias mode is successfully set, __ADLX_OK__ is returned.<br>
        * If the bias mode is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        *AMD SmartShift Max dynamically shifts power between CPU and GPU to boost performance, depending on workload. 
        *@ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftMax_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL SetBiasMode(ADLX_SSM_BIAS_MODE mode) = 0;

        /**
        *@page DOX_IADLXSmartShiftMax_GetBiasRange GetBiasRange
        *@ENG_START_DOX @brief Gets maximum bias, minimum bias, and step bias of AMD SmartShift Max. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetBiasRange (@ref ADLX_IntRange* range)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],range,@ref ADLX_IntRange*,@ENG_START_DOX The pointer to a variable where the bias range of AMD SmartShift Max is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX If the bias range is successfully returned, __ADLX_OK__ is returned.<br>
        * If the bias range is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        *AMD SmartShift Max dynamically shifts power between CPU and GPU to boost performance, depending on workload. 
        *@ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftMax_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL GetBiasRange(ADLX_IntRange* range) = 0;

        /**
        *@page DOX_IADLXSmartShiftMax_GetBias GetBias
        *@ENG_START_DOX @brief Gets the AMD SmartShift Max current bias. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetBias (adlx_int* bias)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],bias,adlx_int*,@ENG_START_DOX The pointer to a variable where the AMD SmartShift Max current bias is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the current bias is successfully returned, __ADLX_OK__ is returned.<br>
        * If the current bias is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        *AMD SmartShift Max dynamically shifts power between CPU and GPU to boost performance, depending on workload. 
        *@ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftMax_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL GetBias(adlx_int* bias) = 0;

        /**
        *@page DOX_IADLXSmartShiftMax_SetBias SetBias
        *@ENG_START_DOX @brief Sets the bias of AMD SmartShift Max. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    SetBias (adlx_int bias)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[in],bias,adlx_int,@ENG_START_DOX The new AMD SmartShift Max bias. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the bias is successfully set, __ADLX_OK__ is returned.<br>
        * If the bias is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        *AMD SmartShift Max dynamically shifts power between CPU and GPU to boost performance, depending on workload. 
        *@ENG_END_DOX
        *
        *@copydoc IADLXSmartShiftMax_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL SetBias(adlx_int bias) = 0;
    };  //IADLXSmartShiftMax
     //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXSmartShiftMax> IADLXSmartShiftMaxPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXSmartShiftMax, L"IADLXSmartShiftMax")
typedef struct IADLXSmartShiftMax IADLXSmartShiftMax;
typedef struct IADLXSmartShiftMaxVtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXSmartShiftMax* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXSmartShiftMax* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXSmartShiftMax* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXSmartShiftMax
    ADLX_RESULT (ADLX_STD_CALL *IsSupported)(IADLXSmartShiftMax* pThis, adlx_bool* supported);
    ADLX_RESULT (ADLX_STD_CALL *GetBiasMode)(IADLXSmartShiftMax* pThis, ADLX_SSM_BIAS_MODE* mode);
    ADLX_RESULT (ADLX_STD_CALL *SetBiasMode)(IADLXSmartShiftMax* pThis, ADLX_SSM_BIAS_MODE mode);
    ADLX_RESULT (ADLX_STD_CALL *GetBiasRange)(IADLXSmartShiftMax* pThis, ADLX_IntRange* range);
    ADLX_RESULT (ADLX_STD_CALL *GetBias)(IADLXSmartShiftMax* pThis, adlx_int* bias);
    ADLX_RESULT (ADLX_STD_CALL *SetBias)(IADLXSmartShiftMax* pThis, adlx_int bias);
}IADLXSmartShiftMaxVtbl;
struct IADLXSmartShiftMax { const IADLXSmartShiftMaxVtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXSmartShiftMax

#pragma region IADLXPowerTuningChangedEvent
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXPowerTuningChangedEvent : public IADLXChangedEvent
    {
    public:
        ADLX_DECLARE_IID (L"IADLXPowerTuningChangedEvent")

        /**
        *@page DOX_IADLXPowerTuningChangedEvent_IsSmartShiftMaxChanged IsSmartShiftMaxChanged
        *@ENG_START_DOX @brief Checks for changes to the AMD SmartShift Max settings. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsSmartShiftMaxChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX  If there are any changes to the AMD SmartShift Max settings, __true__ is returned.<br>
        * If there are on changes to the AMD SmartShift Max settings, __false__ is returned.<br> @ENG_END_DOX
        *
        *@copydoc IADLXPowerTuningChangedEvent_REQ_TABLE
        *
        */
        virtual adlx_bool   ADLX_STD_CALL IsSmartShiftMaxChanged () = 0;
    }; //IADLXPowerTuningChangedEvent
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXPowerTuningChangedEvent> IADLXPowerTuningChangedEventPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXPowerTuningChangedEvent, L"IADLXPowerTuningChangedEvent")
typedef struct IADLXPowerTuningChangedEvent IADLXPowerTuningChangedEvent;
typedef struct IADLXPowerTuningChangedEventVtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXPowerTuningChangedEvent* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXPowerTuningChangedEvent* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXPowerTuningChangedEvent* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXChangedEvent
    ADLX_SYNC_ORIGIN(ADLX_STD_CALL* GetOrigin)(IADLXPowerTuningChangedEvent* pThis);

    //IADLXPowerTuningChangedEvent
    adlx_bool (ADLX_STD_CALL *IsSmartShiftMaxChanged)(IADLXPowerTuningChangedEvent* pThis);
}IADLXPowerTuningChangedEventVtbl;
struct IADLXPowerTuningChangedEvent { const IADLXPowerTuningChangedEventVtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXPowerTuningChangedEvent

#pragma region IADLXPowerTuningChangedListener
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXPowerTuningChangedListener
    {
    public:
        /**
        *@page DOX_IADLXPowerTuningChangedListener_OnPowerTuningChanged OnPowerTuningChanged
        *@ENG_START_DOX @brief __OnPowerTuningChanged__ is called by ADLX when power tuning settings change. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    OnPowerTuningChanged (@ref DOX_IADLXPowerTuningChangedEvent* pPowerTuningChangedEvent)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pPowerTuningChangedEvent,@ref DOX_IADLXPowerTuningChangedEvent* ,@ENG_START_DOX The pointer to a power tuning settings change event. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the application requires ADLX to continue notifying the next listener, __true__ must be returned.<br>
        * If the application requires ADLX to stop notifying the next listener, __false__ must be returned.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX  Once the application registers to the notifications with @ref DOX_IADLXPowerTuningChangedHandling_AddPowerTuningEventListener, ADLX will call this method until the application unregisters from the notifications with @ref DOX_IADLXPowerTuningChangedHandling_RemovePowerTuningEventListener.
        * The method should return quickly to not block the execution path in ADLX. If the method requires a long processing of the event notification, the application must hold onto a reference to the power tuning settings change event with @ref DOX_IADLXInterface_Acquire and make it available on an asynchronous thread and return immediately. When the asynchronous thread is done processing it must discard the power tuning settings change event with @ref DOX_IADLXInterface_Release. @ENG_END_DOX
        *
        *@copydoc IADLXPowerTuningChangedListener_REQ_TABLE
        *
        */
        virtual adlx_bool ADLX_STD_CALL OnPowerTuningChanged(IADLXPowerTuningChangedEvent* pPowerTuningChangedEvent) = 0;
    }; //IADLXPowerTuningChangedListener
} //namespace adlx
#else //__cplusplus
typedef struct IADLXPowerTuningChangedListener IADLXPowerTuningChangedListener;
typedef struct IADLXPowerTuningChangedListenerVtbl
{
    adlx_bool (ADLX_STD_CALL *OnPowerTuningChanged)(IADLXPowerTuningChangedListener* pThis, IADLXPowerTuningChangedEvent* pPowerTuningChangedEvent);
} IADLXPowerTuningChangedListenerVtbl;
struct IADLXPowerTuningChangedListener { const IADLXPowerTuningChangedListenerVtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXPowerTuningChangedListener

#pragma region IADLXPowerTuningChangedHandling
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXPowerTuningChangedHandling : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID (L"IADLXPowerTuningChangedHandling")

        /**
        *@page DOX_IADLXPowerTuningChangedHandling_AddPowerTuningEventListener AddPowerTuningEventListener
        *@ENG_START_DOX @brief Registers an event listener for notifications whenever power tuning settings are changed. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    AddPowerTuningEventListener (@ref DOX_IADLXPowerTuningChangedListener* pPowerTuningChangedListener)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pPowerTuningChangedListener,@ref DOX_IADLXPowerTuningChangedListener* ,@ENG_START_DOX The pointer to the event listener interface to register for receiving power tuning settings change notifications. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the event listener is successfully registered, __ADLX_OK__ is returned.<br>
        * If the event listener is not successfully registered, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX  Once the event listener is successfully registered, ADLX calls the @ref DOX_IADLXPowerTuningChangedListener_OnPowerTuningChanged listener method whenever power tuning settings are changed.<br>
        * The event listener instance must exist until the application unregisters the event listener with @ref DOX_IADLXPowerTuningChangedHandling_RemovePowerTuningEventListener.<br> @ENG_END_DOX
        *
        *@copydoc IADLXPowerTuningChangedHandling_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL AddPowerTuningEventListener (IADLXPowerTuningChangedListener* pPowerTuningChangedListener) = 0;

        /**
        *@page DOX_IADLXPowerTuningChangedHandling_RemovePowerTuningEventListener RemovePowerTuningEventListener
        *@ENG_START_DOX @brief Unregisters an event listener from the power tuning settings event list. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    RemovePowerTuningEventListener (@ref DOX_IADLXPowerTuningChangedListener* pPowerTuningChangedListener)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pPowerTuningChangedListener,@ref DOX_IADLXPowerTuningChangedListener* ,@ENG_START_DOX The pointer to the event listener interface to unregister from receiving power tuning settings change notifications. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the event listener is successfully unregistered, __ADLX_OK__ is returned.<br>
        * If the event listener is not successfully unregistered, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX  Once the event listener is successfully unregistered, ADLX will no longer call the @ref DOX_IADLXPowerTuningChangedListener_OnPowerTuningChanged listener method when power tuning settings are changed.
        * The application can discard the event listener instance. @ENG_END_DOX
        *
        *@copydoc IADLXPowerTuningChangedHandling_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL RemovePowerTuningEventListener (IADLXPowerTuningChangedListener* pPowerTuningChangedListener) = 0;

    }; //IADLXPowerTuningChangedHandling
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXPowerTuningChangedHandling> IADLXPowerTuningChangedHandlingPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXPowerTuningChangedHandling, L"IADLXPowerTuningChangedHandling")
typedef struct IADLXPowerTuningChangedHandling IADLXPowerTuningChangedHandling;
typedef struct IADLXPowerTuningChangedHandlingVtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXPowerTuningChangedHandling* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXPowerTuningChangedHandling* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXPowerTuningChangedHandling* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXPowerTuningChangedHandling
    ADLX_RESULT (ADLX_STD_CALL *AddPowerTuningEventListener)(IADLXPowerTuningChangedHandling* pThis, IADLXPowerTuningChangedListener* pPowerTuningChangedListener);
    ADLX_RESULT (ADLX_STD_CALL *RemovePowerTuningEventListener)(IADLXPowerTuningChangedHandling* pThis, IADLXPowerTuningChangedListener* pPowerTuningChangedListener);
} IADLXPowerTuningChangedHandlingVtbl;
struct IADLXPowerTuningChangedHandling { const IADLXPowerTuningChangedHandlingVtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXPowerTuningChangedHandling

#pragma region IADLXPowerTuningServices
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXPowerTuningServices : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID (L"IADLXPowerTuningServices")

        /**
        *@page DOX_IADLXPowerTuningServices_GetPowerTuningChangedHandling GetPowerTuningChangedHandling
        *@ENG_START_DOX @brief Gets the reference counted interface that allows registering and unregistering for notifications when power tuning settings change. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetPowerTuningChangedHandling (@ref DOX_IADLXPowerTuningChangedHandling** ppPowerTuningChangedHandling)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,ppPowerTuningChangedHandling,@ref DOX_IADLXPowerTuningChangedHandling** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppPowerTuningChangedHandling__ to __nullptr__. @ENG_END_DOX}
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
        *@copydoc IADLXPowerTuningServices_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL GetPowerTuningChangedHandling (IADLXPowerTuningChangedHandling** ppPowerTuningChangedHandling) = 0;

        /**
        *@page DOX_IADLXPowerTuningServices_GetSmartShiftMax GetSmartShiftMax
        *@ENG_START_DOX @brief Gets the reference counted AMD SmartShift Max interface. @ENG_END_DOX
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetSmartShiftMax (@ref DOX_IADLXSmartShiftMax** ppSmartShiftMax)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,ppSmartShiftMax,@ref DOX_IADLXSmartShiftMax** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppSmartShiftMax__ to __nullptr__. @ENG_END_DOX}
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
        *@copydoc IADLXPowerTuningServices_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL GetSmartShiftMax (IADLXSmartShiftMax** ppSmartShiftMax) = 0;
    };  //IADLXPowerTuningServices
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXPowerTuningServices> IADLXPowerTuningServicesPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXPowerTuningServices, L"IADLXPowerTuningServices")
typedef struct IADLXPowerTuningServices IADLXPowerTuningServices;
typedef struct IADLXPowerTuningServicesVtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXPowerTuningServices* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXPowerTuningServices* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXPowerTuningServices* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXPowerTuningServices
    ADLX_RESULT (ADLX_STD_CALL *GetPowerTuningChangedHandling)(IADLXPowerTuningServices* pThis, IADLXPowerTuningChangedHandling** ppPowerTuningChangedHandling);
    ADLX_RESULT (ADLX_STD_CALL *GetSmartShiftMax)(IADLXPowerTuningServices* pThis, IADLXSmartShiftMax** ppSmartShiftMax);
}IADLXPowerTuningServicesVtbl;
struct IADLXPowerTuningServices { const IADLXPowerTuningServicesVtbl *pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXPowerTuningServices

#endif //ADLX_POWERTUNING_H
