#pragma once

#include <cstdint>
#include <mutex>

// Shared by the input poller and capture worker. All timestamps use QPC. A button
// press is consumed even when capture is not ready, so holding it cannot become
// a new, incorrectly timestamped click later.
class FlmPassiveClickTracker
{
public:
    enum class State : int32_t
    {
        WarmingUp = 0, WaitingForClick = 1, WaitingForResponse = 2,
        SceneMoving = 3, NoResponse = 4, Measured = 5, NoFrames = 6
    };

    struct Snapshot
    {
        State state = State::WarmingUp;
        uint64_t clicks = 0;
        uint64_t rejectedClicks = 0;
        uint64_t timeouts = 0;
        uint64_t frames = 0;
        int64_t lastFrameQpc = 0;
    };

    void Reset(int64_t frequency, bool buttonDown, int warmupFrames)
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        m_frequency = frequency;
        m_warmupFrames = warmupFrames;
        m_buttonDown = buttonDown;
        m_pendingClick = 0;
        m_lastMotion = false;
        m_snapshot = {};
    }

    void ObserveButton(bool down, int64_t now, bool enabled)
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        const bool pressed = down && !m_buttonDown;
        m_buttonDown = down;
        if (!enabled)
        {
            m_pendingClick = 0;
            return;
        }
        Expire(now);
        if (!pressed)
            return;

        ++m_snapshot.clicks;
        // Two overlapping clicks cannot be attributed to one screen response.
        if (m_pendingClick != 0 || !HasRecentFrame(now) ||
            m_snapshot.frames < static_cast<uint64_t>(m_warmupFrames) || m_lastMotion)
        {
            m_pendingClick = 0;
            ++m_snapshot.rejectedClicks;
            return;
        }
        m_pendingClick = now;
        m_snapshot.state = State::WaitingForResponse;
    }

    bool ObserveFrame(int64_t frameQpc, int64_t now, bool motion, int64_t& inputQpc)
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        Expire(now);
        if (frameQpc <= m_snapshot.lastFrameQpc || frameQpc > now)
            return false;

        m_snapshot.lastFrameQpc = frameQpc;
        ++m_snapshot.frames;
        m_lastMotion = motion;
        if (m_pendingClick != 0)
        {
            // A delayed capture of a frame presented before the click is not a response.
            if (motion && frameQpc > m_pendingClick)
            {
                inputQpc = m_pendingClick;
                m_pendingClick = 0;
                m_snapshot.state = State::Measured;
                return true;
            }
            return false;
        }

        if (m_snapshot.frames < static_cast<uint64_t>(m_warmupFrames))
            m_snapshot.state = State::WarmingUp;
        else if (m_snapshot.state != State::NoResponse)
            m_snapshot.state = motion ? State::SceneMoving : State::WaitingForClick;
        return false;
    }

    bool HasPendingClick()
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        return m_pendingClick != 0;
    }

    Snapshot GetSnapshot(int64_t now)
    {
        std::lock_guard<std::mutex> lock(m_mutex);
        Expire(now);
        auto snapshot = m_snapshot;
        if (!HasRecentFrame(now))
            snapshot.state = State::NoFrames;
        return snapshot;
    }

private:
    bool HasRecentFrame(int64_t now) const
    {
        return m_snapshot.lastFrameQpc > 0 && now >= m_snapshot.lastFrameQpc &&
            now - m_snapshot.lastFrameQpc <= m_frequency / 4;
    }

    void Expire(int64_t now)
    {
        if (m_pendingClick != 0 && now - m_pendingClick > m_frequency * 3 / 10)
        {
            m_pendingClick = 0;
            ++m_snapshot.timeouts;
            m_snapshot.state = State::NoResponse;
        }
    }

    std::mutex m_mutex;
    Snapshot m_snapshot;
    int64_t m_frequency = 1;
    int64_t m_pendingClick = 0;
    int m_warmupFrames = 100;
    bool m_buttonDown = false;
    bool m_lastMotion = false;
};
