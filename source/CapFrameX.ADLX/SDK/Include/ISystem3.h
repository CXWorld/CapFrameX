//
// Copyright Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_ISYSTEM3_H
#define ADLX_ISYSTEM3_H
#pragma once

#include "ISystem2.h"
#include "ADLXStructures.h"

#pragma region IADLXGPUStressTestFinishedListener interface
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPU3;
    class ADLX_NO_VTABLE IADLXGPUStressTestFinishedListener
    {
    public:
        /**
        * @page DOX_IADLXGPUStressTestFinishedListener_OnGPUStressTestFinished OnGPUStressTestFinished
        * @ENG_START_DOX @brief The __OnGPUStressTestFinished__ is called by ADLX when the GPU stress test is completed. @ENG_END_DOX
        * 
        * @syntax
        * @codeStart
        * adlx_bool    OnGPUStressTestFinished (@ref DOX_IADLXGPU3* pGPU, adlx_bool result)
        * @codeEnd
        * 
        * @params
        * @paramrow{1.,[out],pGPU,@ref DOX_IADLXGPU3*,@ENG_START_DOX The pointer to a GPU where the GPU stress test finished. @ENG_END_DOX}
        * @paramrow{2.,[out],result,adlx_bool,@ENG_START_DOX The result of the GPU stress test. The variable is __true__ if the stress test was successful. The variable is __false__ if the stress test failed. @ENG_END_DOX}
        * 
        * @retvalues
        * @ENG_START_DOX  If the application requires ADLX to continue notifying the next listener, __true__ must be returned.<br>
        * If the application requires ADLX to stop notifying the next listener, __false__ must be returned.<br> @ENG_END_DOX
        * 
        * @detaileddesc
        * @ENG_START_DOX  Once the application registers for notification with @ref DOX_IADLXGPU3_StartStressTest, ADLX will call this method when the GPU stress test is completed.
        * This method should return quickly to avoid blocking ADLX's execution path. If processing the event notification requires a significant time, the application must:
        * - Hold onto a reference to the GPU stress test finished event with @ref DOX_IADLXInterface_Acquire.
        * - Make it available on an asynchronous thread.
        * - Return immediately.
        * - When the asynchronous thread is done processing, discard the GPU stress test finished event with @ref DOX_IADLXInterface_Release. @ENG_END_DOX.
        *
        * @copydoc IADLXGPUStressTestFinishedListener_REQ_TABLE
        * 
        */
        virtual adlx_bool ADLX_STD_CALL OnGPUStressTestFinished(IADLXGPU3* pGPU, adlx_bool result) = 0;

    }; // IADLXGPUStressTestFinishedListener
}   // namespace adlx
#else // __cplusplus
typedef struct IADLXGPU3 IADLXGPU3;
typedef struct IADLXGPUStressTestFinishedListener IADLXGPUStressTestFinishedListener;
typedef struct IADLXGPUStressTestFinishedListenerVtbl
{
    adlx_bool(ADLX_STD_CALL* OnGPUStressTestFinished)(IADLXGPUStressTestFinishedListener* pThis, IADLXGPU3* pGPU, adlx_bool result);
} IADLXGPUStressTestFinishedListenerVtbl;
struct IADLXGPUStressTestFinishedListener
{
    const IADLXGPUStressTestFinishedListenerVtbl* pVtbl;
};
#endif // __cplusplus
#pragma endregion IADLXGPUStressTestFinishedListener interface

