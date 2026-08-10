//=============================================================================
// Copyright (c) 2024 Advanced Micro Devices, Inc. All rights reserved.
/// @author AMD Developer Tools Team
/// @file flm_capture_amf.cpp
/// @brief  FLM AMF capture interface
//=============================================================================

#include "flm_capture_amf.h"
#include "flm_utils.h"
#include <intrin.h>

// AMF debug macros, enable as needed
#define AMF_DEBUG_PRINT_STACK()  //printf(__FUNCTION__ "\n");

// ===================== Public Interface =======================

//CRITICAL_SECTION g_criticalSection;

FLM_Capture_AMF::FLM_Capture_AMF(FLM_RUNTIME_OPTIONS* pRuntimeOptions)
{
   m_pRuntimeOptions = pRuntimeOptions;
}

FLM_Capture_AMF::~FLM_Capture_AMF()
{
    Release();
}

FLM_STATUS FLM_Capture_AMF::InitCaptureDevice(unsigned int OutputAdapter, FLM_Timer_AMF* timer)
{
    AMF_DEBUG_PRINT_STACK()

    if (m_pContext.GetPtr() == nullptr)
    {
        if (InitContext(FLM_GPU_VENDOR_TYPE::AMD) == false)
        {
            FlmPrintError("Failed AMF InitContext");
            return FLM_STATUS::FAILED;
        }
    }

    if (m_vendor != FLM_GPU_VENDOR_TYPE::AMD)
    {
        FlmPrintError("AMF requires AMD GPU");
        return FLM_STATUS::FAILED;
    }

    AMF_RESULT res;
    res = g_AMFFactory.GetFactory()->CreateComponent(m_pContext, AMFDisplayCapture, &m_pDisplayCapture);

    if (res != AMF_OK)
    {
        FlmPrintError("CreateComponent AMFDisplayCapture [%d]", res);
        return FLM_STATUS::FAILED;
    }

    res = m_pDisplayCapture->SetProperty(AMF_DISPLAYCAPTURE_MODE, AMF_DISPLAYCAPTURE_MODE_WAIT_FOR_PRESENT);
    if (res != AMF_OK)
    {
        FlmPrintError("SetProperty AMF_DISPLAYCAPTURE_MODE [%d]", res);
        return FLM_STATUS::FAILED;
    }

    res = m_pDisplayCapture->SetProperty(AMF_DISPLAYCAPTURE_MONITOR_INDEX, OutputAdapter);
    if (res != AMF_OK)
    {
        FlmPrintError("SetProperty AMF_DISPLAYCAPTURE_MONITOR_INDEX for OutputAdapter = %d [%d]", OutputAdapter, res);
        return FLM_STATUS::FAILED;
    }

    m_iOutputAdapter = OutputAdapter;

    if (timer != nullptr)
    {
        res = m_pDisplayCapture->SetProperty(AMF_DISPLAYCAPTURE_CURRENT_TIME_INTERFACE, timer->GetCurrentTimer());
        if (res != AMF_OK)
        {
            FlmPrintError("SetProperty AMF_DISPLAYCAPTURE_CURRENT_TIME_INTERFACE [%d]", res);
            return FLM_STATUS::FAILED;
        }
    }

    res = m_pDisplayCapture->Init(amf::AMF_SURFACE_UNKNOWN, 0, 0);
    if (res != AMF_OK)
    {
        FlmPrintError("Init AMF_SURFACE_UNKNOWN [%d]", res);
        return FLM_STATUS::FAILED;
    }

    Sleep(500);  // Sleep a bit to avoid unnecessary error messages...

    res = UpdateFormat();
    if (res != AMF_OK)
    {
        FlmPrintError("UpdateFormat [%d]", res);
        return FLM_STATUS::FAILED;
    }

    res = InitConverters();
    if (res != AMF_OK)
    {
        FlmPrintError("InitConverters [%d]", res);
        return FLM_STATUS::FAILED;
    }

    res = InitSurfaces();
    if (res != AMF_OK)
    {
        FlmPrintError("InitSurfaces [%d]", res);
        return FLM_STATUS::FAILED;
    }

    m_bNeedToRebuildPipeline = false;
    m_bDoCaptureFrames       = false;
    m_bFrameLocked           = false;

    return FLM_STATUS::OK;
}

