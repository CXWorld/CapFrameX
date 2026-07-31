//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_pipeline.cpp
/// @brief  frame pipeline interface to set keyboard, mouse and capture
//=============================================================================

#include "flm_pipeline.h"
#ifndef CAPFRAMEX_FLM_EMBEDDED
#include "flm_user_interface.h"
#endif
#include <fstream>

#define CLEAR_CONSOLE_TO_END_OF_LINE "\033[s\033[0K\033[u"  // used if console virtual terminal feature is available else use console buffer API
#define FLM_MOUSE_CLICK_UPPER_LIMIT 300 // adjust as needed: ToDo make this user programmable 
#define FLM_MOUSE_CLICK_ADJUST_BIAS_FOR_NOISE 1.00f // adjust as needed: ToDo make this user programmable 

#define PIPELINE_DEBUG_PRINT_STACK()                           // printf(__FUNCTION__"\n");
#define PIPELINE_DEBUG_PRINT_MouseEventThreadFunction(f, ...)  // printf((f), __VA_ARGS__);
#define PIPELINE_DEBUG_PRINT_mouse_event(f, ...)               // printf((f), __VA_ARGS__);

// Vendor ID
#ifdef _WIN32
#include "dxgi.h"
#pragma comment(lib, "dxgi.lib")
#endif

ProgressCallback* g_pUserCallBack    = NULL;

void FLM_Pipeline::SetUserProcessCallback(ProgressCallback Info)
{
    g_pUserCallBack = Info;
}

DWORD WINAPI FLM_Pipeline::CaptureDisplayThreadFunctionStub(LPVOID lpParameter)
{
    FLM_Pipeline* p = reinterpret_cast<FLM_Pipeline*>(lpParameter);
    p->m_capture->DisplayThreadFunction();
    return 0;
}

FLM_STATUS FLM_Pipeline::saveUserSettings()
{
    const char* section = "PIPELINE";
    CSimpleIniA ini;
    SI_Error    rc = ini.LoadFile("flm.ini");
    if (rc < 0)
    {
        FlmPrintError("flm.ini not found");
        return FLM_STATUS::FAILED;
    }

    ini.SetBoolValue(section, "AppWindowTopMost", m_runtimeOptions.appWindowTopMost);
    ini.SetLongValue(section, "PrintLevel", (int)m_runtimeOptions.printLevel);
    ini.SetLongValue(section, "MouseEventType", (int)m_runtimeOptions.mouseEventType);
    ini.SetBoolValue(section, "AutoBias", m_runtimeOptions.autoBias);
    ini.SetBoolValue(section, "GameUsesFrameGeneration", m_runtimeOptions.gameUsesFrameGeneration);

#if !defined(HIDE_MINIMIZE_APP_TOGGLE)
    ini.SetBoolValue(section, "MinimizeApplication",m_runtimeOptions.minimizeApp);
#endif

    if (m_runtimeOptions.autoBias == false)
    {
        if (m_runtimeOptions.monitorRefreshRate >= 239)
            ini.SetDoubleValue(section, "MonitorCalibration_240Hz", m_runtimeOptions.biasOffset);
        else if (m_runtimeOptions.monitorRefreshRate >= 143)
            ini.SetDoubleValue(section, "MonitorCalibration_144Hz", m_runtimeOptions.biasOffset);
        else if (m_runtimeOptions.monitorRefreshRate >= 119)
            ini.SetDoubleValue(section, "MonitorCalibration_120Hz", m_runtimeOptions.biasOffset);
        else if (m_runtimeOptions.monitorRefreshRate >= 59)
            ini.SetDoubleValue(section, "MonitorCalibration_60Hz", m_runtimeOptions.biasOffset);
        else if (m_runtimeOptions.monitorRefreshRate >= 49)
            ini.SetDoubleValue(section, "MonitorCalibration_50Hz", m_runtimeOptions.biasOffset);
        else if (m_runtimeOptions.monitorRefreshRate >= 23)
            ini.SetDoubleValue(section, "MonitorCalibration_24Hz", m_runtimeOptions.biasOffset);
    }

    ini.SetDoubleValue(section, "ThresholdCoefficientMove", m_runtimeOptions.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE]);
    ini.SetDoubleValue(section, "ThresholdCoefficientClick", m_runtimeOptions.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_CLICK]);

    const char* sectionCapture = "CAPTURE";

    // Save back only if we have a valid backbuffer size
    if ((m_capture->m_iBackBufferWidth > 0) && (m_capture->m_iBackBufferHeight > 0))
    {
        float fStartX = (float)m_runtimeOptions.iCaptureX / m_capture->m_iBackBufferWidth;
        float fStartY = (float)m_runtimeOptions.iCaptureY / m_capture->m_iBackBufferHeight;
        ini.SetDoubleValue(sectionCapture, "StartX",fStartX);
        ini.SetDoubleValue(sectionCapture, "StartY",fStartY);

        // Valid capture size 
        if ((m_runtimeOptions.iCaptureWidth > 0) && (m_runtimeOptions.iCaptureHeight > 0))
      {
            float fCaptureWidth  = (float)m_runtimeOptions.iCaptureWidth / m_capture->m_iBackBufferWidth;
            float fCaptureHeight = (float)m_runtimeOptions.iCaptureHeight / m_capture->m_iBackBufferHeight;

            ini.SetDoubleValue(sectionCapture, "CaptureWidth", fCaptureWidth );
            ini.SetDoubleValue(sectionCapture, "CaptureHeight",fCaptureHeight);
        }
    }

    rc = ini.SaveFile("flm.ini");
    if (rc < 0)
    {
        FlmPrintError("failed to save user settings into flm.ini");
        return FLM_STATUS::FAILED;
    }
    return FLM_STATUS::OK;
}

