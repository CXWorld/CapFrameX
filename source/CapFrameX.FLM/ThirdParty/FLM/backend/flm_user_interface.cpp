//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_user_interface.h
/// @brief  FLM UI Options used by console application
//=============================================================================
#include "flm_user_interface.h"

FLM_UI_SETTINGS g_ui;
FLM_USER_INTERFACE g_user_interface;

static WNDPROC InputWndProcOriginal = NULL;

LRESULT CALLBACK InputPositiveFloatWndProc(HWND hwnd, unsigned int msg, WPARAM wParam, LPARAM lParam)
{
    if (msg == WM_CHAR)
    {
        // Make sure we only allow specific characters
        if (!((wParam >= '0' && wParam <= '9') || wParam == '.' || wParam == VK_RETURN || wParam == VK_DELETE || wParam == VK_BACK))
        {
            return 0;
        }
    }

    return CallWindowProc(InputWndProcOriginal, hwnd, msg, wParam, lParam);
}


extern FLM_USER_INTERFACE g_user_interface;
// Function to handle and call each event method for a specific Windows Message
LRESULT CALLBACK WndProcUI(HWND hWnd, unsigned int msg, WPARAM wParam, LPARAM lParam)
{
    static bool editFloatBiasOk        = false;
    static bool editFloatThresholdOk   = false;
    LRESULT     result                 = 0;

    switch (msg)
    {
    case WM_KEYDOWN: if( wParam == VK_ESCAPE )
                        g_user_interface.HideUI();
                     break;
    case WM_COMMAND:
    {
        if (g_ui.runtimeOptions)
        {
            // unused notifyCode = HIWORD(wParam)
            unsigned short idc_buttons = LOWORD(wParam);
            switch (idc_buttons)
            {
            case IDC_RADIO_DISPLAY_RUN:
                g_ui.runtimeOptions->printLevel = FLM_PRINT_LEVEL::RUN;
                break;
            case IDC_RADIO_DISPLAY_AVERAGE:
                g_ui.runtimeOptions->printLevel = FLM_PRINT_LEVEL::ACCUMULATED;
                break;
            case IDC_RADIO_DISPLAY_OPERATIONAL:
                g_ui.runtimeOptions->printLevel = FLM_PRINT_LEVEL::OPERATIONAL;
                break;
            case IDC_RADIO_MOUSE_MOVE:
            case IDC_RADIO_MOUSE_CLICK:
            {
                if (idc_buttons == IDC_RADIO_MOUSE_MOVE)
                    g_ui.runtimeOptions->mouseEventType = FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE;
                else
                    g_ui.runtimeOptions->mouseEventType = FLM_MOUSE_EVENT_TYPE::MOUSE_CLICK;
                float       threshold               = g_ui.runtimeOptions->thresholdCoefficient[g_ui.runtimeOptions->mouseEventType];
                std::string str                     = FlmFormatStr("%3.1f", threshold);
                SetWindowText(g_ui.hWndEditThreshold, str.c_str());
            }
            break;
            case IDC_CHECK_AUTO_BIAS:
                g_ui.runtimeOptions->autoBias = !g_ui.runtimeOptions->autoBias;
                if (g_ui.hWndAutoBias)
                    SendMessage(g_ui.hWndAutoBias, BM_SETCHECK, g_ui.runtimeOptions->autoBias ? TRUE : FALSE, NULL);
                if (g_ui.hWndEditBias)
                    SendMessage(g_ui.hWndEditBias, EM_SETREADONLY, g_ui.runtimeOptions->autoBias ? TRUE : FALSE, NULL);
                break;
            case IDC_CHECK_MINIMIZE_APP:
                g_ui.runtimeOptions->minimizeApp = !g_ui.runtimeOptions->minimizeApp;
                if (g_ui.hWndMinimizeApp)
                    SendMessage(g_ui.hWndMinimizeApp, BM_SETCHECK, g_ui.runtimeOptions->minimizeApp ? TRUE : FALSE, NULL);
                break;
            case IDC_CHECK_FRAME_GEN:
                g_ui.runtimeOptions->gameUsesFrameGeneration = !g_ui.runtimeOptions->gameUsesFrameGeneration;
                if (g_ui.hWndGameHasFG)
                    SendMessage(g_ui.hWndGameHasFG, BM_SETCHECK, g_ui.runtimeOptions->gameUsesFrameGeneration ? TRUE : FALSE, NULL);
                break;
            case IDC_CAPTURE_REGION:
            {
                if (g_ui.capture_region_showing)
                {
                    g_ui.processWindowRegion = false;
                    ShowWindow(g_ui.hWndRegion, SW_HIDE);
                }
                else
                {
                    SetWindowPos(g_ui.hWndRegion,
                                 HWND_TOPMOST,
                                 g_ui.runtimeOptions->iCaptureX,
                                 g_ui.runtimeOptions->iCaptureY,
                                 g_ui.runtimeOptions->iCaptureWidth,
                                 g_ui.runtimeOptions->iCaptureHeight,
                                 SWP_DRAWFRAME);
                    g_ui.processWindowRegion = true;
                    ShowWindow(g_ui.hWndRegion, SW_SHOW);
                }
                g_ui.capture_region_showing = !g_ui.capture_region_showing;
            }
            break;
            case IDC_EDIT_BIAS:
            {
                int len = GetWindowTextLengthW(g_ui.hWndEditBias) + 1;
                if ((len > 1) && (len < MAX_EDIT_TXT))
                {
                    // assign the new text for processing
                    GetWindowTextA(g_ui.hWndEditBias, g_ui.editTextBias, len);
                    if (g_ui.runtimeOptions)
                    {
                        std::string str = g_ui.editTextBias;
                        editFloatBiasOk = FlmIsFloatNumber(str);
                        if (editFloatBiasOk)
                        {
                            g_ui.runtimeOptions->biasOffset = std::stof(str);
                        }
                    }
                }
            }
            break;
            case IDC_EDIT_THRESHOLD:
            {
                int len = GetWindowTextLengthW(g_ui.hWndEditThreshold) + 1;
                if ((len > 1) && (len < MAX_EDIT_TXT))
                {
                    // assign the new text for processing
                    GetWindowTextA(g_ui.hWndEditThreshold, g_ui.editTextThreshold, len);
                    if (g_ui.runtimeOptions)
                    {
                        std::string str = g_ui.editTextThreshold;
                        editFloatThresholdOk = FlmIsFloatNumber(str);
                        if (editFloatThresholdOk)
                        {
                            g_ui.runtimeOptions->thresholdCoefficient[g_ui.runtimeOptions->mouseEventType] = std::stof(str);
                        }

                    }
                }
            }
            break;
            case IDC_BUTTON_CLOSE:
                g_user_interface.HideUI();
                break;
            case IDC_BUTTON_SAVE:
                g_ui.runtimeOptions->saveUserSettings = true;
                break;
            };
        }
    }
    break;

    case WM_CREATE:
    {
        HINSTANCE hInst = ((LPCREATESTRUCT)lParam)->hInstance;
        // Group box
        CreateWindowEx(WS_EX_WINDOWEDGE,
                       "BUTTON",
                       " FLM Latency Display Mode ",
                       WS_VISIBLE | WS_CHILD | BS_GROUPBOX,
                       10,
                       10,
                       FLM_OPTIONS_WINDOW_WIDTH - 20,
                       100,
                       hWnd,
                       (HMENU)IDC_GRPBUTTONS,
                       hInst,
                       NULL);
        {  // Radio buttons group
            g_ui.hWndLatencyDisplay[0] = CreateWindowEx(WS_EX_WINDOWEDGE,
                                                        "BUTTON",
                                                        "Run latency measurements using small samples",
                                                        WS_VISIBLE | WS_CHILD | BS_AUTORADIOBUTTON | WS_GROUP | WS_TABSTOP,
                                                        20,
                                                        35,
                                                        FLM_OPTIONS_WINDOW_WIDTH - 40,
                                                        20,
                                                        hWnd,
                                                        (HMENU)IDC_RADIO_DISPLAY_RUN,
                                                        hInst,
                                                        NULL);

            g_ui.hWndLatencyDisplay[1] = CreateWindowEx(WS_EX_WINDOWEDGE,
                                                        "BUTTON",
                                                        "Continuously accumulated measurement",
                                                        WS_VISIBLE | WS_CHILD | BS_AUTORADIOBUTTON,
                                                        20,
                                                        60,
                                                        FLM_OPTIONS_WINDOW_WIDTH - 40,
                                                        20,
                                                        hWnd,
                                                        (HMENU)IDC_RADIO_DISPLAY_AVERAGE,
                                                        hInst,
                                                        NULL);
            g_ui.hWndLatencyDisplay[2] = CreateWindowEx(WS_EX_WINDOWEDGE,
                                                        "BUTTON",
                                                        "Show all measurements per line",
                                                        WS_VISIBLE | WS_CHILD | BS_AUTORADIOBUTTON,
                                                        20,
                                                        85,
                                                        FLM_OPTIONS_WINDOW_WIDTH - 40,
                                                        20,
                                                        hWnd,
                                                        (HMENU)IDC_RADIO_DISPLAY_OPERATIONAL,
                                                        hInst,
                                                        NULL);
        }

        // Group box
        CreateWindowEx(WS_EX_WINDOWEDGE,
                       "BUTTON",
                       " Measurement Using ",
                       WS_VISIBLE | WS_CHILD | BS_GROUPBOX,
                       10,
                       120,
                       FLM_OPTIONS_WINDOW_WIDTH - 20,
                       64,
                       hWnd,
                       (HMENU)IDC_GRPBUTTONS,
                       hInst,
                       NULL);
        {  // Radio buttons group

            g_ui.hWndMouseClick[0] = CreateWindowEx(WS_EX_WINDOWEDGE,
                                                    "BUTTON",
                                                    "Mouse Move",
                                                    WS_VISIBLE | WS_CHILD | BS_AUTORADIOBUTTON | WS_GROUP | WS_TABSTOP,
                                                    20,
                                                    140,
                                                    125,
                                                    20,
                                                    hWnd,
                                                    (HMENU)IDC_RADIO_MOUSE_MOVE,
                                                    hInst,
                                                    NULL);
            g_ui.hWndMouseClick[1] = CreateWindowEx(WS_EX_WINDOWEDGE,
                                                    "BUTTON",
                                                    "Mouse Click",
                                                    WS_VISIBLE | WS_CHILD | BS_AUTORADIOBUTTON,
                                                    20,
                                                    160,
                                                    120,
                                                    20,
                                                    hWnd,
                                                    (HMENU)IDC_RADIO_MOUSE_CLICK,
                                                    hInst,
                                                    NULL);
        }

        g_ui.hWndEditBias = CreateWindowEx(WS_EX_WINDOWEDGE,
                                           "EDIT",
                                           NULL,
                                           WS_BORDER | WS_CHILD | WS_VISIBLE | ES_LEFT,  // | BS_TEXT,  //WS_VSCROLL
                                           160,
                                           160,
                                           50,
                                           20,
                                           hWnd,
                                           (HMENU)IDC_EDIT_BIAS,
                                           hInst,
                                           NULL);

        InputWndProcOriginal = (WNDPROC)SetWindowLongPtr(g_ui.hWndEditBias, GWLP_WNDPROC, (LONG_PTR)InputPositiveFloatWndProc);

        g_ui.hWndAutoBias = CreateWindowEx(
            WS_EX_WINDOWEDGE, "BUTTON", "Auto", WS_VISIBLE | WS_CHILD | BS_CHECKBOX, 300, 160, 65, 20, hWnd, (HMENU)IDC_CHECK_AUTO_BIAS, hInst, NULL);

        // Group box
        CreateWindowEx(WS_EX_WINDOWEDGE,
                       "BUTTON",
                       " Frame Capture Setting ",
                       WS_VISIBLE | WS_CHILD | BS_GROUPBOX,
                       10,
                       190,
                       FLM_OPTIONS_WINDOW_WIDTH - 20,
                       50,
                       hWnd,
                       (HMENU)IDC_GRPBUTTONS,
                       hInst,
                       NULL);
        {  // Capture group

            g_ui.hWndButton = CreateWindowEx(WS_EX_WINDOWEDGE,
                                             "BUTTON",
                                             "Set Capture Region",
                                             WS_VISIBLE | WS_CHILD | BS_DEFPUSHBUTTON,
                                             20,
                                             210,
                                             165,
                                             25,
                                             hWnd,
                                             (HMENU)IDC_CAPTURE_REGION,
                                             hInst,
                                             NULL);

            g_ui.hWndEditThreshold = CreateWindowEx(WS_EX_WINDOWEDGE,
                                                    "EDIT",
                                                    NULL,
                                                    WS_BORDER | WS_CHILD | WS_VISIBLE | ES_LEFT,  // | BS_TEXT,  //WS_VSCROLL
                                                    200,
                                                    210,
                                                    50,
                                                    20,
                                                    hWnd,
                                                    (HMENU)IDC_EDIT_THRESHOLD,
                                                    hInst,
                                                    NULL);

            SetWindowLongPtr(g_ui.hWndEditThreshold, GWLP_WNDPROC, (LONG_PTR)InputPositiveFloatWndProc);
        }

        #if !defined(HIDE_MINIMIZE_APP_TOGGLE)
            g_ui.hWndMinimizeApp = CreateWindowEx(
                WS_EX_WINDOWEDGE, "BUTTON", "Minimize window during measurements", WS_VISIBLE | WS_CHILD | BS_CHECKBOX, 10, FLM_OPTIONS_WINDOW_HEIGHT - 55, FLM_OPTIONS_WINDOW_WIDTH-60, 20, hWnd, (HMENU)IDC_CHECK_MINIMIZE_APP, hInst, NULL);
        #endif

        g_ui.hWndGameHasFG = CreateWindowEx(
            WS_EX_WINDOWEDGE, "BUTTON", "Game uses Frame Generation", WS_VISIBLE | WS_CHILD | BS_CHECKBOX, 10, FLM_OPTIONS_WINDOW_HEIGHT - 75, FLM_OPTIONS_WINDOW_WIDTH-60, 20, hWnd, (HMENU)IDC_CHECK_FRAME_GEN, hInst, NULL);

        CreateWindowEx(WS_EX_WINDOWEDGE,
                       "BUTTON",
                       "Save settings to INI",
                       WS_VISIBLE | WS_CHILD | BS_DEFPUSHBUTTON,
                       10,
                       FLM_OPTIONS_WINDOW_HEIGHT - 30,
                       165,
                       25,
                       hWnd,
                       (HMENU)IDC_BUTTON_SAVE,
                       hInst,
                       NULL);


        CreateWindowEx(WS_EX_WINDOWEDGE,
                       "BUTTON",
                       "Close",
                       WS_VISIBLE | WS_CHILD | BS_DEFPUSHBUTTON,
                       FLM_OPTIONS_WINDOW_WIDTH  - 65,
                       FLM_OPTIONS_WINDOW_HEIGHT - 30,
                       55,
                       25,
                       hWnd,
                       (HMENU)IDC_BUTTON_CLOSE,
                       hInst,
                       NULL);
    }
    break;
    case WM_PAINT:  // Called once
    {
        HDC         hdc;
        PAINTSTRUCT ps;
        hdc = BeginPaint(hWnd, &ps);
        if (hdc)
        {
            SetTextColor(hdc, RGB(255, 255, 255));
            SetBkColor(hdc, RGB(50, 50, 50));
            std::string str = "bias (ms) ";
            TextOut(hdc, 220, 160, str.c_str(), (INT)str.length());
            str = "Threshold";
            TextOut(hdc, 260, 210, str.c_str(), (INT)str.length());
        }
        EndPaint(hWnd, &ps);
    }
    break;
    case WM_CTLCOLOREDIT:
    {
        HDC  hdcEdit = (HDC)wParam;
        HWND WNDEdit = WindowFromDC(hdcEdit);
        if (WNDEdit == g_ui.hWndEditBias)
        {
            if (editFloatBiasOk)
                SetTextColor(hdcEdit, RGB(0, 0, 0));
            else
                SetTextColor(hdcEdit, RGB(255, 0, 0));
        }
        if (WNDEdit == g_ui.hWndEditThreshold)
        {
            if (editFloatThresholdOk)
                SetTextColor(hdcEdit, RGB(0, 0, 0));
            else
                SetTextColor(hdcEdit, RGB(255, 0, 0));
        }
    }
    break;
    case WM_CTLCOLORSTATIC:
    {
        HDC hdcStatic = (HDC)wParam;
        SetTextColor(hdcStatic, RGB(255, 255, 255));
        SetBkColor(hdcStatic, RGB(50, 50, 50));
        if (g_ui.hbrBkgnd == NULL)
        {
            g_ui.hbrBkgnd = CreateSolidBrush(RGB(50, 50, 50));
        }
        return (INT_PTR)g_ui.hbrBkgnd;
    }
    break;
    case WM_NCHITTEST:
    {
        LRESULT position = DefWindowProc(hWnd, msg, wParam, lParam);
        return position == HTCLIENT ? HTCAPTION : position;
    }
    break;
    case WM_QUIT:
    case WM_DESTROY:
        if (g_ui.hbrBkgnd != NULL)
        {
            DeleteObject(g_ui.hbrBkgnd);
            g_ui.hbrBkgnd = NULL;
        }
        break;
    default:
        result = DefWindowProc(hWnd, msg, wParam, lParam);
        break;
    }

    return result;
}

