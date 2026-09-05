#pragma once

#include <algorithm>

// AMD FLM's adaptive SAD baseline, scoped to one capture session. Freeze the
// estimator while waiting for a click response so the response is not learned
// as background. The coefficient remains user adjustable.
class FlmSadFilter
{
public:
    void Reset()
    {
        m_background = 0;
        m_previous = m_previous2 = m_previous3 = 0;
    }

    int Process(int sad, float coefficient, float alpha, bool updateBaseline = true)
    {
        const int result = std::max(0, sad - static_cast<int>(m_background * coefficient));
        if (updateBaseline)
        {
            sad = std::max(1, sad + m_previous / 4);
            if (sad <= m_previous * coefficient && sad <= m_previous2 * coefficient &&
                sad <= m_previous3 * coefficient)
                m_background = m_background * alpha + (1 - alpha) * sad;
            m_previous3 = m_previous2;
            m_previous2 = m_previous;
            m_previous = sad;
        }
        return result;
    }

    float Background() const { return m_background; }

private:
    float m_background = 0;
    int m_previous = 0;
    int m_previous2 = 0;
    int m_previous3 = 0;
};
