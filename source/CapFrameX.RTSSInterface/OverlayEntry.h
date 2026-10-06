#if _MSC_VER > 1000
#pragma once
#endif // _MSC_VER > 1000

#include "stdafx.h"

enum class OverlayValueDisplayMode
{
  Text = 0,
  Bar = 1,
  TextAndBar = 2
};

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
  OverlayValueDisplayMode ValueDisplayMode = OverlayValueDisplayMode::Text;
  double PercentageValue = 0.0;
  BOOL HasPercentageValue = FALSE;
  CString PercentageBarColor;
};