FLM_STATUS FLM_Pipeline::loadUserSettings()
{
    CSimpleIniA ini;
    SI_Error    rc = ini.LoadFile("flm.ini");
    if (rc < 0)
    {
        FlmPrintError("flm.ini not found");
        return FLM_STATUS::INIT_FAILED;
    }

    try
    {
        const char* section               = "PIPELINE";

        m_runtimeOptions.autoBias         = ini.GetBoolValue(section, "AutoBias", m_runtimeOptions.autoBias);
        m_runtimeOptions.appWindowTopMost = ini.GetBoolValue(section, "AppWindowTopMost", m_runtimeOptions.appWindowTopMost);
        m_runtimeOptions.printLevel       = (FLM_PRINT_LEVEL)std::clamp((int)ini.GetLongValue(section, "PrintLevel", (int)m_runtimeOptions.printLevel), 0, (int)FLM_PRINT_LEVEL::PRINT_LEVEL_COUNT - 1);
        m_runtimeOptions.minimizeApp      = ini.GetBoolValue(section, "MinimizeApplication", m_runtimeOptions.minimizeApp);
        m_runtimeOptions.gameUsesFrameGeneration = ini.GetBoolValue(section, "GameUsesFrameGeneration", m_runtimeOptions.gameUsesFrameGeneration);
        m_runtimeOptions.mouseEventType   = (FLM_MOUSE_EVENT_TYPE)(int)ini.GetLongValue(section, "MouseEventType", (int)m_runtimeOptions.mouseEventType?1:0);
        m_runtimeOptions.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE]  = (float)ini.GetDoubleValue(section, "ThresholdCoefficientMove", m_runtimeOptions.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE]);
        m_runtimeOptions.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_CLICK] = (float)ini.GetDoubleValue(section, "ThresholdCoefficientClick", m_runtimeOptions.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_CLICK]);
        m_runtimeOptions.initAMFUsingDX12 = (int)ini.GetLongValue(section, "InitAMFUsingDX12", m_runtimeOptions.initAMFUsingDX12);


        m_setting.appExitKeys             = ini.GetValue(section, "AppExitKeys", m_setting.appExitKeys.c_str());
        m_setting.captureSurfaceKeys      = ini.GetValue(section, "CaptureFileKeys", m_setting.captureSurfaceKeys.c_str());
        m_setting.extraWaitMilliseconds   = (float)ini.GetDoubleValue(section, "ExtraWaitMilliseconds",  m_setting.extraWaitMilliseconds);
        m_setting.extraWaitMillisecondsFG = (float)ini.GetDoubleValue(section, "ExtraWaitMillisecondsFG",m_setting.extraWaitMillisecondsFG);
        m_setting.extraWaitFrames         = ini.GetLongValue(section, "ExtraWaitFrames", m_setting.extraWaitFrames);
        m_setting.extraWaitFramesFG       = ini.GetLongValue(section, "ExtraWaitFramesFG", m_setting.extraWaitFramesFG);
        m_setting.iNumMeasurementsPerLine = std::clamp((int)ini.GetLongValue(section, "MeasurementsPerLine", m_setting.iNumMeasurementsPerLine), 1, 32);

        // ToDo
        //.showUserOptionsKeys     = ini.GetValue(section, "UserOptionKeys", m_setting.showUserOptionsKeys.c_str());

        m_setting.measurementKeys          = ini.GetValue(section, "MeasurementKeys", m_setting.measurementKeys.c_str());
        m_setting.iMouseHorizontalStep     = std::clamp((int)ini.GetLongValue(section, "MouseHorizontalStep", m_setting.iMouseHorizontalStep), 10, 1000);
        m_setting.iNumDequantizationPhases = std::clamp((int)ini.GetLongValue(section, "NumDequantizingPhases", m_setting.iNumDequantizationPhases), 1, 3);
        m_setting.outputFileName           = ini.GetValue(section, "OutputFile", m_setting.outputFileName.c_str());
        m_setting.saveToFile               = ini.GetBoolValue(section, "SaveToFile", m_setting.saveToFile);
        m_setting.showAdvancedMeasurements = ini.GetBoolValue(section, "ShowAdvancedMeasurements", m_setting.showAdvancedMeasurements);
        m_setting.showBoundingBox          = ini.GetBoolValue(section, "ShowBoundingBox", m_setting.showBoundingBox);
        m_setting.validateCaptureKeys      = ini.GetValue(section, "ValidateCaptureKeys", m_setting.validateCaptureKeys.c_str());
        m_setting.validateCaptureNumOfFrames = std::clamp((int)ini.GetLongValue(section, "ValidateCaptureNumOfFrames", m_setting.validateCaptureNumOfFrames), 1, 999);

        // Check m_codec is at auto: user has not selected an override from command line
        // else use ini setting
        std::string codec = "auto"; // Default ini
        codec = ini.GetValue(section, "Codec", codec.c_str());
        if (m_codec == FLM_CAPTURE_CODEC_TYPE::AUTO)
        {
            std::transform(codec.begin(), codec.end(), codec.begin(), ::tolower);
            if (codec.compare("auto") == 0)
                m_codec = FLM_CAPTURE_CODEC_TYPE::AUTO;
            else
            if (codec.compare("amf") == 0)
                m_codec = FLM_CAPTURE_CODEC_TYPE::AMF;
            else
            if (codec.compare("dxgi") == 0)
                m_codec = FLM_CAPTURE_CODEC_TYPE::DXGI;
            else
            {
                FlmPrintError("Error reading flm.ini file codec %s is not supported",codec.c_str());
                return FLM_STATUS::FAILED;
            }
        }

        m_setting.monitorCalibration_240Hz =
            std::clamp((float)ini.GetDoubleValue(section, "MonitorCalibration_240Hz", m_setting.monitorCalibration_240Hz), 0.0f, 100.0f);
        m_setting.monitorCalibration_144Hz =
            std::clamp((float)ini.GetDoubleValue(section, "MonitorCalibration_144Hz", m_setting.monitorCalibration_144Hz), 0.0f, 100.0f);
        m_setting.monitorCalibration_120Hz =
            std::clamp((float)ini.GetDoubleValue(section, "MonitorCalibration_120Hz", m_setting.monitorCalibration_120Hz), 0.0f, 100.0f);
        m_setting.monitorCalibration_60Hz =
            std::clamp((float)ini.GetDoubleValue(section, "MonitorCalibration_60Hz", m_setting.monitorCalibration_60Hz), 0.0f, 100.0f);
        m_setting.monitorCalibration_50Hz =
            std::clamp((float)ini.GetDoubleValue(section, "MonitorCalibration_50Hz", m_setting.monitorCalibration_50Hz), 0.0f, 100.0f);
        m_setting.monitorCalibration_24Hz =
            std::clamp((float)ini.GetDoubleValue(section, "MonitorCalibration_24Hz", m_setting.monitorCalibration_24Hz), 0.0f, 100.0f);
    }
    catch (...)
    {
        FlmPrintError("Error reading flm.ini file");
        return FLM_STATUS::FAILED;
    }
    return FLM_STATUS::OK;
}

FLM_STATUS FLM_Pipeline::InitSettings()
{
    PIPELINE_DEBUG_PRINT_STACK()
    // Main
    m_bMeasuringInProgress = false;

    // Mouse
    m_runtimeOptions.mouseEventType = FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE;
    m_iiMouseMoveEventTime          = 0L;
    m_hMouseThread                  = NULL;
    m_iMeasurementPhaseCounter      = 0;

    // Keyboard
    m_hKbdThread = NULL;

    // Capture
    m_hCaptureThread                = NULL;
    m_eventMovementDetected         = NULL;
    m_iDequantizingPhaseCounter     = 0;
    m_iiMotionDetectedFrameFlipTime = 0L;

    // Pipeline
    m_fCumulativeLatencyTimesMS = 0.0f;
    m_iCumulativeLatencySamples = 0;
    m_fAccumulatedLatencyMS     = 0.0f;
    m_fAccumulatedFrameTimeMS   = 0.0f;

#ifdef CAPFRAMEX_FLM_EMBEDDED
    m_runtimeOptions.appWindowTopMost = false;
    m_runtimeOptions.printLevel = FLM_PRINT_LEVEL::RUN;
    m_runtimeOptions.mouseEventType = FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE;
    m_runtimeOptions.autoBias = false;
    m_runtimeOptions.minimizeApp = false;
    m_runtimeOptions.showOptions = false;
    m_runtimeOptions.saveUserSettings = false;
    m_runtimeOptions.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE] =
        std::max(0.1f, m_runtimeOptions.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE]);
    m_runtimeOptions.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_CLICK] = 5.0f;
    m_setting.iMouseHorizontalStep = std::clamp(m_runtimeOptions.mouseHorizontalStep, 10, 1000);
    m_setting.saveToFile = false;
    m_setting.showBoundingBox = false;
    m_hWnd = NULL;
    return FLM_STATUS::OK;
#else
    if (loadUserSettings() == FLM_STATUS::OK)
    {
        // Set keyboard keys
        if (m_keyboard.SetKeys(m_setting.measurementKeys, m_measurementKeys) == false)
        {
            FlmPrintError("Parsing flm.ini for ToggleKeys: %s", m_keyboard.GetErrorMessage().c_str());
            return FLM_STATUS::INIT_FAILED;
        }

        if (m_keyboard.SetKeys(m_setting.appExitKeys, m_appExitKeys) == false)
        {
            FlmPrintError("Parsing flm.ini for AppExitKeys: %s", m_keyboard.GetErrorMessage().c_str());
            return FLM_STATUS::INIT_FAILED;
        }

        if (m_keyboard.SetKeys(m_setting.captureSurfaceKeys, m_captureSurfaceKeys) == false)
        {
            FlmPrintError("Parsing flm.ini for CaptureFileKeys: %s", m_keyboard.GetErrorMessage().c_str());
            return FLM_STATUS::INIT_FAILED;
        }

        if (m_keyboard.SetKeys(m_setting.validateCaptureKeys, m_validateCaptureKeys) == false)
        {
            FlmPrintError("Parsing flm.ini for ValidateCaptureKeys: %s", m_keyboard.GetErrorMessage().c_str());
            return FLM_STATUS::INIT_FAILED;
        }
    }
    else
        return FLM_STATUS::INIT_FAILED;

    return FLM_STATUS::OK;