FLM_STATUS FLM_Capture_AMF::GetFrame()
{
#ifdef USE_FRAME_LOCK_AMF
    if (m_bFrameLocked)
        return FLM_STATUS::OK;
#endif

    if (m_bNeedToRebuildPipeline)
        return FLM_STATUS::OK;

    AMF_DEBUG_PRINT_STACK()

    amf::AMFDataPtr pDisplayCaptureData;
    amf_int64       iiTimeStamp;
    amf_int64       iiFrameIdx;
    AMF_RESULT      res;

    for (;;)  // Get access to the back buffer
    {
        {
            //FLM_Profile_Timer profile_timer("GetFrame");
            res = m_pDisplayCapture->QueryOutput(&pDisplayCaptureData);
        }
        if (res == AMF_OK)
            break;
        else if (res == AMF_REPEAT)
        {
#ifdef CAPFRAMEX_FLM_EMBEDDED
            // Sleep(0) yield-spins a core for the whole inter-frame gap — too expensive for an
            // always-on background service. Sleep(1) delays frame pickup by at most ~1 ms
            // (timeBeginPeriod(1) is active for the session), well below the refresh-quantization
            // noise of the click-to-screen-response metric.
            Sleep(1);
#else
            Sleep(0);  // Sleep(1) should be just as good...
#endif
        }
        else if (res == AMF_FAIL)
        {
            if (g_pUserCallBack)
            {
                g_pUserCallBack(FLM_PROCESS_MESSAGE_TYPE::ERROR_MESSAGE, "CaptureFrame() failed!");
                Sleep(100); // Give AMF a chance to recover before repeating the request.
            }

            continue;
        }
        else
        {
            FlmPrintError("CaptureFrame() caused AMF error [%d]", res);
            return FLM_STATUS::FAILED;
        }
    };

    // Extract the frame flip time and the frame index, update the average fps.
    if (res == AMF_OK)
    {
        iiTimeStamp = pDisplayCaptureData->GetPts();
        res         = pDisplayCaptureData->GetProperty(AMF_DISPLAYCAPTURE_FRAME_INDEX, &iiFrameIdx);
        UpdateAverageFrameTime(iiTimeStamp, iiFrameIdx);  // Also updates m_fMovingAverageOddFramesTimeMS and m_fMovingAverageEvenFramesTimeMS
    }
    else
    {
        return FLM_STATUS::FAILED;
    }

    if ((res == AMF_OK) && m_ppConverters[0])
    {
        amf::AMFSurfacePtr pDisplayCaptureSurface(pDisplayCaptureData);

        // 1. See if we need to rebuild the pipeline
        amf::AMF_SURFACE_FORMAT curFormat = pDisplayCaptureSurface->GetFormat();

        if (m_iBackBufferFormat != (uint32_t)curFormat)
        {
            FlmPrint("\nDisplayCapture: format changed.\n");
            m_bNeedToRebuildPipeline = true;
        }
        else if (m_iBackBufferWidth != pDisplayCaptureSurface->GetPlaneAt(0)->GetWidth())
        {
            FlmPrint("\nDisplayCapture: width changed.\n");
            m_bNeedToRebuildPipeline = true;
        }
        else if (m_iBackBufferHeight != pDisplayCaptureSurface->GetPlaneAt(0)->GetHeight())
        {
            FlmPrint("\nDisplayCapture: height changed.\n");
            m_bNeedToRebuildPipeline = true;
        }

        if (m_bNeedToRebuildPipeline)
        {
            return FLM_STATUS::OK;
        }

#ifdef FLM_DEBUG_CODE
        if (0)  // Debug: Check captured surface
        {
            res = pDisplayCaptureSurface->Convert(amf::AMF_MEMORY_HOST);
            if (res == AMF_OK)
                AMF_SaveImage("AMF_GetFrame_01", pDisplayCaptureSurface->GetPlaneAt(0));
        }
#endif

        // 2. Copy to the staging surface
        // This will allow us to use a converter to access a part of the image.
        res = pDisplayCaptureSurface->SetCrop(m_iCaptureOriginX, m_iCaptureOriginY, m_iCaptureWidth, m_iCaptureHeight);
        if (res != AMF_OK)
        {
            return FLM_STATUS::FAILED;
        }

#ifdef FLM_DEBUG_CODE
        if (0)  // Debug: Check cropped surface plane region
        {
            res = pDisplayCaptureSurface->Convert(amf::AMF_MEMORY_HOST);
            if (res == AMF_OK)
                AMF_SaveImage("AMF_GetFrame_02", pDisplayCaptureSurface->GetPlaneAt(0));
        }
#endif

        // 3. Convert using the 0-th converter
        res = m_ppConverters[0]->SubmitInput(pDisplayCaptureSurface);
        if (res != AMF_OK)
        {
            return FLM_STATUS::FAILED;
        }

        res = m_ppConverters[0]->QueryOutput(&m_ppConverterOutputs[0]);
        if (res != AMF_OK)
        {
            return FLM_STATUS::FAILED;
        }

#ifdef FLM_DEBUG_CODE
        if (0)  // Debug: Check converted surface
        {
            res = m_ppConverterOutputs[0]->Convert(amf::AMF_MEMORY_HOST);
            if (res == AMF_OK)
                AMF_SaveImage("AMF_GetFrame_03", amf::AMFSurfacePtr(m_ppConverterOutputs[0])->GetPlaneAt(0));
        }
#endif

        // 4. Copy timestamp and frame index attributes to the staging surface
        if (m_ppConverterOutputs[0])
        {
            m_ppConverterOutputs[0]->SetPts(iiTimeStamp);

            // This is a hack - using the duration as a container for frame index...
            m_ppConverterOutputs[0]->SetDuration(iiFrameIdx);
        }

#ifdef FLM_DEBUG_CODE
        if (0)  // Debug: Check time stamped surface
        {
            res = m_ppConverterOutputs[0]->Convert(amf::AMF_MEMORY_HOST);
            if (res == AMF_OK)
                AMF_SaveImage("AMF_GetFrame_04", amf::AMFSurfacePtr(m_ppConverterOutputs[0])->GetPlaneAt(0));
        }
#endif

#ifdef USE_FRAME_LOCK_AMF
        m_bFrameLocked = true;
#endif
    }
    else
        return FLM_STATUS::FAILED;

    return FLM_STATUS::CAPTURE_PROCESS_FRAME;
}

