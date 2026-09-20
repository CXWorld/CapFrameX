// stdafx.h : include file for standard system include files,
//  or project specific include files that are used frequently, but
//      are changed infrequently
//
// MFC is not available for C++/CLI targeting .NET (NetCore); plain Win32
// headers plus the CxString compatibility layer replace the afx includes.

#if !defined(AFX_STDAFX_H__6D63CA46_F2CF_40D3_B925_F1C75DFBFC28__INCLUDED_)
#define AFX_STDAFX_H__6D63CA46_F2CF_40D3_B925_F1C75DFBFC28__INCLUDED_

#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000

#define WIN32_LEAN_AND_MEAN		// Exclude rarely-used stuff from Windows headers

#include <windows.h>
#include <tchar.h>

#include "CxString.h"

#ifdef _DEBUG
#ifndef DEBUG_NEW
#define DEBUG_NEW new
#endif
#endif

#endif // !defined(AFX_STDAFX_H__6D63CA46_F2CF_40D3_B925_F1C75DFBFC28__INCLUDED_)
