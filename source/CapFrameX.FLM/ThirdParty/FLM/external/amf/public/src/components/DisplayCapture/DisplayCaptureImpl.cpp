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

#include "DisplayCaptureImpl.h"
//#include "CaptureStats.h"

#include "../../../include/core/Context.h"
#include "../../../include/core/Trace.h"
#include "../../../common/TraceAdapter.h"
#include "../../../common/AMFFactory.h"
#include "../../../common/TraceAdapter.h"

extern "C"
{
	// Function called from application code to create the component
	AMF_RESULT AMF_CDECL_CALL AMFCreateComponentDisplayCapture(amf::AMFContext* pContext, void* /*reserved*/, amf::AMFComponent** ppComponent)
	{
		*ppComponent = new amf::AMFInterfaceMultiImpl< amf::AMFDisplayCaptureImpl, amf::AMFComponent, amf::AMFContext* >(pContext);
		(*ppComponent)->Acquire();
		return AMF_OK;
	}

#ifdef WANT_CAPTURE_STATS
	CaptureStats gCaptureStats;
#endif
}

#define AMF_FACILITY L"AMFDisplayCaptureImpl"

using namespace amf;

//
//
// AMFDisplayCaptureImpl
//
//

static const AMFEnumDescriptionEntry AMF_ROTATION_ENUM_DESC[] =
{
    {AMF_ROTATION_NONE,       L"None"},
    {AMF_ROTATION_90,          L"90"},
    {AMF_ROTATION_180,          L"180"},
    {AMF_ROTATION_270,          L"270"},
    {AMF_ROTATION_NONE,       0}  // This is end of description mark
};


static const AMFEnumDescriptionEntry AMF_DISPLAYCAPTURE_MODE_ENUM_DESC[] =
{
    {AMF_DISPLAYCAPTURE_MODE_KEEP_FRAMERATE, L"Keep framerate"},
    {AMF_DISPLAYCAPTURE_MODE_WAIT_FOR_PRESENT, L"Wait for present"},
    {AMF_DISPLAYCAPTURE_MODE_GET_CURRENT_SURFACE , L"Get current surface"},
    {0,       0}  // This is end of description mark
};

//-------------------------------------------------------------------------------------------------
AMFDisplayCaptureImpl::AMFDisplayCaptureImpl(AMFContext* pContext)
  : m_pContext(pContext)
  , m_pDesktopDuplication()
  , m_pCurrentTime()
  , m_eof(false)
  , m_lastStartPts(-1)
  , m_frameRate(AMFConstructRate(60,1))
  , m_bCopyOutputSurface(false)
  , m_bDrawDirtyRects(false)
  , m_eRotation(AMF_ROTATION_NONE)
{
	// Add display dvr properties
    AMFPrimitivePropertyInfoMapBegin
        // Assume a max of 4 display adapters
        AMFPropertyInfoInt64(AMF_DISPLAYCAPTURE_MONITOR_INDEX, L"Index of the display monitor", 0, 0, 65535, true),
        AMFPropertyInfoRateEx(AMF_DISPLAYCAPTURE_FRAMERATE, L"Capture frame rate", AMFConstructRate(0, 1), AMFConstructRate(0, 1), AMFConstructRate(200, 1), false),
        AMFPropertyInfoInterface(AMF_DISPLAYCAPTURE_CURRENT_TIME_INTERFACE, L"Interface object for getting current time", NULL, false),
        AMFPropertyInfoInt64(AMF_DISPLAYCAPTURE_FORMAT, L"Capture surface format", AMF_SURFACE_BGRA, AMF_SURFACE_FIRST, AMF_SURFACE_LAST, true),
        AMFPropertyInfoSize(AMF_DISPLAYCAPTURE_RESOLUTION, L"Scrreen resolution", AMFConstructSize(1920,1080), AMFConstructSize(2, 2), AMFConstructSize(10000, 10000), true),
        AMFPropertyInfoBool(AMF_DISPLAYCAPTURE_DUPLICATEOUTPUT, L"Copy output surface", false, true),
        AMFPropertyInfoRect(AMF_DISPLAYCAPTURE_DESKTOP_RECT, L"Desktop rect", 0, 0, 0, 0, true),
        AMFPropertyInfoBool(AMF_DISPLAYCAPTURE_DRAW_DIRTY_RECTS, L"Draw dirty rectangles", false, true),
        AMFPropertyInfoBool(AMF_DISPLAYCAPTURE_ENABLE_DIRTY_RECTS, L"Enable dirty rectangles", false, true),
        AMFPropertyInfoEnum(AMF_DISPLAYCAPTURE_ROTATION, L"Rotation", AMF_ROTATION_NONE, AMF_ROTATION_ENUM_DESC, true),
        AMFPropertyInfoEnum(AMF_DISPLAYCAPTURE_MODE, L"Capture Mode", AMF_DISPLAYCAPTURE_MODE_KEEP_FRAMERATE, AMF_DISPLAYCAPTURE_MODE_ENUM_DESC, true),
        AMFPrimitivePropertyInfoMapEnd
}

