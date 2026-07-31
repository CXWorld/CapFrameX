//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_capture_dxgi.cpp
/// @brief  FLM DXGI desktop capture interface
//=============================================================================

// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved

#include "FLM_capture_dxgi.h"
#include <intrin.h>

#pragma comment(lib, "d3d11.lib")

#if !defined(SAFE_RELEASE)
#define SAFE_RELEASE(X) \
    if (X)              \
    {                   \
        X->Release();   \
        X = nullptr;    \
    }
#endif

// DXGI debug macros, enable as needed
#define DXGI_DEBUG_PRINT_STACK()                     //printf("%-38s\n",__FUNCTION__)
#define DXGI_DEBUG_PRINT_GetConverterOutput(f, ...)  //printf((f), __VA_ARGS__)
#define DXGI_DEBUG_PRINT_GetFrameBuffer(f, ...)      //printf((f), __VA_ARGS__)
#define DXGI_DEBUG_PRINT_GetFrame(f, ...)            //printf((f), __VA_ARGS__)
#define DXGI_DEBUG_PRINT_DoneWithFrame(f, ...)       //printf((f), __VA_ARGS__)
#define DXGI_DEBUG_PRINT_CalculateSAD(f, ...)        //printf((f), __VA_ARGS__)
#define DXGI_DEBUG_PRINT_CopyImage(f, ...)           //printf((f), __VA_ARGS__)
#define DXGI_DEBUG_PRINT_ProcessFailure(f, ...)      //printf((f), __VA_ARGS__)

// Below are lists of errors expect from Dxgi API calls when a transition event like mode change, PnpStop, PnpStart
// desktop switch, TDR or session disconnect/reconnect. In all these cases we want the application to clean up the threads that process
// the desktop updates and attempt to recreate them.
// If we get an error that is not on the appropriate list then we exit the application

// These are the errors we expect from general DXGI API due to a transition
HRESULT SystemTransitionsExpectedErrors[] = {
    DXGI_ERROR_DEVICE_REMOVED,
    DXGI_ERROR_ACCESS_LOST,
    static_cast<HRESULT>(WAIT_ABANDONED),
    S_OK  // Terminate list with zero valued HRESULT
};

// These are the errors we expect from IDXGIOutput1::DuplicateOutput due to a transition
HRESULT CreateDuplicationExpectedErrors[] = {
    DXGI_ERROR_DEVICE_REMOVED,
    static_cast<HRESULT>(E_ACCESSDENIED),
    DXGI_ERROR_UNSUPPORTED,
    DXGI_ERROR_SESSION_DISCONNECTED,
    S_OK  // Terminate list with zero valued HRESULT
};

// These are the errors we expect from IDXGIOutputDuplication methods due to a transition
HRESULT FrameInfoExpectedErrors[] = {
    DXGI_ERROR_DEVICE_REMOVED,
    DXGI_ERROR_ACCESS_LOST,
    S_OK  // Terminate list with zero valued HRESULT
};

// These are the errors we expect from IDXGIAdapter::EnumOutputs methods due to outputs becoming stale during a transition
HRESULT EnumOutputsExpectedErrors[] = {
    DXGI_ERROR_NOT_FOUND,
    S_OK  // Terminate list with zero valued HRESULT
};

FLM_Capture_DXGI::FLM_Capture_DXGI(FLM_RUNTIME_OPTIONS* pRuntimeOptions)
    : m_pDXGIOutputDuplication(nullptr)
    , m_iImagePitch(0)
{
    m_pRuntimeOptions          = pRuntimeOptions;
    m_pAcquiredDesktopImage[0] = nullptr;
    m_pAcquiredDesktopImage[1] = nullptr;
    m_pDestGPUCopy[0]          = nullptr;
    m_pDestGPUCopy[1]          = nullptr;
    RtlZeroMemory(&m_outputDescriptor, sizeof(m_outputDescriptor));
}

FLM_Capture_DXGI::~FLM_Capture_DXGI()
{
    Release();
}