FLM_STATUS FLM_Capture_AMF::GetFrameBuffer(FLM_PIXEL_DATA& pixelData)
{
    AMF_DEBUG_PRINT_STACK()

    FLM_STATUS res = FLM_STATUS::FAILED;

    if ((m_bDoCaptureFrames == true) && (m_bNeedToRebuildPipeline == false))
    {
        if (GetFrame() == FLM_STATUS::CAPTURE_PROCESS_FRAME)
        {
            int64_t pTimeStamp;
            int64_t pFrameIdx;

            if (GetConverterOutput(&pTimeStamp, &pFrameIdx))
            {
                // Get the current frame
                amf::AMFPlane* plane       = m_pTargetHostSurface->GetPlaneAt(0);  // This is either m_pHostSurface1 or m_pHostSurface0
                pixelData.height           = plane->GetHeight();
                pixelData.width            = plane->GetWidth();
                pixelData.pitchH           = plane->GetHPitch();
                pixelData.pixelSizeInBytes = plane->GetPixelSizeInBytes();  // expect to be 4 bytes
                pixelData.timestamp        = pTimeStamp;
                pixelData.format           = GetImageFormat();
                pixelData.data             = reinterpret_cast<uint8_t*>(plane->GetNative());  // note: data size is pixelData.pitchH * m_captureHeight

                m_iCaptureHeight = pixelData.height;
                m_iCaptureWidth  = pixelData.width;
                res              = FLM_STATUS::OK;
            }
        }
    }

    return res;
}

FLM_STATUS FLM_Capture_AMF::ReleaseFrameBuffer(FLM_PIXEL_DATA& pixelData)
{
    AMF_DEBUG_PRINT_STACK()
    // No need to release frame
    return FLM_STATUS::OK;
}

unsigned int FLM_Capture_AMF::GetImageFormat()
{
    AMF_DEBUG_PRINT_STACK()

    DXGI_FORMAT format = DXGI_FORMAT_UNKNOWN;
    switch ((AMF_SURFACE_FORMAT)m_iBackBufferFormat)
    {
    case AMF_SURFACE_BGRA:
        format = DXGI_FORMAT_B8G8R8A8_UNORM;
        break;
    }
    return (unsigned int)format;
}

