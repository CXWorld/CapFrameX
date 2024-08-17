/////////////////////////////////////////////////////////////////////////////
// created by Unwinder - modified by ZeroStrat
/////////////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "RTSSProfileInterface.h"

#include <shlwapi.h>
/////////////////////////////////////////////////////////////////////////////
CRTSSProfileInterface::CRTSSProfileInterface()
{
	m_hRTSSHooksDLL = NULL;

	m_pFnEnumProfiles = NULL;
	m_pFnLoadProfile = NULL;
	m_pFnSaveProfile = NULL;
	m_pFnGetProfileProperty = NULL;
	m_pFnSetProfileProperty = NULL;
	m_pFnDeleteProfile = NULL;
	m_pFnResetProfile = NULL;
	m_pFnUpdateProfiles = NULL;
}
/////////////////////////////////////////////////////////////////////////////
CRTSSProfileInterface::~CRTSSProfileInterface()
{
	Uninit();
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSProfileInterface::Uninit()
{
	if (m_hRTSSHooksDLL)
		FreeLibrary(m_hRTSSHooksDLL);
	m_hRTSSHooksDLL = NULL;

	m_pFnEnumProfiles = NULL;
	m_pFnLoadProfile = NULL;
	m_pFnSaveProfile = NULL;
	m_pFnGetProfileProperty = NULL;
	m_pFnSetProfileProperty = NULL;
	m_pFnDeleteProfile = NULL;
	m_pFnResetProfile = NULL;
	m_pFnUpdateProfiles = NULL;
}
/////////////////////////////////////////////////////////////////////////////
BOOL CRTSSProfileInterface::Init(LPCSTR lpInstallPath)
{
	Uninit();

	char szLibraryPath[MAX_PATH];
	strcpy_s(szLibraryPath, sizeof(szLibraryPath), lpInstallPath);
	PathRemoveFileSpec(szLibraryPath);
	strcat_s(szLibraryPath, sizeof(szLibraryPath), "\\RTSSHooks.dll");

	m_hRTSSHooksDLL = LoadLibrary(szLibraryPath);

	if (m_hRTSSHooksDLL)
	{
		m_pFnEnumProfiles = (PFNENUMPROFILES)GetProcAddress(m_hRTSSHooksDLL, "EnumProfiles");
		m_pFnLoadProfile = (PFNLOADPROFILE)GetProcAddress(m_hRTSSHooksDLL, "LoadProfile");
		m_pFnSaveProfile = (PFNSAVEPROFILE)GetProcAddress(m_hRTSSHooksDLL, "SaveProfile");
		m_pFnGetProfileProperty = (PFNGETPROFILEPROPERTY)GetProcAddress(m_hRTSSHooksDLL, "GetProfileProperty");
		m_pFnSetProfileProperty = (PFNSETPROFILEPROPERTY)GetProcAddress(m_hRTSSHooksDLL, "SetProfileProperty");
		m_pFnDeleteProfile = (PFNDELETEPROFILE)GetProcAddress(m_hRTSSHooksDLL, "DeleteProfile");
		m_pFnResetProfile = (PFNRESETPROFILE)GetProcAddress(m_hRTSSHooksDLL, "ResetProfile");
		m_pFnUpdateProfiles = (PFNUPDATEPROFILES)GetProcAddress(m_hRTSSHooksDLL, "UpdateProfiles");

		if (m_pFnEnumProfiles &&
			m_pFnLoadProfile &&
			m_pFnSaveProfile &&
			m_pFnGetProfileProperty &&
			m_pFnSetProfileProperty &&
			m_pFnDeleteProfile &&
			m_pFnResetProfile &&
			m_pFnUpdateProfiles)
			return TRUE;

		Uninit();
	}

	return FALSE;
}
/////////////////////////////////////////////////////////////////////////////
BOOL CRTSSProfileInterface::IsInitialized()
{
	return (m_hRTSSHooksDLL != NULL);
}
/////////////////////////////////////////////////////////////////////////////
DWORD CRTSSProfileInterface::EnumProfiles(LPSTR lpProfilesList, DWORD dwProfilesListSize)
{
	if (m_pFnEnumProfiles)
		return m_pFnEnumProfiles(lpProfilesList, dwProfilesListSize);

	return 0;
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSProfileInterface::LoadProfile(LPCSTR lpProfile)
{
	if (m_pFnLoadProfile)
		m_pFnLoadProfile(lpProfile);
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSProfileInterface::SaveProfile(LPCSTR lpProfile)
{
	if (m_pFnSaveProfile)
		m_pFnSaveProfile(lpProfile);
}
/////////////////////////////////////////////////////////////////////////////
BOOL CRTSSProfileInterface::GetProfileProperty(LPCSTR lpPropertyName, LPBYTE lpPropertyData, DWORD dwPropertySize)
{
	if (m_pFnGetProfileProperty)
		return m_pFnGetProfileProperty(lpPropertyName, lpPropertyData, dwPropertySize);

	return FALSE;
}
/////////////////////////////////////////////////////////////////////////////
BOOL CRTSSProfileInterface::SetProfileProperty(LPCSTR lpPropertyName, LPBYTE lpPropertyData, DWORD dwPropertySize)
{
	if (m_pFnSetProfileProperty)
		return m_pFnSetProfileProperty(lpPropertyName, lpPropertyData, dwPropertySize);

	return FALSE;
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSProfileInterface::DeleteProfile(LPCSTR lpProfile)
{
	if (m_pFnDeleteProfile)
		m_pFnDeleteProfile(lpProfile);
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSProfileInterface::ResetProfile(LPCSTR lpProfile)
{
	if (m_pFnResetProfile)
		m_pFnResetProfile(lpProfile);
}
/////////////////////////////////////////////////////////////////////////////
void CRTSSProfileInterface::UpdateProfiles()
{
	if (m_pFnUpdateProfiles)
		m_pFnUpdateProfiles();
}
/////////////////////////////////////////////////////////////////////////////


