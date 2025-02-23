//
// Copyright (c) 2021 - 2024 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_ISYSTEM1_H
#define ADLX_ISYSTEM1_H
#pragma once

#include "ISystem.h"

// Interfaces for GPU1 Info
#pragma region IADLXGPU1 interface
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPU1 : public IADLXGPU
    {
    public:
        ADLX_DECLARE_IID(L"IADLXGPU1")

        /**
        * @page DOX_IADLXGPU1_PCIBusType PCIBusType
        * @ENG_START_DOX @brief Gets the PCI bus type of a GPU. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    PCIBusType (@ref ADLX_PCI_BUS_TYPE* busType)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],busType,@ref ADLX_PCI_BUS_TYPE*,@ENG_START_DOX The pointer to a variable where the GPU PCI bus type is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the GPU PCI bus type is successfully returned, __ADLX_OK__ is returned.<br>
        * If the GPU PCI bus type is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL PCIBusType(ADLX_PCI_BUS_TYPE* busType) const = 0;

        /**
        * @page DOX_IADLXGPU1_PCIBusLaneWidth PCIBusLaneWidth
        * @ENG_START_DOX @brief Gets the PCI bus lane width of a GPU. @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    PCIBusLaneWidth (adlx_uint* laneWidth)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out] ,laneWidth,adlx_uint* ,@ENG_START_DOX The pointer to a variable where the PCI bus lane width is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX  If the PCI bus lane width is successfully returned, __ADLX_OK__ is returned.<br>
        * If the PCI bus lane width is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        * @copydoc IADLXGPU1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL PCIBusLaneWidth(adlx_uint* laneWidth) const = 0;

        /**
         * @page DOX_IADLXGPU1_MultiGPUMode MultiGPUMode
         * @ENG_START_DOX @brief Gets the AMD MGPU mode of a GPU. @ENG_END_DOX
         * @syntax
         * @codeStart
         *  @ref ADLX_RESULT    MultiGPUMode (@ref ADLX_MGPU_MODE* mode)
         * @codeEnd
         *
         * @params
         * @paramrow{1.,[out],mode,ADLX_MGPU_MODE*,@ENG_START_DOX The pointer to a variable where the AMD MGPU mode is returned. The variable is __MGPU_NONE__ if the GPU is not part of an AMD MGPU configuration. The variable is __MGPU_PRIMARY__ if the GPU is the primary GPU in an AMD MGPU configuration. The variable is __MGPU_SECONDARY__ if the GPU is the secondary GPU in an AMD MGPU configuration. @ENG_END_DOX}
         *
         * @retvalues
         * @ENG_START_DOX
         * If __MultiGPUMode__ is successfully returned, __ADLX_OK__ is returned.<br>
         * If __MultiGPUMode__ is not successfully returned, an error code is returned.<br>
         * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
         * @ENG_END_DOX
         *
         * @addinfo
         * @ENG_START_DOX
         * AMD MGPU technology harnesses the power of two or more discrete graphics cards working in parallel to dramatically improve performance in games and applications.<br>
         * On systems with AMD MGPU enabled the video output is delivered through the primary GPU and the workload is allocated to all supported GPUs in the setup.<br>
         * @ENG_END_DOX
         * 
         * @copydoc IADLXGPU1_REQ_TABLE
         *
         */
        virtual ADLX_RESULT ADLX_STD_CALL MultiGPUMode(ADLX_MGPU_MODE* mode) = 0;

        /**
         * @page DOX_IADLXGPU1_ProductName ProductName
         * @ENG_START_DOX @brief Gets the product name of a GPU. @ENG_END_DOX
         * @syntax
         * @codeStart
         *  @ref ADLX_RESULT    ProductName (const char** productName)
         * @codeEnd
         *
         * @params
         * @paramrow{1.,[out],productName,const char**,@ENG_START_DOX The pointer to a zero-terminated string where the productName string of a GPU is returned. @ENG_END_DOX}
         *
         * @retvalues
         * @ENG_START_DOX
         * If the productName string is successfully returned, __ADLX_OK__ is returned.<br>
         * If the productName string is not successfully returned, an error code is returned.<br>
         * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
         * @ENG_END_DOX
         *
         * @addinfo
         * @ENG_START_DOX
         * The returned memory buffer is valid within a lifetime of the @ref DOX_IADLXGPU1 interface.<br>
         * If the application uses the productName string beyond the lifetime of the @ref DOX_IADLXGPU1 interface, the application must make a copy of the productName string.<br>
         * @ENG_END_DOX
         *
         * @copydoc IADLXGPU1_REQ_TABLE
         *
         */
        virtual ADLX_RESULT ADLX_STD_CALL ProductName(const char** productName) const = 0;
    }; //IADLXGPU1
     //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXGPU1> IADLXGPU1Ptr;
}   //namespace adlx
#else
ADLX_DECLARE_IID(IADLXGPU1, L"IADLXGPU1");
typedef struct IADLXGPU1 IADLXGPU1;

