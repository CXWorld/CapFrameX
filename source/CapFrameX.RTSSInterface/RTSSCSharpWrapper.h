#pragma once
#include "RTSSCoreControl.h"

#pragma managed
using namespace System;
//using namespace CapFrameX::Contracts::Overlay;
using namespace System::Collections::Generic;

public ref class RTSSCSharpWrapper
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

	virtual void StartTimer()
	{

	}

	virtual void StopdTimer()
	{

	}

private: 
	RTSSCoreControl* _coreControl;
};

