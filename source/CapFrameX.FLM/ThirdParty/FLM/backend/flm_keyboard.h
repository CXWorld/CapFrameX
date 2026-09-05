//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_keyboard.h
/// @brief  FLM interface to set and get keyboard keys
//=============================================================================

#ifndef FLM_KEYBOARD_H
#define FLM_KEYBOARD_H

#include <Windows.h>
#include <algorithm>
#include <conio.h>
#include <string>

struct VK_KEY_PAIRS
{
    uint8_t     key;
    const char* keyname;
};

#define VK_KEY_PAIRS_SIZE 28

class FLM_Keyboard
{
public:
    bool        AnyKeyboardHit();
    bool        AssignKey(std::string token, uint8_t& key);
    void        ClearKeyboardBuffer();
    std::string GetErrorMessage();
    std::string GetKeyName(uint8_t vkCode);
    bool        KeyCombinationPressed(uint8_t keys[3]);
    bool        SetKeys(std::string userKey, uint8_t key[3]);

private:
    std::string m_errorMessage;

    std::string GetVKCodeKeyName(uint8_t vkCode);
    void        PrintMessage(const char* Format, ...);
};

#endif
