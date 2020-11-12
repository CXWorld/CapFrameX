// RTSSHooksInterface.h: interface for the CRTSSHooksInterface class.
//
// created by Unwinder
//////////////////////////////////////////////////////////////////////
#pragma once
//////////////////////////////////////////////////////////////////////
#include "RTSSHooksTypes.h"
//////////////////////////////////////////////////////////////////////
typedef DWORD	(*SETFLAGS			)(DWORD, DWORD);
typedef void	(*SCREENCAPTURE		)(LPCSTR, DWORD, DWORD, BOOL);
typedef void	(*VIDEOCAPTUREEX	)(LPVIDEO_CAPTURE_PARAM);
typedef void	(*PTTEVENT			)(BOOL, DWORD);
typedef void	(*BEGINRECORD		)();
typedef void	(*ENDRECORD			)(LPCSTR, BOOL);
typedef	void	(*LOADPROFILE		)(LPCSTR);
typedef	void	(*SAVEPROFILE		)(LPCSTR);
typedef	BOOL	(*GETPROFILEPROPERTY)(LPCSTR, LPBYTE, DWORD);
typedef	BOOL	(*SETPROFILEPROPERTY)(LPCSTR, LPBYTE, DWORD);
typedef	void	(*DELETEPROFILE		)(LPCSTR);
typedef	void	(*RESETPROFILE		)(LPCSTR);
typedef	void	(*UPDATEPROFILES	)();	
//////////////////////////////////////////////////////////////////////
#define RTSSHOOKSFLAG_OSD_VISIBLE						1
#define RTSSHOOKSFLAG_LIMITER_DISABLED					4
//////////////////////////////////////////////////////////////////////
#define WM_RTSS_UPDATESETTINGS							WM_APP + 100
#define WM_RTSS_SHOW_PROPERTIES							WM_APP + 102
//////////////////////////////////////////////////////////////////////
class CRTSSHooksInterface
{
public:
	CRTSSHooksInterface();
	virtual ~CRTSSHooksInterface();

	//General functionality

	DWORD	SetFlags(DWORD dwAND, DWORD dwXOR);
		//modify shared RTSS flags with AND and XOR bitmaks and return resulting modified bitmask
    void	PostMessage(UINT Msg, WPARAM wParam, LPARAM lParam);
		//post notification message to RTSS

	//screen capture functionality

	void	ScreenCapture(LPCSTR lpFilename, DWORD dwQuality, DWORD dwThreads, BOOL bCaptureOSD);
		//capture screenshot and save it to file

		//NOTE: screen capture call is asynchonous if at least one 3D application is running, in this case screen capture request
		//is queued and actual screen capture is performed by 3D application when it is presenting the next frame. 
		//Due to this reason asynchronous screen capture request may take infinite time if some application is using 3D API 
		//but passively running and not actually rendering/presenting anything. 

	//video capture functionality

	void	VideoCaptureEx(LPVIDEO_CAPTURE_PARAM lpParam);
		//start/stop video prerecord, capture or encoding
	void	PTTEvent(BOOL bPush, DWORD dwTrack);
		//redirect audio capture PTT events to video capture engine

	//benchmark functionality

	void	BeingRecord();
		//begin benchmark recording or reset benchmark statistics if the benchmarking is already in progress
	void	EndRecord(LPCSTR lpFilename, BOOL bAppend);
		//stop bechmark recording and write recorded benchmark statistics to file
		
		//NOTE: this function must be called twice to completely remove benchmark statistics from OSD, that's by
		//design of benchmark hotkey control functionality:

		//On the 1st press of "End recording" hotkey benchmark statistics stop being recorded, but you still see it in OSD 
		//and can analyze the results (e.g. grab sceenshot for example)
		//On the 2nd press of "End recording" hotkey benchmark statistics are removed from OSD

	//profile control functionality

	void LoadProfile(LPCSTR lpProfile);
		//load application specific or global profile, set lpProfile to empty string to load global profile
	void SaveProfile(LPCSTR lpProfile);
		//save application specific or global profile, set lpProfile to empty string to save global profile
	BOOL GetProfileProperty(LPCSTR lpPropertyName, LPBYTE lpPropertyData, DWORD dwPropertySize);
		//get profile property, return FALSE if specified property name is invalid

		//NOTE: you may call this function without previously loding the profile to validate property name

	BOOL SetProfileProperty(LPCSTR lpPropertyName, LPBYTE lpPropertyData, DWORD dwPropertySize);
		//set profile property, return FALSE if specified property name is invalid or value is out of range 


	void DeleteProfile(LPCSTR lpProfile);
		//delete profile
	void ResetProfile(LPCSTR lpProfile);
		//reset profile settings to defaults
	void UpdateProfiles();	
		//force all currently running 3D applications to reload the profiles
};
//////////////////////////////////////////////////////////////////////
