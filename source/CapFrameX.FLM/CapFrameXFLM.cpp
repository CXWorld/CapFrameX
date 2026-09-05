#include "CapFrameXFLM.h"

#include "ThirdParty/FLM/backend/flm_pipeline.h"

#include <timeapi.h>

#include <algorithm>
#include <atomic>
#include <cstring>
#include <cmath>
#include <memory>
#include <mutex>
#include <new>
#include <string>
#include <thread>

#pragma comment(lib, "winmm.lib")

namespace
{
    thread_local std::string g_creationError;

    class FlmSession
    {
    public:
        ~FlmSession()
        {
            Shutdown();
        }

        int32_t Initialize(const FlmInteropConfig& config)
        {
            try
            {
                m_pipeline = std::make_unique<FLM_Pipeline>();
                auto& options = m_pipeline->m_runtimeOptions;
                options.gameUsesFrameGeneration = config.gameUsesFrameGeneration != 0;
                options.initAMFUsingDX12 = config.initAmfUsingDx12 != 0;
                options.mouseHorizontalStep = std::clamp(config.mouseHorizontalStep, 10, 1000);
                options.captureStartX = std::clamp(config.captureStartX, 0.0f, 1.0f);
                options.captureStartY = std::clamp(config.captureStartY, 0.0f, 1.0f);
                options.captureWidth = std::clamp(config.captureWidth, 0.01f, 1.0f);
                options.captureHeight = std::clamp(config.captureHeight, 0.01f, 1.0f);
                options.captureAverageFilterFrames = std::clamp(config.averageFilterFrames, 1, 99999);
                options.captureFilmGrainThreshold = std::clamp(config.filmGrainThreshold, 0, 255);
                options.captureOutputIndex = config.captureOutputIndex;
                options.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE] =
                    std::max(0.1f, config.thresholdCoefficient);
                options.thresholdCoefficient[FLM_MOUSE_EVENT_TYPE::MOUSE_CLICK] =
                    std::max(0.1f, config.thresholdCoefficient);
                options.mouseEventType = config.mouseEventType == 1
                    ? FLM_MOUSE_EVENT_TYPE::MOUSE_CLICK
                    : FLM_MOUSE_EVENT_TYPE::MOUSE_MOVE;

                const auto codec = static_cast<FLM_CAPTURE_CODEC_TYPE>(config.codec);
                const FLM_STATUS status = m_pipeline->Init(codec);
                if (status != FLM_STATUS::OK)
                {
                    SetLastError(DrainBackendError("FLM initialization failed"));
                    m_pipeline->Close();
                    m_pipeline.reset();
                    return FLM_INTEROP_INITIALIZATION_FAILED;
                }

                // The click-mode poll loops sleep 1 ms; without a 1 ms timer period those
                // sleeps can stretch to ~15 ms and delay click detection, which would bias
                // the measured latency low. Scoped to the session lifetime.
                m_timerPeriodActive = timeBeginPeriod(1) == TIMERR_NOERROR;

                return FLM_INTEROP_OK;
            }
            catch (const std::exception& exception)
            {
                SetLastError(exception.what());
                Shutdown();
                return FLM_INTEROP_INTERNAL_ERROR;
            }
            catch (...)
            {
                SetLastError("Unexpected FLM initialization failure");
                Shutdown();
                return FLM_INTEROP_INTERNAL_ERROR;
            }
        }

        int32_t Start()
        {
            std::lock_guard<std::mutex> lock(m_lifecycleMutex);
            if (!m_pipeline || m_shutdownRequested)
                return FLM_INTEROP_INVALID_STATE;

            std::lock_guard<std::mutex> processLock(m_processMutex);
            m_pipeline->StartMeasurements();
            m_measuring = true;
            try
            {
                if (!m_worker.joinable())
                    m_worker = std::thread(&FlmSession::ProcessLoop, this);
            }
            catch (const std::exception& exception)
            {
                m_pipeline->StopMeasurements();
                m_measuring = false;
                SetLastError(exception.what());
                return FLM_INTEROP_INTERNAL_ERROR;
            }
            return FLM_INTEROP_OK;
        }

