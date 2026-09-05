//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm.h
/// @brief  FLM user pipeline definitions
//=============================================================================

#ifndef FLM_H
#define FLM_H

#include <string>
#include <vector>
#include "stdint.h"
#include "version.h"

// #define FLM_DEBUG_CODE

#define USE_FRAME_LOCK_AMF
#define USE_FRAME_LOCK_DXGI

#if defined(WIN32) || defined(_WIN64)
#define FLM_API __cdecl
#else
#define FLM_API
#endif

#define APP_NAME_STRING "Frame latency meter"

#define PROCESSOR_YIELD_CYCLES 120

enum class FLM_GPU_VENDOR_TYPE
{
    UNKNOWN = 0,
    AMD     = 1,
    NVIDIA  = 2,
    INTEL   = 3
};

enum class FLM_CAPTURE_CODEC_TYPE
{
    AUTO    = 0,
    AMF     = 1,
    DXGI    = 2,
};

enum class FLM_PRINT_LEVEL
{
    RUN,
    ACCUMULATED,
    OPERATIONAL,
    PRINT_DEBUG,
    PRINT_LEVEL_COUNT
};

//#define MOUSE_EVENT_TYPE_SIZE 2
enum FLM_MOUSE_EVENT_TYPE
{
    MOUSE_MOVE  = 0,
    MOUSE_CLICK = 1,
    MOUSE_EVENT_TYPE_SIZE
};

enum class FLM_PROCESS_MESSAGE_TYPE
{
    PRINT,
    STATUS,
    ERROR_MESSAGE
};

enum class FLM_PROCESS_STATUS
{
    CLOSE = 0,
    WAIT_FOR_START,
    PROCESSING
};

enum class FLM_STATUS
{
    OK,
    FAILED,
    INIT_FAILED,
    TIMER_INIT_FAILED,
    CAPTURE_PROCESS_FRAME,
    CAPTURE_TIMEOUT,
    CAPTURE_RETRY,
    CAPTURE_INIT_FAILED,
    CAPTURE_ERROR_EXPECTED,
    CAPTURE_ERROR_UNEXPECTED,
    CAPTURE_REPEAT,
    CREATE_MOUSE_THREAD_FAILED,
    CREATE_KEYBOARD_THREAD_FAILED,
    CREATE_CAPTURE_THREAD_FAILED,
    MEMORY_ALLOCATION_ERROR,
    VENDOR_NOT_SUPPORTED,
    STATUS_COUNT
};

struct FLM_PIXEL_DATA
{
    uint8_t* data;
    int32_t  height;
    int32_t  width;
    int32_t  pitchH;
    int32_t  pixelSizeInBytes;
    uint32_t format;
    int64_t  timestamp;  // QueryPerformance time stamp for the frame, set internally by capture codecs
};

struct FLM_LATENCY_SAMPLE
{
    uint64_t sequence      = 0;
    int64_t  inputQpc      = 0;
    int64_t  frameQpc      = 0;
    float    latencyMs     = 0.0f;
    float    latencyFrames = 0.0f;
    float    fps           = 0.0f;
};

class FLM_TELEMETRY_DATA
{
public:
    float              fps        = 0.0f;  // Average framerate
    float              fpsOdd     = 0.0f;  // Average framerate for odd frames
    float              fpsEven    = 0.0f;  // Average framerate for even frames
    float              accLatency = 0.0f;  // latency [ms] accumulated over ALL measurements since the current capture session began
    float              accFrames  = 0.0f;  // latency [frames] accumulated over ALL measurements since the last capture session began
    float              accFps     = 0.0f;  // framerate accumulated over ALL measurements since the last capture session began
    float              rowLatency = 0.0f;  // latency [ms] averaged over the current row
    float              rowFrames  = 0.0f;  // latency [frames] averaged over the current row.
    std::vector<float> lMeasurementMS;     // the latencies[ms] for individual frames, size set by config MeasurementsPerLine

