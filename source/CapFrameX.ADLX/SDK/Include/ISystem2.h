//
// Copyright (c) 2023 - 2025 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_ISYSTEM2_H
#define ADLX_ISYSTEM2_H
#pragma once

#include "ISystem1.h"
#include "ADLXStructures.h"

// Interfaces for GPU2 Info
#pragma region IADLXGPU2 interface
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPUConnectChangedListener;
    class ADLX_NO_VTABLE IADLXApplicationList;
    class ADLX_NO_VTABLE IADLXGPU2 : public IADLXGPU1
    {
    public:
        ADLX_DECLARE_IID(L"IADLXGPU2")

        /**
        * @page DOX_IADLXGPU2_IsPowerOff IsPowerOff
        * @ENG_START_DOX @brief Checks if a GPU is powered off. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    IsPowerOff (adlx_bool* state)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],state,adlx_bool*,@ENG_START_DOX The pointer to a variable where the GPU powered off state is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the GPU powered off state is successfully returned, __ADLX_OK__ is returned.<br>
        * If the GPU powered off state is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        * 
        * @detaileddesc
        * @ENG_START_DOX
        * If the method returns __ADLX_PENDING_OPERATION__, the GPU is busy executing another power related operation, and the call should be repeated.
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsPowerOff (adlx_bool* state) = 0;

        /**
        * @page DOX_IADLXGPU2_PowerOn PowerOn
        * @ENG_START_DOX @brief Powers on a GPU. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    PowerOn ()
        * @codeEnd
        *
        * @params
        * N/A
        * 
        * @retvalues
        * @ENG_START_DOX
        * If powers on the GPU successfully, __ADLX_OK__ is returned.<br>
        * If powers on the GPU unsuccessfully, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        * 
        * @detaileddesc
        * @ENG_START_DOX This method controls the power of the discrete GPU power by disabling the __SmartShift Eco__ feature when called, which can be re-enabled by calling @ref DOX_IADLXSmartShiftEco_SetEnabled.<br>
        * If the method returns __ADLX_PENDING_OPERATION__, the GPU is busy executing another power related operation, and the call should be repeated.
        *
        * @depifc
        * When the GPU is powered on, @ref DOX_IADLXSmartShiftEco "AMD SmartShift Eco" is automatically disabled. To return the power control of this GPU to the AMD driver, use @ref DOX_IADLXSmartShiftEco_SetEnabled.<br>
        * 
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL PowerOn () = 0;

        /**
        * @page DOX_IADLXGPU2_StartPowerOff StartPowerOff
        * @ENG_START_DOX @brief Powers off a GPU. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    StartPowerOff (@ref DOX_IADLXGPUConnectChangedListener* pGPUConnectChangedListener, adlx_int timeout)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[in],pGPUConnectChangedListener,@ref DOX_IADLXGPUConnectChangedListener*,@ENG_START_DOX The pointer to a GPU Connect change complete listener interface. @ENG_END_DOX}
        * @paramrow{2.,[in],timeout,adlx_int,@ENG_START_DOX The timeout for power off operation. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If starts to power off the GPU successfully, __ADLX_OK__ is returned.<br>
        * If starts to power off the GPU unsuccessfully, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX

        * @detaileddesc
        * @ENG_START_DOX  This method triggers an asynchronous execution for GPU power off and returns immediately.
        * When the GPUConnect settings change, ADLX calls @ref DOX_IADLXGPUConnectChangedListener_OnGPUConnectChanged in the AMD GPUConnect changed listener.
        *
        * After the event is raised, @ref DOX_IADLXGPUConnectChangedEvent_IsGPUPowerChanged returns __true__ for the power state change of a GPU.<br/>
        *
        * If it returns __false__, the @ref DOX_IADLXGPUConnectChangedEvent_IsGPUPowerChangeError returns the specific error.
        * This method controls the discrete GPU power, it disables the SmartShift Eco feature when called, using @ref DOX_IADLXSmartShiftEco_SetEnabled to return
        * the power control to AMD.<br>
        *
        * If the method returns __ADLX_PENDING_OPERATION__, the GPU is busy executing another power related operation, and the call should be repeated.<br/>
        *
        * @depifc
        * When the GPU is powered off, @ref DOX_IADLXSmartShiftEco "AMD SmartShift Eco" is automatically disabled. To return the power control of this GPU to the AMD driver, use @ref DOX_IADLXSmartShiftEco_SetEnabled.<br>
        * 
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL StartPowerOff (IADLXGPUConnectChangedListener* pGPUConnectChangedListener, adlx_int timeout) = 0;

        /**
        * @page DOX_IADLXGPU2_AbortPowerOff AbortPowerOff
        * @ENG_START_DOX @brief Aborts powering off a GPU. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    AbortPowerOff ()
        * @codeEnd
        *
        * @params
        * N/A
        * 
        * @retvalues
        * @ENG_START_DOX
        * If GPU power off was aborted successfully, __ADLX_OK__ is returned.<br>
        * If GPU power off was aborted unsuccessfully returned, an error code is returned.<br>
        * 
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL AbortPowerOff () = 0;

        /**
        * @page DOX_IADLXGPU2_IsSupportedApplicationList IsSupportedApplicationList
        * @ENG_START_DOX @brief Checks if reporting the list of applications running on a GPU is supported. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    IsSupportedApplicationList (adlx_bool* supported)
        * @codeEnd
        * 
        * @params
        * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the Application list support status is returned. The variable is __true__ if Integer Display Scaling is supported. The variable is __false__ if Integer Display Scaling is not supported. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the Application list support status is successfully returned, __ADLX_OK__ is returned.<br>
        * If the Application list support status is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsSupportedApplicationList (adlx_bool* supported) = 0;

        /**
        * @page DOX_IADLXGPU2_GetApplications GetApplications
        * @ENG_START_DOX @brief Gets the reference counted list of applications that run in the context of a GPU. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    GetApplications (@ref DOX_IADLXApplicationList** ppApplications)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],ppApplications,@ref DOX_IADLXApplicationList**,@ENG_START_DOX The pointer to a variable where the application list is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the application list is successfully returned, __ADLX_OK__ is returned.<br>
        * If the application list is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetApplications (IADLXApplicationList** ppApplications) = 0;

        /**
        * @page DOX_IADLXGPU2_AMDSoftwareReleaseDate AMDSoftwareReleaseDate
        * @ENG_START_DOX
        * @brief Gets the AMD software release date of a GPU.
        * @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    AMDSoftwareReleaseDate(adlx_uint* year, adlx_uint* month, adlx_uint* day)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],year,adlx_uint*,@ENG_START_DOX Year of the AMD software release date. @ENG_END_DOX}
        * @paramrow{2.,[out],month,adlx_uint*,@ENG_START_DOX Month of the AMD software release date. @ENG_END_DOX}
        * @paramrow{3.,[out],day,adlx_uint*,@ENG_START_DOX Day of the AMD software release date. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the AMD software release date is successfully returned, __ADLX_OK__ is returned.<br>
        * If the AMD software release date is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL AMDSoftwareReleaseDate(adlx_uint* year, adlx_uint* month, adlx_uint* day) = 0;

        /**
        * @page DOX_IADLXGPU2_AMDSoftwareEdition AMDSoftwareEdition
        * @ENG_START_DOX
        * @brief Gets the AMD software edition of a GPU.
        * @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    AMDSoftwareEdition(const char** edition)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],edition,const char**,@ENG_START_DOX The pointer to a zero-terminated string where the AMD software edition is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the AMD software edition is successfully returned, __ADLX_OK__ is returned.<br>
        * If the AMD software edition is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * The returned memory buffer is valid within the lifetime of the @ref DOX_IADLXGPU2 interface.<br>
        * If the application uses the AMD software edition beyond the lifetime of the @ref DOX_IADLXGPU2 interface, the application must make a copy of the AMD software edition.
        * @ENG_END_DOX
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL AMDSoftwareEdition(const char** edition) = 0;

        /**
        * @page DOX_IADLXGPU2_AMDSoftwareVersion AMDSoftwareVersion
        * @ENG_START_DOX
        * @brief Gets the AMD software version of a GPU.
        * @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    AMDSoftwareVersion(const char** version)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],version,const char**,@ENG_START_DOX The pointer to a zero-terminated string where the AMD software version is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the AMD software version is successfully returned, __ADLX_OK__ is returned.<br>
        * If the AMD software version is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * The returned memory buffer is valid within the lifetime of the @ref DOX_IADLXGPU2 interface.<br>
        * If the application uses the AMD software version beyond the lifetime of the @ref DOX_IADLXGPU2 interface, the application must make a copy of the AMD software version.
        * @ENG_END_DOX
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL AMDSoftwareVersion(const char** version) = 0;

        /**
        * @page DOX_IADLXGPU2_DriverVersion DriverVersion
        * @ENG_START_DOX
        * @brief Gets the driver version of a GPU.
        * @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    DriverVersion(const char** version)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],version,const char**,@ENG_START_DOX The pointer to a zero-terminated string where the driver version is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the driver version is successfully returned, __ADLX_OK__ is returned.<br>
        * If the driver version is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * The returned memory buffer is valid within the lifetime of the @ref DOX_IADLXGPU2 interface.<br>
        * If the application uses the driver version beyond the lifetime of the @ref DOX_IADLXGPU2 interface, the application must make a copy of the driver version.
        * @ENG_END_DOX
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL DriverVersion(const char** version) = 0;

        /**
        * @page DOX_IADLXGPU2_AMDWindowsDriverVersion AMDWindowsDriverVersion
        * @ENG_START_DOX
        * @brief Gets the AMD Windows driver version of a GPU.
        * @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    AMDWindowsDriverVersion(const char** version)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],version,const char**,@ENG_START_DOX The pointer to a zero-terminated string where the AMD Windows driver version is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the AMD Windows driver version is successfully returned, __ADLX_OK__ is returned.<br>
        * If the AMD Windows driver version is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * The returned memory buffer is valid within the lifetime of the @ref DOX_IADLXGPU2 interface.<br>
        * If the application uses the AMD windows driver version beyond the lifetime of the @ref DOX_IADLXGPU2 interface, the application must make a copy of the AMD windows driver version.
        * @ENG_END_DOX
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL AMDWindowsDriverVersion(const char** version) = 0;

        /**
        * @page DOX_IADLXGPU2_LUID LUID
        * @ENG_START_DOX
        * @brief Gets the local identifier information of a GPU.
        * @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    LUID (@ref ADLX_LUID* luid)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],luid,@ref ADLX_LUID*,@ENG_START_DOX The pointer to a variable where the local identifier information of the GPU is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the local identifier information is successfully returned, __ADLX_OK__ is returned.<br>
        * If the local identifier information is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL LUID(ADLX_LUID* luid) = 0;

    }; //IADLXGPU2
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXGPU2> IADLXGPU2Ptr;
}   //namespace adlx
#else
ADLX_DECLARE_IID(IADLXGPU2, L"IADLXGPU2");
typedef struct IADLXGPU2 IADLXGPU2;
typedef struct IADLXGPUConnectChangedListener IADLXGPUConnectChangedListener;
typedef struct IADLXApplicationList IADLXApplicationList;
typedef struct IADLXGPU2Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXGPU2* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXGPU2* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXGPU2* pThis, const wchar_t* interfaceId, void** ppInterface);
    //IADLXGPU
    ADLX_RESULT(ADLX_STD_CALL* VendorId)(IADLXGPU2* pThis, const char** vendorId);
    ADLX_RESULT(ADLX_STD_CALL* ASICFamilyType)(IADLXGPU2* pThis, ADLX_ASIC_FAMILY_TYPE* asicFamilyType);
    ADLX_RESULT(ADLX_STD_CALL* Type)(IADLXGPU2* pThis, ADLX_GPU_TYPE* gpuType);
    ADLX_RESULT(ADLX_STD_CALL* IsExternal)(IADLXGPU2* pThis, adlx_bool* isExternal);
    ADLX_RESULT(ADLX_STD_CALL* Name)(IADLXGPU2* pThis, const char** gpuName);
    ADLX_RESULT(ADLX_STD_CALL* DriverPath)(IADLXGPU2* pThis, const char** driverPath);
    ADLX_RESULT(ADLX_STD_CALL* PNPString)(IADLXGPU2* pThis, const char** pnpString);
    ADLX_RESULT(ADLX_STD_CALL* HasDesktops)(IADLXGPU2* pThis, adlx_bool* hasDesktops);
    ADLX_RESULT(ADLX_STD_CALL* TotalVRAM)(IADLXGPU2* pThis, adlx_uint* vramMB);
    ADLX_RESULT(ADLX_STD_CALL* VRAMType)(IADLXGPU2* pThis, const char** type);
    ADLX_RESULT(ADLX_STD_CALL* BIOSInfo)(IADLXGPU2* pThis, const char** partNumber, const char** version, const char** date);
    ADLX_RESULT(ADLX_STD_CALL* DeviceId)(IADLXGPU2* pThis, const char** deviceId);
    ADLX_RESULT(ADLX_STD_CALL* RevisionId)(IADLXGPU2* pThis, const char** revisionId);
    ADLX_RESULT(ADLX_STD_CALL* SubSystemId)(IADLXGPU2* pThis, const char** subSystemId);
    ADLX_RESULT(ADLX_STD_CALL* SubSystemVendorId)(IADLXGPU2* pThis, const char** subSystemVendorId);
    ADLX_RESULT(ADLX_STD_CALL* UniqueId)(IADLXGPU2* pThis, adlx_int* uniqueId);
    //IADLXGPU1
    ADLX_RESULT(ADLX_STD_CALL* PCIBusType)(IADLXGPU2* pThis, ADLX_PCI_BUS_TYPE* busType);
    ADLX_RESULT(ADLX_STD_CALL* PCIBusLaneWidth)(IADLXGPU2* pThis, adlx_uint* laneWidth);
    ADLX_RESULT(ADLX_STD_CALL* MultiGPUMode)(IADLXGPU2* pThis, ADLX_MGPU_MODE* mode);
    ADLX_RESULT(ADLX_STD_CALL* ProductName)(IADLXGPU2* pThis, const char** productName);
    //IADLXGPU2
    ADLX_RESULT(ADLX_STD_CALL* IsPowerOff)(IADLXGPU2* pThis, adlx_bool* state);
    ADLX_RESULT(ADLX_STD_CALL* PowerOn)(IADLXGPU2* pThis);
    ADLX_RESULT(ADLX_STD_CALL* StartPowerOff)(IADLXGPU2* pThis, IADLXGPUConnectChangedListener* pGPUConnectChangedListener, adlx_int timeout);
    ADLX_RESULT(ADLX_STD_CALL* AbortPowerOff)(IADLXGPU2* pThis);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedApplicationList)(IADLXGPU2* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetApplications)(IADLXGPU2* pThis, IADLXApplicationList** ppApplications);
    ADLX_RESULT(ADLX_STD_CALL* AMDSoftwareReleaseDate)(IADLXGPU2* pThis, adlx_uint* year, adlx_uint* month, adlx_uint* day);
    ADLX_RESULT(ADLX_STD_CALL* AMDSoftwareEdition)(IADLXGPU2* pThis, const char** edition);
    ADLX_RESULT(ADLX_STD_CALL* AMDSoftwareVersion)(IADLXGPU2* pThis, const char** version);
    ADLX_RESULT(ADLX_STD_CALL* DriverVersion)(IADLXGPU2* pThis, const char** version);
    ADLX_RESULT(ADLX_STD_CALL* AMDWindowsDriverVersion)(IADLXGPU2* pThis, const char** version);
    ADLX_RESULT(ADLX_STD_CALL* LUID)(IADLXGPU2* pThis, ADLX_LUID* luid);
} IADLXGPU2Vtbl;

struct IADLXGPU2 { const IADLXGPU2Vtbl* pVtbl; };
#endif
#pragma endregion IADLXGPU2 interface

#pragma region IADLXGPU2List interface
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPU2List : public IADLXList
    {
    public:
        ADLX_DECLARE_IID (L"IADLXGPU2List")
        //Lists must declare the type of items it holds - what was passed as ADLX_DECLARE_IID() in that interface
        ADLX_DECLARE_ITEM_IID (IADLXGPU2::IID ())

        /**
        * @page DOX_IADLXGPU2List_At At
        * @ENG_START_DOX
        * @brief Returns the reference counted interface at the requested location.
        * @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    At (const adlx_uint location, @ref DOX_IADLXGPU2** ppItem)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[in] ,location,const adlx_uint ,@ENG_START_DOX The location of the requested interface.  @ENG_END_DOX}
        * @paramrow{2.,[out] ,ppItem,@ref DOX_IADLXGPU2** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned then the method sets the dereferenced address __*ppItem__ to __nullptr__.  @ENG_END_DOX}
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
        * @copydoc IADLXGPU2List_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL At (const adlx_uint location, IADLXGPU2** ppItem) = 0;
        /**
         * @page DOX_IADLXGPU2List_Add_Back Add_Back
         * @ENG_START_DOX
         * @brief Adds an interface to the end of a list.
         * @ENG_END_DOX
         * @syntax
         * @codeStart
         *  @ref ADLX_RESULT    Add_Back (@ref DOX_IADLXGPU2* pItem)
         * @codeEnd
         *
         * @params
         * @paramrow{1.,[in] ,pItem,@ref DOX_IADLXGPU2* ,@ENG_START_DOX The pointer to the interface to be added to the list.  @ENG_END_DOX}
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
         * @copydoc IADLXGPU2List_REQ_TABLE
         *
         */
        virtual ADLX_RESULT ADLX_STD_CALL Add_Back (IADLXGPU2* pItem) = 0;
    };  //IADLXGPU2List
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXGPU2List> IADLXGPU2ListPtr;
}   //namespace adlx
#else
ADLX_DECLARE_IID (IADLXGPU2List, L"IADLXGPU2List")
ADLX_DECLARE_ITEM_IID (IADLXGPU2, IID_IADLXGPU2 ())

