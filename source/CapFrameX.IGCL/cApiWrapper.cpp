//===========================================================================
//Copyright (C) 2022-23 Intel Corporation
//
// 
//
//SPDX-License-Identifier: MIT
//--------------------------------------------------------------------------

/**
 *
 * @file ctl_api.cpp
 * @version v1-r0
 *
 */

 // Note: UWP applications should have defined WINDOWS_UWP in their compiler settings
 // Also at this point, it's easier by not enabling pre-compiled option to compile this file
 // Not all functionalities are tested for a UWP application

#include "pch.h"
#include <windows.h>
#include <strsafe.h>

//#define CTL_APIEXPORT

#include "igcl_api.h"

/////////////////////////////////////////////////////////////////////////////////
//
// Implementation of wrapper functions
//
static HINSTANCE hinstLib = NULL;
static ctl_runtime_path_args_t* pRuntimeArgs = NULL;

/**
 * @brief Function to get DLL name based on app version
 *
 */
#if defined(_WIN64)
#define CTL_DLL_NAME L"ControlLib"
#else
#define CTL_DLL_NAME L"ControlLib32"
#endif
#define CTL_DLL_PATH_LEN 512

ctl_result_t GetControlAPIDLLPath(ctl_init_args_t* pInitArgs, wchar_t* pwcDLLPath)
{
    if ((NULL == pRuntimeArgs) || (NULL == pRuntimeArgs->pRuntimePath))
    {
        // Load the requested DLL based on major version in init args
        uint16_t majorVersion = CTL_MAJOR_VERSION(pInitArgs->AppVersion);

        // If caller's major version is higher than the DLL's, then simply not support the caller!
        // This is not supposed to happen as wrapper is part of the app itself which includes igcl_api.h with right major version
        if (majorVersion > CTL_IMPL_MAJOR_VERSION)
            return CTL_RESULT_ERROR_UNSUPPORTED_VERSION;

        if (majorVersion > 1)
            StringCbPrintfW(pwcDLLPath, CTL_DLL_PATH_LEN, L"%s%d.dll", CTL_DLL_NAME, majorVersion);
        else // just control_api.dll
            StringCbPrintfW(pwcDLLPath, CTL_DLL_PATH_LEN, L"%s.dll", CTL_DLL_NAME);
    }
    else if (pRuntimeArgs->pRuntimePath)
    {
        // caller specified a specific RT, use it instead
        wcsncpy_s(pwcDLLPath, CTL_DLL_PATH_LEN, pRuntimeArgs->pRuntimePath, CTL_DLL_PATH_LEN - 1);
    }
    return CTL_RESULT_SUCCESS;
}


/**
* @brief Control Api Init
*
* @details
*     - Control Api Init
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pInitDesc`
*         + `nullptr == phAPIHandle`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlInit(
    ctl_init_args_t* pInitDesc,                     ///< [in][out] App's control API version
    ctl_api_handle_t* phAPIHandle                   ///< [in][out][release] Control API handle
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;

    // special code - only for ctlInit()
    if (NULL == hinstLib)
    {
        wchar_t strDLLPath[CTL_DLL_PATH_LEN];
        result = GetControlAPIDLLPath(pInitDesc, strDLLPath);
        if (result == CTL_RESULT_SUCCESS)
        {
#ifdef WINDOWS_UWP
            hinstLib = LoadPackagedLibrary(strDLLPath, 0);
#else
            DWORD dwFlags = LOAD_LIBRARY_SEARCH_SYSTEM32;
#ifdef _DEBUG
            dwFlags = dwFlags | LOAD_LIBRARY_SEARCH_APPLICATION_DIR;
#endif
            hinstLib = LoadLibraryExW(strDLLPath, NULL, dwFlags);
#endif
            if (NULL == hinstLib)
            {
                result = CTL_RESULT_ERROR_LOAD;
            }
            else if (pRuntimeArgs)
            {
                ctlSetRuntimePath(pRuntimeArgs);
            }
        }
    }

    if (NULL != hinstLib)
    {
        ctl_pfnInit_t pfnInit = (ctl_pfnInit_t)GetProcAddress(hinstLib, "ctlInit");
        if (pfnInit)
        {
            result = pfnInit(pInitDesc, phAPIHandle);
        }
    }

    return result;
}


/**
* @brief Control Api Destroy
*
* @details
*     - Control Api Close
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hAPIHandle`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlClose(
    ctl_api_handle_t hAPIHandle                     ///< [in][release] Control API implementation handle obtained during init
                                                    ///< call
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnClose_t pfnClose = (ctl_pfnClose_t)GetProcAddress(hinstLib, "ctlClose");
        if (pfnClose)
        {
            result = pfnClose(hAPIHandle);
        }
    }

    // special code - only for ctlClose()
    // might get CTL_RESULT_SUCCESS_STILL_OPEN_BY_ANOTHER_CALLER
    // if its open by another caller do not free the instance handle 
    if (result == CTL_RESULT_SUCCESS)
    {
        if (NULL != hinstLib)
        {
            FreeLibrary(hinstLib);
            hinstLib = NULL;
        }
    }
    // set runtime args back to NULL
    // no need to free this as it's allocated by caller   
    pRuntimeArgs = NULL;
    return result;
}


/**
* @brief Runtime path
*
* @details
*     - Control Api set runtime path. Optional call from a loader which allows
*       the loaded runtime to enumerate only the adapters which the specified
*       runtime is responsible for. This is done usually by a loader or by
*       callers who know how to get the specific runtime of interest. This
*       call right now is reserved for use by Intel components.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pArgs`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlSetRuntimePath(
    ctl_runtime_path_args_t* pArgs                  ///< [in] Runtime path
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnSetRuntimePath_t pfnSetRuntimePath = (ctl_pfnSetRuntimePath_t)GetProcAddress(hinstLib, "ctlSetRuntimePath");
        if (pfnSetRuntimePath)
        {
            result = pfnSetRuntimePath(pArgs);
        }
    }

    // special code - only for ctlSetRuntimePath()
    // might get CTL_RESULT_SUCCESS_STILL_OPEN_BY_ANOTHER_CALLER
    // if its open by another caller do not free the instance handle 
    else if (pArgs->pRuntimePath)
    {
        // this is a case where the caller app is interested in loading a RT directly
        pRuntimeArgs = pArgs;
        result = CTL_RESULT_SUCCESS;
    }
    return result;
}


/**
* @brief Wait for a property change. Note that this is a blocking call
*
* @details
*     - Wait for a property change in display, 3d, media etc.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceAdapter`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pArgs`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlWaitForPropertyChange(
    ctl_device_adapter_handle_t hDeviceAdapter,     ///< [in][release] handle to control device adapter
    ctl_wait_property_change_args_t* pArgs          ///< [in] Argument containing information about which property changes to
                                                    ///< listen for
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnWaitForPropertyChange_t pfnWaitForPropertyChange = (ctl_pfnWaitForPropertyChange_t)GetProcAddress(hinstLib, "ctlWaitForPropertyChange");
        if (pfnWaitForPropertyChange)
        {
            result = pfnWaitForPropertyChange(hDeviceAdapter, pArgs);
        }
    }

    return result;
}


/**
* @brief Reserved function
*
* @details
*     - Reserved function
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceAdapter`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pArgs`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlReservedCall(
    ctl_device_adapter_handle_t hDeviceAdapter,     ///< [in][release] handle to control device adapter
    ctl_reserved_args_t* pArgs                      ///< [in] Argument containing information
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnReservedCall_t pfnReservedCall = (ctl_pfnReservedCall_t)GetProcAddress(hinstLib, "ctlReservedCall");
        if (pfnReservedCall)
        {
            result = pfnReservedCall(hDeviceAdapter, pArgs);
        }
    }

    return result;
}


/**
* @brief Get 3D capabilities
*
* @details
*     - The application gets 3D properties
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pFeatureCaps`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetSupported3DCapabilities(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    ctl_3d_feature_caps_t* pFeatureCaps             ///< [in,out][release] 3D properties
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetSupported3DCapabilities_t pfnGetSupported3DCapabilities = (ctl_pfnGetSupported3DCapabilities_t)GetProcAddress(hinstLib, "ctlGetSupported3DCapabilities");
        if (pfnGetSupported3DCapabilities)
        {
            result = pfnGetSupported3DCapabilities(hDAhandle, pFeatureCaps);
        }
    }

    return result;
}


/**
* @brief Get/Set 3D feature
*
* @details
*     - 3D feature details
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pFeature`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetSet3DFeature(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    ctl_3d_feature_getset_t* pFeature               ///< [in][release] 3D feature get/set parameter
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetSet3DFeature_t pfnGetSet3DFeature = (ctl_pfnGetSet3DFeature_t)GetProcAddress(hinstLib, "ctlGetSet3DFeature");
        if (pfnGetSet3DFeature)
        {
            result = pfnGetSet3DFeature(hDAhandle, pFeature);
        }
    }

    return result;
}


/**
* @brief Check Driver version
*
* @details
*     - The application checks driver version
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceAdapter`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlCheckDriverVersion(
    ctl_device_adapter_handle_t hDeviceAdapter,     ///< [in][release] handle to control device adapter
    ctl_version_info_t version_info                 ///< [in][release] Driver version info
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnCheckDriverVersion_t pfnCheckDriverVersion = (ctl_pfnCheckDriverVersion_t)GetProcAddress(hinstLib, "ctlCheckDriverVersion");
        if (pfnCheckDriverVersion)
        {
            result = pfnCheckDriverVersion(hDeviceAdapter, version_info);
        }
    }

    return result;
}


/**
* @brief Enumerate devices
*
* @details
*     - The application enumerates all device adapters in the system
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hAPIHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pCount`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlEnumerateDevices(
    ctl_api_handle_t hAPIHandle,                    ///< [in][release] Applications should pass the Control API handle returned
                                                    ///< by the CtlInit function 
    uint32_t* pCount,                               ///< [in,out][release] pointer to the number of device instances. If count
                                                    ///< is zero, then the api will update the value with the total
                                                    ///< number of drivers available. If count is non-zero, then the api will
                                                    ///< only retrieve the number of drivers.
                                                    ///< If count is larger than the number of drivers available, then the api
                                                    ///< will update the value with the correct number of drivers available.
    ctl_device_adapter_handle_t* phDevices          ///< [in,out][optional][release][range(0, *pCount)] array of driver
                                                    ///< instance handles
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEnumerateDevices_t pfnEnumerateDevices = (ctl_pfnEnumerateDevices_t)GetProcAddress(hinstLib, "ctlEnumerateDevices");
        if (pfnEnumerateDevices)
        {
            result = pfnEnumerateDevices(hAPIHandle, pCount, phDevices);
        }
    }

    return result;
}


/**
* @brief Enumerate display outputs
*
* @details
*     - Enumerates display output capabilities
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceAdapter`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pCount`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlEnumerateDisplayOutputs(
    ctl_device_adapter_handle_t hDeviceAdapter,     ///< [in][release] handle to control device adapter
    uint32_t* pCount,                               ///< [in,out][release] pointer to the number of display output instances.
                                                    ///< If count is zero, then the api will update the value with the total
                                                    ///< number of outputs available. If count is non-zero, then the api will
                                                    ///< only retrieve the number of outputs.
                                                    ///< If count is larger than the number of drivers available, then the api
                                                    ///< will update the value with the correct number of drivers available.
    ctl_display_output_handle_t* phDisplayOutputs   ///< [in,out][optional][release][range(0, *pCount)] array of display output
                                                    ///< instance handles
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEnumerateDisplayOutputs_t pfnEnumerateDisplayOutputs = (ctl_pfnEnumerateDisplayOutputs_t)GetProcAddress(hinstLib, "ctlEnumerateDisplayOutputs");
        if (pfnEnumerateDisplayOutputs)
        {
            result = pfnEnumerateDisplayOutputs(hDeviceAdapter, pCount, phDisplayOutputs);
        }
    }

    return result;
}


/**
* @brief Get Device Properties
*
* @details
*     - The application gets device properties
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pProperties`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetDeviceProperties(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to control device adapter
    ctl_device_adapter_properties_t* pProperties    ///< [in,out][release] Query result for device properties
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetDeviceProperties_t pfnGetDeviceProperties = (ctl_pfnGetDeviceProperties_t)GetProcAddress(hinstLib, "ctlGetDeviceProperties");
        if (pfnGetDeviceProperties)
        {
            result = pfnGetDeviceProperties(hDAhandle, pProperties);
        }
    }

    return result;
}


/**
* @brief Get Display  Properties
*
* @details
*     - The application gets display  properties
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pProperties`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetDisplayProperties(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_display_properties_t* pProperties           ///< [in,out][release] Query result for display  properties
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetDisplayProperties_t pfnGetDisplayProperties = (ctl_pfnGetDisplayProperties_t)GetProcAddress(hinstLib, "ctlGetDisplayProperties");
        if (pfnGetDisplayProperties)
        {
            result = pfnGetDisplayProperties(hDisplayOutput, pProperties);
        }
    }

    return result;
}


/**
* @brief Get Adapter Display encoder  Properties
*
* @details
*     - The application gets the graphic adapters display encoder properties
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pProperties`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetAdaperDisplayEncoderProperties(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_adapter_display_encoder_properties_t* pProperties   ///< [in,out][release] Query result for adapter display encoder properties
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetAdaperDisplayEncoderProperties_t pfnGetAdaperDisplayEncoderProperties = (ctl_pfnGetAdaperDisplayEncoderProperties_t)GetProcAddress(hinstLib, "ctlGetAdaperDisplayEncoderProperties");
        if (pfnGetAdaperDisplayEncoderProperties)
        {
            result = pfnGetAdaperDisplayEncoderProperties(hDisplayOutput, pProperties);
        }
    }

    return result;
}


/**
* @brief Get Level0 Device handle
*
* @details
*     - The application gets OneAPI Level0 Device handles
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pZeDevice`
*         + `nullptr == hInstance`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetZeDevice(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    void* pZeDevice,                                ///< [out][release] ze_device handle
    void** hInstance                                ///< [out][release] Module instance which caller can use to get export
                                                    ///< functions directly
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetZeDevice_t pfnGetZeDevice = (ctl_pfnGetZeDevice_t)GetProcAddress(hinstLib, "ctlGetZeDevice");
        if (pfnGetZeDevice)
        {
            result = pfnGetZeDevice(hDAhandle, pZeDevice, hInstance);
        }
    }

    return result;
}


/**
* @brief Get Sharpness capability
*
* @details
*     - Returns sharpness capability
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pSharpnessCaps`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetSharpnessCaps(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_sharpness_caps_t* pSharpnessCaps            ///< [in,out][release] Query result for sharpness capability
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetSharpnessCaps_t pfnGetSharpnessCaps = (ctl_pfnGetSharpnessCaps_t)GetProcAddress(hinstLib, "ctlGetSharpnessCaps");
        if (pfnGetSharpnessCaps)
        {
            result = pfnGetSharpnessCaps(hDisplayOutput, pSharpnessCaps);
        }
    }

    return result;
}


/**
* @brief Get Sharpness setting
*
* @details
*     - Returns current sharpness settings
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pSharpnessSettings`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetCurrentSharpness(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_sharpness_settings_t* pSharpnessSettings    ///< [in,out][release] Query result for sharpness current settings
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetCurrentSharpness_t pfnGetCurrentSharpness = (ctl_pfnGetCurrentSharpness_t)GetProcAddress(hinstLib, "ctlGetCurrentSharpness");
        if (pfnGetCurrentSharpness)
        {
            result = pfnGetCurrentSharpness(hDisplayOutput, pSharpnessSettings);
        }
    }

    return result;
}


/**
* @brief Set Sharpness setting
*
* @details
*     - Set current sharpness settings
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pSharpnessSettings`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlSetCurrentSharpness(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_sharpness_settings_t* pSharpnessSettings    ///< [in][release] Set sharpness current settings
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnSetCurrentSharpness_t pfnSetCurrentSharpness = (ctl_pfnSetCurrentSharpness_t)GetProcAddress(hinstLib, "ctlSetCurrentSharpness");
        if (pfnSetCurrentSharpness)
        {
            result = pfnSetCurrentSharpness(hDisplayOutput, pSharpnessSettings);
        }
    }

    return result;
}


/**
* @brief I2C Access
*
* @details
*     - The application does I2C aceess
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pI2cAccessArgs`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_INVALID_OPERATION_TYPE - "Invalid operation type"
*     - ::CTL_RESULT_ERROR_INVALID_SIZE - "Invalid I2C data size"
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS - "Insufficient permissions"
*     - ::CTL_RESULT_ERROR_INVALID_NULL_POINTER - "Invalid null pointer"
*     - ::CTL_RESULT_ERROR_NULL_OS_DISPLAY_OUTPUT_HANDLE - "Null OS display output handle"
*     - ::CTL_RESULT_ERROR_NULL_OS_INTERFACE - "Null OS interface"
*     - ::CTL_RESULT_ERROR_NULL_OS_ADAPATER_HANDLE - "Null OS adapter handle"
*     - ::CTL_RESULT_ERROR_KMD_CALL - "Kernal mode driver call failure"
*/
ctl_result_t CTL_APICALL
ctlI2CAccess(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in] Handle to display output
    ctl_i2c_access_args_t* pI2cAccessArgs           ///< [in,out] I2c access arguments
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnI2CAccess_t pfnI2CAccess = (ctl_pfnI2CAccess_t)GetProcAddress(hinstLib, "ctlI2CAccess");
        if (pfnI2CAccess)
        {
            result = pfnI2CAccess(hDisplayOutput, pI2cAccessArgs);
        }
    }

    return result;
}