    void Reset()
    {
        FLM_TELEMETRY_DATA zeroObject;
        *this = zeroObject;
    }
};

struct FLM_RUNTIME_OPTIONS
{
    bool                 appWindowTopMost     = false;                 // true sets to stay on top of all window stacks
    FLM_PRINT_LEVEL      printLevel           = FLM_PRINT_LEVEL::RUN;  // Print measurement status 0:Run 1:Average  2:Operational 3:Debug
    FLM_MOUSE_EVENT_TYPE mouseEventType       = FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE;
    bool                 captureRegionChanged = false;  // set to true if user set a new capture region, FLM must reinitialize capture device if true
    bool                 saveUserSettings      = false; // Changes in UI that needs to be updated in ini file for next app run
    int                  iCaptureX             = 0;  // pixel co-ordinates
    int                  iCaptureY             = 0;  // pixel co-ordinates
    int                  iCaptureWidth         = 0;  // pixel co-ordinates
    int                  iCaptureHeight        = 0;  // pixel co-ordinates

    // Calibration offset for mouse click latency measurements
    // This offset provides a closer value to the actual system level latency
    // and is set to be constant for FLM measurements around 1/2 frame rates
    // this is not a linear relationship with monitors refresh rate near lower and upper ranges
    // (rates < 60 Hz and > 120 Hz), values may vary according to monitor types and settings
    // Users can set these ranges via ini file
    float biasOffset         = 0.0f;
    bool  autoBias           = false;
    int   monitorRefreshRate = 60;      // Default 60Hz

    // Threshold values used to end the measurement cycle of the mouse to frame latency measurement
    float thresholdCoefficient[MOUSE_EVENT_TYPE_SIZE] = {0.0f, 0.0f};

    bool showOptions = false;
    bool minimizeApp = false;
    bool gameUsesFrameGeneration = false;     // enable this to add extra wait time delay when using the mouse move option

    int  initAMFUsingDX12           = 0; // Set to 1 for AMF to use DX12 instead of DX11 (default)

#ifdef CAPFRAMEX_FLM_EMBEDDED
    int   mouseHorizontalStep       = 50;
    float captureStartX             = 0.125f;
    float captureStartY             = 0.03125f;
    float captureWidth              = 0.75f;
    float captureHeight             = 0.0149f;
    int   captureAverageFilterFrames = 100;
    int   captureFilmGrainThreshold  = 4;
    int   captureOutputIndex         = 0;
#endif

};

// function for messages provided during processing
typedef void ProgressCallback(FLM_PROCESS_MESSAGE_TYPE messagetype, const char* progress);

class FLM_Context
{
public:
    // Main code
    virtual FLM_STATUS         Init(FLM_CAPTURE_CODEC_TYPE codec) = 0;
    virtual FLM_PROCESS_STATUS Process()                                                      = 0;
    virtual void               Close()                                                        = 0;

    // Status
    virtual void                SetUserProcessCallback(ProgressCallback Info) = 0;
    virtual std::string         GetErrorStr()                                 = 0;
    virtual FLM_GPU_VENDOR_TYPE GetGPUVendorType()                            = 0;
    virtual FLM_CAPTURE_CODEC_TYPE GetCodec()                                 = 0;

    // User assigned app keys
    virtual std::string GetToggleKeyNames()  = 0;
    virtual std::string GetAppExitKeyNames() = 0;

    // Full frame capture surface sizes
    virtual int GetBackBufferWidth()  = 0;
    virtual int GetBackBufferHeight() = 0;

    FLM_RUNTIME_OPTIONS m_runtimeOptions;
};

// Class Factory Interface
extern "C" FLM_Context* CreateFLMContext();

// The embedded host owns a capture worker already. Keep acquisition, conversion,
// reset and rebuild on that worker to avoid racing over the backend frame buffers.
#ifndef CAPFRAMEX_FLM_EMBEDDED
#define CAPTURE_FRAMES_ON_SEPARATE_THREAD 1
#endif

#endif