        int32_t Stop()
        {
            std::lock_guard<std::mutex> lock(m_lifecycleMutex);
            if (!m_pipeline || m_shutdownRequested)
                return FLM_INTEROP_INVALID_STATE;

            std::lock_guard<std::mutex> processLock(m_processMutex);
            m_pipeline->StopMeasurements();
            m_measuring = false;
            return FLM_INTEROP_OK;
        }

        int32_t TryGetSample(FlmInteropSample& output)
        {
            FLM_Pipeline* pipeline = m_pipeline.get();
            if (!pipeline || m_shutdownRequested)
                return FLM_INTEROP_INVALID_STATE;

            FLM_LATENCY_SAMPLE sample;
            if (!pipeline->TryGetLatencySample(sample))
                return FLM_INTEROP_NO_SAMPLE;

            output.structSize = sizeof(FlmInteropSample);
            output.flags = 1;
            output.sequence = sample.sequence;
            output.inputQpc = sample.inputQpc;
            output.frameQpc = sample.frameQpc;
            output.latencyMs = sample.latencyMs;
            output.latencyFrames = sample.latencyFrames;
            output.fps = sample.fps;
            output.reserved = 0;
            return FLM_INTEROP_OK;
        }

        std::string GetLastError()
        {
            std::lock_guard<std::mutex> lock(m_errorMutex);
            if (m_lastError.empty() && m_pipeline)
                m_lastError = DrainBackendError("");
            return m_lastError;
        }

        int32_t GetDiagnostics(FlmInteropDiagnostics& output)
        {
            if (!m_pipeline || m_shutdownRequested)
                return FLM_INTEROP_INVALID_STATE;
            const auto status = m_pipeline->GetClickStatus();
            output.structSize = sizeof(FlmInteropDiagnostics);
            output.state = static_cast<int32_t>(status.state);
            output.clicks = status.clicks;
            output.rejectedClicks = status.rejectedClicks;
            output.timeouts = status.timeouts;
            output.frames = status.frames;
            output.lastFrameQpc = status.lastFrameQpc;
            return FLM_INTEROP_OK;
        }

        void Shutdown()
        {
            {
                std::lock_guard<std::mutex> lock(m_lifecycleMutex);
                if (!m_pipeline)
                    return;

                m_shutdownRequested = true;
                m_pipeline->RequestShutdown();
                std::lock_guard<std::mutex> processLock(m_processMutex);
                if (m_measuring)
                {
                    m_pipeline->StopMeasurements();
                    m_measuring = false;
                }
            }

            if (m_worker.joinable())
                m_worker.join();

            m_pipeline->Close();
            m_pipeline.reset();

            if (m_timerPeriodActive)
            {
                timeEndPeriod(1);
                m_timerPeriodActive = false;
            }
        }

    private:
        void ProcessLoop()
        {
            while (!m_shutdownRequested)
            {
                {
                    std::lock_guard<std::mutex> processLock(m_processMutex);
                    if (m_shutdownRequested)
                        break;
                    try
                    {
                        if (m_pipeline->Process() == FLM_PROCESS_STATUS::CLOSE)
                        {
                            SetLastError(DrainBackendError("FLM processing stopped"));
                            m_shutdownRequested = true;
                            break;
                        }
                    }
                    catch (const std::exception& exception)
                    {
                        SetLastError(exception.what());
                        m_shutdownRequested = true;
                        break;
                    }
                }
                // Give a pending start/stop a chance to acquire the processing lock.
                std::this_thread::yield();
            }
        }

        std::string DrainBackendError(const char* fallback)
        {
            if (!m_pipeline)
                return fallback;

            std::string combined;
            for (std::string error = m_pipeline->GetErrorStr(); !error.empty(); error = m_pipeline->GetErrorStr())
            {
                if (!combined.empty())
                    combined.append("; ");
                combined.append(error);
            }

            return combined.empty() ? fallback : combined;
        }

        void SetLastError(const std::string& error)
        {
            std::lock_guard<std::mutex> lock(m_errorMutex);
            m_lastError = error;
        }

