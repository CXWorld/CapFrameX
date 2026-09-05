/////////////////////////////////////////////////////////////////////////////
// created by Unwinder - modified by ZeroStrat
/////////////////////////////////////////////////////////////////////////////

#include "stdafx.h"
#include "RTSSSharedMemory.h"
#include "RTSSCoreControl.h"
#include "GroupedString.h"

#include <shlwapi.h>
#include <float.h>
#include <io.h>
#include <tuple>
#include <iostream>
#include <stdexcept>
#include <string>

#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif

// RTSS OSD text slots. The server keeps three of them per OSD entry, each newer one larger:
// szOSD (256 B), szOSDEx (4 KB, shared memory v2.7+) and szOSDEx2 (32 KB, v2.20+ / RTSS 2021).
// We used szOSDEx, whose 4 KB silently truncated the OSD as soon as a profile enabled per-core
// rows — and because the graph markup is appended AFTER the entry text, the framerate/frametime
// graphs were the first thing to fall off the end. RTSS' author recommends szOSDEx2; picking the
// largest slot the running server actually offers is what the helpers below do.
typedef RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_OSD_ENTRY RTSS_OSD_ENTRY;

enum EOSDTextSlot { OSD_SLOT_LEGACY, OSD_SLOT_EX, OSD_SLOT_EX2 };

// The entry is shared memory owned by the server, so a field at the wrong offset would not fail
// loudly — it would scribble over a neighbouring OSD slot. Pin the layout against the official
// RTSS SDK header (v2.20) so a botched merge of RTSSSharedMemory.h breaks the build instead.
static_assert(offsetof(RTSS_OSD_ENTRY, szOSD) == 0, "RTSS OSD entry layout changed");
static_assert(offsetof(RTSS_OSD_ENTRY, szOSDOwner) == 256, "RTSS OSD entry layout changed");
static_assert(offsetof(RTSS_OSD_ENTRY, szOSDEx) == 512, "RTSS OSD entry layout changed");
static_assert(offsetof(RTSS_OSD_ENTRY, buffer) == 4608, "RTSS OSD entry layout changed");
static_assert(offsetof(RTSS_OSD_ENTRY, szOSDEx2) == 266752, "RTSS OSD entry layout changed");
static_assert(sizeof(RTSS_OSD_ENTRY) == 299520, "RTSS OSD entry layout changed");

#define RTSS_SHM_VERSION_OSD_EX		0x00020007	// v2.7:  szOSDEx
#define RTSS_SHM_VERSION_OSD_EX2	0x00020014	// v2.20: szOSDEx2

// Deciding on the version ALONE would be unsafe: the entry stride comes from the running server
// (dwOSDEntrySize), so on a server whose entries are shorter than our struct, writing szOSDEx2
// would run past the entry and corrupt the next OSD slot. Require the field to physically fit.
static EOSDTextSlot GetOSDTextSlot(DWORD dwVersion, DWORD dwOSDEntrySize)
{
	if (dwVersion >= RTSS_SHM_VERSION_OSD_EX2 &&
		dwOSDEntrySize >= offsetof(RTSS_OSD_ENTRY, szOSDEx2) + sizeof(RTSS_OSD_ENTRY().szOSDEx2))
		return OSD_SLOT_EX2;

	if (dwVersion >= RTSS_SHM_VERSION_OSD_EX &&
		dwOSDEntrySize >= offsetof(RTSS_OSD_ENTRY, szOSDEx) + sizeof(RTSS_OSD_ENTRY().szOSDEx))
		return OSD_SLOT_EX;

	return OSD_SLOT_LEGACY;
}

static DWORD GetOSDTextCapacity(EOSDTextSlot slot)
{
	switch (slot)
	{
	case OSD_SLOT_EX2:	return sizeof(RTSS_OSD_ENTRY().szOSDEx2);
	case OSD_SLOT_EX:	return sizeof(RTSS_OSD_ENTRY().szOSDEx);
	default:			return sizeof(RTSS_OSD_ENTRY().szOSD);
	}
}

static void WriteOSDText(RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY pEntry,
	EOSDTextSlot slot, LPCSTR lpText)
{
	switch (slot)
	{
	case OSD_SLOT_EX2:
		// Write ONLY this slot and leave the smaller ones untouched — that is what the RTSS SDK's
		// own client does (SDK\Plugins\Client\OverlayEditor\RTSSSharedMemoryInterface.cpp). An
		// earlier version here also zeroed szOSDEx/szOSD to keep a stale slot from shadowing this
		// one; that is a deviation from the reference, and if the server treats an empty szOSD /
		// szOSDEx as "slot carries no text" it suppresses the whole OSD.
		strncpy_s(pEntry->szOSDEx2, sizeof(pEntry->szOSDEx2), lpText, sizeof(pEntry->szOSDEx2) - 1);
		break;
	case OSD_SLOT_EX:
		strncpy_s(pEntry->szOSDEx, sizeof(pEntry->szOSDEx), lpText, sizeof(pEntry->szOSDEx) - 1);
		break;
	default:
		strncpy_s(pEntry->szOSD, sizeof(pEntry->szOSD), lpText, sizeof(pEntry->szOSD) - 1);
		break;
	}
}