typedef struct IADLXGPU1Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXGPU1* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXGPU1* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXGPU1* pThis, const wchar_t* interfaceId, void** ppInterface);
    //IADLXGPU
    ADLX_RESULT(ADLX_STD_CALL* VendorId)(IADLXGPU1* pThis, const char** vendorId);
    ADLX_RESULT(ADLX_STD_CALL* ASICFamilyType)(IADLXGPU1* pThis, ADLX_ASIC_FAMILY_TYPE* asicFamilyType);
    ADLX_RESULT(ADLX_STD_CALL* Type)(IADLXGPU1* pThis, ADLX_GPU_TYPE* gpuType);
    ADLX_RESULT(ADLX_STD_CALL* IsExternal)(IADLXGPU1* pThis, adlx_bool* isExternal);
    ADLX_RESULT(ADLX_STD_CALL* Name)(IADLXGPU1* pThis, const char** gpuName);
    ADLX_RESULT(ADLX_STD_CALL* DriverPath)(IADLXGPU1* pThis, const char** driverPath);
    ADLX_RESULT(ADLX_STD_CALL* PNPString)(IADLXGPU1* pThis, const char** pnpString);
    ADLX_RESULT(ADLX_STD_CALL* HasDesktops)(IADLXGPU1* pThis, adlx_bool* hasDesktops);
    ADLX_RESULT(ADLX_STD_CALL* TotalVRAM)(IADLXGPU1* pThis, adlx_uint* vramMB);
    ADLX_RESULT(ADLX_STD_CALL* VRAMType)(IADLXGPU1* pThis, const char** type);
    ADLX_RESULT(ADLX_STD_CALL* BIOSInfo)(IADLXGPU1* pThis, const char** partNumber, const char** version, const char** date);
    ADLX_RESULT(ADLX_STD_CALL* DeviceId)(IADLXGPU1* pThis, const char** deviceId);
    ADLX_RESULT(ADLX_STD_CALL* RevisionId)(IADLXGPU1* pThis, const char** revisionId);
    ADLX_RESULT(ADLX_STD_CALL* SubSystemId)(IADLXGPU1* pThis, const char** subSystemId);
    ADLX_RESULT(ADLX_STD_CALL* SubSystemVendorId)(IADLXGPU1* pThis, const char** subSystemVendorId);
    ADLX_RESULT(ADLX_STD_CALL* UniqueId)(IADLXGPU1* pThis, adlx_int* uniqueId);
    //IADLXGPU1
    ADLX_RESULT(ADLX_STD_CALL* PCIBusType)(IADLXGPU1* pThis, ADLX_PCI_BUS_TYPE* busType);
    ADLX_RESULT(ADLX_STD_CALL* PCIBusLaneWidth)(IADLXGPU1* pThis, adlx_uint* laneWidth);
    ADLX_RESULT(ADLX_STD_CALL* MultiGPUMode)(IADLXGPU1* pThis, ADLX_MGPU_MODE* mode);
    ADLX_RESULT(ADLX_STD_CALL* ProductName)(IADLXGPU1* pThis, const char** productName);
} IADLXGPU1Vtbl;

struct IADLXGPU1
{
    const IADLXGPU1Vtbl* pVtbl;
};
#endif
#pragma endregion IADLXGPU1 interface


#pragma region IADLXSystem1 interface
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXPowerTuningServices;

    class ADLX_NO_VTABLE IADLXSystem1 : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID (L"IADLXSystem1")

        /**
        * @page DOX_IADLXSystem1_GetPowerTuningServices GetPowerTuningServices
        * @ENG_START_DOX
        * @brief Gets the reference counted main interface to the @ref DOX_IADLXPowerTuningServices "Power Tuning" domain.
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    GetPowerTuningServices (@ref DOX_IADLXPowerTuningServices** ppPowerTuningServices)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out] ,ppPowerTuningServices,@ref DOX_IADLXPowerTuningServices**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address  __*ppPowerTuningServices__ to __nullptr__. @ENG_END_DOX}
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
        * @copydoc IADLXSystem1_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetPowerTuningServices(IADLXPowerTuningServices** ppPowerTuningServices) = 0;
    };  //IADLXSystem1
    typedef IADLXInterfacePtr_T<IADLXSystem1> IADLXSystem1Ptr;
}   //namespace adlx
#else
ADLX_DECLARE_IID (IADLXSystem1, L"IADLXSystem1")

typedef struct IADLXPowerTuningServices IADLXPowerTuningServices;
typedef struct IADLXSystem1 IADLXSystem1;
typedef struct IADLXSystem1Vtbl
{
    //IADLXInterface
    adlx_long (ADLX_STD_CALL *Acquire)(IADLXSystem1* pThis);
    adlx_long (ADLX_STD_CALL *Release)(IADLXSystem1* pThis);
    ADLX_RESULT (ADLX_STD_CALL *QueryInterface)(IADLXSystem1* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXSystem1
    ADLX_RESULT (ADLX_STD_CALL *GetPowerTuningServices)(IADLXSystem1* pThis, IADLXPowerTuningServices** ppPowerTuningServices);
} IADLXSystem1Vtbl;

struct IADLXSystem1 { const IADLXSystem1Vtbl*pVtbl; };
#endif
#pragma endregion IADLXSystem1 interface

#endif  //ADLX_ISYSTEM1_H