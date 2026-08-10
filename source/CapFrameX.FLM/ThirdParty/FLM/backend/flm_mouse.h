//=============================================================================
// Copyright (c) 2023 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_mouse.h
/// @brief  FLM interface to get mouse button states
//=============================================================================

#ifndef FLAME_MOUSE_H
#define FLAME_MOUSE_H

#include <Windows.h>
#include <algorithm>
#include <conio.h>
#include <string>

#define MOUSE_LEFT_BUTTON    VK_LBUTTON
#define MOUSE_MIDDLE_BUTTON  VK_MBUTTON
#define MOUSE_RIGHT_BUTTON   VK_RBUTTON


class FLM_Mouse
{
public:
    bool IsButtonDown(uint8_t key);
    bool IsButtonPressed(uint8_t key);
    std::string GetErrorMessage();

private:
    std::string m_errorMessage;
    void PrintMessage(const char* Format, ...);
};

#endif