// Interfaces for GPU3 Info
#pragma region IADLXGPU3 interface
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXGPUConnectChangedListener;
    class ADLX_NO_VTABLE IADLXApplicationList;
    class ADLX_NO_VTABLE IADLXGPU3 : public IADLXGPU2
    {
    public:
        ADLX_DECLARE_IID(L"IADLXGPU3")

        /**
        * @page DOX_IADLXGPU3_MicroArchitecture MicroArchitecture
        * @ENG_START_DOX @brief Gets the microarchitecture of a GPU. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT MicroArchitecture(const char** microArchitecture)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],microArchitecture,const char**,@ENG_START_DOX The pointer to a variable where the microarchitecture is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the GPU microarchitecture is successfully returned, __ADLX_OK__ is returned.<br>
        * If the GPU microarchitecture is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL MicroArchitecture(const char** microArchitecture) = 0;

        /**
        *@page DOX_IADLXGPU3_HighestVRAMBandwidth HighestVRAMBandwidth
        *@ENG_START_DOX @brief Gets the maximum VRAM bandwidth on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    HighestVRAMBandwidth (adlx_uint* data)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,data,adlx_uint* ,@ENG_START_DOX The pointer to a variable where the highest default performance level Memory bandwidth (in Mbytes/s) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the memory bandwidth is successfully returned, __ADLX_OK__ is returned.<br>
        * If the memory bandwidth is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLXGPU3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL HighestVRAMBandwidth(adlx_uint* data) = 0;
        /**
        *@page DOX_IADLXGPU3_InvisibleVRAM InvisibleVRAM
        *@ENG_START_DOX @brief Gets the size of the invisible VRAM on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    InvisibleVRAM (adlx_uint* data)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,data,adlx_uint* ,@ENG_START_DOX The pointer to a variable where the invisible memory size (in MB) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the invisible memory size is successfully returned, __ADLX_OK__ is returned.<br>
        * If the invisible memory size is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details Invisible VRAM is the portion of VRAM that is inaccessible to applications via the CPU. @ENG_END_DOX
        *
        *@copydoc IADLXGPU3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL InvisibleVRAM(adlx_uint* data) = 0;
        /**
        *@page DOX_IADLXGPU3_VisibleVRAM VisibleVRAM
        *@ENG_START_DOX @brief Gets the size of the visible VRAM on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    VisibleVRAM (adlx_uint* data)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,data,adlx_uint* ,@ENG_START_DOX The pointer to a variable where the visible memory size (in MB) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the visible memory size is successfully returned, __ADLX_OK__ is returned.<br>
        * If the visible memory size is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details Visible VRAM is the portion of the VRAM accessible to applications via the CPU. @ENG_END_DOX
        *
        *@copydoc IADLXGPU3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL VisibleVRAM(adlx_uint* data) = 0;
        /**
        *@page DOX_IADLXGPU3_VRAMVendorRevId VRAMVendorRevId
        *@ENG_START_DOX @brief Gets the VRAM vendor ID. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    VRAMVendorRevId (adlx_uint* data)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,data,adlx_uint* ,@ENG_START_DOX The pointer to a variable where the VRAM vendor ID returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the VRAM vendor ID is successfully returned, __ADLX_OK__ is returned.<br>
        * If the VRAM vendor ID is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLXGPU3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL VRAMVendorRevId(adlx_uint* data) = 0;
        /**
        *@page DOX_IADLXGPU3_VRAMBandwidth VRAMBandwidth
        *@ENG_START_DOX @brief Gets the VRAM bandwidth on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    VRAMBandwidth (adlx_uint* data)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,data,adlx_uint* ,@ENG_START_DOX The pointer to a variable where the bandwidth of the VRAM (in Mbytes/s) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the bandwidth of the VRAM is successfully returned, __ADLX_OK__ is returned.<br>
        * If the bandwidth of the VRAM is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The VRAM bandwidth can be obtained only when the VRAM type is LPDDR5. To obtain the VRAM type, use @ref DOX_IADLXGPU_VRAMType. @ENG_END_DOX
        *
        *@copydoc IADLXGPU3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL VRAMBandwidth(adlx_uint* data) = 0;
        /**
        *@page DOX_IADLXGPU3_VRAMBitRate VRAMBitRate
        *@ENG_START_DOX @brief Gets the bit rate of the VRAM on a GPU. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    VRAMBitRate (adlx_uint* data)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out] ,data,adlx_uint* ,@ENG_START_DOX The pointer to a variable where the bit rate of the VRAM (in Mbps) is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the bit rate of the VRAM is successfully returned, __ADLX_OK__ is returned.<br>
        * If the bit rate of the VRAM is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The VRAM bit rate can be obtained only when the VRAM type is LPDDR5. To obtain the VRAM type, use @ref DOX_IADLXGPU_VRAMType. @ENG_END_DOX
        *
        *@copydoc IADLXGPU3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL VRAMBitRate(adlx_uint* data) = 0;

        /**
        * @page DOX_IADLXGPU3_IsSupportedStressTest IsSupportedStressTest
        * @ENG_START_DOX @brief Checks if GPU stress testing is supported. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    IsSupportedStressTest (adlx_bool* supported)
        * @codeEnd
        * 
        * @params
        * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the GPU stress test support state is returned. The variable is __true__ if the GPU stress test is supported. The variable is __false__ if the GPU stress test is not supported. @ENG_END_DOX}
        * 
        * @retvalues
        * @ENG_START_DOX
        * If the GPU stress test support state is successfully returned, __ADLX_OK__ is returned.<br>
        * If the GPU stress test support state is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        * 
        * @copydoc IADLXGPU3_REQ_TABLE
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsSupportedStressTest(adlx_bool* supported) = 0;

        /**
        * @page DOX_IADLXGPU3_StartStressTest StartStressTest
        * @ENG_START_DOX @brief Starts the GPU stress test. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    StartStressTest (@ref DOX_IADLXGPUStressTestFinishedListener* pGPUStressTestFinishedListener, adlx_uint duration)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[in],pGPUStressTestFinishedListener,@ref DOX_IADLXGPUStressTestFinishedListener*,@ENG_START_DOX The pointer to a GPU stress test complete listener. @ENG_END_DOX}
        * @paramrow{2.,[in],duration,adlx_uint,@ENG_START_DOX The duration for the stress test in seconds. @ENG_END_DOX}
        * 
        * @retvalues
        * @ENG_START_DOX
        * If the GPU stress test is successfully started, __ADLX_OK__ is returned.
        * If the GPU is busy executing another tuning related operation. __ADLX_PENDING_OPERATION__ is returned. In this case, the StartStressTest call should be repeated.
        * If the GPU stress test is not successfully started, an error code is returned.<br/>
        *
		* Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        * 
        * @detaileddesc
        * @ENG_START_DOX  The StartStressTest method triggers an asynchronous execution for the GPU stress test and returns immediately.
        * When the GPU stress test finishes, ADLX calls @ref DOX_IADLXGPUStressTestFinishedListener_OnGPUStressTestFinished in the GPU stress test complete listener.<br>
        *
		* **Note**: Once started, the GPU stress test cannot be interrupted and will run for the specified duration.
        * @ENG_END_DOX
        *
        * @copydoc IADLXGPU3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL StartStressTest(IADLXGPUStressTestFinishedListener* pGPUStressTestFinishedListener, adlx_uint duration) = 0;

    }; //IADLXGPU3
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXGPU3> IADLXGPU3Ptr;
}   //namespace adlx
#else
ADLX_DECLARE_IID(IADLXGPU3, L"IADLXGPU3");
typedef struct IADLXGPU3 IADLXGPU3;
typedef struct IADLXGPUConnectChangedListener IADLXGPUConnectChangedListener;
typedef struct IADLXApplicationList IADLXApplicationList;
typedef struct IADLXGPUStressTestFinishedListener IADLXGPUStressTestFinishedListener;
typedef struct IADLXGPU3Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXGPU3* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXGPU3* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXGPU3* pThis, const wchar_t* interfaceId, void** ppInterface);
    //IADLXGPU
    ADLX_RESULT(ADLX_STD_CALL* VendorId)(IADLXGPU3* pThis, const char** vendorId);
    ADLX_RESULT(ADLX_STD_CALL* ASICFamilyType)(IADLXGPU3* pThis, ADLX_ASIC_FAMILY_TYPE* asicFamilyType);
    ADLX_RESULT(ADLX_STD_CALL* Type)(IADLXGPU3* pThis, ADLX_GPU_TYPE* gpuType);
    ADLX_RESULT(ADLX_STD_CALL* IsExternal)(IADLXGPU3* pThis, adlx_bool* isExternal);
    ADLX_RESULT(ADLX_STD_CALL* Name)(IADLXGPU3* pThis, const char** gpuName);
    ADLX_RESULT(ADLX_STD_CALL* DriverPath)(IADLXGPU3* pThis, const char** driverPath);
    ADLX_RESULT(ADLX_STD_CALL* PNPString)(IADLXGPU3* pThis, const char** pnpString);
    ADLX_RESULT(ADLX_STD_CALL* HasDesktops)(IADLXGPU3* pThis, adlx_bool* hasDesktops);
    ADLX_RESULT(ADLX_STD_CALL* TotalVRAM)(IADLXGPU3* pThis, adlx_uint* vramMB);
    ADLX_RESULT(ADLX_STD_CALL* VRAMType)(IADLXGPU3* pThis, const char** type);
    ADLX_RESULT(ADLX_STD_CALL* BIOSInfo)(IADLXGPU3* pThis, const char** partNumber, const char** version, const char** date);
    ADLX_RESULT(ADLX_STD_CALL* DeviceId)(IADLXGPU3* pThis, const char** deviceId);
    ADLX_RESULT(ADLX_STD_CALL* RevisionId)(IADLXGPU3* pThis, const char** revisionId);
    ADLX_RESULT(ADLX_STD_CALL* SubSystemId)(IADLXGPU3* pThis, const char** subSystemId);
    ADLX_RESULT(ADLX_STD_CALL* SubSystemVendorId)(IADLXGPU3* pThis, const char** subSystemVendorId);
    ADLX_RESULT(ADLX_STD_CALL* UniqueId)(IADLXGPU3* pThis, adlx_int* uniqueId);
    //IADLXGPU1
    ADLX_RESULT(ADLX_STD_CALL* PCIBusType)(IADLXGPU3* pThis, ADLX_PCI_BUS_TYPE* busType);
    ADLX_RESULT(ADLX_STD_CALL* PCIBusLaneWidth)(IADLXGPU3* pThis, adlx_uint* laneWidth);
    ADLX_RESULT(ADLX_STD_CALL* MultiGPUMode)(IADLXGPU3* pThis, ADLX_MGPU_MODE* mode);
    ADLX_RESULT(ADLX_STD_CALL* ProductName)(IADLXGPU3* pThis, const char** productName);
    //IADLXGPU2
    ADLX_RESULT(ADLX_STD_CALL* IsPowerOff)(IADLXGPU3* pThis, adlx_bool* state);
    ADLX_RESULT(ADLX_STD_CALL* PowerOn)(IADLXGPU3* pThis);
    ADLX_RESULT(ADLX_STD_CALL* StartPowerOff)(IADLXGPU3* pThis, IADLXGPUConnectChangedListener* pGPUConnectChangedListener, adlx_int timeout);
    ADLX_RESULT(ADLX_STD_CALL* AbortPowerOff)(IADLXGPU3* pThis);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedApplicationList)(IADLXGPU3* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetApplications)(IADLXGPU3* pThis, IADLXApplicationList** ppApplications);
    ADLX_RESULT(ADLX_STD_CALL* AMDSoftwareReleaseDate)(IADLXGPU3* pThis, adlx_uint* year, adlx_uint* month, adlx_uint* day);
    ADLX_RESULT(ADLX_STD_CALL* AMDSoftwareEdition)(IADLXGPU3* pThis, const char** edition);
    ADLX_RESULT(ADLX_STD_CALL* AMDSoftwareVersion)(IADLXGPU3* pThis, const char** version);
    ADLX_RESULT(ADLX_STD_CALL* DriverVersion)(IADLXGPU3* pThis, const char** version);
    ADLX_RESULT(ADLX_STD_CALL* AMDWindowsDriverVersion)(IADLXGPU3* pThis, const char** version);
    ADLX_RESULT(ADLX_STD_CALL* LUID)(IADLXGPU3* pThis, ADLX_LUID* luid);
    //IADLXGPU3
    ADLX_RESULT(ADLX_STD_CALL* MicroArchitecture)(IADLXGPU3* pThis, const char** microArchitecture);
    ADLX_RESULT(ADLX_STD_CALL* HighestVRAMBandwidth)(IADLXGPU3* pThis, adlx_uint* data);
    ADLX_RESULT(ADLX_STD_CALL* InvisibleVRAM)(IADLXGPU3* pThis, adlx_uint* data);
    ADLX_RESULT(ADLX_STD_CALL* VisibleVRAM)(IADLXGPU3* pThis, adlx_uint* data);
    ADLX_RESULT(ADLX_STD_CALL* VRAMVendorRevId)(IADLXGPU3* pThis, adlx_uint* data);
    ADLX_RESULT(ADLX_STD_CALL* VRAMBandwidth)(IADLXGPU3* pThis, adlx_uint* data);
    ADLX_RESULT(ADLX_STD_CALL* VRAMBitRate)(IADLXGPU3* pThis, adlx_uint* data);
    ADLX_RESULT(ADLX_STD_CALL* IsSupportedStressTest)(IADLXGPU3* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* StartStressTest)(IADLXGPU3* pThis, IADLXGPUStressTestFinishedListener* pGPUStressTestFinishedListener, adlx_uint time);
} IADLXGPU3Vtbl;

struct IADLXGPU3 { const IADLXGPU3Vtbl* pVtbl; };
#endif
#pragma endregion IADLXGPU3 interface


#pragma region IADLXVariableGraphicsMemoryOption
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXVariableGraphicsMemoryOptionList;
    class ADLX_NO_VTABLE IADLXVariableGraphicsMemoryOption : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLXVariableGraphicsMemoryOption")
        /**
        * @page DOX_IADLXVariableGraphicsMemoryOption_Name Name
        * @ENG_START_DOX @brief Gets the name of the Variable Graphics Memory option. @ENG_END_DOX
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    Name(const wchar_t** optionName)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],optionName,const wchar_t**,@ENG_START_DOX The pointer to a zero-terminated string where the optionName string is returned. @ENG_END_DOX}
        *
        * @retvalues
        * @ENG_START_DOX
        * If the optionName string is successfully returned, __ADLX_OK__ is returned.<br>
        * If the optionName string is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
        * @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * The returned memory buffer is valid within the duration of the @ref DOX_IADLXVariableGraphicsMemoryOption interface lifetime.<br>
        *
        * If the application uses the optionName string beyond the lifetime of the @ref DOX_IADLXVariableGraphicsMemoryOption interface, it must make a copy of the optionName string.<br>
        * @ENG_END_DOX
        *
        * @copydoc IADLXVariableGraphicsMemoryOption_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL Name(const char** optionName) = 0;
        /**
        *@page DOX_IADLXVariableGraphicsMemoryOption_Mode Mode
        *@ENG_START_DOX @brief Gets the mode of the Variable Graphics Memory option. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    Mode (@ref ADLX_VARIABLE_GRAPHICS_MEMORY_MODE* mode)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],mode,@ref ADLX_VARIABLE_GRAPHICS_MEMORY_MODE*,@ENG_START_DOX The pointer to a variable where the option mode of Variable Graphics Memory is returned. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the Variable Graphics Memory option mode is successfully returned, __ADLX_OK__ is returned.<br>
        * If the Variable Graphics Memory option mode is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@copydoc IADLXVariableGraphicsMemoryOption_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL Mode(ADLX_VARIABLE_GRAPHICS_MEMORY_MODE* mode) = 0;
        /**
        *@page DOX_IADLXVariableGraphicsMemoryOption_MemoryCarved MemoryCarved
        *@ENG_START_DOX @brief Gets the size of the system RAM dedicated to vRAM of the Variable Graphics Memory option. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    MemoryCarved (adlx_double* memoryCarvedGb)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],memoryCarvedGb,adlx_double*,@ENG_START_DOX The pointer to a variable where the system RAM dedicated to graphics memory is returned (in GB). @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the size of memory carved is successfully returned, __ADLX_OK__ is returned. <br>
        * If the size of memory carved is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@copydoc IADLXVariableGraphicsMemoryOption_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL MemoryCarved(adlx_double* memoryCarvedGb) = 0;
        /**
        *@page DOX_IADLXVariableGraphicsMemoryOption_MemoryRemaining MemoryRemaining
        *@ENG_START_DOX @brief Gets the remaining system RAM of the Variable Graphics Memory option. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    MemoryRemaining (adlx_double* memoryRemainingGb)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out],memoryRemainingGb,adlx_double*,@ENG_START_DOX The pointer to a variable where the remaining system RAM is returned (in GB). @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the remaining system RAM is successfully returned, __ADLX_OK__ is returned. <br>
        * If the remaining system RAM is not successfully returned, an error code is returned. <br>
        * Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
        *
        *@copydoc IADLXVariableGraphicsMemoryOption_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL MemoryRemaining(adlx_double* memoryRemainingGb) = 0;
        
    };  //IADLXVariableGraphicsMemoryOption
    //----------------------------------------------------------------------------------------------    
    typedef IADLXInterfacePtr_T<IADLXVariableGraphicsMemoryOption> IADLXVariableGraphicsMemoryOptionPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXVariableGraphicsMemoryOption, L"IADLXVariableGraphicsMemoryOption")
typedef struct IADLXVariableGraphicsMemoryOption IADLXVariableGraphicsMemoryOption;
typedef struct IADLXVariableGraphicsMemoryOptionList IADLXVariableGraphicsMemoryOptionList;
typedef struct IADLXVariableGraphicsMemoryOptionVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXVariableGraphicsMemoryOption* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXVariableGraphicsMemoryOption* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXVariableGraphicsMemoryOption* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXVariableGraphicsMemoryOption
    ADLX_RESULT(ADLX_STD_CALL* Name) (IADLXVariableGraphicsMemoryOption* pThis, const char** optionName);
    ADLX_RESULT(ADLX_STD_CALL* Mode) (IADLXVariableGraphicsMemoryOption* pThis, ADLX_VARIABLE_GRAPHICS_MEMORY_MODE* mode);
    ADLX_RESULT(ADLX_STD_CALL* MemoryCarved) (IADLXVariableGraphicsMemoryOption* pThis, adlx_double* memoryCarvedGb);
    ADLX_RESULT(ADLX_STD_CALL* MemoryRemaining) (IADLXVariableGraphicsMemoryOption* pThis, adlx_double* memoryRemainingGb);
}IADLXVariableGraphicsMemoryOptionVtbl;
struct IADLXVariableGraphicsMemoryOption { const IADLXVariableGraphicsMemoryOptionVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXVariableGraphicsMemoryOption

#pragma region IADLXVariableGraphicsMemoryOptionList
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXVariableGraphicsMemoryOptionList : public IADLXList
    {
    public:
        ADLX_DECLARE_IID(L"IADLXVariableGraphicsMemoryOptionList")
            //Lists must declare the type of items it holds - what was passed as ADLX_DECLARE_IID() in that interface
            ADLX_DECLARE_ITEM_IID(IADLXVariableGraphicsMemoryOption::IID())
            ADLX_DECLARE_LIST_METHODS
            /**
            * @page DOX_IADLXVariableGraphicsMemoryOptionList_At At
            * @ENG_START_DOX
            * @brief Returns the reference counted interface at the requested location.
            * @ENG_END_DOX
            *
            * @syntax
            * @codeStart
            *  @ref ADLX_RESULT    At (const adlx_uint location, @ref DOX_IADLXVariableGraphicsMemoryOption** ppItem)
            * @codeEnd
            *
            * @params
            * @paramrow{1.,[in] ,location,const adlx_uint ,@ENG_START_DOX The location of the requested interface. @ENG_END_DOX}
            * @paramrow{2.,[out] ,ppItem,@ref DOX_IADLXVariableGraphicsMemoryOption** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned then the method sets the dereferenced address __*ppItem__ to __nullptr__. @ENG_END_DOX}
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
            * @copydoc IADLXVariableGraphicsMemoryOptionList_REQ_TABLE
            *
            */
            virtual ADLX_RESULT         ADLX_STD_CALL At(const adlx_uint location, IADLXVariableGraphicsMemoryOption** ppItem) = 0;
        /**
        *@page DOX_IADLXVariableGraphicsMemoryOptionList_Add_Back Add_Back
        *@ENG_START_DOX @brief Adds an interface to the end of a list. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    Add_Back (@ref DOX_IADLXVariableGraphicsMemoryOption* pItem)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[in] ,pItem,@ref DOX_IADLXVariableGraphicsMemoryOption* ,@ENG_START_DOX The pointer to the interface to be added to the list. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the interface is added to the end of the list, __ADLX_OK__ is returned.<br>
        * If the interface is not added to the end of the list, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details @ENG_END_DOX
        *
        *@copydoc IADLXVariableGraphicsMemoryOptionList_REQ_TABLE
        *
        */
        virtual ADLX_RESULT         ADLX_STD_CALL Add_Back(IADLXVariableGraphicsMemoryOption* pItem) = 0;
    };  //IADLXVariableGraphicsMemoryOptionList
    //----------------------------------------------------------------------------------------------    
    typedef IADLXInterfacePtr_T<IADLXVariableGraphicsMemoryOptionList> IADLXVariableGraphicsMemoryOptionListPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXVariableGraphicsMemoryOptionList, L"IADLXVariableGraphicsMemoryOptionList")