FLM_STATUS FLM_Capture_DXGI::InitCaptureDevice(unsigned int OutputAdapter, FLM_Timer_AMF* timer = nullptr)
{
    DXGI_DEBUG_PRINT_STACK();

    // DXGI frame share QueryPerformance counters
    // Get timers frequency
    if (QueryPerformanceFrequency((LARGE_INTEGER*)&m_iiFreqCountPerSecond) == false)
    {
        FlmPrintError("Error:DXGI get performance frequency failed");
        return FLM_STATUS::TIMER_INIT_FAILED;
    }

    FLM_STATUS Ret = CreateD3D11Device();
    if (Ret != FLM_STATUS::OK)
    {
        FlmPrintError("Error [%x] creating CreateD3D11Device", Ret);
        return Ret;
    }

    // Get DXGI device
    IDXGIDevice* DxgiDevice = nullptr;
    HRESULT      hr         = m_pD3D11Device->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&DxgiDevice));
    if (FAILED(hr))
    {
        return ProcessFailure(nullptr, "Failed to QueryInterface for DXGI Device", hr);
    }

    // Get DXGI adapter
    IDXGIAdapter* DxgiAdapter = nullptr;
    hr                        = DxgiDevice->GetParent(__uuidof(IDXGIAdapter), reinterpret_cast<void**>(&DxgiAdapter));
    DxgiDevice->Release();
    DxgiDevice = nullptr;
    if (FAILED(hr) || (DxgiAdapter == nullptr))
    {
        return ProcessFailure(m_pD3D11Device, "Failed to get parent DXGI Adapter", hr, SystemTransitionsExpectedErrors);
    }

    // Get output adaptor (Display Monitor) to duplicate
    IDXGIOutput* DxgiOutput = nullptr;
    hr                      = DxgiAdapter->EnumOutputs(OutputAdapter, &DxgiOutput);
    SAFE_RELEASE(DxgiAdapter);
    if (FAILED(hr) || (DxgiOutput == nullptr))
    {
        return ProcessFailure(m_pD3D11Device, "Failed to get specified output in DUPLICATIONMANAGER", hr, EnumOutputsExpectedErrors);
    }

    m_iOutputAdapter = OutputAdapter;
    DxgiOutput->GetDesc(&m_outputDescriptor);

    // Query Interface Output1
    IDXGIOutput1* DxgiOutput1 = nullptr;
    hr                        = DxgiOutput->QueryInterface(__uuidof(DxgiOutput1), reinterpret_cast<void**>(&DxgiOutput1));
    SAFE_RELEASE(DxgiOutput);
    if (FAILED(hr) || (DxgiOutput1 == nullptr))
    {
        return ProcessFailure(nullptr, "Failed to Query Interface Output1 in DUPLICATIONMANAGER", hr);
    }

    // Create desktop duplication
    if (m_pDXGIOutputDuplication != nullptr)
    {
        m_pDXGIOutputDuplication->ReleaseFrame();
        m_pDXGIOutputDuplication->Release();
        m_pDXGIOutputDuplication = nullptr;
    }

    hr = DxgiOutput1->DuplicateOutput(m_pD3D11Device, &m_pDXGIOutputDuplication);
    SAFE_RELEASE(DxgiOutput1);
    if (FAILED(hr) || (m_pDXGIOutputDuplication == nullptr))
    {
        if (hr == DXGI_ERROR_NOT_CURRENTLY_AVAILABLE)
        {
            FlmPrint(
                "There is already the maximum number of applications using the Desktop Duplication API running, please close one of those applications and "
                "then try again,Error");
            return FLM_STATUS::CAPTURE_ERROR_UNEXPECTED;
        }
        return ProcessFailure(m_pD3D11Device, "Failed to get duplicate output in DUPLICATIONMANAGER", hr, CreateDuplicationExpectedErrors);
    }

    DXGI_OUTDUPL_DESC lOutputDuplicationDescriptor;
    m_pDXGIOutputDuplication->GetDesc(&lOutputDuplicationDescriptor);

    // Store Display properties
    m_iBackBufferWidth  = lOutputDuplicationDescriptor.ModeDesc.Width;
    m_iBackBufferHeight = lOutputDuplicationDescriptor.ModeDesc.Height;
    m_iBackBufferFormat = (uint32_t)lOutputDuplicationDescriptor.ModeDesc.Format;

    D3D11_TEXTURE2D_DESC texture2d_descriptor;

    if ((m_setting.fCaptureWidth > 0.0f) && (m_setting.fCaptureWidth <= 1.0f))
        m_iCaptureWidth = int(float(m_iBackBufferWidth) * m_setting.fCaptureWidth);
    else
        m_iCaptureWidth = int(m_setting.fCaptureWidth);

    if ((m_setting.fStartX > 0.0f) && (m_setting.fStartX <= 1.0f))
        m_iCaptureOriginX = (int)(float(m_iBackBufferWidth) * m_setting.fStartX);
    else
        m_iCaptureOriginX = int(m_setting.fStartX);

    if ((m_setting.fCaptureHeight > 0.0f) && (m_setting.fCaptureHeight <= 1.0f))
        m_iCaptureHeight = int(float(m_iBackBufferHeight) * m_setting.fCaptureHeight);
    else
        m_iCaptureHeight = int(m_setting.fCaptureHeight);

    if ((m_setting.fStartY > 0.0f) && (m_setting.fStartY <= 1.0f))
        m_iCaptureOriginY = (int)(float(m_iBackBufferHeight) * m_setting.fStartY);
    else
        m_iCaptureOriginY = (int)(m_setting.fStartY);

    texture2d_descriptor.Width              = m_iCaptureWidth;
    texture2d_descriptor.Height             = m_iCaptureHeight;
    texture2d_descriptor.Format             = lOutputDuplicationDescriptor.ModeDesc.Format;
    texture2d_descriptor.ArraySize          = 1;
    texture2d_descriptor.BindFlags          = 0;
    texture2d_descriptor.MiscFlags          = 0;
    texture2d_descriptor.SampleDesc.Count   = 1;
    texture2d_descriptor.SampleDesc.Quality = 0;
    texture2d_descriptor.MipLevels          = 1;
    texture2d_descriptor.CPUAccessFlags     = D3D11_CPU_ACCESS_READ;
    texture2d_descriptor.Usage              = D3D11_USAGE_STAGING;

    for (int i = 0; i < 2; i++)
    {
        hr = m_pD3D11Device->CreateTexture2D(&texture2d_descriptor, NULL, &m_pDestGPUCopy[i]);

        if (FAILED(hr))
        {
            ProcessFailure(nullptr, "Creating a cpu accessible texture failed.", hr);
            return FLM_STATUS::CAPTURE_ERROR_UNEXPECTED;
        }

        if (m_pDestGPUCopy[i] == nullptr)
        {
            ProcessFailure(nullptr, "Creating a cpu accessible texture failed.", hr);
            return FLM_STATUS::CAPTURE_ERROR_UNEXPECTED;
        }
    }

    Sleep(50);

    m_bNeedToRebuildPipeline = false;
    m_bDoCaptureFrames       = false;

    return FLM_STATUS::OK;
}