#define BORDERWIDTH 10

// Function to handle and call each event method for a specific Windows Message
LRESULT CALLBACK WndProcRegion(HWND hWnd, unsigned int msg, WPARAM wParam, LPARAM lParam)
{
    LRESULT result = 0;

    switch (msg)
    {
    case WM_MOVE:
    {
        if (g_ui.processWindowRegion)  // enabled only when user selected Set Capture Region
        {
            INT xPos = (int)(short)LOWORD(lParam);
            INT yPos = (int)(short)HIWORD(lParam);

            if (xPos >= 8)
                xPos = (xPos / 8) * 8;  // 8 bit bound
            else
                xPos = 0;

            if (yPos < 0)
                yPos = 0;

            if (g_ui.runtimeOptions)
            {
                // Clip region within screen res
                // if ((yPos + g_ui.runtimeOptions->captureHeight) > g_ui.maxHeight)
                //     yPos = g_ui.runtimeOptions->captureY;
                // if ((xPos + g_ui.runtimeOptions->captureWidth) > g_ui.maxWidth)
                //     xPos = g_ui.runtimeOptions->captureX;
                // update
                g_ui.runtimeOptions->iCaptureX             = xPos;
                g_ui.runtimeOptions->iCaptureY             = yPos;
                g_ui.runtimeOptions->captureRegionChanged = true;

                // Reposition select window to updated boundary
                SetWindowPos(g_ui.hWndRegion,
                             HWND_TOPMOST,
                             g_ui.runtimeOptions->iCaptureX,
                             g_ui.runtimeOptions->iCaptureY,
                             g_ui.runtimeOptions->iCaptureWidth,
                             g_ui.runtimeOptions->iCaptureHeight,
                             SWP_DRAWFRAME);
            }
        }
    }
    break;
    case WM_SIZE:
    {
        if (g_ui.processWindowRegion)  // enabled only when user selected Set Capture Region
        {
            INT width  = LOWORD(lParam);
            INT height = HIWORD(lParam);

            if (width < 64)
                width = 64;
            else
                width = (width / 8) * 8;  // 8 bit bound

            if (height < 64)
                height = 64;

            if (g_ui.runtimeOptions)
            {
                if ((width + g_ui.runtimeOptions->iCaptureX) < g_ui.maxWidth)
                    g_ui.runtimeOptions->iCaptureWidth = width;

                if ((height + g_ui.runtimeOptions->iCaptureY) < g_ui.maxHeight)
                    g_ui.runtimeOptions->iCaptureHeight = height;

                g_ui.runtimeOptions->captureRegionChanged = true;
                // Set GUI to updated boundary
                SetWindowPos(g_ui.hWndRegion,
                             HWND_TOPMOST,
                             g_ui.runtimeOptions->iCaptureX,
                             g_ui.runtimeOptions->iCaptureY,
                             g_ui.runtimeOptions->iCaptureWidth,
                             g_ui.runtimeOptions->iCaptureHeight,
                             SWP_DRAWFRAME);
            }
        }
    }
    break;
    case WM_CTLCOLORSTATIC:
    {
        HDC hdcStatic = (HDC)wParam;
        SetTextColor(hdcStatic, RGB(255, 255, 255));
        SetBkColor(hdcStatic, RGB(50, 50, 50));
        if (g_ui.hbrBkgndCaptureRegion == NULL)
        {
            g_ui.hbrBkgndCaptureRegion = CreateSolidBrush(RGB(50, 50, 50));
        }
        return (INT_PTR)g_ui.hbrBkgndCaptureRegion;
    }
    break;
    case WM_DESTROY:
        if (g_ui.hbrBkgndCaptureRegion != NULL)
        {
            DeleteObject(g_ui.hbrBkgndCaptureRegion);
            g_ui.hbrBkgndCaptureRegion = NULL;
        }
        break;
    case WM_NCHITTEST:
    {
        POINT pt;
        pt.x = LOWORD(lParam);
        pt.y = HIWORD(lParam);

        RECT rc;
        GetClientRect(hWnd, &rc);
        ScreenToClient(hWnd, &pt);

        if (pt.y < BORDERWIDTH)
        {
            if (pt.x < BORDERWIDTH)
            {
                return HTTOPLEFT;
            }
            else if (pt.x > (rc.right - BORDERWIDTH))
            {
                return HTTOPRIGHT;
            }
            return HTTOP;
        }
        if (pt.y > (rc.bottom - BORDERWIDTH))
        {
            if (pt.x < BORDERWIDTH)
            {
                return HTBOTTOMLEFT;
            }
            else if (pt.x > (rc.right - BORDERWIDTH))
            {
                return HTBOTTOMRIGHT;
            }
            return HTBOTTOM;
        }
        if (pt.x < BORDERWIDTH)
        {
            return HTLEFT;
        }
        if (pt.x > (rc.right - BORDERWIDTH))
        {
            return HTRIGHT;
        }

        return HTCAPTION;
    }
    break;
    default:
        result = DefWindowProc(hWnd, msg, wParam, lParam);
        break;
    }
    return result;
}

