// RTSSProfileInterface.h : implementation file
//
// created by Unwinder
/////////////////////////////////////////////////////////////////////////////
#pragma once
/////////////////////////////////////////////////////////////////////////////
typedef DWORD(*PFNENUMPROFILES)(LPSTR lpProfilesList, DWORD dwProfilesListSize);
typedef void	(*PFNLOADPROFILE)(LPCSTR lpProfile);
typedef void	(*PFNSAVEPROFILE)(LPCSTR lpProfile);
typedef BOOL(*PFNGETPROFILEPROPERTY)(LPCSTR lpPropertyName, LPBYTE lpPropertyData, DWORD dwPropertySize);
typedef BOOL(*PFNSETPROFILEPROPERTY)(LPCSTR lpPropertyName, LPBYTE lpPropertyData, DWORD dwPropertySize);
typedef void	(*PFNDELETEPROFILE)(LPCSTR lpProfile);
typedef void	(*PFNRESETPROFILE)(LPCSTR lpProfile);
typedef void	(*PFNUPDATEPROFILES)();
/////////////////////////////////////////////////////////////////////////////
class CRTSSProfileInterface
{
public:
	BOOL	Init(LPCSTR lpInstallPath);
	//initialize profile interface, full path to RTSS installation folder must be specified
	void	Uninit();
	//uninitialize profile interface
	BOOL	IsInitialized();
	//check if profile interface is initialized

	DWORD	EnumProfiles(LPSTR lpProfilesList, DWORD dwProfilesListSize);
	//enumerate existing profiles and return result in comma-separated list in specified string buffer
	//Return value: required string buffer size. If specified buffer is not large enough to fit all profiles, the list is truncated
	void	LoadProfile(LPCSTR lpProfile);
	//load specified application-specific profile (e.g. "3DMark.exe") or global profile if specified profile is missing
	//Once a profile is loaded, you may access profile properties with GetProfileProperty, modify them with SetProfileProperty then save
	//modified profile
	//Note: use empty string to load global profile
	void	SaveProfile(LPCSTR lpProfile);
	//save previously loaded (and optionally modified) application-speific profile (e.g. "3DMark.exe") or global profile
	//Note: use empty string to save global profile
	//Hint: if you're about to create new application-specific profile, load settings from global profile then save them into 
	//application-specific profile
	BOOL	GetProfileProperty(LPCSTR lpPropertyName, LPBYTE lpPropertyData, DWORD dwPropertySize);
	//get profile property from previously loaded profile

	//The following properties are available:

	//AppDetectionLevel						0..3	- Application detection level
	//Implementation						0..1	- On-Screen Display rendering mode
	//EnableFloatingInjectionAddress		0..1	- Stealth mode
	//EnableDynamicOffsetDetection			0..1	- Custom Direct3D support
	//FramerateLimit						....	- Framerate limit
	//FontWeight							....	- font weight for Raster3D On-Screen Display rendering mode
	//FontFace								string	- font face (e.g. "Tahoma") for Raster3D On-Screen Display rendering mode
	//EnableOSD								0..1	- On-Screen Display support
	//EnableBgnd							0..1	- On-Screen Display shadow
	//EnableStat							....	- Show own statistics
	//BaseColor, BgndColor					....	- On-Screen Display palette
	//PositionX, PositionY					....	- On-Screen Display position
	//ZoomRatio								1..8	- On-Screen Display zoom
	//CoordinateSpace						0..1	- On-Screen Display coordinate space

	BOOL	SetProfileProperty(LPCSTR lpPropertyName, LPBYTE lpPropertyData, DWORD dwPropertySize);
	//set profile property
	void	DeleteProfile(LPCSTR lpProfile);
	//delete specified application-specific profile
	void	ResetProfile(LPCSTR lpProfile);
	//reset profile settings to defaults
	//Note: profile must be reloaded after reset
	void	UpdateProfiles();
	//force all currently loaded 3D applications to reload the profiles

	CRTSSProfileInterface();
	~CRTSSProfileInterface();

protected:
	HMODULE					m_hRTSSHooksDLL;

	PFNENUMPROFILES			m_pFnEnumProfiles;
	PFNLOADPROFILE			m_pFnLoadProfile;
	PFNSAVEPROFILE			m_pFnSaveProfile;
	PFNGETPROFILEPROPERTY	m_pFnGetProfileProperty;
	PFNSETPROFILEPROPERTY	m_pFnSetProfileProperty;
	PFNDELETEPROFILE		m_pFnDeleteProfile;
	PFNRESETPROFILE			m_pFnResetProfile;
	PFNUPDATEPROFILES		m_pFnUpdateProfiles;
};
/////////////////////////////////////////////////////////////////////////////
