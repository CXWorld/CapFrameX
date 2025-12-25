//
// Copyright (c) 2023 - 2025 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_MULTIMEDIA_H
#define ADLX_MULTIMEDIA_H
#pragma once

#include "ADLXStructures.h"
#include "IChangedEvent.h"

#pragma region IADLXVideoUpscale
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXVideoUpscale : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLXVideoUpscale")

            /**
            *@page DOX_IADLXVideoUpscale_IsSupported IsSupported
            *@ENG_START_DOX @brief Checks if video upscale is supported on a GPU. @ENG_END_DOX
            *
            *@syntax
            *@codeStart
            * @ref ADLX_RESULT    IsSupported (adlx_bool* supported)
            *@codeEnd
            *
            *@params
            * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of video upscale is returned. The variable is __true__ if video upscale is supported. The variable is __false__ if video upscale is not supported. @ENG_END_DOX}
            *
            *@retvalues
            *@ENG_START_DOX  If the state of video upscale is successfully returned, __ADLX_OK__ is returned.<br>
            * If the state of video upscale is not successfully returned, an error code is returned.<br>
            * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
            *
            *@addinfo
            *@ENG_START_DOX
            *Video upscale improves video playback quality by applying sharpening on the upscaled video.
            *@ENG_END_DOX
            *
            *@copydoc IADLXVideoUpscale_REQ_TABLE
            *
            */
            virtual ADLX_RESULT         ADLX_STD_CALL IsSupported(adlx_bool* supported) = 0;

            /**
            *@page DOX_IADLXVideoUpscale_IsEnabled IsEnabled
            *@ENG_START_DOX @brief Checks if video upscale is enabled on a GPU. @ENG_END_DOX
            *
            *@syntax
            *@codeStart
            * @ref ADLX_RESULT    IsEnabled (adlx_bool* enabled)
            *@codeEnd
            *
            *@params
            * @paramrow{1.,[out],enabled,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of video upscale is returned. The variable is __true__ if video upscale is enabled. The variable is __false__ if video upscale is not enabled. @ENG_END_DOX}
            *
            *@retvalues
            *@ENG_START_DOX  If the state of video upscale is successfully returned, __ADLX_OK__ is returned.<br>
            * If the state of video upscale is not successfully returned, an error code is returned.<br>
            * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
            *
            * @addinfo
            *@ENG_START_DOX
            *Video upscale improves video playback quality by applying sharpening on the upscaled video.
            *@ENG_END_DOX
            *
            *@copydoc IADLXVideoUpscale_REQ_TABLE
            *
            */
            virtual ADLX_RESULT         ADLX_STD_CALL IsEnabled (adlx_bool* isEnabled) = 0;

            /**
            *@page DOX_IADLXVideoUpscale_GetSharpnessRange GetSharpnessRange
            *@ENG_START_DOX @brief Gets the maximum sharpness, minimum sharpness, and step sharpness of video upscale on a GPU. @ENG_END_DOX
            *
            *@syntax
            *@codeStart
            * @ref ADLX_RESULT    GetSharpnessRange (@ref ADLX_IntRange* range)
            *@codeEnd
            *
            *@params
            * @paramrow{1.,[out],range,@ref ADLX_IntRange*,@ENG_START_DOX The pointer to a variable where the sharpness range of video upscale is returned. @ENG_END_DOX}
            *
            *@retvalues
            *@ENG_START_DOX  If the sharpness range is successfully returned, __ADLX_OK__ is returned.<br>
            * If the sharpness range is not successfully returned, an error code is returned.<br>
            * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
            *
            *@detaileddesc
            *@ENG_START_DOX @details The maximum sharpness, minimum sharpness, and step sharpness of video upscale are read only. @ENG_END_DOX
            *
            * @addinfo
            *@ENG_START_DOX
            *Video upscale improves video playback quality by applying sharpening on the upscaled video.
            *@ENG_END_DOX
            *
            *@copydoc IADLXVideoUpscale_REQ_TABLE
            *
            */
            virtual ADLX_RESULT         ADLX_STD_CALL GetSharpnessRange(ADLX_IntRange* range) = 0;

            /**
            *@page DOX_IADLXVideoUpscale_GetSharpness GetSharpness
            *@ENG_START_DOX @brief Gets the current minimum sharpness of video upscale on a GPU. @ENG_END_DOX
            *
            *@syntax
            *@codeStart
            * @ref ADLX_RESULT    GetSharpness (adlx_int* currentMinRes)
            *@codeEnd
            *
            *@params
            * @paramrow{1.,[out],currentMinRes,adlx_int*,@ENG_START_DOX The pointer to a variable where the current minimum sharpness of video upscale is returned. @ENG_END_DOX}
            *
            *@retvalues
            *@ENG_START_DOX  If the current minimum sharpness is successfully returned, __ADLX_OK__ is returned.<br>
            * If the current minimum sharpness is not successfully returned, an error code is returned.<br>
            * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
            *
            *@addinfo
            *@ENG_START_DOX
            *Video upscale improves video playback quality by applying sharpening on the upscaled video.
            *@ENG_END_DOX
            *
            *@copydoc IADLXVideoUpscale_REQ_TABLE
            *
            */
            virtual ADLX_RESULT         ADLX_STD_CALL GetSharpness(adlx_int* currentMinRes) = 0;

            /**
             *@page DOX_IADLXVideoUpscale_SetEnabled SetEnabled
             *@ENG_START_DOX @brief Sets video upscale to enabled or disabled on a GPU. @ENG_END_DOX
             *
             *@syntax
             *@codeStart
             * @ref ADLX_RESULT    SetEnabled (adlx_bool enable)
             *@codeEnd
             *
             *@params
             * @paramrow{1.,[in],enable,adlx_bool,@ENG_START_DOX The new video upscale state. Set __true__ to enable video upscale. Set __false__ to disable video upscale. @ENG_END_DOX}
             *
             *@retvalues
             *@ENG_START_DOX  If the state of video upscale is successfully set, __ADLX_OK__ is returned.<br>
             * If the state of video upscale is not successfully set, an error code is returned.<br>
             * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
             *
             * @addinfo
            *@ENG_START_DOX
            *Video upscale improves video playback quality by applying sharpening on the upscaled video.
            *@ENG_END_DOX
            *
            *@copydoc IADLXVideoUpscale_REQ_TABLE
             *
             */
            virtual ADLX_RESULT         ADLX_STD_CALL SetEnabled(adlx_bool enable) = 0;

            /**
            *@page DOX_IADLXVideoUpscale_SetSharpness SetSharpness
            *@ENG_START_DOX @brief Sets the minimum sharpness of video upscale on a GPU. @ENG_END_DOX
            *
            *@syntax
            *@codeStart
            * @ref ADLX_RESULT    SetSharpness (adlx_int minRes)
            *@codeEnd
            *
            *@params
            * @paramrow{1.,[in],minRes,adlx_int,@ENG_START_DOX The new minimum sharpness of video upscale. @ENG_END_DOX}
            *
            *@retvalues
            *@ENG_START_DOX  If the minimum sharpness is successfully set, __ADLX_OK__ is returned.<br>
            * If the minimum sharpness is not successfully set, an error code is returned.<br>
            * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
            *
            * @addinfo
            *@ENG_START_DOX
            *Video upscale improves video playback quality by applying sharpening on the upscaled video.
            *@ENG_END_DOX
            *
            *@copydoc IADLXVideoUpscale_REQ_TABLE
            *
            */
            virtual ADLX_RESULT         ADLX_STD_CALL SetSharpness(adlx_int minSharp) = 0;

    };  //IADLXVideoUpscale
     //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXVideoUpscale> IADLXVideoUpscalePtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXVideoUpscale, L"IADLXVideoUpscale")
