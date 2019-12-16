#pragma once
#include "RTSSCoreControl.h"

#pragma managed
using namespace System;
using namespace System::Threading;
using namespace System::Threading::Tasks;
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
		_stopFlag = true;
	/*	_coreControl->Refresh();*/
		Task^ myTask = Task::Factory->StartNew(gcnew Action(this, &RTSSCSharpWrapper::LoopRefresh));
	}

	virtual void ReleaseOverlay()
	{
		_stopFlag = false;
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

	virtual void StopTimer()
	{

	}

private: 
	RTSSCoreControl* _coreControl;
	bool _stopFlag;

	void LoopRefresh()
	{
		while (_stopFlag)
		{
			Thread::Sleep(500);
			_coreControl->Refresh();
		}
		
		_coreControl->ReleaseOSD();
	}
};