void FLM_Capture_AMF::SaveCaptureSurface(uint32_t file_counter)
{
    AMF_DEBUG_PRINT_STACK()
    std::string filename = m_setting.captureFileName;
    if (file_counter > 0)
    {
        filename.append(FlmFormatStr("_%03d", file_counter));
    }

    AMF_SaveImage(filename.c_str(), m_pHostSurface0->GetPlaneAt(0));
}

int FLM_Capture_AMF::CalculateSAD()
{
    AMF_DEBUG_PRINT_STACK()

    if (m_bHostSurfaceInit == false)
        return 0;

    int iWidth  = m_pHostSurface0->GetPlaneAt(0)->GetWidth();
    int iHeight = m_pHostSurface0->GetPlaneAt(0)->GetHeight();
    int iPitch  = m_pHostSurface0->GetPlaneAt(0)->GetHPitch();

    int iWidth1  = m_pHostSurface1->GetPlaneAt(0)->GetWidth();
    int iHeight1 = m_pHostSurface1->GetPlaneAt(0)->GetHeight();
    int iPitch1  = m_pHostSurface1->GetPlaneAt(0)->GetHPitch();

    if ((iWidth != iWidth1) || (iHeight != iHeight1) || (iPitch != iPitch1))
        return 0;  // This is not a valid case for calculating SAD - the sizes need to be identical

    unsigned char* pData0 = reinterpret_cast<unsigned char*>(m_pHostSurface0->GetPlaneAt(0)->GetNative());
    unsigned char* pData1 = reinterpret_cast<unsigned char*>(m_pHostSurface1->GetPlaneAt(0)->GetNative());

    if ((pData0 == 0) || (pData1 == 0))
        return 0;  // Both surfaces need to be in host memory...

    bool bSkipFilmGrainFiltering = (m_setting.iFilmGrainThreshold == 0) ? true : false;

    if (m_pRuntimeOptions->printLevel == FLM_PRINT_LEVEL::PRINT_DEBUG)
        if (KEY_DOWN(VK_LSHIFT))
            bSkipFilmGrainFiltering = true;

    __int64 iiSAD = 0;

    const __m128i film_grain_thresh128 = _mm_set1_epi8(m_setting.iFilmGrainThreshold); // ignore small deltas - helps filtering out film grain
    const __m128i zero128              = _mm_set1_epi8(0); // == {0}, == _mm_setzero_si128(); 

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
                const __m128i mm0     = _mm_load_si128(pMM0++);
                const __m128i mm1     = _mm_load_si128(pMM1++);

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
                const __m128i mm0     = _mm_load_si128(pMM0++);
                const __m128i mm1     = _mm_load_si128(pMM1++);

                mm_2sad = _mm_sad_epu8(mm0, mm1); // Sum the absolute differences of packed unsigned 8-bit integers, 2 values representing 8 SADs each.

                mm_line_2sad = _mm_add_epi64(mm_line_2sad, mm_2sad);  // Accumulate
            }
        }

        iiSAD += _mm_extract_epi64(mm_line_2sad,0) + _mm_extract_epi64(mm_line_2sad,1);

        pData0 += iPitch;
        pData1 += iPitch;
    }

    iiSAD = iiSAD * 10 / (iHeight * iWidth * 3);  // Average change per pixel, multiplied by 10...

    return (int)iiSAD;
}