FLM_STATUS FLM_Capture_DXGI::GetFrame()
{
    // Do not get next frame until last frame resource has been copied
#ifdef USE_FRAME_LOCK_DXGI
    if (m_bFrameLocked)
    {
        //DXGI_DEBUG_PRINT_GetFrame("[Locked]");
        return FLM_STATUS::OK;
    }
#endif

    // DoneWithFrame was not called
    if (m_iGetFrameInstance)
        DoneWithAcquiredFrame(false);

    if (m_pDesktopResource)
    {
        m_pDXGIOutputDuplication->ReleaseFrame();
        m_pDesktopResource->Release();
        m_pDesktopResource = nullptr;
    }

    // Get new frame
    HRESULT hr = m_pDXGIOutputDuplication->AcquireNextFrame(ACQUIRE_FRAME_CAPTURE_TIMEOUT, &m_frameInfo, &m_pDesktopResource);
    if (hr == DXGI_ERROR_WAIT_TIMEOUT)
    {
        DXGI_DEBUG_PRINT_GetFrame("[GF:TimeOut %d]", m_iGetFrameInstance);
        return FLM_STATUS::CAPTURE_TIMEOUT;
    }
    if (FAILED(hr))
    {
        DXGI_DEBUG_PRINT_GetFrame("[GF:GetError %d]", m_iGetFrameInstance);
        return ProcessFailure(m_pD3D11Device, "Failed to acquire next frame in DUPLICATIONMANAGER", hr, FrameInfoExpectedErrors);
    }

    // This happens when frame capture gets a mouse movement, and no change in frame content
    if (m_frameInfo.AccumulatedFrames == 0 || m_frameInfo.LastPresentTime.QuadPart == 0)
    {
        return FLM_STATUS::CAPTURE_RETRY;
    }

    if (!m_pDesktopResource)
    {
        return FLM_STATUS::CAPTURE_ERROR_EXPECTED;
    }

    // check incremental time stamp
    static int64_t lastTimeStamp = 0;
    int64_t        lapTime       = m_frameInfo.LastPresentTime.QuadPart - lastTimeStamp;

    // No change in frame
    if (lapTime == 0)
        return FLM_STATUS::CAPTURE_RETRY;

    lastTimeStamp = m_frameInfo.LastPresentTime.QuadPart;

    DXGI_DEBUG_PRINT_GetFrame("%-38s frame %d [%I64d]\n", __FUNCTION__, m_iCurrentFrame,lapTime);

    // If still holding old frame, destroy it
    if (m_pAcquiredDesktopImage[m_iCurrentFrame])
    {
        m_pAcquiredDesktopImage[m_iCurrentFrame]->Release();
        m_pAcquiredDesktopImage[m_iCurrentFrame] = nullptr;
    }

    hr = m_pDesktopResource->QueryInterface(__uuidof(ID3D11Texture2D), reinterpret_cast<void**>(&m_pAcquiredDesktopImage[m_iCurrentFrame]));

    if (FAILED(hr))
    {
        return ProcessFailure(nullptr, "Failed to QueryInterface for ID3D11Texture2D from acquired IDXGIResource in DUPLICATIONMANAGER", hr);
    }

#ifdef USE_FRAME_LOCK_DXGI
    m_bFrameLocked = true;
#endif

    m_iGetFrameInstance++;
    return FLM_STATUS::CAPTURE_PROCESS_FRAME;
}

