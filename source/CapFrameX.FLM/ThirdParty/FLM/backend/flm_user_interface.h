//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_user_interface.h
/// @brief  FLM UI Options used by console application
//=============================================================================
#ifndef FLM_USER_INTERFACE_H
#define FLM_USER_INTERFACE_H

#include <windows.h>
#include "windowsx.h"
#include "stdio.h"
#include <tchar.h>
#include <climits>
#include <algorithm>

#include "flm.h"
#include "flm_utils.h"

#define HIDE_MINIMIZE_APP_TOGGLE

#define IDC_BUTTON_CLOSE 1600
#define IDC_BUTTON_SAVE  1601

#define IDC_GRPBUTTONS 1700
#define IDC_RADIO_DISPLAY_RUN 1701
#define IDC_RADIO_DISPLAY_AVERAGE 1702
#define IDC_RADIO_DISPLAY_OPERATIONAL 1703
#define IDC_CAPTURE_REGION 1706

#define IDC_RADIO_MOUSE_MOVE 1801
#define IDC_RADIO_MOUSE_CLICK 1802
#define IDC_EDIT_BIAS 1803
#define IDC_CHECK_AUTO_BIAS 1804
#define IDC_EDIT_THRESHOLD 1805
#define IDC_CHECK_MINIMIZE_APP 1806
#define IDC_CHECK_FRAME_GEN 1807

#define FLM_OPTIONS_WINDOW_HEIGHT 325
#define FLM_OPTIONS_WINDOW_WIDTH 450
#define MAX_EDIT_TXT 20

struct FLM_UI_SETTINGS
{
    HWND                 hWndConsole            = NULL;
    HWND                 hWndUser               = NULL;
    FLM_RUNTIME_OPTIONS* runtimeOptions         = NULL;
    HWND                 hWndLatencyDisplay[3]  = {};
    HWND                 hWndMouseClick[2]      = {};
    HWND                 hWndButton             = NULL;
    HBRUSH               hbrBkgnd               = NULL;
    HBRUSH               hbrBkgndCaptureRegion  = NULL;
    HWND                 hWndRegion             = NULL;
    HWND                 hWndEditBias           = NULL;
    HWND                 hWndEditThreshold      = NULL;
    bool                 processWindowRegion    = false;
    bool                 ui_showing             = false;
    bool                 capture_region_showing = false;
    HWND                 hWndAutoBias           = NULL;
    HWND                 hWndMinimizeApp        = NULL;
    HWND                 hWndGameHasFG          = NULL;
    INT                  maxWidth               = 4096;
    INT                  maxHeight              = 4096;
    char                 editTextBias[MAX_EDIT_TXT];
    char                 editTextThreshold[MAX_EDIT_TXT];
};

class FLM_USER_INTERFACE
{
public:
    int  ThreadMain();
    void Init(FLM_RUNTIME_OPTIONS* displayOption, HANDLE hUserInterfaceThread);
    void ShowUI(int x, int y);
    void HideUI();
    void CloseUI();

private:
    void   UpdateRunTimeOptions();
    HANDLE m_hUserInterfaceThread;
};

extern FLM_UI_SETTINGS g_ui;
extern FLM_USER_INTERFACE g_user_interface;

#endif
