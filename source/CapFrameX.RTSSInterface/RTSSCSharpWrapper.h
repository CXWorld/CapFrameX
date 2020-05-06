#pragma once
#include "RTSSCoreControl.h"
#include <msclr\lock.h>

#pragma managed
using namespace System;
using namespace System::Threading;
using namespace System::Threading::Tasks;
using namespace System::Collections::Generic;
using namespace CapFrameX::Contracts::Overlay;
using namespace System::Runtime::CompilerServices;

public ref class RTSSCSharpWrapper
{
public:
	RTSSCSharpWrapper(Action<Exception^>^ exceptionAction)
	{
		_exceptionAction = exceptionAction;
		_coreControl = new RTSSCoreControl();
	}

	~RTSSCSharpWrapper()
	{
		_coreControl->ReleaseOSD();
		delete _coreControl;
		delete m_lock;
		delete _exceptionAction;
	}

	void ResetOSD()
	{
		try
		{
			{
				msclr::lock l(m_lock);
				_coreControl->UpdateOSD("");
			}
		}
		catch (Exception ^ ex)
		{
			_exceptionAction->Invoke(ex);
		}
	}

	void Refresh()
	{
		try
		{
			{
				msclr::lock l(m_lock);
				_coreControl->Refresh();
			}
		}
		catch (Exception^ ex)
		{
			_exceptionAction->Invoke(ex);
		}
	}

	void ReleaseOSD()
	{
		try
		{
			{
				msclr::lock l(m_lock);
				_coreControl->ReleaseOSD();
			}
		}
		catch (Exception^ ex)
		{
			_exceptionAction->Invoke(ex);
		}
	}

	void SetIsCaptureTimerActive(bool isActive)
	{
		try
		{
			{
				msclr::lock l(m_lock);
				_coreControl->IsCaptureTimerActive = isActive;
			}
		}
		catch (Exception^ ex)
		{
			_exceptionAction->Invoke(ex);
		}
	}

	void SetRunHistory(array<String^>^ runHistory)
	{
		try
		{
			{
				msclr::lock l(m_lock);
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
		}
		catch (Exception^ ex)
		{
			_exceptionAction->Invoke(ex);
		}
	}

	void SetRunHistoryOutlierFlags(array<bool>^ outlierFlags)
	{
		try
		{
			{
				msclr::lock l(m_lock);
				_coreControl->RunHistoryOutlierFlags.clear();

				if (outlierFlags == nullptr)
				{
					for (size_t i = 0; i < 3; i++)
					{
						_coreControl->RunHistoryOutlierFlags.push_back(false);
					}
				}
				else
				{
					for (size_t i = 0; i < outlierFlags->Length; i++)
					{
						bool outlierFlag = outlierFlags[i];
						_coreControl->RunHistoryOutlierFlags.push_back(outlierFlag);
					}
				}
			}
		}
		catch (Exception^ ex)
		{
			_exceptionAction->Invoke(ex);
		}
	}

	void SetRunHistoryAggregation(String^ aggregation)
	{
		try
		{
			{
				msclr::lock l(m_lock);
				_coreControl->RunHistoryAggregation = aggregation;
			}
		}
		catch (Exception^ ex)
		{
			_exceptionAction->Invoke(ex);
		}
	}

	void SetOverlayEntries(array<IOverlayEntry^>^ overlayEntries)
	{
		if (overlayEntries == nullptr || overlayEntries->Length == 0)
			return;

		try
		{
			{
				msclr::lock l(m_lock);
				_coreControl->OverlayEntries.clear();

				for (size_t i = 0; i < overlayEntries->Length; i++)
				{
					IOverlayEntry^ managedEntry = overlayEntries[i];
					OverlayEntry entry;

					// mapping member
					entry.Identifier = managedEntry->Identifier;
					entry.Description = managedEntry->Description;
					entry.ShowOnOverlay = managedEntry->ShowOnOverlay;
					entry.GroupName = managedEntry->GroupName;
					entry.Value = managedEntry->FormattedValue;
					entry.ShowGraph = managedEntry->ShowGraph;
					entry.Color = managedEntry->Color;

					_coreControl->OverlayEntries.push_back(entry);
				}
			}
		}
		catch (Exception^ ex)
		{
			_exceptionAction->Invoke(ex);
		}
	}

	void SetOverlayEntry(IOverlayEntry^ managedEntry)
	{
		if (managedEntry == nullptr || _coreControl->OverlayEntries.size() == 0)
			return;

		try
		{
			{
				msclr::lock l(m_lock);
				for (std::vector<int>::size_type i = 0; i < _coreControl->OverlayEntries.size(); i++)
				{
					if (_coreControl->OverlayEntries[i].Identifier == managedEntry->Identifier)
					{
						// mapping member
						_coreControl->OverlayEntries[i].Description = managedEntry->Description;
						_coreControl->OverlayEntries[i].ShowOnOverlay = managedEntry->ShowOnOverlay;
						_coreControl->OverlayEntries[i].GroupName = managedEntry->GroupName;
						_coreControl->OverlayEntries[i].Value = managedEntry->FormattedValue;
						_coreControl->OverlayEntries[i].ShowGraph = managedEntry->ShowGraph;
						_coreControl->OverlayEntries[i].Color = managedEntry->Color;
						break;
					}
				}
			}
		}
		catch (Exception ^ ex)
		{
			_exceptionAction->Invoke(ex);
		}
	}

	String^ GetApiInfo(UINT processId)
	{
		if (processId == 0)
			return "";

		return gcnew String(_coreControl->GetApiInfo(processId));
	}

private:
	RTSSCoreControl* _coreControl;
	Action<Exception^>^ _exceptionAction;
	Object^ m_lock = gcnew Object();
};