ADLX_DECLARE_ITEM_IID(IADLXVariableGraphicsMemoryOption, IID_IADLXVariableGraphicsMemoryOption())

typedef struct IADLXVariableGraphicsMemoryOptionList IADLXVariableGraphicsMemoryOptionList;
typedef struct IADLXVariableGraphicsMemoryOptionListVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXVariableGraphicsMemoryOptionList* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXVariableGraphicsMemoryOptionList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXVariableGraphicsMemoryOptionList* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXList
    adlx_uint(ADLX_STD_CALL* Size)(IADLXVariableGraphicsMemoryOptionList* pThis);
    adlx_bool(ADLX_STD_CALL* Empty)(IADLXVariableGraphicsMemoryOptionList* pThis);
    adlx_uint(ADLX_STD_CALL* Begin)(IADLXVariableGraphicsMemoryOptionList* pThis);
    adlx_uint(ADLX_STD_CALL* End)(IADLXVariableGraphicsMemoryOptionList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* At)(IADLXVariableGraphicsMemoryOptionList* pThis, const adlx_uint location, IADLXInterface** ppItem);
    ADLX_RESULT(ADLX_STD_CALL* Clear)(IADLXVariableGraphicsMemoryOptionList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* Remove_Back)(IADLXVariableGraphicsMemoryOptionList* pThis);
    ADLX_RESULT(ADLX_STD_CALL* Add_Back)(IADLXVariableGraphicsMemoryOptionList* pThis, IADLXInterface* pItem);

    //IADLXVariableGraphicsMemoryOptionList
    ADLX_RESULT(ADLX_STD_CALL* At_OptionList)(IADLXVariableGraphicsMemoryOptionList* pThis, const adlx_uint location, IADLXVariableGraphicsMemoryOption** ppItem);
    ADLX_RESULT(ADLX_STD_CALL* Add_Back_OptionList)(IADLXVariableGraphicsMemoryOptionList* pThis, IADLXVariableGraphicsMemoryOption* pItem);

}IADLXVariableGraphicsMemoryOptionListVtbl;