/**
* @brief Aux Access
*
* @details
*     - The application does Aux aceess, PSR needs to be disabled for AUX
*       call.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pAuxAccessArgs`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_INVALID_OPERATION_TYPE - "Invalid operation type"
*     - ::CTL_RESULT_ERROR_INVALID_SIZE - "Invalid AUX data size"
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS - "Insufficient permissions"
*     - ::CTL_RESULT_ERROR_INVALID_NULL_POINTER - "Invalid null pointer"
*     - ::CTL_RESULT_ERROR_NULL_OS_DISPLAY_OUTPUT_HANDLE - "Null OS display output handle"
*     - ::CTL_RESULT_ERROR_NULL_OS_INTERFACE - "Null OS interface"
*     - ::CTL_RESULT_ERROR_NULL_OS_ADAPATER_HANDLE - "Null OS adapter handle"
*     - ::CTL_RESULT_ERROR_INVALID_AUX_ACCESS_FLAG - "Invalid flag for AUX access"
*     - ::CTL_RESULT_ERROR_KMD_CALL - "Kernal mode driver call failure"
*/
ctl_result_t CTL_APICALL
ctlAUXAccess(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in] Handle to display output
    ctl_aux_access_args_t* pAuxAccessArgs           ///< [in,out] Aux access arguments
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnAUXAccess_t pfnAUXAccess = (ctl_pfnAUXAccess_t)GetProcAddress(hinstLib, "ctlAUXAccess");
        if (pfnAUXAccess)
        {
            result = pfnAUXAccess(hDisplayOutput, pAuxAccessArgs);
        }
    }

    return result;
}


/**
* @brief Get Power optimization features
*
* @details
*     - Returns power optimization capabilities
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pPowerOptimizationCaps`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetPowerOptimizationCaps(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_power_optimization_caps_t* pPowerOptimizationCaps   ///< [in,out][release] Query result for power optimization features
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetPowerOptimizationCaps_t pfnGetPowerOptimizationCaps = (ctl_pfnGetPowerOptimizationCaps_t)GetProcAddress(hinstLib, "ctlGetPowerOptimizationCaps");
        if (pfnGetPowerOptimizationCaps)
        {
            result = pfnGetPowerOptimizationCaps(hDisplayOutput, pPowerOptimizationCaps);
        }
    }

    return result;
}


/**
* @brief Get Power optimization setting
*
* @details
*     - Returns power optimization setting for a specific feature
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pPowerOptimizationSettings`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_INVALID_POWERFEATURE_OPTIMIZATION_FLAG - "Unsupported PowerOptimizationFeature"
*     - ::CTL_RESULT_ERROR_INVALID_POWERSOURCE_TYPE_FOR_DPST - "DPST is supported only in DC Mode"
*/
ctl_result_t CTL_APICALL
ctlGetPowerOptimizationSetting(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_power_optimization_settings_t* pPowerOptimizationSettings   ///< [in,out][release] Power optimization data to be fetched
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetPowerOptimizationSetting_t pfnGetPowerOptimizationSetting = (ctl_pfnGetPowerOptimizationSetting_t)GetProcAddress(hinstLib, "ctlGetPowerOptimizationSetting");
        if (pfnGetPowerOptimizationSetting)
        {
            result = pfnGetPowerOptimizationSetting(hDisplayOutput, pPowerOptimizationSettings);
        }
    }

    return result;
}


/**
* @brief Set Power optimization setting
*
* @details
*     - Set power optimization setting for a specific feature
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pPowerOptimizationSettings`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_INVALID_POWERFEATURE_OPTIMIZATION_FLAG - "Unsupported PowerOptimizationFeature"
*     - ::CTL_RESULT_ERROR_INVALID_POWERSOURCE_TYPE_FOR_DPST - "DPST is supported only in DC Mode"
*/
ctl_result_t CTL_APICALL
ctlSetPowerOptimizationSetting(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_power_optimization_settings_t* pPowerOptimizationSettings   ///< [in][release] Power optimization data to be applied
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnSetPowerOptimizationSetting_t pfnSetPowerOptimizationSetting = (ctl_pfnSetPowerOptimizationSetting_t)GetProcAddress(hinstLib, "ctlSetPowerOptimizationSetting");
        if (pfnSetPowerOptimizationSetting)
        {
            result = pfnSetPowerOptimizationSetting(hDisplayOutput, pPowerOptimizationSettings);
        }
    }

    return result;
}


/**
* @brief Set Brightness on companion display
*
* @details
*     - Set Brightness for a target display. Currently support is only for
*       companion display.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pSetBrightnessSetting`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_INVALID_ARGUMENT - "Invalid Brightness data passed as argument"
*     - ::CTL_RESULT_ERROR_DISPLAY_NOT_ACTIVE - "Display not active"
*     - ::CTL_RESULT_ERROR_INVALID_OPERATION_TYPE - "Currently Brightness API is supported only on companion display"
*/
ctl_result_t CTL_APICALL
ctlSetBrightnessSetting(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_set_brightness_t* pSetBrightnessSetting     ///< [in][release] Brightness settings to be applied
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnSetBrightnessSetting_t pfnSetBrightnessSetting = (ctl_pfnSetBrightnessSetting_t)GetProcAddress(hinstLib, "ctlSetBrightnessSetting");
        if (pfnSetBrightnessSetting)
        {
            result = pfnSetBrightnessSetting(hDisplayOutput, pSetBrightnessSetting);
        }
    }

    return result;
}


