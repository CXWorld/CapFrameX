// 
// Copyright (c) 2023 - 2025 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_IAPPLICATIONS_H
#define ADLX_IAPPLICATIONS_H
#pragma once

#include "ADLXStructures.h"
#include "ICollections.h"

//-------------------------------------------------------------------------------------------------
//IDesktops.h - Interfaces for ADLX Applications functionality

#pragma region IADLXApplication
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXApplication : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLXApplication")

        /**
        * @page DOX_IADLXApplication_ProcessID ProcessID
        * @ENG_START_DOX
        * @brief Gets the process ID for an application.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    ProcessID (adlx_ulong* pid)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out] ,pid,adlx_ulong* ,@ENG_START_DOX The pointer to a variable where the process ID for an application is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the process ID is successfully returned, __ADLX_OK__ is returned.<br>
        * If the process ID is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXApplication_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL ProcessID(adlx_ulong* pid) = 0;

        /**
        * @page DOX_IADLXApplication_Name Name
        * @ENG_START_DOX
        * @brief Gets the name for an application.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    Name (const wchar_t** ppAppName)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out] ,ppAppName,const wchar_t** ,@ENG_START_DOX The pointer to a zero-terminated string where the name of the application is returned the returned\, memory buffer is valid within a lifetime of the IADLXApplication interface. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the name is successfully returned, __ADLX_OK__ is returned.<br>
        * If the name is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXApplication_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL Name(const wchar_t** ppAppName) = 0;

        /**
        * @page DOX_IADLXApplication_FullPath FullPath
        * @ENG_START_DOX
        * @brief Gets the full-qualified path for an application.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    FullPath (const wchar_t** ppAppPath)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out] ,ppAppName,const wchar_t** ,@ENG_START_DOX The pointer to a zero-terminated string where the full path of the application is returned the returned\, memory buffer is valid within a lifetime of the IADLXApplication interface. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the full path is successfully returned, __ADLX_OK__ is returned.<br>
        * If the full path is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXApplication_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL FullPath(const wchar_t** ppAppPath) = 0;

        /**
        * @page DOX_IADLXApplication_GPUDependencyType GPUDependencyType
        * @ENG_START_DOX
        * @brief Gets the GPU dependency type for an application.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    GPUDependencyType (@ref ADLX_APP_GPU_DEPENDENCY* gpuDependency)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out] ,gpuDependency,ADLX_APP_GPU_DEPENDENCY* ,@ENG_START_DOX The pointer to a variable where the GPU dependency type for an application is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the GPU dependency type is successfully returned, __ADLX_OK__ is returned.<br>
        * If the GPU dependency type is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXApplication_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GPUDependencyType(ADLX_APP_GPU_DEPENDENCY* gpuDependency) = 0;
    }; // IADLXApplication 
    //------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXApplication> IADLXApplicationPtr;
} // namespace adlx
#else // __cplusplus
ADLX_DECLARE_IID(IADLXApplication, L"IADLXApplication")
typedef struct IADLXApplication IADLXApplication;
typedef struct IADLXApplicationVtbl
{
    // IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXApplication* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXApplication* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXApplication* pThis, const wchar_t* interfaceId, void** ppInterface);

    // IADLXApplication
    ADLX_RESULT(ADLX_STD_CALL* ProcessID)(IADLXApplication* pThis, adlx_ulong* pid);
    ADLX_RESULT(ADLX_STD_CALL* Name)(IADLXApplication* pThis, const wchar_t** ppAppName);
    ADLX_RESULT(ADLX_STD_CALL* FullPath)(IADLXApplication* pThis, const wchar_t** ppAppPath);
    ADLX_RESULT(ADLX_STD_CALL* GPUDependencyType)(IADLXApplication* pThis, ADLX_APP_GPU_DEPENDENCY* gpuDependency);
} IADLXApplicationVtbl;
struct IADLXApplication { const IADLXApplicationVtbl* pVtbl; };
#endif
#pragma endregion IADLXApplication

#pragma region IADLXApplicationList
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXApplicationList : public IADLXList
    {
    public:
        ADLX_DECLARE_IID(L"IADLXApplicationList")

        //Lists must declare the type of items it holds - what was passed as ADLX_DECLARE_IID() in that interface
        ADLX_DECLARE_ITEM_IID(IADLXApplication::IID())

        /**
        * @page DOX_IADLXApplicationList_At At
        * @ENG_START_DOX
        * @brief Returns the reference counted interface at the requested location.
        * @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    At (const adlx_uint location, IADLXApplication** ppItem)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[in] ,location,const adlx_uint ,@ENG_START_DOX The location of the requested interface.  @ENG_END_DOX}
        * @paramrow{2.,[out] ,ppItem,@ref DOX_IADLXApplication** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned then the method sets the dereferenced address __*ppItem__ to __nullptr__.  @ENG_END_DOX}
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
        * @copydoc IADLXApplicationList_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL At(const adlx_uint location, IADLXApplication** ppItem) = 0;

        /**
         * @page DOX_IADLXApplicationList_Add_Back Add_Back
         * @ENG_START_DOX
         * @brief Adds an interface to the end of a list.
         * @ENG_END_DOX
         * @syntax
         * @codeStart
         *  @ref ADLX_RESULT    Add_Back (IADLXApplication* pItem)
         * @codeEnd
         *
         * @params
         * @paramrow{1.,[in] ,pItem,@ref DOX_IADLXApplication* ,@ENG_START_DOX The pointer to the interface to be added to the list.  @ENG_END_DOX}
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
         * @details
         * @ENG_END_DOX
         *
         *
         * @copydoc IADLXApplicationList_REQ_TABLE
         *
         */
        virtual ADLX_RESULT ADLX_STD_CALL Add_Back(IADLXApplication* pItem) = 0;
    };  //IADLXApplicationList
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXApplicationList> IADLXApplicationListPtr;
}   //namespace adlx
#else // __cplusplus
ADLX_DECLARE_IID(IADLXApplicationList, L"IADLXApplicationList")
ADLX_DECLARE_ITEM_IID(IADLXApplication, IID_IADLXApplication())