// Removes RTSS OSD format tags (e.g. <S1>, <C100>, <A0>) from a string, leaving
// the plain text. Used to reuse an already formatted overlay entry value (which
// carries the CapFrameX computed value) as a graph label instead of RTSS' own
// <FR>/<FT> macros.
static CString StripOSDFormatTags(const CString& input)
{
	CString output;
	bool insideTag = false;
	for (int i = 0; i < input.GetLength(); i++)
	{
		TCHAR ch = input[i];
		if (ch == '<') { insideTag = true; continue; }
		if (ch == '>') { insideTag = false; continue; }
		if (!insideTag) output += ch;
	}
	output.Trim();
	return output;
}

RTSSCoreControl::RTSSCoreControl()
{
	m_strInstallPath = "";

	RunHistory.push_back("N/A");
	RunHistory.push_back("N/A");
	RunHistory.push_back("N/A");

	m_bMultiLineOutput = TRUE;
	m_bFormatTags = TRUE;
	m_bFillGraphs = FALSE;
	m_bConnected = FALSE;

	OSDCustomPosition = FALSE;
	OverlayPositionX = 0;
	OverlayPositionY = 0;
}

RTSSCoreControl::~RTSSCoreControl() { }

CString RTSSCoreControl::GetApiInfo(DWORD processId)
{
	CString api = "unknown";
	HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");

	if (hMapFile)
	{
		LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') && (pMem->dwVersion >= 0x00020000))
			{
				for (DWORD dwEntry = 0; dwEntry < pMem->dwAppArrSize; dwEntry++)
				{
					RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY pEntry = (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY)((LPBYTE)pMem + pMem->dwAppArrOffset + dwEntry * pMem->dwAppEntrySize);

					if (pEntry->dwProcessID == processId)
					{
						api = (pEntry->dwFlags & APPFLAG_API_USAGE_MASK) == APPFLAG_OGL ? "OpenGL"
							: (pEntry->dwFlags & APPFLAG_API_USAGE_MASK) == APPFLAG_DD ? "DirectDraw"
							: (pEntry->dwFlags & APPFLAG_API_USAGE_MASK) == APPFLAG_D3D8 ? "DX8"
							: (pEntry->dwFlags & APPFLAG_API_USAGE_MASK) == APPFLAG_D3D9 ? "DX9"
							: (pEntry->dwFlags & APPFLAG_API_USAGE_MASK) == APPFLAG_D3D9EX ? "DX9 EX"
							: (pEntry->dwFlags & APPFLAG_API_USAGE_MASK) == APPFLAG_D3D10 ? "DX10"
							: (pEntry->dwFlags & APPFLAG_API_USAGE_MASK) == APPFLAG_D3D11 ? "DX11"
							: (pEntry->dwFlags & APPFLAG_API_USAGE_MASK) == APPFLAG_D3D12 ? "DX12"
							: (pEntry->dwFlags & APPFLAG_API_USAGE_MASK) == APPFLAG_D3D12AFR ? "DX12 AFR"
							: (pEntry->dwFlags & APPFLAG_API_USAGE_MASK) == APPFLAG_VULKAN ? "Vulkan"
							: "unknown";

						break;
					}
				}
				UnmapViewOfFile(pMapAddr);
			}
			CloseHandle(hMapFile);
		}
	}

	return api;
}

CString RTSSCoreControl::GetResolution(DWORD processId)
{
	// returns the render resolution of the given 3D application as "WxH"
	// (e.g. "2560x1440") or an empty string when it cannot be determined
	CString resolution = "";
	HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");

	if (hMapFile)
	{
		LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;

		if (pMem)
		{
			// dwResolutionX / dwResolutionY are valid for v2.20 (0x00020014) and
			// newer shared memory format only. Reading them on older RTSS versions
			// would point beyond the actual (smaller) app entry, so guard the version.
			if ((pMem->dwSignature == 'RTSS') && (pMem->dwVersion >= 0x00020014))
			{
				for (DWORD dwEntry = 0; dwEntry < pMem->dwAppArrSize; dwEntry++)
				{
					RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY pEntry = (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY)((LPBYTE)pMem + pMem->dwAppArrOffset + dwEntry * pMem->dwAppEntrySize);

					if (pEntry->dwProcessID == processId)
					{
						// resolution stays 0 until RTSS has hooked and measured the
						// swapchain, so only format it once both dimensions are valid
						if (pEntry->dwResolutionX > 0 && pEntry->dwResolutionY > 0)
							resolution.Format("%ux%u", pEntry->dwResolutionX, pEntry->dwResolutionY);

						break;
					}
				}
			}
			UnmapViewOfFile(pMapAddr);
		}
		CloseHandle(hMapFile);
	}

	return resolution;
}