typedef struct IADLXGPU2List IADLXGPU2List;
typedef struct IADLXGPU2ListVtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXGPU2List* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXGPU2List* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXGPU2List* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXList
    adlx_uint (ADLX_STD_CALL *Size)(IADLXGPU2List* pThis);
    adlx_uint8 (ADLX_STD_CALL *Empty)(IADLXGPU2List* pThis);
    adlx_uint (ADLX_STD_CALL *Begin)(IADLXGPU2List* pThis);
    adlx_uint (ADLX_STD_CALL *End)(IADLXGPU2List* pThis);
    ADLX_RESULT (ADLX_STD_CALL *At)(IADLXGPU2List* pThis, const adlx_uint location, IADLXInterface** ppItem);
    ADLX_RESULT (ADLX_STD_CALL *Clear)(IADLXGPU2List* pThis);
    ADLX_RESULT (ADLX_STD_CALL *Remove_Back)(IADLXGPU2List* pThis);
    ADLX_RESULT (ADLX_STD_CALL *Add_Back)(IADLXGPU2List* pThis, IADLXInterface* pItem);

    //IADLXGPU2List
    ADLX_RESULT (ADLX_STD_CALL *At_GPU2List)(IADLXGPU2List* pThis, const adlx_uint location, IADLXGPU2** ppItem);
    ADLX_RESULT (ADLX_STD_CALL *Add_Back_GPU2List)(IADLXGPU2List* pThis, IADLXGPU2* pItem);

} IADLXGPU2ListVtbl;

