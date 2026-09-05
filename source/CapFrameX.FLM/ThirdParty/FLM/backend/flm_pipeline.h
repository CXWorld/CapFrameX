//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_pipeline.h
/// @brief  FLM pipeline interface header
//=============================================================================

#ifndef FLM_PIPELINE_H
#define FLM_PIPELINE_H

#include "flm.h"
#include "flm_utils.h"
#include "flm_timer.h"
#include "flm_keyboard.h"
#include "flm_mouse.h"

#include "flm_capture_AMF.h"
#include "flm_capture_DXGI.h"

#include <inttypes.h>
#include <array>
#include <atomic>
#include <mutex>
#include "../../../FlmPassiveClickTracker.h"
#include "ini/SimpleIni.h"

struct FLM_PIPELINE_SETTINGS
{
    bool         showBoundingBox           = true;      // Show a frame capture region using dimensions set in "CAPTURE" section, set 0 to disable, 1 to enable
    bool         showAdvancedMeasurements  = false;     // mode to show stats like odd,even and AVG, RUN latency measurements
    float        extraWaitMilliseconds     = 10.0f;     // Number of milliseconds to wait after motion has been detected, before starting the next measurement
    int          extraWaitFrames           = 1;         // 
    float        extraWaitMillisecondsFG   = 20.0f;     // The extra wait frames are needed to prevent locking onto the double frequency and also to prevent problems with motion blur
    int          extraWaitFramesFG         = 3;         // 
    std::string  measurementKeys           = "ALT+T";   // Use both keys combined to start and stop measurements
    std::string  appExitKeys               = "ALT+Q";   // Use both keys combined to exit application
    std::string  captureSurfaceKeys        = "RSHIFT+ENTER";  // capture the current frame to file, the file name is set in "CAPTURE" option using "CaptureFile"
    std::string  validateCaptureKeys       = "LSHIFT+ENTER";  // Capture a sequence of frames numbered set in ValidateCaptureNumOfFrames to BMP files
    bool         saveToFile                = true;            // Sets saving measurements to a specified CSV file
    std::string  outputFileName            = "fml_latency.csv";  // File to save measurements
    unsigned int iNumMeasurementsPerLine   = 16;                 // Number of measurements taken before averaging. Default 16 Range:1 to 32
    int          iNumDequantizationPhases  = 2;                  // This introduces a very small periodic phase shift to work around the quantization
    int          validateCaptureNumOfFrames = 32;                // Number of frames to capture
    int          iMouseHorizontalStep       = 50;                // Mouse horizontal step size , can be adjusted if game requires a wider value
    float        monitorCalibration_240Hz   = 0.0;
    float        monitorCalibration_144Hz   = 0.0;
    float        monitorCalibration_120Hz   = 0.0;
    float        monitorCalibration_60Hz    = 0.0;
    float        monitorCalibration_50Hz    = 0.0;
    float        monitorCalibration_24Hz    = 0.0;
};

class FLM_Pipeline : public FLM_Context
{
public:
    FLM_STATUS          Init(FLM_CAPTURE_CODEC_TYPE codec);
    FLM_PROCESS_STATUS  Process();
    void                Close();

    void                SetUserProcessCallback(ProgressCallback Info);
    std::string         GetErrorStr();
    FLM_GPU_VENDOR_TYPE GetGPUVendorType();
    FLM_CAPTURE_CODEC_TYPE GetCodec() { return m_codec; }
    void                ResetState();
    std::string         GetToggleKeyNames();
    std::string         GetAppExitKeyNames();
    int                 GetBackBufferWidth();
    int                 GetBackBufferHeight();
    void                ProcessKeyboardCommands();
    void                SendMouseMove();
    bool                WaitForFrameDetection();
    FLM_STATUS          loadUserSettings();
    FLM_STATUS          saveUserSettings();
    void                StartMeasurements();
    void                StopMeasurements();
    bool                TryGetLatencySample(FLM_LATENCY_SAMPLE& sample);
    FlmPassiveClickTracker::Snapshot GetClickStatus();
    void                RequestShutdown();

    FLM_PIPELINE_SETTINGS m_setting;

    HWND                 m_hWnd = GetConsoleWindow();

private:
    FlmPassiveClickTracker m_clickTracker;
    int64_t m_qpcFrequency = 1;
    int64_t m_lastCaptureQpc = 0;
    FLM_STATUS InitSettings();
    FLM_STATUS SetCodec(std::string codec);
    void       UpdateAverageLatency(float fLatencyMS);
    void       PushLatencySample(const FLM_LATENCY_SAMPLE& sample);
    float      CalculateAutoRefreshScanOffset();
    bool       isRunningOnPrimaryDisplay();

