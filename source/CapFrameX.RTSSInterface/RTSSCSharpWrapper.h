#pragma once
#include "RTSSCoreControl.h"

#pragma managed
using namespace System;
using namespace System::Threading;
using namespace System::Threading::Tasks;
using namespace System::Collections::Generic;
using namespace CapFrameX::Contracts::Overlay;

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

  void SetOverlayEntries(array<IOverlayEntry^>^ overlayEntries)
  {
    if (overlayEntries == nullptr || overlayEntries->Length == 0)
      return;

    _coreControl->OverlayEntries.clear();

    for (size_t i = 0; i < overlayEntries->Length; i++)
    {
      IOverlayEntry^ managedEntry = overlayEntries[i];
      OverlayEntry entry;

      // mapping member
      entry.Identifier = managedEntry->Identifier;
      entry.ShowOnOverlay = managedEntry->ShowOnOverlay;
      entry.GroupName = managedEntry->GroupName;
      entry.Value = managedEntry->FormattedValue;
      entry.ShowGraph = managedEntry->ShowGraph;
      entry.Color = managedEntry->Color;

      _coreControl->OverlayEntries.push_back(entry);
    }
  }

private:
  RTSSCoreControl* _coreControl;
};

