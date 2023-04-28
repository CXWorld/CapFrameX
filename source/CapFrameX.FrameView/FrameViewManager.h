#pragma once
#define FrameView_API __declspec(dllimport)

namespace FrameView
{
	extern "C" FrameView_API bool IntializeFrameViewSession();

	extern "C" FrameView_API bool CloseFrameViewSession();

	extern "C" FrameView_API double GetAveragePcl(const int32_t pid);
}