typedef struct IADLXVideoUpscale IADLXVideoUpscale;
typedef struct IADLXVideoUpscaleVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXVideoUpscale* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXVideoUpscale* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXVideoUpscale* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXVideoUpscale
    ADLX_RESULT(ADLX_STD_CALL* IsSupported)         (IADLXVideoUpscale* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsEnabled)           (IADLXVideoUpscale* pThis, adlx_bool* isEnabled);
    ADLX_RESULT(ADLX_STD_CALL* GetSharpnessRange)   (IADLXVideoUpscale* pThis, ADLX_IntRange* range);
    ADLX_RESULT(ADLX_STD_CALL* GetSharpness)        (IADLXVideoUpscale* pThis, adlx_int* currentMinRes);
    ADLX_RESULT(ADLX_STD_CALL* SetEnabled)          (IADLXVideoUpscale* pThis, adlx_bool enable);
    ADLX_RESULT(ADLX_STD_CALL* SetSharpness)        (IADLXVideoUpscale* pThis, adlx_int minSharp);

}IADLXVideoUpscaleVtbl;

struct IADLXVideoUpscale { const IADLXVideoUpscaleVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXVideoUpscale

#pragma region IADLXVideoSuperResolution
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXVideoSuperResolution : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLXVideoSuperResolution")