std::vector<float> RTSSCoreControl::GetCurrentFramerate(DWORD processId)
{
	std::vector<float> result;
	float currentFramerate = 0;
	float currentFrametime = 0;
	LPDWORD lpdwProcessiD = 0;
	HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");

	if (hMapFile)
	{
		LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') && (pMem->dwVersion >= 0x00020000))
			{
				for (DWORD dwEntry = 0; dwEntry < pMem->dwAppArrSize; dwEntry++)
				{
					RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY pEntry = (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY)((LPBYTE)pMem + pMem->dwAppArrOffset + dwEntry * pMem->dwAppEntrySize);

					if (pEntry->dwProcessID)
					{
						if (pEntry->dwProcessID == processId)
						{
							currentFramerate = pEntry->dwStatFrameTimeBufFramerate / 10.0f;

							// Derive the frametime from the same statistical framerate that the
							// framerate value and the framerate/frametime graphs are based on.
							// Previously the frametime was read from the last single sample in
							// dwStatFrameTimeBuf, which made the frametime text value jitter and
							// read several ms higher than both the frametime graph and the
							// 1000 / FPS value (see issue #394). Using the reciprocal of the
							// framerate keeps the frametime text consistent with the FPS value
							// and the graph (frametime text == 1000 / framerate text).
							currentFrametime = currentFramerate > 0.0f ? 1000.0f / currentFramerate : 0.0f;

							break;
						}
					}
				}
			}
			UnmapViewOfFile(pMapAddr);
		}
		CloseHandle(hMapFile);
	}

	result.push_back(currentFramerate);
	result.push_back(currentFrametime);
	return result;
}

std::vector<float> RTSSCoreControl::GetFrameTimesInterval(DWORD processId, DWORD milliSeconds)
{
	std::vector<float> frameTimes;
	DWORD microseconds = 1000 * milliSeconds;
	LPDWORD lpdwProcessiD = 0;
	HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");

	if (hMapFile)
	{
		LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') && (pMem->dwVersion >= 0x00020000))
			{
				for (DWORD dwEntry = 0; dwEntry < pMem->dwAppArrSize; dwEntry++)
				{
					RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY pEntry = (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY)((LPBYTE)pMem + pMem->dwAppArrOffset + dwEntry * pMem->dwAppEntrySize);

					if (pEntry->dwProcessID)
					{
						if (pEntry->dwProcessID == processId)
						{
							// & 1023 enforces upper limit = 1023 = max index
							DWORD frameTimePos = pEntry->dwStatFrameTimeBufPos;
							DWORD frametimeSum = 0;

							for (DWORD i = frameTimePos; i >= 0; i--)
							{
								frametimeSum += pEntry->dwStatFrameTimeBuf[i & 1023];

								if (frametimeSum < microseconds)
								{
									frameTimes.push_back(pEntry->dwStatFrameTimeBuf[i & 1023] / 1000.0f);
								}
								else
								{
									break;
								}
							}

							break;
						}
					}
				}
			}
			UnmapViewOfFile(pMapAddr);
		}
		CloseHandle(hMapFile);
	}

	std::reverse(frameTimes.begin(), frameTimes.end());
	return frameTimes;
}

// lpOSDEntrySize optionally receives the running server's OSD entry stride, which decides
// together with the version which text slot may be written (see GetOSDTextSlot).
DWORD RTSSCoreControl::GetSharedMemoryVersion(DWORD* lpOSDEntrySize)
{
	DWORD dwResult = 0;

	if (lpOSDEntrySize)
		*lpOSDEntrySize = 0;

	HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");

	if (hMapFile)
	{
		LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') &&
				(pMem->dwVersion >= 0x00020000))
			{
				dwResult = pMem->dwVersion;

				if (lpOSDEntrySize)
					*lpOSDEntrySize = pMem->dwOSDEntrySize;
			}

			UnmapViewOfFile(pMapAddr);
		}

		CloseHandle(hMapFile);
	}

	return dwResult;
}

