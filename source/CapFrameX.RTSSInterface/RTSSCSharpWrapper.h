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

  virtual void Refresh()
  {
    _coreControl->Refresh();
  }

  virtual void ReleaseOSD()
  {
    _coreControl->ReleaseOSD();
  }

private:
  RTSSCoreControl* _coreControl;
};

