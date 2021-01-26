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

#ifdef _DEBUG
#define new DEBUG_NEW
#undef THIS_FILE
static char THIS_FILE[] = __FILE__;
#endif

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
}

RTSSCoreControl::~RTSSCoreControl() { }

BOOL RTSSCoreControl::IsProcessDetected(DWORD processId)
{
	BOOL isProcessDetected = false;

	if (m_hMapFile)
	{
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)m_pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') && (pMem->dwVersion >= 0x00020000))
			{
				for (DWORD dwEntry = 0; dwEntry < pMem->dwAppArrSize; dwEntry++)
				{
					RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY pEntry = (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY)((LPBYTE)pMem + pMem->dwAppArrOffset + dwEntry * pMem->dwAppEntrySize);

					if (pEntry->dwProcessID == processId)
					{
						isProcessDetected = true;
						break;
					}
				}
			}
		}
	}

	return isProcessDetected;
}

CString RTSSCoreControl::GetApiInfo(DWORD processId)
{
	CString api = "unknown";

	if (m_hMapFile)
	{
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)m_pMapAddr;

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
			}
		}
	}

	return api;
}

std::vector<float> RTSSCoreControl::GetCurrentFramerate(DWORD processId)
{
	std::vector<float> result;
	float currentFramerate = 0;
	float currentFrametime = 0;
	LPDWORD lpdwProcessiD = 0;

	if (m_hMapFile)
	{
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)m_pMapAddr;

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
							currentFrametime = pEntry->dwStatFrameTimeBuf[(pEntry->dwStatFrameTimeBufPos - 1) & 1023] / 1000.0f;
							currentFramerate = pEntry->dwStatFrameTimeBufFramerate / 10.0f;
							break;
						}
					}
				}
			}
		}
	}

	result.push_back(currentFramerate);
	result.push_back(currentFrametime);
	return result;
}

std::vector<float> RTSSCoreControl::GetCurrentFramerateFromForegroundWindow()
{
	std::vector<float> result;
	float currentFramerate = 0;
	float currentFrametime = 0;
	LPDWORD lpdwProcessiD = 0;

	if (m_hMapFile)
	{
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)m_pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') && (pMem->dwVersion >= 0x00020000))
			{
				HWND	hWnd = GetForegroundWindow();
				DWORD	dwProcessID = 0;

				if (hWnd)
					GetWindowThreadProcessId(hWnd, &dwProcessID);

				for (DWORD dwEntry = 0; dwEntry < pMem->dwAppArrSize; dwEntry++)
				{
					RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY pEntry = (RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_APP_ENTRY)((LPBYTE)pMem + pMem->dwAppArrOffset + dwEntry * pMem->dwAppEntrySize);

					if (pEntry->dwProcessID)
					{
						if (pEntry->dwProcessID == dwProcessID)
						{
							currentFrametime = pEntry->dwStatFrameTimeBuf[(pEntry->dwStatFrameTimeBufPos - 1) & 1023] / 1000.0f;
							currentFramerate = pEntry->dwStatFrameTimeBufFramerate / 10.0f;
						}

						if ((dwProcessID == pEntry->dwProcessID) || !dwProcessID)
							break;
					}
				}
			}
		}
	}

	result.push_back(currentFramerate);
	result.push_back(currentFrametime);
	return result;
}

DWORD RTSSCoreControl::GetSharedMemoryVersion()
{
	DWORD dwResult = 0;

	if (m_hMapFile)
	{
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)m_pMapAddr;

		if (pMem)
		{
			if ((pMem->dwSignature == 'RTSS') &&
				(pMem->dwVersion >= 0x00020000))
				dwResult = pMem->dwVersion;
		}
	}

	return dwResult;
}

DWORD RTSSCoreControl::EmbedGraph(DWORD dwOffset, FLOAT* lpBuffer, DWORD dwBufferPos, DWORD dwBufferSize, LONG dwWidth, LONG dwHeight, LONG dwMargin, FLOAT fltMin, FLOAT fltMax, DWORD dwFlags)
{
	DWORD dwResult = 0;

	if (m_hMapFile)
	{
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)m_pMapAddr;

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
		}
	}

	return dwResult;
}