void FLM_USER_INTERFACE::CloseUI()
{
    if (m_hUserInterfaceThread == NULL)
        return;

    if (g_ui.hWndUser)
    {
        PostMessage(g_ui.hWndUser, WM_QUIT, 0, 0);
        g_ui.hWndUser = NULL;
    }

    WaitForSingleObject(m_hUserInterfaceThread, INFINITE);
    CloseHandle(m_hUserInterfaceThread);
    m_hUserInterfaceThread = NULL;
}

void FLM_USER_INTERFACE::UpdateRunTimeOptions()
{
    if (g_ui.runtimeOptions == NULL)
        return;

    int printOption = std::clamp((int)g_ui.runtimeOptions->printLevel, 0, (int)FLM_PRINT_LEVEL::PRINT_LEVEL_COUNT - 1);

    SendMessage(g_ui.hWndLatencyDisplay[(int)(printOption)], BM_SETCHECK, TRUE, NULL);

    if (g_ui.runtimeOptions->mouseEventType == FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE)
        SendMessage(g_ui.hWndMouseClick[0], BM_SETCHECK, TRUE, NULL);
    else
        SendMessage(g_ui.hWndMouseClick[1], BM_SETCHECK, TRUE, NULL);

    if (g_ui.hWndEditBias)
    {
        std::string str = FlmFormatStr("%3.1f", g_ui.runtimeOptions->biasOffset);
        SetWindowText(g_ui.hWndEditBias, str.c_str());
        SendMessage(g_ui.hWndEditBias, EM_SETREADONLY, g_ui.runtimeOptions->autoBias ? TRUE : FALSE, NULL);
    }

    if (g_ui.hWndEditThreshold)
    {
        float       threshold = g_ui.runtimeOptions->thresholdCoefficient[g_ui.runtimeOptions->mouseEventType];
        std::string str       = FlmFormatStr("%3.1f", threshold);
        SetWindowText(g_ui.hWndEditThreshold, str.c_str());
    }

    if (g_ui.hWndAutoBias)
        SendMessage(g_ui.hWndAutoBias, BM_SETCHECK, g_ui.runtimeOptions->autoBias ? TRUE : FALSE, NULL);

    if (g_ui.hWndMinimizeApp)
        SendMessage(g_ui.hWndMinimizeApp, BM_SETCHECK, g_ui.runtimeOptions->minimizeApp ? TRUE : FALSE, NULL);

    if (g_ui.hWndGameHasFG)
        SendMessage(g_ui.hWndGameHasFG, BM_SETCHECK, g_ui.runtimeOptions->gameUsesFrameGeneration ? TRUE : FALSE, NULL);

}