DWORD RTSSCoreControl::EmbedGraph(DWORD dwOffset, FLOAT* lpBuffer, DWORD dwBufferPos, DWORD dwBufferSize, LONG dwWidth, LONG dwHeight, LONG dwMargin, FLOAT fltMin, FLOAT fltMax, DWORD dwFlags)
{
	DWORD dwResult = 0;
	HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");

	if (hMapFile)
	{
		LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') &&
				(pMem->dwVersion >= 0x00020000))
			{
				for (DWORD dwPass = 0; dwPass < 2; dwPass++)
					//1st pass : find previously captured OSD slot
					//2nd pass : otherwise find the first unused OSD slot and capture it
				{
					for (DWORD dwEntry = 1; dwEntry < pMem->dwOSDArrSize; dwEntry++)
						//allow primary OSD clients (i.e. EVGA Precision / MSI Afterburner) to use the first slot exclusively, so third party
						//applications start scanning the slots from the second one
					{
						RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY pEntry = (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY)((LPBYTE)pMem + pMem->dwOSDArrOffset + dwEntry * pMem->dwOSDEntrySize);

						if (dwPass)
						{
							// CapFrameX
							if (!strlen(pEntry->szOSDOwner))
								strcpy_s(pEntry->szOSDOwner, sizeof(pEntry->szOSDOwner), "CapFrameX");
						}

						if (!strcmp(pEntry->szOSDOwner, "CapFrameX"))
						{
							if (pMem->dwVersion >= 0x0002000c)
								//embedded graphs are supported for v2.12 and higher shared memory
							{
								if (dwOffset + sizeof(RTSS_EMBEDDED_OBJECT_GRAPH) + dwBufferSize * sizeof(FLOAT) > sizeof(pEntry->buffer))
									//validate embedded object offset and size and ensure that we don't overrun the buffer
								{
									UnmapViewOfFile(pMapAddr);
									CloseHandle(hMapFile);

									return 0;
								}

								LPRTSS_EMBEDDED_OBJECT_GRAPH lpGraph = (LPRTSS_EMBEDDED_OBJECT_GRAPH)(pEntry->buffer + dwOffset);
								//get pointer to object in buffer

								lpGraph->header.dwSignature = RTSS_EMBEDDED_OBJECT_GRAPH_SIGNATURE;
								lpGraph->header.dwSize = sizeof(RTSS_EMBEDDED_OBJECT_GRAPH) + dwBufferSize * sizeof(FLOAT);
								lpGraph->header.dwWidth = dwWidth;
								lpGraph->header.dwHeight = dwHeight;
								lpGraph->header.dwMargin = dwMargin;
								lpGraph->dwFlags = dwFlags;
								lpGraph->fltMin = fltMin;
								lpGraph->fltMax = fltMax;
								lpGraph->dwDataCount = dwBufferSize;

								if (lpBuffer && dwBufferSize)
								{
									for (DWORD dwPos = 0; dwPos < dwBufferSize; dwPos++)
									{
										FLOAT fltData = lpBuffer[dwBufferPos];

										lpGraph->fltData[dwPos] = (fltData == FLT_MAX) ? 0 : fltData;

										dwBufferPos = (dwBufferPos + 1) & (dwBufferSize - 1);
									}
								}

								dwResult = lpGraph->header.dwSize;
							}

							break;
						}
					}

					if (dwResult)
						break;
				}
			}
			UnmapViewOfFile(pMapAddr);
		}
		CloseHandle(hMapFile);
	}

	return dwResult;
}