FLM_STATUS FLM_Capture_DXGI::ReleaseFrameBuffer(FLM_PIXEL_DATA& pixelData)
{
    DXGI_DEBUG_PRINT_STACK();

    if (pixelData.data)
    {
        delete pixelData.data;
        pixelData.data = NULL;
    }
    return FLM_STATUS::OK;
}

bool FLM_Capture_DXGI::InitContext(FLM_GPU_VENDOR_TYPE vendor)
{
    DXGI_DEBUG_PRINT_STACK();
    return true;
}

void FLM_Capture_DXGI::SaveCaptureSurface(uint32_t file_counter)
{
    DXGI_DEBUG_PRINT_STACK();
    if ((DXGI_FORMAT)GetImageFormat() != DXGI_FORMAT_B8G8R8A8_UNORM)
        return;
    if (m_pixelData[m_iCurrentFrame].data != NULL)
    {
        char bmp_file_name[MAX_PATH];
        if (file_counter == 0)
            sprintf_s(bmp_file_name, "%s.bmp", m_setting.captureFileName.c_str());
        else
        {
            sprintf_s(bmp_file_name, "%s_%03d.bmp",m_setting.captureFileName.c_str(), file_counter);
        }
        SaveAsBitmap(bmp_file_name, m_pixelData[m_iCurrentFrame], true);
    }
}