void FLM_USER_INTERFACE::Init(FLM_RUNTIME_OPTIONS* displayOption, HANDLE hUserInterfaceThread)
{
    if (!g_ui.hWndUser)
        return;

    m_hUserInterfaceThread = hUserInterfaceThread;

    g_ui.runtimeOptions = displayOption;

    UpdateRunTimeOptions();

    // Reset flag, so it can be used to notify FLM to reset capture codec if user changes to a new region
    g_ui.runtimeOptions->captureRegionChanged = false;
}

void FLM_USER_INTERFACE::HideUI()
{
    if (!g_ui.hWndUser)
        return;
    if (g_ui.runtimeOptions)
        g_ui.runtimeOptions->showOptions = false;
    ShowWindow(g_ui.hWndUser, SW_HIDE);
    g_ui.capture_region_showing = false;
    ShowWindow(g_ui.hWndRegion, SW_HIDE);
    g_ui.ui_showing = false;
}

void FLM_USER_INTERFACE::ShowUI(int x, int y)
{
    if (!g_ui.hWndUser)
        return;
    SetWindowPos(g_ui.hWndUser, HWND_TOPMOST, x, y, FLM_OPTIONS_WINDOW_WIDTH, FLM_OPTIONS_WINDOW_HEIGHT, SWP_DRAWFRAME | SWP_SHOWWINDOW);
    SetForegroundWindow(g_ui.hWndUser);
    if (g_ui.runtimeOptions)
    {
        UpdateRunTimeOptions();
        g_ui.runtimeOptions->showOptions = true;
    }
    g_ui.ui_showing = true;
}

