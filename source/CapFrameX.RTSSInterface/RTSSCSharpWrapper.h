#pragma once
#include "RTSSCoreControl.h"

#pragma managed
using namespace System;
using namespace CapFrameX::Contracts::RTSSInterface;
using namespace System::Collections::Generic;

ref class RTSSCSharpWrapper:IRTSSCSharpWrapper
{
public:
	RTSSCSharpWrapper()
	{
		_coreControl = new RTSSCoreControl();
	}

	~RTSSCSharpWrapper()
	{
		ReleaseOverlay();
		delete _coreControl;
	}

	virtual void ShowOverlay()
	{
		_coreControl->Refresh();
	}

	virtual void ReleaseOverlay()
	{
		_coreControl->ReleaseOSD();
	}

	virtual void SetOverlayHeader(IList<String^>^ entries)
	{

	}

	virtual void StartCountDown(int seconds)
	{

	}

private: 
	RTSSCoreControl* _coreControl;
};

