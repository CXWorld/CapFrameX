// RTSSHooksInterface.cpp: implementation of the CRTSSHooksInterface class.
//
// created by Unwinder
//////////////////////////////////////////////////////////////////////
#include "StdAfx.h"
#include "RTSSHooksInterface.h"
//////////////////////////////////////////////////////////////////////
CRTSSHooksInterface::CRTSSHooksInterface()
{
}
//////////////////////////////////////////////////////////////////////
CRTSSHooksInterface::~CRTSSHooksInterface()
{
}
//////////////////////////////////////////////////////////////////////
DWORD CRTSSHooksInterface::SetFlags(DWORD dwAND, DWORD dwXOR)
{
	DWORD	dwResult	= 0;

	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		SETFLAGS pSetFlags = (SETFLAGS)GetProcAddress(hRTSSHooks, "SetFlags");

		if (pSetFlags)
			dwResult = pSetFlags(dwAND, dwXOR);

		PostMessage(WM_RTSS_UPDATESETTINGS, 0 , 0);
			//force RTSS to update settings in main window GUI
	}

	return dwResult;
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::PostMessage(UINT Msg, WPARAM wParam, LPARAM lParam)
{
	HWND hWnd = hWnd = ::FindWindow(NULL, "RTSS");
	if (!hWnd)
		hWnd = ::FindWindow(NULL, "RivaTuner Statistics Server");

	if (hWnd)
		::PostMessage(hWnd, Msg, wParam, lParam);
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::ScreenCapture(LPCSTR lpFilename, DWORD dwQuality, DWORD dwThreads, BOOL bCaptureOSD)
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		SCREENCAPTURE pScreenCapture = (SCREENCAPTURE)GetProcAddress(hRTSSHooks, "ScreenCapture");

		if (pScreenCapture)
			pScreenCapture(lpFilename, dwQuality, dwThreads, bCaptureOSD);
	}
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::VideoCaptureEx(LPVIDEO_CAPTURE_PARAM lpParam)
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		VIDEOCAPTUREEX pVideoCaptureEx = (VIDEOCAPTUREEX)GetProcAddress(hRTSSHooks, "VideoCaptureEx");

		if (pVideoCaptureEx)
			pVideoCaptureEx(lpParam);
	}
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::PTTEvent(BOOL bPush, DWORD dwTrack)
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		PTTEVENT pPTTEvent = (PTTEVENT)GetProcAddress(hRTSSHooks, "PTTEvent");

		if (pPTTEvent)
			pPTTEvent(bPush, dwTrack);
	}
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::BeingRecord()
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		BEGINRECORD pBeginRecord = (BEGINRECORD)GetProcAddress(hRTSSHooks, "BeginRecord");

		if (pBeginRecord)
			pBeginRecord();
	}
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::EndRecord(LPCSTR lpFilename, BOOL bAppend)
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		ENDRECORD pEndRecord = (ENDRECORD)GetProcAddress(hRTSSHooks, "EndRecord");

		if (pEndRecord)
			pEndRecord(lpFilename, bAppend);
	}
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::LoadProfile(LPCSTR lpProfile)
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		LOADPROFILE pLoadProfile = (LOADPROFILE)GetProcAddress(hRTSSHooks, "LoadProfile");

		if (pLoadProfile)
			pLoadProfile(lpProfile);
	}
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::SaveProfile(LPCSTR lpProfile)
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		SAVEPROFILE pSaveProfile = (SAVEPROFILE)GetProcAddress(hRTSSHooks, "SaveProfile");

		if (pSaveProfile)
			pSaveProfile(lpProfile);
	}
}
/////////////////////////////////////////////////////////////////////////////
BOOL CRTSSHooksInterface::GetProfileProperty(LPCSTR lpPropertyName, LPBYTE lpPropertyData, DWORD dwPropertySize)
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		GETPROFILEPROPERTY pGetProfileProperty = (GETPROFILEPROPERTY)GetProcAddress(hRTSSHooks, "GetProfileProperty");

		if (pGetProfileProperty)
			return pGetProfileProperty(lpPropertyName, lpPropertyData, dwPropertySize);
	}

	return FALSE;
}
/////////////////////////////////////////////////////////////////////////////
BOOL CRTSSHooksInterface::SetProfileProperty(LPCSTR lpPropertyName, LPBYTE lpPropertyData, DWORD dwPropertySize)
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		SETPROFILEPROPERTY pSetProfileProperty = (SETPROFILEPROPERTY)GetProcAddress(hRTSSHooks, "SetProfileProperty");

		if (pSetProfileProperty)
			return pSetProfileProperty(lpPropertyName, lpPropertyData, dwPropertySize);
	}

	return FALSE;
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::DeleteProfile(LPCSTR lpProfile)
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		DELETEPROFILE pDeleteProfile = (DELETEPROFILE)GetProcAddress(hRTSSHooks, "DeleteProfile");

		if (pDeleteProfile)
			pDeleteProfile(lpProfile);
	}
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::ResetProfile(LPCSTR lpProfile)
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		RESETPROFILE pResetProfile = (RESETPROFILE)GetProcAddress(hRTSSHooks, "ResetProfile");

		if (pResetProfile)
			pResetProfile(lpProfile);
	}
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSHooksInterface::UpdateProfiles()
{
	HMODULE hRTSSHooks	= GetModuleHandle("RTSSHooks.dll");

	if (hRTSSHooks)
	{
		UPDATEPROFILES pUpdateProfiles = (UPDATEPROFILES)GetProcAddress(hRTSSHooks, "UpdateProfiles");

		if (pUpdateProfiles)
			pUpdateProfiles();
	}
}
/////////////////////////////////////////////////////////////////////////////
