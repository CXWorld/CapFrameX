// CapFrameXInterface.h: interface for the CCapFrameXInterface class.
//
// CapFrameX RTSS OverlayEditor data provider interface
//////////////////////////////////////////////////////////////////////
#pragma once
//////////////////////////////////////////////////////////////////////
#include "PMDPSharedMemory.h"
//////////////////////////////////////////////////////////////////////
#define CAPFRAMEX_COUNTER_BUFFER_SIZE 1024
//////////////////////////////////////////////////////////////////////
// Counter identifiers for CapFrameX data sources
#define CAPFRAMEX_COUNTER_STATUS                    "Status"

#define CAPFRAMEX_COUNTER_DROPPED                   "Dropped"
#define CAPFRAMEX_COUNTER_MS_IN_PRESENT_API         "msInPresentApi"
#define CAPFRAMEX_COUNTER_MS_BETWEEN_PRESENTS       "msBetweenPresents"
#define CAPFRAMEX_COUNTER_PRESENT_MODE              "PresentMode"
#define CAPFRAMEX_COUNTER_MS_UNTIL_RENDER_COMPLETE  "msUntilRenderComplete"
#define CAPFRAMEX_COUNTER_MS_UNTIL_DISPLAYED        "msUntilDisplayed"
#define CAPFRAMEX_COUNTER_MS_BETWEEN_DISPLAY_CHANGE "msBetweenDisplayChange"
#define CAPFRAMEX_COUNTER_MS_UNTIL_RENDER_START     "msUntilRenderStart"
#define CAPFRAMEX_COUNTER_MS_SINCE_INPUT            "msSinceInput"
#define CAPFRAMEX_COUNTER_MS_GPU_ACTIVE             "msGpuActive"
#define CAPFRAMEX_COUNTER_MS_INPUT_LATENCY          "msInputLatency"
#define CAPFRAMEX_COUNTER_MS_REPORTING_LAG          "msReportingLag"
#define CAPFRAMEX_COUNTER_FRAMERATE_PRESENTED       "FrameratePresented"
#define CAPFRAMEX_COUNTER_FRAMERATE_DISPLAYED       "FramerateDisplayed"

#define CAPFRAMEX_COUNTER_GPU_POWER                 "GPU power"
#define CAPFRAMEX_COUNTER_GPU_POWER_LIMIT           "GPU power limit"
#define CAPFRAMEX_COUNTER_GPU_VOLTAGE               "GPU voltage"
#define CAPFRAMEX_COUNTER_GPU_CLOCK                 "GPU clock"
#define CAPFRAMEX_COUNTER_GPU_TEMPERATURE           "GPU temperature"
#define CAPFRAMEX_COUNTER_GPU_USAGE                 "GPU usage"
#define CAPFRAMEX_COUNTER_GPU_FAN_TACHOMETER        "GPU fan tachometer"
#define CAPFRAMEX_COUNTER_GPU_FAN_TACHOMETER2       "GPU fan tachometer 2"

#define CAPFRAMEX_COUNTER_VRAM_POWER                "VRAM power"
#define CAPFRAMEX_COUNTER_VRAM_VOLTAGE              "VRAM voltage"
#define CAPFRAMEX_COUNTER_VRAM_CLOCK                "VRAM clock"
#define CAPFRAMEX_COUNTER_VRAM_TOTAL                "VRAM total"
#define CAPFRAMEX_COUNTER_VRAM_USAGE                "VRAM usage"

#define CAPFRAMEX_COUNTER_CPU_POWER                 "CPU power"
#define CAPFRAMEX_COUNTER_CPU_POWER_LIMIT           "CPU power limit"
#define CAPFRAMEX_COUNTER_CPU_CLOCK                 "CPU clock"
#define CAPFRAMEX_COUNTER_CPU_TEMPERATURE           "CPU temperature"
#define CAPFRAMEX_COUNTER_CPU_USAGE                 "CPU usage"
/////////////////////////////////////////////////////////////////////////////
typedef struct CAPFRAMEX_COUNTER_DESC
{
    LPCSTR lpID;
    LPCSTR lpFormat;
    LPCSTR lpUnits;
    float* lpBuffer;
} CAPFRAMEX_COUNTER_DESC, *LPCAPFRAMEX_COUNTER_DESC;
/////////////////////////////////////////////////////////////////////////////
class CCapFrameXInterface : public CList<LPCAPFRAMEX_COUNTER_DESC, LPCAPFRAMEX_COUNTER_DESC>
{
public:
    void    AddDescriptor(LPCSTR lpID, LPCSTR lpFormat, LPCSTR lpUnits, float* lpBuffer);
    void    DestroyDescriptors();
    LPCAPFRAMEX_COUNTER_DESC GetDescriptor(LPCSTR lpCounter);

    DWORD   GetFrameCount();
    BOOL    Capture(DWORD dwDisplayDelay = 0);
    float   GetCounter(LPCSTR lpCounter);
    DWORD   GetWindowSize(DWORD dwWindowTime);
    float   GetCounterAvg(LPCSTR lpCounter, DWORD dwWindowSize);
    float*  GetCounterBuffer(LPCSTR lpCounter);
    CString GetCounterFormat(LPCSTR lpCounter);
    CString GetCounterUnits(LPCSTR lpCounter);
    BOOL    IsBufferedCounter(LPCSTR lpCounter);
    float   GetFramerate(LPCSTR lpCounter);

    CString GetProviderPath();
    BOOL    SpawnProvider();
    BOOL    IsProviderRunning();
    BOOL    IsProviderSpawned();
    BOOL    CloseProvider();

    DWORD   GetProcessId();
    void    SetProcessId(DWORD dwProcessId);

    CCapFrameXInterface();
    virtual ~CCapFrameXInterface();

protected:
    DWORD       m_dwStatus;
    DWORD       m_dwFrameCount;
    DWORD       m_dwProcessId;

    uint64_t    m_timestamp                 [CAPFRAMEX_COUNTER_BUFFER_SIZE];

    float       m_Dropped                   [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_msInPresentApi            [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_msBetweenPresents         [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_PresentMode               [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_msUntilRenderComplete     [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_msUntilDisplayed          [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_msBetweenDisplayChange    [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_msUntilRenderStart        [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_msSinceInput              [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_msGpuActive               [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_msInputLatency            [CAPFRAMEX_COUNTER_BUFFER_SIZE];

    float       m_gpuPower                  [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_gpuPowerLimit             [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_gpuVoltage                [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_gpuClock                  [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_gpuTemperature            [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_gpuUsage                  [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_gpuFanTachometer          [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_gpuFanTachometer2         [CAPFRAMEX_COUNTER_BUFFER_SIZE];

    float       m_vramPower                 [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_vramVoltage               [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_vramClock                 [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_vramTemperature           [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_vramTotal                 [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_vramUsage                 [CAPFRAMEX_COUNTER_BUFFER_SIZE];

    float       m_cpuPower                  [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_cpuPowerLimit             [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_cpuClock                  [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_cpuTemperature            [CAPFRAMEX_COUNTER_BUFFER_SIZE];
    float       m_cpuUsage                  [CAPFRAMEX_COUNTER_BUFFER_SIZE];

    BOOL        m_bProviderSpawned;
};
//////////////////////////////////////////////////////////////////////