int WINAPI FLM_USER_INTERFACE::ThreadMain()
{
    TCHAR szClassName[] = _T("FLM User Interface");
    MSG   msg           = {};
    HINSTANCE hIns      = GetModuleHandle(NULL);

    WNDCLASSEX wc;
    wc.lpszClassName = szClassName;
    wc.lpfnWndProc   = WndProcUI;
    wc.cbSize        = sizeof(WNDCLASSEX);
    wc.style         = CS_DROPSHADOW | CS_DBLCLKS;
    wc.hIcon         = LoadIcon(NULL, IDI_APPLICATION);
    wc.hInstance     = hIns;
    wc.hIconSm       = LoadIcon(NULL, IDI_APPLICATION);
    wc.hCursor       = LoadCursor(NULL, IDC_ARROW);
    wc.hbrBackground = (HBRUSH)COLOR_BTNSHADOW;
    wc.cbWndExtra    = 0;
    wc.lpszMenuName  = NULL;
    wc.cbClsExtra    = 0;

    RegisterClassEx(&wc);
    g_ui.hWndUser =
        CreateWindowEx(0, szClassName, szClassName, WS_POPUP | WS_BORDER, 0, 0, FLM_OPTIONS_WINDOW_WIDTH, FLM_OPTIONS_WINDOW_HEIGHT, HWND_DESKTOP, 0, hIns, 0);

    HBRUSH brush = CreateSolidBrush(RGB(50, 50, 50));
    SetClassLongPtr(g_ui.hWndUser, GCLP_HBRBACKGROUND, (LONG_PTR)brush);

    ShowWindow(g_ui.hWndUser, SW_HIDE);

    TCHAR szClassName2[] = _T("FLM Capture Region");
    wc.lpszClassName     = szClassName2;
    wc.lpfnWndProc       = WndProcRegion;
    wc.hbrBackground     = (HBRUSH)CreateSolidBrush(RGB(50, 50, 50));
    RegisterClassEx(&wc);
    g_ui.hWndRegion = CreateWindowExA(0, szClassName2, szClassName2, WS_POPUP, 0, 0, 0, 0, HWND_DESKTOP, 0, hIns, 0);

    ShowWindow(g_ui.hWndRegion, SW_HIDE);

    // Get device info for primary monitor
    DWORD    iModeNum   = ENUM_CURRENT_SETTINGS;
    DEVMODEA DeviceMode = {};
    if (EnumDisplaySettingsA(NULL, iModeNum, &DeviceMode))
    {
        g_ui.maxHeight = DeviceMode.dmPelsHeight;
        g_ui.maxWidth  = DeviceMode.dmPelsWidth;
    }

    //PeekMessage loop example
    while (WM_QUIT != msg.message)
    {
        if (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE) > 0)
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    exit(0);
}
