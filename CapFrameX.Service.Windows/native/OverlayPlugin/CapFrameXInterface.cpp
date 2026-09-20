// CapFrameXInterface.cpp: implementation of the CCapFrameXInterface class.
//
// CapFrameX RTSS OverlayEditor data provider interface
//////////////////////////////////////////////////////////////////////
#include "stdafx.h"
#include "CapFrameXInterface.h"
#include "OverlayEditor.h"

#include <io.h>
//////////////////////////////////////////////////////////////////////
CCapFrameXInterface::CCapFrameXInterface()
{
    // Initialize counter buffers
    m_dwStatus          = PMDP_STATUS_OK;
    m_dwFrameCount      = 0;
    m_dwProcessId       = 0;

    for (DWORD dwPos = 0; dwPos < CAPFRAMEX_COUNTER_BUFFER_SIZE; dwPos++)
    {
        m_timestamp             [dwPos] = 0;

        m_Dropped               [dwPos] = FLT_MAX;
        m_msInPresentApi        [dwPos] = FLT_MAX;
        m_msBetweenPresents     [dwPos] = FLT_MAX;
        m_PresentMode           [dwPos] = FLT_MAX;
        m_msUntilRenderComplete [dwPos] = FLT_MAX;
        m_msUntilDisplayed      [dwPos] = FLT_MAX;
        m_msBetweenDisplayChange[dwPos] = FLT_MAX;
        m_msUntilRenderStart    [dwPos] = FLT_MAX;
        m_msSinceInput          [dwPos] = FLT_MAX;
        m_msGpuActive           [dwPos] = FLT_MAX;
        m_msInputLatency        [dwPos] = FLT_MAX;

        m_gpuPower              [dwPos] = FLT_MAX;
        m_gpuPowerLimit         [dwPos] = FLT_MAX;
        m_gpuVoltage            [dwPos] = FLT_MAX;
        m_gpuClock              [dwPos] = FLT_MAX;
        m_gpuTemperature        [dwPos] = FLT_MAX;
        m_gpuUsage              [dwPos] = FLT_MAX;
        m_gpuFanTachometer      [dwPos] = FLT_MAX;
        m_gpuFanTachometer2     [dwPos] = FLT_MAX;

        m_vramPower             [dwPos] = FLT_MAX;
        m_vramVoltage           [dwPos] = FLT_MAX;
        m_vramClock             [dwPos] = FLT_MAX;
        m_vramTemperature       [dwPos] = FLT_MAX;
        m_vramTotal             [dwPos] = FLT_MAX;
        m_vramUsage             [dwPos] = FLT_MAX;

        m_cpuPower              [dwPos] = FLT_MAX;
        m_cpuPowerLimit         [dwPos] = FLT_MAX;
        m_cpuClock              [dwPos] = FLT_MAX;
        m_cpuTemperature        [dwPos] = FLT_MAX;
        m_cpuUsage              [dwPos] = FLT_MAX;
    }

    // Populate counter descriptors
    AddDescriptor(CAPFRAMEX_COUNTER_STATUS,                     "%.0f", "",                  NULL);

    AddDescriptor(CAPFRAMEX_COUNTER_DROPPED,                    "%.0f", "",                  m_Dropped);
    AddDescriptor(CAPFRAMEX_COUNTER_MS_IN_PRESENT_API,          "%.1f", "ms",                m_msInPresentApi);
    AddDescriptor(CAPFRAMEX_COUNTER_MS_BETWEEN_PRESENTS,        "%.1f", "ms",                m_msBetweenPresents);
    AddDescriptor(CAPFRAMEX_COUNTER_PRESENT_MODE,               "%.0f", "",                  m_PresentMode);
    AddDescriptor(CAPFRAMEX_COUNTER_MS_UNTIL_RENDER_COMPLETE,   "%.1f", "ms",                m_msUntilRenderComplete);
    AddDescriptor(CAPFRAMEX_COUNTER_MS_UNTIL_DISPLAYED,         "%.1f", "ms",                m_msUntilDisplayed);
    AddDescriptor(CAPFRAMEX_COUNTER_MS_BETWEEN_DISPLAY_CHANGE,  "%.1f", "ms",                m_msBetweenDisplayChange);
    AddDescriptor(CAPFRAMEX_COUNTER_MS_UNTIL_RENDER_START,      "%.1f", "ms",                m_msUntilRenderStart);
    AddDescriptor(CAPFRAMEX_COUNTER_MS_SINCE_INPUT,             "%.1f", "ms",                m_msSinceInput);
    AddDescriptor(CAPFRAMEX_COUNTER_MS_GPU_ACTIVE,              "%.1f", "ms",                m_msGpuActive);
    AddDescriptor(CAPFRAMEX_COUNTER_MS_INPUT_LATENCY,           "%.1f", "ms",                m_msInputLatency);
    AddDescriptor(CAPFRAMEX_COUNTER_MS_REPORTING_LAG,           "%.1f", "ms",                NULL);
    AddDescriptor(CAPFRAMEX_COUNTER_FRAMERATE_PRESENTED,        "%.1f", "FPS",               NULL);
    AddDescriptor(CAPFRAMEX_COUNTER_FRAMERATE_DISPLAYED,        "%.1f", "FPS",               NULL);

    AddDescriptor(CAPFRAMEX_COUNTER_GPU_POWER,                  "%.1f", "W",                 m_gpuPower);
    AddDescriptor(CAPFRAMEX_COUNTER_GPU_POWER_LIMIT,            "%.1f", "W",                 m_gpuPowerLimit);
    AddDescriptor(CAPFRAMEX_COUNTER_GPU_VOLTAGE,                "%.1f", "mV",                m_gpuVoltage);
    AddDescriptor(CAPFRAMEX_COUNTER_GPU_CLOCK,                  "%.1f", "MHz",               m_gpuClock);
    AddDescriptor(CAPFRAMEX_COUNTER_GPU_TEMPERATURE,            "%.1f", GetDegreeCelsius(),  m_gpuTemperature);
    AddDescriptor(CAPFRAMEX_COUNTER_GPU_USAGE,                  "%.1f", "%",                 m_gpuUsage);
    AddDescriptor(CAPFRAMEX_COUNTER_GPU_FAN_TACHOMETER,         "%.1f", "RPM",               m_gpuFanTachometer);
    AddDescriptor(CAPFRAMEX_COUNTER_GPU_FAN_TACHOMETER2,        "%.1f", "RPM",               m_gpuFanTachometer2);

    AddDescriptor(CAPFRAMEX_COUNTER_VRAM_POWER,                 "%.1f", "W",                 m_vramPower);
    AddDescriptor(CAPFRAMEX_COUNTER_VRAM_VOLTAGE,               "%.1f", "V",                 m_vramVoltage);
    AddDescriptor(CAPFRAMEX_COUNTER_VRAM_CLOCK,                 "%.1f", "MHz",               m_vramClock);
    AddDescriptor(CAPFRAMEX_COUNTER_VRAM_TOTAL,                 "%.1f", "MB",                m_vramTotal);
    AddDescriptor(CAPFRAMEX_COUNTER_VRAM_USAGE,                 "%.1f", "MB",                m_vramUsage);

    AddDescriptor(CAPFRAMEX_COUNTER_CPU_POWER,                  "%.1f", "W",                 m_cpuPower);
    AddDescriptor(CAPFRAMEX_COUNTER_CPU_POWER_LIMIT,            "%.1f", "W",                 m_cpuPowerLimit);
    AddDescriptor(CAPFRAMEX_COUNTER_CPU_CLOCK,                  "%.1f", "MHz",               m_cpuClock);
    AddDescriptor(CAPFRAMEX_COUNTER_CPU_TEMPERATURE,            "%.1f", GetDegreeCelsius(),  m_cpuTemperature);
    AddDescriptor(CAPFRAMEX_COUNTER_CPU_USAGE,                  "%.1f", "%",                 m_cpuUsage);

    m_bProviderSpawned = FALSE;
}
//////////////////////////////////////////////////////////////////////
CCapFrameXInterface::~CCapFrameXInterface()
{
    if (IsProviderSpawned())
        CloseProvider();

    DestroyDescriptors();
}
//////////////////////////////////////////////////////////////////////
BOOL CCapFrameXInterface::Capture(DWORD dwDisplayDelay)
{
    LARGE_INTEGER pf;
    QueryPerformanceFrequency(&pf);

    LARGE_INTEGER pc;
    QueryPerformanceCounter(&pc);

    BOOL bResult = FALSE;

    // Open the shared memory created by CapFrameXDataProvider
    HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "PMDPSharedMemory");

    if (hMapFile)
    {
        LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);

        if (pMapAddr)
        {
            LPPMDP_SHARED_MEMORY lpSharedMemory = (LPPMDP_SHARED_MEMORY)pMapAddr;

            if (lpSharedMemory->dwSignature == 'PMDP')
            {
                m_dwStatus = lpSharedMemory->dwStatus;

                DWORD dwBufferSize = lpSharedMemory->dwFrameArrSize;
                DWORD dwFrameCount = min(lpSharedMemory->dwFrameCount, dwBufferSize);
                DWORD dwFramePos = (lpSharedMemory->dwFramePos - dwFrameCount) & (dwBufferSize - 1);

                if (dwDisplayDelay)
                {
                    // Smooth scrolling: discard frames newer than current time minus delay
                    while (dwFrameCount)
                    {
                        if ((1000.0f * (pc.QuadPart - lpSharedMemory->arrFrame[(dwFramePos + dwFrameCount - 1) & (dwBufferSize - 1)].qpc_time) / pf.QuadPart) > dwDisplayDelay)
                            break;

                        dwFrameCount--;
                    }
                }

                if (dwFrameCount > CAPFRAMEX_COUNTER_BUFFER_SIZE)
                {
                    dwFramePos = (dwFramePos + dwFrameCount - CAPFRAMEX_COUNTER_BUFFER_SIZE) & (dwBufferSize - 1);
                    dwFrameCount = CAPFRAMEX_COUNTER_BUFFER_SIZE;
                }

                float msInputLatency = FLT_MAX;

                for (DWORD dwFrame = 0; dwFrame < dwFrameCount; dwFrame++)
                {
                    m_timestamp             [dwFrame] = lpSharedMemory->arrFrame[dwFramePos].qpc_time;

                    m_Dropped               [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].dropped;
                    m_msInPresentApi        [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].ms_in_present_api;
                    m_msBetweenPresents     [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].ms_between_presents;
                    m_PresentMode           [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].present_mode;
                    m_msUntilRenderComplete [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].ms_until_render_complete;
                    m_msUntilDisplayed      [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].ms_until_displayed;
                    m_msBetweenDisplayChange[dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].ms_between_display_change;
                    m_msUntilRenderStart    [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].ms_until_render_start;
                    m_msSinceInput          [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].ms_since_input;
                    m_msGpuActive           [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].ms_gpu_active;

                    if (dwFrame && (m_Dropped[dwFrame] == 0.0f))
                        msInputLatency = m_msUntilDisplayed[dwFrame] + m_msBetweenPresents[dwFrame] - m_msInPresentApi[dwFrame - 1];

                    m_msInputLatency        [dwFrame] = msInputLatency;

                    // GPU telemetry
                    if (lpSharedMemory->arrFrame[dwFramePos].gpu_power_w.valid)
                        m_gpuPower          [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].gpu_power_w.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].gpu_sustained_power_limit_w.valid)
                        m_gpuPowerLimit     [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].gpu_sustained_power_limit_w.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].gpu_voltage_v.valid)
                        m_gpuVoltage        [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].gpu_voltage_v.data * 1000.0f;
                    if (lpSharedMemory->arrFrame[dwFramePos].gpu_frequency_mhz.valid)
                        m_gpuClock          [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].gpu_frequency_mhz.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].gpu_temperature_c.valid)
                        m_gpuTemperature    [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].gpu_temperature_c.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].gpu_utilization.valid)
                        m_gpuUsage          [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].gpu_utilization.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].fan_speed_rpm[0].valid)
                        m_gpuFanTachometer  [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].fan_speed_rpm[0].data;
                    if (lpSharedMemory->arrFrame[dwFramePos].fan_speed_rpm[1].valid)
                        m_gpuFanTachometer2 [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].fan_speed_rpm[1].data;

                    // VRAM telemetry
                    if (lpSharedMemory->arrFrame[dwFramePos].vram_power_w.valid)
                        m_vramPower         [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].vram_power_w.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].vram_voltage_v.valid)
                        m_vramVoltage       [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].vram_voltage_v.data * 1000.0f;
                    if (lpSharedMemory->arrFrame[dwFramePos].vram_frequency_mhz.valid)
                        m_vramClock         [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].vram_frequency_mhz.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].vram_temperature_c.valid)
                        m_vramTemperature   [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].vram_temperature_c.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].gpu_mem_total_size_b.valid)
                        m_vramTotal         [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].gpu_mem_total_size_b.data / 1048576.0f;
                    if (lpSharedMemory->arrFrame[dwFramePos].gpu_mem_used_b.valid)
                        m_vramUsage         [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].gpu_mem_used_b.data / 1048576.0f;

                    // CPU telemetry
                    if (lpSharedMemory->arrFrame[dwFramePos].cpu_power_w.valid)
                        m_cpuPower          [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].cpu_power_w.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].cpu_power_limit_w.valid)
                        m_cpuPowerLimit     [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].cpu_power_limit_w.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].cpu_frequency.valid)
                        m_cpuClock          [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].cpu_frequency.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].cpu_temperature_c.valid)
                        m_cpuTemperature    [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].cpu_temperature_c.data;
                    if (lpSharedMemory->arrFrame[dwFramePos].cpu_utilization.valid)
                        m_cpuUsage          [dwFrame] = (float)lpSharedMemory->arrFrame[dwFramePos].cpu_utilization.data;

                    dwFramePos = (dwFramePos + 1) & (dwBufferSize - 1);
                }

                m_dwFrameCount = dwFrameCount;

                bResult = TRUE;
            }

            UnmapViewOfFile(pMapAddr);
        }

        CloseHandle(hMapFile);
    }

    return bResult;
}
//////////////////////////////////////////////////////////////////////
DWORD CCapFrameXInterface::GetFrameCount()
{
    return m_dwFrameCount;
}
//////////////////////////////////////////////////////////////////////
float* CCapFrameXInterface::GetCounterBuffer(LPCSTR lpCounter)
{
    LPCAPFRAMEX_COUNTER_DESC lpDesc = GetDescriptor(lpCounter);

    if (lpDesc)
        return lpDesc->lpBuffer;

    return NULL;
}
//////////////////////////////////////////////////////////////////////
float CCapFrameXInterface::GetCounter(LPCSTR lpCounter)
{
    if (!_stricmp(lpCounter, CAPFRAMEX_COUNTER_STATUS))
        return (float)m_dwStatus;

    if (m_dwFrameCount)
    {
        if (!_stricmp(lpCounter, CAPFRAMEX_COUNTER_MS_REPORTING_LAG))
        {
            if (m_dwFrameCount)
            {
                LARGE_INTEGER pf;
                QueryPerformanceFrequency(&pf);

                if (pf.QuadPart)
                {
                    LARGE_INTEGER pc;
                    QueryPerformanceCounter(&pc);

                    return 1000.0f * (pc.QuadPart - m_timestamp[m_dwFrameCount - 1]) / pf.QuadPart;
                }
            }
        }
        else if (!_stricmp(lpCounter, CAPFRAMEX_COUNTER_FRAMERATE_PRESENTED))
        {
            return GetFramerate(CAPFRAMEX_COUNTER_MS_BETWEEN_PRESENTS);
        }
        else if (!_stricmp(lpCounter, CAPFRAMEX_COUNTER_FRAMERATE_DISPLAYED))
        {
            return GetFramerate(CAPFRAMEX_COUNTER_MS_BETWEEN_DISPLAY_CHANGE);
        }
        else
        {
            float* lpCounterBuffer = GetCounterBuffer(lpCounter);

            if (lpCounterBuffer)
                return lpCounterBuffer[m_dwFrameCount - 1];
        }
    }

    return FLT_MAX;
}
//////////////////////////////////////////////////////////////////////
DWORD CCapFrameXInterface::GetWindowSize(DWORD dwWindowTime)
{
    if (m_dwFrameCount)
    {
        float fltTime = (float)dwWindowTime;

        for (DWORD dwSize = 0; dwSize < m_dwFrameCount; dwSize++)
        {
            float fltFrametime = m_msBetweenPresents[m_dwFrameCount - 1 - dwSize];

            if (fltFrametime != FLT_MAX)
            {
                if (fltTime > fltFrametime)
                    fltTime = fltTime - fltFrametime;
                else
                    return dwSize + 1;
            }
        }

        return 0;
    }

    return 0;
}
//////////////////////////////////////////////////////////////////////
float CCapFrameXInterface::GetCounterAvg(LPCSTR lpCounter, DWORD dwWindowSize)
{
    if (m_dwFrameCount)
    {
        float* lpCounterBuffer = GetCounterBuffer(lpCounter);

        if (!lpCounterBuffer)
            return GetCounter(lpCounter);

        if (!_stricmp(lpCounter, CAPFRAMEX_COUNTER_DROPPED) ||
            !_stricmp(lpCounter, CAPFRAMEX_COUNTER_PRESENT_MODE))
            return GetCounter(lpCounter);

        float fltResult = 0.0f;
        DWORD dwCount = 0;

        dwWindowSize = min(dwWindowSize, m_dwFrameCount);

        while (dwWindowSize)
        {
            float fltData = lpCounterBuffer[m_dwFrameCount - dwWindowSize];

            if (fltData != FLT_MAX)
            {
                fltResult += fltData;
                dwCount++;
            }

            dwWindowSize--;
        }

        if (dwCount)
            return fltResult / dwCount;
    }

    return FLT_MAX;
}
//////////////////////////////////////////////////////////////////////
float CCapFrameXInterface::GetFramerate(LPCSTR lpCounter)
{
    if (m_dwFrameCount)
    {
        float* lpCounterBuffer = GetCounterBuffer(lpCounter);

        if (!lpCounterBuffer)
            return FLT_MAX;

        float fltTime = 0.0f;
        DWORD dwFrame = 0;

        for (dwFrame = 0; dwFrame < m_dwFrameCount; dwFrame++)
        {
            float fltFrametime = lpCounterBuffer[m_dwFrameCount - dwFrame - 1];

            if (fltFrametime != FLT_MAX)
                fltTime += fltFrametime;

            if (fltTime > 1000.0f)
                break;
        }

        if (fltTime != 0.0f)
            return 1000.0f * (dwFrame + 1) / fltTime;
    }

    return FLT_MAX;
}
//////////////////////////////////////////////////////////////////////
CString CCapFrameXInterface::GetCounterUnits(LPCSTR lpCounter)
{
    LPCAPFRAMEX_COUNTER_DESC lpDesc = GetDescriptor(lpCounter);

    if (lpDesc)
        return lpDesc->lpUnits;

    return "";
}
//////////////////////////////////////////////////////////////////////
CString CCapFrameXInterface::GetCounterFormat(LPCSTR lpCounter)
{
    LPCAPFRAMEX_COUNTER_DESC lpDesc = GetDescriptor(lpCounter);

    if (lpDesc)
        return lpDesc->lpFormat;

    return "%.0f";
}
//////////////////////////////////////////////////////////////////////
BOOL CCapFrameXInterface::IsBufferedCounter(LPCSTR lpCounter)
{
    if (!_stricmp(lpCounter, CAPFRAMEX_COUNTER_STATUS))
        return FALSE;
    if (!_stricmp(lpCounter, CAPFRAMEX_COUNTER_MS_REPORTING_LAG))
        return FALSE;
    if (!_stricmp(lpCounter, CAPFRAMEX_COUNTER_FRAMERATE_PRESENTED))
        return FALSE;
    if (!_stricmp(lpCounter, CAPFRAMEX_COUNTER_FRAMERATE_DISPLAYED))
        return FALSE;

    return TRUE;
}
//////////////////////////////////////////////////////////////////////
CString CCapFrameXInterface::GetProviderPath()
{
    char szServicePath[MAX_PATH];
    GetModuleFileName(g_hModule, szServicePath, MAX_PATH);
    PathRemoveFileSpec(szServicePath);

    strcat_s(szServicePath, MAX_PATH, "\\CapFrameXDataProvider\\CapFrameXDataProvider.exe");

    if (_taccess(szServicePath, 0))
        return "";

    return szServicePath;
}
//////////////////////////////////////////////////////////////////////
BOOL CCapFrameXInterface::SpawnProvider()
{
    CString strProviderPath = GetProviderPath();

    if (!strProviderPath.IsEmpty())
    {
        CString strCmdLine;
        strCmdLine.Format("-i %d", m_dwProcessId);

        if (ShellExecute(NULL, "open", strProviderPath, strCmdLine, NULL, SW_SHOWNORMAL) > (HINSTANCE)32)
        {
            m_bProviderSpawned = TRUE;
            return TRUE;
        }
    }

    return FALSE;
}
//////////////////////////////////////////////////////////////////////
BOOL CCapFrameXInterface::IsProviderRunning()
{
    return (FindWindow(NULL, "CapFrameXDataProviderConnectWnd") != NULL);
}
//////////////////////////////////////////////////////////////////////
BOOL CCapFrameXInterface::IsProviderSpawned()
{
    return m_bProviderSpawned;
}
//////////////////////////////////////////////////////////////////////
BOOL CCapFrameXInterface::CloseProvider()
{
    CString strProviderPath = GetProviderPath();

    if (!strProviderPath.IsEmpty())
    {
        if (ShellExecute(NULL, "open", strProviderPath, "-u", NULL, SW_SHOWNORMAL) > (HINSTANCE)32)
        {
            m_bProviderSpawned = FALSE;
            return TRUE;
        }
    }

    return FALSE;
}
//////////////////////////////////////////////////////////////////////
void CCapFrameXInterface::AddDescriptor(LPCSTR lpID, LPCSTR lpFormat, LPCSTR lpUnits, float* lpBuffer)
{
    LPCAPFRAMEX_COUNTER_DESC lpDesc = new CAPFRAMEX_COUNTER_DESC;

    lpDesc->lpID        = lpID;
    lpDesc->lpFormat    = lpFormat;
    lpDesc->lpUnits     = lpUnits;
    lpDesc->lpBuffer    = lpBuffer;

    AddTail(lpDesc);
}
//////////////////////////////////////////////////////////////////////
LPCAPFRAMEX_COUNTER_DESC CCapFrameXInterface::GetDescriptor(LPCSTR lpCounter)
{
    POSITION pos = GetHeadPosition();

    while (pos)
    {
        LPCAPFRAMEX_COUNTER_DESC lpDesc = GetNext(pos);

        if (!_stricmp(lpDesc->lpID, lpCounter))
            return lpDesc;
    }

    return NULL;
}
//////////////////////////////////////////////////////////////////////
void CCapFrameXInterface::DestroyDescriptors()
{
    POSITION pos = GetHeadPosition();

    while (pos)
        delete GetNext(pos);

    RemoveAll();
}
//////////////////////////////////////////////////////////////////////
DWORD CCapFrameXInterface::GetProcessId()
{
    return m_dwProcessId;
}
//////////////////////////////////////////////////////////////////////
void CCapFrameXInterface::SetProcessId(DWORD dwProcessId)
{
    m_dwProcessId = dwProcessId;
}
//////////////////////////////////////////////////////////////////////
