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

#if !defined(WIN32_LEAN_AND_MEAN)
#define WIN32_LEAN_AND_MEAN
#endif

#include "../../../include/core/Context.h"
#include "../../../common/InterfaceImpl.h"
#include "../../../common/PropertyStorageImpl.h"
#include "public/include/components/DisplayCapture.h"

#define USE_DUPLICATEOUTPUT1

#include <d3d11_1.h>
#ifdef USE_DUPLICATEOUTPUT1
// Requires Windows SDK 10.0.10586
#include <dxgi1_5.h>
#include <dxgi1_6.h>
#else
#include <dxgi1_2.h>
#endif
#include <atlbase.h>


namespace amf
{
	class AMFDDAPISourceImpl : public 
		AMFInterfaceImpl< AMFInterface >,
		public amf::AMFSurfaceObserver
	{
	public:
		AMFDDAPISourceImpl(AMFContext* pContext);
		~AMFDDAPISourceImpl();

		AMF_RESULT                      InitDisplayCapture(uint32_t displayMonitorIndex, amf_pts frameDuration, bool bEnableDirtyRects);
		AMF_RESULT                      TerminateDisplayCapture();

		AMF_RESULT                      AcquireSurface(bool bCopyOutputSurface, amf::AMFSurface **pSurface);

        AMFSize                         GetResolution();

        AMFRect                         GetDesktopRect();
        AMF_ROTATION_ENUM               GetRotation();
        void                            SetMode(AMF_DISPLAYCAPTURE_MODE_ENUM mode);

		// AMFSurfaceObserver interface
		virtual void        AMF_STD_CALL OnSurfaceDataRelease(AMFSurface* pSurface);

	private:
		// Utility methods
        AMF_RESULT                      GetHDRInformation(AMFSurface* pSurface);
        AMF_RESULT                      GetNewDuplicator();

		// When we are done with a texture, we push it onto a free list
		// We must track the AMF surfaces in case Terminate() is called
		// before the surface is released
		amf_list< AMFSurface* >                 m_freeCopySurfaces;

		AMFContextPtr                           m_pContext;
        AMFComputePtr                           m_pCompute;

		mutable AMFCriticalSection              m_sync;

		ATL::CComPtr<IDXGIOutputDuplication>    m_displayDuplicator;

        ATL::CComQIPtr<ID3D11Device1>           m_deviceAMF;
        ATL::CComPtr<ID3D11DeviceContext>       m_contextAMF;
        ATL::CComPtr < ID3D11Texture2D >        m_acquiredTextureAMF;

        volatile bool                           m_bAcquired;
        DXGI_OUTPUT_DESC                        m_outputDescription;

		amf_pts                                 m_frameDuration; // in 100 of nanosec
		amf_pts                                 m_lastPts;

        amf_int64                               m_iFrameCount;

#ifdef USE_DUPLICATEOUTPUT1
		ATL::CComPtr<IDXGIOutput5>              m_dxgiOutput5;
#else
		ATL::CComPtr<IDXGIOutput1>              m_dxgiOutput1;
#endif
        amf_vector<RECT>                        m_DirtyRects;
        amf_vector<DXGI_OUTDUPL_MOVE_RECT>      m_MoveRects;

        bool                                    m_bEnableDirtyRects;
        AMF_DISPLAYCAPTURE_MODE_ENUM            m_eCaptureMode;
    };
	typedef AMFInterfacePtr_T<AMFDDAPISourceImpl>    AMFDDAPISourceImplPtr;
} //namespace amf
