#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000

#include "stdafx.h"

class OverlayEntry
{
  // Construction
public:
  OverlayEntry();	// standard constructor
  ~OverlayEntry();	// standard destructor

public:
  CString Identifier;
  CString Description;
  BOOL ShowOnOverlay;
  CString GroupName;
  CString Value;
  BOOL ShowGraph;
  CString Color;

  /*
  Identifier List:

  //////////////////////////////////CX///////////////////////////////////////////
  RunHistory
  CaptureServiceStatus
  CaptureTimer

  /////////////////////////////////RTSS//////////////////////////////////////////
  Framerate
  Frametime

  /////////////////////////Hardware Monitoring///////////////////////////////////
  */
};

