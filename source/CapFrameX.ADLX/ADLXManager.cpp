#include "pch.h"
#include "ADLXManager.h"
#include <crtdbg.h>
#include <stdio.h>
#include <stdlib.h>
#include "SDK/ADLXHelper/Windows/Cpp/ADLXHelper.h"
#include "SDK/Include/IPerformanceMonitoring.h"

// Use ADLX namespace
using namespace adlx;

// ADLXHelper instance
// No outstanding interfaces from ADLX must exist when ADLX is destroyed.
// Use global variables to ensure validity of the interface.
static ADLXHelper g_ADLXHelp;

IADLXPerformanceMonitoringServicesPtr perfMonitoringService;
IADLXGPUListPtr gpus;

bool IntializeAdlx()
{
    ADLX_RESULT res = ADLX_FAIL;

    // Initialize ADLX
    res = g_ADLXHelp.Initialize();

    if (ADLX_SUCCEEDED(res))
    {
        // Get Performance Monitoring services
        ADLX_RESULT res = g_ADLXHelp.GetSystemServices()->GetPerformanceMonitoringServices(&perfMonitoringService);
        if (ADLX_SUCCEEDED(res))
        {
            // Get GPU list
            res = g_ADLXHelp.GetSystemServices()->GetGPUs(&gpus);
            if (ADLX_SUCCEEDED(res))
            {
                IADLXGPUPtr oneGPU;
                // Use the first GPU in the list
                res = gpus->At(gpus->Begin(), &oneGPU);
                if (ADLX_SUCCEEDED(res))
                {
                    return true;
                }
                else
                    return false;
            }
            else
                return false;
        }
        else
            return false;
    }
    else
        return false;

    // Destroy ADLX
    res = g_ADLXHelp.Terminate();

    return false;
}

adlx_uint GetAtiAdpaterCount()
{
    if (gpus == nullptr)
        return 0u;

    adlx_uint size = 0;
    size = gpus->Size();
    return size;
}

bool GetAdlxTelemetry(const adlx_uint index, AdlxTelemetryData* telemetryData)
{
    bool check = true;
    ADLX_RESULT res = ADLX_FAIL;

    // Get GPU metrics support
    IADLXGPUMetricsSupportPtr gpuMetricsSupport;

    IADLXGPUPtr oneGPU;
    res = gpus->At(index, &oneGPU);

    if (ADLX_SUCCEEDED(res))
    {
        ADLX_RESULT res1 = perfMonitoringService->GetSupportedGPUMetrics(oneGPU, &gpuMetricsSupport);
    }

    return false;
}
