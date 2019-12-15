#pragma once

#pragma managed
using namespace System;
using namespace CapFrameX::Contracts::RTSSInterface;
using namespace System::Collections::Generic;


ref class RTSSCSharpWrapper:IRTSSCSharpWrapper
{
public:
	RTSSCSharpWrapper()
	{
			
	}

	virtual void ShowOverlay()
	{

	}

	virtual void ReleaseOverlay()
	{

	}

	virtual void SetOverlayHeader(IList<String^>^ entries)
	{

	}

	virtual void StartCountDown(int seconds)
	{

	}
};