int FLM_Capture_DXGI::CalculateSAD()
{
    if ((m_pixelData[0].data == NULL) || (m_pixelData[1].data == NULL))
        return 0;

    if ((m_pixelData[0].timestamp == 0) || (m_pixelData[1].timestamp == 0))
        return 0;

    int iWidth  = m_pixelData[0].width;
    int iHeight = m_pixelData[0].height;
    int iPitch  = m_pixelData[0].pitchH;

    if ((iWidth != m_pixelData[1].width) || (iHeight != m_pixelData[1].height) || (iPitch != m_pixelData[1].pitchH))
        return 0;  // This is not a valid case for calculating SAD - the sizes need to be identical

    unsigned char* pData0 = m_pixelData[0].data;
    unsigned char* pData1 = m_pixelData[1].data;

    bool bSkipFilmGrainFiltering = (m_setting.iFilmGrainThreshold == 0) ? true : false;

    if (m_pRuntimeOptions->printLevel == FLM_PRINT_LEVEL::PRINT_DEBUG)
        if (KEY_DOWN(VK_LSHIFT))
            bSkipFilmGrainFiltering = true;

    __int64 iiSAD = 0;

    const __m128i film_grain_thresh128 = _mm_set1_epi8(m_setting.iFilmGrainThreshold); // ignore small deltas - helps filtering out film grain
    const __m128i zero128              = _mm_set1_epi8(0); // == {0}, == _mm_setzero_si128(); 

    iWidth = iWidth / 4;                 // To reduce sensitivity to random noise (film grain), we are going to be averaging 4 adjacent pixel blocks...
    const int iHCount = iWidth * 4 / 16; // each pixel is 4 bytes, and there are 16 bytes in one __m128i register

    for (int y = 0; y < iHeight; y++)
    {
        __m128i* pMM0         = (__m128i*)pData0;
        __m128i* pMM1         = (__m128i*)pData1;
        __m128i  mm_line_2sad = zero128;
        __m128i  mm_2sad;

        if (bSkipFilmGrainFiltering == false)
        {
            for (int i = iHCount - 1; i >= 0; i--)
            {
                const __m128i mm0a = _mm_load_si128(pMM0++);
                const __m128i mm0b = _mm_load_si128(pMM0++);
                const __m128i mm0c = _mm_load_si128(pMM0++);
                const __m128i mm0d = _mm_load_si128(pMM0++);

                const __m128i mm1a = _mm_load_si128(pMM1++);
                const __m128i mm1b = _mm_load_si128(pMM1++);
                const __m128i mm1c = _mm_load_si128(pMM1++);
                const __m128i mm1d = _mm_load_si128(pMM1++);

                const __m128i mm0x = _mm_avg_epu8(mm0a, mm0b);
                const __m128i mm0y = _mm_avg_epu8(mm0c, mm0d);

                const __m128i mm1x = _mm_avg_epu8(mm1a, mm1b);
                const __m128i mm1y = _mm_avg_epu8(mm1c, mm1d);

                const __m128i mm0  = _mm_avg_epu8(mm0x, mm0y);
                const __m128i mm1  = _mm_avg_epu8(mm1x, mm1y);

                //const __m128i mm0 = _mm_load_si128(pMM0++);
                //const __m128i mm1 = _mm_load_si128(pMM1++);

                const __m128i diff            = _mm_sub_epi8(mm0, mm1);
                const __m128i abs_diff        = _mm_abs_epi8(diff);
                const __m128i thresh_abs_diff = _mm_subs_epu8(abs_diff, film_grain_thresh128);
                mm_2sad = _mm_sad_epu8(thresh_abs_diff, zero128); // A hack: sum of absolute differences with zero ==> just a sum...

                mm_line_2sad = _mm_add_epi64(mm_line_2sad, mm_2sad);  // Accumulate
            }
        }
        else
        {
            for (int i = iHCount - 1; i >= 0; i--)
            {
                const __m128i mm0a = _mm_load_si128(pMM0++);
                const __m128i mm0b = _mm_load_si128(pMM0++);
                const __m128i mm0c = _mm_load_si128(pMM0++);
                const __m128i mm0d = _mm_load_si128(pMM0++);

                const __m128i mm1a = _mm_load_si128(pMM1++);
                const __m128i mm1b = _mm_load_si128(pMM1++);
                const __m128i mm1c = _mm_load_si128(pMM1++);
                const __m128i mm1d = _mm_load_si128(pMM1++);

                const __m128i mm0x = _mm_avg_epu8(mm0a, mm0b);
                const __m128i mm0y = _mm_avg_epu8(mm0c, mm0d);

                const __m128i mm1x = _mm_avg_epu8(mm1a, mm1b);
                const __m128i mm1y = _mm_avg_epu8(mm1c, mm1d);

                const __m128i mm0  = _mm_avg_epu8(mm0x, mm0y);
                const __m128i mm1  = _mm_avg_epu8(mm1x, mm1y);

                //const __m128i mm0 = _mm_load_si128(pMM0++);
                //const __m128i mm1 = _mm_load_si128(pMM1++);

                mm_2sad = _mm_sad_epu8(mm0, mm1); // Sum the absolute differences of packed unsigned 8-bit integers, 2 values representing 8 SADs each.

                mm_line_2sad = _mm_add_epi64(mm_line_2sad, mm_2sad);  // Accumulate
            }
        }

        iiSAD += _mm_extract_epi64(mm_line_2sad,0) + _mm_extract_epi64(mm_line_2sad,1);

        pData0 += iPitch;
        pData1 += iPitch;
    }

    iiSAD = iiSAD * 10 / (iHeight * iWidth * 3);  // Average change per pixel, multiplied by 10...

    DXGI_DEBUG_PRINT_CalculateSAD("%-38s frame 0 [%I64d] - frame 1 [%I64d]: iSAD = %d Current Frame %d\n",
                                  __FUNCTION__,
                                  m_pixelData[0].timestamp,
                                  m_pixelData[1].timestamp,
                                  (int)iiSAD,
                                  m_currentFrame);

    return (int)iiSAD;
}