#endif
}

// Feed back of processed data in Operational Mode
// Optional output to a file stream is also provided
void FLM_Pipeline::PrintStream(const char* Format, ...)
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

    if (g_pUserCallBack)
        g_pUserCallBack(FLM_PROCESS_MESSAGE_TYPE::PRINT, buff);
}

void FLM_Pipeline::CreateCSV()
{
    if (m_outputFile != NULL)
        return;

    m_outputFile = fopen(m_setting.outputFileName.c_str(), "w");

    // Print CSV file header
    if (m_outputFile != NULL)
    {
        if (m_setting.showAdvancedMeasurements)
        {
            fprintf(m_outputFile, "FPS,Odd,Even,");
            for (unsigned int i = 0; i < m_setting.iNumMeasurementsPerLine; i++)
                fprintf(m_outputFile, FlmFormatStr("lat%02d,", i).c_str());
            fprintf(m_outputFile, "ACC Latency (ms), ACC Frame, latency (ms), frames\n");
        }
        else 
        {
            fprintf(m_outputFile, "FPS,");
            for (unsigned int i = 0; i < m_setting.iNumMeasurementsPerLine; i++)
                fprintf(m_outputFile, FlmFormatStr("lat%02d,", i).c_str());
            fprintf(m_outputFile, " latency (ms), frames\n");
        }
    }
    else
    {
        if (g_pUserCallBack)
            g_pUserCallBack(FLM_PROCESS_MESSAGE_TYPE::ERROR_MESSAGE, "Unable to open output csv file");
    }
}

void FLM_Pipeline::CloseCSV()
{
    if (m_outputFile != NULL)
    {
        fclose(m_outputFile);
        m_outputFile = NULL;
    }
}

void FLM_Pipeline::SaveTelemetryCSV()
{
    // No saving to file if null
    if (m_outputFile == NULL)
        return;

    PIPELINE_DEBUG_PRINT_STACK()

    fprintf(m_outputFile, "%4.1f,", m_telemetry.fps);
    if (m_setting.showAdvancedMeasurements)
    {
        fprintf(m_outputFile, "%4.1f,", m_telemetry.fpsOdd);
        fprintf(m_outputFile, "%4.1f,", m_telemetry.fpsEven);
    }

    for (int i = 0; i < m_telemetry.lMeasurementMS.size(); i++)
        fprintf(m_outputFile, "%3.0f, ", m_telemetry.lMeasurementMS[i]);

    if (m_setting.showAdvancedMeasurements)
    {
        fprintf(m_outputFile, "%4.2f, ", m_telemetry.accLatency);
        fprintf(m_outputFile, "%4.2f,", m_telemetry.accFrames);
    }

    fprintf(m_outputFile, "%4.1f,", m_telemetry.rowLatency);
    fprintf(m_outputFile, "%3.2f\n", m_telemetry.rowFrames);
}

void FLM_Pipeline::PrintAverageTelemetry(float fFrameLatencyMS)
{
    PIPELINE_DEBUG_PRINT_STACK()
    static unsigned int runningCount                = 0;
    static int          iMeasurementPerLineCounter  = 0;
    static float        fTotalLineLatencyMS         = 0;

    // Reset Condition
    if (fFrameLatencyMS < 0.0f)
    {
        fTotalLineLatencyMS          = 0;
        iMeasurementPerLineCounter   = 0;
        runningCount                 = 0;
        return;
    }

    ///////////////////////////////////////////////////////////////////////////////////
    // Handling MEASUREMENTS_PER_LINE
    {
        // Accumulate
        fTotalLineLatencyMS += fFrameLatencyMS;
        iMeasurementPerLineCounter += 1;

        if (iMeasurementPerLineCounter == 1)
            m_telemetry.lMeasurementMS.clear();

        // Current measurement
        m_telemetry.lMeasurementMS.push_back(fFrameLatencyMS);

        // "Running" telemetry is updated once a row of measurements (16) is available
        if (iMeasurementPerLineCounter == m_setting.iNumMeasurementsPerLine)
        {
            m_telemetry.rowLatency = fTotalLineLatencyMS / iMeasurementPerLineCounter;
            m_telemetry.rowFrames  = fTotalLineLatencyMS / iMeasurementPerLineCounter / m_capture->m_fMovingAverageFrameTimeMS - 0.5f;

            fTotalLineLatencyMS        = 0;
            iMeasurementPerLineCounter = 0;

            // Save to CSV file telemetry data
            if (m_setting.saveToFile)
                SaveTelemetryCSV();
        }
    }

    FlmPrintStaticPos("ACCUMULATED MEASUREMENTS: %i, FPS: %0.2f, Latency: %0.1f ms, %0.2f frames        ",
        runningCount, m_telemetry.accFps, m_telemetry.accLatency, m_telemetry.accFrames);

    runningCount++;
}

void FLM_Pipeline::PrintOperationalTelemetry(float fFrameLatencyMS, bool bFull)
{
    PIPELINE_DEBUG_PRINT_STACK()
    static int   iMeasurementPerLineCounter = 0;
    static float fTotalLineLatencyMS        = 0;

    // Reset Condition
    if (fFrameLatencyMS < 0.0f)
    {
        fTotalLineLatencyMS        = 0;
        iMeasurementPerLineCounter = 0;
        return;
    }

    ///////////////////////////////////////////////////////////////////////////////////
    // Handling MEASUREMENTS_PER_LINE
    {
        // Accumulate
        fTotalLineLatencyMS += fFrameLatencyMS;
        iMeasurementPerLineCounter += 1;

        if (iMeasurementPerLineCounter == 1)
        {
            m_telemetry.lMeasurementMS.clear();
            PrintStream("fps = %5.1f", m_telemetry.fps);
            if (m_setting.showAdvancedMeasurements)
            {
                PrintStream(" | odd = %5.1f", m_telemetry.fpsOdd);
                PrintStream(" | even = %5.1f", m_telemetry.fpsEven);
            }
            PrintStream(" | ");
        }

        // Collect the current measurement
        m_telemetry.lMeasurementMS.push_back(fFrameLatencyMS);

        // Print the current measurement
        if (bFull)
            PrintStream("%5.1f ", fFrameLatencyMS);
        else
            PrintStream(".", fFrameLatencyMS);

        if (iMeasurementPerLineCounter == m_setting.iNumMeasurementsPerLine)
        {
            m_telemetry.rowLatency = fTotalLineLatencyMS / iMeasurementPerLineCounter;
            m_telemetry.rowFrames  = fTotalLineLatencyMS / iMeasurementPerLineCounter / m_capture->m_fMovingAverageFrameTimeMS - 0.5f;

            if (m_setting.showAdvancedMeasurements)
                PrintStream(" | acc latency = %6.2fms | acc frame = %4.2f", m_telemetry.accLatency, m_telemetry.accFrames);

            PrintStream(" | latency = %4.1f | frames = %3.2f\n", m_telemetry.rowLatency, m_telemetry.rowFrames);

            fTotalLineLatencyMS       = 0;
            iMeasurementPerLineCounter = 0;

            // Save to CSV file telemetry data
            if (m_setting.saveToFile)
                SaveTelemetryCSV();
        }
    }
}

