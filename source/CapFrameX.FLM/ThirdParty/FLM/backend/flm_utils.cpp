//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_utils.h
/// @brief  FLM Common API
//=============================================================================

#include "flm_utils.h"

static std::vector<std::string> g_flm_error_message;  // shared across multiple classes and accessible in FLM_Pipeline class
static HANDLE                   g_hConsoleOutput = GetStdHandle(STD_OUTPUT_HANDLE);

// Last In Fist Out (LIFO) instance of an errors, remove the errors from list until empty
std::string FlmGetErrorStr()
{
    std::string flm_err;
    if (g_flm_error_message.size() > 0)
    {
        flm_err = g_flm_error_message[g_flm_error_message.size() - 1];
        g_flm_error_message.pop_back();
    }
    else
        flm_err = "";
    return flm_err.c_str();
}

void FlmClearErrorStr()
{
    g_flm_error_message.clear();
}

HANDLE FlmGetConsoleHandle()
{
    return g_hConsoleOutput;
}

std::string FlmFormatStr(const char* Format, ...)
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

    return (buff);
}

void FlmPrint(const char* Format, ...)
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

    // select stream output
    printf(buff);
}

void FlmPrintError(const char* Format, ...)
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

    g_flm_error_message.push_back(buff);

    // Use FLMPrint for this, if its a none exit print message
    // printf("FLM Error: ");
    // printf(buff);
    // printf("\n");
}

void FlmPrintStaticPos(const char* Format, ...)
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

    CONSOLE_SCREEN_BUFFER_INFO ConsoleScreenBufferInfo;
    CONSOLE_CURSOR_INFO        cursorInfo;
    GetConsoleCursorInfo(g_hConsoleOutput, &cursorInfo);
    cursorInfo.bVisible = false;
    SetConsoleCursorInfo(g_hConsoleOutput, &cursorInfo);

    if (GetConsoleScreenBufferInfo(g_hConsoleOutput, &ConsoleScreenBufferInfo))
    {
        COORD coord;
        coord.X = ConsoleScreenBufferInfo.dwCursorPosition.X;
        coord.Y = ConsoleScreenBufferInfo.dwCursorPosition.Y;

        printf(buff);
        SetConsoleCursorPosition(g_hConsoleOutput, coord);
    }
    else
        printf(buff);

    cursorInfo.bVisible = true;
    SetConsoleCursorInfo(g_hConsoleOutput, &cursorInfo);
}

void FlmPrintClearEndOfLine()
{
    CONSOLE_SCREEN_BUFFER_INFO ConsoleScreenBufferInfo;

    if (GetConsoleScreenBufferInfo(g_hConsoleOutput, &ConsoleScreenBufferInfo))
    {
        COORD coord = {};
        coord.X     = ConsoleScreenBufferInfo.dwCursorPosition.X;
        coord.Y     = ConsoleScreenBufferInfo.dwCursorPosition.Y;
        int len;
        if ((ConsoleScreenBufferInfo.dwMaximumWindowSize.X - ConsoleScreenBufferInfo.dwCursorPosition.X) > 0)
            len = ConsoleScreenBufferInfo.dwMaximumWindowSize.X - ConsoleScreenBufferInfo.dwCursorPosition.X;
        else
            len = 1;
        std::string sformat = "%";
        std::string spaces  = std::to_string(len);
        sformat.append(spaces);
        sformat.append("s");
        printf(sformat.c_str(), " ");
        SetConsoleCursorPosition(g_hConsoleOutput, coord);
    }
}

bool FlmIsFloatNumber(const std::string& string)
{
    if (string.length() < 1)
        return false;

    if (string.length() == 1)
    {
        if (string[0] == '.')
            return false;
    }

    bool decimalPoint = false;
    std::string::const_iterator it = string.begin();
    if (string.length() > 0)
    {
        if ((string[0] == '+') || (string[0] == '-'))
            it++;
    }

    while(it != string.end()){
      if(*it == '.')
      {
        if(!decimalPoint) decimalPoint = true;
        else break;
      }
      else 
      if(!std::isdigit(*it))
        break;
      ++it;
    }

    return (it == string.end());
}