bool FLM_Capture_AMF::GetConverterOutput(int64_t* pTimeStamp, int64_t* pFrameIdx)
{
    if (m_bHostSurfaceInit == false)
        return false;

#ifdef USE_FRAME_LOCK_AMF
    if (m_bFrameLocked == false)
        return true;
#endif

    AMF_DEBUG_PRINT_STACK()

    if (m_ppConverterOutputs[0] == 0)
        return false;

    AMF_RESULT res = AMF_FAIL;
    if (m_ppConverterOutputs[0].GetPtr())
    {
        amf_pts timeStamp  = m_ppConverterOutputs[0]->GetPts();
        amf_pts iiFrameIdx = m_ppConverterOutputs[0]->GetDuration();  // This is a hack - using the duration as a container for frame index...

        // Do the cascading downscale using the converters:
        amf::AMFSurfacePtr converterOutput = amf::AMFSurfacePtr(m_ppConverterOutputs[0]);
        for (int i = 1; i < m_iNumConverters; i++)  // Starting from 1! The 0-th converter is used in Pipeline::Capture_GetFrame()
        {
            amf::AMFSurfacePtr converterInput = amf::AMFSurfacePtr(m_ppConverterOutputs[i - 1]);

            res = m_ppConverters[i]->SubmitInput(converterInput);
            if (res == AMF_OK)
                res = m_ppConverters[i]->QueryOutput(&m_ppConverterOutputs[i]);

            if (res == AMF_OK)
                converterOutput = amf::AMFSurfacePtr(m_ppConverterOutputs[i]);

            if (res != AMF_OK)
                return res;

#ifdef FLM_DEBUG_CODE
            if (0)  // Debug: Check input surface
            {
                res = converterOutput->Convert(amf::AMF_MEMORY_HOST);
                if (res == AMF_OK)
                    AMF_SaveImage("GetConverterOutput_00", converterInput->GetPlaneAt(0));
            }
#endif
            converterInput = converterOutput;
        }

        if (converterOutput)
        {
            // Copy to host
            m_bLatestSurfaceIs1  = !m_bLatestSurfaceIs1;  // Toggle target
            m_pTargetHostSurface = m_bLatestSurfaceIs1 ? m_pHostSurface1 : m_pHostSurface0;

            res = converterOutput->CopySurfaceRegion(
                m_pTargetHostSurface, 0, 0, 0, 0, converterOutput->GetPlaneAt(0)->GetWidth(), converterOutput->GetPlaneAt(0)->GetHeight());

#ifdef FLM_DEBUG_CODE
            if (0)  // Debug: Check we have a target surface
                AMF_SaveImage("GetConverterOutput_01", m_pTargetHostSurface->GetPlaneAt(0));
#endif
            // Finally - assign return values.
            if (pTimeStamp)
                *pTimeStamp = (int64_t)timeStamp;

            if (pFrameIdx)
                *pFrameIdx = (int64_t)iiFrameIdx;
        }
        else
        {
            if (g_pUserCallBack)
                g_pUserCallBack(FLM_PROCESS_MESSAGE_TYPE::ERROR_MESSAGE, "!converters choked!");
            res = AMF_FAIL;
        }
    }
#ifdef USE_FRAME_LOCK_AMF
    m_bFrameLocked = false;
#endif

    return (res == AMF_OK);
}

void FLM_Capture_AMF::Release()
{
    AMF_DEBUG_PRINT_STACK()

    if (m_bDoCaptureFrames)
    {
        m_bDoCaptureFrames = false;
        Sleep(100);  // Delay for for capture thread to terminate

        ReleaseSurfaces();
        ReleaseConverters();
        ReleaseDisplay();
    }

    if (m_pContext.GetPtr() != nullptr)
    {
        m_pContext->Terminate();
        m_pContext.Release();  // context is the last
    }
}

bool FLM_Capture_AMF::InitContext(FLM_GPU_VENDOR_TYPE vendor)
{
    AMF_DEBUG_PRINT_STACK()

    AMF_RESULT res = AMF_OK;  // error checking can be added later

    m_vendor = vendor;
    if (vendor == FLM_GPU_VENDOR_TYPE::AMD)
    {
        res = g_AMFFactory.Init();
        if (res != AMF_OK)
        {
            FlmPrintError("AMF Failed to initialize [%d]", res);
            return false;
        }

        // initialize AMF
        res = g_AMFFactory.GetFactory()->CreateContext(&m_pContext);
        if (res != AMF_OK)
        {
            FlmPrintError("AMF Failed to initialize Context [%d]", res);
            return false;
        }

        amf::AMFContext2Ptr context2(m_pContext);

        try
        {
            res = m_pRuntimeOptions->initAMFUsingDX12 ? context2->InitDX12(NULL) : context2->InitDX11(NULL);  // Init the DX11 or DX12 device
            if (res != AMF_OK)
            {
                FlmPrintError("AMF Failed to initialize DX Context [%d]", res);
                return false;
            }
        }
        catch (...)
        {
            FlmPrintError("AMF Failed to initialize DX Context [%d]", res);
            return false;
        }

        return true;
    }
    else
        FlmPrintError("AMF capture codec supports only AMD GPU");

    return false;
}