void FLM_Pipeline::PrintDebugTelemetry(float fFrameLatencyMS)
{
    PIPELINE_DEBUG_PRINT_STACK()

    static int64_t printTimeStampPrev = 0;
    int64_t        printTimeStamp     = m_timer.now();
    float          fPrintTimeMS       = float(printTimeStamp - printTimeStampPrev) / AMF_MILLISECOND;
    float          fFrameTimeMS       = float(m_iiFrameTimeStamp - m_iiFrameTimeStampPrev) / AMF_MILLISECOND;
    float          fFPS               = 1000.f / std::max<float>(0.1f,fFrameTimeMS);
    PrintStream("FPS =%5.1f, AvFt =%5.1fms, Pt =%6.1fms, BG/SAD/ThSAD(%3i,%3i,%3i), latency[ms] =%6.1f, frames =%4.1f  ",
                fFPS,
                m_capture->m_fMovingAverageFrameTimeMS,
                fPrintTimeMS,
                (int)m_capture->m_fBackgroundSAD, m_iSAD, m_iThSAD,
                fFrameLatencyMS,
                fFrameLatencyMS / m_capture->m_fMovingAverageFrameTimeMS - 0.5f);

    if (m_iThSAD > 0)
        PrintStream(" ==> motion detected!");

    if (m_iiFrameIdx == m_iiFrameIdxPrev)
        PrintStream(" 00000000000000");
    else if (m_iiFrameIdx == m_iiFrameIdxPrev + 1)  // ok
    {
    }
    else
        PrintStream(" ??????????????");

    PrintStream("\n");
    printTimeStampPrev = printTimeStamp;
    while (KEY_DOWN(VK_LSHIFT))
        Sleep(10);
}

void FLM_Pipeline::UpdateAverageLatency(float fLatencyMS)
{
    PIPELINE_DEBUG_PRINT_STACK()

    m_fCumulativeLatencyTimesMS += fLatencyMS;
    m_iCumulativeLatencySamples++;

    // Averages all the accumulated samples in the current measurement experiment:
    m_fAccumulatedLatencyMS = m_fCumulativeLatencyTimesMS / std::max<int>(1, m_iCumulativeLatencySamples);

    // Update average accumulated frame time for the entire experiment
    m_fAccumulatedFrameTimeMS = std::max<float>(.1f, m_capture->m_fCumulativeFrameTimesMS / std::max<int>(1, m_capture->m_iCumulativeFrameTimeSamples));

    // Accumulated telemetry is updated on every measurement
    m_telemetry.accLatency = m_fAccumulatedLatencyMS;
    m_telemetry.accFrames  = m_fAccumulatedLatencyMS / m_fAccumulatedFrameTimeMS - 0.5f;
    m_telemetry.accFps     = 1000.0f / m_fAccumulatedFrameTimeMS;

    // Update m_telemetry FPS values
    m_telemetry.fps     = 1000.0f / std::max<float>(0.01f, m_capture->m_fMovingAverageFrameTimeMS);
    m_telemetry.fpsOdd  = 1000.0f / std::max<float>(0.01f, m_capture->m_fMovingAverageOddFramesTimeMS);
    m_telemetry.fpsEven = 1000.0f / std::max<float>(0.01f, m_capture->m_fMovingAverageEvenFramesTimeMS);
}

LARGE_INTEGER GetTimeStamp()
{
    LARGE_INTEGER timestamp = {};
    QueryPerformanceCounter((LARGE_INTEGER*)&timestamp);
    return timestamp;
}

void FLM_send_mouse_move_event(int iHorzStep)
{
    PIPELINE_DEBUG_PRINT_mouse_event("%-38s [%I64d]\n", __FUNCTION__, GetTimeStamp().QuadPart);

    static INPUT input = {INPUT_MOUSE, {0, 0, 0, MOUSEEVENTF_MOVE}};

    //     input.type       = INPUT_MOUSE;
    //     input.mi.dwFlags = MOUSEEVENTF_MOVE;
    //     input.mi.dx      = (LONG)iHorzStep;

    input.mi.dx = (LONG)iHorzStep;
    SendInput(1, &input, sizeof(input));  // This is a little bit faster - less chances for a context switch in the middle.

    //mouse_event(MOUSEEVENTF_MOVE, (DWORD)iHorzStep, 0, 0, 0); // too slow
}

DWORD WINAPI FLM_Pipeline::MouseEventThreadFunctionStub(LPVOID lpParameter)
{
    PIPELINE_DEBUG_PRINT_STACK()

    FLM_Pipeline* p = reinterpret_cast<FLM_Pipeline*>(lpParameter);
    p->MouseEventThreadFunction();
    return 0;
}

// Wait for the game to respond
bool FLM_Pipeline::WaitForFrameDetection()
{
    ResetEvent(m_eventMovementDetected);
    DWORD dwWaitResult;
    for (int i = 0; i < 20; i++)
    {
        dwWaitResult = WaitForSingleObject(m_eventMovementDetected, 50);  // 20*50ms == 1 sec
        if (dwWaitResult != WAIT_TIMEOUT)
            break;
    }

    if (dwWaitResult == WAIT_TIMEOUT)
        return false;

    return true;
}

void FLM_Pipeline::SendMouseMove()
{
    // Move the mouse
    const bool bAMF = (m_codec == FLM_CAPTURE_CODEC_TYPE::AMF) ? true : false;

    int64_t iiMouseEventTime0 = bAMF ? m_timer.now() : GetTimeStamp().QuadPart; // Used for sanity check only
    FLM_send_mouse_move_event(m_setting.iMouseHorizontalStep);
    m_iiMouseMoveEventTime    = bAMF ? m_timer.now() : GetTimeStamp().QuadPart; // Measure time after the slow(-ish) function returns...

#ifdef _DEBUG
    if ((m_iiMouseMoveEventTime - iiMouseEventTime0) > 5000)  // More than 50us?!
    {
        PIPELINE_DEBUG_PRINT_MouseEventThreadFunction("%-38s  mouse event too slow\n", __FUNCTION__);
        //PrintStream("mouse event too slow ");
    }
#endif

    m_setting.iMouseHorizontalStep = -m_setting.iMouseHorizontalStep;
}