// C4793: _interlockedbittestandset below is a compiler intrinsic with no MSIL equivalent, so the
// compiler emits this whole function as native code and reports that it did. That is required, not
// incidental - the bit it sets is RTSS's shared-memory OSD lock, which is only correct if the test
// and the set are one atomic instruction. Silenced here because there is nothing to repair.
#pragma warning(push)
#pragma warning(disable : 4793)
BOOL RTSSCoreControl::UpdateOSD(LPCSTR lpText)
{
	BOOL bResult = FALSE;
	HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");

	if (hMapFile)
	{
		LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') &&
				(pMem->dwVersion >= 0x00020000))
			{
				for (DWORD dwPass = 0; dwPass < 2; dwPass++)
					//1st pass : find previously captured OSD slot
					//2nd pass : otherwise find the first unused OSD slot and capture it
				{
					for (DWORD dwEntry = 1; dwEntry < pMem->dwOSDArrSize; dwEntry++)
						//allow primary OSD clients (i.e. EVGA Precision / MSI Afterburner) to use the first slot exclusively, so third party
						//applications start scanning the slots from the second one
					{
						RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY pEntry = (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY)((LPBYTE)pMem + pMem->dwOSDArrOffset + dwEntry * pMem->dwOSDEntrySize);

						if (dwPass)
						{
							if (!strlen(pEntry->szOSDOwner))
								strcpy_s(pEntry->szOSDOwner, sizeof(pEntry->szOSDOwner), "CapFrameX");
						}

						if (!strcmp(pEntry->szOSDOwner, "CapFrameX"))
						{
							//write the largest text slot this server offers: szOSDEx2 (32768 symbols) on
							//v2.20 and higher, szOSDEx (4096) on v2.7, szOSD (256) otherwise. Must agree
							//with the capacity Refresh() budgeted the text for.
							const EOSDTextSlot osdSlot = GetOSDTextSlot(pMem->dwVersion, pMem->dwOSDEntrySize);

							if (pMem->dwVersion >= 0x0002000e)
								//OSD locking is supported on v2.14 and higher shared memory
							{
								DWORD dwBusy = _interlockedbittestandset(&pMem->dwBusy, 0);
								//bit 0 of this variable will be set if OSD is locked by renderer and cannot be refreshed
								//at the moment

								if (!dwBusy)
								{
									WriteOSDText(pEntry, osdSlot, lpText);

									pMem->dwBusy = 0;
								}
							}
							else
								WriteOSDText(pEntry, osdSlot, lpText);

							pMem->dwOSDFrame++;

							bResult = TRUE;

							break;
						}
					}

					if (bResult)
						break;
				}
			}
			UnmapViewOfFile(pMapAddr);
		}
		CloseHandle(hMapFile);
	}

	return bResult;
}
#pragma warning(pop)

void RTSSCoreControl::ReleaseOSD()
{
	HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");

	if (hMapFile)
	{
		LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') &&
				(pMem->dwVersion >= 0x00020000))
			{
				for (DWORD dwEntry = 1; dwEntry < pMem->dwOSDArrSize; dwEntry++)
				{
					RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY pEntry
						= (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY)((LPBYTE)pMem + pMem->dwOSDArrOffset + dwEntry * pMem->dwOSDEntrySize);

					if (!strcmp(pEntry->szOSDOwner, "CapFrameX"))
					{
						memset(pEntry, 0, pMem->dwOSDEntrySize);
						pMem->dwOSDFrame++;
					}
				}
			}
			UnmapViewOfFile(pMapAddr);
		}
		CloseHandle(hMapFile);
	}
}

DWORD RTSSCoreControl::GetClientsNum()
{
	DWORD dwClients = 0;
	HANDLE hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");

	if (hMapFile)
	{
		LPVOID pMapAddr = MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') &&
				(pMem->dwVersion >= 0x00020000))
			{
				for (DWORD dwEntry = 0; dwEntry < pMem->dwOSDArrSize; dwEntry++)
				{
					RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY pEntry = (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY)((LPBYTE)pMem + pMem->dwOSDArrOffset + dwEntry * pMem->dwOSDEntrySize);

					if (strlen(pEntry->szOSDOwner))
						dwClients++;
				}
			}
			UnmapViewOfFile(pMapAddr);
		}
		CloseHandle(hMapFile);
	}

	return dwClients;
}