    FLM_TELEMETRY_DATA     m_telemetry;
    FLM_Keyboard           m_keyboard;
    FLM_Timer_AMF          m_timer;
    FLM_Performance_Timer  m_timer_performance;
    FLM_Mouse              m_mouse;
    FLM_Capture_Context*   m_capture              = NULL;
    FLM_CAPTURE_CODEC_TYPE m_codec                = FLM_CAPTURE_CODEC_TYPE::AUTO;
    bool                   m_bValidateCaptureLoop = false;  // when set will run a validation capture loop that save current captured latency frame used in SAD
    bool                   m_bVirtualTerminalEnabled = false;  // This is set to true if windows virtual terminal escape char is supported

    // Thread termination
    std::atomic_bool m_bTerminateMouseThread    = false;
    std::atomic_bool m_bTerminateKeyboardThread = false;
    std::atomic_bool m_bExitMouseThread         = false;
    std::atomic_bool m_bExitKeyboardThread      = false;
    std::atomic_bool m_bExitApp                 = false;

    static DWORD WINAPI MouseEventThreadFunctionStub(LPVOID lpParameter);
    static DWORD WINAPI KeyboardListenThreadFunctionStub(LPVOID lpParameter);
    static DWORD WINAPI CaptureDisplayThreadFunctionStub(LPVOID lpParameter);

    void MouseEventThreadFunction();
    void KeyboardListenThreadFunction();

    // configurable outputs
    void CreateCSV();
    void CloseCSV();
    void SaveTelemetryCSV();
    void PrintStream(const char* format, ...);
    void PrintAverageTelemetry(float fFrameLatencyMS);
    void PrintOperationalTelemetry(float fFrameLatencyMS, bool bFull);
    void PrintDebugTelemetry(float fFrameLatencyMS);

    int     m_iUserSetVendorType            = 0;
    float   m_fCumulativeLatencyTimesMS     = 0.0f;
    int     m_iCumulativeLatencySamples     = 0;
    float   m_fAccumulatedLatencyMS         = 0.0f;
    float   m_fAccumulatedFrameTimeMS       = 0.0f;
    std::atomic_bool m_bMeasuringInProgress = false;  // State of latency measurements
    HANDLE  m_eventMovementDetected         = NULL;
    bool    m_bMouseClickDetected           = false;
    int64_t m_iiMouseMoveEventTime          = 0;
    int64_t m_iiMouseClickEventTime         = 0;  // CapFrameX: QPC of the real user click (MOUSE_CLICK mode)
    int     m_iMeasurementPhaseCounter      = 0;
    int     m_iDequantizingPhaseCounter     = 0;
    int64_t m_iiMotionDetectedFrameFlipTime = 0;
    int     m_iSkipMeasurementsOnInitCount  = 0;

    // set by user as defined in FLM_PIPELINE_SETTINGS and override from json
    int           m_iSetVendor             = 0;  //  0 using GetGPUVendorType else set vendor to FLM_GPU_VENDOR_TYPE (1 = AMD 2 = Nvidia 3 = Intel)
    unsigned char m_measurementKeys[3]     = {0, 0, 0};
    unsigned char m_appExitKeys[3]         = {0, 0, 0};
    unsigned char m_captureSurfaceKeys[3]  = {0, 0, 0};
    unsigned char m_validateCaptureKeys[3] = {0, 0, 0};
    FILE*         m_outputFile             = NULL;

    // testCapture Options
    int m_validateCounter = 0;

    // Variables used in the MainLoop():

    int64_t m_iiFrameTimeStampPrev = 0;
    int64_t m_iiFrameTimeStamp     = 0;
    int64_t m_iiFrameIdx           = 0;
    int64_t m_iiFrameIdxPrev       = 0;

    float m_fLatestMeasuredLatencyMS = 0.0f;
    int   m_iSAD                     = 0;
    int   m_iThSAD                   = 0;
    int   m_iThSADPrev               = 0;
    float m_autoRefreshScanOffset    = 0.0f;

    // Threads

    HANDLE m_hMouseThread   = NULL;
    HANDLE m_hKbdThread     = NULL;
    HANDLE m_hCaptureThread = NULL;

    static constexpr size_t LATENCY_SAMPLE_CAPACITY = 256;
    std::array<FLM_LATENCY_SAMPLE, LATENCY_SAMPLE_CAPACITY> m_latencySamples = {};
    std::mutex m_latencySamplesMutex;
    size_t m_latencySampleReadIndex  = 0;
    size_t m_latencySampleWriteIndex = 0;
    size_t m_latencySampleCount      = 0;
    uint64_t m_latencySampleSequence = 0;
};

#endif
