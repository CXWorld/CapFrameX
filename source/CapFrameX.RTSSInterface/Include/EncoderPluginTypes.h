#ifndef _ENCODERPLUGINTYPES_H_INCLUDED_
#define _ENCODERPLUGINTYPES_H_INCLUDED_
//////////////////////////////////////////////////////////////////////
#define ENCODER_CAPS_VERSION_V1_0							0x00010000
//////////////////////////////////////////////////////////////////////
#define ENCODER_CAPS_MAX_INPUT_FORMATS						4
//////////////////////////////////////////////////////////////////////
#define ENCODER_CAPS_FLAGS_RESIZE_SUPPORTED					0x00000001
#define ENCODER_CAPS_FLAGS_QUALITY_SUPPORTED				0x00000002
#define ENCODER_CAPS_FLAGS_CONFIGURE_SUPPORTED				0x00000004
#define ENCODER_CAPS_FLAGS_MULTITHREADING_SAFE				0x00000008
//////////////////////////////////////////////////////////////////////
#define ENCODER_INPUT_FORMAT_RGB3							'3BGR'
#define ENCODER_INPUT_FORMAT_RGB4							'4BGR'
#define ENCODER_INPUT_FORMAT_NV12							'21VN'
//////////////////////////////////////////////////////////////////////
typedef struct ENCODER_CAPS
{	
	//v1.0
	DWORD		dwVersion;
	DWORD		dwFlags;
	DWORD		dwInputFormatsNum;
	DWORD		dwInputFormats[ENCODER_CAPS_MAX_INPUT_FORMATS];
	DWORD		dwOutputFormat;
	char		szDesc[MAX_PATH];
} ENCODER_CAPS, *LPENCODER_CAPS;
//////////////////////////////////////////////////////////////////////
#define ENCODER_STAT_VERSION_V1_0							0x00010000
//////////////////////////////////////////////////////////////////////
#define ENCODER_STAT_TYPE_DESC								0x00000000
#define ENCODER_STAT_TYPE_LAST_ERROR						0x00000001
//////////////////////////////////////////////////////////////////////
typedef struct ENCODER_STAT
{	
	//v1.0
	DWORD		dwVersion;
	DWORD		dwType;
	LPBYTE		lpData;
	DWORD		dwSize;
} ENCODER_STAT, *LPENCODER_STAT;
//////////////////////////////////////////////////////////////////////
#define ENCODER_INPUT_VERSION_V1_0							0x00010000
//////////////////////////////////////////////////////////////////////
typedef struct ENCODER_INPUT
{	
	//v1.0
	DWORD		dwVersion;
	DWORD		dwFlags;
	DWORDLONG	qwTimestamp;
	DWORD		dwInputFormat;
	DWORD		dwInputWidth;
	DWORD		dwInputHeight;
	DWORD		dwOutputWidth;
	DWORD		dwOutputHeight;
	DWORD		dwFramerate;
	DWORD		dwQuality;
	LPBYTE		lpInputData;
	DWORD		dwInputSize;
} ENCODER_INPUT, *LPENCODER_INPUT;
//////////////////////////////////////////////////////////////////////
#define ENCODER_OUTPUT_FLAG_KEYFRAME						0x00000001
//////////////////////////////////////////////////////////////////////
#define ENCODER_OUTPUT_VERSION_V1_0							0x00010000
//////////////////////////////////////////////////////////////////////
typedef struct ENCODER_OUTPUT
{	
	//v1.0
	DWORD		dwVersion;
	DWORD		dwFlags;
	DWORDLONG	qwTimestamp;
	LPBYTE		lpOutputData;
	DWORD		dwOutputSize;
	LPVOID		lpContext;
} ENCODER_OUTPUT, *LPENCODER_OUTPUT;
#endif
//////////////////////////////////////////////////////////////////////
