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
    _coreControl->ReleaseOSD();
    delete _coreControl;
  }

  void Refresh()
  {
    _coreControl->Refresh();
  }

  void ReleaseOSD()
  {
    _coreControl->ReleaseOSD();
  }

  void SetShowCaptureTimer(bool showTimer)
  {
    _coreControl->ShowCaptureTimer = showTimer;
  }

  void SetCaptureTimerValue(Int32 captureTimerValue)
  {
    _coreControl->CaptureTimerValue = (UINT)captureTimerValue;
  }

  void SetCaptureServiceStatus(String^ status)
  {
    _coreControl->CaptureServiceStatus = status;
  }

  void SetShowRunHistory(bool showHistory)
  {
    _coreControl->ShowRunHistory = showHistory;
  }

  void SetRunHistory(array<String^>^ runHistory)
  {
    _coreControl->RunHistory.clear();

    // reset history
    if (runHistory == nullptr)
    {
      _coreControl->RunHistory.push_back("N/A");
      _coreControl->RunHistory.push_back("N/A");
      _coreControl->RunHistory.push_back("N/A");
    }
    else
    {
      for (size_t i = 0; i < runHistory->Length; i++)
      {
        String^ run = runHistory[i];
        _coreControl->RunHistory.push_back(run);
      }
    }
  }

private:
  RTSSCoreControl* _coreControl;
};