void RTSSCoreControl::Refresh()
{
	//init RivaTuner Statistics Server installation path
	if (m_strInstallPath.IsEmpty())
	{
		HKEY hKey;

		if (ERROR_SUCCESS == RegOpenKey(HKEY_LOCAL_MACHINE, "Software\\Unwinder\\RTSS", &hKey))
		{
			char buf[MAX_PATH];

			DWORD dwSize = MAX_PATH;
			DWORD dwType;

			if (ERROR_SUCCESS == RegQueryValueEx(hKey, "InstallPath", 0, &dwType, (LPBYTE)buf, &dwSize))
			{
				if (dwType == REG_SZ)
					m_strInstallPath = buf;
			}

			RegCloseKey(hKey);
		}
	}

	//validate RivaTuner Statistics Server installation path
	if (_taccess(m_strInstallPath, 0))
		m_strInstallPath = "";

	//init profile interface 
	if (!m_strInstallPath.IsEmpty())
	{
		if (!m_profileInterface.IsInitialized())
			m_profileInterface.Init(m_strInstallPath);
	}

	//init shared memory version and OSD entry stride
	DWORD dwOSDEntrySize = 0;
	DWORD dwSharedMemoryVersion = GetSharedMemoryVersion(&dwOSDEntrySize);

	//init max OSD text size from the largest text slot this server offers: 32768 symbols on
	//shared memory v2.20 and higher (szOSDEx2), 4096 on v2.7 (szOSDEx), 256 otherwise
	DWORD dwMaxTextSize = GetOSDTextCapacity(GetOSDTextSlot(dwSharedMemoryVersion, dwOSDEntrySize));

	// RivaTuner based products use similar CGroupedString object for convenient OSD text formatting and length control
	// You may use it to format your OSD similar to RivaTuner's one or just use your own routines to format OSD text

	//text format tags are supported for shared memory v2.11 and higher
	BOOL bFormatTagsSupported = (dwSharedMemoryVersion >= 0x0002000b);
	//embedded object tags are supporoted for shared memory v2.12 and higher
	BOOL bObjTagsSupported = (dwSharedMemoryVersion >= 0x0002000c);

	CString strOSD;

	if (bFormatTagsSupported && m_bFormatTags)
	{
		if (OSDCustomPosition)
		{
			std::string posString = "<P=" + std::to_string(OverlayPositionX) + "," + std::to_string(OverlayPositionY) + ">";
			CString posCString(posString.c_str());
			strOSD += posCString;
		}


		// add format variables
		if (!m_formatVariables.IsEmpty())
		{
			strOSD += m_formatVariables;
		}

		//Note: take a note that position is specified in absolute coordinates so use this tag with caution because your text may
		//overlap with text slots displayed by other applications, so in this demo we explicitly disable this tag usage if more than
		//one client is currently rendering something in OSD
		//move to position 0,0 (in zoomed pixel units)

		//strOSD += "<A0=-5>";
		////define align variable A[0] as right alignment by 5 symbols (positive is left, negative is right)
		//strOSD += "<A1=4>";
		////define align variable A[1] as left alignment by 4 symbols (positive is left, negative is right)
		//strOSD += "<C0=FFA0A0>";
		strOSD += "<S1=75>"; //Graph Text Size
		////define color variable C[0] as R=FF,G=A0 and B=A0
		strOSD += "<C100=AEEA00>"; //CX Green
		//define color variable C[1] as R=FF,G=00 and B=A0
		strOSD += "<C200=FFFFFF>"; // White
		////define color variable C[1] as R=FF,G=FF and B=FF
		// CX blue
		strOSD += "<C250=0271F9>"; //CX Blue
		////define color variable C[1] as R=FF,G=FF and B=FF
		//// CX orange
		//strOSD += "<C4=F17D20>"; //CX Orange
		////define color variable C[1] as R=FF,G=FF and B=FF

		//add \r just for this demo to make tagged text more readable in demo preview window, OSD ignores \r anyway
		strOSD += "\r";

		//Note: we could apply explicit alignment,size and color definitions when necerrary (e.g. <C=FFFFFF>, however
		//variables usage makes tagged text more compact and readable
	}
	else
		strOSD = "";

	//The entry text shares the slot with the format-variable header written above and with the
	//graph section appended below, so budget the grouped string for what is actually left. The
	//graphs are appended LAST and were therefore the first thing the slot limit cut off; keeping
	//their markup reserved means a long entry list drops a sensor row instead of a whole graph.
	//A slot too small to hold even the reserve (the legacy 256 byte one) gives the text priority.
	const int nGraphReserve = 512;
	int nGroupedStringMaxLen = (int)dwMaxTextSize - 1 - strOSD.GetLength();
	if (nGroupedStringMaxLen - nGraphReserve >= 1)
		nGroupedStringMaxLen -= nGraphReserve;
	if (nGroupedStringMaxLen < 1)
		nGroupedStringMaxLen = 1;

	CGroupedString groupedString(nGroupedStringMaxLen);

	if (OverlayEntries.size() > 0)
	{
		for (size_t i = 0; i < OverlayEntries.size(); i++)
		{
			AddOverlayEntry(&groupedString, &OverlayEntries[i], bFormatTagsSupported);
		}
	}

	BOOL bTruncated = FALSE;
	strOSD += groupedString.Get(bTruncated, FALSE, m_bFormatTags ? "\t" : " \t: ");

	// manage graphs
	if (OverlayEntries.size() > 0)
	{
		bool hasAnyGraphToShow = false;
		for (size_t i = 0; i < OverlayEntries.size(); i++)
		{
			if (OverlayEntries[i].ShowGraph)
			{
				hasAnyGraphToShow = true;
				break;
			}
		}

		if (hasAnyGraphToShow)
			strOSD += "\n\n";

		DWORD dwObjectOffset = 0;
		DWORD dwObjectSize = 0;
		DWORD dwFlags = 0;
		CString strObj;

		for (size_t i = 0; i < OverlayEntries.size(); i++)
		{
			if (OverlayEntries[i].ShowGraph)
			{
				if (OverlayEntries[i].Identifier == "Framerate")
				{
					// set graph name

					int indexStart = OverlayEntries[i].GroupName.Find('C') - 1;
					int indexEnd = (indexStart >= 0) ? OverlayEntries[i].GroupName.Find('>', indexStart) : -1;
					CString color = (indexStart >= 0 && indexEnd > indexStart)
						? OverlayEntries[i].GroupName.Mid(indexStart, indexEnd + 1 - indexStart)
						: CString("<C>");
					CString string;
					string.Format("%s<S1>Framerate\n<S><C>", (LPCSTR)color);
					strOSD += string;

					//embed framerate graph object into the buffer
					dwObjectSize = EmbedGraph(dwObjectOffset, NULL, 0, 0, -32, -2, 1, 0.0f, 200.0f, dwFlags | RTSS_EMBEDDED_OBJECT_GRAPH_FLAG_FRAMERATE);

					if (dwObjectSize)
					{
						int indexStart = OverlayEntries[i].Value.Find('C') - 1;
						int indexEnd = (indexStart >= 0) ? OverlayEntries[i].Value.Find('>', indexStart) : -1;
						CString color = (indexStart >= 0 && indexEnd > indexStart)
							? OverlayEntries[i].Value.Mid(indexStart, indexEnd + 1 - indexStart)
							: CString("<C>");

						strObj.Format("%s<OBJ=%08X><A0><S1><FR><A> FPS<S><C>\n", (LPCSTR)color, dwObjectOffset);
						//print embedded object
						strOSD += strObj;
						//modify object offset
						dwObjectOffset += dwObjectSize;
					}
				}
				else if (OverlayEntries[i].Identifier == "Frametime")
				{
					// set graph name

					int indexStart = OverlayEntries[i].GroupName.Find('C') - 1;
					int indexEnd = (indexStart >= 0) ? OverlayEntries[i].GroupName.Find('>', indexStart) : -1;
					CString color = (indexStart >= 0 && indexEnd > indexStart)
						? OverlayEntries[i].GroupName.Mid(indexStart, indexEnd + 1 - indexStart)
						: CString("<C>");
					CString string;
					string.Format("%s<S1>Frametime\n<S><C>", (LPCSTR)color);
					strOSD += string;

					//embed frametime graph object into the buffer
					dwObjectSize = EmbedGraph(dwObjectOffset, NULL, 0, 0, -32, -2, 1, 0.0f, 50000.0f, dwFlags | RTSS_EMBEDDED_OBJECT_GRAPH_FLAG_FRAMETIME);

					if (dwObjectSize)
					{
						int indexStart = OverlayEntries[i].Value.Find('C') - 1;
						int indexEnd = (indexStart >= 0) ? OverlayEntries[i].Value.Find('>', indexStart) : -1;
						CString color = (indexStart >= 0 && indexEnd > indexStart)
							? OverlayEntries[i].Value.Mid(indexStart, indexEnd + 1 - indexStart)
							: CString("<C>");

						// Show CapFrameX's own frametime value (consistent with the FPS value
						// and the frametime text entry) next to the graph instead of RTSS' <FT>
						// macro, which reflects the noisy last single frame and reads several ms
						// higher than the FPS / 1000 value (see issue #394). The "ms" unit is
						// appended separately so the label mirrors the framerate graph label
						// style ("8.3 ms" like "120 FPS").
						CString frametimeValue = StripOSDFormatTags(OverlayEntries[i].Value);
						if (frametimeValue.GetLength() >= 2 && frametimeValue.Right(2) == "ms")
						{
							frametimeValue = frametimeValue.Left(frametimeValue.GetLength() - 2);
							frametimeValue.Trim();
						}
						strObj.Format("%s<OBJ=%08X><A0><S1>%s<A> ms<S><C>\n", (LPCSTR)color, dwObjectOffset, (LPCSTR)frametimeValue);
						//print embedded object
						strOSD += strObj;
						//modify object offset
						dwObjectOffset += dwObjectSize;
					}
				}
			}
		}
	}

	if (!strOSD.IsEmpty())
	{
		BOOL bResult = UpdateOSD(strOSD);
		m_bConnected = bResult;
	}
}

