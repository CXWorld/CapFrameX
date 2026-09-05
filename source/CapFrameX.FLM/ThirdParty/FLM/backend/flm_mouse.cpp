//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_mouse.cpp
/// @brief  FLM code interface to mouse
//=============================================================================

#include "flm_mouse.h"
#include "flm_utils.h"


std::string FLM_Mouse::GetErrorMessage()
{
    return m_errorMessage;
}

void FLM_Mouse::PrintMessage(const char* Format, ...)
{
    // define a pointer to save argument list
    va_list args;
    char    buff[1024];

    // process the arguments into our debug buffer
    va_start(args, Format);
#ifdef _WIN32
    vsprintf_s(buff, Format, args);
#else
    vsprint(buff, 1024, Format, args);
#endif
    va_end(args);

    m_errorMessage = buff;
}


bool FLM_Mouse::IsButtonDown(uint8_t key)
{
    if (KEY_DOWN(key))
        return true;
    return false;
}

bool FLM_Mouse::IsButtonPressed(uint8_t key)
{
    if (IsButtonDown(key))
    {
        while (IsButtonDown(key));
        return true;
    }
    return false;
}
