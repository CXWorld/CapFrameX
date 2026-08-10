// MIT license 
// 
// Copyright (c) 2017-2024 Advanced Micro Devices, Inc. All rights reserved.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

#include "DDAPISource.h"
//#include "CaptureStats.h"
#include "public/include/components/DisplayCapture.h"
#include "public/include/components/ColorSpace.h"

#include <VersionHelpers.h>

using namespace amf;

#define AMF_FACILITY L"AMFDDAPISourceImpl"

#define MAX_RECTS 100

namespace
{
#ifdef USE_DUPLICATEOUTPUT1
	const DXGI_FORMAT DesktopFormats[] = { 
//        DXGI_FORMAT_R8G8B8A8_UNORM,
        DXGI_FORMAT_B8G8R8A8_UNORM,
        DXGI_FORMAT_R16G16B16A16_FLOAT,
        DXGI_FORMAT_R10G10B10A2_UNORM,
    };
	const unsigned DesktopFormatsCounts = amf_countof(DesktopFormats);
#endif
}

//-------------------------------------------------------------------------------------------------
AMFDDAPISourceImpl::AMFDDAPISourceImpl(AMFContext* pContext) :
    m_pContext(pContext),
    m_bAcquired(false),
    m_outputDescription{},
    m_frameDuration(0),
    m_lastPts(-1LL),
    m_iFrameCount(0),
    m_bEnableDirtyRects(false),
    m_eCaptureMode(AMF_DISPLAYCAPTURE_MODE_KEEP_FRAMERATE)
{
    m_DirtyRects.resize(MAX_RECTS);
    m_MoveRects.resize(MAX_RECTS);
}