struct IADLXVariableGraphicsMemoryOptionList { const IADLXVariableGraphicsMemoryOptionListVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXVariableGraphicsMemoryOptionList

#pragma region IADLXVariableGraphicsMemory
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXVariableGraphicsMemory : public IADLXInterface
    {
    public:
        ADLX_DECLARE_IID(L"IADLXVariableGraphicsMemory")
        /**
        *@page DOX_IADLXVariableGraphicsMemory_IsSupported IsSupported
        *@ENG_START_DOX @brief Checks if Variable Graphics Memory is supported. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    IsSupported (adlx_bool* supported)
        *@codeEnd
        *
        *@params
        * @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of Variable Graphics Memory is returned. The variable is __true__ if Variable Graphics Memory is supported. The variable is __false__ if Variable Graphics Memory is not supported. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the state of Variable Graphics Memory is successfully returned, __ADLX_OK__ is returned.<br>
        * If the state of Variable Graphics Memory is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        * @addinfo
        * @ENG_START_DOX
        * Variable Graphics Memory optimizes memory usage for best performance based on the selected profile by converting up to 75% of the system RAM to dedicated graphics memory (vRAM).
        * @ENG_END_DOX
        *
        *@copydoc IADLXVariableGraphicsMemory_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL IsSupported(adlx_bool* supported) = 0;
        /**
        *@page DOX_IADLXVariableGraphicsMemory_GetDefaultOption GetDefaultOption
        *@ENG_START_DOX @brief Gets the default Variable Graphics Memory option. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetDefaultOption (@ref DOX_IADLXVariableGraphicsMemoryOption** ppOption)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out],ppOption,@ref DOX_IADLXVariableGraphicsMemoryOption**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppOption__ to __nullptr__. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the default Variable Graphics Memory option is successfully returned, __ADLX_OK__ is returned.<br>
        * If the default Variable Graphics Memory option is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed. @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation.<br>
        * Variable Graphics Memory optimizes memory usage for best performance based on the selected profile by converting up to 75% of the system RAM to dedicated graphics memory (vRAM).
        * @ENG_END_DOX
        *
        *@copydoc IADLXVariableGraphicsMemory_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetDefaultOption(IADLXVariableGraphicsMemoryOption** ppOption) = 0;
        /**
        *@page DOX_IADLXVariableGraphicsMemory_GetOption GetOption
        *@ENG_START_DOX @brief Gets the current Variable Graphics Memory option. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    GetOption (@ref DOX_IADLXVariableGraphicsMemoryOption** ppOption)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out],ppOption,@ref DOX_IADLXVariableGraphicsMemoryOption**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppOption__ to __nullptr__. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the current Variable Graphics Memory option is successfully returned, __ADLX_OK__ is returned.<br>
        * If the current Variable Graphics Memory option is not successfully returned, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@detaileddesc
        *@ENG_START_DOX @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed. @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. <br>
        * Variable Graphics Memory optimizes memory usage for best performance based on the selected profile by converting up to 75% of the system RAM to dedicated graphics memory (vRAM).
        * @ENG_END_DOX
        *
        *@copydoc IADLXVariableGraphicsMemory_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetOption(IADLXVariableGraphicsMemoryOption** ppOption) = 0;
        /**
         * @page DOX_IADLXVariableGraphicsMemory_GetAvailableOptions GetAvailableOptions
         * @ENG_START_DOX
         * @brief Gets the reference-counted list of all supported Variable Graphics Memory options.
         * @ENG_END_DOX
         * @syntax
         * @codeStart
         *  @ref ADLX_RESULT    GetAvailableOptions (@ref DOX_IADLXVariableGraphicsMemoryOptionList** ppOptions)
         * @codeEnd
         *
         * @params
         * @paramrow{1.,[out] ,ppGPUs,@ref DOX_IADLXVariableGraphicsMemoryOptionList** ,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address __*ppOptions__ to __nullptr__. @ENG_END_DOX}
         *
         * @retvalues
         * @ENG_START_DOX
         * If the available Variable Graphics Memory options are successfully returned, __ADLX_OK__ is returned.<br>
         * If the avaialble Variable Graphics Memory options are not successfully returned, an error code is returned.<br>
         * Refer to @ref ADLX_RESULT for success codes and error codes.<br>
         * @ENG_END_DOX
         *
         * @detaileddesc
         * @ENG_START_DOX
         * @details The returned interface must be discarded with @ref DOX_IADLXInterface_Release when it is no longer needed.
         * @ENG_END_DOX
         *
         *@addinfo
         *@ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation. <br>
         * Variable Graphics Memory optimizes memory usage for best performance based on the selected profile by converting up to 75% of the system RAM to dedicated graphics memory (vRAM).
         * @ENG_END_DOX
         *
         * @copydoc IADLXVariableGraphicsMemory_REQ_TABLE
         *
         */
        virtual ADLX_RESULT ADLX_STD_CALL GetAvailableOptions(IADLXVariableGraphicsMemoryOptionList** ppOptions) = 0;
        /**
        *@page DOX_IADLXVariableGraphicsMemory_SetOption SetOption
        *@ENG_START_DOX @brief Sets the Variable Graphics Memory option. @ENG_END_DOX
        *
        *@syntax
        *@codeStart
        * @ref ADLX_RESULT    SetOption (@ref DOX_IADLXVariableGraphicsMemoryOption* pOption)
        *@codeEnd
        *
        *@params
        *@paramrow{1.,[out],pOption,@ref DOX_IADLXVariableGraphicsMemoryOption*,@ENG_START_DOX The pointer to the Variable Graphics Memory option interface. @ENG_END_DOX}
        *
        *@retvalues
        *@ENG_START_DOX  If the Variable Graphics Memory option is successfully set, __ADLX_OK__ is returned.<br>
        * If the Variable Graphics Memory option is not successfully set, an error code is returned.<br>
        * Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
        *
        *@addinfo
        *@ENG_START_DOX
        * Variable Graphics Memory optimizes memory usage for best performance based on the selected profile by converting up to 75% of the system RAM to dedicated graphics memory (vRAM). <br>
        *
        * Note that a system restart is required to apply new Variable Graphics Memory options. <br>
        *
        * The __SetOption__ method triggers a system restart. It is recommended that the application advises users to save any work, and requests confirmation before proceeding with the action invoked by this method.
        * @ENG_END_DOX
        *
        *@copydoc IADLXVariableGraphicsMemory_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL SetOption(IADLXVariableGraphicsMemoryOption* pOption) = 0;

    };  //IADLXVariableGraphicsMemory
    //----------------------------------------------------------------------------------------------
    typedef IADLXInterfacePtr_T<IADLXVariableGraphicsMemory> IADLXVariableGraphicsMemoryPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXVariableGraphicsMemory, L"IADLXVariableGraphicsMemory")
typedef struct IADLXVariableGraphicsMemory IADLXVariableGraphicsMemory;
typedef struct IADLXVariableGraphicsMemoryVtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXVariableGraphicsMemory* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXVariableGraphicsMemory* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXVariableGraphicsMemory* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXVariableGraphicsMemory
    ADLX_RESULT(ADLX_STD_CALL* IsSupported) (IADLXVariableGraphicsMemory* pThis, adlx_bool* supported);
    ADLX_RESULT(ADLX_STD_CALL* GetDefaultOption) (IADLXVariableGraphicsMemory* pThis, IADLXVariableGraphicsMemoryOption** option);
    ADLX_RESULT(ADLX_STD_CALL* GetOption) (IADLXVariableGraphicsMemory* pThis, IADLXVariableGraphicsMemoryOption** option);
    ADLX_RESULT(ADLX_STD_CALL* GetAvailableOptions) (IADLXVariableGraphicsMemory* pThis, IADLXVariableGraphicsMemoryOptionList** option);
    ADLX_RESULT(ADLX_STD_CALL* SetOption) (IADLXVariableGraphicsMemory* pThis, IADLXVariableGraphicsMemoryOption* option);
}IADLXVariableGraphicsMemoryVtbl;
struct IADLXVariableGraphicsMemory { const IADLXVariableGraphicsMemoryVtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXVariableGraphicsMemory

#pragma region IADLXSystem3 interface
#if defined (__cplusplus)
namespace adlx
{
    class ADLX_NO_VTABLE IADLXVariableGraphicsMemory;
    class ADLX_NO_VTABLE IADLXSystem3 : public IADLXSystem2
    {
    public:
        ADLX_DECLARE_IID(L"IADLXSystem3")

        /**
        * @page DOX_IADLXSystem3_GetVariableGraphicsMemory GetVariableGraphicsMemory
        * @ENG_START_DOX
        * @brief Gets the reference counted Variable Graphics Memory interface. @ENG_END_DOX
        * @ENG_END_DOX
        *
        * @syntax
        * @codeStart
        *  @ref ADLX_RESULT    GetVariableGraphicsMemory (@ref DOX_IADLXVariableGraphicsMemory** ppVariableGraphicsMemory)
        * @codeEnd
        *
        * @params
        * @paramrow{1.,[out],ppVariableGraphicsMemory,@ref DOX_IADLXVariableGraphicsMemory**,@ENG_START_DOX The address of a pointer to the returned interface. If the interface is not successfully returned\, the method sets the dereferenced address  __*ppVariableGraphicsMemory__ to __nullptr__. @ENG_END_DOX}
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
        * @ENG_START_DOX  In C++, when using ADLX interfaces as smart pointers, there is no need to call @ref DOX_IADLXInterface_Release because smart pointers call it in their internal implementation.
        * @ENG_END_DOX
        * @copydoc IADLXSystem3_REQ_TABLE
        *
        */
        virtual ADLX_RESULT ADLX_STD_CALL GetVariableGraphicsMemory(IADLXVariableGraphicsMemory** ppVariableGraphicsMemory) = 0;

        };  //IADLXSystem3
    typedef IADLXInterfacePtr_T<IADLXSystem3> IADLXSystem3Ptr;
}   //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXSystem3, L"IADLXSystem3")