//-------------------------------------------------------------------------------------------------
AMFDisplayCaptureImpl::~AMFDisplayCaptureImpl()
{
	m_pCurrentTime.Release();
	//
	Terminate();
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT AMF_STD_CALL  AMFDisplayCaptureImpl::Init(AMF_SURFACE_FORMAT /*format*/, amf_int32 /*width*/, amf_int32 /*height*/)
{
	AMF_RESULT res = AMF_OK;
    AMFLock lock(&m_sync);
    // clean up any information we might previously have
	res = Terminate();
	AMF_RETURN_IF_FAILED(res, L"Terminate() failed");

	m_pDesktopDuplication = new AMFDDAPISourceImpl(m_pContext);
	AMF_RETURN_IF_INVALID_POINTER(m_pDesktopDuplication);

	// Get the display adapter index
	uint32_t displayMonitorIndex = 0;
	GetProperty(AMF_DISPLAYCAPTURE_MONITOR_INDEX, &displayMonitorIndex);
	// Get the frame rate
	GetProperty(AMF_DISPLAYCAPTURE_FRAMERATE, &m_frameRate);
    amf_pts frameDuration = 0;
    if (m_frameRate.num != 0)
    {
        frameDuration = amf_pts(AMF_SECOND * m_frameRate.den / m_frameRate.num);
    }
	// Get the current time interface property if it has been set
	AMFInterfacePtr pTmp;
	GetProperty(AMF_DISPLAYCAPTURE_CURRENT_TIME_INTERFACE, &pTmp);
	m_pCurrentTime = (AMFCurrentTimePtr)pTmp.GetPtr();

    bool bEnableDirtyRects = false;
    GetProperty(AMF_DISPLAYCAPTURE_ENABLE_DIRTY_RECTS, &bEnableDirtyRects);

	res = m_pDesktopDuplication->InitDisplayCapture(displayMonitorIndex, frameDuration, bEnableDirtyRects);
	AMF_RETURN_IF_FAILED(res, L"InitDisplayCapture() failed");

    amf_int64 mode = AMF_DISPLAYCAPTURE_MODE_KEEP_FRAMERATE;
    GetProperty(AMF_DISPLAYCAPTURE_MODE, &mode);
    m_pDesktopDuplication->SetMode((AMF_DISPLAYCAPTURE_MODE_ENUM)mode);

    AMFSize resolution = m_pDesktopDuplication->GetResolution();
    SetProperty(AMF_DISPLAYCAPTURE_RESOLUTION, resolution);

    AMFRect desktopRect = m_pDesktopDuplication->GetDesktopRect();
    SetProperty(AMF_DISPLAYCAPTURE_DESKTOP_RECT, desktopRect);

#ifdef WANT_CAPTURE_STATS
	gCaptureStats.Init("./displaycapture-queryoutput-log.txt", "Display Capture Statistics");
#endif

    GetProperty(AMF_DISPLAYCAPTURE_DUPLICATEOUTPUT, &m_bCopyOutputSurface);

    GetProperty(AMF_DISPLAYCAPTURE_DRAW_DIRTY_RECTS, &m_bDrawDirtyRects);
    if (m_bDrawDirtyRects)
    {
        InitDrawDirtyRects();
    }

    AMF_ROTATION_ENUM   rotation = m_pDesktopDuplication->GetRotation();
    m_eRotation = rotation;
    SetProperty(AMF_DISPLAYCAPTURE_ROTATION, rotation);

	return res;
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT AMF_STD_CALL  AMFDisplayCaptureImpl::ReInit(amf_int32 width, amf_int32 height)
{
	AMFLock lock(&m_sync);
	Terminate();
#ifdef WANT_CAPTURE_STATS
	gCaptureStats.Reinit();
#endif
	return Init(AMF_SURFACE_UNKNOWN, width, height);
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT AMF_STD_CALL  AMFDisplayCaptureImpl::Terminate()
{
	AMFLock lock(&m_sync);

	AMF_RESULT res = AMF_OK;
	if (m_pDesktopDuplication)
	{
		res = m_pDesktopDuplication->TerminateDisplayCapture();
		AMF_RETURN_IF_FAILED(res, L"TerminateDisplayCapture() failed");

		m_pDesktopDuplication.Release();
	}
    TerminateDrawDirtyRects();

#ifdef WANT_CAPTURE_STATS
	gCaptureStats.Terminate();
#endif
	return res;
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT AMF_STD_CALL  AMFDisplayCaptureImpl::Drain()
{
	AMFLock lock(&m_sync);
	SetEOF(true);
	return AMF_OK;
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT AMF_STD_CALL  AMFDisplayCaptureImpl::Flush()
{
	AMFLock lock(&m_sync);

	return AMF_OK;
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT AMF_STD_CALL  AMFDisplayCaptureImpl::SubmitInput(AMFData* /*pData*/)
{
	AMFLock lock(&m_sync);

	return AMF_FAIL;
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT AMF_STD_CALL  AMFDisplayCaptureImpl::QueryOutput(AMFData** ppData)
{
	AMFLock lock(&m_sync);

	AMF_RETURN_IF_FALSE(m_pDesktopDuplication != NULL, AMF_FAIL, L"GetOutput() - m_pDesktopDuplication == NULL");

	// Check to see if we are done
	if (GetEOF())
	{
		return AMF_EOF;
	}

	AMF_RESULT res = AMF_OK;
	// check some required parameters
	AMF_RETURN_IF_FALSE(ppData != NULL, AMF_INVALID_ARG, L"GetOutput() - ppData == NULL");
	
	// initialize output
	*ppData = NULL;

	amf::AMFSurfacePtr surfPtr;
	res = m_pDesktopDuplication->AcquireSurface(m_bCopyOutputSurface, &surfPtr);
	if (AMF_REPEAT == res)
	{	// Specal case for repeat
			return res;
	}
	AMF_RETURN_IF_FAILED(res, L"AcquireSurface() failed");

    if (m_bDrawDirtyRects)
    {
        DrawDirtyRects(surfPtr);
    }


	// Update the surface format in case it has changed
	AMF_SURFACE_FORMAT surfFormat = surfPtr->GetFormat();
	res = SetProperty(AMF_DISPLAYCAPTURE_FORMAT, surfFormat);

	amf_pts currentPts = GetCurrentPts();
	// Duration
	if (m_lastStartPts < 0)
	{
		m_lastStartPts = currentPts;
	}
	amf_pts duration = currentPts - m_lastStartPts;
	surfPtr->SetDuration(duration);
	m_lastStartPts = currentPts;

    AMFSize resolution = AMFConstructSize(surfPtr->GetPlaneAt(0)->GetWidth(), surfPtr->GetPlaneAt(0)->GetHeight());
    SetProperty(AMF_DISPLAYCAPTURE_RESOLUTION, resolution);

    AMFRect desktopRect = m_pDesktopDuplication->GetDesktopRect();
    SetProperty(AMF_DISPLAYCAPTURE_DESKTOP_RECT, desktopRect);

	// Pts
	surfPtr->SetPts(currentPts);
	//

    AMF_ROTATION_ENUM   rotation = m_pDesktopDuplication->GetRotation();
    if (m_eRotation != rotation)
    {
        m_eRotation = rotation;
        SetProperty(AMF_DISPLAYCAPTURE_ROTATION, rotation);
}
    surfPtr->SetProperty(AMF_SURFACE_ROTATION, rotation);

	*ppData = surfPtr.Detach();

#ifdef WANT_CAPTURE_STATS
	gCaptureStats.addDuration(duration);
#endif

	return AMF_OK;
}

//-------------------------------------------------------------------------------
AMF_RESULT AMF_STD_CALL AMFDisplayCaptureImpl::GetCaps(AMFCaps** /*ppCaps*/)
{
	AMFLock lock(&m_sync);
	///////////TODO:///////////////////////////////
	return AMF_NOT_IMPLEMENTED;
}

//-------------------------------------------------------------------------------
AMF_RESULT AMF_STD_CALL AMFDisplayCaptureImpl::Optimize(AMFComponentOptimizationCallback* /*pCallback*/)
{
	///////////TODO:///////////////////////////////
	return AMF_NOT_IMPLEMENTED;
}
void AMF_STD_CALL amf::AMFDisplayCaptureImpl::OnPropertyChanged(const wchar_t* pName)
{
	if (std::wcscmp(pName, AMF_DISPLAYCAPTURE_FRAMERATE) == 0
		|| std::wcscmp(pName, AMF_DISPLAYCAPTURE_MONITOR_INDEX) == 0)
	{
		Init(AMF_SURFACE_FORMAT::AMF_SURFACE_UNKNOWN, 0, 0);
	}
    else if (std::wcscmp(pName, AMF_DISPLAYCAPTURE_MODE) == 0)
    {
        if (m_pDesktopDuplication != nullptr)
        {
            amf_int64 mode = AMF_DISPLAYCAPTURE_MODE_KEEP_FRAMERATE;
            GetProperty(AMF_DISPLAYCAPTURE_MODE, &mode);
            m_pDesktopDuplication->SetMode(AMF_DISPLAYCAPTURE_MODE_ENUM(mode));
        }
    }

	return;
}
//-------------------------------------------------------------------------------------------------
amf_pts AMFDisplayCaptureImpl::GetCurrentPts() const
{
	amf_pts result = 0;
	if (m_pCurrentTime)
	{
		result = m_pCurrentTime->Get();
	}
	else
	{
		result = amf_high_precision_clock();
	}
	return result;
}
#if defined( _M_AMD64)
#include "DrawRectsBGRA_64.h"
#else 
#include "DrawRectsBGRA_32.h"
#endif
//-------------------------------------------------------------------------------------------------
static amf::AMF_KERNEL_ID  kernelIDs[AMF_MEMORY_VULKAN + 1];

//-------------------------------------------------------------------------------------------------
AMF_RESULT  AMFDisplayCaptureImpl::InitDrawDirtyRects()
{
    //MM TODO add vulkan and DX12 when capure is avaiable
    AMF_RESULT res = AMF_OK;
    if (kernelIDs[AMF_MEMORY_DX11] == 0)
    {
        AMFPrograms* pPograms = NULL;
        res = g_AMFFactory.GetFactory()->GetPrograms(&pPograms);

        AMF_RETURN_IF_FAILED(pPograms->RegisterKernelBinary1(AMF_MEMORY_DX11, &kernelIDs[AMF_MEMORY_DX11], L"main", "main", sizeof(DrawRectsBGRA), DrawRectsBGRA, NULL));
    }
    res = m_pContext->GetCompute(AMF_MEMORY_DX11, &m_pComputeDevice);
    AMF_RETURN_IF_FAILED(res, L"Failed to get DX11 Compute");

    res = m_pComputeDevice->GetKernel(kernelIDs[AMF_MEMORY_DX11], &m_pDirtyRectsKernel);
    AMF_RETURN_IF_FAILED(res, L"Failed to get DrawRectsBGRA kernel");

    return AMF_OK;
}
//-------------------------------------------------------------------------------------------------
AMF_RESULT  AMFDisplayCaptureImpl::TerminateDrawDirtyRects()
{
    m_pDirtyRectsKernel.Release();
    m_pComputeDevice.Release();
    m_pDirtyRectsBuffer.Release();
    return AMF_OK;
}

//-------------------------------------------------------------------------------------------------
AMF_RESULT  AMFDisplayCaptureImpl::DrawDirtyRects(AMFSurfacePtr& surface)
{
    AMF_RETURN_IF_FALSE(m_pDirtyRectsKernel != nullptr, AMF_NOT_INITIALIZED, L"m_pDirtyRectsKernel  == nullptr");
    // process buffer with rectangles

    AMF_RESULT res = AMF_OK;

    AMFVariant var;
    surface->GetProperty(AMF_DISPLAYCAPTURE_DIRTY_RECTS, &var);
    if (var.type != AMF_VARIANT_INTERFACE || var.pInterface == nullptr)
    {
        return AMF_NOT_FOUND;
    }
    AMFBufferPtr pBuffer(var.pInterface);
    amf_uint count = amf_uint(pBuffer->GetSize() / sizeof(AMFRect));
    if (count == 0)
    {
        return AMF_NOT_FOUND;
    }
    if (m_pDirtyRectsBuffer != nullptr)
    {
        if (m_pDirtyRectsBuffer->GetSize() < count * sizeof(AMFRect))
        {
            m_pDirtyRectsBuffer = nullptr;
        }
    }


    AMFPlane* pPlaneSrc = surface->GetPlane(AMF_PLANE_PACKED);

    // copy texture 
    AMFSurfacePtr surfaceOut;
    res = m_pContext->AllocSurface(AMF_MEMORY_DX11, surface->GetFormat(), pPlaneSrc->GetWidth(), pPlaneSrc->GetHeight(), &surfaceOut);
    AMF_RETURN_IF_FAILED(res, L"AllocSurface() failed");

    if (m_pDirtyRectsBuffer == nullptr)
    {
        res = m_pContext->AllocBufferEx(AMF_MEMORY_DX11, count * sizeof(AMFRect), AMF_BUFFER_USAGE_SHADER_RESOURCE, 0, &m_pDirtyRectsBuffer);
        AMF_RETURN_IF_FAILED(res, L"AllocBufferEx() failed");
    }
    DXGI_FORMAT formatDX11 = DXGI_FORMAT_R32G32B32A32_UINT;
    ((ID3D11Buffer*)(m_pDirtyRectsBuffer->GetNative()))->SetPrivateData(AMFStructuredBufferFormatGUID, sizeof(DXGI_FORMAT), &formatDX11);

    res = m_pComputeDevice->CopyBufferFromHost(pBuffer->GetNative(), pBuffer->GetSize(), m_pDirtyRectsBuffer, 0, false);
    AMF_RETURN_IF_FAILED(res, L"CopyBufferFromHost() failed");


    amf::AMFContext::AMFDX11Locker dxLock(m_pContext);

    AMFPlane* pPlaneDst = surfaceOut->GetPlane(AMF_PLANE_PACKED);
    // submit kernel
    amf_size index = 0;
    m_pDirtyRectsKernel->SetArgPlane(index++, pPlaneSrc, AMF_ARGUMENT_ACCESS_READ);
    m_pDirtyRectsKernel->SetArgBuffer(index++, m_pDirtyRectsBuffer, AMF_ARGUMENT_ACCESS_READ);
    m_pDirtyRectsKernel->SetArgPlane(index++, pPlaneDst, AMF_ARGUMENT_ACCESS_WRITE);
    m_pDirtyRectsKernel->SetArgInt32(index++, count);

    amf_size offset[2] = { 0,0 };
    amf_size size[2] = { (amf_size)(pPlaneDst->GetWidth() + 7) / 8 * 8, (amf_size)(pPlaneDst->GetHeight() + 7) / 8 * 8 };
    amf_size localSize[2] = { 8,8 };


    AMF_RETURN_IF_FAILED(m_pDirtyRectsKernel->Enqueue(2, offset, size, localSize));

    AMF_RETURN_IF_FAILED(m_pComputeDevice->FlushQueue());

    surface->CopyTo(surfaceOut, true);

    surface = surfaceOut;
    return AMF_OK;
}