void FLM_Pipeline::MouseEventThreadFunction()
{
    PIPELINE_DEBUG_PRINT_MouseEventThreadFunction("%-38s\n", __FUNCTION__);
    m_bExitMouseThread   = false;
    const int CYCLE_SIZE = m_setting.iNumMeasurementsPerLine;
    for (; m_bTerminateMouseThread == false;)
    {
        if (m_bMeasuringInProgress)
        {
            if (m_runtimeOptions.mouseEventType == FLM_MOUSE_EVENT_TYPE::MOUSE_CLICK)
            {
                while (m_iSAD > 0)
                {
                    Sleep(0);
                    if (m_bTerminateMouseThread)
                        break;
                }

                if (m_mouse.IsButtonDown(MOUSE_LEFT_BUTTON))
                {
                    m_bMouseClickDetected = false;
                    m_timer_performance.Start();
                    if (WaitForFrameDetection())
                    {
                        // save latency result
                        m_fLatestMeasuredLatencyMS = (float)m_timer_performance.Stop_ms();
                        m_bMouseClickDetected      = true;
                        while (m_mouse.IsButtonDown(MOUSE_LEFT_BUTTON))
                        {
                            if (m_bTerminateMouseThread)
                                break;
                        }
                    }
                }
            }
            else if (m_runtimeOptions.mouseEventType == FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE)
            {
                SendMouseMove();
                if (WaitForFrameDetection())
                {
                    // Wait a bit before launching the next mouse event
                    // We need to sleep for all portions of 1 frame time to work around the frame quantization effect.
                    float fTimeToSleepMS       = m_capture->m_fMovingAverageFrameTimeMS * m_iMeasurementPhaseCounter / CYCLE_SIZE;
                    m_iMeasurementPhaseCounter = (m_iMeasurementPhaseCounter + 1) % CYCLE_SIZE;

                    // Add a small sub-cycle shift to work around the quantization issue. This will allow averaging results from
                    // adjacent rows to get a better precision.
                    fTimeToSleepMS += m_capture->m_fMovingAverageFrameTimeMS * m_iDequantizingPhaseCounter / CYCLE_SIZE / m_setting.iNumDequantizationPhases;
                    if (m_iMeasurementPhaseCounter == 0)
                        m_iDequantizingPhaseCounter = (m_iDequantizingPhaseCounter + 1) % m_setting.iNumDequantizationPhases;

                    // Precision sleeping:
                    int64_t iiSleepStart = m_iiMotionDetectedFrameFlipTime;  // We need to start our sleeping relative to the flip time

                    const bool bFG = m_runtimeOptions.gameUsesFrameGeneration ? true : false; // just aliasing to a shorter name...
                    float extraWaitMS     = bFG ? m_setting.extraWaitMillisecondsFG : m_setting.extraWaitMilliseconds;
                    int   extraWaitFrames = bFG ? m_setting.extraWaitFramesFG       : m_setting.extraWaitFrames;

                    // The extra delay (in addition to the extra frame below) should cover the fact that the flip time is a little bit in the past...
                    // or when game has game generation enabled to prevent locking onto double frequency
                    fTimeToSleepMS += extraWaitMS;

                    // The extra frame should prevent locking onto the double frequency and also prevent problems with motion blur. Half frame is not enough...
                    fTimeToSleepMS += extraWaitFrames * m_capture->m_fMovingAverageFrameTimeMS;

                    m_timer.PrecisionSleepMS(fTimeToSleepMS, iiSleepStart);
                }
            }
        }
        else
        {
            if (m_runtimeOptions.mouseEventType == FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE)
            {
                if (m_setting.iMouseHorizontalStep < 0)  // Make sure we end up in the original position, ready for the next measurement.
                {
                    FLM_send_mouse_move_event(m_setting.iMouseHorizontalStep);
                    m_setting.iMouseHorizontalStep = -m_setting.iMouseHorizontalStep;
                }
            }
            Sleep(10);
        }
    }

    m_bExitMouseThread = true;
}

int FLM_Pipeline::GetBackBufferWidth()
{
    if (m_capture)
        return (m_capture->m_iBackBufferWidth);
    else
        return 0;
}

int FLM_Pipeline::GetBackBufferHeight()
{
    if (m_capture)
        return (m_capture->m_iBackBufferHeight);
    else
        return 0;
}

std::string FLM_Pipeline::GetToggleKeyNames()
{
    return (m_keyboard.GetKeyName(m_measurementKeys[0]) + "+" + m_keyboard.GetKeyName(m_measurementKeys[1]));
}

std::string FLM_Pipeline::GetAppExitKeyNames()
{
    return (m_keyboard.GetKeyName(m_appExitKeys[0]) + "+" + m_keyboard.GetKeyName(m_appExitKeys[1]));
}

DWORD WINAPI FLM_Pipeline::KeyboardListenThreadFunctionStub(LPVOID lpParameter)
{
    PIPELINE_DEBUG_PRINT_STACK()

    FLM_Pipeline* p = reinterpret_cast<FLM_Pipeline*>(lpParameter);
    p->KeyboardListenThreadFunction();
    return 0;
}

bool FLM_Pipeline::isRunningOnPrimaryDisplay()
{
    HMONITOR currentMonitor = MonitorFromWindow(m_hWnd , MONITOR_DEFAULTTONEAREST );
    MONITORINFO monitorInfo;
    monitorInfo.cbSize = sizeof( MONITORINFO );
    GetMonitorInfo( currentMonitor, (LPMONITORINFO) &monitorInfo );
    bool isPrimary = (monitorInfo.dwFlags == MONITORINFOF_PRIMARY);
    return isPrimary;
}


void FLM_Pipeline::StartMeasurements()
{
    if (m_capture == NULL || m_bMeasuringInProgress)
        return;

    ResetState();

    if (m_runtimeOptions.printLevel == FLM_PRINT_LEVEL::ACCUMULATED)
        PrintAverageTelemetry(-1);  // This will reset the printout state
    else
    if ((m_runtimeOptions.printLevel == FLM_PRINT_LEVEL::RUN) || (m_runtimeOptions.printLevel == FLM_PRINT_LEVEL::OPERATIONAL))
        PrintOperationalTelemetry(-1, false);  // This will reset the printout state

    m_capture->ClearCaptureRegion();

    bool isPrimaryWindow = isRunningOnPrimaryDisplay();
    if ((isPrimaryWindow == false)&& (m_codec == FLM_CAPTURE_CODEC_TYPE::DXGI))
        printf("Warning running application DXGI desktop capture from non-primary display will be slow\n");

    if (m_runtimeOptions.minimizeApp && m_hWnd && isPrimaryWindow )
    {
        ShowWindow(m_hWnd,SW_MINIMIZE);
    }

    if (m_setting.saveToFile)
        CreateCSV();

    m_bMeasuringInProgress = true;
}

void FLM_Pipeline::StopMeasurements()
{
    if (!m_bMeasuringInProgress)
        return;

    m_bMeasuringInProgress = false;

    // To avoid the 1 second wait on stop
    SetEvent(m_eventMovementDetected);

    if (m_setting.saveToFile)
        CloseCSV();

    if (m_runtimeOptions.minimizeApp && m_hWnd)
    {
        ShowWindow(m_hWnd,SW_RESTORE);
    }

}

void FLM_Pipeline::KeyboardListenThreadFunction()
{
    PIPELINE_DEBUG_PRINT_STACK()
    m_bExitKeyboardThread = false;

    while((m_bTerminateKeyboardThread == false) && (m_bExitApp == false))
    {
        ProcessKeyboardCommands();
        Sleep(1);
    }

    m_bExitKeyboardThread = true;
}

FLM_GPU_VENDOR_TYPE FLM_Pipeline::GetGPUVendorType()
{
    PIPELINE_DEBUG_PRINT_STACK()

    FLM_GPU_VENDOR_TYPE vendor = FLM_GPU_VENDOR_TYPE::UNKNOWN;

#ifdef _WIN32
    HRESULT hr;

    IDXGIFactory* pFactory = nullptr;
    hr = CreateDXGIFactory(__uuidof(IDXGIFactory), (void**)(&pFactory));
    if (hr != S_OK)
    {
        FlmPrintError("GetGPUVendorType: CreateDXGIFactory failed %X", hr);
        return FLM_GPU_VENDOR_TYPE::UNKNOWN;
    }

    // use main Adapter = 0, alternate adapters will be supported in future releases
    IDXGIAdapter* pAdapter = nullptr;
    hr = pFactory->EnumAdapters(0, &pAdapter);
    if (hr != S_OK)
    {
        FlmPrintError("GetGPUVendorType: EnumAdapters failed %X", hr);
        pFactory->Release();
        return FLM_GPU_VENDOR_TYPE::UNKNOWN;
    }

    DXGI_ADAPTER_DESC adapterDesc;
    hr = pAdapter->GetDesc(&adapterDesc);
    if (hr != S_OK)
    {
        FlmPrintError("GetGPUVendorType: GetDesc failed %X", hr);
        pAdapter->Release();
        pFactory->Release();
        return FLM_GPU_VENDOR_TYPE::UNKNOWN;
    }

    switch (adapterDesc.VendorId)
    {
    case 0x1002:
    case 0x1022:
        // AMD
        vendor = FLM_GPU_VENDOR_TYPE::AMD;
        break;
    case 0x163C:
    case 0x8086:
    case 0x8087:
        // Intel
        vendor = FLM_GPU_VENDOR_TYPE::INTEL;
        break;
    case 0x10DE:
        // Nvidia
        vendor = FLM_GPU_VENDOR_TYPE::NVIDIA;
        break;
    }

    pAdapter->Release();
    pFactory->Release();
#else
    FlmPrintError("Only windows is supported");
#endif

    return vendor;
}