bool FLM_Capture_DXGI::GetConverterOutput(int64_t* pTimeStamp, int64_t* pFrameIdx)
{
    static int64_t frameIDX      = 0;
    static int64_t lastTimeStamp = 0;

    if (pTimeStamp)
    {
        *pTimeStamp = (int64_t)m_frameInfo.LastPresentTime.QuadPart;
        if (lastTimeStamp != *pTimeStamp)
        {
            lastTimeStamp = *pTimeStamp;
            frameIDX++;
        }

        UpdateAverageFrameTime(*pTimeStamp, frameIDX);

        if (pFrameIdx)
            *pFrameIdx = frameIDX;
    }

    DXGI_DEBUG_PRINT_GetConverterOutput("%-38s frame %d [%I64d]\n", __FUNCTION__, m_iCurrentFrame, *pTimeStamp);

    return CopyImage(m_pixelData[m_iCurrentFrame]);
}

// Release resources in dependency order
void FLM_Capture_DXGI::Release()
{
    DXGI_DEBUG_PRINT_STACK();

    m_bDoCaptureFrames = false;

    for (int i = 0; i < 2; i++)
        ReleaseFrameBuffer(m_pixelData[m_iCurrentFrame]);

    for (int i = 0; i < 2; i++)
    {
        SAFE_RELEASE(m_pAcquiredDesktopImage[i]);
        SAFE_RELEASE(m_pDestGPUCopy[i]);
    }

    if (m_pDesktopResource)
    {
        m_pDXGIOutputDuplication->ReleaseFrame();
        SAFE_RELEASE(m_pDesktopResource);
    }

    SAFE_RELEASE(m_pDXGIOutputDuplication);
    SAFE_RELEASE(m_pD3D11Device);
    SAFE_RELEASE(m_pD3D11DeviceContext);
}

// ===================== Private Interface  =======================

