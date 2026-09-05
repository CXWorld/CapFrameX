//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_capture_context.h
/// @brief  user pipeline definitions
//=============================================================================

#ifndef FLM_CAPTURE_CONTEXT_H
#define FLM_CAPTURE_CONTEXT_H

#include "flm.h"
#include "flm_utils.h"
#include "flm_timer.h"

#include "ini/SimpleIni.h"
#include <atomic>
#include "../../../FlmSadFilter.h"

extern ProgressCallback* g_pUserCallBack;

struct FLM_CAPTURE_SETTINGS
{
    float       fStartX             = 0.25f;
    float       fStartY             = 0.0f;
    float       fCaptureWidth       = 0.75f;             // size = 3/4  of bufferWidth
    float       fCaptureHeight      = 0.125f;            // size = 1/8  of bufferHeight
    std::string captureFileName     = "captured_frame";  // file name for saved frames to image, exclude file extension
    int         iAVGFilterFrames    = 100;               // Can be set by user via ini
    int         iFilmGrainThreshold = 4;                 // film grain
};

class FLM_Capture_Context
{
public:
    virtual ~FLM_Capture_Context() = default;

    virtual int          CalculateSAD()                                                      = 0;
    virtual bool         GetConverterOutput(int64_t* pTimeStamp, int64_t* pFrameIdx)         = 0;
    virtual FLM_STATUS   GetFrame()                                                          = 0;
    virtual unsigned int GetImageFormat()                                                    = 0;
    virtual FLM_STATUS   InitCaptureDevice(unsigned int OutputAdapter, FLM_Timer_AMF* timer) = 0;
    virtual bool         InitContext(FLM_GPU_VENDOR_TYPE vendor)                             = 0;
    virtual void         Release()                                                           = 0;
    virtual FLM_STATUS   ReleaseFrameBuffer(FLM_PIXEL_DATA& pixelData)                       = 0;
    virtual void         SaveCaptureSurface(uint32_t file_counter)                           = 0;

    FLM_RUNTIME_OPTIONS* m_pRuntimeOptions = nullptr;
    FLM_CAPTURE_SETTINGS m_setting;

    std::atomic_bool m_bFrameLocked  = false;  // GetFrame() sets this to true, will not capture a new frame until flag is reset by CopyImage
    int8_t  m_iCurrentFrame          = 0;
    int8_t  m_iCurrentFrameHold      = -1;
    int32_t m_iOutputAdapter         = 0;      // 0: default primary monitor, else 1..n where n is total number of monitors
    std::atomic_bool m_bNeedToRebuildPipeline = false;  // Processing

    // Normalized co-ordinates for display resolution width(0..1) and height = (0..1)
    // or pixel position and size when values range < 0.0 or > 1.0
    int m_iDownScale = 2;

    uint32_t    m_iBackBufferFormat        = 0;  // This varies according to capture codecs been used AMF, DXGI, ...
    uint32_t    m_iBackBufferWidth         = 0;  // Display width
    uint32_t    m_iBackBufferHeight        = 0;  // Display height
    int         m_iCaptureOriginX          = 0;
    int         m_iCaptureOriginY          = 0;
    int32_t     m_iCaptureWidth            = 0;  // capture width in pixel coordinates (can be smaller then full frame buffer size)
    int32_t     m_iCaptureHeight           = 0;  // capture height in pixel coordinates (can be smaller then full frame buffer size)
    int         m_iUserSetOutputAdapter    = 0;
    std::string m_displayName              = "";
    HANDLE      m_hEventFrameReady         = 0;
    HDC         m_screenHDC                = 0;
    float       m_fBackgroundSAD           = 0.0f;
    std::atomic_bool m_bDoCaptureFrames    = true;

    float       m_fCumulativeFrameTimesMS        = 0.0f;
    int         m_iCumulativeFrameTimeSamples    = 0;
    float       m_fMovingAverageFrameTimeMS      = 0.0f; // not strictly a moving average - it is implemented via IIR rather than FIR filter
    float       m_fMovingAverageOddFramesTimeMS  = 0.0f; // not strictly a moving average - it is implemented via IIR rather than FIR filter
    float       m_fMovingAverageEvenFramesTimeMS = 0.0f; // not strictly a moving average - it is implemented via IIR rather than FIR filter
    std::atomic_bool m_bTerminateCaptureThread = false;
    std::atomic_bool m_bExitCaptureThread      = false;

    //samples are needed to get within 1% of the final value
    float m_fAVGFilterAlpha   = 0.0f;  // Result of CalculateFilterAlpha() for m_iAVGFilterFrames
    float m_fClickFilterAlpha = 0.0f;  // Result of CalculateFilterAlpha()

    bool AcquireFrameAndDownscaleToHost(int64_t* pTimeStamp, int64_t* pFrameIdx);
    void ClearCaptureRegion();
    void RedrawMainScreen();
    void DisplayThreadFunction();
    int  GetThresholdedSAD(int64_t frameIdx, int iSAD, float fThresholdMultiplierCoeff, bool updateBaseline = true);
    bool InitCapture(FLM_Timer_AMF& m_timer);
    void InitSettings();
    void ResetState();
    void TextDC(int x, int y, const char* Format, ...);
    void SaveAsBitmap(const char* filename, FLM_PIXEL_DATA pixelData, bool vertFlip);
    void ShowCaptureRegion(COLORREF color);
    void UpdateAverageFrameTime(int64_t iiTimeStamp, int64_t iiFrameIdx);

private:
    FlmSadFilter m_sadFilter;
    int64_t m_previousFrameTimestamp = 0;
    int64_t m_previousFrameIndex = 0;
    FLM_STATUS   LoadUserSettings();
    FLM_STATUS   SaveUserSettings();
    static float CalculateFilterAlpha(int iNumIterations);
};

#endif
