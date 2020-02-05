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
		catch (Exception ^ ex)
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
		catch (Exception ^ ex)
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
		catch (Exception ^ ex)
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
		catch (Exception ^ ex)
		{
			_exceptionAction->Invoke(ex);
		}
	}

private:
	RTSSCoreControl* _coreControl;
	Action<Exception^>^ _exceptionAction;
	Object^ m_lock = gcnew Object();
};