// Last In Fist Out (LIFO) instance of an errors, remove the errors from list until empty
std::string FLM_Pipeline::GetErrorStr()
{
    return FlmGetErrorStr();
}

float FLM_Pipeline::CalculateAutoRefreshScanOffset()
{
    // Adjust for capture frame position in monitor refresh
    float width                 = (float)GetBackBufferWidth();
    float height                = (float)GetBackBufferHeight();
    float totalPixels           = width * height;
    float autoRefreshScanOffset = 60;

    if (totalPixels > 0.0)
    {
        autoRefreshScanOffset = float(m_runtimeOptions.iCaptureX + (width * m_runtimeOptions.iCaptureY)) / totalPixels;
        autoRefreshScanOffset = (1000 / m_runtimeOptions.monitorRefreshRate) * autoRefreshScanOffset;
    }

    return autoRefreshScanOffset;
}

FLM_STATUS FLM_Pipeline::Init(FLM_CAPTURE_CODEC_TYPE cli_codec)
{
    PIPELINE_DEBUG_PRINT_STACK()

    FLM_STATUS status;
    FLM_GPU_VENDOR_TYPE vendor  = GetGPUVendorType();

    // read config file, m_codec may be overwritten by ini file
    status = InitSettings();
    if (status != FLM_STATUS::OK)
        return status;

    // The cli setting for codec overrides the INI setting - if specified
    if( (int)cli_codec >= 0 )
        m_codec = cli_codec;

    // codec is auto: set default codec based on vendor
    if (m_codec == FLM_CAPTURE_CODEC_TYPE::AUTO)
    {
        if (vendor == FLM_GPU_VENDOR_TYPE::AMD)
            m_codec = FLM_CAPTURE_CODEC_TYPE::AMF;
        else
            m_codec = FLM_CAPTURE_CODEC_TYPE::DXGI;
    }

    // Set the selected codec
    if (m_codec == FLM_CAPTURE_CODEC_TYPE::AMF)
        m_capture = new FLM_Capture_AMF(&m_runtimeOptions);
    else
        m_capture = new FLM_Capture_DXGI(&m_runtimeOptions);

    if (m_capture == NULL)
    {
        FlmPrintError("Failed to create capture codec");
        return FLM_STATUS::CAPTURE_INIT_FAILED;
    }

    if (m_capture->InitContext(vendor) == false)
    {
        FlmPrintError("Failed to initialize capture codec context");
        return FLM_STATUS::INIT_FAILED;
    }

    m_eventMovementDetected = CreateEvent(NULL, TRUE, FALSE, NULL);

    if (m_timer.Init() == false)
    {
        FlmPrintError("Timer init failed");
        return FLM_STATUS::TIMER_INIT_FAILED;
    }

    // This only needs to be done once, because both timers use the same system function - QueryPerformanceCounter()
    if (m_codec == FLM_CAPTURE_CODEC_TYPE::AMF)
        m_timer.UpdateAmfTimeToPerformanceCounterOffset();

    m_hMouseThread = CreateThread(0, 0, FLM_Pipeline::MouseEventThreadFunctionStub, this, 0, NULL);
    if (m_hMouseThread == NULL)
    {
        FlmPrintError("Failed to create mouse thread");
        return FLM_STATUS::CREATE_MOUSE_THREAD_FAILED;
    }

#ifndef CAPFRAMEX_FLM_EMBEDDED
    m_hKbdThread = CreateThread(0, 0, FLM_Pipeline::KeyboardListenThreadFunctionStub, this, 0, NULL);
    if (m_hKbdThread == NULL)
    {
        FlmPrintError("Failed to create keyboard thread");
        return FLM_STATUS::CREATE_KEYBOARD_THREAD_FAILED;
    }
#endif

    if (m_capture->InitCapture(m_timer) == false)
    {
        FlmPrintError("m_capture->InitCapture failed");
        return FLM_STATUS::TIMER_INIT_FAILED;
    }
    // Transfer some capture settings over to pipeline
    m_runtimeOptions.iCaptureX      = m_capture->m_iCaptureOriginX;
    m_runtimeOptions.iCaptureY      = m_capture->m_iCaptureOriginY;
    m_runtimeOptions.iCaptureWidth  = m_capture->m_iCaptureWidth;
    m_runtimeOptions.iCaptureHeight = m_capture->m_iCaptureHeight;

#ifdef CAPTURE_FRAMES_ON_SEPARATE_THREAD
    m_hCaptureThread = CreateThread(0, 0, FLM_Pipeline::CaptureDisplayThreadFunctionStub, this, 0, NULL);
    if (m_hCaptureThread == NULL)
    {
        FlmPrintError("Failed to create capture thread");
        return FLM_STATUS::CREATE_CAPTURE_THREAD_FAILED;
    }
#endif
#ifndef CAPFRAMEX_FLM_EMBEDDED
    m_bVirtualTerminalEnabled = SetConsoleMode(FlmGetConsoleHandle(), ENABLE_PROCESSED_OUTPUT | ENABLE_VIRTUAL_TERMINAL_PROCESSING);

    if (m_bVirtualTerminalEnabled == false)
        FlmPrint("Warning: Unable to set console terminal mode features\n");
#endif

    m_capture->m_bDoCaptureFrames       = true;
    m_runtimeOptions.monitorRefreshRate = 60;

    // Get device info for primary monitor
    DWORD    iModeNum   = ENUM_CURRENT_SETTINGS;
    DEVMODEA DeviceMode = {};
    if (EnumDisplaySettingsA(NULL, iModeNum, &DeviceMode))
    {
        m_runtimeOptions.monitorRefreshRate = (int)DeviceMode.dmDisplayFrequency;
        if (DeviceMode.dmDisplayFrequency >= 239)
            m_runtimeOptions.biasOffset = m_setting.monitorCalibration_240Hz;
        else if (DeviceMode.dmDisplayFrequency >= 143)
            m_runtimeOptions.biasOffset = m_setting.monitorCalibration_144Hz;
        else if (DeviceMode.dmDisplayFrequency >= 119)
            m_runtimeOptions.biasOffset = m_setting.monitorCalibration_120Hz;
        else if (DeviceMode.dmDisplayFrequency >= 59)
            m_runtimeOptions.biasOffset = m_setting.monitorCalibration_60Hz;
        else if (DeviceMode.dmDisplayFrequency >= 49)
            m_runtimeOptions.biasOffset = m_setting.monitorCalibration_50Hz;
        else if (DeviceMode.dmDisplayFrequency >= 23)
            m_runtimeOptions.biasOffset = m_setting.monitorCalibration_24Hz;
        else
        {
            m_runtimeOptions.biasOffset = 0.0f;
        }
        FlmPrint("Monitor refresh rate is at %3d Hz\n", DeviceMode.dmDisplayFrequency);
    }
    else
        FlmPrint("Warning: Unable to get default monitor display settings,Mouse click Bias offset set to 0.0 ms\n");

    // Adjust for capture frame position in monitor refresh
    m_autoRefreshScanOffset = CalculateAutoRefreshScanOffset();

    return FLM_STATUS::OK;
}

