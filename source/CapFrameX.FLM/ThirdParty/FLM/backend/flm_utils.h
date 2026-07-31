//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_utils.h
/// @brief  FLM Common API
//=============================================================================
#ifndef FLM_UTILS_H
#define FLM_UTILS_H

#include "Windows.h"
#include "string"
#include "vector"

#define KEY_DOWN(key) ((GetAsyncKeyState(key) & 0x8000) != 0)
#define IF_CONDITION_BECOMES_TRUE(condition) { bool kd = (condition); static bool pr_kd = kd; if (kd && !pr_kd) bDo = true; else bDo = false; pr_kd = kd; } if( bDo )

extern std::string FlmFormatStr(const char* Format, ...);
extern void        FlmPrint(const char* Format, ...);
extern void        FlmPrintError(const char* Format, ...);
extern void        FlmPrintStaticPos(const char* Format, ...);
extern void        FlmPrintClearEndOfLine();
extern std::string FlmGetErrorStr();
extern HANDLE      FlmGetConsoleHandle();
extern void        FlmClearErrorStr();
extern bool        FlmIsFloatNumber(const std::string& string);
#endif