/**
* @brief Get Brightness setting
*
* @details
*     - Get Brightness for a target display. Currently support is only for
*       companion display.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pGetBrightnessSetting`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_DISPLAY_NOT_ACTIVE - "Display not active"
*     - ::CTL_RESULT_ERROR_INVALID_OPERATION_TYPE - "Currently Brightness API is supported only on companion display"
*/
ctl_result_t CTL_APICALL
ctlGetBrightnessSetting(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_get_brightness_t* pGetBrightnessSetting     ///< [out][release] Brightness settings data to be fetched
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetBrightnessSetting_t pfnGetBrightnessSetting = (ctl_pfnGetBrightnessSetting_t)GetProcAddress(hinstLib, "ctlGetBrightnessSetting");
        if (pfnGetBrightnessSetting)
        {
            result = pfnGetBrightnessSetting(hDisplayOutput, pGetBrightnessSetting);
        }
    }

    return result;
}


/**
* @brief Pixel transformation get pipe configuration
*
* @details
*     - The application does pixel transformation get pipe configuration
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pPixTxGetConfigArgs`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS - "Insufficient permissions"
*     - ::CTL_RESULT_ERROR_INVALID_NULL_POINTER - "Invalid null pointer"
*     - ::CTL_RESULT_ERROR_NULL_OS_DISPLAY_OUTPUT_HANDLE - "Null OS display output handle"
*     - ::CTL_RESULT_ERROR_NULL_OS_INTERFACE - "Null OS interface"
*     - ::CTL_RESULT_ERROR_NULL_OS_ADAPATER_HANDLE - "Null OS adapter handle"
*     - ::CTL_RESULT_ERROR_KMD_CALL - "Kernal mode driver call failure"
*     - ::CTL_RESULT_ERROR_INVALID_PIXTX_GET_CONFIG_QUERY_TYPE - "Invalid query type"
*     - ::CTL_RESULT_ERROR_INVALID_PIXTX_BLOCK_ID - "Invalid block id"
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PIXTX_BLOCK_CONFIG_MEMORY - "Insufficient memery allocated for BlockConfigs"
*     - ::CTL_RESULT_ERROR_3DLUT_INVALID_PIPE - "Invalid pipe for 3dlut"
*     - ::CTL_RESULT_ERROR_3DLUT_INVALID_DATA - "Invalid 3dlut data"
*     - ::CTL_RESULT_ERROR_3DLUT_NOT_SUPPORTED_IN_HDR - "3dlut not supported in HDR"
*     - ::CTL_RESULT_ERROR_3DLUT_INVALID_OPERATION - "Invalid 3dlut operation"
*     - ::CTL_RESULT_ERROR_3DLUT_UNSUCCESSFUL - "3dlut call unsuccessful"
*/
ctl_result_t CTL_APICALL
ctlPixelTransformationGetConfig(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in] Handle to display output
    ctl_pixtx_pipe_get_config_t* pPixTxGetConfigArgs///< [in,out] Pixel transformation get pipe configiguration arguments
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnPixelTransformationGetConfig_t pfnPixelTransformationGetConfig = (ctl_pfnPixelTransformationGetConfig_t)GetProcAddress(hinstLib, "ctlPixelTransformationGetConfig");
        if (pfnPixelTransformationGetConfig)
        {
            result = pfnPixelTransformationGetConfig(hDisplayOutput, pPixTxGetConfigArgs);
        }
    }

    return result;
}


/**
* @brief Pixel transformation set pipe configuration
*
* @details
*     - The application does pixel transformation set pipe configuration
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pPixTxSetConfigArgs`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS - "Insufficient permissions"
*     - ::CTL_RESULT_ERROR_INVALID_NULL_POINTER - "Invalid null pointer"
*     - ::CTL_RESULT_ERROR_NULL_OS_DISPLAY_OUTPUT_HANDLE - "Null OS display output handle"
*     - ::CTL_RESULT_ERROR_NULL_OS_INTERFACE - "Null OS interface"
*     - ::CTL_RESULT_ERROR_NULL_OS_ADAPATER_HANDLE - "Null OS adapter handle"
*     - ::CTL_RESULT_ERROR_KMD_CALL - "Kernal mode driver call failure"
*     - ::CTL_RESULT_ERROR_INVALID_PIXTX_SET_CONFIG_OPERATION_TYPE - "Invalid operation type"
*     - ::CTL_RESULT_ERROR_INVALID_SET_CONFIG_NUMBER_OF_SAMPLES - "Invalid number of samples"
*     - ::CTL_RESULT_ERROR_INVALID_PIXTX_BLOCK_ID - "Invalid block id"
*     - ::CTL_RESULT_ERROR_PERSISTANCE_NOT_SUPPORTED - "Persistance not supported"
*     - ::CTL_RESULT_ERROR_3DLUT_INVALID_PIPE - "Invalid pipe for 3dlut"
*     - ::CTL_RESULT_ERROR_3DLUT_INVALID_DATA - "Invalid 3dlut data"
*     - ::CTL_RESULT_ERROR_3DLUT_NOT_SUPPORTED_IN_HDR - "3dlut not supported in HDR"
*     - ::CTL_RESULT_ERROR_3DLUT_INVALID_OPERATION - "Invalid 3dlut operation"
*     - ::CTL_RESULT_ERROR_3DLUT_UNSUCCESSFUL - "3dlut call unsuccessful"
*/
ctl_result_t CTL_APICALL
ctlPixelTransformationSetConfig(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in] Handle to display output
    ctl_pixtx_pipe_set_config_t* pPixTxSetConfigArgs///< [in,out] Pixel transformation set pipe configiguration arguments
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnPixelTransformationSetConfig_t pfnPixelTransformationSetConfig = (ctl_pfnPixelTransformationSetConfig_t)GetProcAddress(hinstLib, "ctlPixelTransformationSetConfig");
        if (pfnPixelTransformationSetConfig)
        {
            result = pfnPixelTransformationSetConfig(hDisplayOutput, pPixTxSetConfigArgs);
        }
    }

    return result;
}


/**
* @brief Panel Descriptor Access
*
* @details
*     - The application does EDID or Display ID access
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pPanelDescriptorAccessArgs`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_INVALID_OPERATION_TYPE - "Invalid operation type"
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS - "Insufficient permissions"
*     - ::CTL_RESULT_ERROR_INVALID_NULL_POINTER - "Invalid null pointer"
*     - ::CTL_RESULT_ERROR_NULL_OS_DISPLAY_OUTPUT_HANDLE - "Null OS display output handle"
*     - ::CTL_RESULT_ERROR_NULL_OS_INTERFACE - "Null OS interface"
*     - ::CTL_RESULT_ERROR_NULL_OS_ADAPATER_HANDLE - "Null OS adapter handle"
*     - ::CTL_RESULT_ERROR_KMD_CALL - "Kernal mode driver call failure"
*/
ctl_result_t CTL_APICALL
ctlPanelDescriptorAccess(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in] Handle to display output
    ctl_panel_descriptor_access_args_t* pPanelDescriptorAccessArgs  ///< [in,out] Panel descriptor access arguments
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnPanelDescriptorAccess_t pfnPanelDescriptorAccess = (ctl_pfnPanelDescriptorAccess_t)GetProcAddress(hinstLib, "ctlPanelDescriptorAccess");
        if (pfnPanelDescriptorAccess)
        {
            result = pfnPanelDescriptorAccess(hDisplayOutput, pPanelDescriptorAccessArgs);
        }
    }

    return result;
}


/**
* @brief Get Supported Retro Scaling Types
*
* @details
*     - Returns supported retro scaling capabilities
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pRetroScalingCaps`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetSupportedRetroScalingCapability(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to adapter
    ctl_retro_scaling_caps_t* pRetroScalingCaps     ///< [in,out][release] Query result for supported retro scaling types
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetSupportedRetroScalingCapability_t pfnGetSupportedRetroScalingCapability = (ctl_pfnGetSupportedRetroScalingCapability_t)GetProcAddress(hinstLib, "ctlGetSupportedRetroScalingCapability");
        if (pfnGetSupportedRetroScalingCapability)
        {
            result = pfnGetSupportedRetroScalingCapability(hDAhandle, pRetroScalingCaps);
        }
    }

    return result;
}


/**
* @brief Get/Set Retro Scaling
*
* @details
*     - Get or Set the status of retro scaling.This Api will do a physical
*       modeset resulting in flash on the screen
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pGetSetRetroScalingType`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetSetRetroScaling(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to adapter
    ctl_retro_scaling_settings_t* pGetSetRetroScalingType   ///< [in,out][release] Get or Set the retro scaling type
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetSetRetroScaling_t pfnGetSetRetroScaling = (ctl_pfnGetSetRetroScaling_t)GetProcAddress(hinstLib, "ctlGetSetRetroScaling");
        if (pfnGetSetRetroScaling)
        {
            result = pfnGetSetRetroScaling(hDAhandle, pGetSetRetroScalingType);
        }
    }

    return result;
}


/**
* @brief Get Supported Scaling Types
*
* @details
*     - Returns supported scaling capabilities
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pScalingCaps`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetSupportedScalingCapability(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_scaling_caps_t* pScalingCaps                ///< [in,out][release] Query result for supported scaling types
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetSupportedScalingCapability_t pfnGetSupportedScalingCapability = (ctl_pfnGetSupportedScalingCapability_t)GetProcAddress(hinstLib, "ctlGetSupportedScalingCapability");
        if (pfnGetSupportedScalingCapability)
        {
            result = pfnGetSupportedScalingCapability(hDisplayOutput, pScalingCaps);
        }
    }

    return result;
}


/**
* @brief Get Current Scaling
*
* @details
*     - Returns current active scaling
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pGetCurrentScalingType`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetCurrentScaling(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_scaling_settings_t* pGetCurrentScalingType  ///< [in,out][release] Query result for active scaling types
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetCurrentScaling_t pfnGetCurrentScaling = (ctl_pfnGetCurrentScaling_t)GetProcAddress(hinstLib, "ctlGetCurrentScaling");
        if (pfnGetCurrentScaling)
        {
            result = pfnGetCurrentScaling(hDisplayOutput, pGetCurrentScalingType);
        }
    }

    return result;
}


/**
* @brief Set Scaling Type
*
* @details
*     - Returns current active scaling
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pSetScalingType`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlSetCurrentScaling(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_scaling_settings_t* pSetScalingType         ///< [in,out][release] Set scaling types
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnSetCurrentScaling_t pfnSetCurrentScaling = (ctl_pfnSetCurrentScaling_t)GetProcAddress(hinstLib, "ctlSetCurrentScaling");
        if (pfnSetCurrentScaling)
        {
            result = pfnSetCurrentScaling(hDisplayOutput, pSetScalingType);
        }
    }

    return result;
}


/**
* @brief Get LACE Config
*
* @details
*     - Returns current LACE Config
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pLaceConfig`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_LACE_INVALID_DATA_ARGUMENT_PASSED - "Lace Incorrrect AggressivePercent data or LuxVsAggressive Map data passed by user"
*/
ctl_result_t CTL_APICALL
ctlGetLACEConfig(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in] Handle to display output
    ctl_lace_config_t* pLaceConfig                  ///< [out]Lace configuration
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetLACEConfig_t pfnGetLACEConfig = (ctl_pfnGetLACEConfig_t)GetProcAddress(hinstLib, "ctlGetLACEConfig");
        if (pfnGetLACEConfig)
        {
            result = pfnGetLACEConfig(hDisplayOutput, pLaceConfig);
        }
    }

    return result;
}


