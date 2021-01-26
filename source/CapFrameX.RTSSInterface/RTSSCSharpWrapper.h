#pragma once
#include "RTSSCoreControl.h"
#include <msclr\lock.h>
#include <string>

#pragma managed
using namespace System;
using namespace System::Threading;
using namespace System::Threading::Tasks;
using namespace System::Collections::Generic;
using namespace CapFrameX::Contracts::Overlay;
using namespace System::Runtime::CompilerServices;
using namespace System::Runtime::InteropServices;

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

	void OnOSDOn()
	{
		_coreControl->OnOSDOn();
	}

	void OnOSDOff()
	{
		_coreControl->OnOSDOff();
	}

	void OnOSDToggle()
	{
		_coreControl->OnOSDToggle();
	}

	void ClearOSD()
	{
		try
		{
			{
				msclr::lock l(m_lock);
				_coreControl->UpdateOSD("");
			}
		}
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while resetting OSD."));
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
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while refreshing OSD."));
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
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while releasing OSD."));
		}
	}

	void SetFormatVariables(String^ variables)
	{
		try
		{
			{
				msclr::lock l(m_lock);
				_coreControl->SetFormatVariables(variables);
			}
		}
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while setting format variables."));
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
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while setting capture time to active."));
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
					for (int i = 0; i < runHistory->Length; i++)
					{
						String^ run = runHistory[i];
						_coreControl->RunHistory.push_back(run);
					}
				}
			}
		}
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while setting run history."));
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
					for (int i = 0; i < outlierFlags->Length; i++)
					{
						bool outlierFlag = outlierFlags[i];
						_coreControl->RunHistoryOutlierFlags.push_back(outlierFlag);
					}
				}
			}
		}
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while setting outlier flags."));
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
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while setting run history aggregation."));
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

				for (int i = 0; i < overlayEntries->Length; i++)
				{
					IOverlayEntry^ managedEntry = overlayEntries[i];
					OverlayEntry entry;

					// mapping member
					entry.Identifier = managedEntry->Identifier;
					entry.Description = managedEntry->Description;
					entry.ShowOnOverlay = managedEntry->ShowOnOverlay;
					entry.GroupName = managedEntry->FormattedGroupName;
					entry.Value = managedEntry->FormattedValue;
					entry.ShowGraph = managedEntry->ShowGraph;

					_coreControl->OverlayEntries.push_back(entry);
				}
			}
		}
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while setting overlay entries."));
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
						_coreControl->OverlayEntries[i].GroupName = managedEntry->FormattedGroupName;
						_coreControl->OverlayEntries[i].Value = managedEntry->FormattedValue;
						_coreControl->OverlayEntries[i].ShowGraph = managedEntry->ShowGraph;
						_coreControl->OverlayEntries[i].Color = managedEntry->Color;
						break;
					}
				}
			}
		}
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while setting single overlay enetry."));
		}
	}

	bool IsOSDLocked()
	{
		return _coreControl->IsOSDLocked();
	}

	bool IsProcessDetected(UINT processId)
	{
		return _coreControl->IsProcessDetected(processId);
	}

	String^ GetApiInfo(UINT processId)
	{
		return gcnew String(_coreControl->GetApiInfo(processId));
	}

	Tuple<double, double>^ GetCurrentFramerate(UINT processId)
	{
		std::vector<float> result = _coreControl->GetCurrentFramerate(processId);
		return gcnew Tuple<double, double>(result[0], result[1]);
	}

	Tuple<double, double>^ GetCurrentFramerateFromForegroundWindow()
	{
		std::vector<float> result = _coreControl->GetCurrentFramerateFromForegroundWindow();
		return gcnew Tuple<double, double>(result[0], result[1]);
	}

	void CloseHandles()
	{
		_coreControl->CloseHandles();
	}

private:
	RTSSCoreControl* _coreControl;
	Action<Exception^>^ _exceptionAction;
	Object^ m_lock = gcnew Object();
};