//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_keyboard.cpp
/// @brief  FLM code interface to keyboard
//=============================================================================

#include "flm_keyboard.h"
#include "flm_utils.h"

VK_KEY_PAIRS vk_key_pairs[VK_KEY_PAIRS_SIZE] = {{VK_MENU, "ALT"},      {VK_CONTROL, "CTRL"},   {VK_SHIFT, "SHIFT"},    {VK_LMENU, "LALT"},
                                                {VK_RMENU, "RALT"},    {VK_LCONTROL, "LCTRL"}, {VK_RCONTROL, "RCTRL"}, {VK_LSHIFT, "LSHIFT"},
                                                {VK_RSHIFT, "RSHIFT"}, {VK_RETURN, "ENTER"},   {VK_LEFT, "LEFT"},      {VK_UP, "UP"},
                                                {VK_RIGHT, "RIGHT"},   {VK_DOWN, "DOWN"},      {VK_ADD, "ADD"},        {VK_SUBTRACT, "SUBTRACT"},
                                                {VK_F1, "F1"},         {VK_F2, "F2"},          {VK_F3, "F3"},          {VK_F4, "F4"},
                                                {VK_F5, "F5"},         {VK_F6, "F6"},          {VK_F7, "F7"},          {VK_F8, "F8"},
                                                {VK_F9, "F9"},         {VK_F10, "F10"},        {VK_F11, "F11"},        {VK_F12, "F12"}};

std::string FLM_Keyboard::GetErrorMessage()
{
    return m_errorMessage;
}

void FLM_Keyboard::PrintMessage(const char* Format, ...)
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

std::string FLM_Keyboard::GetVKCodeKeyName(uint8_t vkCode)
{
#define STRING_SIZE 256
    char lpString[STRING_SIZE];
    UINT sc = MapVirtualKeyW((UINT)vkCode, 0);

    LPARAM lParam;
    // if using extended keys add 0x1 << 24
    lParam = (LPARAM)sc << 16;
    lParam |= (LPARAM)0x1 << 25;
    if (GetKeyNameTextA((LONG)lParam, lpString, STRING_SIZE) != 0)
    {
        return (lpString);
    }

    return ("");
}

std::string FLM_Keyboard::GetKeyName(uint8_t vkCode)
{
    for (int i = 0; i < VK_KEY_PAIRS_SIZE; i++)
    {
        if (vk_key_pairs[i].key == vkCode)
        {
            return (vk_key_pairs[i].keyname);
        }
    }
    // Other keys not listed, mostly single char unless user assigned unsupported flame keys
    return (GetVKCodeKeyName(vkCode));
}

bool FLM_Keyboard::AssignKey(std::string token, uint8_t& key)
{
    if (token.size() == 1)
    {
        key = token[0];
        return true;
    }

    for (int i = 0; i < VK_KEY_PAIRS_SIZE; i++)
    {
        if (token.compare(vk_key_pairs[i].keyname) == 0)
        {
            key = vk_key_pairs[i].key;
            return true;
        }
    }
    return false;
}

bool FLM_Keyboard::SetKeys(std::string userKey, uint8_t key[3])
{
    std::string inputKey = userKey;
    key[0] = key[1] = key[2] = 0;

    // convert to upper
    std::transform(inputKey.begin(), inputKey.end(), inputKey.begin(), ::toupper);

    std::string delimiter = "+";
    int         pos       = (int)inputKey.find(delimiter);

    // Single key
    if (pos <= 0)
    {
        if (AssignKey(inputKey, key[0]) == false)
        {
            PrintMessage("Unknown key assignment [%s] in [%s]\n", inputKey.c_str(), userKey.c_str());
            return false;
        }
        return true;
    }

    // More then 1 key
    std::string token[3] = {"", "", ""};
    token[0]             = inputKey.substr(0, pos);
    std::string temp     = inputKey.substr(pos + delimiter.size(), inputKey.size());
    pos                  = (int)temp.find(delimiter);
    // check for any 3rd key
    if (pos >= 0)
    {
        token[1] = temp.substr(0, pos);
        token[2] = temp.substr(pos + delimiter.size(), temp.size());
    }
    else
    {
        token[1] = temp;
    }

    for (int i = 0; i < 3; i++)
    {
        if (token[i].size() > 0)
        {
            if (AssignKey(token[i], key[i]) == false)
            {
                PrintMessage("Unknown key assignment [%s] in [%s]\n", token[i].c_str(), userKey.c_str());
                return false;
            }
        }
        else
        {
            if (i < 1)
            {
                PrintMessage("Error minimum of 2 key sequence is required in [%s]\n", userKey.c_str());
                return false;
            }
        }
    }

    return true;
}

bool FLM_Keyboard::KeyCombinationPressed(uint8_t keys[3])
{
    // Will remain true as long as the user holds the keys down
    if (KEY_DOWN(keys[0]))
    {
        if (keys[2] > 0)
            return (KEY_DOWN(keys[0]) && KEY_DOWN(keys[1]) && KEY_DOWN(keys[2]));
        else if (keys[1] > 0)
            return (KEY_DOWN(keys[0]) && KEY_DOWN(keys[1]));
        return true;
    }

    return false;
}

void FLM_Keyboard::ClearKeyboardBuffer()
{
    while (_kbhit())
    {
        int ch = _getch();
    }
}


bool FLM_Keyboard::AnyKeyboardHit()
{
    return _kbhit() > 0;
}