/**
* @brief Sets LACE Config
*
* @details
*     - Sets LACE Config
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pLaceConfig`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_LACE_INVALID_DATA_ARGUMENT_PASSED - "Lace Incorrrect AggressivePercent data or LuxVsAggressive Map data passed by user"
*/
ctl_result_t CTL_APICALL
ctlSetLACEConfig(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in]Handle to display output
    ctl_lace_config_t* pLaceConfig                  ///< [in]Lace configuration
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnSetLACEConfig_t pfnSetLACEConfig = (ctl_pfnSetLACEConfig_t)GetProcAddress(hinstLib, "ctlSetLACEConfig");
        if (pfnSetLACEConfig)
        {
            result = pfnSetLACEConfig(hDisplayOutput, pLaceConfig);
        }
    }

    return result;
}


/**
* @brief Get Software PSR caps/Set software PSR State
*
* @details
*     - Returns Software PSR status or Sets Software PSR capabilities. This is
*       a reserved capability. By default, software PSR is not supported/will
*       not be enabled, need application to activate it, please contact Intel
*       for activation.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pSoftwarePsrSetting`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlSoftwarePSR(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_sw_psr_settings_t* pSoftwarePsrSetting      ///< [in,out][release] Get Software PSR caps/state or Set Software PSR
                                                    ///< state
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnSoftwarePSR_t pfnSoftwarePSR = (ctl_pfnSoftwarePSR_t)GetProcAddress(hinstLib, "ctlSoftwarePSR");
        if (pfnSoftwarePSR)
        {
            result = pfnSoftwarePSR(hDisplayOutput, pSoftwarePsrSetting);
        }
    }

    return result;
}


/**
* @brief Get Intel Arc Sync information for monitor
*
* @details
*     - Returns Intel Arc Sync information for selected monitor
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pIntelArcSyncMonitorParams`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetIntelArcSyncInfoForMonitor(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_intel_arc_sync_monitor_params_t* pIntelArcSyncMonitorParams ///< [in,out][release] Intel Arc Sync params for monitor
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetIntelArcSyncInfoForMonitor_t pfnGetIntelArcSyncInfoForMonitor = (ctl_pfnGetIntelArcSyncInfoForMonitor_t)GetProcAddress(hinstLib, "ctlGetIntelArcSyncInfoForMonitor");
        if (pfnGetIntelArcSyncInfoForMonitor)
        {
            result = pfnGetIntelArcSyncInfoForMonitor(hDisplayOutput, pIntelArcSyncMonitorParams);
        }
    }

    return result;
}


/**
* @brief Enumerate Display MUX Devices on this system across adapters
*
* @details
*     - The application enumerates all MUX devices in the system
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hAPIHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pCount`
*         + `nullptr == phMuxDevices`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlEnumerateMuxDevices(
    ctl_api_handle_t hAPIHandle,                    ///< [in][release] Applications should pass the Control API handle returned
                                                    ///< by the CtlInit function 
    uint32_t* pCount,                               ///< [in,out][release] pointer to the number of MUX device instances. If
                                                    ///< input count is zero, then the api will update the value with the total
                                                    ///< number of MUX devices available and return the Count value. If input
                                                    ///< count is non-zero, then the api will only retrieve the number of MUX Devices.
                                                    ///< If count is larger than the number of MUX devices available, then the
                                                    ///< api will update the value with the correct number of MUX devices available.
    ctl_mux_output_handle_t* phMuxDevices           ///< [out][range(0, *pCount)] array of MUX device instance handles
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEnumerateMuxDevices_t pfnEnumerateMuxDevices = (ctl_pfnEnumerateMuxDevices_t)GetProcAddress(hinstLib, "ctlEnumerateMuxDevices");
        if (pfnEnumerateMuxDevices)
        {
            result = pfnEnumerateMuxDevices(hAPIHandle, pCount, phMuxDevices);
        }
    }

    return result;
}


/**
* @brief Get Display Mux properties
*
* @details
*     - Get the propeties of the Mux device
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hMuxDevice`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pMuxProperties`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetMuxProperties(
    ctl_mux_output_handle_t hMuxDevice,             ///< [in] MUX device instance handle
    ctl_mux_properties_t* pMuxProperties            ///< [in,out] MUX device properties
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetMuxProperties_t pfnGetMuxProperties = (ctl_pfnGetMuxProperties_t)GetProcAddress(hinstLib, "ctlGetMuxProperties");
        if (pfnGetMuxProperties)
        {
            result = pfnGetMuxProperties(hMuxDevice, pMuxProperties);
        }
    }

    return result;
}


/**
* @brief Switch Mux output
*
* @details
*     - Switches the MUX output
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hMuxDevice`
*         + `nullptr == hInactiveDisplayOutput`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlSwitchMux(
    ctl_mux_output_handle_t hMuxDevice,             ///< [in] MUX device instance handle
    ctl_display_output_handle_t hInactiveDisplayOutput  ///< [out] Input selection for this MUX, which if active will drive the
                                                    ///< output of this MUX device. This should be one of the display output
                                                    ///< handles reported under this MUX device's properties.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnSwitchMux_t pfnSwitchMux = (ctl_pfnSwitchMux_t)GetProcAddress(hinstLib, "ctlSwitchMux");
        if (pfnSwitchMux)
        {
            result = pfnSwitchMux(hMuxDevice, hInactiveDisplayOutput);
        }
    }

    return result;
}


/**
* @brief Get Intel Arc Sync profile
*
* @details
*     - Returns Intel Arc Sync profile for selected monitor
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pIntelArcSyncProfileParams`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetIntelArcSyncProfile(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_intel_arc_sync_profile_params_t* pIntelArcSyncProfileParams ///< [in,out][release] Intel Arc Sync params for monitor
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetIntelArcSyncProfile_t pfnGetIntelArcSyncProfile = (ctl_pfnGetIntelArcSyncProfile_t)GetProcAddress(hinstLib, "ctlGetIntelArcSyncProfile");
        if (pfnGetIntelArcSyncProfile)
        {
            result = pfnGetIntelArcSyncProfile(hDisplayOutput, pIntelArcSyncProfileParams);
        }
    }

    return result;
}


/**
* @brief Set Intel Arc Sync profile
*
* @details
*     - Sets Intel Arc Sync profile for selected monitor. In a mux situation,
*       this API should be called for all display IDs associated with a
*       physical display.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pIntelArcSyncProfileParams`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlSetIntelArcSyncProfile(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in][release] Handle to display output
    ctl_intel_arc_sync_profile_params_t* pIntelArcSyncProfileParams ///< [in][release] Intel Arc Sync params for monitor
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnSetIntelArcSyncProfile_t pfnSetIntelArcSyncProfile = (ctl_pfnSetIntelArcSyncProfile_t)GetProcAddress(hinstLib, "ctlSetIntelArcSyncProfile");
        if (pfnSetIntelArcSyncProfile)
        {
            result = pfnSetIntelArcSyncProfile(hDisplayOutput, pIntelArcSyncProfileParams);
        }
    }

    return result;
}


/**
* @brief EDID Management allows managing an output's EDID or Plugged Status.
*
* @details
*     - To manage output's EDID or Display ID. Supports native DP SST and HDMI
*       Display types.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDisplayOutput`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pEdidManagementArgs`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*     - ::CTL_RESULT_ERROR_INVALID_OPERATION_TYPE - "Invalid operation type"
*     - ::CTL_RESULT_ERROR_INVALID_NULL_POINTER - "Invalid null pointer"
*     - ::CTL_RESULT_ERROR_NULL_OS_DISPLAY_OUTPUT_HANDLE - "Null OS display output handle"
*     - ::CTL_RESULT_ERROR_NULL_OS_INTERFACE - "Null OS interface"
*     - ::CTL_RESULT_ERROR_NULL_OS_ADAPATER_HANDLE - "Null OS adapter handle"
*     - ::CTL_RESULT_ERROR_KMD_CALL - "Kernal mode driver call failure"
*     - ::CTL_RESULT_ERROR_INVALID_ARGUMENT - "Invalid combination of parameters"
*     - ::CTL_RESULT_ERROR_DISPLAY_NOT_ATTACHED - "Error for Output Device not attached"
*     - ::CTL_RESULT_ERROR_OUT_OF_DEVICE_MEMORY - "Insufficient device memory to satisfy call"
*     - ::CTL_RESULT_ERROR_DATA_NOT_FOUND - "Requested EDID data not present."
*/
ctl_result_t CTL_APICALL
ctlEdidManagement(
    ctl_display_output_handle_t hDisplayOutput,     ///< [in] Handle to display output
    ctl_edid_management_args_t* pEdidManagementArgs ///< [in,out] EDID management arguments
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEdidManagement_t pfnEdidManagement = (ctl_pfnEdidManagement_t)GetProcAddress(hinstLib, "ctlEdidManagement");
        if (pfnEdidManagement)
        {
            result = pfnEdidManagement(hDisplayOutput, pEdidManagementArgs);
        }
    }

    return result;
}


/**
* @brief Get handle of engine groups
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pCount`
*/
ctl_result_t CTL_APICALL
ctlEnumEngineGroups(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to adapter
    uint32_t* pCount,                               ///< [in,out] pointer to the number of components of this type.
                                                    ///< if count is zero, then the driver shall update the value with the
                                                    ///< total number of components of this type that are available.
                                                    ///< if count is greater than the number of components of this type that
                                                    ///< are available, then the driver shall update the value with the correct
                                                    ///< number of components.
    ctl_engine_handle_t* phEngine                   ///< [in,out][optional][range(0, *pCount)] array of handle of components of
                                                    ///< this type.
                                                    ///< if count is less than the number of components of this type that are
                                                    ///< available, then the driver shall only retrieve that number of
                                                    ///< component handles.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEnumEngineGroups_t pfnEnumEngineGroups = (ctl_pfnEnumEngineGroups_t)GetProcAddress(hinstLib, "ctlEnumEngineGroups");
        if (pfnEnumEngineGroups)
        {
            result = pfnEnumEngineGroups(hDAhandle, pCount, phEngine);
        }
    }

    return result;
}


/**
* @brief Get engine group properties
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hEngine`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pProperties`
*/
ctl_result_t CTL_APICALL
ctlEngineGetProperties(
    ctl_engine_handle_t hEngine,                    ///< [in] Handle for the component.
    ctl_engine_properties_t* pProperties            ///< [in,out] The properties for the specified engine group.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEngineGetProperties_t pfnEngineGetProperties = (ctl_pfnEngineGetProperties_t)GetProcAddress(hinstLib, "ctlEngineGetProperties");
        if (pfnEngineGetProperties)
        {
            result = pfnEngineGetProperties(hEngine, pProperties);
        }
    }

    return result;
}


/**
* @brief Get the activity stats for an engine group
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hEngine`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pStats`
*/
ctl_result_t CTL_APICALL
ctlEngineGetActivity(
    ctl_engine_handle_t hEngine,                    ///< [in] Handle for the component.
    ctl_engine_stats_t* pStats                      ///< [in,out] Will contain a snapshot of the engine group activity
                                                    ///< counters.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEngineGetActivity_t pfnEngineGetActivity = (ctl_pfnEngineGetActivity_t)GetProcAddress(hinstLib, "ctlEngineGetActivity");
        if (pfnEngineGetActivity)
        {
            result = pfnEngineGetActivity(hEngine, pStats);
        }
    }

    return result;
}


