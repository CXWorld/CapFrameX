//
// Copyright (c) 2021 - 2025 Advanced Micro Devices, Inc. All rights reserved.
//
//-------------------------------------------------------------------------------------------------

#ifndef ADLX_ISMARTACCESSMEMORY_H
#define ADLX_ISMARTACCESSMEMORY_H
#pragma once

#include "ADLXStructures.h"

//-------------------------------------------------------------------------------------------------
//ISmartAccessMemory.h - Interfaces for ADLX AMD SmartAccess Memory functionality

// AMD SmartAccess Memory interface
#pragma region IADLXSmartAccessMemory
#if defined (__cplusplus)
namespace adlx
{
	class ADLX_NO_VTABLE IADLXSmartAccessMemory : public IADLXInterface
	{
	public:
		ADLX_DECLARE_IID(L"IADLXSmartAccessMemory")

		/**
		*@page DOX_IADLXSmartAccessMemory_IsSupported IsSupported
		*@ENG_START_DOX @brief Checks if AMD SmartAccess Memory is supported on a GPU. @ENG_END_DOX
		*
		*@syntax
		*@codeStart
		* @ref ADLX_RESULT    IsSupported (adlx_bool* supported)
		*@codeEnd
		*
		*@params
		* @paramrow{1.,[out],supported,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of AMD SmartAccess Memory is returned. The variable is __true__ if AMD SmartAccess Memory is supported. The variable is __false__ if AMD SmartAccess Memory is not supported. @ENG_END_DOX}
		*
		*@retvalues
		*@ENG_START_DOX  If the state of AMD SmartAccess Memory is successfully returned, __ADLX_OK__ is returned. <br>
		* If the state of AMD SmartAccess Memory is not successfully returned, an error code is returned. <br>
		* Refer to @ref ADLX_RESULT for success codes and error codes. @ENG_END_DOX
		*
		*@copydoc IADLXSmartAccessMemory_REQ_TABLE
		*
		*/
		virtual ADLX_RESULT ADLX_STD_CALL IsSupported(adlx_bool* supported) = 0;

		/**
		*@page DOX_IADLXSmartAccessMemory_IsEnabled IsEnabled
		*@ENG_START_DOX @brief Checks if AMD SmartAccess Memory is enabled on a GPU. @ENG_END_DOX
		*
		*@syntax
		*@codeStart
		* @ref ADLX_RESULT    IsEnabled (adlx_bool* enabled)
		*@codeEnd
		*
		*@params
		* @paramrow{1.,[out],enabled,adlx_bool*,@ENG_START_DOX The pointer to a variable where the state of AMD SmartAccess Memory is returned. The variable is __true__ if AMD SmartAccess Memory is enabled. The variable is __false__ if AMD SmartAccess Memory is not enabled. @ENG_END_DOX}
		*
		*@retvalues
		*@ENG_START_DOX  If the state of AMD SmartAccess Memory is successfully returned, __ADLX_OK__ is returned.<br>
		* If the state of AMD SmartAccess Memory is not successfully returned, an error code is returned.<br>
		* Refer to @ref ADLX_RESULT for success codes and error codes.<br> @ENG_END_DOX
		*
		*@copydoc IADLXSmartAccessMemory_REQ_TABLE
		*
		*/
		virtual ADLX_RESULT ADLX_STD_CALL IsEnabled(adlx_bool* enabled) = 0;
			
		/**
		*@page DOX_IADLXSmartAccessMemory_SetEnabled SetEnabled
		*@ENG_START_DOX @brief This method is deprecated. To enable or disable the state of the AMD SmartAccess Memory, enable or disable Re-Size BAR Support into the system BIOS.  @ENG_END_DOX
		*
		*@syntax
		*@codeStart
		* @ref ADLX_RESULT    SetEnabled (adlx_bool enable)
		*@codeEnd
		*
		*@params
		* @paramrow{1.,[in],enable,adlx_bool,@ENG_START_DOX The new AMD SmartAccess Memory state. Set __true__ to enable AMD SmartAccess Memory. Set __false__ to disable AMD SmartAccess Memory. @ENG_END_DOX}
		*
		*@retvalues
		*@ENG_START_DOX ADLX_NOT_SUPPORTED @ENG_END_DOX
		*
		*@copydoc IADLXSmartAccessMemory_REQ_TABLE
		*
		*/
		virtual ADLX_RESULT ADLX_STD_CALL SetEnabled(adlx_bool enabled) = 0;
	};
	//----------------------------------------------------------------------------------------------
	typedef IADLXInterfacePtr_T<IADLXSmartAccessMemory> IADLXSmartAccessMemoryPtr;
} //namespace adlx
#else //__cplusplus
ADLX_DECLARE_IID(IADLXSmartAccessMemory, L"IADLXSmartAccessMemory")

typedef struct IADLXSmartAccessMemory IADLXSmartAccessMemory;

typedef struct IADLXSmartAccessMemoryVtbl
{
	//IADLXInterface
	adlx_long(ADLX_STD_CALL* Acquire)(IADLXSmartAccessMemory* pThis);
	adlx_long(ADLX_STD_CALL* Release)(IADLXSmartAccessMemory* pThis);
	ADLX_RESULT(ADLX_STD_CALL* QueryInterface)(IADLXSmartAccessMemory* pThis, const wchar_t* interfaceId, void** ppInterface);

	//IADLXSmartAccessMemory
	ADLX_RESULT(ADLX_STD_CALL* IsSupported)(IADLXSmartAccessMemory* pThis, adlx_bool* supported);
	ADLX_RESULT(ADLX_STD_CALL* IsEnabled)(IADLXSmartAccessMemory* pThis, adlx_bool* enabled);
	ADLX_RESULT(ADLX_STD_CALL* SetEnabled)(IADLXSmartAccessMemory* pThis, adlx_bool enabled);
} IADLXSmartAccessMemoryVtbl;

struct IADLXSmartAccessMemory { const IADLXSmartAccessMemoryVtbl* pVtbl; };
#endif
#pragma endregion IADLXSmartAccessMemory

#endif // !ADLX_ISMARTACCESSMEMORY_H