            virtual ADLX_RESULT         ADLX_STD_CALL IsSupported(adlx_bool* supported) = 0;
            virtual ADLX_RESULT         ADLX_STD_CALL IsEnabled(adlx_bool* isEnabled) = 0;
            virtual ADLX_RESULT         ADLX_STD_CALL SetEnabled(adlx_bool enable) = 0;

    };  //IADLXVideoSuperResolution
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXVideoSuperResolution> IADLXVideoSuperResolutionPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXVideoSuperResolution, L"IADLXVideoSuperResolution")
typedef struct IADLXVideoSuperResolution IADLXVideoSuperResolution;
typedef struct IADLXVideoSuperResolutionVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXVideoSuperResolution* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXVideoSuperResolution* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXVideoSuperResolution* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXVideoSuperResolution
    ADLX_RESULT(ADLX_STD_CALL* IsSupported)         (IADLXVideoSuperResolution* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* IsEnabled)           (IADLXVideoSuperResolution* pThis, adlx_bool* isEnabled);
    ADLX_RESULT(ADLX_STD_CALL* SetEnabled)          (IADLXVideoSuperResolution* pThis, adlx_bool enable);

}IADLXVideoSuperResolutionVtbl;

struct IADLXVideoSuperResolution { const IADLXVideoSuperResolutionVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXVideoSuperResolution


#pragma region IADLXMultimediaChangedEvent 
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPU;

    class ADLX_NO_VTABLE IADLXMultimediaChangedEvent : public IADLXChangedEvent
    {
    public:
        ADLX_DECLARE_IID(L"IADLXMultimediaChangedEvent")

        /**
        *@page DOX_IADLXMultimediaChangedEvent_GetGPU GetGPU
        *@ENG_START_DOX @brief Gets the reference-counted GPU interface on which multimedia settings are changed. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetGPU (@ref DOX_IADLXGPU **ppGPU)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,ppGPU,@ref DOX_IADLXGPU** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppGPU__ to __nullptr__. @ENG_END_DOX}
        *
        *
        *@retvalues
        *@ENG_START_DOX  If the GPU interface is successfully returned, __ADLX_OK__ is returned.<br>
        * If the GPU interface is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed. @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. <br>
        * __Note:__ @ref DOX_IADLXMultimediaChangedEvent_GetGPU returns the reference counted GPU used by all the methods in this interface to check if there are any changes in multimedia settings.
        @ENG_END_DOX
        *
        *
        *@copydoc IADLXMultimediaChangedEvent_REQ_TABLE
        *
        */
        virtual ADLX_RESULT     ADLX_STD_CALL GetGPU(IADLXGPU** ppGPU) = 0;