/**
* @brief Get handle of fans
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pCount`
*/
ctl_result_t CTL_APICALL
ctlEnumFans(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to the adapter
    uint32_t* pCount,                               ///< [in,out] pointer to the number of components of this type.
                                                    ///< if count is zero, then the driver shall update the value with the
                                                    ///< total number of components of this type that are available.
                                                    ///< if count is greater than the number of components of this type that
                                                    ///< are available, then the driver shall update the value with the correct
                                                    ///< number of components.
    ctl_fan_handle_t* phFan                         ///< [in,out][optional][range(0, *pCount)] array of handle of components of
                                                    ///< this type.
                                                    ///< if count is less than the number of components of this type that are
                                                    ///< available, then the driver shall only retrieve that number of
                                                    ///< component handles.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEnumFans_t pfnEnumFans = (ctl_pfnEnumFans_t)GetProcAddress(hinstLib, "ctlEnumFans");
        if (pfnEnumFans)
        {
            result = pfnEnumFans(hDAhandle, pCount, phFan);
        }
    }

    return result;
}


/**
* @brief Get fan properties
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFan`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pProperties`
*/
ctl_result_t CTL_APICALL
ctlFanGetProperties(
    ctl_fan_handle_t hFan,                          ///< [in] Handle for the component.
    ctl_fan_properties_t* pProperties               ///< [in,out] Will contain the properties of the fan.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFanGetProperties_t pfnFanGetProperties = (ctl_pfnFanGetProperties_t)GetProcAddress(hinstLib, "ctlFanGetProperties");
        if (pfnFanGetProperties)
        {
            result = pfnFanGetProperties(hFan, pProperties);
        }
    }

    return result;
}


/**
* @brief Get fan configurations and the current fan speed mode (default, fixed,
*        temp-speed table)
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFan`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pConfig`
*/
ctl_result_t CTL_APICALL
ctlFanGetConfig(
    ctl_fan_handle_t hFan,                          ///< [in] Handle for the component.
    ctl_fan_config_t* pConfig                       ///< [in,out] Will contain the current configuration of the fan.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFanGetConfig_t pfnFanGetConfig = (ctl_pfnFanGetConfig_t)GetProcAddress(hinstLib, "ctlFanGetConfig");
        if (pfnFanGetConfig)
        {
            result = pfnFanGetConfig(hFan, pConfig);
        }
    }

    return result;
}


/**
* @brief Configure the fan to run with hardware factory settings (set mode to
*        ::CTL_FAN_SPEED_MODE_DEFAULT)
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFan`
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS
*         + User does not have permissions to make these modifications.
*/
ctl_result_t CTL_APICALL
ctlFanSetDefaultMode(
    ctl_fan_handle_t hFan                           ///< [in] Handle for the component.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFanSetDefaultMode_t pfnFanSetDefaultMode = (ctl_pfnFanSetDefaultMode_t)GetProcAddress(hinstLib, "ctlFanSetDefaultMode");
        if (pfnFanSetDefaultMode)
        {
            result = pfnFanSetDefaultMode(hFan);
        }
    }

    return result;
}


/**
* @brief Configure the fan to rotate at a fixed speed (set mode to
*        ::CTL_FAN_SPEED_MODE_FIXED)
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFan`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == speed`
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS
*         + User does not have permissions to make these modifications.
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_FEATURE
*         + Fixing the fan speed not supported by the hardware or the fan speed units are not supported. See ::ctl_fan_properties_t.supportedModes and ::ctl_fan_properties_t.supportedUnits.
*/
ctl_result_t CTL_APICALL
ctlFanSetFixedSpeedMode(
    ctl_fan_handle_t hFan,                          ///< [in] Handle for the component.
    const ctl_fan_speed_t* speed                    ///< [in] The fixed fan speed setting
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFanSetFixedSpeedMode_t pfnFanSetFixedSpeedMode = (ctl_pfnFanSetFixedSpeedMode_t)GetProcAddress(hinstLib, "ctlFanSetFixedSpeedMode");
        if (pfnFanSetFixedSpeedMode)
        {
            result = pfnFanSetFixedSpeedMode(hFan, speed);
        }
    }

    return result;
}


/**
* @brief Configure the fan to adjust speed based on a temperature/speed table
*        (set mode to ::CTL_FAN_SPEED_MODE_TABLE)
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFan`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == speedTable`
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS
*         + User does not have permissions to make these modifications.
*     - ::CTL_RESULT_ERROR_INVALID_ARGUMENT
*         + The temperature/speed pairs in the array are not sorted on temperature from lowest to highest.
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_FEATURE
*         + Fan speed table not supported by the hardware or the fan speed units are not supported. See ::ctl_fan_properties_t.supportedModes and ::ctl_fan_properties_t.supportedUnits.
*/
ctl_result_t CTL_APICALL
ctlFanSetSpeedTableMode(
    ctl_fan_handle_t hFan,                          ///< [in] Handle for the component.
    const ctl_fan_speed_table_t* speedTable         ///< [in] A table containing temperature/speed pairs.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFanSetSpeedTableMode_t pfnFanSetSpeedTableMode = (ctl_pfnFanSetSpeedTableMode_t)GetProcAddress(hinstLib, "ctlFanSetSpeedTableMode");
        if (pfnFanSetSpeedTableMode)
        {
            result = pfnFanSetSpeedTableMode(hFan, speedTable);
        }
    }

    return result;
}


/**
* @brief Get current state of a fan - current mode and speed
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFan`
*     - CTL_RESULT_ERROR_INVALID_ENUMERATION
*         + `::CTL_FAN_SPEED_UNITS_PERCENT < units`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pSpeed`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_FEATURE
*         + The requested fan speed units are not supported. See ::ctl_fan_properties_t.supportedUnits.
*/
ctl_result_t CTL_APICALL
ctlFanGetState(
    ctl_fan_handle_t hFan,                          ///< [in] Handle for the component.
    ctl_fan_speed_units_t units,                    ///< [in] The units in which the fan speed should be returned.
    int32_t* pSpeed                                 ///< [in,out] Will contain the current speed of the fan in the units
                                                    ///< requested. A value of -1 indicates that the fan speed cannot be
                                                    ///< measured.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFanGetState_t pfnFanGetState = (ctl_pfnFanGetState_t)GetProcAddress(hinstLib, "ctlFanGetState");
        if (pfnFanGetState)
        {
            result = pfnFanGetState(hFan, units, pSpeed);
        }
    }

    return result;
}


/**
* @brief Get handle of frequency domains
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pCount`
*/
ctl_result_t CTL_APICALL
ctlEnumFrequencyDomains(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    uint32_t* pCount,                               ///< [in,out] pointer to the number of components of this type.
                                                    ///< if count is zero, then the driver shall update the value with the
                                                    ///< total number of components of this type that are available.
                                                    ///< if count is greater than the number of components of this type that
                                                    ///< are available, then the driver shall update the value with the correct
                                                    ///< number of components.
    ctl_freq_handle_t* phFrequency                  ///< [in,out][optional][range(0, *pCount)] array of handle of components of
                                                    ///< this type.
                                                    ///< if count is less than the number of components of this type that are
                                                    ///< available, then the driver shall only retrieve that number of
                                                    ///< component handles.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEnumFrequencyDomains_t pfnEnumFrequencyDomains = (ctl_pfnEnumFrequencyDomains_t)GetProcAddress(hinstLib, "ctlEnumFrequencyDomains");
        if (pfnEnumFrequencyDomains)
        {
            result = pfnEnumFrequencyDomains(hDAhandle, pCount, phFrequency);
        }
    }

    return result;
}


/**
* @brief Get frequency properties - available frequencies
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFrequency`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pProperties`
*/
ctl_result_t CTL_APICALL
ctlFrequencyGetProperties(
    ctl_freq_handle_t hFrequency,                   ///< [in] Handle for the component.
    ctl_freq_properties_t* pProperties              ///< [in,out] The frequency properties for the specified domain.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFrequencyGetProperties_t pfnFrequencyGetProperties = (ctl_pfnFrequencyGetProperties_t)GetProcAddress(hinstLib, "ctlFrequencyGetProperties");
        if (pfnFrequencyGetProperties)
        {
            result = pfnFrequencyGetProperties(hFrequency, pProperties);
        }
    }

    return result;
}


/**
* @brief Get available non-overclocked hardware clock frequencies for the
*        frequency domain
*
* @details
*     - The list of available frequencies is returned in order of slowest to
*       fastest.
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFrequency`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pCount`
*/
ctl_result_t CTL_APICALL
ctlFrequencyGetAvailableClocks(
    ctl_freq_handle_t hFrequency,                   ///< [in] Device handle of the device.
    uint32_t* pCount,                               ///< [in,out] pointer to the number of frequencies.
                                                    ///< if count is zero, then the driver shall update the value with the
                                                    ///< total number of frequencies that are available.
                                                    ///< if count is greater than the number of frequencies that are available,
                                                    ///< then the driver shall update the value with the correct number of frequencies.
    double* phFrequency                             ///< [in,out][optional][range(0, *pCount)] array of frequencies in units of
                                                    ///< MHz and sorted from slowest to fastest.
                                                    ///< if count is less than the number of frequencies that are available,
                                                    ///< then the driver shall only retrieve that number of frequencies.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFrequencyGetAvailableClocks_t pfnFrequencyGetAvailableClocks = (ctl_pfnFrequencyGetAvailableClocks_t)GetProcAddress(hinstLib, "ctlFrequencyGetAvailableClocks");
        if (pfnFrequencyGetAvailableClocks)
        {
            result = pfnFrequencyGetAvailableClocks(hFrequency, pCount, phFrequency);
        }
    }

    return result;
}


/**
* @brief Get current frequency limits
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFrequency`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pLimits`
*/
ctl_result_t CTL_APICALL
ctlFrequencyGetRange(
    ctl_freq_handle_t hFrequency,                   ///< [in] Handle for the component.
    ctl_freq_range_t* pLimits                       ///< [in,out] The range between which the hardware can operate for the
                                                    ///< specified domain.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFrequencyGetRange_t pfnFrequencyGetRange = (ctl_pfnFrequencyGetRange_t)GetProcAddress(hinstLib, "ctlFrequencyGetRange");
        if (pfnFrequencyGetRange)
        {
            result = pfnFrequencyGetRange(hFrequency, pLimits);
        }
    }

    return result;
}


/**
* @brief Set frequency range between which the hardware can operate.
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFrequency`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pLimits`
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS
*         + User does not have permissions to make these modifications.
*/
ctl_result_t CTL_APICALL
ctlFrequencySetRange(
    ctl_freq_handle_t hFrequency,                   ///< [in] Handle for the component.
    const ctl_freq_range_t* pLimits                 ///< [in] The limits between which the hardware can operate for the
                                                    ///< specified domain.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFrequencySetRange_t pfnFrequencySetRange = (ctl_pfnFrequencySetRange_t)GetProcAddress(hinstLib, "ctlFrequencySetRange");
        if (pfnFrequencySetRange)
        {
            result = pfnFrequencySetRange(hFrequency, pLimits);
        }
    }

    return result;
}


/**
* @brief Get current frequency state - frequency request, actual frequency, TDP
*        limits
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFrequency`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pState`
*/
ctl_result_t CTL_APICALL
ctlFrequencyGetState(
    ctl_freq_handle_t hFrequency,                   ///< [in] Handle for the component.
    ctl_freq_state_t* pState                        ///< [in,out] Frequency state for the specified domain.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFrequencyGetState_t pfnFrequencyGetState = (ctl_pfnFrequencyGetState_t)GetProcAddress(hinstLib, "ctlFrequencyGetState");
        if (pfnFrequencyGetState)
        {
            result = pfnFrequencyGetState(hFrequency, pState);
        }
    }

    return result;
}


