#ifndef _RTSSHOOKSTYPES_H_INCLUDED_
#define _RTSSHOOKSTYPES_H_INCLUDED_
//////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////
typedef struct VIDEO_CAPTURE_PARAM
{
	DWORD	dwVersion;
	char	szFilename[MAX_PATH];
	DWORD	dwFramerate;
	DWORD	dwFramesize;
	DWORD	dwFormat;
	DWORD	dwQuality;
	DWORD	dwThreads;
	BOOL	bCaptureOSD;
	DWORD	dwAudioCaptureFlags;
	DWORD	dwVideoCaptureFlagsEx;
	DWORD	dwAudioCaptureFlags2;
	DWORD	dwPrerecordSizeLimit;
	DWORD	dwPrerecordTimeLimit;
} VIDEO_CAPTURE_PARAM, *LPVIDEO_CAPTURE_PARAM;
//////////////////////////////////////////////////////////////////////
#endif
//////////////////////////////////////////////////////////////////////
