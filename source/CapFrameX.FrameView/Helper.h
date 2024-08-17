#pragma once
#include <iostream>
#include <string.h>
#include <stdlib.h>
#include <Windows.h>
namespace
{
	HANDLE g_StopProcessEvent;

	enum SDKMode
	{
		FVSDK_MODE_NOT_SET,
		FVSDK_REALTIME_METRICS_CAPTURE,
		FVSDK_BENCHMARK_METRICS_CAPTURE,
	};

	struct CmdArgs
	{
		SDKMode			mode = FVSDK_MODE_NOT_SET;
		UINT64			benchmarkTime = 0;
	};

	void PrintHelp()
	{
		// --help/-h
		// --sdk_mode/-m [realtime | benchmark ]
		//			realtime: Realtime metrics capture
		//			benchmark: Capture average metrics of a benchmarking period
		//
		// --benchmark_time [time in seconds]
		printf("FvSDK Sample App help:\n");
		printf("\t--help/-h:\t\t\t\tPrint this menu.\n");
		printf("\t-m [realtime | benchmark]:\tSet FvSDK client mode to capture either Realtime metrics or End of Benchmark Metrics\n");
		printf("\t-t [time in seconds]:\tSet this if mode is set to \"benchmark\". This will set the time for which the benchmark is captured\n");
		printf("\n\n");
		printf("Command for running in realtime mode:\n");
		printf("\tFvSDK_SampleApp.exe -m realtime\n");
		printf("\n");
		printf("Command for running in benchmarking mode for 20 seconds:\n");
		printf("\tFvSDK_SampleApp.exe -m benchmark -t 20\n");

		exit(0);
	}
	CmdArgs ParseCmdArgs(int argc, char* argv[])
	{
		if (argc <= 1)
		{
			PrintHelp();
		}
		CmdArgs args;
		for (int i = 1; i < argc; i++)
		{
			if (strcmp(argv[i], "--help") == 0 || strcmp(argv[i], "-h") == 0)
			{
				PrintHelp();
			}
			else if (strcmp(argv[i], "-m") == 0)
			{
				if (++i < argc)
				{
					if (strcmp(argv[i], "realtime") == 0)
					{
						args.mode = FVSDK_REALTIME_METRICS_CAPTURE;
					}
					else if (strcmp(argv[i], "benchmark") == 0)
					{
						args.mode = FVSDK_BENCHMARK_METRICS_CAPTURE;
					}
					else
					{
						PrintHelp();
					}
				}
				else
				{
					PrintHelp();
				}
			}
			else if (strcmp(argv[i], "-t") == 0)
			{
				if (++i < argc)
				{
					args.benchmarkTime = atoi(argv[i]);
				}
				else
				{
					PrintHelp();
				}
			}
		}
		if (args.mode == FVSDK_BENCHMARK_METRICS_CAPTURE && args.benchmarkTime == 0)
		{
			PrintHelp();
		}
		return args;
	}

	BOOL CALLBACK HandleCtrlEvent(DWORD ctrlType)
	{
		SetEvent(g_StopProcessEvent);
		return TRUE; // The signal was handled, don't call any other handlers
	}

	void InitializeSampleApp()
	{
		SetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_SYSTEM32);
		SetConsoleCtrlHandler(HandleCtrlEvent, TRUE);

		g_StopProcessEvent = CreateEvent(NULL, TRUE, FALSE, NULL);
	}

	void DeinitializeSampleApp()
	{
		ResetEvent(g_StopProcessEvent);
		CloseHandle(g_StopProcessEvent);
	}

}