/**
* @brief Get frequency throttle time
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hFrequency`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pThrottleTime`
*/
ctl_result_t CTL_APICALL
ctlFrequencyGetThrottleTime(
    ctl_freq_handle_t hFrequency,                   ///< [in] Handle for the component.
    ctl_freq_throttle_time_t* pThrottleTime         ///< [in,out] Will contain a snapshot of the throttle time counters for the
                                                    ///< specified domain.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnFrequencyGetThrottleTime_t pfnFrequencyGetThrottleTime = (ctl_pfnFrequencyGetThrottleTime_t)GetProcAddress(hinstLib, "ctlFrequencyGetThrottleTime");
        if (pfnFrequencyGetThrottleTime)
        {
            result = pfnFrequencyGetThrottleTime(hFrequency, pThrottleTime);
        }
    }

    return result;
}


/**
* @brief Get Video Processing capabilities
*
* @details
*     - The application gets Video Processing properties
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pFeatureCaps`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetSupportedVideoProcessingCapabilities(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    ctl_video_processing_feature_caps_t* pFeatureCaps   ///< [in,out][release] Video Processing properties
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetSupportedVideoProcessingCapabilities_t pfnGetSupportedVideoProcessingCapabilities = (ctl_pfnGetSupportedVideoProcessingCapabilities_t)GetProcAddress(hinstLib, "ctlGetSupportedVideoProcessingCapabilities");
        if (pfnGetSupportedVideoProcessingCapabilities)
        {
            result = pfnGetSupportedVideoProcessingCapabilities(hDAhandle, pFeatureCaps);
        }
    }

    return result;
}


/**
* @brief Get/Set Video Processing feature details
*
* @details
*     - Video Processing feature details
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pFeature`
*     - ::CTL_RESULT_ERROR_UNSUPPORTED_VERSION - "Unsupported version"
*/
ctl_result_t CTL_APICALL
ctlGetSetVideoProcessingFeature(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    ctl_video_processing_feature_getset_t* pFeature ///< [in][release] Video Processing feature get/set parameter
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnGetSetVideoProcessingFeature_t pfnGetSetVideoProcessingFeature = (ctl_pfnGetSetVideoProcessingFeature_t)GetProcAddress(hinstLib, "ctlGetSetVideoProcessingFeature");
        if (pfnGetSetVideoProcessingFeature)
        {
            result = pfnGetSetVideoProcessingFeature(hDAhandle, pFeature);
        }
    }

    return result;
}


/**
* @brief Get handle of memory modules
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pCount`
*/
ctl_result_t CTL_APICALL
ctlEnumMemoryModules(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    uint32_t* pCount,                               ///< [in,out] pointer to the number of components of this type.
                                                    ///< if count is zero, then the driver shall update the value with the
                                                    ///< total number of components of this type that are available.
                                                    ///< if count is greater than the number of components of this type that
                                                    ///< are available, then the driver shall update the value with the correct
                                                    ///< number of components.
    ctl_mem_handle_t* phMemory                      ///< [in,out][optional][range(0, *pCount)] array of handle of components of
                                                    ///< this type.
                                                    ///< if count is less than the number of components of this type that are
                                                    ///< available, then the driver shall only retrieve that number of
                                                    ///< component handles.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEnumMemoryModules_t pfnEnumMemoryModules = (ctl_pfnEnumMemoryModules_t)GetProcAddress(hinstLib, "ctlEnumMemoryModules");
        if (pfnEnumMemoryModules)
        {
            result = pfnEnumMemoryModules(hDAhandle, pCount, phMemory);
        }
    }

    return result;
}


/**
* @brief Get memory properties
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hMemory`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pProperties`
*/
ctl_result_t CTL_APICALL
ctlMemoryGetProperties(
    ctl_mem_handle_t hMemory,                       ///< [in] Handle for the component.
    ctl_mem_properties_t* pProperties               ///< [in,out] Will contain memory properties.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnMemoryGetProperties_t pfnMemoryGetProperties = (ctl_pfnMemoryGetProperties_t)GetProcAddress(hinstLib, "ctlMemoryGetProperties");
        if (pfnMemoryGetProperties)
        {
            result = pfnMemoryGetProperties(hMemory, pProperties);
        }
    }

    return result;
}


/**
* @brief Get memory state - health, allocated
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hMemory`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pState`
*/
ctl_result_t CTL_APICALL
ctlMemoryGetState(
    ctl_mem_handle_t hMemory,                       ///< [in] Handle for the component.
    ctl_mem_state_t* pState                         ///< [in,out] Will contain the current health and allocated memory.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnMemoryGetState_t pfnMemoryGetState = (ctl_pfnMemoryGetState_t)GetProcAddress(hinstLib, "ctlMemoryGetState");
        if (pfnMemoryGetState)
        {
            result = pfnMemoryGetState(hMemory, pState);
        }
    }

    return result;
}


/**
* @brief Get memory bandwidth
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hMemory`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pBandwidth`
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS
*         + User does not have permissions to query this telemetry.
*/
ctl_result_t CTL_APICALL
ctlMemoryGetBandwidth(
    ctl_mem_handle_t hMemory,                       ///< [in] Handle for the component.
    ctl_mem_bandwidth_t* pBandwidth                 ///< [in,out] Will contain the current health, free memory, total memory
                                                    ///< size.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnMemoryGetBandwidth_t pfnMemoryGetBandwidth = (ctl_pfnMemoryGetBandwidth_t)GetProcAddress(hinstLib, "ctlMemoryGetBandwidth");
        if (pfnMemoryGetBandwidth)
        {
            result = pfnMemoryGetBandwidth(hMemory, pBandwidth);
        }
    }

    return result;
}


/**
* @brief Get overclock properties - available properties.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pOcProperties`
*/
ctl_result_t CTL_APICALL
ctlOverclockGetProperties(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    ctl_oc_properties_t* pOcProperties              ///< [in,out] The overclocking properties for the specified domain.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockGetProperties_t pfnOverclockGetProperties = (ctl_pfnOverclockGetProperties_t)GetProcAddress(hinstLib, "ctlOverclockGetProperties");
        if (pfnOverclockGetProperties)
        {
            result = pfnOverclockGetProperties(hDeviceHandle, pOcProperties);
        }
    }

    return result;
}


/**
* @brief Overclock Waiver - Warranty Waiver.
*
* @details
*     - Most of the overclock functions will return an error if the waiver is
*       not set. This is because most overclock settings will increase the
*       electric/thermal stress on the part and thus reduce its lifetime.
*     - By setting the waiver, the user is indicate that they are accepting a
*       reduction in the lifetime of the part.
*     - It is the responsibility of overclock applications to notify each user
*       at least once with a popup of the dangers and requiring acceptance.
*     - Only once the user has accepted should this function be called by the
*       application.
*     - It is acceptable for the application to cache the user choice and call
*       this function on future executions without issuing the popup.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*/
ctl_result_t CTL_APICALL
ctlOverclockWaiverSet(
    ctl_device_adapter_handle_t hDeviceHandle       ///< [in][release] Handle to display adapter
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockWaiverSet_t pfnOverclockWaiverSet = (ctl_pfnOverclockWaiverSet_t)GetProcAddress(hinstLib, "ctlOverclockWaiverSet");
        if (pfnOverclockWaiverSet)
        {
            result = pfnOverclockWaiverSet(hDeviceHandle);
        }
    }

    return result;
}


/**
* @brief Get the Overclock Frequency Offset for the GPU in MHz.
*
* @details
*     - Determine the current frequency offset in effect (refer to
*       ::ctlOverclockGpuFrequencyOffsetSet() for details).
*     - The value returned may be different from the value that was previously
*       set by the application depending on hardware limitations or if the
*       function ::ctlOverclockGpuFrequencyOffsetSet() has been called or
*       another application that has changed the value.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pOcFrequencyOffset`
*/
ctl_result_t CTL_APICALL
ctlOverclockGpuFrequencyOffsetGet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double* pOcFrequencyOffset                      ///< [in,out] The Turbo Overclocking Frequency Desired in MHz.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockGpuFrequencyOffsetGet_t pfnOverclockGpuFrequencyOffsetGet = (ctl_pfnOverclockGpuFrequencyOffsetGet_t)GetProcAddress(hinstLib, "ctlOverclockGpuFrequencyOffsetGet");
        if (pfnOverclockGpuFrequencyOffsetGet)
        {
            result = pfnOverclockGpuFrequencyOffsetGet(hDeviceHandle, pOcFrequencyOffset);
        }
    }

    return result;
}


/**
* @brief Set the Overclock Frequency Offset for the GPU in MHZ.
*
* @details
*     - The purpose of this function is to increase/decrease the frequency at
*       which typical workloads will run within the same thermal budget.
*     - The frequency offset is expressed in units of ±1MHz.
*     - The actual operating frequency for each workload is not guaranteed to
*       change exactly by the specified offset.
*     - For positive frequency offsets, the factory maximum frequency may
*       increase by up to the specified amount.
*     - For negative frequency offsets, the overclock waiver must have been
*       set since this can result in running the part at voltages beyond the
*       part warrantee limits. An error is returned if the waiver has not been
*       set.
*     - Specifying large values for the frequency offset can lead to
*       instability. It is recommended that changes are made in small
*       increments and stability/performance measured running intense GPU
*       workloads before increasing further.
*     - This setting is not persistent through system reboots or driver
*       resets/hangs. It is up to the overclock application to reapply the
*       settings in those cases.
*     - This setting can cause system/device instability. It is up to the
*       overclock application to detect if the system has rebooted
*       unexpectedly or the device was restarted. When this occurs, the
*       application should not reapply the overclock settings automatically
*       but instead return to previously known good settings or notify the
*       user that the settings are not being applied.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*/
ctl_result_t CTL_APICALL
ctlOverclockGpuFrequencyOffsetSet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double ocFrequencyOffset                        ///< [in] The Turbo Overclocking Frequency Desired in MHz.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockGpuFrequencyOffsetSet_t pfnOverclockGpuFrequencyOffsetSet = (ctl_pfnOverclockGpuFrequencyOffsetSet_t)GetProcAddress(hinstLib, "ctlOverclockGpuFrequencyOffsetSet");
        if (pfnOverclockGpuFrequencyOffsetSet)
        {
            result = pfnOverclockGpuFrequencyOffsetSet(hDeviceHandle, ocFrequencyOffset);
        }
    }

    return result;
}


/**
* @brief Get the Overclock Gpu Voltage Offset in mV.
*
* @details
*     - Determine the current voltage offset in effect on the hardware (refer
*       to ::ctlOverclockGpuVoltageOffsetSet for details).
*     - The value returned may be different from the value that was previously
*       set by the application depending on hardware limitations or if the
*       function ::ctlOverclockGpuVoltageOffsetSet has been called or another
*       application that has changed the value.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pOcVoltageOffset`
*/
ctl_result_t CTL_APICALL
ctlOverclockGpuVoltageOffsetGet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double* pOcVoltageOffset                        ///< [in,out] The Turbo Overclocking Frequency Desired in mV.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockGpuVoltageOffsetGet_t pfnOverclockGpuVoltageOffsetGet = (ctl_pfnOverclockGpuVoltageOffsetGet_t)GetProcAddress(hinstLib, "ctlOverclockGpuVoltageOffsetGet");
        if (pfnOverclockGpuVoltageOffsetGet)
        {
            result = pfnOverclockGpuVoltageOffsetGet(hDeviceHandle, pOcVoltageOffset);
        }
    }

    return result;
}