void RTSSCoreControl::SetFormatVariables(CString variables)
{
	m_formatVariables = variables;
}

void RTSSCoreControl::OnOSDOn()
{
	m_rtssInterface.SetFlags(~RTSSHOOKSFLAG_OSD_VISIBLE, RTSSHOOKSFLAG_OSD_VISIBLE);
}

void RTSSCoreControl::OnOSDOff()
{
	m_rtssInterface.SetFlags(~RTSSHOOKSFLAG_OSD_VISIBLE, 0);
}

void RTSSCoreControl::OnOSDToggle()
{
	m_rtssInterface.SetFlags(0xFFFFFFFF, RTSSHOOKSFLAG_OSD_VISIBLE);
}

void RTSSCoreControl::AddOverlayEntry(CGroupedString* groupedString, OverlayEntry* entry, BOOL bFormatTagsSupported)
{
	// handle special cases first
	// ToDo: When more special cases, better use switch-case with string/index mapping table
	if (entry->Identifier == "RunHistory")
	{
		if (entry->ShowOnOverlay && ShowRunHistory)
		{
			for (int i = 0; i < RunHistory.size(); i++)
			{
				CString strGroup;
				strGroup.Format("<C200>Run %d: <C>", i + 1);

				if (RunHistoryOutlierFlags.size() == RunHistory.size())
				{
					if (!RunHistoryOutlierFlags[i])
						groupedString->Add("<C250> " + RunHistory[i] + "<C>", strGroup, "\n");
					else
						groupedString->Add("<C=C80000> " + RunHistory[i] + "<C>", strGroup, "\n");
				}
				else
				{
					groupedString->Add("<C250> " + RunHistory[i] + "<C>", strGroup, "\n");
				}
			}

			// add aggregation
			if (RunHistoryAggregation != "")
			{
				groupedString->Add("<C250> " + RunHistoryAggregation + "<C>", "<C200>Result: <C>", "\n");
			}
		}
	}
	else if (entry->Identifier == "CaptureServiceStatus")
	{
		if (entry->ShowOnOverlay)
		{
			CString groupName = entry->GroupName;

			if (groupName != "")
			{
				groupedString->Add(entry->Value, groupName, "\n", " ");
			}
			else
			{
				groupedString->Add(entry->Value, "", "\n", " ");
			}
		}
	}
	else if (entry->Identifier == "CaptureTimer")
	{
		if (entry->ShowOnOverlay && IsCaptureTimerActive)
		{
			CString groupName = entry->GroupName;

			if (groupName != "")
			{
				groupedString->Add(entry->Value, groupName, "\n", " ");
			}
			else
			{
				groupedString->Add(entry->Value, "", "\n", " ");
			}
		}
	}
	else if (entry->Identifier == "Framerate")
	{
		if (entry->ShowOnOverlay)
		{
			if (bFormatTagsSupported && m_bFormatTags)
			{
				groupedString->Add(entry->Value, entry->GroupName, "\n", m_bFormatTags ? " " : ", ");
				//print application-specific 3D API, framerate and frametime using tags
			}
			else
			{
				groupedString->Add("%FRAMERATE%", "", "\n");
				//print application-specific 3D API, framerate and frametime using deprecated macro
			}
		}
	}
	else if (entry->Identifier == "Frametime")
	{
		if (entry->ShowOnOverlay)
		{
			if (bFormatTagsSupported && m_bFormatTags)
			{
				groupedString->Add(entry->Value, entry->GroupName, "\n", m_bFormatTags ? " " : ", ");
				//print application-specific 3D API, framerate and frametime using tags
			}
			else
			{
				groupedString->Add("%FRAMETIME%", "", "\n");
				//print application-specific 3D API, framerate and frametime using deprecated macro
			}
		}
	}
	else
	{
		if (entry->ShowOnOverlay)
		{
			CString groupName = entry->GroupName;

			if (groupName != "")
			{
				groupedString->Add(entry->Value, groupName, "\n", " ");
			}
			else
			{
				groupedString->Add(entry->Value, "", "\n", " ");
			}
		}
	}
}

void RTSSCoreControl::IncProfileProperty(LPCSTR lpProfile, LPCSTR lpProfileProperty, LONG dwIncrement)
{
	if (m_profileInterface.IsInitialized())
	{
		m_profileInterface.LoadProfile(lpProfile);

		LONG dwProperty = 0;

		if (m_profileInterface.GetProfileProperty(lpProfileProperty, (LPBYTE)&dwProperty, sizeof(dwProperty)))
		{
			dwProperty += dwIncrement;

			m_profileInterface.SetProfileProperty(lpProfileProperty, (LPBYTE)&dwProperty, sizeof(dwProperty));
			m_profileInterface.SaveProfile(lpProfile);
			m_profileInterface.UpdateProfiles();
		}
	}
}

void RTSSCoreControl::SetProfileProperty(LPCSTR lpProfile, LPCSTR lpProfileProperty, DWORD dwProperty)
{
	if (m_profileInterface.IsInitialized())
	{
		m_profileInterface.LoadProfile(lpProfile);
		m_profileInterface.SetProfileProperty(lpProfileProperty, (LPBYTE)&dwProperty, sizeof(dwProperty));
		m_profileInterface.SaveProfile(lpProfile);
		m_profileInterface.UpdateProfiles();
	}
}