typedef struct IADLXApplicationList IADLXApplicationList;
typedef struct IADLXApplicationListVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXApplicationList* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXApplicationList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXApplicationList* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXList
    adlx_uint(ADLX_STD_CALL* Size)(IADLXApplicationList* pThis);
    adlx_uint8(ADLX_STD_CALL* Empty)(IADLXApplicationList* pThis);
    adlx_uint(ADLX_STD_CALL* Begin)(IADLXApplicationList* pThis);
    adlx_uint(ADLX_STD_CALL* End)(IADLXApplicationList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* At)(IADLXApplicationList* pThis, const adlx_uint location, IADLXInterface** ppItem);
    ADLX_RESULT(ADLX_STD_CALL* Clear)(IADLXApplicationList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* Remove_Back)(IADLXApplicationList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* Add_Back)(IADLXApplicationList* pThis, IADLXInterface* pItem);

    //IADLXApplicationList
    ADLX_RESULT(ADLX_STD_CALL* At_ApplicationList)(IADLXApplicationList* pThis, const adlx_uint location, IADLXApplication** ppItem);
    ADLX_RESULT(ADLX_STD_CALL* Add_Back_ApplicationList)(IADLXApplicationList* pThis, IADLXApplication* pItem);

} IADLXApplicationListVtbl;

struct IADLXApplicationList { const IADLXApplicationListVtbl* pVtbl; };
#endif
#pragma endregion IADLXApplicationList

#pragma region IADLXGPUAppsListEventListener
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPU2;
    class ADLX_NO_VTABLE IADLXGPUAppsListEventListener
    {
    public:
        /**
        *@page DOX_IADLXGPUAppsListEventListener_OnGPUAppsListChanged OnGPUAppsListChanged
        *@ENG_START_DOX @brief __OnGPUAppsListChanged__ is called by ADLX to provide an updated list of GPU applications. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * adlx_bool    OnGPUAppsListChanged (@ref DOX_IADLXGPU2* pGPU, @ref DOX_IADLXApplicationList* pApplications)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,pGPU,@ref DOX_IADLXGPU2* ,@ENG_START_DOX The pointer to a GPU that is changed. @ENG_END_DOX}
        *@paramrow{2.,[out] ,pApplications,@ref DOX_IADLXApplicationList* ,@ENG_START_DOX The pointer to a list of applications that is changed. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the application requires ADLX to continue notifying the next listener, __true__ must be returned.<br>
        * If the application requires ADLX to stop notifying the next listener, __false__ must be returned.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX  Once the application registers to the notifications with @ref DOX_IADLXGPUAppsListChangedHandling_AddGPUAppsListEventListener,
        * ADLX will call this method until the application unregisters from the notifications with @ref DOX_IADLXGPUAppsListChangedHandling_RemoveGPUAppsListEventListener.
        * The method should return quickly to not block the execution path in ADLX. If the method requires a long processing of the event notification, the application
        * must hold onto a reference to the new GPU application list with @ref DOX_IADLXInterface_Acquire and make it available on an asynchronous thread and return immediately.
        * When the asynchronous thread is done processing, it must discard the new GPU application list with @ref DOX_IADLXInterface_Release. @ENG_END_DOX
        *
        *@copydoc IADLXGPUAppsListEventListener_REQ_TABLE
        *
        */
        virtual adlx_bool ADLX_STD_CALL OnGPUAppsListChanged (IADLXGPU2* pGPU, IADLXApplicationList* pApplications) = 0;
    }; //IADLXGPUAppsListEventListener
} //namespace adlx
#else //__cplusplus
typedef struct IADLXGPUAppsListEventListener IADLXGPUAppsListEventListener;
typedef struct IADLXGPUAppsListEventListenerVtbl
{
    adlx_bool (ADLX_STD_CALL* OnGPUAppsListChanged)(IADLXGPUAppsListEventListener* pThis, IADLXGPU2* pGPU, IADLXApplicationList* pApplications);
} IADLXGPUAppsListEventListenerVtbl;
struct IADLXGPUAppsListEventListener { const IADLXGPUAppsListEventListenerVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXGPUAppsListEventListener

#pragma region IADLXGPUAppsListChangedHandling
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPUAppsListChangedHandling : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLXGPUAppsListChangedHandling")

        /**
        *@page DOX_IADLXGPUAppsListChangedHandling_AddGPUAppsListEventListener AddGPUAppsListEventListener
        *@ENG_START_DOX @brief Registers an event listener for notifications when the list of applications running on a GPU changes. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    AddGPUAppsListEventListener (@ref DOX_IADLXGPUAppsListEventListener* pGPUAppsListEventListener)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pGPUAppsListEventListener,@ref DOX_IADLXGPUAppsListEventListener* ,@ENG_START_DOX The pointer to the event listener interface to register for receiving list of applications running on a GPU change notifications. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the event listener is successfully registered, __ADLX_OK__ is returned.<br>
        * If the event listener is not successfully registered, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX After the event listener is successfully registered, ADLX calls the @ref DOX_IADLXGPUAppsListEventListener_OnGPUAppsListChanged method of the listener when power tuning settings change.<br>
        * The event listener instance must exist until the application unregisters the event listener with @ref DOX_IADLXGPUAppsListChangedHandling_RemoveGPUAppsListEventListener.<br> @ENG_END_DOX
        *
        *@copydoc IADLXGPUAppsListChangedHandling_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL AddGPUAppsListEventListener (IADLXGPUAppsListEventListener* pGPUAppsListEventListener) = 0;

        /**
        *@page DOX_IADLXGPUAppsListChangedHandling_RemoveGPUAppsListEventListener RemoveGPUAppsListEventListener
        *@ENG_START_DOX @brief Unregisters an event listener from notifications when the list of applications running on a GPU changes. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    RemoveGPUAppsListEventListener (@ref DOX_IADLXGPUAppsListEventListener* pGPUAppsListEventListener)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pGPUAppsListEventListener,@ref DOX_IADLXGPUAppsListEventListener* ,@ENG_START_DOX The pointer to the event listener interface to unregister from receiving list of applications running on a GPU change notifications. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the event listener is successfully unregistered, __ADLX_OK__ is returned.<br>
        * If the event listener is not successfully unregistered, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX After the event listener is successfully unregistered, ADLX will no longer call @ref DOX_IADLXGPUAppsListEventListener_OnGPUAppsListChanged method of the listener when Power Tuning settings change.
        * The application can discard the event listener instance. @ENG_END_DOX
        *
        *@copydoc IADLXGPUAppsListChangedHandling_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL RemoveGPUAppsListEventListener (IADLXGPUAppsListEventListener* pGPUAppsListEventListener) = 0;

    }; //IADLXGPUAppsListChangedHandling
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXGPUAppsListChangedHandling> IADLXGPUAppsListChangedHandlingPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXGPUAppsListChangedHandling, L"IADLXGPUAppsListChangedHandling")
typedef struct IADLXGPUAppsListChangedHandling IADLXGPUAppsListChangedHandling;
typedef struct IADLXGPUAppsListChangedHandlingVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXGPUAppsListChangedHandling* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXGPUAppsListChangedHandling* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXGPUAppsListChangedHandling* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXGPUAppsListChangedHandling
    ADLX_RESULT(ADLX_STD_CALL* AddGPUAppsListEventListener)(IADLXGPUAppsListChangedHandling* pThis, IADLXGPUAppsListEventListener* pGPUAppsListEventListener);
    ADLX_RESULT(ADLX_STD_CALL* RemoveGPUAppsListEventListener)(IADLXGPUAppsListChangedHandling* pThis, IADLXGPUAppsListEventListener* pGPUAppsListEventListener);
} IADLXGPUAppsListChangedHandlingVtbl;
struct IADLXGPUAppsListChangedHandling { const IADLXGPUAppsListChangedHandlingVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXGPUAppsListChangedHandling

#endif