/**
* @brief Set the Overclock Gpu Voltage Offset in mV.
*
* @details
*     - The purpose of this function is to attempt to run the GPU up to higher
*       voltages beyond the part warrantee limits. This can permit running at
*       even higher frequencies than can be obtained using the frequency
*       offset setting, but at the risk of reducing the lifetime of the part.
*     - The voltage offset is expressed in units of ±Volts with decimal
*       values permitted down to a resolution of 1 millivolt.
*     - The overclock waiver must be set before calling this function
*       otherwise and error will be returned.
*     - There is no guarantee that a workload can operate at the higher
*       frequencies permitted by this setting. Significantly more heat will be
*       generated at these high frequencies/voltages which will necessitate a
*       good cooling solution.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*/
ctl_result_t CTL_APICALL
ctlOverclockGpuVoltageOffsetSet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double ocVoltageOffset                          ///< [in] The Turbo Overclocking Frequency Desired in mV.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockGpuVoltageOffsetSet_t pfnOverclockGpuVoltageOffsetSet = (ctl_pfnOverclockGpuVoltageOffsetSet_t)GetProcAddress(hinstLib, "ctlOverclockGpuVoltageOffsetSet");
        if (pfnOverclockGpuVoltageOffsetSet)
        {
            result = pfnOverclockGpuVoltageOffsetSet(hDeviceHandle, ocVoltageOffset);
        }
    }

    return result;
}


/**
* @brief Gets the Locked GPU Voltage for Overclocking in mV.
*
* @details
*     - The purpose of this function is to determine if the current values of
*       the frequency/voltage lock.
*     - If the lock is not currently active, will return 0 for frequency and
*       voltage.
*     - Note that the operating frequency/voltage may be lower than these
*       settings if power/thermal limits are exceeded.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pVfPair`
*/
ctl_result_t CTL_APICALL
ctlOverclockGpuLockGet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    ctl_oc_vf_pair_t* pVfPair                       ///< [out] The current locked voltage and frequency.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockGpuLockGet_t pfnOverclockGpuLockGet = (ctl_pfnOverclockGpuLockGet_t)GetProcAddress(hinstLib, "ctlOverclockGpuLockGet");
        if (pfnOverclockGpuLockGet)
        {
            result = pfnOverclockGpuLockGet(hDeviceHandle, pVfPair);
        }
    }

    return result;
}


/**
* @brief Locks the GPU voltage for Overclocking in mV.
*
* @details
*     - The purpose of this function is to provide an interface for scanners
*       to lock the frequency and voltage to fixed values.
*     - The frequency is expressed in units of MHz with a resolution of 1MHz.
*     - The voltage is expressed in units of ±Volts with decimal values
*       permitted down to a resolution of 1 millivolt.
*     - The overclock waiver must be set since fixing the frequency at a high
*       value puts unnecessary stress on the part.
*     - The actual frequency may reduce depending on power/thermal
*       limitations.
*     - Requesting a frequency and/or voltage of 0 will return the hardware to
*       dynamic frequency/voltage management with any previous frequency
*       offset or voltage offset settings reapplied.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*/
ctl_result_t CTL_APICALL
ctlOverclockGpuLockSet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    ctl_oc_vf_pair_t vFPair                         ///< [in] The current locked voltage and frequency.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockGpuLockSet_t pfnOverclockGpuLockSet = (ctl_pfnOverclockGpuLockSet_t)GetProcAddress(hinstLib, "ctlOverclockGpuLockSet");
        if (pfnOverclockGpuLockSet)
        {
            result = pfnOverclockGpuLockSet(hDeviceHandle, vFPair);
        }
    }

    return result;
}


/**
* @brief Get the current Vram Frequency Offset in GT/s.
*
* @details
*     - The purpose of this function is to return the current VRAM frequency
*       offset in units of GT/s.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pOcFrequencyOffset`
*/
ctl_result_t CTL_APICALL
ctlOverclockVramFrequencyOffsetGet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double* pOcFrequencyOffset                      ///< [in,out] The current Memory Frequency in GT/s.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockVramFrequencyOffsetGet_t pfnOverclockVramFrequencyOffsetGet = (ctl_pfnOverclockVramFrequencyOffsetGet_t)GetProcAddress(hinstLib, "ctlOverclockVramFrequencyOffsetGet");
        if (pfnOverclockVramFrequencyOffsetGet)
        {
            result = pfnOverclockVramFrequencyOffsetGet(hDeviceHandle, pOcFrequencyOffset);
        }
    }

    return result;
}


/**
* @brief Set the desired Vram frquency Offset in GT/s
*
* @details
*     - The purpose of this function is to increase/decrease the frequency of
*       VRAM.
*     - The frequency offset is expressed in units of GT/s with a minimum step
*       size given by ::ctlOverclockGetProperties.
*     - The actual operating frequency for each workload is not guaranteed to
*       change exactly by the specified offset.
*     - The waiver must be set using clibOverclockWaiverSet() before this
*       function can be called.
*     - This setting is not persistent through system reboots or driver
*       resets/hangs. It is up to the overclock application to reapply the
*       settings in those cases.
*     - This setting can cause system/device instability. It is up to the
*       overclock application to detect if the system has rebooted
*       unexpectedly or the device was restarted. When this occurs, the
*       application should not reapply the overclock settings automatically
*       but instead return to previously known good settings or notify the
*       user that the settings are not being applied.
*     - If the memory controller doesn't support changes to frequency on the
*       fly, one of the following return codes will be given:
*     - ::CTL_RESULT_ERROR_RESET_DEVICE_REQUIRED: The requested memory
*       overclock will be applied when the device is reset or the system is
*       rebooted. In this case, the overclock software should check if the
*       overclock request was applied after the reset/reboot. If it was and
*       when the overclock application shuts down gracefully and if the
*       overclock application wants the setting to be persistent, the
*       application should request the same overclock settings again so that
*       they will be applied on the next reset/reboot. If this is not done,
*       then every time the device is reset and overclock is requested, the
*       device needs to be reset a second time.
*     - ::CTL_RESULT_ERROR_FULL_REBOOT_REQUIRED: The requested memory
*       overclock will be applied when the system is rebooted. In this case,
*       the overclock software should check if the overclock request was
*       applied after the reboot. If it was and when the overclock application
*       shuts down gracefully and if the overclock application wants the
*       setting to be persistent, the application should request the same
*       overclock settings again so that they will be applied on the next
*       reset/reboot. If this is not done and the overclock setting is
*       requested after the reboot has occurred, a second reboot will be
*       required.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*/
ctl_result_t CTL_APICALL
ctlOverclockVramFrequencyOffsetSet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double ocFrequencyOffset                        ///< [in] The desired Memory Frequency in GT/s.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockVramFrequencyOffsetSet_t pfnOverclockVramFrequencyOffsetSet = (ctl_pfnOverclockVramFrequencyOffsetSet_t)GetProcAddress(hinstLib, "ctlOverclockVramFrequencyOffsetSet");
        if (pfnOverclockVramFrequencyOffsetSet)
        {
            result = pfnOverclockVramFrequencyOffsetSet(hDeviceHandle, ocFrequencyOffset);
        }
    }

    return result;
}


/**
* @brief Get the Overclock Vram Voltage Offset in mV.
*
* @details
*     - The purpose of this function is to increase/decrease the voltage of
*       VRAM.
*     - The voltage offset is expressed in units of Volts with a minimum step
*       size given by ::ctlOverclockGetProperties.
*     - The waiver must be set using ::ctlOverclockWaiverSet before this
*       function can be called.
*     - This setting is not persistent through system reboots or driver
*       resets/hangs. It is up to the overclock application to reapply the
*       settings in those cases.
*     - This setting can cause system/device instability. It is up to the
*       overclock application to detect if the system has rebooted
*       unexpectedly or the device was restarted. When this occurs, the
*       application should not reapply the overclock settings automatically
*       but instead return to previously known good settings or notify the
*       user that the settings are not being applied.
*     - If the memory controller doesn't support changes to voltage on the
*       fly, one of the following return codes will be given:
*     - ::CTL_RESULT_ERROR_RESET_DEVICE_REQUIRED: The requested memory
*       overclock will be applied when the device is reset or the system is
*       rebooted. In this case, the overclock software should check if the
*       overclock request was applied after the reset/reboot. If it was and
*       when the overclock application shuts down gracefully and if the
*       overclock application wants the setting to be persistent, the
*       application should request the same overclock settings again so that
*       they will be applied on the next reset/reboot. If this is not done,
*       then every time the device is reset and overclock is requested, the
*       device needs to be reset a second time.
*     - ::CTL_RESULT_ERROR_FULL_REBOOT_REQUIRED: The requested memory
*       overclock will be applied when the system is rebooted. In this case,
*       the overclock software should check if the overclock request was
*       applied after the reboot. If it was and when the overclock application
*       shuts down gracefully and if the overclock application wants the
*       setting to be persistent, the application should request the same
*       overclock settings again so that they will be applied on the next
*       reset/reboot. If this is not done and the overclock setting is
*       requested after the reboot has occurred, a second reboot will be
*       required.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pVoltage`
*/
ctl_result_t CTL_APICALL
ctlOverclockVramVoltageOffsetGet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double* pVoltage                                ///< [out] The current locked voltage in mV.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockVramVoltageOffsetGet_t pfnOverclockVramVoltageOffsetGet = (ctl_pfnOverclockVramVoltageOffsetGet_t)GetProcAddress(hinstLib, "ctlOverclockVramVoltageOffsetGet");
        if (pfnOverclockVramVoltageOffsetGet)
        {
            result = pfnOverclockVramVoltageOffsetGet(hDeviceHandle, pVoltage);
        }
    }

    return result;
}


/**
* @brief Set the Overclock Vram Voltage Offset in mV.
*
* @details
*     - The purpose of this function is to set the maximum sustained power
*       limit. If the average GPU power averaged over a few seconds exceeds
*       this value, the frequency of the GPU will be throttled.
*     - Set a value of 0 to disable this power limit. In this case, the GPU
*       frequency will not throttle due to average power but may hit other
*       limits.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*/
ctl_result_t CTL_APICALL
ctlOverclockVramVoltageOffsetSet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double voltage                                  ///< [in] The voltage to be locked in mV.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockVramVoltageOffsetSet_t pfnOverclockVramVoltageOffsetSet = (ctl_pfnOverclockVramVoltageOffsetSet_t)GetProcAddress(hinstLib, "ctlOverclockVramVoltageOffsetSet");
        if (pfnOverclockVramVoltageOffsetSet)
        {
            result = pfnOverclockVramVoltageOffsetSet(hDeviceHandle, voltage);
        }
    }

    return result;
}


/**
* @brief Get the sustained power limit in mW.
*
* @details
*     - The purpose of this function is to read the current sustained power
*       limit.
*     - A value of 0 means that the limit is disabled - the GPU frequency can
*       run as high as possible until other limits are hit.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pSustainedPowerLimit`
*/
ctl_result_t CTL_APICALL
ctlOverclockPowerLimitGet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double* pSustainedPowerLimit                    ///< [in,out] The current sustained power limit in mW.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockPowerLimitGet_t pfnOverclockPowerLimitGet = (ctl_pfnOverclockPowerLimitGet_t)GetProcAddress(hinstLib, "ctlOverclockPowerLimitGet");
        if (pfnOverclockPowerLimitGet)
        {
            result = pfnOverclockPowerLimitGet(hDeviceHandle, pSustainedPowerLimit);
        }
    }

    return result;
}