        /**
        *@page DOX_IADLXMultimediaChangedEvent_IsVideoUpscaleChanged IsVideoUpscaleChanged
        *@ENG_START_DOX @brief Checks for changes to the video upscale settings. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    IsVideoUpscaleChanged ()
        *@codeEnd
        *
        *@params
        *N/A
        *
        *@retvalues
        *@ENG_START_DOX  If there are any changes to the video upscale settings, __true__ is returned.<br>
        * If there are no changes to the video upscale settings, __false__ is returned.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        * __Note:__ To obtain the GPU, use @ref DOX_IADLXMultimediaChangedEvent_GetGPU.
        * @ENG_END_DOX
        * 
        *@copydoc IADLXMultimediaChangedEvent_REQ_TABLE
        *
        */
        virtual adlx_bool       ADLX_STD_CALL IsVideoUpscaleChanged() = 0;


        virtual adlx_bool       ADLX_STD_CALL IsVideoSuperResolutionChanged() = 0;

    }; //IADLXMultimediaChangedEvent
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXMultimediaChangedEvent> IADLXMultimediaChangedEventPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXMultimediaChangedEvent, L"IADLXMultimediaChangedEvent")
typedef struct IADLXMultimediaChangedEvent IADLXMultimediaChangedEvent;
typedef struct IADLXMultimediaChangedEventVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXMultimediaChangedEvent* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXMultimediaChangedEvent* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXMultimediaChangedEvent* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXChangedEvent
    ADLX_SYNC_ORIGIN(ADLX_STD_CALL* GetOrigin)(IADLXMultimediaChangedEvent* pThis);

    //IADLXMultimediaChangedEvent interface
    ADLX_RESULT(ADLX_STD_CALL* GetGPU)(IADLXMultimediaChangedEvent* pThis, IADLXGPU** ppGPU);
    adlx_bool (ADLX_STD_CALL* IsVideoUpscaleChanged)(IADLXMultimediaChangedEvent* pThis);
    adlx_bool (ADLX_STD_CALL* IsVideoSuperResolutionChanged)(IADLXMultimediaChangedEvent* pThis);

}IADLXMultimediaChangedEventVtbl;
struct IADLXMultimediaChangedEvent { const IADLXMultimediaChangedEventVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXMultimediaChangedEvent

#pragma region IADLXMultimediaChangedEventListener
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXMultimediaChangedEventListener
    {
    public:
        /**
        *@page DOX_IADLXMultimediaChangedEventListener_OnMultimediaChanged OnMultimediaChanged
        *@ENG_START_DOX @brief __OnMultimediaChanged__ is called by ADLX when multimedia settings change. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    OnMultimediaChanged (@ref DOX_IADLXMultimediaChangedEvent* pMultimediaChangedEvent)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pMultimediaChangedEvent,@ref DOX_IADLXMultimediaChangedEvent* ,@ENG_START_DOX The pointer to a multimedia settings change event. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the application requires ADLX to continue notifying the next listener, __true__ must be returned.<br>
        * If the application requires ADLX to stop notifying the next listener, __false__ must be returned.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX  Once the application registers to the notifications with @ref DOX_IADLXMultimediaChangedHandling_AddMultimediaEventListener, ADLX will call this method until the application unregisters from the notifications with @ref DOX_IADLXMultimediaChangedHandling_RemoveMultimediaEventListener.
        * The method should return quickly to not block the execution path in ADLX. If the method requires a long processing of the event notification, the application must hold onto a reference to the multimedia settings change event with @ref DOX_IADLXInterface_Acquire and make it available on an asynchronous thread and return immediately. When the asynchronous thread is done processing it must discard the multimedia settings change event with @ref DOX_IADLXInterface_Release. @ENG_END_DOX
        *
        *@copydoc IADLXMultimediaChangedEventListener_REQ_TABLE
        *
        */
        virtual adlx_bool ADLX_STD_CALL OnMultimediaChanged(IADLXMultimediaChangedEvent* pMultimediaChangedEvent) = 0;
    }; //IADLXMultimediaChangedEventListener
} //namespace adlx
#else //__cplusplus
typedef struct IADLXMultimediaChangedEventListener  IADLXMultimediaChangedEventListener;
typedef struct IADLXMultimediaChangedEventListenerVtbl
{
    adlx_bool(ADLX_STD_CALL* OnMultimediaChanged)(IADLXMultimediaChangedEventListener* pThis, IADLXMultimediaChangedEvent* pMultimediaChangedEvent);
} IADLXMultimediaChangedEventListenerVtbl;
struct IADLXMultimediaChangedEventListener { const IADLXMultimediaChangedEventListenerVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXMultimediaChangedEventListener

#pragma region IADLXMultimediaChangedHandling
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXMultimediaChangedHandling : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLXMultimediaChangedHandling")

        /**
        *@page DOX_IADLXMultimediaChangedHandling_AddMultimediaEventListener AddMultimediaEventListener
        *@ENG_START_DOX @brief Registers an event listener for notifications whenever multimedia settings are changed. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    AddMultimediaEventListener (@ref DOX_IADLXMultimediaChangedEventListener* pMultimediaChangedEventListener)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pMultimediaChangedEventListener,@ref DOX_IADLXMultimediaChangedEventListener* ,@ENG_START_DOX The pointer to the event listener interface to register for receiving multimedia settings change notifications. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the event listener is successfully registered, __ADLX_OK__ is returned.<br>
        * If the event listener is not successfully registered, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX  Once the event listener is successfully registered, ADLX calls the @ref DOX_IADLXMultimediaChangedEventListener_OnMultimediaChanged listener method whenever multimedia settings are changed.<br>
        * The event listener instance must exist until the application unregisters the event listener with @ref DOX_IADLXMultimediaChangedHandling_RemoveMultimediaEventListener.<br> @ENG_END_DOX
        *
        *@copydoc IADLXMultimediaChangedHandling_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL AddMultimediaEventListener(IADLXMultimediaChangedEventListener* pMultimediaChangedEventListener) = 0;

        /**
        *@page DOX_IADLXMultimediaChangedHandling_RemoveMultimediaEventListener RemoveMultimediaEventListener
        *@ENG_START_DOX @brief Unregisters an event listener from the multimedia settings event list. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    RemoveMultimediaEventListener (@ref DOX_IADLXMultimediaChangedEventListener* pMultimediaChangedEventListener)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pMultimediaChangedEventListener,@ref DOX_IADLXMultimediaChangedEventListener* ,@ENG_START_DOX The pointer to the event listener interface to unregister from receiving multimedia settings change notifications. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the event listener is successfully unregistered, __ADLX_OK__ is returned.<br>
        * If the event listener is not successfully unregistered, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX  Once the event listener is successfully unregistered, ADLX will no longer call the @ref DOX_IADLXMultimediaChangedEventListener_OnMultimediaChanged listener method when multimedia settings are changed.
        * The application can discard the event listener instance. @ENG_END_DOX
        *
        *@copydoc IADLXMultimediaChangedHandling_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL RemoveMultimediaEventListener(IADLXMultimediaChangedEventListener* pMultimediaChangedEventListener) = 0;
    }; //IADLXMultimediaChangedHandling
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXMultimediaChangedHandling> IADLXMultimediaChangedHandlingPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXMultimediaChangedHandling, L"IADLXMultimediaChangedHandling")
typedef struct IADLXMultimediaChangedHandling IADLXMultimediaChangedHandling;
typedef struct IADLXMultimediaChangedHandlingVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXMultimediaChangedHandling* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXMultimediaChangedHandling* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXMultimediaChangedHandling* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXMultimediaChangedHandling
    ADLX_RESULT(ADLX_STD_CALL* AddMultimediaEventListener)(IADLXMultimediaChangedHandling* pThis, IADLXMultimediaChangedEventListener* pMultimediaChangedEventListener);
    ADLX_RESULT(ADLX_STD_CALL* RemoveMultimediaEventListener)(IADLXMultimediaChangedHandling* pThis, IADLXMultimediaChangedEventListener* pMultimediaChangedEventListener);
} IADLXMultimediaChangedHandlingVtbl;
struct IADLXMultimediaChangedHandling { const IADLXMultimediaChangedHandlingVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXMultimediaChangedHandling

#pragma region IADLXMultimediaServices
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPU;

    class ADLX_NO_VTABLE IADLXMultimediaServices : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLXMultimediaServices")

            /**
            *@page DOX_IADLXMultimediaServices_GetMultimediaChangedHandling GetMultimediaChangedHandling
            *@ENG_START_DOX @brief Gets the reference counted interface that allows registering and unregistering for notifications when multimedia settings change. @ENG_END_DOX
            *
            *@syntax
            *@codeStart
            * @ref ADLX_RESULT    GetMultimediaChangedHandling (@ref DOX_IADLXMultimediaChangedHandling** ppMultimediaChangedHandling)
            *@codeEnd
            *
            *@params
            *@paramrow{1.,[out] ,ppMultimediaChangedHandling,@ref DOX_IADLXMultimediaChangedHandling** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppMultimediaChangedHandling__ to __nullptr__. @ENG_END_DOX}
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
            *@copydoc IADLXMultimediaServices_REQ_TABLE
            *
            */
            virtual ADLX_RESULT   ADLX_STD_CALL GetMultimediaChangedHandling(IADLXMultimediaChangedHandling** ppMultimediaChangedHandling) = 0;