//-------------------------------------------------------------------------------------------------
AMFDDAPISourceImpl::~AMFDDAPISourceImpl()
{
	TerminateDisplayCapture();
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT AMFDDAPISourceImpl::InitDisplayCapture(uint32_t displayMonitorIndex, amf_pts frameDuration, bool bEnableDirtyRects)
{
	// Make sure we are in Windows 8 or higher
	if (!IsWindows8OrGreater())
	{
		return AMF_UNEXPECTED;
	}

	m_frameDuration = frameDuration;
    m_bEnableDirtyRects = bEnableDirtyRects;

    m_pContext->GetCompute(amf::AMF_MEMORY_DX11, &m_pCompute);

	// Set the device
    m_deviceAMF = static_cast<ID3D11Device*>(m_pContext->GetDX11Device());
	AMF_RETURN_IF_FALSE(m_deviceAMF != NULL, AMF_NOT_INITIALIZED, L"Could not get m_device");
    m_deviceAMF->GetImmediateContext(&m_contextAMF);

	// Get DXGI device
	ATL::CComPtr<IDXGIDevice> dxgiDevice;
	HRESULT hr = m_deviceAMF->QueryInterface(__uuidof(IDXGIDevice), reinterpret_cast<void**>(&dxgiDevice));
    ASSERT_RETURN_IF_HR_FAILED(hr, AMF_FAIL, L"Could not find dxgiDevice");

	// Get DXGI adapter
	ATL::CComPtr<IDXGIAdapter> dxgiAdapter;
	hr = dxgiDevice->GetParent(__uuidof(IDXGIAdapter), reinterpret_cast<void**>(&dxgiAdapter));
    ASSERT_RETURN_IF_HR_FAILED(hr, AMF_FAIL, L"Could not find dxgiAdapter");

	// Get output
	std::vector<ATL::CComPtr<IDXGIOutput>> outputs;
    CComPtr<IDXGIOutput> dxgiOutput;
    UINT i = 0;
    while (SUCCEEDED(dxgiAdapter->EnumOutputs(i, &dxgiOutput)))
    {
        outputs.push_back(dxgiOutput);
        dxgiOutput = nullptr;
        ++i;
    }

    AMF_RETURN_IF_FALSE(outputs.empty() == false, AMF_FAIL, L"Could not find dxgiOutput when calling EnumOutputs");

    dxgiOutput = outputs[displayMonitorIndex % outputs.size()];

	// m_OutputDescription will allow access to DesktopCoordinates
	hr = dxgiOutput->GetDesc(&m_outputDescription);
    ASSERT_RETURN_IF_HR_FAILED(hr, AMF_FAIL, L"Could not find dxgiOutput description");

	// Basic checks for simple screen display
	bool supportedRotations = m_outputDescription.Rotation == DXGI_MODE_ROTATION_UNSPECIFIED ||
		                      m_outputDescription.Rotation == DXGI_MODE_ROTATION_IDENTITY;
	if (!supportedRotations)
	{
		AMF_RETURN_IF_FAILED(AMF_FAIL, L"Unsupported display rotation");
	}
    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE);
    
    // Create desktop duplication
#ifdef USE_DUPLICATEOUTPUT1
	// Query the IDXGIOutput5 interface.  IDXGIOutput5 derives from IDXGIOutput and
	// has a few extra methods such as DuplicateOutput1 which we use below
	hr = dxgiOutput->QueryInterface(__uuidof(IDXGIOutput5), reinterpret_cast<void**>(&m_dxgiOutput5));
    ASSERT_RETURN_IF_HR_FAILED(hr, AMF_FAIL, L"could not find dxgiOutput5");

	hr = m_dxgiOutput5->DuplicateOutput1(m_deviceAMF, 0, DesktopFormatsCounts, DesktopFormats, &m_displayDuplicator);
#else
	// Query the IDXGIOutput1 interface.  IDXGIOutput1 derives from IDXGIOutput and
	// has a few extra methods such as DuplicateOutput which we use below
	hr = dxgiOutput->QueryInterface(__uuidof(IDXGIOutput1), reinterpret_cast<void**>(&m_dxgiOutput1));
    ASSERT_RETURN_IF_HR_FAILED(hr, AMF_FAIL, L"Could not find dxgiOutput1");

	hr = m_dxgiOutput1->DuplicateOutput(m_deviceAMF, &m_displayDuplicator);
#endif
    ASSERT_RETURN_IF_HR_FAILED(hr, AMF_FAIL, L"Could not get display duplication object");

    m_lastPts = -1LL;
    m_iFrameCount = 0;
	return AMF_OK;
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT AMFDDAPISourceImpl::TerminateDisplayCapture()
{
	AMFLock lock(&m_sync);
	//

	// Clear any active observers and then the surfaces
	for (amf_list< AMFSurface* >::iterator it = m_freeCopySurfaces.begin(); it != m_freeCopySurfaces.end(); it++)
	{
		(*it)->RemoveObserver(this);
	}
	m_freeCopySurfaces.clear();

	m_displayDuplicator.Release();
#ifdef USE_DUPLICATEOUTPUT1
	m_dxgiOutput5.Release();
#else
	m_dxgiOutput1.Release();
#endif
    m_acquiredTextureAMF.Release();
    m_deviceAMF.Release();

    m_contextAMF.Release();
    m_pCompute.Release();
    m_pContext.Release();

	return AMF_OK;
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT AMFDDAPISourceImpl::AcquireSurface(bool bCopyOutputSurface, amf::AMFSurface** ppSurface)
{
    
    for (int i = 0; i < 1000; i++)
    {
        {
            AMFLock lock(&m_sync);
            if (m_acquiredTextureAMF == nullptr)
            {
                break;
            }
        }
        amf_sleep(1);
    }

	AMFLock lock(&m_sync);

	AMF_RESULT res = AMF_OK;

	// AMF_RETURN_IF_FALSE(m_displayDuplicator != NULL, AMF_FAIL, L"AcquireSurface() has null display duplicator");
	if (!m_displayDuplicator)
	{
		res = GetNewDuplicator();
        if (res != AMF_OK)
        {
            return AMF_REPEAT;
        }
    }

	// Get new frame

    bool bWait = m_frameDuration != 0 && m_eCaptureMode == AMF_DISPLAYCAPTURE_MODE_KEEP_FRAMERATE;

	DXGI_OUTDUPL_FRAME_INFO frameInfo = {};
	ATL::CComPtr<IDXGIResource> desktopResource;
	ATL::CComPtr<ID3D11Texture2D> displayTexture;

// MM AcquireNextFrame() blocks DX11 calls including calls to query encoder. Wait ourselves here
//	UINT kAcquireTimeout = UINT(waitTime / 10000); // to ms
    UINT kAcquireTimeout = 0;

    amf_pts prevLastTime = m_lastPts;

    if (bWait)
    {
        amf_pts startTime = 0;
        if (m_lastPts == -1LL)
        {
            m_lastPts = amf_high_precision_clock() - m_frameDuration;
        }
        //AMFTraceInfo(AMF_FACILITY, L"WaitTime=%5.2f", (m_frameDuration - (amf_high_precision_clock() - m_lastPts)) / 10000.f);

        while (true)
        {
            startTime = amf_high_precision_clock();
            amf_pts passedTime = startTime - m_lastPts;
            amf_pts waitTime = m_frameDuration - passedTime;
            if (waitTime < AMF_MILLISECOND)
            {
                break;
            }
            amf_sleep(1);
        }
        m_lastPts = startTime;
    }
    HRESULT hr = S_OK;

    hr = m_displayDuplicator->AcquireNextFrame(kAcquireTimeout, &frameInfo, &desktopResource);

    AMFBufferPtr pDirtyRectBuffer;

	if (hr == S_OK)
	{
        if (frameInfo.LastPresentTime.QuadPart == 0)
        {
            m_displayDuplicator->ReleaseFrame();
            return AMF_REPEAT;
        }

        if(m_bEnableDirtyRects)
        { 
            UINT moveRectsRequired = 0;
            hr = m_displayDuplicator->GetFrameMoveRects((UINT)m_MoveRects.size(), &m_MoveRects[0], &moveRectsRequired);
            if (hr == DXGI_ERROR_MORE_DATA)
            {
                m_MoveRects.resize(moveRectsRequired);
                hr = m_displayDuplicator->GetFrameMoveRects((UINT)m_MoveRects.size(), &m_MoveRects[0], &moveRectsRequired);
            }
            if (SUCCEEDED(hr))
            {
                UINT dirtyRectsRequired = 0;
                hr = m_displayDuplicator->GetFrameDirtyRects((UINT)m_DirtyRects.size(), &m_DirtyRects[0], &dirtyRectsRequired);
                if (hr == DXGI_ERROR_MORE_DATA)
                {
                    m_DirtyRects.resize(dirtyRectsRequired);
                    hr = m_displayDuplicator->GetFrameDirtyRects((UINT)m_DirtyRects.size(), &m_DirtyRects[0], &dirtyRectsRequired);
                }
                if (SUCCEEDED(hr))
                {
                    // combine move rects and Dirty rects
                    if (m_DirtyRects.size() < moveRectsRequired * 2 + dirtyRectsRequired)
                    {
                        m_DirtyRects.resize(moveRectsRequired * 2 + dirtyRectsRequired);
                    }
                    for (UINT i = 0; i < moveRectsRequired; i++)
                    {
                        DXGI_OUTDUPL_MOVE_RECT &src = m_MoveRects[i];
                        RECT &dst1 = m_DirtyRects[dirtyRectsRequired + i * 2];
                        RECT &dst2 = m_DirtyRects[dirtyRectsRequired + i * 2 + 1];

                        dst1.left = src.SourcePoint.x;
                        dst1.top = src.SourcePoint.y;
                        dst1.right = dst1.left + src.DestinationRect.right - src.DestinationRect.left;
                        dst1.bottom = dst1.top + src.DestinationRect.bottom - src.DestinationRect.top;

                        dst2 = src.DestinationRect;
                    }
                    if (moveRectsRequired * 2 + dirtyRectsRequired > 0)
                    {
                        res = m_pContext->AllocBuffer(AMF_MEMORY_HOST, sizeof(AMFRect) * (moveRectsRequired * 2 + dirtyRectsRequired), &pDirtyRectBuffer);
                        memcpy(pDirtyRectBuffer->GetNative(), &m_DirtyRects[0], pDirtyRectBuffer->GetSize());
                    }
                }
            }
        }

		hr = desktopResource->QueryInterface(__uuidof(ID3D11Texture2D), reinterpret_cast<void **>(&displayTexture));
		AMF_RETURN_IF_FALSE(SUCCEEDED(hr), AMF_FAIL, L"Could not get captured display texture");

        m_bAcquired = true;

        amf_pts now = amf_high_precision_clock();
        if (m_lastPts != -1LL)
        {
//            AMFTraceInfo(AMF_FACILITY, L"Since Last frame = %5.2f", (now - m_lastPts) / 10000.f);
        }
        m_lastPts = now;

        res = m_pContext->CreateSurfaceFromDX11Native(displayTexture, ppSurface, this);
        AMF_RETURN_IF_FAILED(res, L"CreateSurfaceFromDX11Native() failed");

        // for tracking
        m_acquiredTextureAMF = displayTexture;
        m_freeCopySurfaces.push_back(*ppSurface);

        if (bCopyOutputSurface)
        {
            AMFDataPtr pDataCopy;
            res = (*ppSurface)->Duplicate((*ppSurface)->GetMemoryType(), &pDataCopy);
            AMF_RETURN_IF_FAILED(res, L"Duplicate() failed. FrameCount = %lld", m_iFrameCount);

            AMFSurfacePtr pSurfaceCopy(pDataCopy);
            (*ppSurface)->Release();
            *ppSurface = pSurfaceCopy.Detach();
        }

        m_iFrameCount++;

        if (pDirtyRectBuffer != nullptr)
        {
            (*ppSurface)->SetProperty(AMF_DISPLAYCAPTURE_DIRTY_RECTS, (AMFInterface*)pDirtyRectBuffer);
        }

        res = GetHDRInformation(*ppSurface);
//      AMF_RETURN_IF_FAILED(res, L"GetHDRInformation() failed");

        return AMF_OK;
    }
	if (DXGI_ERROR_WAIT_TIMEOUT == hr)
	{
        //AMFTraceInfo(AMF_FACILITY, L"Timeout");
        m_lastPts = prevLastTime;
        return AMF_REPEAT;
    }
	if (DXGI_ERROR_ACCESS_LOST == hr)
	{
		res = GetNewDuplicator();
        if (res != AMF_OK)
        {
            return AMF_REPEAT;
        }
        //
		return AMF_REPEAT;
	}
	else 
	{
        res = GetNewDuplicator();
        // Check for other errors
		AMF_RETURN_IF_FALSE(SUCCEEDED(hr), AMF_FAIL, L"Could not AcquireNextFrame");
        return AMF_REPEAT;
    }

	return AMF_OK;
}
//-------------------------------------------------------------------------------------------------
AMF_RESULT AMFDDAPISourceImpl::GetHDRInformation(amf::AMFSurface* pSurface)
{
    AMF_RETURN_IF_INVALID_POINTER(pSurface, L"No surface to write HDR info into");

#ifdef USE_DUPLICATEOUTPUT1
    if (m_dxgiOutput5 != nullptr)
    {
        CComQIPtr<IDXGIOutput6>  spOutput6(m_dxgiOutput5);
#else
    if (m_dxgiOutput1 != nullptr)
    {
        CComQIPtr<IDXGIOutput6>  spOutput6(m_dxgiOutput1);
#endif
        if (spOutput6 != nullptr)
        {
            // Get HDR information from DX
            DXGI_OUTPUT_DESC1 desc1 = { 0 };
            HRESULT           hRes  = spOutput6->GetDesc1(&desc1);
            ASSERT_RETURN_IF_HR_FAILED(hRes, AMF_FAIL, L"Could not get descriiption information");

            // copy the HDR information into an AMF buffer
            AMFBufferPtr  spHDRBuffer;
            AMF_RESULT  res = m_pContext->AllocBuffer(AMF_MEMORY_HOST, sizeof(AMFHDRMetadata), &spHDRBuffer);
            AMF_RETURN_IF_FAILED(res, L"AllocBuffer() failed");

            AMFHDRMetadata *pMetadata = (AMFHDRMetadata *) spHDRBuffer->GetNative();
            pMetadata->redPrimary[0] = amf_uint16(desc1.RedPrimary[0] * 50000.0f);
            pMetadata->redPrimary[1] = amf_uint16(desc1.RedPrimary[1] * 50000.0f);
            pMetadata->greenPrimary[0] = amf_uint16(desc1.GreenPrimary[0] * 50000.0f);
            pMetadata->greenPrimary[1] = amf_uint16(desc1.GreenPrimary[1] * 50000.0f);
            pMetadata->bluePrimary[0] = amf_uint16(desc1.BluePrimary[0] * 50000.0f);
            pMetadata->bluePrimary[1] = amf_uint16(desc1.BluePrimary[1] * 50000.0f);
            pMetadata->whitePoint[0] = amf_uint16(desc1.WhitePoint[0] * 50000.0f);
            pMetadata->whitePoint[1] = amf_uint16(desc1.WhitePoint[1] * 50000.0f);
            pMetadata->minMasteringLuminance = amf_uint32(desc1.MinLuminance * 10000.0f);
            pMetadata->maxMasteringLuminance = amf_uint32(desc1.MaxLuminance * 10000.0f);
            pMetadata->maxContentLightLevel = amf_uint16(desc1.MaxLuminance);
            pMetadata->maxFrameAverageLightLevel = amf_uint16(desc1.MaxFullFrameLuminance);

            // attach the AMF buffer to the surface, so it's available 
            // for later on if needed
            res = pSurface->SetProperty(AMF_VIDEO_COLOR_HDR_METADATA, spHDRBuffer);
            AMF_RETURN_IF_FAILED(res, L"SetProperty(AMF_VIDEO_COLOR_HDR_METADATA) failed");


            AMF_COLOR_TRANSFER_CHARACTERISTIC_ENUM  colorTransfer = AMF_COLOR_TRANSFER_CHARACTERISTIC_BT709;
            AMF_COLOR_PRIMARIES_ENUM                primaries     = AMF_COLOR_PRIMARIES_UNDEFINED;
            bool                                    bFullRange    = false;

            switch (desc1.ColorSpace)
            {
            case  DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P709:
                colorTransfer = AMF_COLOR_TRANSFER_CHARACTERISTIC_GAMMA22;
                primaries = AMF_COLOR_PRIMARIES_BT709;
                bFullRange = true;
                break;

            case  DXGI_COLOR_SPACE_RGB_FULL_G10_NONE_P709:
                colorTransfer = AMF_COLOR_TRANSFER_CHARACTERISTIC_LINEAR;
                primaries = AMF_COLOR_PRIMARIES_BT709;
                bFullRange = true;
                break;

            case  DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P709:
                colorTransfer = AMF_COLOR_TRANSFER_CHARACTERISTIC_GAMMA22;
                primaries = AMF_COLOR_PRIMARIES_BT709;
                break;

            case  DXGI_COLOR_SPACE_RGB_STUDIO_G22_NONE_P2020:
                colorTransfer = AMF_COLOR_TRANSFER_CHARACTERISTIC_GAMMA22;
                primaries = AMF_COLOR_PRIMARIES_BT2020;
                break;

        //    case  DXGI_COLOR_SPACE_RESERVED:
        //    case  DXGI_COLOR_SPACE_YCBCR_FULL_G22_NONE_P709_X601:
        //    case  DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P601:
        //    case  DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P601:
        //    case  DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P709:
        //    case  DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P709:
        //    case  DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_LEFT_P2020:
        //    case  DXGI_COLOR_SPACE_YCBCR_FULL_G22_LEFT_P2020:
            case  DXGI_COLOR_SPACE_RGB_FULL_G2084_NONE_P2020:
                colorTransfer = AMF_COLOR_TRANSFER_CHARACTERISTIC_SMPTE2084;
                primaries = AMF_COLOR_PRIMARIES_CCCS;
                if (pSurface->GetFormat() == AMF_SURFACE_RGBA_F16)
                {
                    colorTransfer = AMF_COLOR_TRANSFER_CHARACTERISTIC_LINEAR;
                    primaries = AMF_COLOR_PRIMARIES_CCCS;
                }

                bFullRange = true;
                break;

        //    case  DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_LEFT_P2020:
            case  DXGI_COLOR_SPACE_RGB_STUDIO_G2084_NONE_P2020:
                colorTransfer = AMF_COLOR_TRANSFER_CHARACTERISTIC_SMPTE2084;
                primaries = AMF_COLOR_PRIMARIES_BT2020;
                break;

        //    case  DXGI_COLOR_SPACE_YCBCR_STUDIO_G22_TOPLEFT_P2020:
        //    case  DXGI_COLOR_SPACE_YCBCR_STUDIO_G2084_TOPLEFT_P2020:
            case  DXGI_COLOR_SPACE_RGB_FULL_G22_NONE_P2020:
                colorTransfer = AMF_COLOR_TRANSFER_CHARACTERISTIC_GAMMA22;
                primaries = AMF_COLOR_PRIMARIES_BT2020;
                bFullRange = true;
                break;

        //    case  DXGI_COLOR_SPACE_YCBCR_STUDIO_GHLG_TOPLEFT_P2020:
        //    case  DXGI_COLOR_SPACE_YCBCR_FULL_GHLG_TOPLEFT_P2020:
            case  DXGI_COLOR_SPACE_RGB_STUDIO_G24_NONE_P709:
        //        colorTransfer =
                primaries = AMF_COLOR_PRIMARIES_BT709;
                break;

            case  DXGI_COLOR_SPACE_RGB_STUDIO_G24_NONE_P2020:
        //        colorTransfer =
                primaries = AMF_COLOR_PRIMARIES_BT2020;
                break;

        //    case  DXGI_COLOR_SPACE_YCBCR_STUDIO_G24_LEFT_P709:
        //    case  DXGI_COLOR_SPACE_YCBCR_STUDIO_G24_LEFT_P2020:
        //    case  DXGI_COLOR_SPACE_YCBCR_STUDIO_G24_TOPLEFT_P2020:
            }

            if (pSurface->GetFormat() == AMF_SURFACE_RGBA_F16)
            {
                colorTransfer = AMF_COLOR_TRANSFER_CHARACTERISTIC_LINEAR;
            }


            res = pSurface->SetProperty(AMF_VIDEO_COLOR_TRANSFER_CHARACTERISTIC, colorTransfer);
            AMF_RETURN_IF_FAILED(res, L"SetProperty(AMF_VIDEO_COLOR_TRANSFER_CHARACTERISTIC) failed");

            res = pSurface->SetProperty(AMF_VIDEO_COLOR_PRIMARIES, primaries);
            AMF_RETURN_IF_FAILED(res, L"SetProperty(AMF_VIDEO_COLOR_PRIMARIES) failed");

            if (bFullRange)
            {
                res = pSurface->SetProperty(AMF_VIDEO_COLOR_RANGE, AMF_COLOR_RANGE_FULL);
                AMF_RETURN_IF_FAILED(res, L"SetProperty(AMF_VIDEO_COLOR_RANGE) failed");
            }
        }
    }
    
    return AMF_OK;
}
//-------------------------------------------------------------------------------------------------
AMF_RESULT AMFDDAPISourceImpl::GetNewDuplicator()
{
	HRESULT hr = S_OK;

    AMFLock lock(&m_sync);
    m_displayDuplicator.Release();
    m_bAcquired = false;
	// Recreate desktop duplication ptr
#ifdef USE_DUPLICATEOUTPUT1
	hr = m_dxgiOutput5->DuplicateOutput1(m_deviceAMF, 0, DesktopFormatsCounts, DesktopFormats, &m_displayDuplicator);
#else
	hr = m_dxgiOutput1->DuplicateOutput(m_device, &m_displayDuplicator);
#endif
    if (hr == E_ACCESSDENIED)
    {
        AMFTraceWarning(AMF_FACILITY, L"GetNewDuplicator(): DuplicateOutput() failed hr=E_ACCESSDENIED");
        return AMF_FAIL;
    }

    if (hr != S_OK)
    {
        AMFTraceWarning(AMF_FACILITY, L"GetNewDuplicator(): DuplicateOutput() failed hr=0x%08X", hr);
        return AMF_FAIL;
    }
#ifdef USE_DUPLICATEOUTPUT1
    m_dxgiOutput5->GetDesc(&m_outputDescription);
#else
    m_dxgiOutput1->GetDesc(&m_outputDescription);
#endif

//    ASSERT_RETURN_IF_HR_FAILED(hr, AMF_FAIL, L"DuplicateOutput() failed");

	return AMF_OK;
}
//-------------------------------------------------------------------------------------------------
AMFSize     AMFDDAPISourceImpl::GetResolution()
{
    AMFLock lock(&m_sync);
    //
    AMFSize resolution = {1920, 1080};
#ifdef USE_DUPLICATEOUTPUT1
    if(m_dxgiOutput5 != nullptr)
#else
    if (m_dxgiOutput1 != nullptr)
#endif
    {
        resolution.width = m_outputDescription.DesktopCoordinates.right - m_outputDescription.DesktopCoordinates.left;
        resolution.height = m_outputDescription.DesktopCoordinates.bottom - m_outputDescription.DesktopCoordinates.top;
    }
    return resolution;
}
//-------------------------------------------------------------------------------------------------
AMFRect amf::AMFDDAPISourceImpl::GetDesktopRect()
{
    AMFLock lock(&m_sync);
    //
    AMFRect rect = { 0, 0, 0, 0 };
#ifdef USE_DUPLICATEOUTPUT1
    if (m_dxgiOutput5 != nullptr)
#else
    if (m_dxgiOutput1 != nullptr)
#endif
    {
        rect.left = m_outputDescription.DesktopCoordinates.left;
        rect.right = m_outputDescription.DesktopCoordinates.right;
        rect.top = m_outputDescription.DesktopCoordinates.top;
        rect.bottom = m_outputDescription.DesktopCoordinates.bottom;
    }
    return rect;
}
//-------------------------------------------------------------------------------------------------
void AMF_STD_CALL AMFDDAPISourceImpl::OnSurfaceDataRelease(amf::AMFSurface* pSurface)
{
    AMFLock lock(&m_sync);
    for (amf_list< AMFSurface* >::iterator it = m_freeCopySurfaces.begin(); it != m_freeCopySurfaces.end(); it++)
    {
        if (*it == pSurface)
        {
            m_freeCopySurfaces.erase(it);
            break;
        }
    }
    m_acquiredTextureAMF = nullptr;
	if (m_displayDuplicator != nullptr)
	{
		m_displayDuplicator->ReleaseFrame();
	}
	m_bAcquired = false;
}
//-------------------------------------------------------------------------------------------------
AMF_ROTATION_ENUM               AMFDDAPISourceImpl::GetRotation()
{
   AMF_ROTATION_ENUM ret = AMF_ROTATION_NONE;
   switch (m_outputDescription.Rotation)
   {
   case DXGI_MODE_ROTATION_ROTATE90: ret = AMF_ROTATION_90; break;
   case DXGI_MODE_ROTATION_ROTATE180:ret = AMF_ROTATION_180; break;
   case DXGI_MODE_ROTATION_ROTATE270:ret = AMF_ROTATION_270; break;
   default:
       ret = AMF_ROTATION_NONE; break;
   }
   return ret;

}
//-------------------------------------------------------------------------------------------------
void                            AMFDDAPISourceImpl::SetMode(AMF_DISPLAYCAPTURE_MODE_ENUM mode)
{
    m_eCaptureMode = mode;
}
//-------------------------------------------------------------------------------------------------
