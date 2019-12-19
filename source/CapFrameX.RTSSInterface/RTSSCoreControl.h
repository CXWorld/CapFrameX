// RTSSSharedMemorySampleDlg.h : header file
//
// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#ifndef _RTSSSHAREDMEMORYSAMPLEDLG_H_INCLUDED_
#define _RTSSSHAREDMEMORYSAMPLEDLG_H_INCLUDED_
/////////////////////////////////////////////////////////////////////////////
#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000
/////////////////////////////////////////////////////////////////////////////
#include "RTSSSharedMemory.h"
#include "RTSSProfileInterface.h"
/////////////////////////////////////////////////////////////////////////////
// define constants / structures and function prototype for NTDLL.dll
// NtQuerySystemInformation function which will be used for CPU usage 
// calculation
/////////////////////////////////////////////////////////////////////////////
class RTSSCoreControl
{
	// Construction
public:
	RTSSCoreControl();	// standard constructor

	// ClassWizard generated virtual function overrides
	//{{AFX_VIRTUAL(CRTSSSharedMemorySampleDlg)
public:
	void						ReleaseOSD();
	void						Refresh();

// Implementation
protected:
	DWORD						EmbedGraph(DWORD dwOffset, FLOAT* lpBuffer, DWORD dwBufferPos, DWORD dwBufferSize, LONG dwWidth, LONG dwHeight, LONG dwMargin, FLOAT fltMin, FLOAT fltMax, DWORD dwFlags);

	DWORD						GetClientsNum();
	DWORD						GetSharedMemoryVersion();
	BOOL						UpdateOSD(LPCSTR lpText);
	void						IncProfileProperty(LPCSTR lpProfile, LPCSTR lpProfileProperty, LONG dwIncrement);
	void						SetProfileProperty(LPCSTR lpProfile, LPCSTR lpProfileProperty, DWORD dwProperty);

	BOOL						m_bMultiLineOutput;
	BOOL						m_bFormatTags;
	BOOL						m_bFillGraphs;
	BOOL						m_bConnected;

	UINT						m_nTimerID;

	CString						m_strInstallPath;

	CRTSSProfileInterface		m_profileInterface;
};
/////////////////////////////////////////////////////////////////////////////
//{{AFX_INSERT_LOCATION}}
// Microsoft Visual C++ will insert additional declarations immediately before the previous line.
/////////////////////////////////////////////////////////////////////////////
#endif
/////////////////////////////////////////////////////////////////////////////