// ===================== Private Interface  =======================

void FLM_Capture_AMF::ReleaseDisplay()
{
    AMF_DEBUG_PRINT_STACK()

    //amf::AMFLock lock(&m_cs);
    if (m_pDisplayCapture.GetPtr() != nullptr)
    {
        m_pDisplayCapture->Terminate();
        m_pDisplayCapture.Release();
    }
}

void FLM_Capture_AMF::AMF_SaveImage(const char* filename, amf::AMFPlane* plane)
{
    AMF_DEBUG_PRINT_STACK()

    amf_int32 pixelSize = plane->GetPixelSizeInBytes();  // expect to be 4 bytes
    // expected 4 byte per pixel, may need to check plane->GetFormat() for other sizes
    if (pixelSize != 4)
        return;

    FLM_PIXEL_DATA pixelData;
    pixelData.data             = reinterpret_cast<amf_uint8*>(plane->GetNative());
    pixelData.height           = plane->GetHeight();
    pixelData.width            = plane->GetWidth();  // AMF bug? this is smaller then PitchH / pixelSizeInBytes
    pixelData.pitchH           = plane->GetHPitch();
    pixelData.pixelSizeInBytes = pixelSize;
    pixelData.format           = GetImageFormat();

    char bmp_file_name[MAX_PATH];
    sprintf_s(bmp_file_name, "%s.bmp", filename);

    SaveAsBitmap(bmp_file_name, pixelData, true);
}

AMF_RESULT FLM_Capture_AMF::UpdateFormat()
{
    AMF_DEBUG_PRINT_STACK()

    amf::AMFDataPtr pTempCaptureData;

    AMF_RESULT res = AMF_FAIL;

    int numRetries = 10;
    for(;;)
    {
        res = m_pDisplayCapture->QueryOutput(&pTempCaptureData);
        if (res == AMF_OK)
            break;
        else
        if (res == AMF_REPEAT)
            Sleep(100);
        else
        if (res == AMF_FAIL)
        {
            printf("."); // will be appended to the message "Rebuilding pipeline - please wait............."

            if (--numRetries == 0)
            {
                printf("\n");
                break;
            }

            Sleep(1000);
        }
        else
            break; // this never happens
    }

    if (res == AMF_OK)
    {
        amf::AMFSurfacePtr pTempCaptureSurface(pTempCaptureData);

        m_iBackBufferFormat = (uint32_t)pTempCaptureSurface->GetFormat();
        m_iBackBufferWidth  = pTempCaptureSurface->GetPlaneAt(0)->GetWidth();
        m_iBackBufferHeight = pTempCaptureSurface->GetPlaneAt(0)->GetHeight();

        // This is what we are capturing:
        // To avoid capturing all sorts of FPS counters on the right or left side of the screen
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
    }
    else
        FlmPrintError("AMF DisplayCapture->QueryOutput timed out");

    return res;
}

int FLM_Capture_AMF::GetConvertersCount()
{
    AMF_DEBUG_PRINT_STACK()

    int iMinDimension = std::min<int>(m_iCaptureWidth, m_iCaptureHeight);
    int iCount;

    for (iCount = 0; iCount < MAX_CONVERTERS; iCount++)
    {
        if (iMinDimension <= 40)
            break;
        else
            iMinDimension /= 2;
    }

    return (iCount == 0) ? 1 : iCount;
}

