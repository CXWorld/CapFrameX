//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_capture_dxgi.h
/// @brief  FLM DXGI desktop capture interface header
//=============================================================================

// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved
// File: DuplicationManager.h

#ifndef FLM_DXGI_H
#define FLM_DXGI_H

#include <Windows.h>
#include <d3d11.h>
#include <dxgi1_2.h>
#include <sal.h>
#include <stdio.h>
#include <new>
#include <string>

#include "flm.h"
#include "flm_utils.h"
#include "flm_capture_context.h"

#define ACQUIRE_FRAME_CAPTURE_TIMEOUT 1000

extern HRESULT SystemTransitionsExpectedErrors[];
extern HRESULT CreateDuplicationExpectedErrors[];
extern HRESULT FrameInfoExpectedErrors[];
extern HRESULT AcquireFrameExpectedError[];
extern HRESULT EnumOutputsExpectedErrors[];

//
// Handles the task of duplicating an output.
//
class FLM_Capture_DXGI : public FLM_Capture_Context
{
public:
    FLM_Capture_DXGI(FLM_RUNTIME_OPTIONS *runtimeOptions);
    ~FLM_Capture_DXGI();

    FLM_STATUS   GetFrame();
    FLM_STATUS   ReleaseFrameBuffer(FLM_PIXEL_DATA& pixelData);
    FLM_STATUS   InitCaptureDevice(unsigned int MonitorToCapture, FLM_Timer_AMF* timer);
    unsigned int GetImageFormat();
    int          CalculateSAD();
    bool         GetConverterOutput(int64_t* pTimeSmp, int64_t* pFrameIdx);
    void         Release();
    void         SaveCaptureSurface(uint32_t file_counter);
    bool         InitContext(FLM_GPU_VENDOR_TYPE vendor);

private:
    DXGI_OUTDUPL_FRAME_INFO m_frameInfo;  // Current captured frame info obtained from GetFrame()
    FLM_PIXEL_DATA          m_pixelData[2]             = {};
    int                     m_iGetFrameInstance        = 0;  // Tracks AcquireNextFrame increments on success
    int64_t                 m_iiFreqCountPerSecond     = 0;
    int32_t                 m_iImagePitch              = 0;
    IDXGIResource*          m_pDesktopResource         = nullptr;
    ID3D11Device*           m_pD3D11Device             = nullptr;             // DirectX adapter object created to interface with GPU
    ID3D11DeviceContext*    m_pD3D11DeviceContext      = nullptr;             // Object interface to the adapter
    ID3D11Texture2D*        m_pDestGPUCopy[2]          = {nullptr, nullptr};  // Copy of acquired desktop resource on GPU used to copy data to CPU memory
    IDXGIOutputDuplication* m_pDXGIOutputDuplication   = nullptr;             // DXGI Desktop duplication API
    ID3D11Texture2D*        m_pAcquiredDesktopImage[2] = {nullptr, nullptr};  // Buffer acquired GPU desktop frames
    DXGI_OUTPUT_DESC        m_outputDescriptor;                               // Information about the display frame been captured

    bool       CopyImage(FLM_PIXEL_DATA& pixelData);
    FLM_STATUS CreateD3D11Device();
    void       DoneWithAcquiredFrame(bool nextFrame);
    FLM_STATUS ProcessFailure(ID3D11Device* Device, std::string str, HRESULT hr, HRESULT* ExpectedErrors = nullptr);

protected:
    FLM_Capture_DXGI(); // hide the default constructor
};

#endif