bool FLM_Capture_DXGI::CopyImage(FLM_PIXEL_DATA& pixelData)
{
    if ((m_bDoCaptureFrames == false) || (m_pDestGPUCopy[m_iCurrentFrame] == NULL))
    {
        DXGI_DEBUG_PRINT_CopyImage("[copy:null]");
        return false;
    }

#ifdef USE_FRAME_LOCK_DXGI
    if (m_bFrameLocked == false)
    {
        DXGI_DEBUG_PRINT_CopyImage("[copy:unlocked]");
        return false;
    }
#endif

    // Copy by region, can use CopyResource to get the full frame, in this case a small sub frame
    // is used for latency measurements.

    D3D11_TEXTURE2D_DESC destText;
    m_pDestGPUCopy[m_iCurrentFrame]->GetDesc(&destText);

    if (destText.Format != DXGI_FORMAT_B8G8R8A8_UNORM)
    {
        DXGI_DEBUG_PRINT_CopyImage("[copy:format]");
        return false;
    }

    D3D11_BOX SrcBox;
    if ((m_setting.fStartX > 0.0f) && (m_setting.fStartX <= 1.0f))
        SrcBox.left = UINT(m_iBackBufferWidth * m_setting.fStartX);
    else
        SrcBox.left = UINT(m_setting.fStartX);

    if ((m_setting.fStartY > 0.0f) && (m_setting.fStartY <= 1.0f))
        SrcBox.top = UINT(m_iBackBufferHeight * m_setting.fStartY);
    else
        SrcBox.top = UINT(m_setting.fStartY);

    SrcBox.right  = SrcBox.left + destText.Width;
    SrcBox.bottom = SrcBox.top + destText.Height;
    SrcBox.front  = 0;
    SrcBox.back   = 1;

    m_pD3D11DeviceContext->CopySubresourceRegion(m_pDestGPUCopy[m_iCurrentFrame], 0, 0, 0, 0, m_pAcquiredDesktopImage[m_iCurrentFrame], 0, &SrcBox);

    D3D11_MAPPED_SUBRESOURCE resource;
    UINT                     subresource = D3D11CalcSubresource(0, 0, 0);
    m_pD3D11DeviceContext->Map(m_pDestGPUCopy[m_iCurrentFrame], subresource, D3D11_MAP_READ, 0, &resource);

    BYTE* sptr = reinterpret_cast<BYTE*>(resource.pData);

    //Store Image Pitch,not the same as width*BytesPerPixel as GPU capture may use a larger buffer for alignment
    m_iImagePitch = resource.RowPitch;

    rsize_t dsSize = m_iImagePitch * destText.Height;  // m_captureHeight;

    if (pixelData.data == NULL)
        pixelData.data = new BYTE[dsSize];
    else
    {
        if ((pixelData.pitchH * pixelData.height) != dsSize)
        {
            delete pixelData.data;
            pixelData.data = new BYTE[dsSize];
        }
    }

    if (pixelData.data == NULL)
    {
        DXGI_DEBUG_PRINT_CopyImage("[copy:mem]");
        FlmPrintError("unable to allocate memory for pixel data");
        return false;
    }

    pixelData.format           = destText.Format;  // Desktop Duplication Capture format:
    pixelData.pixelSizeInBytes = 4;
    pixelData.height           = destText.Height;
    pixelData.width            = destText.Width;
    pixelData.pitchH           = m_iImagePitch;
    pixelData.timestamp        = (int64_t)m_frameInfo.LastPresentTime.QuadPart;

    memcpy_s(pixelData.data, dsSize, sptr, dsSize);

    m_pD3D11DeviceContext->Unmap(m_pDestGPUCopy[m_iCurrentFrame], subresource);

    DXGI_DEBUG_PRINT_CopyImage("%-38s frame %d [%I64d]\n", __FUNCTION__, m_currentFrame, pixelData.timestamp);

    if (m_bDoCaptureFrames)
        DoneWithAcquiredFrame(true);

    return true;
}

unsigned int FLM_Capture_DXGI::GetImageFormat()
{
    DXGI_DEBUG_PRINT_STACK();
    DXGI_OUTDUPL_DESC output_descriptor;
    m_pDXGIOutputDuplication->GetDesc(&output_descriptor);
    return (unsigned int)output_descriptor.ModeDesc.Format;
}

