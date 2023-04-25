#pragma once
#include <cstdint>

#define FrameView_API __declspec(dllimport)

extern "C" FrameView_API bool IntializeFrameViewSession();

extern "C" FrameView_API void CloseFrameViewSession();

extern "C" FrameView_API double GetAveragePcl(const uint32_t pid);