        std::unique_ptr<FLM_Pipeline> m_pipeline;
        std::thread m_worker;
        std::atomic_bool m_shutdownRequested = false;
        bool m_measuring = false;
        bool m_timerPeriodActive = false;
        std::mutex m_lifecycleMutex;
        std::mutex m_processMutex;
        std::mutex m_errorMutex;
        std::string m_lastError;
    };

    bool IsValidConfig(const FlmInteropConfig* config)
    {
        return config != nullptr &&
            config->structSize >= sizeof(FlmInteropConfig) &&
            config->codec >= static_cast<int32_t>(FLM_CAPTURE_CODEC_TYPE::AUTO) &&
            config->codec <= static_cast<int32_t>(FLM_CAPTURE_CODEC_TYPE::DXGI) &&
            config->mouseEventType == 1 &&
            config->captureOutputIndex >= 0 && config->captureOutputIndex < 32 &&
            std::isfinite(config->captureStartX) && std::isfinite(config->captureStartY) &&
            std::isfinite(config->captureWidth) && std::isfinite(config->captureHeight) &&
            std::isfinite(config->thresholdCoefficient) &&
            config->captureStartX >= 0 && config->captureStartY >= 0 &&
            config->captureWidth >= 0.01f && config->captureHeight >= 0.01f &&
            config->captureStartX + config->captureWidth <= 1.000001f &&
            config->captureStartY + config->captureHeight <= 1.000001f &&
            config->thresholdCoefficient >= 1 && config->thresholdCoefficient <= 10;
    }
}

int32_t __cdecl FlmCreate(const FlmInteropConfig* config, void** handle)
{
    if (handle)
        *handle = nullptr;
    if (handle == nullptr || !IsValidConfig(config))
    {
        g_creationError = "Invalid FLM configuration or incompatible struct size (passive click mode required)";
        return FLM_INTEROP_INVALID_ARGUMENT;
    }

    *handle = nullptr;
    g_creationError.clear();

    std::unique_ptr<FlmSession> session(new (std::nothrow) FlmSession());
    if (!session)
        return FLM_INTEROP_INTERNAL_ERROR;

    const int32_t status = session->Initialize(*config);
    if (status != FLM_INTEROP_OK)
    {
        g_creationError = session->GetLastError();
        return status;
    }

    *handle = session.release();
    return FLM_INTEROP_OK;
}

int32_t __cdecl FlmStart(void* handle)
{
    return handle ? static_cast<FlmSession*>(handle)->Start() : FLM_INTEROP_INVALID_ARGUMENT;
}

int32_t __cdecl FlmStop(void* handle)
{
    return handle ? static_cast<FlmSession*>(handle)->Stop() : FLM_INTEROP_INVALID_ARGUMENT;
}

int32_t __cdecl FlmTryGetSample(void* handle, FlmInteropSample* sample)
{
    if (!handle || !sample || sample->structSize < sizeof(FlmInteropSample))
        return FLM_INTEROP_INVALID_ARGUMENT;

    return static_cast<FlmSession*>(handle)->TryGetSample(*sample);
}

int32_t __cdecl FlmGetLastError(void* handle, char* buffer, uint32_t bufferSize)
{
    if (!buffer || bufferSize == 0)
        return FLM_INTEROP_INVALID_ARGUMENT;

    const std::string error = handle
        ? static_cast<FlmSession*>(handle)->GetLastError()
        : g_creationError;
    const size_t charactersToCopy = std::min(error.size(), static_cast<size_t>(bufferSize - 1));
    std::memcpy(buffer, error.data(), charactersToCopy);
    buffer[charactersToCopy] = '\0';
    return FLM_INTEROP_OK;
}

int32_t __cdecl FlmGetDiagnostics(void* handle, FlmInteropDiagnostics* diagnostics)
{
    if (!handle || !diagnostics || diagnostics->structSize < sizeof(FlmInteropDiagnostics))
        return FLM_INTEROP_INVALID_ARGUMENT;
    return static_cast<FlmSession*>(handle)->GetDiagnostics(*diagnostics);
}

void __cdecl FlmDestroy(void* handle)
{
    delete static_cast<FlmSession*>(handle);
}
