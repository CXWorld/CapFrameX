//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_capture_amf.h
/// @brief  FLM AMF capture interface header
//=============================================================================

#ifndef FLM_CAPTURE_AMF_H
#define FLM_CAPTURE_AMF_H

#include <Windows.h>
#include <d3d11.h>

#pragma warning(push)
#pragma warning(disable : 4996)

#include "public/common/AMFFactory.h"
#include "public/include/components/DisplayCapture.h"
#include "public/include/components/VideoConverter.h"

#pragma warning(pop)

#include "flm.h"
#include "flm_utils.h"
#include "flm_capture_context.h"

extern ProgressCallback* g_pUserCallBack;

class FLM_Capture_AMF : public FLM_Capture_Context
{
public:
    FLM_Capture_AMF(FLM_RUNTIME_OPTIONS *runtimeOptions);
    ~FLM_Capture_AMF();

    int          CalculateSAD();
    unsigned int GetImageFormat();
    bool         GetConverterOutput(int64_t* pTimeStamp, int64_t* pFrameIdx);
    FLM_STATUS   GetFrame();
    FLM_STATUS   GetFrameBuffer(FLM_PIXEL_DATA& pixelData);
    FLM_STATUS   InitCaptureDevice(unsigned int OutputAdapter, FLM_Timer_AMF* timer);
    bool         InitContext(FLM_GPU_VENDOR_TYPE vendor);
    void         Release();
    FLM_STATUS   ReleaseFrameBuffer(FLM_PIXEL_DATA& pixelData);
    void         SaveCaptureSurface(uint32_t file_counter);

private:
    void       AMF_SaveImage(const char* filename, amf::AMFPlane* plane);
    int        GetConvertersCount();
    AMF_RESULT InitConverters();
    AMF_RESULT InitSurfaces();
    void       ReleaseDisplay();
    void       ReleaseSurfaces();
    void       ReleaseConverters();
    AMF_RESULT UpdateFormat();

    amf::AMFSurfacePtr   m_pHostSurface0;       // This is the CPU-accessible surface 0
    amf::AMFSurfacePtr   m_pHostSurface1;       // This is the CPU-accessible surface 1
    amf::AMFSurfacePtr   m_pTargetHostSurface;  //current captured surface either m_pHostSurface0 or m_pHostSurface1
    amf::AMFContextPtr   m_pContext;
    amf::AMFComponentPtr m_pDisplayCapture;

    const static int MAX_CONVERTERS     = 16;  // Up to 16 converters
    int              m_iNumConverters    = MAX_CONVERTERS;
    bool             m_bLatestSurfaceIs1 = false;
    bool             m_bHostSurfaceInit  = false;

    amf::AMFComponentPtr m_ppConverters[MAX_CONVERTERS];
    amf::AMFDataPtr      m_ppConverterOutputs[MAX_CONVERTERS];

    FLM_GPU_VENDOR_TYPE m_vendor = FLM_GPU_VENDOR_TYPE::UNKNOWN;

protected:
    FLM_Capture_AMF(); // hide the default constructor
};

#endif