typedef struct IADLXVariableGraphicsMemory IADLXVariableGraphicsMemory;
typedef struct IADLXSystem3 IADLXSystem3;
typedef struct IADLXSystem3Vtbl
{
    //IADLXInterface
    adlx_long(ADLX_STD_CALL* Acquire)(IADLXSystem3* pThis);
    adlx_long(ADLX_STD_CALL* Release)(IADLXSystem3* pThis);
    ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXSystem3* pThis, const wchar_t* interfaceId, void** ppInterface);

    //IADLXSystem1
    ADLX_RESULT(ADLX_STD_CALL* GetPowerTuningServices)(IADLXSystem3* pThis, IADLXPowerTuningServices** ppPowerTuningServices);
    //IADLXSystem2
    ADLX_RESULT(ADLX_STD_CALL* GetMultimediaServices)(IADLXSystem3* pThis, IADLXMultimediaServices** ppMultiMediaServices);
    ADLX_RESULT(ADLX_STD_CALL* GetGPUAppsListChangedHandling)(IADLXSystem3* pThis, IADLXGPUAppsListChangedHandling** ppGPUAppsListChangedHandling);

    //IADLXSystem3
    ADLX_RESULT(ADLX_STD_CALL* GetVariableGraphicsMemory)(IADLXSystem3* pThis, IADLXVariableGraphicsMemory** ppVariableGraphicsMemory);
} IADLXSystem3Vtbl;

struct IADLXSystem3 { const IADLXSystem3Vtbl* pVtbl; };
#endif //__cplusplus
#pragma endregion IADLXSystem3 interface

#endif  //ADLX_ISYSTEM3_H