struct IADLXGPU2List { const IADLXGPU2ListVtbl *pVtbl; };

#endif
#pragma endregion IADLXGPU2List interface

#pragma region IADLXSystem2 interface
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXMultimediaServices;
    class ADLX_NO_VTABLE IADLXGPUAppsListChangedHandling;
    class ADLX_NO_VTABLE IADLXSystem2 : public IADLXSystem1
    {
    public:
        ADLX_DECLARE_IID (L"IADLXSystem2")

        /**
        * @page DOX_IADLXSystem2_GetMultimediaServices GetMultimediaServices
        * @ENG_START_DOX
        * @brief Gets the reference counted main interface to the @ref DOX_IADLXMultimediaServices "Multimedia" domain.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    GetMultimediaServices (@ref DOX_IADLXMultimediaServices** ppMultiMediaServices)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out] ,ppMultiMediaServices,@ref DOX_IADLXMultimediaServices**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address  __*ppMultiMediaServices__ to __nullptr__. @ENG_END_DOX}
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
        * @ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. @ENG_END_DOX
        *
        * @copydoc IADLXSystem2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetMultimediaServices(IADLXMultimediaServices** ppMultiMediaServices) = 0;

        /**
        * @page DOX_IADLXSystem2_GetGPUAppsListChangedHandling GetGPUAppsListChangedHandling
        * @ENG_START_DOX
        * @brief Gets the reference counted interface that allows registering and unregistering for notifications when the list of applications running on a GPU changes.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        * @ref ADLX_RESULT    GetGPUAppsListChangedHandling(@ref DOX_IADLXGPUAppsListChangedHandling** ppGPUAppsListChangedHandling)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out] ,ppGPUAppsListChangedHandling,@ref DOX_IADLXGPUAppsListChangedHandling**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address  __*ppGPUAppsListChangedHandling__ to __nullptr__. @ENG_END_DOX}
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
        * @ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. @ENG_END_DOX
        *
        * @copydoc IADLXSystem2_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetGPUAppsListChangedHandling(IADLXGPUAppsListChangedHandling** ppGPUAppsListChangedHandling) = 0;
    };  //IADLXSystem2
    typedef IADLXInterfacePtr_T<IADLXSystem2> IADLXSystem2Ptr;
}   //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID (IADLXSystem2, L"IADLXSystem2")

typedef struct IADLXMultimediaServices IADLXMultimediaServices;
typedef struct IADLXGPUAppsListChangedHandling IADLXGPUAppsListChangedHandling;
typedef struct IADLXSystem2 IADLXSystem2;
typedef struct IADLXSystem2Vtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXSystem2* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXSystem2* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXSystem2* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXSystem1
    ADLX_RESULT (ADLX_STD_CALL *GetPowerTuningServices)(IADLXSystem2* pThis, IADLXPowerTuningServices** ppPowerTuningServices);

    //IADLXSystem2
    ADLX_RESULT (ADLX_STD_CALL *GetMultimediaServices)(IADLXSystem2* pThis, IADLXMultimediaServices** ppMultiMediaServices);
    ADLX_RESULT (ADLX_STD_CALL *GetGPUAppsListChangedHandling)(IADLXSystem2* pThis, IADLXGPUAppsListChangedHandling** ppGPUAppsListChangedHandling);
} IADLXSystem2Vtbl;

struct IADLXSystem2 { const IADLXSystem2Vtbl*pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXSystem2 interface

#endif  //ADLX_ISYSTEM2_H