BOOL RTSSCoreControl::UpdateOSD(LPCSTR lpText)
{
	BOOL bResult = FALSE;

	if (m_hMapFile)
	{
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)m_pMapAddr;

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
						RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY pEntry =
							(RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY)((LPBYTE)pMem + pMem->dwOSDArrOffset + dwEntry * pMem->dwOSDEntrySize);

						if (dwPass)
						{
							if (!strlen(pEntry->szOSDOwner))
								strcpy_s(pEntry->szOSDOwner, sizeof(pEntry->szOSDOwner), "CapFrameX");
						}

						if (!strcmp(pEntry->szOSDOwner, "CapFrameX"))
						{
							if (pMem->dwVersion >= 0x00020007)
								//use extended text slot for v2.7 and higher shared memory, it allows displaying 4096 symbols
								//instead of 256 for regular text slot
							{
								if (pMem->dwVersion >= 0x0002000e)
									//OSD locking is supported on v2.14 and higher shared memory
								{
									DWORD dwBusy = _interlockedbittestandset(&pMem->dwBusy, 0);
									//bit 0 of this variable will be set if OSD is locked by renderer and cannot be refreshed
									//at the moment

									if (!dwBusy)
									{
										strncpy_s(pEntry->szOSDEx, sizeof(pEntry->szOSDEx), lpText, sizeof(pEntry->szOSDEx) - 1);

										pMem->dwBusy = 0;
									}
								}
								else
									strncpy_s(pEntry->szOSDEx, sizeof(pEntry->szOSDEx), lpText, sizeof(pEntry->szOSDEx) - 1);

							}
							else
								strncpy_s(pEntry->szOSD, sizeof(pEntry->szOSD), lpText, sizeof(pEntry->szOSD) - 1);

							pMem->dwOSDFrame++;

							bResult = TRUE;

							break;
						}
					}

					if (bResult)
						break;
				}
			}
		}
	}

	return bResult;
}

BOOL RTSSCoreControl::IsOSDLocked()
{
	BOOL bResult = FALSE;
	BOOL islocked = FALSE;

	if (m_hMapFile)
	{
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)m_pMapAddr;

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
						RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY pEntry =
							(RTSS_SHARED_MEMORY::LPRTSS_SHARED_MEMORY_OSD_ENTRY)((LPBYTE)pMem + pMem->dwOSDArrOffset + dwEntry * pMem->dwOSDEntrySize);

						if (dwPass)
						{
							if (!strlen(pEntry->szOSDOwner))
								strcpy_s(pEntry->szOSDOwner, sizeof(pEntry->szOSDOwner), "CapFrameX");
						}

						if (!strcmp(pEntry->szOSDOwner, "CapFrameX"))
						{
							if (pMem->dwVersion >= 0x00020007)
								//use extended text slot for v2.7 and higher shared memory, it allows displaying 4096 symbols
								//instead of 256 for regular text slot
							{
								if (pMem->dwVersion >= 0x0002000e)
									//OSD locking is supported on v2.14 and higher shared memory
								{
									DWORD dwBusy = _interlockedbittestandset(&pMem->dwBusy, 0);
									//bit 0 of this variable will be set if OSD is locked by renderer and cannot be refreshed
									//at the moment
									islocked = dwBusy == 0;
								}
							}

							pMem->dwOSDFrame++;
							bResult = TRUE;
							break;
						}
					}

					if (bResult)
						break;
				}
			}
		}
	}

	return islocked;
}

void RTSSCoreControl::ReleaseOSD()
{
	if (m_hMapFile)
	{
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)m_pMapAddr;

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
		}
	}
}

void RTSSCoreControl::CloseHandles()
{
	if (m_hMapFile)
		CloseHandle(m_hMapFile);

	if (m_pMapAddr)
		UnmapViewOfFile(m_pMapAddr);
}

void RTSSCoreControl::CreateHandles()
{
	m_hMapFile = OpenFileMapping(FILE_MAP_ALL_ACCESS, FALSE, "RTSSSharedMemoryV2");
	if (m_hMapFile)
		m_pMapAddr = MapViewOfFile(m_hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, 0);
}