void FLM_Pipeline::ProcessKeyboardCommands()
{
#ifdef CAPFRAMEX_FLM_EMBEDDED
    return;
#else
    bool bDo = false; // needed for IF_CONDITION_BECOMES_TRUE macro

    // 1. Handle app exit
    IF_CONDITION_BECOMES_TRUE( m_keyboard.KeyCombinationPressed(m_appExitKeys) )
    {
        m_bExitApp = true;
        return;
    }

    // 2. Handle start/stop measurements
    IF_CONDITION_BECOMES_TRUE( m_keyboard.KeyCombinationPressed(m_measurementKeys) )
    {
        if (m_bMeasuringInProgress == false)  // we are about to turn on the measurement
        {
            PrintStream("\nStarting measuring\n");
            StartMeasurements();
        }
        else
        {
            PrintStream("\nStopped measuring ");
            StopMeasurements();
        }
        return;
    }

    // 3. Handle capturing of surface image
    IF_CONDITION_BECOMES_TRUE( m_keyboard.KeyCombinationPressed(m_captureSurfaceKeys) )
    {
        m_capture->SaveCaptureSurface(0);
        if (g_pUserCallBack)
            g_pUserCallBack(FLM_PROCESS_MESSAGE_TYPE::PRINT, "image file saved\n");
        return;
    }

    // 4. Handle capture loop validation
    IF_CONDITION_BECOMES_TRUE( m_keyboard.KeyCombinationPressed(m_validateCaptureKeys) )
    {
        m_bValidateCaptureLoop = true;
        m_validateCounter      = 0;
        return;
    }

    // 5. Handle showing/hiding of settings dialog box on right mouse button click
    IF_CONDITION_BECOMES_TRUE( KEY_DOWN(VK_RBUTTON) )
    {
        if( g_ui.ui_showing )
            g_user_interface.HideUI();
        else
        {
            HWND hFrgWnd = GetForegroundWindow();
            while( GetParent(hFrgWnd) )
                hFrgWnd = GetParent(hFrgWnd);

            if( hFrgWnd == g_ui.hWndConsole ) // Check if the mouse click happened in our window
            {
                POINT p = {};
                GetCursorPos(&p);
                g_user_interface.ShowUI(p.x, p.y);
            }
        }
        return;
    }
#endif
}

void FLM_Pipeline::ResetState()
{
    PIPELINE_DEBUG_PRINT_STACK()

    m_iCumulativeLatencySamples    = 0;
    m_fCumulativeLatencyTimesMS    = 0;
    m_fAccumulatedLatencyMS        = 0;
    m_fAccumulatedFrameTimeMS      = 0;
    m_iMeasurementPhaseCounter     = 0;
    m_iiMouseMoveEventTime         = 0;
    m_iSkipMeasurementsOnInitCount = 1; // = 2; // Skip a few initial measurements, just in case
    m_telemetry.Reset();
    m_capture->ResetState();

    std::lock_guard<std::mutex> lock(m_latencySamplesMutex);
    m_latencySampleReadIndex = 0;
    m_latencySampleWriteIndex = 0;
    m_latencySampleCount = 0;
    m_latencySampleSequence = 0;
}

void FLM_Pipeline::Close()
{
    PIPELINE_DEBUG_PRINT_STACK()

    if (m_bMeasuringInProgress)
        StopMeasurements();

    Sleep(200);

    // Wait on threads
    if (m_hKbdThread != NULL)
    {
        m_bTerminateKeyboardThread = true;
        while (m_bExitKeyboardThread == false)
            Sleep(10);
        WaitForSingleObject(m_hKbdThread, 1000);
        CloseHandle(m_hKbdThread);
        m_hKbdThread = NULL;
    }

    if (m_hMouseThread != NULL)
    {
        m_bTerminateMouseThread = true;
        SetEvent(m_eventMovementDetected);
        while (m_bExitMouseThread == false)
            Sleep(10);
        WaitForSingleObject(m_hMouseThread, 1000);
        CloseHandle(m_hMouseThread);
        m_hMouseThread = NULL;
    }

    if (m_hCaptureThread != NULL)
    {
        if (m_capture)
        {
            m_capture->m_bTerminateCaptureThread = true;
            SetEvent(m_capture->m_hEventFrameReady);
            while (m_capture->m_bExitCaptureThread == false)
                Sleep(10);
        }
        WaitForSingleObject(m_hCaptureThread, 1000);
        CloseHandle(m_hCaptureThread);
        m_hCaptureThread = NULL;
    }

    if (m_capture)
    {
        if (m_setting.showBoundingBox)
            m_capture->ClearCaptureRegion();
        delete m_capture;
        m_capture = NULL;
    }

    if (m_eventMovementDetected != NULL)
    {
        CloseHandle(m_eventMovementDetected);
        m_eventMovementDetected = NULL;
    }

    m_timer.Close();
    FlmClearErrorStr();
}