            /**
            *@page DOX_IADLXMultimediaServices_GetVideoUpscale GetVideoUpscale
            *@ENG_START_DOX @brief Gets the reference-counted video upscale interface of a GPU. @ENG_END_DOX
            *
            *@syntax
            *@codeStart
            * @ref ADLX_RESULT    GetVideoUpscale (@ref DOX_IADLXGPU* pGPU, @ref DOX_IADLXVideoUpscale** ppVideoupscale)
            *@codeEnd
            *
            *@params
            *@paramrow{1.,[in] ,pGPU,@ref DOX_IADLXGPU* ,@ENG_START_DOX The pointer to the GPU interface. @ENG_END_DOX}
            *@paramrow{2.,[out] ,ppVideoupscale,@ref DOX_IADLXVideoUpscale** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppVideoupscale__ to __nullptr__. @ENG_END_DOX}
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
            *@copydoc IADLXMultimediaServices_REQ_TABLE
            *
            */
            virtual ADLX_RESULT   ADLX_STD_CALL GetVideoUpscale(IADLXGPU* pGPU, IADLXVideoUpscale** ppVideoupscale) = 0;


            virtual ADLX_RESULT   ADLX_STD_CALL GetVideoSuperResolution(IADLXGPU* pGPU, IADLXVideoSuperResolution** ppVideoSuperResolution) = 0;

    };  //IADLXPowerTuningServices
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXMultimediaServices> IADLXMultimediaServicesPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXMultimediaServices, L"IADLXMultimediaServices")
typedef struct IADLXMultimediaServices IADLXMultimediaServices;

typedef struct IADLXMultimediaServicesVtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXMultimediaServices* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXMultimediaServices* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXMultimediaServices* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXMultimediaServices
    ADLX_RESULT(ADLX_STD_CALL* GetMultimediaChangedHandling)(IADLXMultimediaServices* pThis, IADLXMultimediaChangedHandling** ppMultimediaChangedHandling);
    ADLX_RESULT(ADLX_STD_CALL* GetVideoUpscale)(IADLXMultimediaServices* pThis, IADLXGPU* pGPU, IADLXVideoUpscale** ppVideoUpscale);
    ADLX_RESULT(ADLX_STD_CALL* GetVideoSuperResolution)(IADLXMultimediaServices* pThis, IADLXGPU* pGPU, IADLXVideoSuperResolution** ppVideoSuperResolution);

}IADLXMultimediaServicesVtbl;

struct IADLXMultimediaServices { const IADLXMultimediaServicesVtbl *pVtbl; };

#endif //__cplusplus
#pragma endregion IADLXMultimediaServices


#endif //ADLX_MULTIMEDIA_H