DWORD RTSSCoreControl::GetClientsNum()
{
	DWORD dwClients = 0;

	if (m_hMapFile)
	{
		LPRTSS_SHARED_MEMORY pMem = (LPRTSS_SHARED_MEMORY)m_pMapAddr;

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
		}
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

	//init shared memory version
	DWORD dwSharedMemoryVersion = GetSharedMemoryVersion();

	//init max OSD text size, we'll use extended text slot for v2.7 and higher shared memory, 
	//it allows displaying 4096 symbols /instead of 256 for regular text slot
	DWORD dwMaxTextSize = (dwSharedMemoryVersion >= 0x00020007) ? sizeof(RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_OSD_ENTRY().szOSDEx)
		: sizeof(RTSS_SHARED_MEMORY::RTSS_SHARED_MEMORY_OSD_ENTRY().szOSD);

	CGroupedString groupedString(dwMaxTextSize - 1);
	// RivaTuner based products use similar CGroupedString object for convenient OSD text formatting and length control
	// You may use it to format your OSD similar to RivaTuner's one or just use your own routines to format OSD text

	//text format tags are supported for shared memory v2.11 and higher
	BOOL bFormatTagsSupported = (dwSharedMemoryVersion >= 0x0002000b);
	//embedded object tags are supporoted for shared memory v2.12 and higher
	BOOL bObjTagsSupported = (dwSharedMemoryVersion >= 0x0002000c);

	CString strOSD;

	if (bFormatTagsSupported && m_bFormatTags)
	{
		auto clientCount = GetClientsNum();
		if (clientCount == 1)
			strOSD += "<P=0,0>";
		else if (clientCount > 1)
		{
			// Add CX label
			groupedString.Add("", "<C250>\nCX OSD<C>", "\n", " ");
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
		////define color variable C[0] as R=FF,G=A0 and B=A0
		strOSD += "<C100=AEEA00>"; //CX Green
		//define color variable C[1] as R=FF,G=00 and B=A0
		strOSD += "<C200=FFFFFF>"; // White
		////define color variable C[1] as R=FF,G=FF and B=FF
		// CX blue
		strOSD += "<C250=2297F3>"; //CX Blue
		////define color variable C[1] as R=FF,G=FF and B=FF
		//// CX orange
		//strOSD += "<C4=F17D20>"; //CX Orange
		////define color variable C[1] as R=FF,G=FF and B=FF
		//strOSD += "<S0=-50>";
		////define size variable S[0] as 50% subscript (positive is superscript, negative is subscript)
		strOSD += "<S100=50>";
		////define size variable S[0] as 50% supercript (positive is superscript, negative is subscript)

		//add \r just for this demo to make tagged text more readable in demo preview window, OSD ignores \r anyway
		strOSD += "\r";

		//Note: we could apply explicit alignment,size and color definitions when necerrary (e.g. <C=FFFFFF>, however
		//variables usage makes tagged text more compact and readable
	}
	else
		strOSD = "";

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
					if (OverlayEntries[i].GroupName.Find("<APP>") != std::string::npos)
						strOSD += "<C100><S=50>Framerate\n<S><C>";
					else
						strOSD += OverlayEntries[i].GroupName + "\n";
					//embed framerate graph object into the buffer
					dwObjectSize = EmbedGraph(dwObjectOffset, NULL, 0, 0, -32, -2, 1, 0.0f, 200.0f, dwFlags | RTSS_EMBEDDED_OBJECT_GRAPH_FLAG_FRAMERATE);

					if (dwObjectSize)
					{
						strObj.Format("<C200><OBJ=%08X><A0><S100><FR><A> FPS<S><C>\n", dwObjectOffset);
						//print embedded object
						strOSD += strObj;
						//modify object offset
						dwObjectOffset += dwObjectSize;
					}
				}
				else if (OverlayEntries[i].Identifier == "Frametime")
				{
					// set graph name
					if (OverlayEntries[i].GroupName.Find("<APP>") != std::string::npos)
						strOSD += "<C100><S=50>Frametime\n<S><C>";
					else
						strOSD += OverlayEntries[i].GroupName + "\n";

					//embed frametime graph object into the buffer
					dwObjectSize = EmbedGraph(dwObjectOffset, NULL, 0, 0, -32, -2, 1, 0.0f, 50000.0f, dwFlags | RTSS_EMBEDDED_OBJECT_GRAPH_FLAG_FRAMETIME);

					if (dwObjectSize)
					{
						strObj.Format("<C200><OBJ=%08X><A0><S100><FT><A> ms<S><C>\n", dwObjectOffset);
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
		if (entry->ShowOnOverlay)
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