FLM_PROCESS_STATUS FLM_Pipeline::Process()
{
    if (m_bExitApp)
    {
        if (m_setting.saveToFile)
            CloseCSV();
        return FLM_PROCESS_STATUS::CLOSE;
    }

    //FLM_Profile_Timer profile_timer(__FUNCTION__);

    static bool                  captureRegionChanged = false;
    static FLM_Performance_Timer m_lapTimer;
    float                        laptime = (float)m_lapTimer.Stop_ms();
    m_lapTimer.Start();

    PIPELINE_DEBUG_PRINT_STACK()

#ifndef CAPFRAMEX_FLM_EMBEDDED
    if (m_runtimeOptions.saveUserSettings)
    {
        int response = MessageBox(nullptr,"Save changes to flm.ini file", "FLM settings changed", MB_TOPMOST| MB_YESNO);
        if(response  == IDYES)
        {
            saveUserSettings();
        }
        m_runtimeOptions.saveUserSettings = false;
    }
#endif

    if (m_runtimeOptions.captureRegionChanged)
    {
        captureRegionChanged = true;

        m_capture->m_iCaptureOriginX = m_runtimeOptions.iCaptureX;
        m_capture->m_iCaptureOriginY = m_runtimeOptions.iCaptureY;
        m_capture->m_iCaptureWidth   = m_runtimeOptions.iCaptureWidth;
        m_capture->m_iCaptureHeight  = m_runtimeOptions.iCaptureHeight;

        if ((m_bMeasuringInProgress == false) && m_setting.showBoundingBox)
            m_capture->ClearCaptureRegion();
        m_runtimeOptions.captureRegionChanged = false;

        m_autoRefreshScanOffset = CalculateAutoRefreshScanOffset();
    }

    if ((m_runtimeOptions.showOptions == false) && captureRegionChanged)
    {
        captureRegionChanged = false;
#ifndef CAPFRAMEX_FLM_EMBEDDED
        saveUserSettings(); // This is needed as rebuild load window size settings
#endif
        m_capture->m_bNeedToRebuildPipeline = true;
    }

    if (m_capture == NULL)
        return FLM_PROCESS_STATUS::CLOSE;

    if (m_bTerminateMouseThread)
        return FLM_PROCESS_STATUS::CLOSE;

    {
        m_iiFrameTimeStampPrev = m_iiFrameTimeStamp;
        m_iiFrameIdxPrev       = m_iiFrameIdx;
        m_iThSADPrev           = m_iThSAD;

        if (m_capture->AcquireFrameAndDownscaleToHost(&m_iiFrameTimeStamp, &m_iiFrameIdx))
        {
            m_iSAD          = m_capture->CalculateSAD();
            m_iThSAD        = m_capture->GetThresholdedSAD(m_runtimeOptions.printLevel == FLM_PRINT_LEVEL::PRINT_DEBUG ? m_iiFrameIdx : 0,
                                                           m_iSAD, m_runtimeOptions.thresholdCoefficient[m_runtimeOptions.mouseEventType]);
            if (m_runtimeOptions.mouseEventType == FLM_MOUSE_EVENT_TYPE::MOUSE_CLICK)
            {
                if (m_iThSAD != 0)
                {
                    SetEvent(m_eventMovementDetected);
                    return FLM_PROCESS_STATUS::PROCESSING;
                }
            }
        }
        else
        {
            // No frames captured, check if we need to rebuild pipeline
            m_iSAD   = 0;
            m_iThSAD = 0;

            if (m_capture->m_bNeedToRebuildPipeline)
            {
                bool hold_startMeasurements = m_bMeasuringInProgress;
                bool hold_DoCaptureFrames   = m_capture->m_bDoCaptureFrames;

                PrintStream("Rebuilding pipeline - please wait...");
                if (hold_startMeasurements)
                    StopMeasurements();

                // Rebuild capture pipeline
                m_capture->Release();
                bool bRes = m_capture->InitCapture(m_timer);

                if (bRes)
                    PrintStream("\nRebuilding pipeline: DONE! Ready for start measurements.\n");
                else
                    PrintStream("\n");

                if (hold_startMeasurements)
                    StartMeasurements();

                m_capture->m_bDoCaptureFrames = hold_DoCaptureFrames;
            }
            else
                return (m_bMeasuringInProgress ? FLM_PROCESS_STATUS::PROCESSING : FLM_PROCESS_STATUS::WAIT_FOR_START);
        }

        bool bGotMeasurement = (m_iThSAD != 0) && (m_iiMouseMoveEventTime != 0) && m_bMeasuringInProgress;  // This is an overkill

        if ((m_runtimeOptions.mouseEventType == FLM_MOUSE_EVENT_TYPE::MOUSE_CLICK) && m_bMouseClickDetected && m_bMeasuringInProgress)
        {
            if (m_runtimeOptions.autoBias)
                m_runtimeOptions.biasOffset = laptime + m_autoRefreshScanOffset;

            m_fLatestMeasuredLatencyMS = m_fLatestMeasuredLatencyMS + m_runtimeOptions.biasOffset * FLM_MOUSE_CLICK_ADJUST_BIAS_FOR_NOISE;
            if (m_fLatestMeasuredLatencyMS < FLM_MOUSE_CLICK_UPPER_LIMIT)
                bGotMeasurement = true;
            // FlmPrintStaticPos("[%d]", (int)m_fLatestMeasuredLatencyMS);
            m_bMouseClickDetected = false;
        }

        // Show capture region
        if ((m_bMeasuringInProgress == false) && m_setting.showBoundingBox)
        {
            static bool bPrevRAltDown = false;
            bool bRAltDown = KEY_DOWN(VK_RMENU);

            if (bRAltDown) // Only show the area if the right alt key is held down
                m_capture->ShowCaptureRegion((m_iiFrameIdx != m_iiFrameIdxPrev) ? RGB(0xFF, 0xFF, 0x00) : RGB(0xFF, 0x00, 0x00));

            // show current capture region values, while user is editing region
            if (captureRegionChanged)
            {
                m_capture->TextDC(m_capture->m_iCaptureOriginX,
                                    m_capture->m_iCaptureOriginY,
                                    "XY (%d,%d) WxH %dx%d ",
                                    m_capture->m_iCaptureOriginX,
                                    m_capture->m_iCaptureOriginY,
                                    m_capture->m_iCaptureWidth,
                                    m_capture->m_iCaptureHeight);
            }

            if (bRAltDown != bPrevRAltDown)
                m_capture->RedrawMainScreen();

            bPrevRAltDown = bRAltDown;
        }

        // We need to have at least one non-motion frame, otherwise GetThresholdedSAD() will get confused.
        // Also, motion blur tends to cause two consecutive detections...
        if ((m_iThSADPrev != 0) && (m_runtimeOptions.mouseEventType == FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE))
            bGotMeasurement = false; // Ignore the motion detection in the frame right after motion - this is probably "motion blur" effect...

        if (bGotMeasurement && m_iSkipMeasurementsOnInitCount)
        {
            m_iSkipMeasurementsOnInitCount--;
            bGotMeasurement = false;
        }

        if (bGotMeasurement) // check again
        {
            const int64_t inputTimestamp = m_iiMouseMoveEventTime;
            if (m_runtimeOptions.mouseEventType == FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE)
                m_fLatestMeasuredLatencyMS = (m_iiFrameTimeStamp - m_iiMouseMoveEventTime) / float(AMF_MILLISECOND);

            UpdateAverageLatency(m_fLatestMeasuredLatencyMS);

            if (m_codec == FLM_CAPTURE_CODEC_TYPE::AMF)
                m_iiMotionDetectedFrameFlipTime = m_timer.TranslateAmfTimeToPerformanceCounter(m_iiFrameTimeStamp);
            else
                m_iiMotionDetectedFrameFlipTime = m_iiFrameTimeStamp;

            if (m_runtimeOptions.mouseEventType == FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE)
            {
                FLM_LATENCY_SAMPLE sample;
                sample.sequence = ++m_latencySampleSequence;
                sample.inputQpc = m_codec == FLM_CAPTURE_CODEC_TYPE::AMF
                    ? m_timer.TranslateAmfTimeToPerformanceCounter(inputTimestamp)
                    : inputTimestamp;
                sample.frameQpc = m_iiMotionDetectedFrameFlipTime;
                sample.latencyMs = m_fLatestMeasuredLatencyMS;
                sample.latencyFrames = m_capture->m_fMovingAverageFrameTimeMS > 0.0f
                    ? m_fLatestMeasuredLatencyMS / m_capture->m_fMovingAverageFrameTimeMS - 0.5f
                    : 0.0f;
                sample.fps = m_telemetry.fps;
                PushLatencySample(sample);
            }

            m_iiMouseMoveEventTime = 0;

            if (m_runtimeOptions.mouseEventType == FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE)
                SetEvent(m_eventMovementDetected);

            if (m_runtimeOptions.printLevel == FLM_PRINT_LEVEL::ACCUMULATED)
                PrintAverageTelemetry(m_fLatestMeasuredLatencyMS);
            else
            if ((m_runtimeOptions.printLevel == FLM_PRINT_LEVEL::RUN) || (m_runtimeOptions.printLevel == FLM_PRINT_LEVEL::OPERATIONAL))
                PrintOperationalTelemetry(m_fLatestMeasuredLatencyMS, m_runtimeOptions.printLevel == FLM_PRINT_LEVEL::OPERATIONAL);
            else
                PrintDebugTelemetry(m_fLatestMeasuredLatencyMS);

            // Test validation of captured frames that were processed
            if (m_bValidateCaptureLoop)
            {
                if (m_validateCounter < m_setting.validateCaptureNumOfFrames)
                {
                    m_validateCounter++;
                    FlmPrintStaticPos("Saving to file frame %3d", m_validateCounter);
                    m_capture->SaveCaptureSurface(m_validateCounter);
                }
                else
                {
                    m_validateCounter      = 0;
                    m_bValidateCaptureLoop = false;
                    FlmPrintClearEndOfLine();
                }
            }
        }
    }

    return m_bMeasuringInProgress ? FLM_PROCESS_STATUS::PROCESSING : FLM_PROCESS_STATUS::WAIT_FOR_START;
}

void FLM_Pipeline::PushLatencySample(const FLM_LATENCY_SAMPLE& sample)
{
    std::lock_guard<std::mutex> lock(m_latencySamplesMutex);

    m_latencySamples[m_latencySampleWriteIndex] = sample;
    m_latencySampleWriteIndex = (m_latencySampleWriteIndex + 1) % LATENCY_SAMPLE_CAPACITY;

    if (m_latencySampleCount == LATENCY_SAMPLE_CAPACITY)
        m_latencySampleReadIndex = (m_latencySampleReadIndex + 1) % LATENCY_SAMPLE_CAPACITY;
    else
        ++m_latencySampleCount;
}

bool FLM_Pipeline::TryGetLatencySample(FLM_LATENCY_SAMPLE& sample)
{
    std::lock_guard<std::mutex> lock(m_latencySamplesMutex);
    if (m_latencySampleCount == 0)
        return false;

    sample = m_latencySamples[m_latencySampleReadIndex];
    m_latencySampleReadIndex = (m_latencySampleReadIndex + 1) % LATENCY_SAMPLE_CAPACITY;
    --m_latencySampleCount;
    return true;
}

// Class Factory Interface
FLM_Context* CreateFLMContext()
{
    FLM_Pipeline* flame = new (std::nothrow) FLM_Pipeline();
    return (FLM_Context*)flame;
}