void FLM_Capture_DXGI::DoneWithAcquiredFrame(bool nextFrame)
{
    DXGI_DEBUG_PRINT_DoneWithFrame("%-38s frame %d\n", __FUNCTION__, m_currentFrame);

    if (m_pAcquiredDesktopImage[m_iCurrentFrame])
    {
        m_pAcquiredDesktopImage[m_iCurrentFrame]->Release();
        m_pAcquiredDesktopImage[m_iCurrentFrame] = nullptr;
    }

    if (m_iGetFrameInstance > 0)
        m_iGetFrameInstance--;

    // move to next frame buffer
    if (nextFrame)
    {
        if (m_iCurrentFrame == 0)
            m_iCurrentFrame = 1;
        else
            m_iCurrentFrame = 0;
    }

#ifdef USE_FRAME_LOCK_DXGI
    m_bFrameLocked = false;
#endif

}

FLM_STATUS FLM_Capture_DXGI::ProcessFailure(ID3D11Device* Device, std::string str, HRESULT hr, HRESULT* ExpectedErrors)
{
    DXGI_DEBUG_PRINT_STACK();
    HRESULT TranslatedHr;

    // On an error check if the DX device is lost
    if (Device)
    {
        HRESULT DeviceRemovedReason = Device->GetDeviceRemovedReason();

        switch (DeviceRemovedReason)
        {
        case DXGI_ERROR_DEVICE_REMOVED:
        case DXGI_ERROR_DEVICE_RESET:
        case static_cast<HRESULT>(E_OUTOFMEMORY):
        {
            // Our device has been stopped due to an external event on the GPU so map them all to
            // device removed and continue processing the condition
            TranslatedHr = DXGI_ERROR_DEVICE_REMOVED;
            break;
        }

        case S_OK:
        {
            // Device is not removed so use original error
            TranslatedHr = hr;
            break;
        }

        default:
        {
            // Device is removed but not a error we want to remap
            TranslatedHr = DeviceRemovedReason;
        }
        }
    }
    else
    {
        TranslatedHr = hr;
    }

    // Check if this error was expected or not
    if (ExpectedErrors)
    {
        HRESULT* CurrentResult = ExpectedErrors;

        while (*CurrentResult != S_OK)
        {
            if (*(CurrentResult++) == TranslatedHr)
            {
                return FLM_STATUS::CAPTURE_ERROR_EXPECTED;
            }
        }
    }

    // Error was not expected send error to main application
    FlmPrintError("Unexpected error[%x]: %s ", TranslatedHr, str.c_str());

    return FLM_STATUS::CAPTURE_ERROR_UNEXPECTED;
}

FLM_STATUS FLM_Capture_DXGI::CreateD3D11Device()
{
    DXGI_DEBUG_PRINT_STACK();
    HRESULT hr = S_OK;

    // Driver types supported
    D3D_DRIVER_TYPE DriverTypes[] = {
        D3D_DRIVER_TYPE_HARDWARE,
        D3D_DRIVER_TYPE_WARP,
        D3D_DRIVER_TYPE_REFERENCE,
    };
    UINT NumDriverTypes = ARRAYSIZE(DriverTypes);

    // Feature levels for the device drivers to use
    D3D_FEATURE_LEVEL FeatureLevels[]  = {D3D_FEATURE_LEVEL_11_0, D3D_FEATURE_LEVEL_10_1, D3D_FEATURE_LEVEL_10_0, D3D_FEATURE_LEVEL_9_1};
    UINT              NumFeatureLevels = ARRAYSIZE(FeatureLevels);

    D3D_FEATURE_LEVEL FeatureLevel;

    // Create device
    for (UINT DriverTypeIndex = 0; DriverTypeIndex < NumDriverTypes; ++DriverTypeIndex)
    {
        hr = D3D11CreateDevice(nullptr,
                               DriverTypes[DriverTypeIndex],
                               nullptr,
                               0,
                               FeatureLevels,
                               NumFeatureLevels,
                               D3D11_SDK_VERSION,
                               &m_pD3D11Device,
                               &FeatureLevel,
                               &m_pD3D11DeviceContext);
        if (SUCCEEDED(hr))
        {
            // one of the selected drivers worked exit loop
            break;
        }
    }

    // Did not find any drivers to use
    if (FAILED(hr))
    {
        return ProcessFailure(nullptr, "Failed to create device", hr);
    }

    return FLM_STATUS::OK;
}
