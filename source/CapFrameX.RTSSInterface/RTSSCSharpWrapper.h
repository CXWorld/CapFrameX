#pragma once
#include "RTSSCoreControl.h"
#include "RTSSFrameTimeRing.h"
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
				_coreControl->RunHistoryLabels.clear();
				for (size_t i = 0; i < _coreControl->RunHistory.size(); i++)
				{
					String^ label = String::Format("Run {0}:", static_cast<int>(i + 1));
					_coreControl->RunHistoryLabels.push_back(RtssTextFormatter::Translate(label, GetACP()));
				}
				_coreControl->RunHistoryResultLabel = RtssTextFormatter::Translate("Result:", GetACP());
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

	// The entries Refresh renders. Read by tests; RTSS itself is never asked.
	property int OverlayEntryCount
	{
		int get()
		{
			msclr::lock l(m_lock);
			return static_cast<int>(_coreControl->OverlayEntries.size());
		}
	}

	void SetOverlayEntries(array<IOverlayEntry^>^ overlayEntries)
	{
		// An empty list is a real state (every entry switched off) and must clear the rows;
		// returning early left the last ones on screen.
		if (overlayEntries == nullptr)
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
					entry.GroupName = RtssTextFormatter::FormatGroupName(managedEntry, GetACP());
					entry.Value = RtssTextFormatter::FormatValue(managedEntry, GetACP());
					entry.ShowGraph = managedEntry->ShowGraph;
					SetPercentageBarProperties(managedEntry, entry);

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
						_coreControl->OverlayEntries[i].GroupName = RtssTextFormatter::FormatGroupName(managedEntry, GetACP());
						_coreControl->OverlayEntries[i].Value = RtssTextFormatter::FormatValue(managedEntry, GetACP());
						_coreControl->OverlayEntries[i].ShowGraph = managedEntry->ShowGraph;
						_coreControl->OverlayEntries[i].Color = managedEntry->Color;
						SetPercentageBarProperties(managedEntry, _coreControl->OverlayEntries[i]);
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

	String^ GetApiInfo(INT processId)
	{
		return gcnew String(_coreControl->GetApiInfo((UINT)processId));
	}

	String^ GetResolution(INT processId)
	{
		return gcnew String(_coreControl->GetResolution((UINT)processId));
	}

	// Whether RTSS' shared memory carries a valid header, which RTSS writes when it starts and
	// invalidates when it exits. Read-only, so it takes no lock.
	bool IsSharedMemoryAvailable()
	{
		try
		{
			return _coreControl->IsSharedMemoryAvailable() != FALSE;
		}
		catch (SEHException^)
		{
			_exceptionAction->Invoke(gcnew Exception("Unmanaged Exception. Error while probing the RTSS shared memory."));
			return false;
		}
	}

	Tuple<double, double>^ GetCurrentFramerate(INT processId)
	{
		std::vector<float> result = _coreControl->GetCurrentFramerate((UINT)processId);
		return gcnew Tuple<double, double>(result[0], result[1]);
	}

	array<float>^ GetFrameTimesInterval(INT processId, INT milliseconds)
	{
		return ToManagedArray(_coreControl->GetFrameTimesInterval((UINT)processId, (UINT)milliseconds));
	}

internal:
	// RTSSFrameTimeRing::Collect on a ring CapFrameX.Test fills (see InternalsVisibleTo in the
	// .cpp), with the interval in ms as GetFrameTimesInterval takes it.
	static array<float>^ CollectRingFrameTimes(array<UInt32>^ ring, UInt32 position, UInt32 milliseconds)
	{
		if (ring == nullptr || ring->Length != static_cast<int>(RTSSFrameTimeRing::Size))
			throw gcnew ArgumentException("The ring holds 1024 frame times.", "ring");

		std::vector<DWORD> nativeRing(RTSSFrameTimeRing::Size);
		for (int i = 0; i < ring->Length; i++)
			nativeRing[i] = ring[i];

		return ToManagedArray(RTSSFrameTimeRing::Collect(nativeRing.data(), position, 1000 * milliseconds));
	}

public:

	void SetShowRunHistory(bool showRunHistory)
	{
		{
			msclr::lock l(m_lock);
			_coreControl->ShowRunHistory = showRunHistory;
		}
	}

	void SetOSDCustomPosition(bool active)
	{
		{
			msclr::lock l(m_lock);
			_coreControl->OSDCustomPosition = active;
		}
	}

	void SetOverlayPosition(INT x, INT y)
	{
		{
			msclr::lock l(m_lock);
			_coreControl->OverlayPositionX = x;
			_coreControl->OverlayPositionY = y;
		}
	}

private:
	static array<float>^ ToManagedArray(const std::vector<float>& frameTimes)
	{
		array<float>^ frameTimesArray = gcnew array<float>(static_cast<int>(frameTimes.size()));

		// RTSS has no frame times of a process it does not track (not running, the game not
		// hooked, a first frame of a second or more): pinning the first element of the empty
		// array threw IndexOutOfRangeException on the capture heartbeat's thread, which ended
		// the app (crash reports of 1.8.6.2 with CaptureRTSSFrameTimes on, 2026-11-17).
		if (!frameTimes.empty())
		{
			pin_ptr<float> dest = &frameTimesArray[0];
			std::memcpy(dest, frameTimes.data(), frameTimes.size() * sizeof(float));
		}
		return frameTimesArray;
	}

	static void SetPercentageBarProperties(IOverlayEntry^ managedEntry, OverlayEntry& entry)
	{
		entry.ValueDisplayMode = static_cast<OverlayValueDisplayMode>(
			static_cast<int>(OverlayPercentageBar::GetDisplayMode(managedEntry)));
		double value = 0.0;
		entry.HasPercentageValue = OverlayPercentageBar::TryGetValue(managedEntry, value);
		entry.PercentageValue = value;
		entry.PercentageBarColor = OverlayPercentageBar::GetColor(managedEntry);
	}

	RTSSCoreControl* _coreControl;
	Action<Exception^>^ _exceptionAction;
	Object^ m_lock = gcnew Object();
};
