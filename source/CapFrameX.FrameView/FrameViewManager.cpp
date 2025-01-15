#include <cassert>
#include <vector>
#include <algorithm>
#include <map>
#include "Helper.h"
#include "FvSDK.h"
#include "FrameViewManager.h"

namespace FrameView
{
	FvSession session;

	bool IntializeFrameViewSession()
	{
		bool check = false;
		SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32);

		FvStatus initStatus = FvSDK_Initialize();
		if (initStatus == FV_SUCCESS)
		{
			initStatus = FvSDK_CreateSession(&session);
			if (initStatus == FV_SUCCESS)
			{
				initStatus = FvSDK_StartSession(session);

				if (initStatus == FV_SDK_SESSION_IN_PROGRESS)
				{
					check = true;
				}
			}
		}

		return check;
	}

	bool CloseFrameViewSession()
	{
		FvStatus closeStatus = FvSDK_StopSession(session);
		if (closeStatus != FV_SUCCESS)
		{
			return false;
		}

		closeStatus = FvSDK_DestroySession(session);
		if (closeStatus != FV_SUCCESS)
		{
			return false;
		}

		closeStatus = FvSDK_Shutdown();
		if (closeStatus != FV_SUCCESS)
		{
			return false;
		}
	}

	double GetAveragePcl(const int32_t pid)
	{
		double avgPCl = 0;

		FvMetricType Metrics[] =
		{
			eAvgFPS,
			eAvgSWPCLatency
		};

		constexpr unsigned int numMetrics = sizeof(Metrics) / sizeof(Metrics[0]);
		FvMetricParams params = {};
		params.metrics = &Metrics[0];
		params.numMetrics = numMetrics;

		auto enableMetricsStatus = FvSDK_EnableMetrics2(session, params);
		if (enableMetricsStatus == FV_SUCCESS)
		{
			auto sampleStatus = FvSDK_SampleData(session, FVSDK_SAMPLEDATA_DONOTWAIT);
			if (sampleStatus != FV_NO_DATA)
			{
				Samples samples[numMetrics] = {};
				samples[0].type = eAvgFPS;
				samples[1].type = eAvgSWPCLatency;

				auto readStatus = FvSDK_ReadData(session, &samples[0], numMetrics);

				if (readStatus == FV_SUCCESS || readStatus == FV_MORE_DATA)
				{
					//Allocating buffer of size samples[0].mNumSamples for AvgFPS
					std::vector<AvgFPS> AvgFPSSamples;
					AvgFPSSamples.resize(samples[0].mNumSamples);
					samples[0].mData = AvgFPSSamples.data();

					//Allocating buffer of size samples[1].mNumSamples for AvgPCL
					std::vector<AvgSWPCLatency> AvgSWPCLatencySamples;
					AvgSWPCLatencySamples.resize(samples[1].mNumSamples);
					samples[1].mData = AvgSWPCLatencySamples.data();

					// Call 2: This time samples[i].mData is not NULL, so ReadData will fill the given buffer with the data it has. 
					readStatus = FvSDK_ReadData(session, &samples[0], numMetrics);

					if (readStatus == FV_SUCCESS)
					{
						for (const auto& PCLSample : AvgSWPCLatencySamples)
						{
							if (PCLSample.mFPS.PID == (unsigned long long)pid)
							{
								avgPCl = PCLSample.mFPS.AvgFPS;
							}
						}
					}
				}
			}
		}

		return avgPCl;
	}
}