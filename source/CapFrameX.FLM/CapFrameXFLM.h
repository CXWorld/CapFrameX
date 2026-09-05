#pragma once

#include <cstdint>

#ifdef CAPFRAMEXFLM_EXPORTS
#define CAPFRAMEX_FLM_API __declspec(dllexport)
#else
#define CAPFRAMEX_FLM_API __declspec(dllimport)
#endif

enum FlmInteropStatus : int32_t
{
    FLM_INTEROP_OK = 0,
    FLM_INTEROP_NO_SAMPLE = 1,
    FLM_INTEROP_INVALID_ARGUMENT = -1,
    FLM_INTEROP_INITIALIZATION_FAILED = -2,
    FLM_INTEROP_INVALID_STATE = -3,
    FLM_INTEROP_INTERNAL_ERROR = -4
};

struct FlmInteropConfig
{
    uint32_t structSize;
    int32_t  codec;
    int32_t  gameUsesFrameGeneration;
    int32_t  initAmfUsingDx12;
    int32_t  mouseHorizontalStep;
    float    captureStartX;
    float    captureStartY;
    float    captureWidth;
    float    captureHeight;
    float    thresholdCoefficient;
    int32_t  averageFilterFrames;
    int32_t  filmGrainThreshold;
    int32_t  mouseEventType;  // 0 = synthetic mouse move (injects input), 1 = passive click-to-photon
};

struct FlmInteropSample
{
    uint32_t structSize;
    uint32_t flags;
    uint64_t sequence;
    int64_t  inputQpc;
    int64_t  frameQpc;
    float    latencyMs;
    float    latencyFrames;
    float    fps;
    uint32_t reserved;
};

extern "C"
{
    CAPFRAMEX_FLM_API int32_t __cdecl FlmCreate(const FlmInteropConfig* config, void** handle);
    CAPFRAMEX_FLM_API int32_t __cdecl FlmStart(void* handle);
    CAPFRAMEX_FLM_API int32_t __cdecl FlmStop(void* handle);
    CAPFRAMEX_FLM_API int32_t __cdecl FlmTryGetSample(void* handle, FlmInteropSample* sample);
    CAPFRAMEX_FLM_API int32_t __cdecl FlmGetLastError(void* handle, char* buffer, uint32_t bufferSize);
    CAPFRAMEX_FLM_API void __cdecl FlmDestroy(void* handle);
}