AMF_RESULT FLM_Capture_AMF::InitConverters()
{
    AMF_DEBUG_PRINT_STACK()

    AMF_RESULT iRes = AMF_OK;

    m_iNumConverters = GetConvertersCount();

    int iWidth  = m_iCaptureWidth;
    int iHeight = m_iCaptureHeight;

    for (int i = 0; i < m_iNumConverters; i++)
    {
        amf::AMFComponentPtr& pConverter = m_ppConverters[i];  // name aliasing

        iRes = g_AMFFactory.GetFactory()->CreateComponent(m_pContext, AMFVideoConverter, &pConverter);
        if (iRes != AMF_OK)
            break;

        iRes = pConverter->SetProperty(AMF_VIDEO_CONVERTER_MEMORY_TYPE, m_pRuntimeOptions->initAMFUsingDX12 ? amf::AMF_MEMORY_DX12 : amf::AMF_MEMORY_DX11);

        if (iRes != AMF_OK)
            break;

        iRes = pConverter->SetProperty(AMF_VIDEO_CONVERTER_COMPUTE_DEVICE, m_pRuntimeOptions->initAMFUsingDX12 ? amf::AMF_MEMORY_DX12 : amf::AMF_MEMORY_DX11);
        if (iRes != AMF_OK)
            break;

        iRes = pConverter->SetProperty(AMF_VIDEO_CONVERTER_OUTPUT_FORMAT, (AMF_SURFACE_FORMAT)m_iBackBufferFormat);
        if (iRes != AMF_OK)
            break;

        int iInWidth = iWidth;
        iWidth /= m_iDownScale;
        int iInHeight = iHeight;
        iHeight /= m_iDownScale;

        iRes = pConverter->SetProperty(AMF_VIDEO_CONVERTER_OUTPUT_SIZE, ::AMFConstructSize(iWidth, iHeight));  // Output size, reduced x2
        if (iRes != AMF_OK)
            break;

        if (i == (m_iNumConverters - 1))  // Last converter also converts the format to an 8 bit 4:4:4 format if needed:
            if ((m_iBackBufferFormat != (uint32_t)amf::AMF_SURFACE_BGRA) &&
                (m_iBackBufferFormat != (uint32_t)amf::AMF_SURFACE_RGBA) &&
                (m_iBackBufferFormat != (uint32_t)amf::AMF_SURFACE_ARGB))
            {
                iRes = pConverter->SetProperty(AMF_VIDEO_CONVERTER_OUTPUT_FORMAT, amf::AMF_SURFACE_BGRA);
                if (iRes != AMF_OK)
                    break;
            }

        iRes = pConverter->Init((AMF_SURFACE_FORMAT)m_iBackBufferFormat, iInWidth, iInHeight);  // Input sizes
        if (iRes != AMF_OK)
            break;
    }

    return iRes;
}

void FLM_Capture_AMF::ReleaseConverters()
{
    AMF_DEBUG_PRINT_STACK()

    // amf::AMFLock lock(&m_cs);
    for (int i = 0; i < m_iNumConverters; i++)
    {
        amf::AMFComponentPtr& pConverter = m_ppConverters[i];  // name aliasing

        pConverter->Drain();
        pConverter->Terminate();
        pConverter              = NULL;
        m_ppConverterOutputs[i] = 0;
    }

    m_iNumConverters = 0;
}

AMF_RESULT FLM_Capture_AMF::InitSurfaces()
{
    AMF_DEBUG_PRINT_STACK()

    //ReleaseSurfaces();

    // Allocate host surfaces. The format and size needs to be the same as that of the last converter:
    amf::AMF_SURFACE_FORMAT hostFormat;
    AMFSize                 hostSize;

    AMF_RESULT iRes = m_ppConverters[m_iNumConverters - 1]->GetProperty(AMF_VIDEO_CONVERTER_OUTPUT_FORMAT, (int*)&hostFormat);
    if (iRes == AMF_OK)
        iRes = m_ppConverters[m_iNumConverters - 1]->GetProperty(AMF_VIDEO_CONVERTER_OUTPUT_SIZE, &hostSize);

    if (iRes == AMF_OK)
    {
        m_pHostSurface0.Release();
        iRes = m_pContext->AllocSurface(amf::AMF_MEMORY_HOST, hostFormat, hostSize.width, hostSize.height, &m_pHostSurface0);
    }
    if (iRes == AMF_OK)
    {
        m_pHostSurface1.Release();
        iRes               = m_pContext->AllocSurface(amf::AMF_MEMORY_HOST, hostFormat, hostSize.width, hostSize.height, &m_pHostSurface1);
        m_bHostSurfaceInit = true;
    }

    m_bLatestSurfaceIs1 = false;

    return iRes;
}

void FLM_Capture_AMF::ReleaseSurfaces()
{
    AMF_DEBUG_PRINT_STACK()

    m_pHostSurface0.Release();
    m_pHostSurface1.Release();
    m_bHostSurfaceInit = false;
}