/**
* @brief Set the sustained power limit in mW.
*
* @details
*     - The purpose of this function is to set the maximum sustained power
*       limit. If the average GPU power averaged over a few seconds exceeds
*       this value, the frequency of the GPU will be throttled.
*     - Set a value of 0 to disable this power limit. In this case, the GPU
*       frequency will not throttle due to average power but may hit other
*       limits.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*/
ctl_result_t CTL_APICALL
ctlOverclockPowerLimitSet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double sustainedPowerLimit                      ///< [in] The desired sustained power limit in mW.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockPowerLimitSet_t pfnOverclockPowerLimitSet = (ctl_pfnOverclockPowerLimitSet_t)GetProcAddress(hinstLib, "ctlOverclockPowerLimitSet");
        if (pfnOverclockPowerLimitSet)
        {
            result = pfnOverclockPowerLimitSet(hDeviceHandle, sustainedPowerLimit);
        }
    }

    return result;
}


/**
* @brief Get the current temperature limit in Celsius.
*
* @details
*     - The purpose of this function is to read the current thermal limit.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pTemperatureLimit`
*/
ctl_result_t CTL_APICALL
ctlOverclockTemperatureLimitGet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double* pTemperatureLimit                       ///< [in,out] The current temperature limit in Celsius.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockTemperatureLimitGet_t pfnOverclockTemperatureLimitGet = (ctl_pfnOverclockTemperatureLimitGet_t)GetProcAddress(hinstLib, "ctlOverclockTemperatureLimitGet");
        if (pfnOverclockTemperatureLimitGet)
        {
            result = pfnOverclockTemperatureLimitGet(hDeviceHandle, pTemperatureLimit);
        }
    }

    return result;
}


/**
* @brief Set the temperature limit in Celsius.
*
* @details
*     - The purpose of this function is to change the maximum thermal limit.
*       When the GPU temperature exceeds this value, the GPU frequency will be
*       throttled.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*/
ctl_result_t CTL_APICALL
ctlOverclockTemperatureLimitSet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    double temperatureLimit                         ///< [in] The desired temperature limit in Celsius.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnOverclockTemperatureLimitSet_t pfnOverclockTemperatureLimitSet = (ctl_pfnOverclockTemperatureLimitSet_t)GetProcAddress(hinstLib, "ctlOverclockTemperatureLimitSet");
        if (pfnOverclockTemperatureLimitSet)
        {
            result = pfnOverclockTemperatureLimitSet(hDeviceHandle, temperatureLimit);
        }
    }

    return result;
}


/**
* @brief Get Power Telemetry.
*
* @details
*     - Limited rate of 50 ms, any call under 50 ms will return the same
*       information.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDeviceHandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pTelemetryInfo`
*/
ctl_result_t CTL_APICALL
ctlPowerTelemetryGet(
    ctl_device_adapter_handle_t hDeviceHandle,      ///< [in][release] Handle to display adapter
    ctl_power_telemetry_t* pTelemetryInfo           ///< [out] The overclocking properties for the specified domain.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnPowerTelemetryGet_t pfnPowerTelemetryGet = (ctl_pfnPowerTelemetryGet_t)GetProcAddress(hinstLib, "ctlPowerTelemetryGet");
        if (pfnPowerTelemetryGet)
        {
            result = pfnPowerTelemetryGet(hDeviceHandle, pTelemetryInfo);
        }
    }

    return result;
}


/**
* @brief Get PCI properties - address, max speed
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pProperties`
*/
ctl_result_t CTL_APICALL
ctlPciGetProperties(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    ctl_pci_properties_t* pProperties               ///< [in,out] Will contain the PCI properties.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnPciGetProperties_t pfnPciGetProperties = (ctl_pfnPciGetProperties_t)GetProcAddress(hinstLib, "ctlPciGetProperties");
        if (pfnPciGetProperties)
        {
            result = pfnPciGetProperties(hDAhandle, pProperties);
        }
    }

    return result;
}


/**
* @brief Get current PCI state - current speed
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pState`
*/
ctl_result_t CTL_APICALL
ctlPciGetState(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    ctl_pci_state_t* pState                         ///< [in,out] Will contain the PCI properties.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnPciGetState_t pfnPciGetState = (ctl_pfnPciGetState_t)GetProcAddress(hinstLib, "ctlPciGetState");
        if (pfnPciGetState)
        {
            result = pfnPciGetState(hDAhandle, pState);
        }
    }

    return result;
}


/**
* @brief Get handle of power domains
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pCount`
*/
ctl_result_t CTL_APICALL
ctlEnumPowerDomains(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    uint32_t* pCount,                               ///< [in,out] pointer to the number of components of this type.
                                                    ///< if count is zero, then the driver shall update the value with the
                                                    ///< total number of components of this type that are available.
                                                    ///< if count is greater than the number of components of this type that
                                                    ///< are available, then the driver shall update the value with the correct
                                                    ///< number of components.
    ctl_pwr_handle_t* phPower                       ///< [in,out][optional][range(0, *pCount)] array of handle of components of
                                                    ///< this type.
                                                    ///< if count is less than the number of components of this type that are
                                                    ///< available, then the driver shall only retrieve that number of
                                                    ///< component handles.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEnumPowerDomains_t pfnEnumPowerDomains = (ctl_pfnEnumPowerDomains_t)GetProcAddress(hinstLib, "ctlEnumPowerDomains");
        if (pfnEnumPowerDomains)
        {
            result = pfnEnumPowerDomains(hDAhandle, pCount, phPower);
        }
    }

    return result;
}


/**
* @brief Get properties related to a power domain
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hPower`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pProperties`
*/
ctl_result_t CTL_APICALL
ctlPowerGetProperties(
    ctl_pwr_handle_t hPower,                        ///< [in] Handle for the component.
    ctl_power_properties_t* pProperties             ///< [in,out] Structure that will contain property data.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnPowerGetProperties_t pfnPowerGetProperties = (ctl_pfnPowerGetProperties_t)GetProcAddress(hinstLib, "ctlPowerGetProperties");
        if (pfnPowerGetProperties)
        {
            result = pfnPowerGetProperties(hPower, pProperties);
        }
    }

    return result;
}


/**
* @brief Get energy counter
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hPower`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pEnergy`
*/
ctl_result_t CTL_APICALL
ctlPowerGetEnergyCounter(
    ctl_pwr_handle_t hPower,                        ///< [in] Handle for the component.
    ctl_power_energy_counter_t* pEnergy             ///< [in,out] Will contain the latest snapshot of the energy counter and
                                                    ///< timestamp when the last counter value was measured.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnPowerGetEnergyCounter_t pfnPowerGetEnergyCounter = (ctl_pfnPowerGetEnergyCounter_t)GetProcAddress(hinstLib, "ctlPowerGetEnergyCounter");
        if (pfnPowerGetEnergyCounter)
        {
            result = pfnPowerGetEnergyCounter(hPower, pEnergy);
        }
    }

    return result;
}


/**
* @brief Get power limits
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hPower`
*/
ctl_result_t CTL_APICALL
ctlPowerGetLimits(
    ctl_pwr_handle_t hPower,                        ///< [in] Handle for the component.
    ctl_power_limits_t* pPowerLimits                ///< [in,out][optional] Structure that will contain the power limits.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnPowerGetLimits_t pfnPowerGetLimits = (ctl_pfnPowerGetLimits_t)GetProcAddress(hinstLib, "ctlPowerGetLimits");
        if (pfnPowerGetLimits)
        {
            result = pfnPowerGetLimits(hPower, pPowerLimits);
        }
    }

    return result;
}


/**
* @brief Set power limits
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hPower`
*     - ::CTL_RESULT_ERROR_INSUFFICIENT_PERMISSIONS
*         + User does not have permissions to make these modifications.
*     - ::CTL_RESULT_ERROR_NOT_AVAILABLE
*         + The device is in use, meaning that the GPU is under Over clocking, applying power limits under overclocking is not supported.
*/
ctl_result_t CTL_APICALL
ctlPowerSetLimits(
    ctl_pwr_handle_t hPower,                        ///< [in] Handle for the component.
    const ctl_power_limits_t* pPowerLimits          ///< [in][optional] Structure that will contain the power limits.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnPowerSetLimits_t pfnPowerSetLimits = (ctl_pfnPowerSetLimits_t)GetProcAddress(hinstLib, "ctlPowerSetLimits");
        if (pfnPowerSetLimits)
        {
            result = pfnPowerSetLimits(hPower, pPowerLimits);
        }
    }

    return result;
}


/**
* @brief Get handle of temperature sensors
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hDAhandle`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pCount`
*/
ctl_result_t CTL_APICALL
ctlEnumTemperatureSensors(
    ctl_device_adapter_handle_t hDAhandle,          ///< [in][release] Handle to display adapter
    uint32_t* pCount,                               ///< [in,out] pointer to the number of components of this type.
                                                    ///< if count is zero, then the driver shall update the value with the
                                                    ///< total number of components of this type that are available.
                                                    ///< if count is greater than the number of components of this type that
                                                    ///< are available, then the driver shall update the value with the correct
                                                    ///< number of components.
    ctl_temp_handle_t* phTemperature                ///< [in,out][optional][range(0, *pCount)] array of handle of components of
                                                    ///< this type.
                                                    ///< if count is less than the number of components of this type that are
                                                    ///< available, then the driver shall only retrieve that number of
                                                    ///< component handles.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnEnumTemperatureSensors_t pfnEnumTemperatureSensors = (ctl_pfnEnumTemperatureSensors_t)GetProcAddress(hinstLib, "ctlEnumTemperatureSensors");
        if (pfnEnumTemperatureSensors)
        {
            result = pfnEnumTemperatureSensors(hDAhandle, pCount, phTemperature);
        }
    }

    return result;
}


/**
* @brief Get temperature sensor properties
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hTemperature`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pProperties`
*/
ctl_result_t CTL_APICALL
ctlTemperatureGetProperties(
    ctl_temp_handle_t hTemperature,                 ///< [in] Handle for the component.
    ctl_temp_properties_t* pProperties              ///< [in,out] Will contain the temperature sensor properties.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnTemperatureGetProperties_t pfnTemperatureGetProperties = (ctl_pfnTemperatureGetProperties_t)GetProcAddress(hinstLib, "ctlTemperatureGetProperties");
        if (pfnTemperatureGetProperties)
        {
            result = pfnTemperatureGetProperties(hTemperature, pProperties);
        }
    }

    return result;
}


/**
* @brief Get the temperature from a specified sensor
*
* @details
*     - The application may call this function from simultaneous threads.
*     - The implementation of this function should be lock-free.
*
* @returns
*     - CTL_RESULT_SUCCESS
*     - CTL_RESULT_ERROR_UNINITIALIZED
*     - CTL_RESULT_ERROR_DEVICE_LOST
*     - CTL_RESULT_ERROR_INVALID_NULL_HANDLE
*         + `nullptr == hTemperature`
*     - CTL_RESULT_ERROR_INVALID_NULL_POINTER
*         + `nullptr == pTemperature`
*/
ctl_result_t CTL_APICALL
ctlTemperatureGetState(
    ctl_temp_handle_t hTemperature,                 ///< [in] Handle for the component.
    double* pTemperature                            ///< [in,out] Will contain the temperature read from the specified sensor
                                                    ///< in degrees Celsius.
)
{
    ctl_result_t result = CTL_RESULT_ERROR_NOT_INITIALIZED;


    if (NULL != hinstLib)
    {
        ctl_pfnTemperatureGetState_t pfnTemperatureGetState = (ctl_pfnTemperatureGetState_t)GetProcAddress(hinstLib, "ctlTemperatureGetState");
        if (pfnTemperatureGetState)
        {
            result = pfnTemperatureGetState(hTemperature, pTemperature);
        }
    }

    return result;
}


//
// End of wrapper function implementation
//
/////////////////////////////////////////////////////////////////////////////////